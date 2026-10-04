using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

/// <summary>
/// A string value object written by hand: up to 200 ASCII letters, kept in upper case.
/// </summary>
/// <remarks>
/// Nothing registers it, neither with the value object registry nor with the JSON one, so the serializer reaches
/// it only through <c>ValueObjectJsonConverterFactory</c>'s general-purpose converter. It honours the contract the
/// way generated code does, rejecting <see langword="null"/> as required rather than throwing on it.
/// </remarks>
public readonly struct HandWrittenCode : IValueObject<HandWrittenCode, string>
{
    private readonly string? _value;

    private HandWrittenCode(string value) => _value = value;

    /// <summary>Declares no rule a reflection-driven caller could publish.</summary>
    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;

    public string Value => _value ?? string.Empty;

    public bool IsDefault => _value is null;

    public static string Normalize(string value) => value is null ? value! : value.Trim().ToUpperInvariant();

    public static ValidationResult Validate(in string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return ValidationResult.Required("A code is required.");
        }

        if (value.Length > 200)
        {
            return ValidationResult.Failure(ValueObjectErrorCodes.TooLong, "A code is at most 200 letters long.");
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetter(character))
            {
                return ValidationResult.InvalidFormat("A code holds ASCII letters only.");
            }
        }

        return ValidationResult.Success;
    }

    public static HandWrittenCode Create(string value)
    {
        var normalized = Normalize(value);
        Validate(normalized).ThrowIfInvalid(typeof(HandWrittenCode), value);

        return new HandWrittenCode(normalized);
    }

    public static bool TryCreate(string value, out HandWrittenCode result) => TryCreate(value, out result, out _);

    public static bool TryCreate(string value, out HandWrittenCode result, out ValidationResult validation)
    {
        var normalized = Normalize(value);
        validation = Validate(normalized);
        result = validation.IsValid ? new HandWrittenCode(normalized) : default;

        return validation.IsValid;
    }

    public static HandWrittenCode CreateUnchecked(string value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out HandWrittenCode result, out ValidationResult validation)
        => TryCreate(text.ToString(), out result, out validation);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out HandWrittenCode result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out HandWrittenCode result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static HandWrittenCode Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());

    public static HandWrittenCode Parse(string s, IFormatProvider? provider) => Create(s);

    public bool Equals(HandWrittenCode other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is HandWrittenCode other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public int CompareTo(HandWrittenCode other) => string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value;

    public string ToString(string? format, IFormatProvider? formatProvider) => Value;

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (Value.TryCopyTo(destination))
        {
            charsWritten = Value.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    public static bool operator ==(HandWrittenCode left, HandWrittenCode right) => left.Equals(right);

    public static bool operator !=(HandWrittenCode left, HandWrittenCode right) => !left.Equals(right);

    public static bool operator <(HandWrittenCode left, HandWrittenCode right) => left.CompareTo(right) < 0;

    public static bool operator >(HandWrittenCode left, HandWrittenCode right) => left.CompareTo(right) > 0;

    public static bool operator <=(HandWrittenCode left, HandWrittenCode right) => left.CompareTo(right) <= 0;

    public static bool operator >=(HandWrittenCode left, HandWrittenCode right) => left.CompareTo(right) >= 0;
}
