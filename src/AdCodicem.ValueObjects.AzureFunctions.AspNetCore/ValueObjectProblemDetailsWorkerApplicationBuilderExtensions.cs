using AdCodicem.ValueObjects.AspNetCore;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdCodicem.ValueObjects.AzureFunctions.AspNetCore;

/// <summary>
/// Registers the middleware that answers an HTTP trigger of the ASP.NET Core integration whose value objects the worker
/// could not bind with RFC 9457 problem details carrying their rule codes.
/// </summary>
public static class ValueObjectProblemDetailsWorkerApplicationBuilderExtensions
{
    /// <summary>
    /// Answers an HTTP trigger whose value objects the worker could not bind with problem details carrying their rule
    /// codes.
    /// </summary>
    /// <param name="builder">The worker's builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Call it after <c>ConfigureFunctionsWebApplication()</c>, or inside
    /// <c>ConfigureFunctionsWebApplication(worker =&gt; …)</c>: the integration's middleware, which writes the answer on the
    /// request, has to run outside this one. Called before, the failure keeps the worker's 500 and a warning is logged
    /// once. The built-in HTTP model, <c>HttpRequestData</c> without the integration, keeps the 500.
    /// </para>
    /// <para>
    /// A refused route or query value is answered 400 with the shape <c>WithValueObjectProblemDetails()</c> writes on a
    /// minimal API: <c>errors</c> holds the message of each rule, <c>errorCodes</c> its code, keyed by the parameter's
    /// name; a required value object with no binding data is <c>value_object.required</c>. A <c>[FromBody]</c> value
    /// object is answered too where the MVC package's <c>AddValueObjects()</c> reads the body, keyed by its JSON path. A
    /// failure no value object explains, a plain <see cref="int"/> among them, keeps the worker's 500. Each answer is
    /// logged once, with the parameters and their codes, never the values.
    /// </para>
    /// <para>Calling it twice adds the middleware once.</para>
    /// </remarks>
    public static IFunctionsWorkerApplicationBuilder UseValueObjectProblemDetails(this IFunctionsWorkerApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        Settings(builder);

        return builder;
    }

    /// <summary>
    /// Answers an HTTP trigger whose value objects the worker could not bind with problem details carrying their rule
    /// codes, with the messages the application's resource gives each code.
    /// </summary>
    /// <param name="builder">The worker's builder.</param>
    /// <param name="configure">Names the resource the messages are read from, through
    /// <see cref="ValueObjectMessageOptions.UseResource{TResource}"/>.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// The messages are read through the request's <c>IStringLocalizerFactory</c>, which
    /// <c>builder.Services.AddLocalization()</c> registers, in the culture <c>UseRequestLocalization()</c> chose when the
    /// application added it to the integration's pipeline, and otherwise in the one the application's
    /// <c>RequestLocalizationOptions</c> choose from the request, falling back to the current culture. A later call
    /// naming messages replaces those of an earlier one.
    /// </remarks>
    public static IFunctionsWorkerApplicationBuilder UseValueObjectProblemDetails(this IFunctionsWorkerApplicationBuilder builder, Action<ValueObjectMessageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var messages = new ValueObjectMessageOptions();
        configure(messages);
        Settings(builder).Messages = messages;

        return builder;
    }

    /// <summary>Adds the middleware the first time, and returns the settings it reads.</summary>
    private static ValueObjectProblemDetailsSettings Settings(IFunctionsWorkerApplicationBuilder builder)
    {
        if (builder.Services.FirstOrDefault(static service => service.ServiceType == typeof(ValueObjectProblemDetailsSettings))?.ImplementationInstance
            is ValueObjectProblemDetailsSettings existing)
        {
            return existing;
        }

        var settings = new ValueObjectProblemDetailsSettings();
        builder.Services.AddSingleton(settings);
        builder.UseMiddleware<ValueObjectProblemDetailsMiddleware>();

        return settings;
    }
}
