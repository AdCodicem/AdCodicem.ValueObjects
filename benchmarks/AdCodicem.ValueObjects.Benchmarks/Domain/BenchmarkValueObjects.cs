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
public readonly partial struct Iban
{
    private static string NormalizeCore(string value) => Normalization.Strip(value);

    private static ValidationResult ValidateCore(in string value)
        => Normalization.HasValidCheckDigits(value)
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");
}

/// <summary>
/// An amount, as the framework generates it.
/// </summary>
[ValueObject<decimal>(Minimum = "0", Arithmetic = true)]
public readonly partial struct Amount
{
    private static decimal NormalizeCore(decimal value)
        => decimal.Round(value, 2, MidpointRounding.ToEven) + 0.00m;
}

/// <summary>
/// A customer identifier, as the framework generates it.
/// </summary>
[ValueObject<Guid>]
public readonly partial struct CustomerId;

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
    /// <summary>Strips separators and upper-cases, in a single pass and a single allocation.</summary>
    /// <param name="value">Text to normalize.</param>
    /// <returns>The normalized text.</returns>
    public static string Strip(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var length = 0;
        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character) && character != '-')
            {
                length++;
            }
        }

        return string.Create(length, value, static (destination, source) =>
        {
            var index = 0;
            foreach (var character in source)
            {
                if (!char.IsWhiteSpace(character) && character != '-')
                {
                    destination[index++] = char.ToUpperInvariant(character);
                }
            }
        });
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
