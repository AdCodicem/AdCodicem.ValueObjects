using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Compares optional value objects using their own equality rather than the underlying value's, as
/// <see cref="ValueObjectComparer{TSelf}"/> compares required ones.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <remarks>
/// <para>
/// The conventions give it to every property holding a <c>TSelf?</c>. Left to itself, Entity Framework Core would
/// wrap <see cref="ValueObjectComparer{TSelf}"/> for such a property, and the compiled model that
/// <c>dotnet ef dbcontext optimize</c> writes for that wrapping does not compile: it names the comparer where the
/// wrapper takes the value type. A comparer of the nullable type itself is written as it stands.
/// </para>
/// <para>
/// Two absent values are equal, an absent value equals no present one, and two present ones are equal when the value
/// object says they are. A value object is immutable, so the snapshot is the value itself.
/// </para>
/// <para>
/// <typeparamref name="TSelf"/> carries the annotation <see cref="ValueComparer{T}"/> puts on its own type argument, so
/// that an application trimmed or published with native AOT gets no warning for the comparer its model names.
/// </para>
/// </remarks>
public sealed class NullableValueObjectComparer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)] TSelf> : ValueComparer<TSelf?>
    where TSelf : struct, IEquatable<TSelf>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NullableValueObjectComparer{TSelf}"/> class.
    /// </summary>
    public NullableValueObjectComparer()
        : base(
            (left, right) => left.HasValue ? right.HasValue && left.Value.Equals(right.Value) : !right.HasValue,
            valueObject => valueObject.HasValue ? valueObject.Value.GetHashCode() : 0,
            valueObject => valueObject)
    {
    }
}
