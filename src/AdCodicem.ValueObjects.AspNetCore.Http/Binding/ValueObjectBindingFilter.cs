using System.CodeDom.Compiler;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AdCodicem.ValueObjects.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace AdCodicem.ValueObjects.AspNetCore.Http.Binding;

/// <summary>
/// The convention <see cref="ValueObjectEndpointConventionBuilderExtensions.WithValueObjectProblemDetails"/> adds, and
/// the endpoint filter it puts on an endpoint that may bind a value object from text.
/// </summary>
internal static class ValueObjectBindingFilter
{
    /// <summary>
    /// What the Request Delegate Generator names itself in the <see cref="GeneratedCodeAttribute"/> it adds to the
    /// metadata of every endpoint it writes the binding of, followed by its assembly's version.
    /// </summary>
    internal const string RequestDelegateGenerator = "Microsoft.AspNetCore.Http.RequestDelegateGenerator";

    private const string RegisteredOnly =
        "ValueObjectRegistry.TryResolve reflects only for a value object nothing registered: one written by hand, or a "
        + "construction of a generic one. A generated value object registers itself statically. Without dynamic code, as "
        + "under native AOT, it is not called, and such a parameter is left unexplained.";

    private const string Unwritable =
        "WithValueObjectProblemDetails() answers a refused value object with problem details the HTTP JSON options of "
        + "this application cannot serialize: no resolver of theirs knows HttpValidationProblemDetails or JsonElement, as "
        + "in a native binary, where reflection-based serialization is off. Call builder.Services.AddProblemDetails(), "
        + "which chains the framework's serializer context for problem details into them, or list both types in a "
        + "JsonSerializerContext chained into ConfigureHttpJsonOptions.";

    /// <summary>
    /// Covers an endpoint: marks it for the exception handler, and adds the filter when its handler may bind a value
    /// object from text.
    /// </summary>
    /// <param name="endpoint">The endpoint being built.</param>
    /// <exception cref="InvalidOperationException">
    /// The HTTP JSON options cannot serialize the problem details the endpoint would answer with.
    /// </exception>
    /// <remarks>
    /// A convention runs before the binder describes the parameters, which the filter factory reads once it does. The
    /// handler's method is in the metadata from the start, and an endpoint whose parameters hold no value object, no
    /// array of them and no <c>[AsParameters]</c> type gets no filter factory, so that the framework builds it as it
    /// would without the package. The marker is added all the same: a body the exception handler explains needs none of
    /// the parameters.
    /// </remarks>
    public static void Apply(EndpointBuilder endpoint)
    {
        if (endpoint.Metadata.OfType<ValueObjectBindingMetadata>().Any())
        {
            return;
        }

        EnsureProblemsCanBeWritten(endpoint.ApplicationServices);

        var metadata = new ValueObjectBindingMetadata();
        endpoint.Metadata.Add(metadata);

        if (endpoint.Metadata.OfType<MethodInfo>().FirstOrDefault() is { } handler && MayBindFromText(handler))
        {
            endpoint.FilterFactories.Add((_, next) => Create(endpoint, metadata, next));
        }
    }

    /// <summary>
    /// Describes the value objects the endpoint binds from text, and wraps the next delegate in the filter when there is
    /// one.
    /// </summary>
    /// <param name="endpoint">The endpoint, whose metadata the binder has now filled.</param>
    /// <param name="metadata">The marker the convention added, which the exception handler reads.</param>
    /// <param name="next">The next delegate of the pipeline.</param>
    /// <returns>The filter, or <paramref name="next"/> itself when no parameter binds a value object from text.</returns>
    public static EndpointFilterDelegate Create(EndpointBuilder endpoint, ValueObjectBindingMetadata metadata, EndpointFilterDelegate next)
    {
        var pattern = (endpoint as RouteEndpointBuilder)?.RoutePattern;
        var parameters = new List<ValueObjectParameter>();
        foreach (var binding in endpoint.Metadata.OfType<IParameterBindingMetadata>())
        {
            if (Describe(binding, pattern) is { } parameter)
            {
                parameters.Add(parameter);
            }
        }

        metadata.Describe(
            [.. parameters],
            endpoint.Metadata.OfType<GeneratedCodeAttribute>().Any(static attribute =>
                attribute.Tool is { } tool && tool.StartsWith(RequestDelegateGenerator, StringComparison.Ordinal)));

        if (parameters.Count == 0)
        {
            return next;
        }

        return invocation => invocation.HttpContext.Response.StatusCode == StatusCodes.Status400BadRequest
            ? Explain(invocation, metadata, next)
            : next(invocation);
    }

    /// <summary>
    /// Answers a binding failure with the refusals of the value objects, or leaves it to the framework when none of
    /// them caused it.
    /// </summary>
    private static ValueTask<object?> Explain(EndpointFilterInvocationContext invocation, ValueObjectBindingMetadata metadata, EndpointFilterDelegate next)
    {
        var httpContext = invocation.HttpContext;
        var refusals = new ValueObjectRefusals();
        metadata.Collect(httpContext.Request, httpContext.Request.RouteValues, refusals);

        return refusals.Count == 0
            ? next(invocation)
            : ValueTask.FromResult<object?>(refusals.ToProblem(httpContext));
    }

