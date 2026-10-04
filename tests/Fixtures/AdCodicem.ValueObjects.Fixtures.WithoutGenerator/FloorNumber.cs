using System.Globalization;
using AdCodicem.ValueObjects.Annotations;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Fixtures.WithoutGenerator;

/// <summary>
/// A floor of a building, from 1 to 10, written by hand: an annotated value object with bounds and no length.
/// </summary>
/// <remarks>
/// It carries the consumer's own generic attribute ahead of its annotation, and <see cref="LightColor"/> after it,
/// so a reader of the annotation looks past one whatever order the attributes are read back in. It keeps the deprecated
/// Minimum and Maximum options, which its schema restates and nothing reads at run time, until they are removed.
/// </remarks>
[Reviewed<FloorNumber>]
#pragma warning disable VO0028 // The deprecated options are what this fixture holds its schema to.
[ValueObject<int>(Minimum = "1", Maximum = "10")]
#pragma warning restore VO0028
public readonly struct FloorNumber : IValueObject<FloorNumber, int>
{
    private readonly int _value;

    private FloorNumber(int value) => _value = value;

    public static ValueObjectSchema Schema { get; } = new() { Minimum = "1", Maximum = "10" };

    public int Value => _value;

    public bool IsDefault => _value == 0;

    public static int Normalize(int value) => value;

    public static ValidationResult Validate(in int value)
        => value is >= 1 and <= 10 ? ValidationResult.Success : ValidationResult.OutOfRange("From 1 to 10.");

    public static FloorNumber Create(int value)
    {
        Validate(value).ThrowIfInvalid(typeof(FloorNumber), value);

        return new FloorNumber(value);
    }

    public static bool TryCreate(int value, out FloorNumber result) => TryCreate(value, out result, out _);

    public static bool TryCreate(int value, out FloorNumber result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new FloorNumber(value) : default;

        return validation.IsValid;
    }

    public static FloorNumber CreateUnchecked(int value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out FloorNumber result, out ValidationResult validation)
    {
        if (int.TryParse(text, NumberStyles.Integer, provider, out var raw))
        {
            return TryCreate(raw, out result, out validation);
        }

        result = default;
        validation = ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, "Not a number.");
        return false;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out FloorNumber result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out FloorNumber result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static FloorNumber Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException("Not a floor.");

    public static FloorNumber Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    public bool Equals(FloorNumber other) => _value == other._value;

    public override bool Equals(object? obj) => obj is FloorNumber other && Equals(other);

    public override int GetHashCode() => _value;

    public int CompareTo(FloorNumber other) => _value.CompareTo(other._value);

    public override string ToString() => _value.ToString(CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider) => _value.ToString(format, formatProvider);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => _value.TryFormat(destination, out charsWritten, format, provider);

    public static bool operator ==(FloorNumber left, FloorNumber right) => left.Equals(right);

    public static bool operator !=(FloorNumber left, FloorNumber right) => !left.Equals(right);

    public static bool operator <(FloorNumber left, FloorNumber right) => left.CompareTo(right) < 0;

    public static bool operator >(FloorNumber left, FloorNumber right) => left.CompareTo(right) > 0;

    public static bool operator <=(FloorNumber left, FloorNumber right) => left.CompareTo(right) <= 0;

    public static bool operator >=(FloorNumber left, FloorNumber right) => left.CompareTo(right) >= 0;
}
