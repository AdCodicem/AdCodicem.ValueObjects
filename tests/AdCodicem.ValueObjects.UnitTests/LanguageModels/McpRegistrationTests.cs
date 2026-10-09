using System.IO.Pipelines;
using AdCodicem.ValueObjects.ModelContextProtocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static AdCodicem.ValueObjects.UnitTests.LanguageModels.McpHarness;

namespace AdCodicem.ValueObjects.UnitTests.LanguageModels;

/// <summary>
/// How the registrations reach a server: one call-tool filter, after every filter its options are configured with,
/// for every tool created with the rules, whichever way it was registered, and how options built or changed by hand
/// get it.
/// </summary>
public sealed class McpRegistrationTests
{
    private const string RefusedQuantity = """{"quantity":1001}""";

    /// <summary>
    /// The check runs inside the application's own filters, even one added after the registration: a filter of the
    /// application sees the refusal as the tool's result.
    /// </summary>
    [Fact]
    public async Task The_check_runs_inside_the_application_s_own_filters()
    {
        var seen = new List<bool?>();
        await using var harness = await StartAsync(server => server
            .WithValueObjectTools<OrderTools>()
            .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (request, cancellationToken) =>
            {
                var result = await next(request, cancellationToken);
                seen.Add(result.IsError);
                return result;
            })));

        ShouldBeRefused(
            await harness.CallAsync("who", RefusedQuantity),
            "quantity",
            ValueObjectErrorCodes.OutOfRange,
            "The value is not a valid Quantity: The value must be less than or equal to 1000.");
        seen.Should().Equal(true);
    }

    /// <summary>
    /// However many registrations a server has, one filter checks them all, and nothing else is added to its filters.
    /// </summary>
    [Fact]
    public async Task One_filter_checks_every_registration()
    {
        var tool = ValueObjectMcpServerTool.Create(typeof(StaticTools).GetMethod(nameof(StaticTools.Ship))!, options: new() { Name = "ship_by_hand" });
        await using var harness = await StartAsync(server => server
            .WithValueObjectTools<OrderTools>()
            .WithValueObjectTools([typeof(StaticTools)])
            .WithValueObjectTools([tool]));

        harness.Services.GetRequiredService<IOptions<McpServerOptions>>().Value.Filters.Request.CallToolFilters.Should().ContainSingle();
        foreach (var name in (string[])["who", "ship", "ship_by_hand"])
        {
            (await harness.CallAsync(name, """{"quantity":1001,"country":"FR"}""")).IsError.Should().BeTrue(name);
        }
    }

    /// <summary>
    /// A tool <c>ValueObjectMcpServerTool.Create</c> created publishes its rules wherever it is registered, but its
    /// arguments are checked only on a server one of the <c>WithValueObjectTools</c> methods configured.
    /// </summary>
    [Fact]
    public async Task A_tool_created_by_hand_is_checked_on_a_server_the_package_configured()
    {
        var tool = ValueObjectMcpServerTool.Create(typeof(OrderTools).GetMethod(nameof(OrderTools.GetIban))!, new OrderTools(new InvocationLog()));
        const string Message = "The value is not a valid Quantity: The value must be less than or equal to 1000.";

        await using (var sdk = await StartAsync(server => server.WithTools([tool])))
        {
            (await sdk.ListAsync())["get_iban"].InputSchema.GetProperty("properties").GetProperty("quantity").GetProperty("maximum").GetInt32().Should().Be(1000);
            TextOf(await sdk.CallAsync("get_iban", RefusedQuantity)).Should().Be("An error occurred invoking 'get_iban'.");
        }

        await using (var configured = await StartAsync(server => server.WithValueObjectTools([tool])))
        {
            ShouldBeRefused(await configured.CallAsync("get_iban", RefusedQuantity), "quantity", ValueObjectErrorCodes.OutOfRange, Message, structured: false);
        }

        await using (var beside = await StartAsync(server => server.WithValueObjectTools([typeof(StaticTools)]).WithTools([tool])))
        {
            ShouldBeRefused(await beside.CallAsync("get_iban", RefusedQuantity), "quantity", ValueObjectErrorCodes.OutOfRange, Message, structured: false);
        }
    }

    /// <summary>
    /// A filter added once the options are configured, as <c>ConfigureSessionOptions</c> adds one under HTTP, runs
    /// inside the check, unless <c>AddValueObjectValidation</c> places the check after it again.
    /// </summary>
    /// <param name="placedAgain">Whether the check is placed again after the filter is added.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_filter_added_later_runs_inside_the_check_unless_the_check_is_placed_again(bool placedAgain)
    {
        var seen = new List<bool?>();
        await using var harness = await StartAsync(
            static server => server.WithValueObjectTools<OrderTools>(),
            services: services => services.PostConfigure<McpServerOptions>(options =>
            {
                options.Filters.Request.CallToolFilters.Add(next => async (request, cancellationToken) =>
                {
                    var result = await next(request, cancellationToken);
                    seen.Add(result.IsError);
                    return result;
                });
                if (placedAgain)
                {
                    options.AddValueObjectValidation();
                }
            }));

        (await harness.CallAsync("who", RefusedQuantity)).IsError.Should().BeTrue();
        harness.Services.GetRequiredService<IOptions<McpServerOptions>>().Value.Filters.Request.CallToolFilters.Should().HaveCount(2);
        if (placedAgain)
        {
            seen.Should().Equal(true);
        }
        else
        {
            seen.Should().BeEmpty();
        }
    }

    /// <summary>
    /// A server created without dependency injection checks the arguments of a tool created with the rules once its
    /// options are given the check, which they hold once however many times it is placed, last; without it, the SDK
    /// answers a refused argument with its bare error.
    /// </summary>
    [Fact]
    public async Task A_server_created_by_hand_checks_the_arguments_once_its_options_are_given_the_check()
    {
        var tool = ValueObjectMcpServerTool.Create(typeof(StaticTools).GetMethod(nameof(StaticTools.Ship))!);
        const string Refused = """{"quantity":1001,"country":"FR"}""";

        TextOf(await CallCreatedByHandAsync(new McpServerOptions { ToolCollection = [tool] }, Refused)).Should().Be("An error occurred invoking 'ship'.");

        var seen = new List<bool?>();
        var options = new McpServerOptions { ToolCollection = [tool] }.AddValueObjectValidation();
        options.Filters.Request.CallToolFilters.Add(next => async (request, cancellationToken) =>
        {
            var result = await next(request, cancellationToken);
            seen.Add(result.IsError);
            return result;
        });
        options.AddValueObjectValidation().Should().BeSameAs(options);
        options.Filters.Request.CallToolFilters.Should().HaveCount(2);

        ShouldBeRefused(
            await CallCreatedByHandAsync(options, Refused),
            "quantity",
            ValueObjectErrorCodes.OutOfRange,
            "The value is not a valid Quantity: The value must be less than or equal to 1000.");
        seen.Should().Equal(true);
        FluentActions.Invoking(static () => ((McpServerOptions)null!).AddValueObjectValidation()).Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    /// <summary>
    /// The SDK refuses call-tool filters beside an explicit alternate handler, and throws when it creates the server,
    /// here when the server is first resolved.
    /// </summary>
    [Fact]
    public async Task A_server_with_an_explicit_alternate_handler_is_refused_when_it_is_created()
    {
        await FluentActions.Awaiting(() => StartAsync(
                static server => server.WithValueObjectTools<OrderTools>(),
                services: static services => services.Configure<McpServerOptions>(
#pragma warning disable MCPEXP002 // Experimental: the SDK refuses call-tool filters beside it, as the guide says.
                    static options => options.Handlers.CallToolWithAlternateHandler = static (_, _) => throw new InvalidOperationException("Never called."))))
#pragma warning restore MCPEXP002
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Cannot apply CallToolFilters*");
    }

    /// <summary>
    /// Each registration refuses a missing builder, type list or tool list, naming the parameter, and the creation of a
    /// tool a missing method; a missing type or tool among others is skipped, as the SDK skips it.
    /// </summary>
    [Fact]
    public async Task Missing_arguments_are_refused_and_missing_elements_skipped()
    {
        IMcpServerBuilder builder = null!;

        FluentActions.Invoking(() => builder.WithValueObjectTools<OrderTools>()).Should().Throw<ArgumentNullException>().WithParameterName("builder");
        FluentActions.Invoking(() => builder.WithValueObjectTools([typeof(OrderTools)])).Should().Throw<ArgumentNullException>().WithParameterName("builder");
        FluentActions.Invoking(() => builder.WithValueObjectToolsFromAssembly()).Should().Throw<ArgumentNullException>().WithParameterName("builder");
        FluentActions.Invoking(() => builder.WithValueObjectTools(Array.Empty<McpServerTool>())).Should().Throw<ArgumentNullException>().WithParameterName("builder");
        FluentActions.Invoking(() => ValueObjectMcpServerTool.Create(null!)).Should().Throw<ArgumentNullException>().WithParameterName("method");

        var server = new ServiceCollection().AddMcpServer();
        FluentActions.Invoking(() => server.WithValueObjectTools((IEnumerable<Type>)null!)).Should().Throw<ArgumentNullException>().WithParameterName("toolTypes");
        FluentActions.Invoking(() => server.WithValueObjectTools((IEnumerable<McpServerTool>)null!)).Should().Throw<ArgumentNullException>().WithParameterName("tools");

        await using var harness = await StartAsync(static server => server.WithValueObjectTools([null!, typeof(StaticTools)]).WithValueObjectTools((McpServerTool[])[null!]));
        (await harness.ListAsync()).Keys.Should().Equal("ship");
    }

    /// <summary>
    /// An instance method needs an instance, which the SDK asks for.
    /// </summary>
    [Fact]
    public void An_instance_method_without_an_instance_is_refused_by_the_SDK()
        => FluentActions.Invoking(() => ValueObjectMcpServerTool.Create(typeof(OrderTools).GetMethod(nameof(OrderTools.PlaceOrder))!))
            .Should().Throw<ArgumentException>();

    /// <summary>
    /// The instance of a call is created from the services of the call, and, without any, from a parameterless
    /// constructor, as the SDK falls back to one, or from a constructor whose parameters all have defaults, which the
    /// SDK's fallback refuses.
    /// </summary>
    [Fact]
    public void The_instance_of_a_call_is_created_with_or_without_services()
    {
        using var services = new ServiceCollection().AddSingleton<InvocationLog>().BuildServiceProvider();

        using var tools = ToolTargets.Create(services, typeof(OrderTools)).Should().BeOfType<OrderTools>().Subject;
        services.GetRequiredService<InvocationLog>().Created.Should().Be(1);
        ToolTargets.Create(null, typeof(OptionalTools)).Should().BeOfType<OptionalTools>().Which.Log.Should().BeNull();
    }

    /// <summary>
    /// Calls a tool of a server created without dependency injection, from options built by hand, through the SDK's
    /// client over a pair of pipes.
    /// </summary>
    private static async Task<CallToolResult> CallCreatedByHandAsync(McpServerOptions options, string arguments)
    {
        Pipe toServer = new(), toClient = new();
        await using var server = McpServer.Create(new StreamServerTransport(toServer.Reader.AsStream(), toClient.Writer.AsStream()), options);
        using var stop = new CancellationTokenSource();
        var run = server.RunAsync(stop.Token);
        try
        {
            await using var client = await McpClient.CreateAsync(
                new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream()),
                cancellationToken: TestContext.Current.CancellationToken);
            return await client.CallToolAsync(
                new CallToolRequestParams { Name = "ship", Arguments = Arguments(arguments) },
                TestContext.Current.CancellationToken);
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

    /// <summary>A tool type whose constructor takes a service it can do without.</summary>
    private sealed class OptionalTools(InvocationLog? log = null)
    {
        public InvocationLog? Log { get; } = log;
    }
}
