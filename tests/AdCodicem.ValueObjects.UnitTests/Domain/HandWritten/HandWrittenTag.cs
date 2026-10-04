using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

#pragma warning disable CA1000 // The static members are the contract of IValueObject<TSelf, TValue>, on a generic type.

/// <summary>
/// A generic string value object written by hand, whose definition a test registers by hand.
/// </summary>
/// <typeparam name="TOwner">What the tag belongs to.</typeparam>
/// <remarks>
/// Its constructions carry no generated schema and no generated converter, so the registry describes one from its
/// annotations, as it describes any value object written by hand.
/// </remarks>
public readonly struct HandWrittenTag<TOwner> : IValueObject<HandWrittenTag<TOwner>, string>
{
    private readonly string? _value;

    private HandWrittenTag(string value) => _value = value;

    /// <summary>Declares no rule a reflection-driven caller could publish.</summary>
    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;

    public string Value => _value ?? string.Empty;

    public bool IsDefault => _value is null;

    public static string Normalize(string value) => value;

    public static ValidationResult Validate(in string value)
        => string.IsNullOrEmpty(value) ? ValidationResult.Required() : ValidationResult.Success;

    public static HandWrittenTag<TOwner> Create(string value)
    {
        Validate(value).ThrowIfInvalid(typeof(HandWrittenTag<TOwner>), value);

        return new HandWrittenTag<TOwner>(value);
    }

    public static bool TryCreate(string value, out HandWrittenTag<TOwner> result) => TryCreate(value, out result, out _);

    public static bool TryCreate(string value, out HandWrittenTag<TOwner> result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new HandWrittenTag<TOwner>(value) : default;

        return validation.IsValid;
    }

    public static HandWrittenTag<TOwner> CreateUnchecked(string value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out HandWrittenTag<TOwner> result, out ValidationResult validation)
        => TryCreate(text.ToString(), out result, out validation);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out HandWrittenTag<TOwner> result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out HandWrittenTag<TOwner> result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static HandWrittenTag<TOwner> Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());

    public static HandWrittenTag<TOwner> Parse(string s, IFormatProvider? provider) => Create(s);

    public bool Equals(HandWrittenTag<TOwner> other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is HandWrittenTag<TOwner> other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public int CompareTo(HandWrittenTag<TOwner> other) => string.CompareOrdinal(Value, other.Value);

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

    public static bool operator ==(HandWrittenTag<TOwner> left, HandWrittenTag<TOwner> right) => left.Equals(right);

    public static bool operator !=(HandWrittenTag<TOwner> left, HandWrittenTag<TOwner> right) => !left.Equals(right);

    public static bool operator <(HandWrittenTag<TOwner> left, HandWrittenTag<TOwner> right) => left.CompareTo(right) < 0;

    public static bool operator >(HandWrittenTag<TOwner> left, HandWrittenTag<TOwner> right) => left.CompareTo(right) > 0;

    public static bool operator <=(HandWrittenTag<TOwner> left, HandWrittenTag<TOwner> right) => left.CompareTo(right) <= 0;

    public static bool operator >=(HandWrittenTag<TOwner> left, HandWrittenTag<TOwner> right) => left.CompareTo(right) >= 0;
}
