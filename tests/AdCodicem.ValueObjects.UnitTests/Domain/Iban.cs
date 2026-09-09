namespace AdCodicem.ValueObjects.UnitTests.Domain;

/// <summary>
/// An International Bank Account Number, stored in its electronic form.
/// </summary>
[ValueObject<string>(
    MinLength = 15,
    MaxLength = 34,
    Pattern = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
    SchemaFormat = "iban",
    Example = "FR7630006000011234567890189",
    ImplicitConversionToValue = true)]
public readonly partial struct Iban
{
    /// <summary>The named formats accepted by <see cref="ToString(string?, IFormatProvider?)"/>.</summary>
    public static class Formats
    {
        /// <summary>Electronic form, without separators. The default.</summary>
        public const string Electronic = "E";

        /// <summary>Print form, in groups of four characters.</summary>
        public const string Print = "P";

        /// <summary>Masked form, keeping only the country code and the last four characters.</summary>
        public const string Masked = "M";
    }

    /// <summary>Gets the ISO 3166 country code of the account.</summary>
    public string CountryCode => Value[..2];

    /// <summary>Strips separators and upper-cases, in a single pass and a single allocation.</summary>
    private static string NormalizeCore(string value)
    {
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
    private static ValidationResult ValidateCore(in string value)
        => HasValidCheckDigits(value)
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");

    private static bool HasValidCheckDigits(ReadOnlySpan<char> value)
    {
        // The first four characters move to the end, letters become two digits, then the whole number mod 97 must be 1.
        var remainder = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[(i + 4) % value.Length];
            remainder = char.IsAsciiDigit(character)
                ? ((remainder * 10) + (character - '0')) % 97
                : ((remainder * 100) + (character - 'A' + 10)) % 97;
        }

        return remainder == 1;
    }

    private static bool TryFormatCore(
        in string value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        _ = provider;

        return format switch
        {
            "P" or "p" => TryFormatGrouped(value, destination, out charsWritten),
            "M" or "m" => TryFormatMasked(value, destination, out charsWritten),
            _ => value.AsSpan().TryCopyTo(destination)
                ? Written(value.Length, out charsWritten)
                : NotWritten(out charsWritten),
        };
    }

    private static bool TryFormatGrouped(ReadOnlySpan<char> value, Span<char> destination, out int charsWritten)
    {
        var required = value.Length + ((value.Length - 1) / 4);
        if (destination.Length < required)
        {
            return NotWritten(out charsWritten);
        }

        var written = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                destination[written++] = ' ';
            }

            destination[written++] = value[i];
        }

        return Written(written, out charsWritten);
    }

    private static bool TryFormatMasked(ReadOnlySpan<char> value, Span<char> destination, out int charsWritten)
    {
        if (destination.Length < value.Length)
        {
            return NotWritten(out charsWritten);
        }

        value.CopyTo(destination);
        destination[2..(value.Length - 4)].Fill('*');

        return Written(value.Length, out charsWritten);
    }

    private static bool Written(int count, out int charsWritten)
    {
        charsWritten = count;
        return true;
    }

    private static bool NotWritten(out int charsWritten)
    {
        charsWritten = 0;
        return false;
    }
}
