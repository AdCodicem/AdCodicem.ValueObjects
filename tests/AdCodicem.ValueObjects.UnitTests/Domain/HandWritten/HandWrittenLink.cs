using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

/// <summary>
/// A value object written by hand over <see cref="Uri"/>, which is none of the 22 underlying types the generator
/// supports: an absolute link.
/// </summary>
/// <remarks>
/// Writing one by hand is the only way to carry such a type, and the serializers then hand the value to their own
/// handling of it. Nothing registers it; the registry describes it by reflection the first time it is resolved.
/// </remarks>
public readonly struct HandWrittenLink : IValueObject<HandWrittenLink, Uri>
{
    private readonly Uri? _value;

    private HandWrittenLink(Uri value) => _value = value;

    /// <summary>Declares no rule a reflection-driven caller could publish.</summary>
    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;

    public Uri Value => _value ?? new Uri("about:blank");

    public bool IsDefault => _value is null;

    public static Uri Normalize(Uri value) => value;

    public static ValidationResult Validate(in Uri value)
        => value switch
        {
            null => ValidationResult.Required("A link is required."),
            { IsAbsoluteUri: false } => ValidationResult.InvalidFormat("A link is an absolute URI."),
            _ => ValidationResult.Success,
        };

    public static HandWrittenLink Create(Uri value)
    {
        Validate(value).ThrowIfInvalid(typeof(HandWrittenLink), value);

        return new HandWrittenLink(value);
    }

    public static bool TryCreate(Uri value, out HandWrittenLink result) => TryCreate(value, out result, out _);

    public static bool TryCreate(Uri value, out HandWrittenLink result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new HandWrittenLink(value) : default;

        return validation.IsValid;
    }

    public static HandWrittenLink CreateUnchecked(Uri value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out HandWrittenLink result, out ValidationResult validation)
    {
        if (Uri.TryCreate(text.ToString(), UriKind.RelativeOrAbsolute, out var link))
        {
            return TryCreate(link, out result, out validation);
        }

        result = default;
        validation = ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, "The text is not a link.");

        return false;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out HandWrittenLink result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out HandWrittenLink result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static HandWrittenLink Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => Create(new Uri(s.ToString(), UriKind.RelativeOrAbsolute));

    public static HandWrittenLink Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    public bool Equals(HandWrittenLink other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is HandWrittenLink other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public int CompareTo(HandWrittenLink other) => string.CompareOrdinal(ToString(), other.ToString());

    public override string ToString() => Value.OriginalString;

    public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        var text = ToString();
        if (text.TryCopyTo(destination))
        {
            charsWritten = text.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    public static bool operator ==(HandWrittenLink left, HandWrittenLink right) => left.Equals(right);

    public static bool operator !=(HandWrittenLink left, HandWrittenLink right) => !left.Equals(right);

    public static bool operator <(HandWrittenLink left, HandWrittenLink right) => left.CompareTo(right) < 0;

    public static bool operator >(HandWrittenLink left, HandWrittenLink right) => left.CompareTo(right) > 0;

    public static bool operator <=(HandWrittenLink left, HandWrittenLink right) => left.CompareTo(right) <= 0;

    public static bool operator >=(HandWrittenLink left, HandWrittenLink right) => left.CompareTo(right) >= 0;
}
