using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.Wolverine.Http.Binding;
using WolverineHttpOptions = Wolverine.Http.WolverineHttpOptions;

namespace AdCodicem.ValueObjects.Wolverine.Http;

/// <summary>
/// Binds the value objects of Wolverine.Http endpoints from route values, query strings, headers and form fields through
/// their rules.
/// </summary>
public static class ValueObjectWolverineHttpOptionsExtensions
{
    /// <summary>
    /// Binds every value-object parameter of these endpoints from its route value, query value, header or form field
    /// through its rules, and answers a request refused for any of them with problem details carrying the rule codes.
    /// </summary>
    /// <param name="options">The options <c>MapWolverineEndpoints</c> hands its callback.</param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Call it inside the callback of <c>MapWolverineEndpoints</c>, the one place Wolverine accepts a parameter strategy. A
    /// parameter of type <c>TSelf</c>, <c>TSelf?</c>, <c>TSelf[]</c>, <c>List&lt;TSelf&gt;</c>, <c>IList&lt;TSelf&gt;</c>,
    /// <c>IReadOnlyList&lt;TSelf&gt;</c> or <c>IEnumerable&lt;TSelf&gt;</c>, of an endpoint or of one of its middleware
    /// methods, is read from where Wolverine reads it: a form field under <c>[FromForm]</c>, a route value, a header under
    /// <c>[FromHeader]</c>, and the query string otherwise, a collection from the query string or a form alone. Its text
    /// goes through <c>TSelf.TryParse</c>, empty text included; a value that cannot be <see langword="null"/>, absent, is
    /// refused as required, unless the parameter has a default value.
    /// </para>
    /// <para>
    /// Every refusal of a request is answered at once, before any middleware method or the handler runs, with the
    /// validation problem a minimal API answers, the codes under <c>errorCodes</c> and recorded on the request
    /// (<see cref="ValueObjectProblemDetails.GetErrorCodes"/>); a refused route value is a 400, where Wolverine answers 404.
    /// <c>[AsParameters]</c> members and a JSON body stay Wolverine's, and so does a parameter of an <c>After</c> or
    /// <c>Finally</c> method reading a value no value object of the endpoint reads; one reading the same value receives the
    /// package's. In static mode, the code <c>codegen write</c> wrote before the package was installed or upgraded keeps
    /// running as it was: write it again.
    /// </para>
    /// <para>
    /// Called again, it keeps the binding it installed, and the messages a call named last.
    /// </para>
    /// </remarks>
    public static WolverineHttpOptions UseValueObjects(this WolverineHttpOptions options)
        => Install(options, messages: null);

    /// <summary>The same, with the messages the application's resource gives each code.</summary>
    /// <param name="options">The options <c>MapWolverineEndpoints</c> hands its callback.</param>
    /// <param name="configure">Names the resource the messages are read from.</param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// The messages are looked up in the current UI culture through the localizer of the request's services, which
    /// <c>builder.Services.AddLocalization()</c> registers, and <c>UseRequestLocalization()</c> sets the culture of the
    /// request; without a resource of the code, a refusal keeps the English message of its rule
    /// (<see cref="ValueObjectMessageOptions"/>).
    /// </remarks>
    public static WolverineHttpOptions UseValueObjects(this WolverineHttpOptions options, Action<ValueObjectMessageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var messages = new ValueObjectMessageOptions();
        configure(messages);

        return Install(options, messages);
    }

    private static WolverineHttpOptions Install(WolverineHttpOptions options, ValueObjectMessageOptions? messages)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Policies.OfType<ValueObjectRefusalPolicy>().FirstOrDefault() is { } installed)
        {
            installed.Messages = messages ?? installed.Messages;
            return options;
        }

        var policy = new ValueObjectRefusalPolicy { Messages = messages };
        options.AddParameterHandlingStrategy(new ValueObjectParameterStrategy(policy));
        options.Policies.Add(policy);

        return options;
    }
}
