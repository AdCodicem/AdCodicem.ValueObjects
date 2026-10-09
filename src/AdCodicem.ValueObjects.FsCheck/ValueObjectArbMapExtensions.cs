using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Testing.Data;
using FsCheck;
using FsCheck.Fluent;

namespace AdCodicem.ValueObjects.FsCheck;

/// <summary>
/// Merges the arbitraries of value objects into an FsCheck map, one per type, so that the map derives the types that
/// hold them too.
/// </summary>
/// <remarks>
/// Each value object is merged with <c>MergeArb</c> on its own arbitrary, <see cref="ValueObjectArbitrary.For{TSelf, TValue}"/>,
/// closed over it through its descriptor's visitor: a single factory constrained to value objects would break the
/// derivation of every other type the map serves.
/// </remarks>
public static class ValueObjectArbMapExtensions
{
    /// <summary>
    /// Merges the arbitrary of every value object the generated registration of an assembly registers, and of every other
    /// value object of that assembly the registry holds by then but a construction of a generic one.
    /// </summary>
    /// <param name="map">The map to merge into: <c>ArbMap.Default</c>, or one already merged.</param>
    /// <param name="assembly">The assembly declaring the value objects: <c>typeof(Iban).Assembly</c>.</param>
    /// <param name="options">How each value is drawn, the generators of the rules no schema carries included, or <see langword="null"/> for the defaults.</param>
    /// <returns>The merged map.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> or <paramref name="assembly"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The constructions of a generic value object, <c>Reference&lt;PurchaseOrder&gt;</c> for one, are not merged, even one
    /// the registry described by then, and neither is a value object written by hand that nothing registered or resolved:
    /// merge each with <see cref="MergeValueObject{TSelf, TValue}"/>. Without it, FsCheck refuses the type, and every type
    /// holding it, with its own message, "not handled automatically", naming it. The assembly is not searched through the
    /// registry's <c>TryResolve</c>, which would describe and keep every value object written by hand it declares.
    /// </remarks>
    [RequiresUnreferencedCode("Locates the generated registration of the assembly by its metadata.")]
    public static IArbMap MergeValueObjects(this IArbMap map, Assembly assembly, ValueObjectSamplerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(assembly);
        ValueObjectRegistry.EnsureAssemblyRegistered(assembly);
        foreach (var descriptor in ValueObjectRegistry.GetRegistered())
        {
            if (descriptor.ValueObjectType.Assembly == assembly && !descriptor.ValueObjectType.IsConstructedGenericType)
            {
                map = descriptor.Accept(new Merge(map, options));
            }
        }

        return map;
    }

    /// <summary>
    /// Merges the arbitrary of one value object: a construction of a generic one, or one written by hand that nothing
    /// registers.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="map">The map to merge into.</param>
    /// <param name="options">How each value is drawn, or <see langword="null"/> for the defaults.</param>
    /// <returns>The merged map.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    public static IArbMap MergeValueObject<TSelf, TValue>(this IArbMap map, ValueObjectSamplerOptions? options = null)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(map);
        return map.MergeArb(ValueObjectArbitrary.For<TSelf, TValue>(options));
    }

    private sealed class Merge(IArbMap map, ValueObjectSamplerOptions? options) : IValueObjectVisitor<IArbMap>
    {
        public IArbMap Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => map.MergeArb(ValueObjectArbitrary.For<TSelf, TValue>(options));
    }
}
