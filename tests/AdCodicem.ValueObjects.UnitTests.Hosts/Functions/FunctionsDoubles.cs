using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AzureFunctions.AspNetCore;
using AdCodicem.ValueObjects.UnitTests.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.UnitTests.Functions;

/// <summary>A conversion context written by hand, as the worker hands one to a converter.</summary>
/// <param name="targetType">The parameter's type.</param>
/// <param name="source">What the host bound to it.</param>
/// <param name="properties">The parameter's properties, as the worker lists them; none by default.</param>
internal sealed class TextContext(Type targetType, object? source, IReadOnlyDictionary<string, object>? properties = null)
    : ConverterContext
{
    public override Type TargetType => targetType;

    public override object? Source => source;

    public override FunctionContext FunctionContext => throw new NotSupportedException("The converter reads no invocation.");

    public override IReadOnlyDictionary<string, object> Properties => properties ?? ImmutableDictionary<string, object>.Empty;
}

/// <summary>
/// An invocation of a function, as the worker hands one to its middleware: its definition, its binding data, the request
/// the ASP.NET Core integration set on it, if any, and what it logs.
/// </summary>
internal sealed class FunctionInvocation : FunctionContext
{
    /// <summary>
    /// The worker's internal feature that holds an invocation's result, which <c>GetInvocationResult()</c> reads: a
    /// substitute implements it, Worker.Core granting its internals to NSubstitute's proxies.
    /// </summary>
    private static readonly Type BindingsFeature = typeof(FunctionContext).Assembly.GetType(
        "Microsoft.Azure.Functions.Worker.Context.Features.IFunctionBindingsFeature",
        throwOnError: true)!;

    private readonly Definition _definition;

    private readonly Bindings _bindings;

    private readonly InvocationFeatures _features = new();

    /// <summary>Creates the invocation of an HTTP trigger, or of another trigger.</summary>
    /// <param name="name">The function's name.</param>
    /// <param name="parameters">Its parameters, the value objects among them.</param>
    /// <param name="bindingData">What the host bound for the invocation, read ignoring case, as the worker reads it.</param>
    /// <param name="httpContext">The request the integration set on the invocation, or <see langword="null"/>.</param>
    /// <param name="trigger">The type of its trigger binding.</param>
    public FunctionInvocation(
        string name,
        IEnumerable<FunctionParameter> parameters,
        IReadOnlyDictionary<string, object?>? bindingData = null,
        HttpContext? httpContext = null,
        string trigger = "httpTrigger")
    {
        _definition = new Definition(name, [.. parameters], trigger);
        _bindings = new Bindings(new Dictionary<string, object?>(
            bindingData ?? new Dictionary<string, object?>(),
            StringComparer.OrdinalIgnoreCase));
        _features.Set(BindingsFeature, Substitute.For([BindingsFeature], []));
        if (httpContext is not null)
        {
            Items["HttpRequestContext"] = httpContext;
        }

        InstanceServices = new ServiceCollection().AddLogging(logging => logging.AddProvider(Logs)).BuildServiceProvider();
    }

    /// <summary>Gets what the invocation logged.</summary>
    public LogCapture Logs { get; } = new();

    /// <summary>Gets the invocation's result, which the middleware sets for the integration to execute.</summary>
    public object? Result => this.GetInvocationResult().Value;

    public override string InvocationId { get; } = Guid.NewGuid().ToString();

    public override string FunctionId => _definition.Id;

    public override TraceContext TraceContext => null!;

    public override BindingContext BindingContext => _bindings;

    public override RetryContext RetryContext => null!;

    public override IServiceProvider InstanceServices { get; set; }

    public override FunctionDefinition FunctionDefinition => _definition;

    public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();

    public override IInvocationFeatures Features => _features;

    /// <summary>A parameter the worker binds through its converters, as it lists one without attributes.</summary>
    /// <typeparam name="T">Its type.</typeparam>
    /// <param name="name">Its name.</param>
    /// <param name="properties">Its properties: a binding attribute, or the converter it names.</param>
    /// <param name="hasDefaultValue">Whether it declares a default value.</param>
    /// <returns>The parameter.</returns>
    public static FunctionParameter Parameter<T>(string name, IReadOnlyDictionary<string, object>? properties = null, bool hasDefaultValue = false)
        => new(name, typeof(T), hasDefaultValue, defaultValue: null, properties ?? ImmutableDictionary<string, object>.Empty);

    private sealed class Definition(string name, ImmutableArray<FunctionParameter> parameters, string trigger) : FunctionDefinition
    {
        public override ImmutableArray<FunctionParameter> Parameters => parameters;

        public override string PathToAssembly => typeof(FunctionInvocation).Assembly.Location;

