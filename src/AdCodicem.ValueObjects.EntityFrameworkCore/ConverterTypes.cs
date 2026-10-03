namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Picks the converter the conventions map a value object through, closed over it.
/// </summary>
internal static class ConverterTypes
{
    /// <summary>
    /// Gives the converter of a property holding a value object.
    /// </summary>
    /// <param name="valueObjectType">Value object type.</param>
    /// <param name="valueType">Underlying value type.</param>
    /// <param name="strict">Whether what is read is validated again.</param>
    /// <returns>The converter type.</returns>
    public static Type Required(Type valueObjectType, Type valueType, bool strict)
        => (strict ? typeof(StrictValueObjectConverter<,>) : typeof(ValueObjectConverter<,>))
            .MakeGenericType(valueObjectType, valueType);

    /// <summary>
    /// Gives the converter of a property holding an optional value object, which stores a value the value object
    /// rejects as <c>NULL</c>.
    /// </summary>
    /// <param name="valueObjectType">Value object type.</param>
    /// <param name="valueType">Underlying value type.</param>
    /// <param name="strict">Whether what is read is validated again.</param>
    /// <returns>
    /// The converter type, or <see langword="null"/> for a value object written by hand over a reference type other
    /// than <see cref="string"/>: its optional properties keep the converter of the value object, which refuses such a
    /// value rather than store it.
    /// </returns>
    public static Type? Optional(Type valueObjectType, Type valueType, bool strict)
    {
        if (valueType.IsValueType)
        {
            return (strict ? typeof(StrictNullableValueObjectConverter<,>) : typeof(NullableValueObjectConverter<,>))
                .MakeGenericType(valueObjectType, valueType);
        }

        if (valueType == typeof(string))
        {
            return (strict ? typeof(StrictNullableValueObjectConverter<>) : typeof(NullableValueObjectConverter<>))
                .MakeGenericType(valueObjectType);
        }

        return null;
    }
}
