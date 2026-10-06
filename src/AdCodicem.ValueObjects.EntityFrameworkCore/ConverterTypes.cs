namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Picks the converter the conventions map a value object through, closed over it.
/// </summary>
/// <remarks>
/// The conventions call it from a visitor, with the type arguments a descriptor hands back
/// (<see cref="Metadata.ValueObjectDescriptor.Accept{TResult}(Metadata.IValueObjectVisitor{TResult})"/>).
/// </remarks>
internal static class ConverterTypes
{
    /// <summary>
    /// Gives the converter of a property holding a value object.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="strict">Whether what is read is validated again.</param>
    /// <returns>The converter type.</returns>
    public static Type Required<TSelf, TValue>(bool strict)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => strict ? typeof(StrictValueObjectConverter<TSelf, TValue>) : typeof(ValueObjectConverter<TSelf, TValue>);

    /// <summary>
    /// Gives the converter of a property holding an optional value object, which stores a value the value object
    /// rejects as <c>NULL</c>.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="strict">Whether what is read is validated again.</param>
    /// <returns>
    /// The converter type, or <see langword="null"/> for a value object written by hand over a reference type other
    /// than <see cref="string"/>: its optional properties keep the converter of the value object, which refuses such a
    /// value rather than store it.
    /// </returns>
    /// <remarks>
    /// These converters store <c>TValue?</c>, which C# names only under <c>TValue : struct</c>, or text, which it
    /// names only under <c>TSelf : IValueObject&lt;TSelf, string&gt;</c>, and the constraints of a visitor prove
    /// neither. They are closed with <see cref="Type.MakeGenericType(Type[])"/> over the type arguments themselves,
    /// never over a <see cref="Type"/> read off a descriptor, and only while Entity Framework Core builds a model,
    /// which it never does under native AOT.
    /// </remarks>
    public static Type? Optional<TSelf, TValue>(bool strict)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        if (typeof(TValue).IsValueType)
        {
            return (strict ? typeof(StrictNullableValueObjectConverter<,>) : typeof(NullableValueObjectConverter<,>))
                .MakeGenericType(typeof(TSelf), typeof(TValue));
        }

        if (typeof(TValue) == typeof(string))
        {
            return (strict ? typeof(StrictNullableValueObjectConverter<>) : typeof(NullableValueObjectConverter<>))
                .MakeGenericType(typeof(TSelf));
        }

        return null;
    }
}
