using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace AdCodicem.ValueObjects.Benchmarks.Domain;

/// <summary>
/// An IBAN, as the framework generates it.
/// </summary>
[ValueObject<string>(
    MinLength = 15,
    MaxLength = 34,
    ImplicitConversionToValue = true)]
public readonly partial struct Iban
    : IValueObjectNormalizer<string>, IValueObjectSpanNormalizer, IValueObjectPatternValidator, IValueObjectValidator<string>
{
    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    public static string NormalizeValue(string value) => Normalization.Strip(value.AsSpan());

    /// <summary>The span overload the generator routes parsing and JSON reading through.</summary>
    public static string NormalizeValue(ReadOnlySpan<char> value) => Normalization.Strip(value);

    public static ValidationResult ValidateValue(in string value)
        => Normalization.HasValidCheckDigits(value)
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");
}

/// <summary>
/// An amount, as the framework generates it.
/// </summary>
/// <remarks>
/// Its bound is the deprecated text option, as when the published numbers were measured. The hook replacing it reads
/// a static readonly field, which the optimizing JIT folds into the check once the field is initialized.
/// </remarks>
#pragma warning disable VO0028 // Kept as measured; see the remarks.
[ValueObject<decimal>(Minimum = "0", Arithmetic = true)]
#pragma warning restore VO0028
public readonly partial struct Amount : IValueObjectNormalizer<decimal>
{
    public static decimal NormalizeValue(decimal value)
        => decimal.Round(value, 2, MidpointRounding.ToEven) + 0.00m;
}

/// <summary>
/// A customer identifier, as the framework generates it.
/// </summary>
[ValueObject<Guid>]
public readonly partial struct CustomerId;

/// <summary>
/// A closed set, whose members are boxed once and shared by the boxed paths.
/// </summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, MinLength = 2, MaxLength = 2)]
[KnownValue("France", "FR")]
[KnownValue("Belgium", "BE")]
[KnownValue("Germany", "DE")]
[KnownValue("Spain", "ES")]
[KnownValue("Italy", "IT")]
public readonly partial struct CountryCode : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.ToUpperInvariant();
}

/// <summary>
/// A basic bank account number with a masked format, written by a span formatter.
/// </summary>
[ValueObject<string>(MinLength = 23, MaxLength = 23)]
public readonly partial struct Bban : IValueObjectFormatter<string>
{
    public static bool TryFormatValue(
        in string value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        _ = provider;

        if (destination.Length < value.Length)
        {
            charsWritten = 0;
            return false;
        }

        value.CopyTo(destination);
        if (format is "M")
        {
            Masking.Mask(destination[..value.Length]);
        }

        charsWritten = value.Length;
        return true;
    }
}

/// <summary>
/// The same rule as <see cref="Bban"/>, written by a string formatter instead.
/// </summary>
/// <remarks>
/// The two differ in one thing only: this hook returns a string where <see cref="Bban"/> writes into a span.
/// </remarks>
[ValueObject<string>(MinLength = 23, MaxLength = 23)]
public readonly partial struct StringFormattedBban : IValueObjectStringFormatter<string>
{
    public static string FormatValue(in string value, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        _ = provider;

        return format is "M"
            ? string.Create(value.Length, value, static (destination, text) =>
            {
                text.CopyTo(destination);
                Masking.Mask(destination);
            })
            : value;
    }
}

/// <summary>
/// A UTC date and time, formatted and parsed in its round-trip form, kind included.
/// </summary>
[ValueObject<DateTime>]
public readonly partial struct RecordedAt;

/// <summary>
/// An instant, formatted and parsed in its round-trip form, offset included.
/// </summary>
[ValueObject<DateTimeOffset>]
public readonly partial struct OccurredAt;

