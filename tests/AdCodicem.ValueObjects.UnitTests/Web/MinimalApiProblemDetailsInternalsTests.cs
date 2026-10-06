using System.Reflection;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.Http;
using AdCodicem.ValueObjects.AspNetCore.Http.Binding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// The convention, the filter and the exception handler called directly, with what no request through ASP.NET Core
/// hands them: an endpoint builder that is not a route's, a feature without an endpoint, route values taken away, and an
/// exception wrapped in another way than the framework wraps it.
/// </summary>
public sealed class MinimalApiProblemDetailsInternalsTests
{
    private static readonly MethodInfo Handler = typeof(MinimalApiProblemDetailsInternalsTests)
        .GetMethod(nameof(Lines), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo PlainHandler = typeof(MinimalApiProblemDetailsInternalsTests)
        .GetMethod(nameof(Plain), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly EndpointFilterDelegate Next = static _ => ValueTask.FromResult<object?>("next");

    [Fact]
    public void An_endpoint_whose_parameters_hold_no_value_object_gets_no_filter_factory()
    {
        var endpoint = new RouteEndpointBuilder(static _ => Task.CompletedTask, RoutePatternFactory.Parse("/plain/{n}"), 0)
        {
            Metadata = { PlainHandler },
        };

        ValueObjectBindingFilter.Apply(endpoint);

        endpoint.FilterFactories.Should().BeEmpty();
        endpoint.Metadata.OfType<ValueObjectBindingMetadata>().Should().ContainSingle("a body is explained without a parameter");
    }

    [Fact]
    public void An_endpoint_covered_twice_gets_one_filter_factory()
    {
        var endpoint = new RouteEndpointBuilder(static _ => Task.CompletedTask, RoutePatternFactory.Parse("/lines"), 0)
        {
            Metadata = { Handler },
        };

        ValueObjectBindingFilter.Apply(endpoint);
        ValueObjectBindingFilter.Apply(endpoint);

        endpoint.FilterFactories.Should().ContainSingle();
        endpoint.Metadata.OfType<ValueObjectBindingMetadata>().Should().ContainSingle();
    }

    [Fact]
    public void An_endpoint_whose_binder_describes_no_value_object_keeps_the_next_delegate()
    {
        var endpoint = new RouteEndpointBuilder(static _ => Task.CompletedTask, RoutePatternFactory.Parse("/lines"), 0)
        {
            Metadata = { Handler },
        };
        ValueObjectBindingFilter.Apply(endpoint);

        endpoint.FilterFactories.Single()(new EndpointFilterFactoryContext { MethodInfo = Handler, ApplicationServices = Services() }, Next)
            .Should().BeSameAs(Next);
    }

    /// <summary>
    /// An endpoint builder that is not a route's has no route pattern, and binds an inferred value object from the query
    /// string, as the binder does without route parameters.
    /// </summary>
    [Fact]
    public async Task An_endpoint_without_a_route_pattern_reads_an_inferred_value_object_from_the_query_string()
    {
        var endpoint = new PatternlessEndpointBuilder
        {
            Metadata = { Handler, new TextBinding("quantity", Handler.GetParameters()[0], isOptional: false) },
        };
        ValueObjectBindingFilter.Apply(endpoint);
        var filter = endpoint.FilterFactories.Single()(
            new EndpointFilterFactoryContext { MethodInfo = Handler, ApplicationServices = Services() },
            Next);
        var context = RequestContext("?quantity=-1");
        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        var result = await filter(new DefaultEndpointFilterInvocationContext(context));

        var problem = result.Should().BeOfType<ValidationProblem>().Subject.ProblemDetails;
        problem.Errors.Should().ContainKey("quantity");
        ((JsonElement)problem.Extensions[ValueObjectProblemDetails.ExtensionName]!).GetProperty("quantity").GetString()
            .Should().Be(ValueObjectErrorCodes.OutOfRange);
        (await filter(new DefaultEndpointFilterInvocationContext(RequestContext("?quantity=1"))))
            .Should().Be("next", "a request that bound is not looked at");
    }

    [Fact]
    public void Registering_the_handler_twice_registers_it_once()
    {
        using var services = new ServiceCollection().AddValueObjectHttpProblemDetails().AddValueObjectHttpProblemDetails().BuildServiceProvider();

        services.GetServices<IExceptionHandler>().Should().ContainSingle().Which.Should().BeOfType<ValueObjectBadRequestExceptionHandler>();
    }

    [Fact]
    public async Task The_handler_leaves_an_exception_that_is_no_bad_request_alone()
    {
        var context = RequestContext(string.Empty, Covered());

        (await TryHandleAsync(context, new InvalidOperationException("no"))).Should().BeFalse();
        (await TryHandleAsync(context, new BadHttpRequestException("too large", StatusCodes.Status413PayloadTooLarge))).Should().BeFalse();
    }

    [Fact]
    public async Task The_handler_leaves_a_request_without_a_covered_endpoint_alone()
    {
        var refused = new BadHttpRequestException("refused", StatusCodes.Status400BadRequest, Refusal());

        (await TryHandleAsync(RequestContext(string.Empty), refused)).Should().BeFalse("no feature");
        (await TryHandleAsync(RequestContext(string.Empty, endpoint: null), refused)).Should().BeFalse("no endpoint");
        (await TryHandleAsync(RequestContext(string.Empty, new Endpoint(null, EndpointMetadataCollection.Empty, "uncovered")), refused))
            .Should().BeFalse("an endpoint the convention does not cover");
    }

    /// <summary>
    /// A code found down the chain, under an exception that is not the serializer's, on a <see cref="JsonException"/> that
    /// no serializer gave a path, is listed under the root; a route value with route values taken away is absent.
    /// </summary>
    [Fact]
    public async Task The_handler_reads_the_code_wherever_the_chain_holds_it_and_the_route_values_wherever_they_are()
    {
        var context = RequestContext(string.Empty, Covered());
        var refused = new BadHttpRequestException("refused", StatusCodes.Status400BadRequest, new InvalidOperationException("wrapped", Refusal()));

        (await TryHandleAsync(context, refused)).Should().BeTrue();

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.Body.Position = 0;
        var problem = JsonDocument.Parse(context.Response.Body).RootElement;
        problem.GetProperty(ValueObjectProblemDetails.ExtensionName).Deserialize<Dictionary<string, string>>().Should().Equal(
            new Dictionary<string, string>
            {
                [string.Empty] = ValueObjectErrorCodes.TooShort,
                ["quantity"] = ValueObjectErrorCodes.Required,
            });
    }

    private static string Lines(Quantity quantity) => quantity.ToString();

    private static int Plain(int n, CancellationToken cancellationToken) => n;

    private static ValueObjectJsonException Refusal()
        => new("The value is too short.", typeof(Iban), ValueObjectErrorCodes.TooShort);

    /// <summary>An endpoint the convention covers, binding a quantity from the route.</summary>
    private static Endpoint Covered()
    {
        var metadata = new ValueObjectBindingMetadata();
        metadata.Describe(
            [new ValueObjectParameter("quantity", ValueObjectParameterSource.Route, false, false, false, ValueObjectTextCheck.For.Instance.Visit<Quantity, short>())],
            skipsEmptyText: false);

        return new Endpoint(null, new EndpointMetadataCollection(metadata), "covered");
    }

    private static ServiceProvider Services() => new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();

    private static DefaultHttpContext RequestContext(string query)
    {
        var context = new DefaultHttpContext { RequestServices = Services() };
        context.Request.QueryString = new QueryString(query);
        context.Response.Body = new MemoryStream();

        return context;
    }

    /// <summary>A request as the exception handler middleware hands it over: the endpoint in the feature, no route values.</summary>
    private static DefaultHttpContext RequestContext(string query, Endpoint? endpoint)
    {
        var context = RequestContext(query);
        context.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature { Error = new InvalidOperationException(), Endpoint = endpoint });

        return context;
    }

    private static ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception)
        => new ValueObjectBadRequestExceptionHandler().TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

    /// <summary>An endpoint builder that is no route's.</summary>
    private sealed class PatternlessEndpointBuilder : EndpointBuilder
    {
        public override Endpoint Build() => throw new NotSupportedException();
    }

    /// <summary>The description a binder gives of a parameter it parses from text.</summary>
    private sealed class TextBinding(string name, ParameterInfo parameter, bool isOptional) : IParameterBindingMetadata
    {
        public string Name => name;

        public bool HasTryParse => true;

        public bool HasBindAsync => false;

        public ParameterInfo ParameterInfo => parameter;

        public bool IsOptional => isOptional;
    }
}
