using AdCodicem.ValueObjects.Shared;

namespace AdCodicem.ValueObjects.Testing.Data;

/// <summary>
/// Thrown when a sampler cannot draw a value its type accepts: no candidate, drawn from its schema or from the generator
/// registered for it, passed its rules, and it declares no example they accept.
/// </summary>
/// <remarks>
/// <para>
/// It is a failure of test set-up, not a value refused at a boundary: it carries the code of the last refusal in
/// <see cref="ErrorCode"/>, and nothing in <see cref="Exception.Data"/>, so <c>ValueObjectErrors.TryGetCode</c> does not
/// read it as a refusal. Its message never holds a candidate value.
/// </para>
/// <para>
/// Its last sentence names the registration that gives the sampler a generator: the one to add, or the one whose values
/// the type refused. A library drawing through the sampler writes it in its own terms by throwing it with its own
/// registration.
/// </para>
/// </remarks>
public sealed class ValueObjectSamplingException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectSamplingException"/> class.
    /// </summary>
    /// <param name="valueObjectType">The value object no value could be drawn for.</param>
    /// <param name="attempts">How many candidates were tried.</param>
    /// <param name="errorCode">The code of the rule that refused the last candidate, or <see langword="null"/> when none was drawn.</param>
    /// <param name="registration">The registration of a generator, as code: <c>options.Use&lt;EvenCode, string&gt;(random =&gt; ...)</c>.</param>
    /// <param name="fromGenerator">
    /// Whether the candidates came from a generator registered for the type, whose values its rules refused, rather than
    /// from its schema.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="valueObjectType"/> or <paramref name="registration"/> is <see langword="null"/>.</exception>
    public ValueObjectSamplingException(Type valueObjectType, int attempts, string? errorCode, string registration, bool fromGenerator = false)
        : base(Describe(valueObjectType, attempts, errorCode, registration, fromGenerator))
    {
        ValueObjectType = valueObjectType;
        Attempts = attempts;
        ErrorCode = errorCode;
        FromGenerator = fromGenerator;
    }

    /// <summary>
    /// Gets the value object no value could be drawn for.
    /// </summary>
    public Type ValueObjectType { get; }

    /// <summary>
    /// Gets how many candidates were tried.
    /// </summary>
    public int Attempts { get; }

    /// <summary>
    /// Gets the code of the rule that refused the last candidate, or <see langword="null"/> when no candidate was drawn at
    /// all: an underlying type the sampler draws no value of, with no known value, or lengths and a pattern no string it
    /// built could meet.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Gets a value indicating whether the candidates came from a generator registered for the type rather than from its
    /// schema: the generator gives values the type refuses.
    /// </summary>
    public bool FromGenerator { get; }

    private static string Describe(Type valueObjectType, int attempts, string? errorCode, string registration, bool fromGenerator)
    {
        ArgumentNullException.ThrowIfNull(valueObjectType);
        ArgumentNullException.ThrowIfNull(registration);

        var name = TypeNames.Of(valueObjectType);
        var refused = errorCode is null ? "was refused" : $"was refused ({errorCode})";
        if (fromGenerator)
        {
            return $"Could not draw a value of '{name}' its rules accept: the last of {attempts} candidates its registered "
                + $"generator gave {refused}, and it declares no example its rules accept. Make the generator registered with "
                + $"{registration} give values its rules accept.";
        }

        var cause = errorCode is null
            ? $"its schema gave no candidate in {attempts} attempts"
            : $"the last of {attempts} candidates drawn from its schema {refused}";
        return $"Could not draw a value of '{name}' its rules accept: {cause}, and it declares no example its rules accept. "
            + $"Register a generator of its underlying value: {registration}.";
    }
}