        public override string EntryPoint => $"{typeof(FunctionInvocation).FullName}.{name}";

        public override string Id => name;

        public override string Name => name;

        public override IImmutableDictionary<string, BindingMetadata> InputBindings { get; }
            = ImmutableDictionary<string, BindingMetadata>.Empty.Add("trigger", new Binding("trigger", trigger));

        public override IImmutableDictionary<string, BindingMetadata> OutputBindings { get; }
            = ImmutableDictionary<string, BindingMetadata>.Empty;
    }

    private sealed class Binding(string name, string type) : BindingMetadata
    {
        public override string Name => name;

        public override string Type => type;

        public override BindingDirection Direction => BindingDirection.In;
    }

    private sealed class Bindings(IReadOnlyDictionary<string, object?> data) : BindingContext
    {
        public override IReadOnlyDictionary<string, object?> BindingData => data;
    }

    private sealed class InvocationFeatures : IInvocationFeatures
    {
        private readonly Dictionary<Type, object> _features = [];

        public void Set(Type type, object instance) => _features[type] = instance;

        public void Set<T>(T instance) => _features[typeof(T)] = instance!;

        public T? Get<T>() => _features.TryGetValue(typeof(T), out var feature) ? (T)feature : default;

        public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _features.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>One entry the middleware logged.</summary>
/// <param name="Category">The logger's category.</param>
/// <param name="Level">Its level.</param>
/// <param name="EventName">The name of its event.</param>
/// <param name="Message">Its message, formatted.</param>
internal sealed record LoggedEntry(string Category, LogLevel Level, string? EventName, string Message);

/// <summary>Keeps what an invocation logs.</summary>
internal sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<LoggedEntry> _entries = new();

    public IReadOnlyCollection<LoggedEntry> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<LoggedEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Enqueue(new LoggedEntry(category, logLevel, eventId.Name, formatter(state, exception)));
    }
}

/// <summary>Builds the pieces a test of the middleware needs: the request, the middleware, and the answer read back.</summary>
internal static class FunctionsHttp
{
    /// <summary>A request whose services hold what an application of the integration registers, and more if asked.</summary>
    /// <param name="configure">Registers more services: localization, or JSON options of the application's own.</param>
    /// <returns>The request, its response written to memory.</returns>
    public static DefaultHttpContext Request(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection().AddOptions().AddLogging();
        configure?.Invoke(services);

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };
    }

    /// <summary>The middleware, as the registration creates it, with the messages it names.</summary>
    /// <param name="messages">The messages, or <see langword="null"/> for the English ones of the rules.</param>
    /// <returns>The middleware.</returns>
    public static ValueObjectProblemDetailsMiddleware Middleware(ValueObjectMessageOptions? messages = null)
        => new(new ValueObjectProblemDetailsSettings { Messages = messages });

    /// <summary>Runs the middleware around an invocation that fails as the worker fails one it could not bind.</summary>
    /// <param name="middleware">The middleware.</param>
    /// <param name="invocation">The invocation.</param>
    /// <param name="failure">What the invocation throws; the worker's binding failure by default.</param>
    /// <returns>A task that completes once the middleware returns.</returns>
    public static Task RefuseAsync(this ValueObjectProblemDetailsMiddleware middleware, FunctionInvocation invocation, Exception? failure = null)
        => middleware.Invoke(invocation, _ => throw (failure ?? Unbound()));

    /// <summary>The exception the worker throws when it cannot bind an invocation's parameters: its text alone.</summary>
    /// <returns>The exception.</returns>
    public static FunctionInputConverterException Unbound()
        => new("Error converting 1 input parameters for Function 'Line': Cannot convert input parameter 'qty'.");

    /// <summary>Executes the result the middleware set on the request, as the integration's proxying middleware does.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="request">The request.</param>
    /// <returns>The problem answered, read as the messages and the codes of each member.</returns>
    public static async Task<ReadProblem> AnswerAsync(FunctionInvocation invocation, HttpContext request)
    {
        var body = await ExecuteAsync(invocation, request);

        return ProblemReading.ProblemOf(request.Response.StatusCode, body);
    }

    /// <summary>Executes the result the middleware set on the request, and reads the body written.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="request">The request.</param>
    /// <returns>The body.</returns>
    public static async Task<string> ExecuteAsync(FunctionInvocation invocation, HttpContext request)
    {
        invocation.Result.Should().BeAssignableTo<IResult>("the middleware answers with a result the integration executes");
        await ((IResult)invocation.Result!).ExecuteAsync(request);
        request.Response.Body.Position = 0;
        using var reader = new StreamReader(request.Response.Body, leaveOpen: true);

        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
