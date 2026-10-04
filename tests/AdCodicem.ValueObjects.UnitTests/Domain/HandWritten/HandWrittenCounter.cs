using System.Globalization;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

/// <summary>
/// A numeric value object written by hand, with no <c>[ValueObject&lt;T&gt;]</c> and therefore nothing generated.
/// </summary>
/// <remarks>
/// <para>
/// Writing one by hand is supported, and it is the only way to reach what the generator always replaces: the
/// default bodies of <see cref="INumericValueObject{TSelf, TValue}"/> and of
/// <see cref="IValueObject{TValue}.Value"/>'s boxed accessor, which this type deliberately leaves undeclared, and
/// the registry's reflection fallback, since nothing registers it.
/// </para>
/// <para>
/// It accepts every <see cref="int"/>, so an overflow cannot hide behind a rejection. Its parser refuses text
/// that is not a number without saying why, as a hand-written parser may: that is the case the boxed parsing path
/// guards against.
/// </para>
/// </remarks>
public readonly struct HandWrittenCounter : INumericValueObject<HandWrittenCounter, int>
{
    private readonly int _value;

    private HandWrittenCounter(int value) => _value = value;

    /// <summary>Declares no rule a reflection-driven caller could publish.</summary>
    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;

    public int Value => _value;

    public bool IsDefault => _value == 0;

    public static int Normalize(int value) => value;

    public static ValidationResult Validate(in int value) => ValidationResult.Success;

    public static HandWrittenCounter Create(int value)
    {
        Validate(value).ThrowIfInvalid(typeof(HandWrittenCounter), value);

        return new HandWrittenCounter(value);
    }

    public static bool TryCreate(int value, out HandWrittenCounter result) => TryCreate(value, out result, out _);

    public static bool TryCreate(int value, out HandWrittenCounter result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new HandWrittenCounter(value) : default;

        return validation.IsValid;
    }

    public static HandWrittenCounter CreateUnchecked(int value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out HandWrittenCounter result, out ValidationResult validation)
    {
        if (int.TryParse(text, NumberStyles.Integer, provider, out var raw))
        {
            return TryCreate(raw, out result, out validation);
        }

        // No reason given: the contract asks for one, and a hand-written parser can forget it.
        result = default;
        validation = ValidationResult.Success;
        return false;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out HandWrittenCounter result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out HandWrittenCounter result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static HandWrittenCounter Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a counter.");

    public static HandWrittenCounter Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    public bool Equals(HandWrittenCounter other) => _value == other._value;

    public override bool Equals(object? obj) => obj is HandWrittenCounter other && Equals(other);

    public override int GetHashCode() => _value;

    public int CompareTo(HandWrittenCounter other) => _value.CompareTo(other._value);

    public override string ToString() => _value.ToString(CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider) => _value.ToString(format, formatProvider);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => _value.TryFormat(destination, out charsWritten, format, provider);

    public static HandWrittenCounter operator +(HandWrittenCounter left, HandWrittenCounter right) => Create(checked(left._value + right._value));

    public static HandWrittenCounter operator -(HandWrittenCounter left, HandWrittenCounter right) => Create(checked(left._value - right._value));

    public static HandWrittenCounter operator *(HandWrittenCounter left, int right) => Create(checked(left._value * right));

    public static HandWrittenCounter operator /(HandWrittenCounter left, int right) => Create(left._value / right);

    public static bool operator ==(HandWrittenCounter left, HandWrittenCounter right) => left.Equals(right);

    public static bool operator !=(HandWrittenCounter left, HandWrittenCounter right) => !left.Equals(right);

    public static bool operator <(HandWrittenCounter left, HandWrittenCounter right) => left.CompareTo(right) < 0;

    public static bool operator >(HandWrittenCounter left, HandWrittenCounter right) => left.CompareTo(right) > 0;

    public static bool operator <=(HandWrittenCounter left, HandWrittenCounter right) => left.CompareTo(right) <= 0;

    public static bool operator >=(HandWrittenCounter left, HandWrittenCounter right) => left.CompareTo(right) >= 0;
}
