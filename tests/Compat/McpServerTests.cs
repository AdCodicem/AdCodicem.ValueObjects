using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdCodicem.ValueObjects.ModelContextProtocol;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// Model Context Protocol tools with AdCodicem.ValueObjects.ModelContextProtocol on the next major, the SDK resolved at
/// the package's floor as an application without central package management resolves it: a server in process and the
/// SDK's client over a pair of pipes, the rules in the tools' schemas, and a refused argument answered with its rule.
/// </summary>
public sealed class McpServerTests
{
    private const string Account = "FR7630006000011234567890189";

    /// <summary>
    /// The schemas a server lists describe each value object a tool takes or returns with the rules declared on it, a
    /// list's items included.
    /// </summary>
    [Fact]
    public async Task The_tool_schemas_carry_the_rules_of_each_value_object()
    {
        await using var server = await Server.StartAsync(protocolVersion: null);
        var tools = (await server.Client.ListToolsAsync(new ListToolsRequestParams(), TestContext.Current.CancellationToken)).Tools.ToDictionary(static tool => tool.Name);

        var input = JsonNode.Parse(tools["place_order"].InputSchema.GetRawText())!["properties"]!;
        input["iban"]!["type"]!.GetValue<string>().Should().Be("string");
        input["iban"]!["minLength"]!.GetValue<int>().Should().Be(15);
        input["iban"]!["pattern"]!.GetValue<string>().Should().Be("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");
        input["quantity"]!["minimum"]!.GetValue<int>().Should().Be(1);
        input["quantity"]!["maximum"]!.GetValue<int>().Should().Be(100);
        input["country"]!["enum"]!.AsArray().Select(static value => value!.GetValue<string>()).Should().Equal("FR", "BE", "LU");
        input["birth"]!["type"]!.AsArray().Select(static value => value!.GetValue<string>()).Should().Equal("string", "null");

        var output = JsonNode.Parse(tools["get_transfer"].OutputSchema!.Value.GetRawText())!["properties"]!;
        output["account"]!["maxLength"]!.GetValue<int>().Should().Be(34);
        output["alternates"]!["items"]!["minLength"]!.GetValue<int>().Should().Be(15);
        output["quantity"]!["maximum"]!.GetValue<int>().Should().Be(100);
    }

    /// <summary>
    /// A refused argument is answered with a tool execution error carrying its rule, never the value sent, and accepted
    /// arguments reach the tool.
    /// </summary>
    [Fact]
    public async Task A_refused_argument_is_answered_with_its_rule()
    {
        await using var server = await Server.StartAsync(protocolVersion: null);

        var refused = await server.CallAsync("place_order", $$"""{"iban":"{{Account}}","quantity":500,"country":"FR"}""");
        var element = await server.CallAsync("book", $$"""{"accounts":["{{Account}}","FR76"]}""");
        var accepted = await server.CallAsync("place_order", $$"""{"iban":"{{Account}}","quantity":3,"country":"fr"}""");

        refused.IsError.Should().BeTrue();
        refused.Content.OfType<TextContentBlock>().Single().Text.Should().Be(
            "Argument 'quantity' rejected (value_object.out_of_range): The value is not a valid Quantity: The value must be less than or equal to 100.");
        refused.StructuredContent!.Value.GetRawText().Should().Be(
            """{"error":"invalid_argument","argument":"quantity","code":"value_object.out_of_range","message":"The value is not a valid Quantity: The value must be less than or equal to 100."}""");
        element.StructuredContent!.Value.GetProperty("code").GetString().Should().Be("value_object.too_short");
        element.StructuredContent!.Value.GetRawText().Should().NotContain("FR76\"");
        accepted.IsError.Should().NotBe(true);
        accepted.Content.OfType<TextContentBlock>().Single().Text.Should().Be($"placed 3 on {Account} in FR");
    }

    /// <summary>
    /// A client on the protocol version the clients in use today ask for gets a value object's output schema wrapped in
    /// an object, its rules kept, and the structured content wrapped likewise: the tools stay the SDK's own.
    /// </summary>
    [Fact]
    public async Task A_client_on_an_older_protocol_gets_the_wrapped_output_schema()
    {
        await using var server = await Server.StartAsync(protocolVersion: "2025-11-25");
        var tools = (await server.Client.ListToolsAsync(new ListToolsRequestParams(), TestContext.Current.CancellationToken)).Tools.ToDictionary(static tool => tool.Name);

        var output = JsonNode.Parse(tools["get_account"].OutputSchema!.Value.GetRawText())!;
        output["type"]!.GetValue<string>().Should().Be("object");
        output["properties"]!["result"]!["pattern"]!.GetValue<string>().Should().Be("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");
        (await server.CallAsync("get_account", null)).StructuredContent!.Value.GetRawText().Should().Be($$"""{"result":"{{Account}}"}""");
    }

    /// <summary>A transfer, as a tool returns it.</summary>
    public sealed record Transfer(Iban Account, Quantity Quantity, List<Iban> Alternates);

    /// <summary>The tools, on an instance created for each call.</summary>
    public sealed class Tools
    {
        private static readonly Iban Default = Iban.Create(Account);

        private readonly string _prefix = "placed";

        [McpServerTool(Name = "place_order")]
        public string PlaceOrder(Iban iban, Quantity quantity, CountryCode country, BirthDate? birth = null)
            => $"{_prefix} {quantity} on {iban} in {country}{(birth is null ? string.Empty : $" for {birth}")}";

        [McpServerTool(Name = "book")]
        public static string Book(List<Iban> accounts) => $"booked {accounts.Count}";

        [McpServerTool(Name = "get_transfer", UseStructuredContent = true)]
        public static Transfer GetTransfer() => new(Default, Quantity.Create(3), [Default]);

        [McpServerTool(Name = "get_account", UseStructuredContent = true)]
        public static Iban GetAccount() => Default;
    }

    /// <summary>A server in process and the SDK's client connected to it.</summary>
    private sealed class Server : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly CancellationTokenSource _stop;
        private readonly Task _run;

        private Server(ServiceProvider provider, CancellationTokenSource stop, Task run, McpClient client)
        {
            _provider = provider;
            _stop = stop;
            _run = run;
            Client = client;
        }

        public McpClient Client { get; }

        public static async Task<Server> StartAsync(string? protocolVersion)
        {
            Pipe toServer = new(), toClient = new();
            var services = new ServiceCollection();
            services.AddMcpServer()
                .WithStreamServerTransport(toServer.Reader.AsStream(), toClient.Writer.AsStream())
                .WithValueObjectTools<Tools>();

            var provider = services.BuildServiceProvider();
            var stop = new CancellationTokenSource();
            var run = provider.GetRequiredService<McpServer>().RunAsync(stop.Token);
            var client = await McpClient.CreateAsync(
                new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream()),
                new McpClientOptions { ProtocolVersion = protocolVersion },
                cancellationToken: TestContext.Current.CancellationToken);
            return new Server(provider, stop, run, client);
        }

        public async Task<CallToolResult> CallAsync(string tool, string? arguments)
        {
            Dictionary<string, JsonElement>? values = null;
            if (arguments is not null)
            {
                using var document = JsonDocument.Parse(arguments);
                values = document.RootElement.EnumerateObject().ToDictionary(static property => property.Name, static property => property.Value.Clone());
            }

            return await Client.CallToolAsync(new CallToolRequestParams { Name = tool, Arguments = values }, TestContext.Current.CancellationToken);
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
}
