namespace AdCodicem.ValueObjects.Metadata;

/// <summary>
/// Recognizes a value object its author classifies as sensitive data, with an attribute derived from
/// <c>Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute</c>.
/// </summary>
/// <remarks>
/// <para>
/// The generator reads the same thing at compile time, and leaves the rejected value out of the exceptions the
/// <c>Create</c> and <c>Parse</c> it writes throw. This reads it at run time, for the one exception the library builds
/// by hand for a value object: <see cref="GenericValueObjectTypeConverter"/>'s, which reaches a construction of a
/// generic value object through its descriptor rather than through code generated for it.
/// </para>
/// <para>
/// The attribute is recognized by the name of the base it derives from, so this assembly references
/// Microsoft.Extensions.Compliance.Abstractions nowhere. <c>NoDataClassificationAttribute</c> derives from it to say
/// the opposite, and does not count. Every other derived attribute does, <c>UnknownDataClassificationAttribute</c>
/// included: data nobody has classified yet is read as sensitive.
/// </para>
/// </remarks>
internal static class DataClassificationReader
{
    private const string Classification = "Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute";

    private const string NoClassification = "Microsoft.Extensions.Compliance.Classification.NoDataClassificationAttribute";

    /// <summary>
    /// Determines whether one of the attributes of a type classifies it as sensitive data.
    /// </summary>
    /// <param name="type">The type, a construction of a generic one reading the attributes of its definition.</param>
    /// <returns><see langword="true"/> when the type is classified.</returns>
    public static bool IsClassified(Type type)
    {
        foreach (var attribute in type.GetCustomAttributesData())
        {
            // Sealed, it is only ever the attribute itself, never the base of another.
            if (string.Equals(attribute.AttributeType.FullName, NoClassification, StringComparison.Ordinal))
            {
                continue;
            }

            for (var current = attribute.AttributeType.BaseType; current is not null; current = current.BaseType)
            {
                if (string.Equals(current.FullName, Classification, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
