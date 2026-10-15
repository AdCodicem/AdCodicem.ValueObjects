using HotChocolate.Data.Filters;

namespace AdCodicem.ValueObjects.HotChocolate.Data;

/// <summary>
/// Binds value objects to the operations of their scalars in a filter convention, closed at compile time.
/// </summary>
public static class ValueObjectFilterConventionDescriptorExtensions
{
    /// <summary>
    /// Binds one value object and its nullable type to the comparable operations of its scalar, closed at compile time.
    /// </summary>
    /// <typeparam name="TSelf">The value object type.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="descriptor">The descriptor <c>AddFiltering(convention => …)</c> hands a filter convention.</param>
    /// <returns><paramref name="descriptor"/>, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// For a construction of a generic value object, or one written by hand, that nothing registered under native AOT, or
    /// for a convention <c>AddValueObjectFiltering()</c> should leave to the application. The binding asks no registry,
    /// but its operands are the value object's scalar, whose rules they are read through: under native AOT, such a value
    /// object also needs its scalar typed, <c>AddValueObjectScalar&lt;TSelf, TValue&gt;()</c>, or else
    /// <c>ValueObjectRegistry.Register&lt;TSelf, TValue&gt;(TSelf.Schema)</c>, which covers the scalar and the filter
    /// alike. Both forms are bound, since HotChocolate.Data looks a binding up by a member's exact type, and a list of them
    /// is named after its element.
    /// </remarks>
    public static IFilterConventionDescriptor BindValueObject<TSelf, TValue>(this IFilterConventionDescriptor descriptor)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor
            .BindRuntimeType<TSelf, ValueObjectOperationFilterInputType<TSelf>>()
            .BindRuntimeType<TSelf?, ValueObjectOperationFilterInputType<TSelf>>();
    }
}