    /// <summary>
    /// Fails the build of the endpoint, as <c>UseExceptionHandler()</c> fails without <c>AddProblemDetails()</c>, when
    /// the HTTP JSON options cannot serialize the validation problem and the <see cref="JsonElement"/> of its codes,
    /// rather than answer each refusal with a 500 where the framework answers a 400.
    /// </summary>
    /// <remarks>
    /// The response is written with those options, read as the framework reads them. Reflection-based serialization
    /// writes any type; without it, as in a native binary, they know a type only through a serializer context, and
    /// <c>AddProblemDetails()</c> chains the framework's own for problem details into them.
    /// </remarks>
    private static void EnsureProblemsCanBeWritten(IServiceProvider services)
    {
        var options = (services.GetService<IOptions<HttpJsonOptions>>()?.Value ?? new HttpJsonOptions()).SerializerOptions;
        if (!options.TryGetTypeInfo(typeof(HttpValidationProblemDetails), out _) || !options.TryGetTypeInfo(typeof(JsonElement), out _))
        {
            throw new InvalidOperationException(Unwritable);
        }
    }

    /// <summary>
    /// Tells whether a handler may bind a value object from text: one of its parameters is a value object, an array of
    /// them, or an <c>[AsParameters]</c> type whose members the binder reads in turn.
    /// </summary>
    private static bool MayBindFromText(MethodInfo handler)
        => handler.GetParameters().Any(static parameter =>
            parameter.IsDefined(typeof(AsParametersAttribute), inherit: false)
            || ValueObjectRegistry.IsValueObject(ElementOf(parameter.ParameterType)));

    /// <summary>
    /// Describes a parameter the binder parses from text, or returns <see langword="null"/> for one it reads otherwise or
    /// that is no value object.
    /// </summary>
    /// <remarks>
    /// The rules are the binder's, in its order. An attribute naming the source comes first: the reflection-based
    /// binding does not mark such a parameter as parsed from text, where the Request Delegate Generator does. A body, a
    /// form and anything the binder does not parse, a service among them, are left out; otherwise a parameter binds from
    /// the route when the route has a parameter of its name, and from the query string when it has not, as an array
    /// always does.
    /// </remarks>
    private static ValueObjectParameter? Describe(IParameterBindingMetadata binding, RoutePattern? pattern)
    {
        var type = binding.ParameterInfo.ParameterType;
        var element = ElementOf(type);
        var isArray = element != type;
        if (!TryDescribe(element, out var descriptor))
        {
            return null;
        }

        var attributes = binding.ParameterInfo.GetCustomAttributes().ToArray();
        ValueObjectParameterSource source;
        string name;
        if (attributes.OfType<IFromRouteMetadata>().FirstOrDefault() is { } route)
        {
            (source, name) = (ValueObjectParameterSource.Route, route.Name ?? binding.Name);
        }
        else if (attributes.OfType<IFromQueryMetadata>().FirstOrDefault() is { } query)
        {
            (source, name) = (ValueObjectParameterSource.Query, query.Name ?? binding.Name);
        }
        else if (attributes.OfType<IFromHeaderMetadata>().FirstOrDefault() is { } header)
        {
            (source, name) = (ValueObjectParameterSource.Header, header.Name ?? binding.Name);
        }
        else if (!binding.HasTryParse || attributes.OfType<IFromFormMetadata>().Any())
        {
            // A body is read by the serializer, never parsed from text; a form field is parsed, and left for later.
            return null;
        }
        else
        {
            name = binding.Name;
            source = !isArray && pattern?.GetParameter(name) is not null
                ? ValueObjectParameterSource.Route
                : ValueObjectParameterSource.Query;
        }

        // An array is read from the query string alone: a route value is one text, and a header array is split by rules of
        // its own.
        if (isArray && source != ValueObjectParameterSource.Query)
        {
            return null;
        }

        return new ValueObjectParameter(
            name,
            source,
            binding.IsOptional,
            isArray,
            skipsEmptyElements: isArray && Nullable.GetUnderlyingType(element) is not null,
            descriptor.Accept(ValueObjectTextCheck.For.Instance));
    }

    /// <summary>Gets the element type of an array, or the type itself.</summary>
    private static Type ElementOf(Type type) => type.IsArray ? type.GetElementType()! : type;

    /// <summary>
    /// Finds the descriptor of a value object, registered or, where the runtime supports dynamic code, described by
    /// reflection.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = RegisteredOnly)]
    private static bool TryDescribe(Type type, [NotNullWhen(true)] out ValueObjectDescriptor? descriptor)
        => ValueObjectRegistry.TryGet(type, out descriptor)
           || (RuntimeFeature.IsDynamicCodeSupported && ValueObjectRegistry.TryResolve(type, out descriptor));
}
