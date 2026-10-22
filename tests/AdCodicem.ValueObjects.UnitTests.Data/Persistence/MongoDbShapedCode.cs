using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Metadata;

#pragma warning disable CA1000 // The static members are the contract of IValueObject<TSelf, TValue>, on a generic type.

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// A code of three letters or more, written by hand, whose pattern is matched under an option that changes what the
/// pattern text means, which the text alone cannot say: the MongoDB validator has to leave the pattern out.
/// </summary>
/// <typeparam name="TShape">The pattern, and the option it is matched under.</typeparam>
/// <remarks>
/// Written by hand because the generator warns of such an option (VO0025), and a source generator's diagnostic ignores
/// <c>#pragma</c>. It implements the pattern hook explicitly, which the validator has to reach as well.
/// </remarks>
public readonly struct ShapedCode<TShape> : IValueObject<ShapedCode<TShape>, string>, IValueObjectPatternValidator
    where TShape : ICodeShape
{
    private readonly string? _value;

    private ShapedCode(string value) => _value = value;

    public static ValueObjectSchema Schema { get; } = new() { Pattern = TShape.Shape.ToString(), MinLength = 3 };

    static Regex IValueObjectPatternValidator.Pattern => TShape.Shape;

    public string Value => _value ?? string.Empty;

    public bool IsDefault => _value is null;

    public static string Normalize(string value) => value;

    public static ValidationResult Validate(in string value)
        => value is null || value.Length < 3 || !TShape.Shape.IsMatch(value)
            ? ValidationResult.Failure(ValueObjectErrorCodes.InvalidFormat, "Three letters.")
            : ValidationResult.Success;

    public static ShapedCode<TShape> Create(string value)
    {
        Validate(value).ThrowIfInvalid(typeof(ShapedCode<TShape>), value);

        return new ShapedCode<TShape>(value);
    }

    public static bool TryCreate(string value, out ShapedCode<TShape> result) => TryCreate(value, out result, out _);

    public static bool TryCreate(string value, out ShapedCode<TShape> result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new ShapedCode<TShape>(value) : default;

        return validation.IsValid;
    }

    public static ShapedCode<TShape> CreateUnchecked(string value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out ShapedCode<TShape> result, out ValidationResult validation)
        => TryCreate(text.ToString(), out result, out validation);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out ShapedCode<TShape> result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out ShapedCode<TShape> result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static ShapedCode<TShape> Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());

    public static ShapedCode<TShape> Parse(string s, IFormatProvider? provider) => Create(s);

    public bool Equals(ShapedCode<TShape> other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ShapedCode<TShape> other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public int CompareTo(ShapedCode<TShape> other) => string.CompareOrdinal(Value, other.Value);

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

    public static bool operator ==(ShapedCode<TShape> left, ShapedCode<TShape> right) => left.Equals(right);

    public static bool operator !=(ShapedCode<TShape> left, ShapedCode<TShape> right) => !left.Equals(right);

    public static bool operator <(ShapedCode<TShape> left, ShapedCode<TShape> right) => left.CompareTo(right) < 0;

    public static bool operator >(ShapedCode<TShape> left, ShapedCode<TShape> right) => left.CompareTo(right) > 0;

    public static bool operator <=(ShapedCode<TShape> left, ShapedCode<TShape> right) => left.CompareTo(right) <= 0;

    public static bool operator >=(ShapedCode<TShape> left, ShapedCode<TShape> right) => left.CompareTo(right) >= 0;
}

/// <summary>The pattern of a <see cref="ShapedCode{TShape}"/>, with the option it is matched under.</summary>
public interface ICodeShape
{
    /// <summary>Gets the regular expression.</summary>
    static abstract Regex Shape { get; }
}

/// <summary>Three letters in either case: <c>ABC</c> matches, which the text alone refuses.</summary>
public sealed partial class CaseFree : ICodeShape
{
    [GeneratedRegex("^[a-z]{3}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Shape { get; }
}

/// <summary>A line of three letters: <c>abc</c> then a line feed and anything matches, which the text alone refuses.</summary>
public sealed partial class LineByLine : ICodeShape
{
    [GeneratedRegex("^[a-z]{3}$", RegexOptions.Multiline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Shape { get; }
}

/// <summary>Three letters, the spaces of the pattern ignored: <c>abc</c> matches, which the text alone refuses.</summary>
public sealed partial class SpacedOut : ICodeShape
{
    [GeneratedRegex("^ [a-z]{3} $", RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Shape { get; }
}
