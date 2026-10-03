using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Compares value objects using their own equality rather than the underlying value's.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <remarks>
/// <para>
/// This matters as soon as a value object declares a comparison other than ordinal: without it, change tracking
/// would consider <c>ORD-42</c> and <c>ord-42</c> different for a case-insensitive reference and issue an
/// UPDATE that changes nothing. A value object is immutable, so the snapshot is the value itself.
/// </para>
/// <para>
/// A property holding a <c>TSelf?</c> takes <see cref="NullableValueObjectComparer{TSelf}"/>, which a compiled model
/// can write, where it cannot write the wrapping Entity Framework Core would give this comparer.
/// </para>
/// </remarks>
public sealed class ValueObjectComparer<TSelf> : ValueComparer<TSelf>
    where TSelf : struct, IEquatable<TSelf>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectComparer{TSelf}"/> class.
    /// </summary>
    public ValueObjectComparer()
        : base(
            (left, right) => left.Equals(right),
            valueObject => valueObject.GetHashCode(),
            valueObject => valueObject)
    {
    }
}
