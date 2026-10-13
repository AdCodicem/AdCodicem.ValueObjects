using System.Globalization;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.TestData;
using HotChocolate;

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>
/// A reference printed in groups of four by its own formatter, its normalizer dropping the spaces: what a scalar writes
/// is its value, never its formatted text.
/// </summary>
[ValueObject<string>(MinLength = 8, MaxLength = 8)]
public readonly partial struct GroupedReference : IValueObjectNormalizer<string>, IValueObjectStringFormatter<string>
{
    public static string NormalizeValue(string value) => value.Replace(" ", string.Empty, StringComparison.Ordinal);

    public static string FormatValue(in string value, ReadOnlySpan<char> format, IFormatProvider? provider)
        => format.IsEmpty ? $"{value[..4]} {value[4..]}" : value;
}

/// <summary>A parcel's code, which names its own scalar through HotChocolate's attribute.</summary>
[ValueObject<string>(MinLength = 4)]
[GraphQLName("ParcelCode")]
public readonly partial struct NamedParcelCode;

/// <summary>An absolute link, written by hand over <see cref="Uri"/>, which nothing registers.</summary>
public sealed class GraphQLLink : IDeclaration<Uri>
{
    public static ValueObjectSchema Schema { get; } = new() { Description = "An absolute link." };

    public static ValidationResult Validate(Uri value)
        => value is { IsAbsoluteUri: true } ? ValidationResult.Success : ValidationResult.InvalidFormat("A link is an absolute URI.");

    public static bool TryRead(string text, out Uri value)
    {
        value = new Uri(text, UriKind.RelativeOrAbsolute);
        return true;
    }
}

/// <summary>A tally from one, written by hand over <see cref="int"/>, which nothing registers.</summary>
public sealed class GraphQLTally : IDeclaration<int>
{
    public static ValueObjectSchema Schema { get; } = new() { Description = "A tally.", Minimum = "1" };

    public static ValidationResult Validate(int value)
        => value >= 1 ? ValidationResult.Success : ValidationResult.OutOfRange("A tally is at least 1.");

    public static bool TryRead(string text, out int value) => int.TryParse(text, CultureInfo.InvariantCulture, out value);
}
