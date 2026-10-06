using AdCodicem.ValueObjects.AspNetCore.Http.Binding;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AdCodicem.ValueObjects.AspNetCore.Http;

/// <summary>
/// Registers what answers a value object a minimal API could not bind when the binder throws instead of answering.
/// </summary>
public static class ValueObjectHttpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the exception handler that answers a refused value object with problem details carrying the code of
    /// its rule when <c>RouteHandlerOptions.ThrowOnBadRequest</c> is on.
    /// </summary>
    /// <param name="services">The application's services.</param>
    /// <returns>The same services, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// With <c>ThrowOnBadRequest</c> on, the default in Development, a minimal API throws a
    /// <see cref="Microsoft.AspNetCore.Http.BadHttpRequestException"/> where it would answer a bare 400, before any
    /// endpoint filter runs. The handler takes that exception, on an endpoint covered by
    /// <see cref="ValueObjectEndpointConventionBuilderExtensions.WithValueObjectProblemDetails"/> alone, and answers 400
    /// with the problem details the filter writes: the route, query and header values of the endpoint parsed again, and
    /// the value object a request body refused, under its JSON path, with the code
    /// <see cref="ValueObjectErrors.TryGetCode"/> reads from the exception the serializer threw. It never copies the
    /// framework's message, which names the refused text; a key a dictionary keyed by a value object refuses is listed
    /// under its JSON path, which holds the key, as MVC lists it. Any other exception, a 400 no value object caused among
    /// them, is left to the next handler, a body the serializer cannot read at all included, even beside a refused route,
    /// query or header value: with <c>ThrowOnBadRequest</c> off, the framework answers such a body before any filter
    /// runs, and the handler keeps that answer.
    /// </para>
    /// <para>
    /// It is an <see cref="IExceptionHandler"/>, which the exception handler middleware calls: register
    /// <c>AddProblemDetails()</c> and call <c>app.UseExceptionHandler()</c> wherever <c>ThrowOnBadRequest</c> is on.
    /// Handlers run in the order they were registered, so register it before a handler that answers every exception.
    /// Registering it twice registers it once.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddValueObjectHttpProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionHandler, ValueObjectBadRequestExceptionHandler>());

        return services;
    }
}
