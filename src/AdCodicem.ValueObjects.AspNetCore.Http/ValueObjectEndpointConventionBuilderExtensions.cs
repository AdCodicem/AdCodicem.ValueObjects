using AdCodicem.ValueObjects.AspNetCore.Http.Binding;
using Microsoft.AspNetCore.Builder;

namespace AdCodicem.ValueObjects.AspNetCore.Http;

/// <summary>
/// Answers a value object a minimal API endpoint could not bind with problem details carrying the code of the rule it
/// broke.
/// </summary>
public static class ValueObjectEndpointConventionBuilderExtensions
{
    /// <summary>
    /// Answers a value object these endpoints could not bind with problem details carrying the code of the rule it
    /// broke.
    /// </summary>
    /// <typeparam name="TBuilder">The builder: a route group, one endpoint, or any other convention builder.</typeparam>
    /// <param name="builder">The endpoints to cover.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// A minimal API refuses a value object it cannot bind with a 400 that says nothing of why. On the endpoints this
    /// covers, the refusal is answered with the validation problem MVC writes, <c>errors</c> holding the message of each
    /// rule a value broke, and <see cref="ValueObjectProblemDetails.ExtensionName"/> mapping each member to the stable
    /// code of that rule, read from the same <c>TryParse</c> the binder ran. A route value, a query value and a header
    /// are covered, under the name they bind from, which <c>[FromRoute]</c>, <c>[FromQuery]</c> and <c>[FromHeader]</c>
    /// may set; so are the members of an <c>[AsParameters]</c> type and an array of value objects read from the query
    /// string, whose refused elements are listed under its name with the code of the first. A required value object
    /// that is absent is reported with <see cref="ValueObjectErrorCodes.Required"/>.
    /// </para>
    /// <para>
    /// A filter is added to each endpoint whose handler may bind a value object from text. When it runs, it reads one
    /// thing, the status the binder left: anything but a 400 goes on untouched, and only a refusal pays for the second
    /// parse. A 400 that no value object caused, a refused <see cref="int"/> or <see cref="Guid"/> parameter for
    /// instance, keeps the framework's answer: a parameter that is not a value object has no rule code to report, and
    /// is not listed beside one that is. The convention acts once on an endpoint it covers twice, through a route group
    /// and the endpoint itself.
    /// </para>
    /// <para>
    /// Empty text follows the binder that ran. The reflection-based binding parses it, so that <c>?country=</c> for a
    /// <c>CountryCode?</c> is reported with the rule that refused it. The Request Delegate Generator refuses no empty
    /// query text: it binds <see langword="null"/> to a nullable value object, and the default instance, which no rule
    /// has checked, to one that cannot be <see langword="null"/>, an array element included, so that nothing is reported
    /// there. It takes an empty header for an absent one, reported with <see cref="ValueObjectErrorCodes.Required"/> when
    /// the parameter is required.
    /// </para>
    /// <para>
    /// The binder refuses a request body before any filter runs, and the framework answers it itself. With
    /// <c>RouteHandlerOptions.ThrowOnBadRequest</c> on, the default in Development, the binder throws instead, before
    /// the filter runs, and the exception handler registered by
    /// <see cref="ValueObjectHttpServiceCollectionExtensions.AddValueObjectHttpProblemDetails"/> answers a body as well
    /// as a route, query or header value with the same problem details. A form field and an array read from headers are
    /// not covered. Under <c>AddValidation()</c>, whose filter runs before every other, a request that also breaks a
    /// DataAnnotations rule is answered with that rule alone.
    /// </para>
    /// <para>
    /// The filter closes the check of each value object over its type through the descriptor's visitor, so that it runs
    /// in a native binary. Under native AOT, a value object nothing registered, one written by hand or a construction of
    /// a generic one, cannot be described there, and its refusal is left unexplained: register it as the JSON package
    /// asks, <c>ValueObjectRegistry.Register&lt;TSelf, TValue&gt;(TSelf.Schema)</c> for one written by hand, with its
    /// generated converter for a construction.
    /// </para>
    /// <para>
    /// The response is written with the HTTP JSON options, which, without reflection-based serialization, as in a native
    /// binary, know a type only through a serializer context: register <c>AddProblemDetails()</c>, which chains the
    /// framework's own for problem details into them. Without it, or a context of the application's listing
    /// <c>HttpValidationProblemDetails</c> and <c>JsonElement</c>, the covered endpoints fail to build, when the first
    /// request needs them, with an <see cref="InvalidOperationException"/> naming the call, rather than answer each
    /// refusal with a 500 where the framework answers a 400.
    /// </para>
    /// <para>
    /// The messages are the English ones of the rules.
    /// <see cref="WithValueObjectProblemDetails{TBuilder}(TBuilder, Action{ValueObjectMessageOptions})"/> answers in the
    /// application's language.
    /// </para>
    /// </remarks>
    public static TBuilder WithValueObjectProblemDetails<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Add(ValueObjectBindingFilter.Apply);

        return builder;
    }

    /// <summary>
    /// Answers a value object these endpoints could not bind with problem details carrying the code of the rule it
    /// broke, and the message the application's resource gives that code.
    /// </summary>
    /// <typeparam name="TBuilder">The builder: a route group, one endpoint, or any other convention builder.</typeparam>
    /// <param name="builder">The endpoints to cover.</param>
    /// <param name="configure">Names the resource the messages are read from, through <see cref="ValueObjectMessageOptions.UseResource{TResource}"/>.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The endpoints are covered as <see cref="WithValueObjectProblemDetails{TBuilder}(TBuilder)"/> covers them, and
    /// <c>errors</c> holds, for each refused member, the message the resource holds under the key of the rule's code,
    /// <c>ValueObject.value_object.out_of_range</c>, looked up after <c>ValueObject.value_object.out_of_range.minimum</c>
    /// or <c>.maximum</c> for a type declaring one bound alone. It is formatted with the name the member binds from as
    /// <c>{0}</c>, the name of the value object for a request body, whose JSON path may hold a refused dictionary key, and
    /// the arguments the type's rules supply from <c>{1}</c>: a length, both bounds, the known values. The codes under
    /// <see cref="ValueObjectProblemDetails.ExtensionName"/> are the same in every language. A key the resource lacks
    /// keeps the English message.
    /// </para>
    /// <para>
    /// The resource is read through the request's <c>IStringLocalizerFactory</c>: register
    /// <c>builder.Services.AddLocalization()</c>, without which a refusal throws an <see cref="InvalidOperationException"/>
    /// naming it, and <c>app.UseRequestLocalization()</c>, so that the request chooses the culture. The current UI culture
    /// chooses the template and the current culture formats the arguments. The exception handler that answers with
    /// <c>ThrowOnBadRequest</c> on runs in the request's culture, whatever the order of <c>UseRequestLocalization()</c>
    /// and <c>UseExceptionHandler()</c>. A template quoting an argument its code does not supply throws its
    /// <see cref="FormatException"/>, which is not caught.
    /// </para>
    /// <para>
    /// The options belong to the endpoint: covered twice, through a route group and the endpoint itself, it takes those
    /// of the convention that runs last, the endpoint's own over its group's, and a call without options keeps the
    /// other's.
    /// </para>
    /// </remarks>
    public static TBuilder WithValueObjectProblemDetails<TBuilder>(this TBuilder builder, Action<ValueObjectMessageOptions> configure)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var messages = new ValueObjectMessageOptions();
        configure(messages);
        builder.Add(endpoint => ValueObjectBindingFilter.Apply(endpoint, messages));

        return builder;
    }
}
