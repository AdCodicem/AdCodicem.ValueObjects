using AdCodicem.ValueObjects.Wolverine.Http;
using AdCodicem.ValueObjects.Wolverine.Http.Binding;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.Http;

namespace AdCodicem.ValueObjects.UnitTests.WolverineHttp;

/// <summary>
/// A Wolverine.Http application over TestHost whose endpoints are the types a test names alone, its chains built by
/// <c>MapWolverineEndpoints</c> as an application's are.
/// </summary>
/// <remarks>
/// Wolverine runs in <see cref="TypeLoadMode.Auto"/>, without the runtime compiler, which would bring Roslyn's
/// Workspaces 5.0.0 into this project: the chains, ApiExplorer, the OpenAPI documents and the code Wolverine writes are
/// all there, but a request to an endpoint fails, since its code is never compiled. Requests go through the fixture
/// application of <see cref="WolverineHttpEndToEndTests"/>. Every test using a host joins <see cref="WolverineHttpHosts"/>:
/// <see cref="GenerateCodeAsync"/> turns JasperFx's static <c>DynamicCodeBuilder.WithinCodegenCommand</c> on while it runs.
/// </remarks>
internal sealed class WolverineHttpHost : IAsyncDisposable
{
    private readonly CapturedLogs _logs;

    private WolverineHttpHost(WebApplication application, CapturedLogs logs)
    {
        Application = application;
        _logs = logs;
        Graph = ((IEndpointRouteBuilder)application).DataSources.OfType<HttpGraph>().Single();
    }

    /// <summary>Gets the application.</summary>
    public WebApplication Application { get; }

    /// <summary>Gets the endpoints Wolverine built.</summary>
    public HttpGraph Graph { get; }

    /// <summary>Gets the warnings logged so far.</summary>
    public IReadOnlyList<string> Warnings => _logs.Messages;

    /// <summary>Gets the policy <c>UseValueObjects</c> installed, which keeps what the strategy read for each endpoint.</summary>
    public ValueObjectRefusalPolicy Policy
        => Application.Services.GetRequiredService<WolverineHttpOptions>().Policies.OfType<ValueObjectRefusalPolicy>().Single();

    /// <summary>
    /// Builds an application over the endpoint types given, and maps their endpoints with <c>UseValueObjects()</c>, or
    /// with what <paramref name="configure"/> does instead.
    /// </summary>
    /// <param name="endpoints">The types whose endpoints are mapped.</param>
    /// <param name="configure">Configures the endpoints in place of <c>UseValueObjects()</c>.</param>
    /// <param name="services">Registers more services.</param>
    /// <returns>The host, its endpoints mapped and not started.</returns>
    public static WolverineHttpHost Map(Type[] endpoints, Action<WolverineHttpOptions>? configure = null, Action<IServiceCollection>? services = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        var logs = new CapturedLogs();
        builder.Logging.AddProvider(logs);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Host.UseWolverine(static options =>
        {
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Auto;
            options.Discovery.DisableConventionalDiscovery();
            options.ApplicationAssembly = typeof(WolverineHttpHost).Assembly;
        });
        builder.Services.AddWolverineHttp();
        services?.Invoke(builder.Services);

        var application = builder.Build();
        try
        {
            application.MapWolverineEndpoints(options =>
            {
                options.CustomizeHttpEndpointDiscovery(query =>
                    query.Excludes.WithCondition("another test's endpoints", type => !endpoints.Contains(type)));
                (configure ?? (static options => options.UseValueObjects()))(options);
            });
        }
        catch
        {
            application.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw;
        }

        return new WolverineHttpHost(application, logs);
    }

    /// <inheritdoc cref="Map(Type[], Action{WolverineHttpOptions}?, Action{IServiceCollection}?)"/>
    public static WolverineHttpHost Map(Type endpoints, Action<WolverineHttpOptions>? configure = null, Action<IServiceCollection>? services = null)
        => Map([endpoints], configure, services);

    /// <summary>Gets the endpoint of a route, as its template is written.</summary>
    /// <param name="route">The template.</param>
    /// <returns>The endpoint.</returns>
    public HttpChain Chain(string route) => Graph.Chains.Single(chain => chain.RoutePattern?.RawText == route);

    /// <summary>Gets what the strategy reads for the endpoint of a route, in order.</summary>
    /// <param name="route">The template.</param>
    /// <returns>The value objects read.</returns>
    public IReadOnlyList<ValueObjectHttpParameter> Reads(string route) => [.. Policy.ReadsOf(Chain(route)).Select(static read => read.Parameter)];

    /// <summary>
    /// Writes the code of every endpoint and handler, as <c>codegen write</c> does, on a started host that then serves no
    /// request: the code is written, not compiled.
    /// </summary>
    /// <returns>The code.</returns>
    public async Task<string> GenerateCodeAsync()
    {
        await Application.StartAsync(TestContext.Current.CancellationToken);
        var services = Application.Services;
        var builder = new DynamicCodeBuilder(
            services,
            [.. services.GetServices<ICodeFileCollection>(), .. services.GetRequiredService<WolverineSupplementalCodeFiles>().Collections]);

        return builder.GenerateAllCode();
    }

    /// <summary>Starts the application, for a test reading what only a running host serves.</summary>
    /// <returns>A client of its server.</returns>
    public async Task<HttpClient> StartAsync()
    {
        await Application.StartAsync(TestContext.Current.CancellationToken);
        return Application.GetTestClient();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => Application.DisposeAsync();

    /// <summary>The warnings an application logs, kept as their text.</summary>
    private sealed class CapturedLogs : ILoggerProvider
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                {
                    return [.. _messages];
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new Logger(this);

        public void Dispose()
        {
        }

        private sealed class Logger(CapturedLogs logs) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

            public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (logs._messages)
                {
                    logs._messages.Add(formatter(state, exception));
                }
            }
        }
    }
}

/// <summary>
/// Runs the tests that build a Wolverine host alone: JasperFx keeps whether code is being written for <c>codegen write</c>
/// in a static property, which a host built meanwhile would read.
/// </summary>
[CollectionDefinition(nameof(WolverineHttpHosts), DisableParallelization = true)]
public sealed class WolverineHttpHosts;
