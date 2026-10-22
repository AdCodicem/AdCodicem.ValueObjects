using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AdCodicem.ValueObjects.UnitTests.LanguageModels;

/// <summary>
/// A Model Context Protocol server in process, and the SDK's own client connected to it over a pair of pipes: no network
/// and no other process.
/// </summary>
internal sealed class McpHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly CancellationTokenSource _stop;
    private readonly Task _run;

    private McpHarness(ServiceProvider provider, CancellationTokenSource stop, Task run, McpClient client)
    {
        _provider = provider;
        _stop = stop;
        _run = run;
        Client = client;
    }

    /// <summary>Gets the client connected to the server.</summary>
    public McpClient Client { get; }

    /// <summary>Gets the services of the server.</summary>
    public IServiceProvider Services => _provider;

    /// <summary>Gets what the instances of the tool types the server created and disposed.</summary>
    public InvocationLog Log => _provider.GetRequiredService<InvocationLog>();

    /// <summary>
    /// Starts a server configured by the test, with an <see cref="InvocationLog"/> among its services, and connects a
    /// client to it.
    /// </summary>
    /// <param name="configure">Registers the tools and anything else on the server's builder.</param>
    /// <param name="protocolVersion">The protocol version the client asks for, or <see langword="null"/> for the client's
    /// latest, which the SDK's own server rewrites no output schema for.</param>
    /// <param name="services">Registers anything else among the server's services.</param>
    /// <returns>The harness.</returns>
    public static async Task<McpHarness> StartAsync(
        Action<IMcpServerBuilder> configure,
        string? protocolVersion = null,
        Action<IServiceCollection>? services = null)
    {
        Pipe toServer = new(), toClient = new();
        var collection = new ServiceCollection();
        collection.AddSingleton<InvocationLog>();
        configure(collection.AddMcpServer().WithStreamServerTransport(toServer.Reader.AsStream(), toClient.Writer.AsStream()));
        services?.Invoke(collection);

        var provider = collection.BuildServiceProvider();
        var stop = new CancellationTokenSource();
        try
        {
            var run = provider.GetRequiredService<McpServer>().RunAsync(stop.Token);
            var client = await McpClient.CreateAsync(
                new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream()),
                new McpClientOptions { ProtocolVersion = protocolVersion },
                cancellationToken: TestContext.Current.CancellationToken);

            return new McpHarness(provider, stop, run, client);
        }
        catch
        {
            stop.Dispose();
            await provider.DisposeAsync();
            throw;
        }
    }

    /// <summary>Lists the tools the server publishes, as the protocol carries them.</summary>
    /// <returns>The tools, by name.</returns>
    public async Task<IReadOnlyDictionary<string, Tool>> ListAsync()
        => (await Client.ListToolsAsync(new ListToolsRequestParams(), TestContext.Current.CancellationToken)).Tools.ToDictionary(tool => tool.Name);

    /// <summary>
    /// Calls a tool with arguments written as the JSON a client sends, each property one argument, exactly as written.
    /// </summary>
    /// <param name="tool">The name of the tool.</param>
    /// <param name="arguments">A JSON object, or <see langword="null"/> for a call that sends no arguments at all.</param>
    /// <returns>The result of the call.</returns>
    public async Task<CallToolResult> CallAsync(string tool, string? arguments)
        => await Client.CallToolAsync(
            new CallToolRequestParams { Name = tool, Arguments = arguments is null ? null : Arguments(arguments) },
            TestContext.Current.CancellationToken);

    /// <summary>Reads a JSON object into the arguments of a call, each property one argument.</summary>
    public static Dictionary<string, JsonElement> Arguments(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone());
    }

    /// <summary>Writes the text of a result, its blocks joined.</summary>
    public static string TextOf(CallToolResult result)
        => string.Join("|", result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    /// <summary>Writes the sentence a refused argument is answered with.</summary>
    public static string Sentence(string argument, string code, string message) => $"Argument '{argument}' rejected ({code}): {message}";

    /// <summary>
    /// Checks that a result is the error a refused argument is answered with, its sentence alone in the text, and its
    /// structured content the refusal, or none.
    /// </summary>
    public static void ShouldBeRefused(CallToolResult result, string argument, string code, string message, bool structured = true)
    {
        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle().Which.Should().BeOfType<TextContentBlock>().Which.Text.Should().Be(Sentence(argument, code, message));
        if (structured)
        {
            JsonNode.DeepEquals(
                    JsonNode.Parse(result.StructuredContent!.Value.GetRawText()),
                    JsonNode.Parse(LanguageModelTools.Rejection(argument, code, message)))
                .Should().BeTrue("the structured content is {0}", result.StructuredContent!.Value.GetRawText());
        }
        else
        {
            result.StructuredContent.Should().BeNull();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await _stop.CancelAsync();
        try
        {
            await _run;
        }
        catch (OperationCanceledException)
        {
            // The server stops with the token it ran under.
        }

        _stop.Dispose();
        await _provider.DisposeAsync();
    }
}

/// <summary>
/// Counts the instances of the tool types a server created and disposed, which the server's services hold.
/// </summary>
internal sealed class InvocationLog
{
    private int _created;
    private int _disposed;

    public int Created => _created;

    public int Disposed => _disposed;

    public void OnCreated() => Interlocked.Increment(ref _created);

    public void OnDisposed() => Interlocked.Increment(ref _disposed);
}
