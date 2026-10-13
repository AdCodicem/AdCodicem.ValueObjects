using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AzureFunctions;
using AdCodicem.ValueObjects.AzureFunctions.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// The Azure Functions packages beside ASP.NET Core of the next major: the converter the isolated worker tries first,
/// registered by <c>UseValueObjects()</c> ahead of the ASP.NET Core integration's own, and the middleware
/// <c>UseValueObjectProblemDetails()</c> adds, answering a refused route value with the code of its rule. The worker's
/// protocol with the host is internal to it, so the converter is called as the worker calls it, and the middleware over
/// an invocation written here, created by the host's own services.
/// </summary>
public sealed class FunctionsTests
{
    [Fact]
    public async Task A_route_value_binds_through_the_rules_and_a_refusal_carries_its_code()
    {
        var converter = new ValueObjectInputConverter();

        (await converter.ConvertAsync(new Text(typeof(Quantity?), "007"))).Value.Should().Be(Quantity.Create(7));
        (await converter.ConvertAsync(new Text(typeof(CustomerId), null))).Status.Should().Be(ConversionStatus.Unhandled);

        var refusal = (await converter.ConvertAsync(new Text(typeof(Quantity), "500"))).Error!;
        refusal.Should().BeOfType<FormatException>();
        refusal.Message.Should().Be("The value is not a valid Quantity: The value must be less than or equal to 100. (value_object.out_of_range)");
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public async Task A_refused_route_value_is_answered_with_the_code_of_its_rule()
    {
        IServiceCollection? services = null;
        using var host = new HostBuilder()
            .ConfigureFunctionsWebApplication(worker =>
            {
                worker.UseValueObjects().UseValueObjectProblemDetails();
                services = worker.Services;
            })
            .Build();

        host.Services.GetRequiredService<IOptions<WorkerOptions>>().Value.InputConverters.Select(static converter => converter.Name)
            .Should().StartWith(["ValueObjectInputConverter", "HttpContextConverter"]);

        var registered = services!.Single(static service => service.ServiceType.Assembly == typeof(ValueObjectProblemDetailsWorkerApplicationBuilderExtensions).Assembly
                                                            && typeof(IFunctionsWorkerMiddleware).IsAssignableFrom(service.ServiceType));
        var middleware = (IFunctionsWorkerMiddleware)host.Services.GetRequiredService(registered.ServiceType);
        var request = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddOptions().AddLogging().BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };
        var invocation = new Invocation(request, new FunctionParameter("qty", typeof(Quantity), false, null, ImmutableDictionary<string, object>.Empty), "500");

        await middleware.Invoke(invocation, _ => throw new FunctionInputConverterException("Error converting 1 input parameters for Function 'Line'."));
        await ((IResult)invocation.GetInvocationResult().Value!).ExecuteAsync(request);

        request.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        request.Response.Body.Position = 0;
        var problem = (await JsonDocument.ParseAsync(request.Response.Body, cancellationToken: TestContext.Current.CancellationToken)).RootElement;
        problem.GetProperty(ValueObjectProblemDetails.ExtensionName).GetProperty("qty").GetString().Should().Be(ValueObjectErrorCodes.OutOfRange);
        problem.GetProperty("errors").GetProperty("qty")[0].GetString().Should().Be("The value must be less than or equal to 100.");
    }

    /// <summary>A conversion context, as the worker hands one to a converter.</summary>
    private sealed class Text(Type target, object? source) : ConverterContext
    {
        public override Type TargetType => target;

        public override object? Source => source;

        public override FunctionContext FunctionContext => throw new NotSupportedException();

        public override IReadOnlyDictionary<string, object> Properties => ImmutableDictionary<string, object>.Empty;
    }

    /// <summary>
    /// An invocation of an HTTP trigger, as the ASP.NET Core integration hands one to the middleware inside its own: the
    /// worker's internal feature holding its result is a substitute, Worker.Core granting its internals to NSubstitute.
    /// </summary>
    private sealed class Invocation : FunctionContext
    {
        private readonly Features _features = new();

        public Invocation(HttpContext request, FunctionParameter parameter, string text)
        {
            var bindings = typeof(FunctionContext).Assembly.GetType("Microsoft.Azure.Functions.Worker.Context.Features.IFunctionBindingsFeature", throwOnError: true)!;
            _features.Set(bindings, Substitute.For([bindings], []));
            FunctionDefinition = new Definition(parameter);
            BindingContext = new Data(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { [parameter.Name] = text });
            Items = new Dictionary<object, object> { ["HttpRequestContext"] = request };
            InstanceServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        }

        public override string InvocationId => "1";

        public override string FunctionId => "Line";

        public override TraceContext TraceContext => null!;

        public override BindingContext BindingContext { get; }

        public override RetryContext RetryContext => null!;

        public override IServiceProvider InstanceServices { get; set; }

        public override FunctionDefinition FunctionDefinition { get; }

        public override IDictionary<object, object> Items { get; set; }

        public override IInvocationFeatures Features => _features;
    }

    private sealed class Definition(FunctionParameter parameter) : FunctionDefinition
    {
        public override ImmutableArray<FunctionParameter> Parameters => [parameter];

        public override string PathToAssembly => typeof(Definition).Assembly.Location;

        public override string EntryPoint => "Line";

        public override string Id => "Line";

        public override string Name => "Line";

        public override IImmutableDictionary<string, BindingMetadata> InputBindings { get; }
            = ImmutableDictionary<string, BindingMetadata>.Empty.Add("request", new Trigger());

        public override IImmutableDictionary<string, BindingMetadata> OutputBindings { get; } = ImmutableDictionary<string, BindingMetadata>.Empty;
    }

    private sealed class Trigger : BindingMetadata
    {
        public override string Name => "request";

        public override string Type => "httpTrigger";

        public override BindingDirection Direction => BindingDirection.In;
    }

    private sealed class Data(IReadOnlyDictionary<string, object?> data) : BindingContext
    {
        public override IReadOnlyDictionary<string, object?> BindingData => data;
    }

    private sealed class Features : IInvocationFeatures
    {
        private readonly Dictionary<Type, object> _features = [];

        public void Set(Type type, object instance) => _features[type] = instance;

        public void Set<T>(T instance) => _features[typeof(T)] = instance!;

        public T? Get<T>() => _features.TryGetValue(typeof(T), out var feature) ? (T)feature : default;

        public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _features.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
