using System.Globalization;
using AdCodicem.ValueObjects.Annotations;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Fixtures.WithoutGenerator;

/// <summary>
/// A sales tax rate, in percent, written by hand, whose schema leaves out one of the known values it marks.
/// </summary>
/// <remarks>
/// It declares its bounds through the hooks, which its schema publishes. Where no generator runs, nothing reads the
/// members marked <c>[KnownValue]</c> when the type compiles, so they and the schema can disagree, as they do here: the
/// schema leaves the last one out, and the registry describes the type from the schema, the source the typed path reads.
/// </remarks>
[ValueObject<decimal>]
public readonly struct SalesTaxRate : IValueObject<SalesTaxRate, decimal>, IValueObjectMinimum<decimal>, IValueObjectMaximum<decimal>
{
    public static decimal Minimum => 0m;

    public static decimal Maximum => 100m;

    [KnownValue]
    public static readonly SalesTaxRate Standard = Create(20.0m);

    [KnownValue]
    public static readonly SalesTaxRate Reduced = Create(5.50m);

    [KnownValue]
    public static readonly SalesTaxRate Exempt = Create(0m);

    /// <summary>
    /// Publishes the bounds of the hooks and the known values the type holds, with their names, all but the last.
    /// </summary>
    public static ValueObjectSchema Schema { get; } = new()
    {
        Minimum = ValueObjectBound.Text(Minimum),
        Maximum = ValueObjectBound.Text(Maximum),
        KnownValues = [20.0m, 5.5m],
        KnownValueDetails = [new KnownValueInfo(20.0m, "Standard"), new KnownValueInfo(5.5m, "Reduced")],
    };

    private readonly decimal _value;

    private SalesTaxRate(decimal value) => _value = value;

    public decimal Value => _value;

    public bool IsDefault => _value == 0m;

    public static decimal Normalize(decimal value) => decimal.Round(value, 1);

    public static ValidationResult Validate(in decimal value)
        => value >= Minimum && value <= Maximum ? ValidationResult.Success : ValidationResult.OutOfRange("From 0 to 100.");

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
