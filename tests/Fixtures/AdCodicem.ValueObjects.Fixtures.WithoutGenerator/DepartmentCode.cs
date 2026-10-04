using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Fixtures.WithoutGenerator;

/// <summary>
/// A French department code written by hand, with no annotation, declaring its pattern through the hook and publishing
/// it in its schema alone.
/// </summary>
public readonly partial struct DepartmentCode : IValueObject<DepartmentCode, string>, IValueObjectPatternValidator
{
    private readonly string? _value;

    private DepartmentCode(string value) => _value = value;

    /// <summary>Publishes the text of the pattern its hook matches, as the generator writes it from the attribute.</summary>
    public static ValueObjectSchema Schema { get; } = new() { Pattern = "^[0-9]{2}$" };

    [GeneratedRegex("^[0-9]{2}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    public string Value => _value ?? string.Empty;

    public bool IsDefault => _value is null;

    public static string Normalize(string value) => value;

    public static ValidationResult Validate(in string value)
        => Pattern.IsMatch(value) ? ValidationResult.Success : ValidationResult.InvalidFormat("Two digits.");

    public static DepartmentCode Create(string value)
    {
        Validate(value).ThrowIfInvalid(typeof(DepartmentCode), value);

        return new DepartmentCode(value);
    }

    public static bool TryCreate(string value, out DepartmentCode result) => TryCreate(value, out result, out _);

    public static bool TryCreate(string value, out DepartmentCode result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new DepartmentCode(value) : default;

        return validation.IsValid;
    }

    public static DepartmentCode CreateUnchecked(string value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out DepartmentCode result, out ValidationResult validation)
        => TryCreate(text.ToString(), out result, out validation);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out DepartmentCode result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out DepartmentCode result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static DepartmentCode Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());

    public static DepartmentCode Parse(string s, IFormatProvider? provider) => Create(s);

    public bool Equals(DepartmentCode other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is DepartmentCode other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public int CompareTo(DepartmentCode other) => string.CompareOrdinal(Value, other.Value);

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

    public static bool operator ==(DepartmentCode left, DepartmentCode right) => left.Equals(right);

    public static bool operator !=(DepartmentCode left, DepartmentCode right) => !left.Equals(right);

    public static bool operator <(DepartmentCode left, DepartmentCode right) => left.CompareTo(right) < 0;

    public static bool operator >(DepartmentCode left, DepartmentCode right) => left.CompareTo(right) > 0;

    public static bool operator <=(DepartmentCode left, DepartmentCode right) => left.CompareTo(right) <= 0;

    public static bool operator >=(DepartmentCode left, DepartmentCode right) => left.CompareTo(right) >= 0;
}
