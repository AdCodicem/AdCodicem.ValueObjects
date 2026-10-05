using System.Text.RegularExpressions;

namespace AdCodicem.ValueObjects.Benchmarks.Domain;

/// <summary>
/// The IBAN of <see cref="Iban"/>, its shape checked by a <see cref="Regex"/> built at run time, as the
/// <c>Pattern</c> option built it before 0.3.0 removed it.
/// </summary>
[ValueObject<string>(MinLength = 15, MaxLength = 34)]
public readonly partial struct IbanByRuntimeRegex : IValueObjectNormalizer<string>, IValueObjectSpanNormalizer, IValueObjectValidator<string>
{
    public static string NormalizeValue(string value) => Normalization.Strip(value.AsSpan());

    public static string NormalizeValue(ReadOnlySpan<char> value) => Normalization.Strip(value);

    public static ValidationResult ValidateValue(in string value)
        => !Shapes.CompiledIban.IsMatch(value)
            ? ValidationResult.InvalidFormat("The value does not match the expected format.")
            : Normalization.HasValidCheckDigits(value)
                ? ValidationResult.Success
                : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");
}

/// <summary>
/// The IBAN of <see cref="Iban"/>, its shape checked by hand in the validator, with no regular expression at all.
/// </summary>
[ValueObject<string>(MinLength = 15, MaxLength = 34)]
public readonly partial struct IbanByHand : IValueObjectNormalizer<string>, IValueObjectSpanNormalizer, IValueObjectValidator<string>
{
    public static string NormalizeValue(string value) => Normalization.Strip(value.AsSpan());

    public static string NormalizeValue(ReadOnlySpan<char> value) => Normalization.Strip(value);

    public static ValidationResult ValidateValue(in string value)
        => !Shapes.IsIban(value)
            ? ValidationResult.InvalidFormat("The value does not match the expected format.")
            : Normalization.HasValidCheckDigits(value)
                ? ValidationResult.Success
                : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");
}

/// <summary>
/// The IBAN of <see cref="Iban"/> with no check of its shape: the floor the other three are measured against.
/// </summary>
[ValueObject<string>(MinLength = 15, MaxLength = 34)]
public readonly partial struct IbanCheckDigitsOnly : IValueObjectNormalizer<string>, IValueObjectSpanNormalizer, IValueObjectValidator<string>
{
    public static string NormalizeValue(string value) => Normalization.Strip(value.AsSpan());

    public static string NormalizeValue(ReadOnlySpan<char> value) => Normalization.Strip(value);

    public static ValidationResult ValidateValue(in string value)
        => Normalization.HasValidCheckDigits(value)
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");
}

/// <summary>
/// A five-digit postal code, its shape the only rule it has, checked through the pattern hook.
/// </summary>
[ValueObject<string>(MinLength = 5, MaxLength = 5)]
public readonly partial struct PostalCode : IValueObjectPatternValidator
{
    [GeneratedRegex(Shapes.PostalCode, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>
/// The postal code of <see cref="PostalCode"/>, its shape checked by a <see cref="Regex"/> built at run time, as the
/// <c>Pattern</c> option built it.
/// </summary>
[ValueObject<string>(MinLength = 5, MaxLength = 5)]
public readonly partial struct PostalCodeByRuntimeRegex : IValueObjectValidator<string>
{
    public static ValidationResult ValidateValue(in string value)
        => Shapes.CompiledPostalCode.IsMatch(value)
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("The value does not match the expected format.");
}

/// <summary>
/// The postal code of <see cref="PostalCode"/>, its digits checked by hand.
/// </summary>
[ValueObject<string>(MinLength = 5, MaxLength = 5)]
public readonly partial struct PostalCodeByHand : IValueObjectValidator<string>
{
    public static ValidationResult ValidateValue(in string value)
        => Shapes.IsPostalCode(value)
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("The value does not match the expected format.");
}

/// <summary>
/// The postal code of <see cref="PostalCode"/> checked for its length only: the floor.
/// </summary>
[ValueObject<string>(MinLength = 5, MaxLength = 5)]
public readonly partial struct PostalCodeLengthsOnly;

/// <summary>
/// The shapes, written once as patterns, and once by hand to measure what a regular expression costs at all.
/// </summary>
public static partial class Shapes
{
    /// <summary>A country code, two check digits, then the account.</summary>
    public const string Iban = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$";

    /// <summary>Five digits.</summary>
    public const string PostalCode = "^[0-9]{5}$";

    /// <summary>The IBAN shape as a source-generated regular expression, for the engine measured alone.</summary>
    [GeneratedRegex(Iban, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex GeneratedIban { get; }

    /// <summary>The IBAN shape compiled at run time, with the options and the timeout the removed option used.</summary>
    public static Regex CompiledIban { get; } =
        new(Iban, RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>The postal code shape compiled at run time, as <see cref="CompiledIban"/> is.</summary>
    public static Regex CompiledPostalCode { get; } =
        new(PostalCode, RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Checks the IBAN shape by hand.</summary>
    /// <param name="value">Normalized IBAN.</param>
    /// <returns><see langword="true"/> when it has the shape.</returns>
    public static bool IsIban(string value)
    {
        if (value.Length is < 15 or > 34)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            var valid = index switch
            {
                < 2 => char.IsAsciiLetterUpper(character),
                < 4 => char.IsAsciiDigit(character),
                _ => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character),
            };

            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Checks the postal code shape by hand.</summary>
    /// <param name="value">Text to check.</param>
    /// <returns><see langword="true"/> when it is five digits.</returns>
    public static bool IsPostalCode(string value)
    {
        if (value.Length != 5)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
