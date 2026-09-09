namespace AdCodicem.ValueObjects.Annotations;

/// <summary>
/// Whether a value object accepts arbitrary valid values or only an enumerated set.
/// </summary>
public enum ValueSetKind
{
    /// <summary>
    /// Any value satisfying the declared rules is accepted. Declared known values are convenient constants only.
    /// </summary>
    Open = 0,

    /// <summary>
    /// Only the values declared through <see cref="KnownValueAttribute"/> are accepted.
    /// </summary>
    /// <remarks>
    /// Membership is tested against a generated frozen lookup, and the known values are emitted as the
    /// <c>enum</c> keyword of the OpenAPI schema. This is how reference-data codes are modelled without paying
    /// for a real C# enumeration, which can carry neither validation nor a stable wire format.
    /// </remarks>
    Closed = 1,
}
