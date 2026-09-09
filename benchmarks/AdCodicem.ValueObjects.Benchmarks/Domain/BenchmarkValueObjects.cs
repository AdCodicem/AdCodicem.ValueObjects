using System.Diagnostics.CodeAnalysis;

namespace AdCodicem.ValueObjects.Benchmarks.Domain;

/// <summary>
/// An IBAN, as the framework generates it.
/// </summary>
[ValueObject<string>(
    MinLength = 15,
    MaxLength = 34,
    Pattern = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
    ImplicitConversionToValue = true)]
public readonly partial struct Iban : IValueObjectNormalizer<string>, IValueObjectSpanNormalizer, IValueObjectValidator<string>
{
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
[ValueObject<decimal>(Minimum = "0", Arithmetic = true)]
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
