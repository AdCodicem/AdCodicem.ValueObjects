namespace AdCodicem.ValueObjects.Sample.Api.Domain;

/// <summary>
/// The identifier of a customer.
/// </summary>
[ValueObject<Guid>(Example = "0193b1c0-0000-7000-8000-000000000001")]
public readonly partial struct CustomerId : IValueObjectValidator<Guid>
{
    /// <summary>Creates a new identifier that a clustered index can live with.</summary>
    /// <returns>A new identifier.</returns>
    public static CustomerId New() => CreateUnchecked(Guid.CreateVersion7());

    public static ValidationResult ValidateValue(in Guid value)
        => value == Guid.Empty
            ? ValidationResult.Required("A customer identifier must not be empty.")
            : ValidationResult.Success;
}

/// <summary>
/// An email address, normalized to lower case.
/// </summary>
[ValueObject<string>(
    MaxLength = 254,
    Pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
    SchemaFormat = "email",
    Example = "ada@example.com")]
public readonly partial struct EmailAddress : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();
}

/// <summary>
/// An International Bank Account Number, stored in its electronic form.
/// </summary>
[ValueObject<string>(
    MinLength = 15,
    MaxLength = 34,
    Pattern = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
    SchemaFormat = "iban",
    Example = "FR7630006000011234567890189")]
public readonly partial struct Iban : IValueObjectNormalizer<string>, IValueObjectValidator<string>
{
    /// <summary>Gets the ISO 3166 country code of the account.</summary>
    public string CountryCode => Value[..2];

    public static string NormalizeValue(string value)
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

    public static ValidationResult ValidateValue(in string value)
        => HasValidCheckDigits(value)
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");

    private static bool HasValidCheckDigits(ReadOnlySpan<char> value)
    {
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
}

/// <summary>
/// A monetary amount in euros, never negative, always carrying two decimals.
/// </summary>
[ValueObject<decimal>(Arithmetic = true, Minimum = "0", Example = "1250.00")]
public readonly partial struct Amount : IValueObjectNormalizer<decimal>
{
    public static decimal NormalizeValue(decimal value) => decimal.Round(value, 2, MidpointRounding.ToEven) + 0.00m;
}

/// <summary>
/// A country the bank operates in.
/// </summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, MinLength = 2, MaxLength = 2, SchemaFormat = "iso-3166-alpha2")]
[KnownValue("France", "FR", Description = "France")]
[KnownValue("Belgium", "BE", Description = "Belgium")]
[KnownValue("Luxembourg", "LU", Description = "Luxembourg")]
[KnownValue("Germany", "DE", Description = "Germany")]
public readonly partial struct CountryCode : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}

/// <summary>
/// The public identifier of a payment, in the shape a client sees it: <c>pay_2K7X9…</c>.
/// </summary>
/// <remarks>
/// Unlike <see cref="CustomerId"/>, which is internal and stored as a native uuid, this one crosses the API
/// boundary. The prefix is what makes it impossible to pass a customer identifier where a payment is expected,
/// and it is kept in the column so a raw SQL join cannot make that mistake either.
/// </remarks>
[EntityId("pay")]
public readonly partial struct PaymentId;
