using System.Collections.Immutable;

namespace AdCodicem.ValueObjects.Metadata;

/// <summary>
/// The declarative constraints of a value object, captured once at compile time.
/// </summary>
/// <remarks>
/// <para>
/// The generator fills this from the <c>ValueObjectAttribute</c> so that the rules enforced by
/// <c>Validate</c> and the rules published in the OpenAPI schema or applied to an EF Core column can never
/// drift apart: they all read the same instance.
/// </para>
/// <para>
/// That instance is the value object's <see cref="IValueObject{TSelf, TValue}.Schema"/>, which generic code reads through
/// its type parameter and the registration hands the descriptor. A value object written by hand builds its own, stating
/// the rules its <c>Validate</c> enforces.
/// </para>
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
    /// Gets an example value, boxed, if any.
    /// </summary>
    /// <remarks>
    /// The generator holds here the underlying value of the example the value object declares through
    /// <see cref="IValueObjectExample{TSelf}"/>, as <see cref="KnownValues"/> holds its known values, and the schemas
    /// write it as the type writes it in JSON. A schema built by hand may hold the text of the value instead, which they
    /// read in the invariant culture as the type parses it.
    /// </remarks>
    public object? Example { get; init; }

    /// <summary>
    /// Gets a value indicating whether only <see cref="KnownValues"/> are accepted.
    /// </summary>
    public bool IsClosedValueSet { get; init; }

    /// <summary>
    /// Gets the declared known underlying values, boxed, in declaration order.
    /// </summary>
    /// <remarks>
    /// Surfaced as the <c>enum</c> keyword of the OpenAPI schema. <see cref="KnownValueDetails"/> lists the same values
    /// with their names and descriptions.
    /// </remarks>
    public ImmutableArray<object> KnownValues { get; init; } = [];

    /// <summary>
    /// Gets the declared known values with the name and the description each was declared with, in declaration order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The generator lists here the values of <see cref="KnownValues"/>, in the same order, each the same value, with
    /// the name of the static property it creates for it and the <c>Description</c> its attribute gives, if any. The
    /// OpenAPI integration publishes the names of a closed value set beside its <c>enum</c>, as
    /// <c>x-enum-varnames</c>, <c>x-enumNames</c> and <c>x-ms-enum</c>, which client generators name the members of
    /// their enumeration after.
    /// </para>
    /// <para>
    /// A schema built by hand fills it as well, listing the values of <see cref="KnownValues"/> in the same order, each
    /// equal to its counterpart. The OpenAPI integration publishes no name otherwise, rather than one beside the wrong
    /// value, and the contract kit of <c>AdCodicem.ValueObjects.Testing</c> fails a schema that leaves it empty beside
    /// known values, or out of step with them.
    /// </para>
    /// </remarks>
    public ImmutableArray<KnownValueInfo> KnownValueDetails { get; init; } = [];

    /// <summary>
    /// Gets how the value object compares two values of its text, if the schema states it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The generator states here the <c>Comparison</c> of the <c>ValueObjectAttribute</c> on every value object over
    /// text, <see cref="StringComparison.Ordinal"/> where the attribute declares none, and leaves it
    /// <see langword="null"/> on a value object over any other type. Equality, ordering and hashing compare this way, and
    /// so does the lookup of a closed set's <see cref="KnownValues"/>.
    /// </para>
    /// <para>
    /// A check written from the known values alone and compared exactly, the <c>enum</c> of the MongoDB collection
    /// validator or the <c>IN</c> of an Entity Framework Core CHECK constraint, reads it: a closed set compared
    /// <see cref="StringComparison.Ordinal"/> stores each value as its known value is spelled and gets one; a set compared
    /// as a culture compares may store another spelling of a known value, longer, shorter or of the same length, and gets
    /// none; a set compared <see cref="StringComparison.OrdinalIgnoreCase"/> gets one when its normalizer spells every
    /// other case back.
    /// </para>
    /// <para>
    /// A schema built by hand states the comparison its <c>Validate</c> looks the known values up with. Left
    /// <see langword="null"/>, those checks try the spellings a comparison ignoring case, or a culture's ignoring a soft
    /// hyphen, would take for a known value, and write one when the type stores none of them; a culture's comparison
    /// takes other spellings still, so a schema over such a set states it.
    /// </para>
    /// </remarks>
    public StringComparison? Comparison { get; init; }
}