/// <summary>
/// The same wrapper written by hand as a struct, with no generated code at all.
/// </summary>
/// <remarks>
/// Paired with <see cref="ClassWrapper"/>, this isolates the cost of the type kind itself. Both hold one string
/// and do exactly the same work, so any difference between them is the struct-versus-class decision and nothing
/// else. Neither is meant to be a good value object: they carry no rules, only the wrapper.
/// </remarks>
public readonly struct StructWrapper(string value) : IEquatable<StructWrapper>, IComparable<StructWrapper>
{
    private readonly string? _value = value;

    public string Value => _value ?? string.Empty;

    public bool Equals(StructWrapper other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is StructWrapper other && Equals(other);

    public override int GetHashCode() => _value is null ? 0 : _value.GetHashCode(StringComparison.Ordinal);

    public int CompareTo(StructWrapper other) => string.CompareOrdinal(_value, other._value);

    public override string ToString() => Value;

    public static bool operator ==(StructWrapper left, StructWrapper right) => left.Equals(right);

    public static bool operator !=(StructWrapper left, StructWrapper right) => !left.Equals(right);

    public static bool operator <(StructWrapper left, StructWrapper right) => left.CompareTo(right) < 0;

    public static bool operator <=(StructWrapper left, StructWrapper right) => left.CompareTo(right) <= 0;

    public static bool operator >(StructWrapper left, StructWrapper right) => left.CompareTo(right) > 0;

    public static bool operator >=(StructWrapper left, StructWrapper right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// The same wrapper written by hand as a class. See <see cref="StructWrapper"/>.
/// </summary>
public sealed class ClassWrapper(string value) : IEquatable<ClassWrapper>, IComparable<ClassWrapper>
{
    private readonly string _value = value;

    public string Value => _value;

    public bool Equals(ClassWrapper? other)
        => other is not null && string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as ClassWrapper);

    public override int GetHashCode() => _value.GetHashCode(StringComparison.Ordinal);

    public int CompareTo(ClassWrapper? other)
        => other is null ? 1 : string.CompareOrdinal(_value, other._value);

    public override string ToString() => _value;
}

/// <summary>
/// The IBAN rules, shared by every variant so that no benchmark measures a different algorithm.
/// </summary>
public static class Normalization
{
    /// <summary>Characters normalization puts on the stack before falling back to the heap.</summary>
    private const int StackLimit = 64;

    /// <summary>Strips separators and upper-cases, allocating only the result.</summary>
    /// <param name="value">Text to normalize.</param>
    /// <returns>The normalized text.</returns>
    public static string Strip(ReadOnlySpan<char> value)
    {
        Span<char> buffer = value.Length <= StackLimit ? stackalloc char[StackLimit] : new char[value.Length];

        var length = 0;
        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character) && character != '-')
            {
                buffer[length++] = char.ToUpperInvariant(character);
            }
        }

        return new string(buffer[..length]);
    }

    /// <summary>Verifies the ISO 7064 MOD-97-10 check digits without allocating.</summary>
    /// <param name="value">Normalized IBAN.</param>
    /// <returns><see langword="true"/> when the check digits hold.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Not a normalization, an arithmetic remainder.")]
    public static bool HasValidCheckDigits(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 15)
        {
            return false;
        }

        var remainder = 0;
        for (var pass = 0; pass < 2; pass++)
        {
            // The first pass walks the account part, the second the country code and check digits, which is
            // what "move the first four characters to the end" means without moving anything.
            var start = pass == 0 ? 4 : 0;
            var end = pass == 0 ? value.Length : 4;

            for (var index = start; index < end; index++)
            {
                var character = value[index];
                if (char.IsAsciiDigit(character))
                {
                    remainder = ((remainder * 10) + (character - '0')) % 97;
                }
                else if (char.IsAsciiLetterUpper(character))
                {
                    remainder = ((remainder * 100) + (character - 'A' + 10)) % 97;
                }
                else
                {
                    return false;
                }
            }
        }

        return remainder == 1;
    }
}

/// <summary>
/// The masking rule, shared by both formatter hooks so that they differ only in how they hand the text back.
/// </summary>
public static class Masking
{
    /// <summary>Hides every character but the first two and the last four.</summary>
    /// <param name="text">Text to mask in place.</param>
    public static void Mask(Span<char> text) => text[2..^4].Fill('*');
}
