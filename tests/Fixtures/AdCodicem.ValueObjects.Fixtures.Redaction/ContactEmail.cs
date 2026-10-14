using AdCodicem.ValueObjects.Annotations;
using AdCodicem.ValueObjects.Fixtures.WithoutJson;

namespace AdCodicem.ValueObjects.Fixtures.Redaction;

/// <summary>An email address, classified on its type as a way to contact a person.</summary>
[ContactData]
[ValueObject<string>(MaxLength = 254)]
public readonly partial struct ContactEmail;

/// <summary>A temperature in degrees Celsius, whose formatting hook writes its unit.</summary>
[ValueObject<int>]
public readonly partial struct Temperature : IValueObjectStringFormatter<int>
{
    /// <inheritdoc />
    public static string FormatValue(in int value, ReadOnlySpan<char> format, IFormatProvider? provider)
        => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{value} °C");
}

/// <summary>A shipment, whose code, a value object of another assembly, is classified on its property.</summary>
/// <param name="Code">The stock keeping unit shipped.</param>
public sealed record Shipment([property: ContactData] Sku Code);
