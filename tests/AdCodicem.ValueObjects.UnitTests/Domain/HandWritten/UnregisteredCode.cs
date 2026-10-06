using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

/// <summary>
/// A string value object written by hand that nothing ever registers.
/// </summary>
/// <remarks>
/// Reserved for the views of an unregistered type: the contract kit's, and the length Entity Framework Core's
/// per-property mapping finds for it. Resolving it through <c>ValueObjectRegistry.TryResolve</c> would register it
/// by reflection for the rest of the process, so nothing may resolve it: the tests that use it would then depend
/// on the order they ran in. A lookup through <c>ValueObjectRegistry.TryGet</c> registers nothing. The native AOT
/// application links it to show the JSON factory refusing a value object nothing registered, where no dynamic code runs.
/// </remarks>
public readonly struct UnregisteredCode : IValueObject<UnregisteredCode, string>
{
    private readonly string? _value;

    private UnregisteredCode(string value) => _value = value;

    /// <summary>Declares no rule a reflection-driven caller could publish.</summary>
    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;

    public string Value => _value ?? string.Empty;

    public bool IsDefault => _value is null;

    public static string Normalize(string value) => value;

    public static ValidationResult Validate(in string value)
        => string.IsNullOrEmpty(value) ? ValidationResult.Required() : ValidationResult.Success;

    public static UnregisteredCode Create(string value)
    {
        Validate(value).ThrowIfInvalid(typeof(UnregisteredCode), value);

        return new UnregisteredCode(value);
    }

    public static bool TryCreate(string value, out UnregisteredCode result) => TryCreate(value, out result, out _);

    public static bool TryCreate(string value, out UnregisteredCode result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new UnregisteredCode(value) : default;

        return validation.IsValid;
    }

    public static UnregisteredCode CreateUnchecked(string value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out UnregisteredCode result, out ValidationResult validation)
        => TryCreate(text.ToString(), out result, out validation);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out UnregisteredCode result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out UnregisteredCode result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static UnregisteredCode Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());

    public static UnregisteredCode Parse(string s, IFormatProvider? provider) => Create(s);

    public bool Equals(UnregisteredCode other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is UnregisteredCode other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public int CompareTo(UnregisteredCode other) => string.CompareOrdinal(Value, other.Value);

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

    public static bool operator ==(UnregisteredCode left, UnregisteredCode right) => left.Equals(right);

    public static bool operator !=(UnregisteredCode left, UnregisteredCode right) => !left.Equals(right);

    public static bool operator <(UnregisteredCode left, UnregisteredCode right) => left.CompareTo(right) < 0;

    public static bool operator >(UnregisteredCode left, UnregisteredCode right) => left.CompareTo(right) > 0;

    public static bool operator <=(UnregisteredCode left, UnregisteredCode right) => left.CompareTo(right) <= 0;

    public static bool operator >=(UnregisteredCode left, UnregisteredCode right) => left.CompareTo(right) >= 0;
}
