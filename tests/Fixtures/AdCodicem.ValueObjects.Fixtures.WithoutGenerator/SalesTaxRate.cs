using System.Globalization;
using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.WithoutGenerator;

/// <summary>
/// A sales tax rate, in percent, written by hand, whose known values a decimal attribute argument cannot carry, so they are
/// written as text.
/// </summary>
/// <remarks>
/// Its last known value is text no decimal parses. Where no generator runs, nothing checks the annotation when the type
/// compiles, and the registry has to describe such a value as it was written.
/// </remarks>
[ValueObject<decimal>]
[KnownValue("Standard", "20.0")]
[KnownValue("Reduced", "5.50")]
[KnownValue("Unreadable", "twenty")]
public readonly struct SalesTaxRate : IValueObject<SalesTaxRate, decimal>
{
    private readonly decimal _value;

    private SalesTaxRate(decimal value) => _value = value;

    public decimal Value => _value;

    public bool IsDefault => _value == 0m;

    public static decimal Normalize(decimal value) => decimal.Round(value, 1);

    public static ValidationResult Validate(in decimal value)
        => value is >= 0m and <= 100m ? ValidationResult.Success : ValidationResult.OutOfRange("From 0 to 100.");

    public static SalesTaxRate Create(decimal value)
    {
        var normalized = Normalize(value);
        Validate(normalized).ThrowIfInvalid(typeof(SalesTaxRate), normalized);

        return new SalesTaxRate(normalized);
    }

    public static bool TryCreate(decimal value, out SalesTaxRate result) => TryCreate(value, out result, out _);

    public static bool TryCreate(decimal value, out SalesTaxRate result, out ValidationResult validation)
    {
        var normalized = Normalize(value);
        validation = Validate(normalized);
        result = validation.IsValid ? new SalesTaxRate(normalized) : default;

        return validation.IsValid;
    }

    public static SalesTaxRate CreateUnchecked(decimal value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out SalesTaxRate result, out ValidationResult validation)
    {
        if (decimal.TryParse(text, NumberStyles.Number, provider, out var raw))
        {
            return TryCreate(raw, out result, out validation);
        }

        result = default;
        validation = ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, "Not a number.");
        return false;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SalesTaxRate result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out SalesTaxRate result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static SalesTaxRate Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException("Not a sales tax rate.");

    public static SalesTaxRate Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    public bool Equals(SalesTaxRate other) => _value == other._value;

    public override bool Equals(object? obj) => obj is SalesTaxRate other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public int CompareTo(SalesTaxRate other) => _value.CompareTo(other._value);

    public override string ToString() => _value.ToString(CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider) => _value.ToString(format, formatProvider);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => _value.TryFormat(destination, out charsWritten, format, provider);

    public static bool operator ==(SalesTaxRate left, SalesTaxRate right) => left.Equals(right);

    public static bool operator !=(SalesTaxRate left, SalesTaxRate right) => !left.Equals(right);

    public static bool operator <(SalesTaxRate left, SalesTaxRate right) => left.CompareTo(right) < 0;

    public static bool operator >(SalesTaxRate left, SalesTaxRate right) => left.CompareTo(right) > 0;

    public static bool operator <=(SalesTaxRate left, SalesTaxRate right) => left.CompareTo(right) <= 0;

    public static bool operator >=(SalesTaxRate left, SalesTaxRate right) => left.CompareTo(right) >= 0;
}
