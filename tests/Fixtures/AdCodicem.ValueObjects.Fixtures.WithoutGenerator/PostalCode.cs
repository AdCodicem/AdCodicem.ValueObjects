using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Annotations;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Fixtures.WithoutGenerator;

/// <summary>
/// A five-digit postal code written by hand, annotated and declaring its pattern through the hook: its schema
/// publishes the length of the one and the pattern of the other.
/// </summary>
[ValueObject<string>(MaxLength = 5)]
public readonly partial struct PostalCode : IValueObject<PostalCode, string>, IValueObjectPatternValidator
{
    private readonly string? _value;

    private PostalCode(string value) => _value = value;

    public static ValueObjectSchema Schema { get; } = new() { Pattern = "^[0-9]{5}$", MaxLength = 5 };

    [GeneratedRegex("^[0-9]{5}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    public string Value => _value ?? string.Empty;

    public bool IsDefault => _value is null;

    public static string Normalize(string value) => value;

    public static ValidationResult Validate(in string value)
        => Pattern.IsMatch(value) ? ValidationResult.Success : ValidationResult.InvalidFormat("Five digits.");

    public static PostalCode Create(string value)
    {
        Validate(value).ThrowIfInvalid(typeof(PostalCode), value);

        return new PostalCode(value);
    }

    public static bool TryCreate(string value, out PostalCode result) => TryCreate(value, out result, out _);

    public static bool TryCreate(string value, out PostalCode result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new PostalCode(value) : default;

        return validation.IsValid;
    }

    public static PostalCode CreateUnchecked(string value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out PostalCode result, out ValidationResult validation)
        => TryCreate(text.ToString(), out result, out validation);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out PostalCode result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out PostalCode result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static PostalCode Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());

    public static PostalCode Parse(string s, IFormatProvider? provider) => Create(s);

    public bool Equals(PostalCode other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is PostalCode other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public int CompareTo(PostalCode other) => string.CompareOrdinal(Value, other.Value);

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

    public static bool operator ==(PostalCode left, PostalCode right) => left.Equals(right);

    public static bool operator !=(PostalCode left, PostalCode right) => !left.Equals(right);

    public static bool operator <(PostalCode left, PostalCode right) => left.CompareTo(right) < 0;

    public static bool operator >(PostalCode left, PostalCode right) => left.CompareTo(right) > 0;

    public static bool operator <=(PostalCode left, PostalCode right) => left.CompareTo(right) <= 0;

    public static bool operator >=(PostalCode left, PostalCode right) => left.CompareTo(right) >= 0;
}
