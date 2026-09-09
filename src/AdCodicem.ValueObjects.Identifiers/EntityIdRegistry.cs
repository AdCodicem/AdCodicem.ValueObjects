using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// Process-wide index from a prefix to the identifier type that claims it.
/// </summary>
/// <remarks>
/// <para>
/// The generator emits a registration per assembly alongside the value object one, so the common case is a
/// lock-free dictionary hit with no reflection and no dynamic code — usable under native AOT.
/// </para>
/// <para>
/// Two types claiming the same prefix is refused rather than tolerated. Within one compilation the generator
/// catches it as <c>VO0016</c>; across assemblies only the second registration can notice, and letting it win
/// silently would make <see cref="AnyEntityId"/> resolve identifiers to the wrong type — a failure that
/// surfaces as data corruption long after the deployment that caused it.
/// </para>
/// </remarks>
public static class EntityIdRegistry
{
    private static readonly ConcurrentDictionary<string, EntityIdDescriptor> ByPrefix = new(StringComparer.Ordinal);

    /// <summary>
    /// The body lengths a text could have, so that resolution costs a bounded number of lookups rather than a
    /// scan of every registered type.
    /// </summary>
    private static readonly IdGranularity[] Granularities = [IdGranularity.Minute, IdGranularity.Hour, IdGranularity.Day];

    /// <summary>
    /// Registers an identifier type without reflection.
    /// </summary>
    /// <typeparam name="TSelf">Identifier type.</typeparam>
    /// <exception cref="InvalidOperationException">Another type already claims the same prefix.</exception>
    public static void Register<TSelf>()
        where TSelf : struct, IEntityId<TSelf>
        => Register(EntityIdDescriptor.For<TSelf>());

    /// <summary>
    /// Registers a descriptor.
    /// </summary>
    /// <param name="descriptor">Descriptor to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Another type already claims the same prefix.</exception>
    public static void Register(EntityIdDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var existing = ByPrefix.GetOrAdd(descriptor.Prefix, descriptor);

        if (existing.ValueObjectType != descriptor.ValueObjectType)
        {
            throw new InvalidOperationException(
                $"'{descriptor.ValueObjectType}' and '{existing.ValueObjectType}' both claim the prefix "
                + $"'{descriptor.Prefix}'. A prefix identifies one type and one only, otherwise an identifier "
                + "of one kind parses as another.");
        }
    }

    /// <summary>
    /// Looks up the type claiming a prefix.
    /// </summary>
    /// <param name="prefix">Prefix, without its trailing separator.</param>
    /// <param name="descriptor">The descriptor when found.</param>
    /// <returns><see langword="true"/> when a type claims <paramref name="prefix"/>.</returns>
    public static bool TryGetByPrefix(string prefix, [NotNullWhen(true)] out EntityIdDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        return ByPrefix.TryGetValue(prefix, out descriptor);
    }

    /// <summary>
    /// Resolves the identifier type a text belongs to, from its prefix.
    /// </summary>
    /// <param name="text">Candidate identifier.</param>
    /// <param name="descriptor">The descriptor when the prefix is claimed.</param>
    /// <returns><see langword="true"/> when the text carries a registered prefix.</returns>
    /// <remarks>
    /// A prefix may itself hold separators, so the split cannot be taken at the first one. The body length is
    /// fixed per granularity instead, which makes the prefix boundary computable and bounds resolution to one
    /// lookup per granularity rather than a scan of the registry.
    /// </remarks>
    public static bool TryResolve(ReadOnlySpan<char> text, [NotNullWhen(true)] out EntityIdDescriptor? descriptor)
    {
        // Keyed on a span so that resolving a prefix does not allocate the substring it looks up.
        var lookup = ByPrefix.GetAlternateLookup<ReadOnlySpan<char>>();

        foreach (var granularity in Granularities)
        {
            var boundary = text.Length - 1 - EntityIdFormat.BodyLength(granularity);

            if (boundary < 1 || text[boundary] != EntityIdPrefix.Separator)
            {
                continue;
            }

            if (lookup.TryGetValue(text[..boundary], out var candidate) && candidate.Granularity == granularity)
            {
                descriptor = candidate;
                return true;
            }
        }

        descriptor = null;
        return false;
    }

    /// <summary>
    /// Gets the identifier types registered so far.
    /// </summary>
    /// <returns>A snapshot of the registered descriptors.</returns>
    public static IReadOnlyCollection<EntityIdDescriptor> GetRegistered() => [.. ByPrefix.Values];
}
