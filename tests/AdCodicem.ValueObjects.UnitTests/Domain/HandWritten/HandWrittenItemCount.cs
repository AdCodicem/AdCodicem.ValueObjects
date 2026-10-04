using System.Globalization;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

/// <summary>
/// A number of items written by hand, which prints with its unit, "3 items", and parses that text back, never the
/// bare number.
/// </summary>
/// <remarks>
/// Its own text is the hand-written counterpart of a formatting hook, text meant for people, with a parser that reads
/// it, as a hand-written value object is free to have: the underlying value in its JSON form is text that parser
/// refuses. Nothing registers it, so the serializer reaches it through the general-purpose converter.
/// </remarks>
public readonly struct HandWrittenItemCount : IValueObject<HandWrittenItemCount, int>
{
    private readonly int _value;

    private HandWrittenItemCount(int value) => _value = value;

    /// <summary>Declares no rule a reflection-driven caller could publish.</summary>
    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;

    public int Value => _value;

    public bool IsDefault => _value == 0;

    public static int Normalize(int value) => value;

    public static ValidationResult Validate(in int value)
        => value > 0 ? ValidationResult.Success : ValidationResult.OutOfRange("A count of items is positive.");

    public static HandWrittenItemCount Create(int value)
    {
        Validate(value).ThrowIfInvalid(typeof(HandWrittenItemCount), value);

        return new HandWrittenItemCount(value);
    }

    public static bool TryCreate(int value, out HandWrittenItemCount result) => TryCreate(value, out result, out _);

    public static bool TryCreate(int value, out HandWrittenItemCount result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new HandWrittenItemCount(value) : default;

        return validation.IsValid;
    }

    public static HandWrittenItemCount CreateUnchecked(int value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out HandWrittenItemCount result, out ValidationResult validation)
    {
        const string Unit = " items";

        if (text.EndsWith(Unit, StringComparison.Ordinal) && int.TryParse(text[..^Unit.Length], NumberStyles.None, provider, out var raw))
        {
            return TryCreate(raw, out result, out validation);
        }

        result = default;
        validation = ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, "The text is not a count of items.");

        return false;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out HandWrittenItemCount result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out HandWrittenItemCount result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static HandWrittenItemCount Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a count of items.");

    public static HandWrittenItemCount Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    public bool Equals(HandWrittenItemCount other) => _value == other._value;

    public override bool Equals(object? obj) => obj is HandWrittenItemCount other && Equals(other);

    public override int GetHashCode() => _value;

    public int CompareTo(HandWrittenItemCount other) => _value.CompareTo(other._value);

    public override string ToString() => ToString(null, null);

    public string ToString(string? format, IFormatProvider? formatProvider)
        => string.Create(CultureInfo.InvariantCulture, $"{_value} items");

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => destination.TryWrite(CultureInfo.InvariantCulture, $"{_value} items", out charsWritten);

    public static bool operator ==(HandWrittenItemCount left, HandWrittenItemCount right) => left.Equals(right);

    public static bool operator !=(HandWrittenItemCount left, HandWrittenItemCount right) => !left.Equals(right);

    public static bool operator <(HandWrittenItemCount left, HandWrittenItemCount right) => left.CompareTo(right) < 0;

    public static bool operator >(HandWrittenItemCount left, HandWrittenItemCount right) => left.CompareTo(right) > 0;

    public static bool operator <=(HandWrittenItemCount left, HandWrittenItemCount right) => left.CompareTo(right) <= 0;

    public static bool operator >=(HandWrittenItemCount left, HandWrittenItemCount right) => left.CompareTo(right) >= 0;
}
