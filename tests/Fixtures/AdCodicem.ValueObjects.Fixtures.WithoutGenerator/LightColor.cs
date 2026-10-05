using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Annotations;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Fixtures.WithoutGenerator;

/// <summary>
/// The color of a traffic light, from a closed set, written by hand and declaring every option an annotation can carry,
/// every hook a schema publishes and its known values, which its schema restates.
/// </summary>
/// <remarks>
/// The rules are restated in the schema and the body because nothing generates them from the annotation here; the
/// schema is what a reflection-driven caller reads, and the body is what validates. Its known values are created through
/// <c>Create</c>: the <c>Known</c> factory is the generator's.
/// </remarks>
[ValueObject<string>(
    ValueSet = ValueSetKind.Closed,
    MaxLength = 6,
    SchemaFormat = "color",
    Description = "The color of a traffic light.")]
[Reviewed<LightColor>]
public readonly partial struct LightColor : IValueObject<LightColor, string>, IValueObjectPatternValidator, IValueObjectExample<LightColor>
{
    private readonly string? _value;

    private LightColor(string value) => _value = value;

    [KnownValue(Description = "Stop.")]
    public static readonly LightColor Red = Create("red");

    [KnownValue]
    public static readonly LightColor Green = Create("green");

    public static LightColor Example => Red;

    [GeneratedRegex("^[a-z]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    public static ValueObjectSchema Schema { get; } = new()
    {
        Pattern = "^[a-z]+$",
        MaxLength = 6,
        Format = "color",
        Description = "The color of a traffic light.",
        Example = "red",
        IsClosedValueSet = true,
        KnownValues = ["red", "green"],
        KnownValueDetails = [new KnownValueInfo("red", "Red", "Stop."), new KnownValueInfo("green", "Green")],
    };

    public string Value => _value ?? string.Empty;

    public bool IsDefault => _value is null;

    public static string Normalize(string value) => value;

    public static ValidationResult Validate(in string value)
        => value is "red" or "green" ? ValidationResult.Success : ValidationResult.InvalidFormat("Red or green.");

    public static LightColor Create(string value)
    {
        Validate(value).ThrowIfInvalid(typeof(LightColor), value);

        return new LightColor(value);
    }

    public static bool TryCreate(string value, out LightColor result) => TryCreate(value, out result, out _);

    public static bool TryCreate(string value, out LightColor result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new LightColor(value) : default;

        return validation.IsValid;
    }

    public static LightColor CreateUnchecked(string value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out LightColor result, out ValidationResult validation)
        => TryCreate(text.ToString(), out result, out validation);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out LightColor result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out LightColor result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static LightColor Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());

    public static LightColor Parse(string s, IFormatProvider? provider) => Create(s);

    public bool Equals(LightColor other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is LightColor other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public int CompareTo(LightColor other) => string.CompareOrdinal(Value, other.Value);

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

    public static bool operator ==(LightColor left, LightColor right) => left.Equals(right);

    public static bool operator !=(LightColor left, LightColor right) => !left.Equals(right);

    public static bool operator <(LightColor left, LightColor right) => left.CompareTo(right) < 0;

    public static bool operator >(LightColor left, LightColor right) => left.CompareTo(right) > 0;

    public static bool operator <=(LightColor left, LightColor right) => left.CompareTo(right) <= 0;

    public static bool operator >=(LightColor left, LightColor right) => left.CompareTo(right) >= 0;
}
