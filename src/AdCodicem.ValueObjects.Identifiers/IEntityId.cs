using System.Diagnostics.CodeAnalysis;

namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// Non-generic marker implemented by every entity identifier.
/// </summary>
/// <remarks>
/// Reflection-driven integrations test for this interface for the same reason they test for
/// <see cref="IValueObject"/>: one <c>IsAssignableTo</c> is far cheaper than walking a generic interface list.
/// It also draws the line that keeps <see cref="AnyEntityId"/> out of persistence, since the polymorphic type
/// deliberately implements neither this nor <see cref="IValueObject"/>.
/// </remarks>
public interface IEntityId : IValueObject<string>
{
    /// <summary>
    /// Gets the prefix this identifier carries, without its trailing separator.
    /// </summary>
    /// <remarks>Reserved for reflection-driven code paths; prefer the static <c>Prefix</c> property.</remarks>
    string PrefixValue { get; }

    /// <summary>
    /// Gets the width of the time bucket heading the body.
    /// </summary>
    /// <remarks>Reserved for reflection-driven code paths; prefer the static <c>Granularity</c> property.</remarks>
    IdGranularity GranularityValue { get; }
}

/// <summary>
/// The full contract of a public entity identifier.
/// </summary>
/// <typeparam name="TSelf">The identifier type itself.</typeparam>
/// <remarks>
/// Implementations are produced by the source generator from <see cref="EntityIdAttribute"/>. Everything the
/// contract adds over <see cref="IValueObject{TSelf, TValue}"/> is static, so a caller holding only a type
/// parameter can mint identifiers without reflection and without a factory to inject.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "New() is the name every caller reaches for, and the alternatives all stutter against the "
                    + "type name. The rule guards Visual Basic consumers, who would write New[](); that trade "
                    + "is worth making for the member domain code touches most.")]
public interface IEntityId<TSelf> : IEntityId, IValueObject<TSelf, string>
    where TSelf : struct, IEntityId<TSelf>
{
    /// <summary>
    /// Gets the prefix identifiers of this type carry, without its trailing separator.
    /// </summary>
    static abstract string Prefix { get; }

    /// <summary>
    /// Gets the width of the time bucket heading the body.
    /// </summary>
    static abstract IdGranularity Granularity { get; }

    /// <summary>
    /// Gets the exact length of an identifier of this type, which is also the width of its database column.
    /// </summary>
    static abstract int Length { get; }

    /// <summary>
    /// Mints a new identifier from the ambient clock and entropy source.
    /// </summary>
    /// <returns>A fresh identifier, canonical and valid by construction.</returns>
    /// <remarks>
    /// Both sources resolve through <see cref="ValueObjectIds"/>, so a test can substitute them for the current
    /// execution flow without an injected factory reaching every aggregate that creates an entity.
    /// </remarks>
    static abstract TSelf New();

    /// <summary>
    /// Mints a new identifier from explicit sources.
    /// </summary>
    /// <param name="timeProvider">Clock supplying the time bucket.</param>
    /// <param name="entropy">Source of the random part.</param>
    /// <returns>A fresh identifier, canonical and valid by construction.</returns>
    static abstract TSelf New(TimeProvider timeProvider, IdEntropySource entropy);

    /// <inheritdoc />
    string IEntityId.PrefixValue => TSelf.Prefix;

    /// <inheritdoc />
    IdGranularity IEntityId.GranularityValue => TSelf.Granularity;
}
