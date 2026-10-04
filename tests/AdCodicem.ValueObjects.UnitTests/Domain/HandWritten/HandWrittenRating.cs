using System.Globalization;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

/// <summary>
/// A rating from 1 to 5 written by hand, whose <c>TryCreate</c> refuses a rating out of range without saying why.
/// </summary>
/// <remarks>
/// <see cref="Validate"/> names the rule, but <c>TryCreate</c> drops it, as a hand-written one can: the contract asks
/// for a reason, and nothing checks it. Over a type that does not travel as text, a reader reaches the value object
/// through <c>TryCreate</c> rather than through its parser, so this is the refusal a reader has to give a code itself.
/// Nothing registers it; the registry describes it by reflection the first time it is resolved.
/// </remarks>
public readonly struct HandWrittenRating : IValueObject<HandWrittenRating, int>
{
    private readonly int _value;

    private HandWrittenRating(int value) => _value = value;

    /// <summary>Declares no rule a reflection-driven caller could publish.</summary>
    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;

    public int Value => _value;

    public bool IsDefault => _value == 0;

    public static int Normalize(int value) => value;

    public static ValidationResult Validate(in int value)
        => value is >= 1 and <= 5 ? ValidationResult.Success : ValidationResult.OutOfRange("A rating is from 1 to 5.");

    public static HandWrittenRating Create(int value)
    {
        Validate(value).ThrowIfInvalid(typeof(HandWrittenRating), value);

        return new HandWrittenRating(value);
    }

    public static bool TryCreate(int value, out HandWrittenRating result) => TryCreate(value, out result, out _);

    public static bool TryCreate(int value, out HandWrittenRating result, out ValidationResult validation)
    {
        var refused = !Validate(value).IsValid;

        // No reason given, though Validate has one.
        validation = ValidationResult.Success;
        result = refused ? default : new HandWrittenRating(value);

        return !refused;
    }

    public static HandWrittenRating CreateUnchecked(int value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out HandWrittenRating result, out ValidationResult validation)
    {
        if (int.TryParse(text, NumberStyles.Integer, provider, out var raw))
        {
            return TryCreate(raw, out result, out validation);
        }

        result = default;
        validation = ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, "The text is not a rating.");

        return false;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out HandWrittenRating result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out HandWrittenRating result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static HandWrittenRating Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a rating.");

    public static HandWrittenRating Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    public bool Equals(HandWrittenRating other) => _value == other._value;

    public override bool Equals(object? obj) => obj is HandWrittenRating other && Equals(other);

    public override int GetHashCode() => _value;

    public int CompareTo(HandWrittenRating other) => _value.CompareTo(other._value);

    public override string ToString() => _value.ToString(CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider) => _value.ToString(format, formatProvider);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => _value.TryFormat(destination, out charsWritten, format, provider);

    public static bool operator ==(HandWrittenRating left, HandWrittenRating right) => left.Equals(right);

    public static bool operator !=(HandWrittenRating left, HandWrittenRating right) => !left.Equals(right);

    public static bool operator <(HandWrittenRating left, HandWrittenRating right) => left.CompareTo(right) < 0;

    public static bool operator >(HandWrittenRating left, HandWrittenRating right) => left.CompareTo(right) > 0;

    public static bool operator <=(HandWrittenRating left, HandWrittenRating right) => left.CompareTo(right) <= 0;

    public static bool operator >=(HandWrittenRating left, HandWrittenRating right) => left.CompareTo(right) >= 0;
}
