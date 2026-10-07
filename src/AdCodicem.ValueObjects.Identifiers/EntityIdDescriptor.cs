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
    private readonly TypedAccess _access;

    private EntityIdDescriptor(
        Type valueObjectType,
        string prefix,
        IdGranularity granularity,
        int length,
        BoxedTryParse tryParse,
        TypedAccess access)
    {
        ValueObjectType = valueObjectType;
        Prefix = prefix;
        Granularity = granularity;
        Length = length;
        TryParse = tryParse;
        _access = access;
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
            },
            TypedAccess<TSelf>.Instance);

    /// <summary>
    /// Hands the identifier type to a visitor, closed at compile time where the descriptor was built.
    /// </summary>
    /// <typeparam name="TResult">What the visitor builds.</typeparam>
    /// <param name="visitor">The visitor.</param>
    /// <returns>What the visitor built for the identifier type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="visitor"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Every descriptor is built by <see cref="For{TSelf}"/>, the generated registration's included, so every descriptor
    /// <see cref="EntityIdRegistry"/> holds can be visited, without reflection and under native AOT.
    /// </remarks>
    public TResult Accept<TResult>(IEntityIdVisitor<TResult> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return _access.Accept(visitor);
    }

    /// <summary>
    /// Keeps the type of an identifier, which no field of a non-generic class can hold, behind a generic virtual method
    /// that hands it to a visitor.
    /// </summary>
    private abstract class TypedAccess
    {
        public abstract TResult Accept<TResult>(IEntityIdVisitor<TResult> visitor);
    }

    /// <summary>
    /// The type of one identifier, created where it is known at compile time, in <see cref="For{TSelf}"/>.
    /// </summary>
    /// <typeparam name="TSelf">Identifier type.</typeparam>
    private sealed class TypedAccess<TSelf> : TypedAccess
        where TSelf : struct, IEntityId<TSelf>
    {
        public static readonly TypedAccess<TSelf> Instance = new();

        public override TResult Accept<TResult>(IEntityIdVisitor<TResult> visitor) => visitor.Visit<TSelf>();
    }
}
