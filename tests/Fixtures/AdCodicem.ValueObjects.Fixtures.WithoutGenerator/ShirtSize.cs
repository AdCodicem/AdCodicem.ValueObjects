using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.WithoutGenerator;

/// <summary>
/// A shirt size from a closed set, written by hand, whose normalizer upper-cases what it is given: its known values are
/// declared in lower case, which is not what the type accepts and writes.
/// </summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, MaxLength = 2)]
[KnownValue("Small", "s")]
[KnownValue("Medium", "m")]
public readonly struct ShirtSize : IValueObject<ShirtSize, string>
{
    private readonly string? _value;

    private ShirtSize(string value) => _value = value;

    public string Value => _value ?? string.Empty;

    public bool IsDefault => _value is null;

    public static string Normalize(string value) => value.ToUpperInvariant();

    public static ValidationResult Validate(in string value)
        => value is "S" or "M" ? ValidationResult.Success : ValidationResult.InvalidFormat("S or M.");

    public static ShirtSize Create(string value)
    {
        var normalized = Normalize(value);
        Validate(normalized).ThrowIfInvalid(typeof(ShirtSize), normalized);

        return new ShirtSize(normalized);
    }

    public static bool TryCreate(string value, out ShirtSize result) => TryCreate(value, out result, out _);

    public static bool TryCreate(string value, out ShirtSize result, out ValidationResult validation)
    {
        var normalized = Normalize(value);
        validation = Validate(normalized);
        result = validation.IsValid ? new ShirtSize(normalized) : default;

        return validation.IsValid;
    }

    public static ShirtSize CreateUnchecked(string value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out ShirtSize result, out ValidationResult validation)
        => TryCreate(text.ToString(), out result, out validation);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out ShirtSize result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out ShirtSize result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static ShirtSize Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());

    public static ShirtSize Parse(string s, IFormatProvider? provider) => Create(s);

    public bool Equals(ShirtSize other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ShirtSize other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public int CompareTo(ShirtSize other) => string.CompareOrdinal(Value, other.Value);

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

    public static bool operator ==(ShirtSize left, ShirtSize right) => left.Equals(right);

    public static bool operator !=(ShirtSize left, ShirtSize right) => !left.Equals(right);

    public static bool operator <(ShirtSize left, ShirtSize right) => left.CompareTo(right) < 0;

    public static bool operator >(ShirtSize left, ShirtSize right) => left.CompareTo(right) > 0;

    public static bool operator <=(ShirtSize left, ShirtSize right) => left.CompareTo(right) <= 0;

    public static bool operator >=(ShirtSize left, ShirtSize right) => left.CompareTo(right) >= 0;
}
