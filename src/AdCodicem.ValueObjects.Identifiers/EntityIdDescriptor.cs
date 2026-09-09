using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// Runtime description of one entity identifier type, reachable from its prefix alone.
/// </summary>
/// <remarks>
/// The delegate it carries operates on boxed values, which is acceptable for the same reason it is on
/// <see cref="ValueObjectDescriptor"/>: this is the path taken by callers that only know a prefix at run time —
/// a webhook dispatcher, a deep link, an audit trail. Request paths that know their type go through the static
/// members of <see cref="IEntityId{TSelf}"/> and stay fully typed.
/// </remarks>
public sealed class EntityIdDescriptor
{
    private EntityIdDescriptor(
        Type valueObjectType,
        string prefix,
        IdGranularity granularity,
        int length,
        BoxedTryParse tryParse)
    {
        ValueObjectType = valueObjectType;
        Prefix = prefix;
        Granularity = granularity;
        Length = length;
        TryParse = tryParse;
    }

    /// <summary>
    /// Gets the identifier type.
    /// </summary>
    public Type ValueObjectType { get; }

    /// <summary>
    /// Gets the prefix the type claims, without its trailing separator.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// Gets the width of the time bucket heading the body.
    /// </summary>
    public IdGranularity Granularity { get; }

    /// <summary>
    /// Gets the exact length of an identifier of this type, which is also the width of its database column.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the non-throwing text parser, producing a boxed identifier.
    /// </summary>
    public BoxedTryParse TryParse { get; }

    /// <summary>
    /// Builds a descriptor, resolving every operation statically.
    /// </summary>
    /// <typeparam name="TSelf">Identifier type.</typeparam>
    /// <returns>The descriptor.</returns>
    public static EntityIdDescriptor For<TSelf>()
        where TSelf : struct, IEntityId<TSelf>
        => new(
            typeof(TSelf),
            TSelf.Prefix,
            TSelf.Granularity,
            TSelf.Length,
            (ReadOnlySpan<char> text, IFormatProvider? provider, out object? result, out ValidationResult validation) =>
            {
                if (TSelf.TryParse(text, provider, out var parsed, out validation))
                {
                    result = parsed;
                    return true;
                }

                result = null;
                return false;
            });
}
