using System.ComponentModel;
using System.IO.Pipelines;
using System.Text.Json;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.ModelContextProtocol;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// Model Context Protocol tools: a server in process, registered with <c>WithValueObjectTools&lt;T&gt;()</c>, and the
/// SDK's client over a pair of pipes, listing the tools, whose schemas carry the rules, on the latest protocol version
/// and on the one the clients in use today ask for, and calling them, a refused argument answered with its rule; and a
/// tool created by hand on a server created without dependency injection, with and without the check.
/// </summary>
/// <remarks>
/// Reflection-based serialization is off, so the server's serializer options are a copy of the SDK's whose resolver chain
/// starts with the application's context, and whose converters hold the converter factory.
/// </remarks>
internal static class Mcp
{
    private const string ValidOrder =
        """{"email":"ada@example.com","quantity":3,"customer":"6f9619ff-8b86-d011-b42d-00c04fc964ff","rate":5.5}""";

    /// <param name="report">Where each scenario is written.</param>
    /// <returns>The scenarios, run.</returns>
    public static async Task RunAsync(Report report)
    {
        // The SDK's own options know no value object, and reflection cannot make up for it.
        try
        {
            await using var unusable = await McpServerUnderTest.StartAsync(serializerOptions: null, protocolVersion: null);
            report.Line("mcp default options", "started");
        }
        catch (NotSupportedException exception)
        {
            report.Line("mcp default options", Report.Describe(exception));
        }

        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
        options.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
        options.Converters.Add(new ValueObjectJsonConverterFactory());

        foreach (var protocolVersion in (string?[])[null, "2025-11-25"])
        {
            await using var server = await McpServerUnderTest.StartAsync(options, protocolVersion);
            var tools = await server.Client.ListToolsAsync(new ListToolsRequestParams());
            foreach (var tool in tools.Tools.OrderBy(static tool => tool.Name, StringComparer.Ordinal))
            {
                report.Line(
                    $"mcp {server.Client.NegotiatedProtocolVersion} tool {tool.Name}",
                    $"input {tool.InputSchema.GetRawText()} output {tool.OutputSchema?.GetRawText() ?? "none"}");
            }

            report.Line($"mcp {server.Client.NegotiatedProtocolVersion} call get_order", await server.CallAsync("get_order", null));
            report.Line($"mcp {server.Client.NegotiatedProtocolVersion} call get_email", await server.CallAsync("get_email", """{"quantity":3}"""));
        }

        await using (var server = await McpServerUnderTest.StartAsync(options, protocolVersion: null))
        {
            foreach (var (name, json) in new (string, string?)[]
                     {
                         ("valid", ValidOrder),
                         ("quantity", "0"), ("quantity", "1001"), ("quantity", "\"7\""), ("quantity", "true"), ("quantity", "null"), ("quantity", null),
                         ("email", "\"ada\""), ("email", "42"), ("customer", "\"bad\""), ("customer", "\"00000000-0000-0000-0000-000000000000\""),
                         ("rate", "7"), ("opens", "\"08:30:00\""), ("opens", "null"),
                     })
            {
                report.Line(
                    $"mcp call place_order {name} {json ?? "absent"}",
                    await server.CallAsync("place_order", name == "valid" ? ValidOrder : With(name, json)));
            }

            foreach (var arguments in (string?[])
                     [
                         """{"addresses":["ada@example.com"],"number":"po-7"}""",
                         """{"addresses":["ada@example.com","ada"],"number":"po-7"}""",
                         """{"addresses":[],"number":"PO-1234567890123"}""",
                         null,
                     ])
            {
                report.Line($"mcp call book {arguments ?? "no arguments"}", await server.CallAsync("book", arguments));
            }

            foreach (var arguments in (string[])["""{"quantity":2}""", """{"quantity":0}""", """{"quantity":3}"""])
            {
                report.Line($"mcp call count {arguments}", await server.CallAsync("count", arguments));
            }

            report.Line("mcp call get_email refused", await server.CallAsync("get_email", """{"quantity":1001}"""));
            report.Line("mcp instances", $"{NativeTools.Created} created");
        }

        // A tool created by hand, on a server created without dependency injection from options built by hand.
        var byHand = ValueObjectMcpServerTool.Create(
            typeof(NativeTools).GetMethod(nameof(NativeTools.PlaceOrder))!,
            options: new McpServerToolCreateOptions { Name = "place_order_by_hand", SerializerOptions = options });
        foreach (var check in (bool[])[false, true])
        {
            var serverOptions = new McpServerOptions { ToolCollection = [byHand] };
            report.Line(
                $"mcp server without services, check {(check ? "added" : "absent")}",
                await CallWithoutServicesAsync(check ? serverOptions.AddValueObjectValidation() : serverOptions, "place_order_by_hand", With("quantity", "1001")));
        }
    }

