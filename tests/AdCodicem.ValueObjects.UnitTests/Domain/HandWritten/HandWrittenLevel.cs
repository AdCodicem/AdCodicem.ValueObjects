using System.Globalization;

namespace AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

/// <summary>
/// A level of a game written by hand, which nothing registers and only the OpenAPI schema transformer is given, so the
/// registry has not described it when the transformer meets it.
/// </summary>
public readonly struct HandWrittenLevel : IValueObject<HandWrittenLevel, int>
{
    private readonly int _value;

    private HandWrittenLevel(int value) => _value = value;

    public int Value => _value;

    public bool IsDefault => _value == 0;

    public static int Normalize(int value) => value;

    public static ValidationResult Validate(in int value)
        => value > 0 ? ValidationResult.Success : ValidationResult.OutOfRange("A level is positive.");

    public static HandWrittenLevel Create(int value)
    {
        Validate(value).ThrowIfInvalid(typeof(HandWrittenLevel), value);

        return new HandWrittenLevel(value);
    }

    public static bool TryCreate(int value, out HandWrittenLevel result) => TryCreate(value, out result, out _);

    public static bool TryCreate(int value, out HandWrittenLevel result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new HandWrittenLevel(value) : default;

        return validation.IsValid;
    }

    public static HandWrittenLevel CreateUnchecked(int value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out HandWrittenLevel result, out ValidationResult validation)
    {
        if (int.TryParse(text, NumberStyles.None, provider, out var raw))
        {
            return TryCreate(raw, out result, out validation);
        }

        result = default;
        validation = ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, "The text is not a level.");

        return false;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out HandWrittenLevel result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out HandWrittenLevel result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static HandWrittenLevel Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a level.");

    public static HandWrittenLevel Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    public bool Equals(HandWrittenLevel other) => _value == other._value;

    public override bool Equals(object? obj) => obj is HandWrittenLevel other && Equals(other);

    public override int GetHashCode() => _value;

    public int CompareTo(HandWrittenLevel other) => _value.CompareTo(other._value);

    public override string ToString() => _value.ToString(CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider) => _value.ToString(format, formatProvider);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => _value.TryFormat(destination, out charsWritten, format, provider);

    public static bool operator ==(HandWrittenLevel left, HandWrittenLevel right) => left.Equals(right);

    public static bool operator !=(HandWrittenLevel left, HandWrittenLevel right) => !left.Equals(right);

    public static bool operator <(HandWrittenLevel left, HandWrittenLevel right) => left.CompareTo(right) < 0;

    public static bool operator >(HandWrittenLevel left, HandWrittenLevel right) => left.CompareTo(right) > 0;

    public static bool operator <=(HandWrittenLevel left, HandWrittenLevel right) => left.CompareTo(right) <= 0;

    public static bool operator >=(HandWrittenLevel left, HandWrittenLevel right) => left.CompareTo(right) >= 0;
}
