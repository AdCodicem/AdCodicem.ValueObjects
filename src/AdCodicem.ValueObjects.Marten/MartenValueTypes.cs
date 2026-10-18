using System.Runtime.CompilerServices;
using JasperFx.Core.Reflection;
using Marten;

namespace AdCodicem.ValueObjects.Marten;

/// <summary>
/// Reaches the value types Marten keeps on its options, a list internal to Marten that only its own
/// <c>RegisterValueType</c> fills, and that its duplicated fields, compiled queries and collections read.
/// </summary>
/// <remarks>
/// <c>RegisterValueType</c> takes a type of a single public property, static ones counted, which no generated value
/// object is. The list has no public writer besides it, and Marten reads it without a lock: the package adds to it under a
/// lock of its own, what the registry holds when the store is configured and the rest when LINQ first meets it. It adds
/// each value object's nullable type alone, for the duplicated fields: a compiled query whose query type holds a value
/// type of the list is accepted, and would be answered with its first call's value
/// (<see cref="MartenValueObject{TSelf, TValue}.AddTo"/>). <c>MartenInternalsTests</c> pins the member, so that a Marten
/// release that renames or removes it fails CI first.
/// </remarks>
internal static class MartenValueTypes
{
    private static readonly Lock Gate = new();

    /// <summary>Adds a value type to a store's list, unless one is there for its type already.</summary>
    /// <param name="options">The store's options.</param>
    /// <param name="valueType">The value type.</param>
    /// <returns><see langword="true"/> when it was added.</returns>
    public static bool TryAdd(StoreOptions options, ValueTypeInfo valueType)
    {
        lock (Gate)
        {
            var valueTypes = Of(options);
            foreach (var known in valueTypes)
            {
                if (known.OuterType == valueType.OuterType)
                {
                    return false;
                }
            }

            valueTypes.Add(valueType);
            return true;
        }
    }

    /// <summary>Gets the list of value types of a store, Marten's internal <c>StoreOptions.ValueTypes</c>.</summary>
    /// <param name="options">The store's options.</param>
    /// <returns>The list itself.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_ValueTypes")]
    internal static extern List<ValueTypeInfo> Of(StoreOptions options);
}