    /// <summary>
    /// Calls a tool of a server created without dependency injection, through the SDK's client over a pair of pipes.
    /// </summary>
    private static async Task<string> CallWithoutServicesAsync(McpServerOptions serverOptions, string tool, string arguments)
    {
        Pipe toServer = new(), toClient = new();
        await using var server = McpServer.Create(new StreamServerTransport(toServer.Reader.AsStream(), toClient.Writer.AsStream()), serverOptions);
        using var stop = new CancellationTokenSource();
        var run = server.RunAsync(stop.Token);
        try
        {
            await using var client = await McpClient.CreateAsync(new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream()));
            using var document = JsonDocument.Parse(arguments);
            var result = await client.CallToolAsync(new CallToolRequestParams
            {
                Name = tool,
                Arguments = document.RootElement.EnumerateObject().ToDictionary(static property => property.Name, static property => property.Value.Clone()),
            });
            return $"error {result.IsError == true}, text {string.Join("|", result.Content.OfType<TextContentBlock>().Select(static block => block.Text))}";
        }
        finally
        {
            await stop.CancelAsync();
            try
            {
                await run;
            }
            catch (OperationCanceledException)
            {
                // The server stops with the token it ran under.
            }
        }
    }

    private static string With(string name, string? json)
    {
        var order = System.Text.Json.Nodes.JsonNode.Parse(ValidOrder)!.AsObject();
        if (json is null)
        {
            order.Remove(name);
        }
        else
        {
            order[name] = System.Text.Json.Nodes.JsonNode.Parse(json);
        }

        return order.ToJsonString();
    }

    /// <summary>A server in process and the SDK's client connected to it.</summary>
    private sealed class McpServerUnderTest : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly CancellationTokenSource _stop;
        private readonly Task _run;

        private McpServerUnderTest(ServiceProvider provider, CancellationTokenSource stop, Task run, McpClient client)
        {
            _provider = provider;
            _stop = stop;
            _run = run;
            Client = client;
        }

        public McpClient Client { get; }

        public static async Task<McpServerUnderTest> StartAsync(JsonSerializerOptions? serializerOptions, string? protocolVersion)
        {
            Pipe toServer = new(), toClient = new();
            var services = new ServiceCollection();
            services.AddMcpServer()
                .WithStreamServerTransport(toServer.Reader.AsStream(), toClient.Writer.AsStream())
                .WithValueObjectTools<NativeTools>(serializerOptions);

            var provider = services.BuildServiceProvider();
            var stop = new CancellationTokenSource();
            try
            {
                var run = provider.GetRequiredService<McpServer>().RunAsync(stop.Token);
                var client = await McpClient.CreateAsync(
                    new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream()),
                    new McpClientOptions { ProtocolVersion = protocolVersion });
                return new McpServerUnderTest(provider, stop, run, client);
            }
            catch
            {
                stop.Dispose();
                await provider.DisposeAsync();
                throw;
            }
        }

        /// <summary>Calls a tool and describes its result: whether it is an error, its text, its structured content.</summary>
        public async Task<string> CallAsync(string tool, string? arguments)
        {
            Dictionary<string, JsonElement>? values = null;
            if (arguments is not null)
            {
                using var document = JsonDocument.Parse(arguments);
                values = document.RootElement.EnumerateObject().ToDictionary(static property => property.Name, static property => property.Value.Clone());
            }

            var result = await Client.CallToolAsync(new CallToolRequestParams { Name = tool, Arguments = values });
            return $"error {result.IsError == true}, text {string.Join("|", result.Content.OfType<TextContentBlock>().Select(static block => block.Text))}, "
                   + $"structured {result.StructuredContent?.GetRawText() ?? "none"}";
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

    /// <summary>The tools, static and on an instance created for each call.</summary>
    private sealed class NativeTools
    {
        private static int _created;

        private readonly int _number;

        public NativeTools() => _number = Interlocked.Increment(ref _created);

        /// <summary>Gets the number of instances created, one for each call of an instance tool the server accepted.</summary>
        public static int Created => _created;

        [McpServerTool(Name = "place_order")]
        [Description("Places an order.")]
        public static string PlaceOrder(
            [Description("The address to confirm the order to.")] EmailAddress email,
            Quantity quantity,
            CustomerId customer,
            VatRate rate,
            OpeningTime? opens = null)
            => $"placed {quantity} for {email} ({customer}) at {rate} %, opens {opens?.ToString() ?? "now"}";

        [McpServerTool(Name = "book")]
        public static string Book(List<EmailAddress> addresses, DocumentNumber<PurchaseOrder> number)
            => $"booked {number} for {addresses.Count}";

        [McpServerTool(Name = "count")]
        public string Count(Quantity quantity) => $"counted {quantity} on instance {_number}";

        [McpServerTool(Name = "get_order", UseStructuredContent = true)]
        public static Order GetOrder() => new(
            EmailAddress.Create("ada@example.com"),
            Quantity.Create(3),
            Amount.Create(12.5m),
            VatRate.Reduced,
            DocumentStatus.Parse("Final", null),
            CustomerId.Parse("0f8fad5b-d9cb-469f-a165-70867728950e", null),
            DocumentNumber<PurchaseOrder>.Create("po-1042"),
            null,
            OpeningTime.Create(new TimeOnly(9, 30)));

        /// <summary>Returns a value object alone, whose output schema the SDK wraps for a client on an older protocol.</summary>
        [McpServerTool(Name = "get_email", UseStructuredContent = true)]
        public static EmailAddress GetEmail(Quantity quantity) => quantity.Value > 0 ? EmailAddress.Example : EmailAddress.Create("grace@example.com");
    }
}
