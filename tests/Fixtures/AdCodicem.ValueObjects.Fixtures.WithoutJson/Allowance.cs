using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.WithoutJson;

/// <summary>
/// An allowance, in the smallest unit of a currency, wider than 64 bits: its generated converter writes it as a JSON
/// string, where a converter built by reflection would write a number.
/// </summary>
[ValueObject<Int128>]
public readonly partial struct Allowance;

/// <summary>A stock keeping unit, in upper case.</summary>
[ValueObject<string>(MaxLength = 12)]
public readonly partial struct Sku : IValueObjectNormalizer<string>
{
    /// <inheritdoc />
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}
