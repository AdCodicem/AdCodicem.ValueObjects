using System.Collections.Immutable;

namespace AdCodicem.ValueObjects.Metadata;

/// <summary>
/// The declarative constraints of a value object, captured once at compile time.
/// </summary>
/// <remarks>
/// The generator fills this from the <c>ValueObjectAttribute</c> so that the rules enforced by
/// <c>Validate</c> and the rules published in the OpenAPI schema or applied to an EF Core column can never
/// drift apart: they all read the same instance.
/// </remarks>
public sealed record ValueObjectSchema
{
    /// <summary>
    /// Gets the schema of a value object that declares no constraint.
    /// </summary>
    public static ValueObjectSchema Unconstrained { get; } = new();

    /// <summary>
    /// Gets the regular expression the value must match, if any.
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Gets the minimum accepted length, if any.
    /// </summary>
    public int? MinLength { get; init; }

    /// <summary>
    /// Gets the maximum accepted length, if any.
    /// </summary>
    /// <remarks>Used by the EF Core integration to size the mapped column.</remarks>
    public int? MaxLength { get; init; }

    /// <summary>
    /// Gets the inclusive lower bound in invariant culture, if any.
    /// </summary>
    public string? Minimum { get; init; }

    /// <summary>
    /// Gets the inclusive upper bound in invariant culture, if any.
    /// </summary>
    public string? Maximum { get; init; }

    /// <summary>
    /// Gets the OpenAPI <c>format</c> keyword for the value, if any.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Gets the human-readable description of the value object, if any.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets an example value, if any.
    /// </summary>
    public string? Example { get; init; }

    /// <summary>
    /// Gets a value indicating whether only <see cref="KnownValues"/> are accepted.
    /// </summary>
    public bool IsClosedValueSet { get; init; }

    /// <summary>
    /// Gets the declared known underlying values, boxed, in declaration order.
    /// </summary>
    /// <remarks>Surfaced as the <c>enum</c> keyword of the OpenAPI schema.</remarks>
    public ImmutableArray<object> KnownValues { get; init; } = [];
}
