using System.Text.RegularExpressions;

namespace AdCodicem.ValueObjects.Sample.Domain;

/// <summary>
/// The identifier of a customer.
/// </summary>
[ValueObject<Guid>]
public readonly partial struct CustomerId : IValueObjectValidator<Guid>, IValueObjectExample<CustomerId>
{
    public static CustomerId Example => Create(new Guid("0193b1c0-0000-7000-8000-000000000001"));

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
    SchemaFormat = "email")]
public readonly partial struct EmailAddress : IValueObjectNormalizer<string>, IValueObjectPatternValidator, IValueObjectExample<EmailAddress>
{
    public static EmailAddress Example => Create("ada@example.com");

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    public static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();
}

/// <summary>
/// An International Bank Account Number, stored in its electronic form.
/// </summary>
[ValueObject<string>(
    MinLength = 15,
    MaxLength = 34,
    SchemaFormat = "iban")]
public readonly partial struct Iban : IValueObjectNormalizer<string>, IValueObjectPatternValidator, IValueObjectValidator<string>, IValueObjectExample<Iban>
{
    public static Iban Example => Create("FR7630006000011234567890189");

    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

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
[ValueObject<decimal>(Arithmetic = true)]
public readonly partial struct Amount : IValueObjectNormalizer<decimal>, IValueObjectMinimum<decimal>, IValueObjectExample<Amount>
{
    public static Amount Example => Create(1250.00m);

    /// <summary>Gets the smallest amount: an amount is never negative.</summary>
    public static decimal Minimum => 0m;

    public static decimal NormalizeValue(decimal value) => decimal.Round(value, 2, MidpointRounding.ToEven) + 0.00m;
}

/// <summary>
/// A country the bank operates in.
/// </summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, MinLength = 2, MaxLength = 2, SchemaFormat = "iso-3166-alpha2")]
public readonly partial struct CountryCode : IValueObjectNormalizer<string>
{
    [KnownValue(Description = "France")]
    public static readonly CountryCode France = Known("FR");

    [KnownValue(Description = "Belgium")]
    public static readonly CountryCode Belgium = Known("BE");

    [KnownValue(Description = "Luxembourg")]
    public static readonly CountryCode Luxembourg = Known("LU");

    [KnownValue(Description = "Germany")]
    public static readonly CountryCode Germany = Known("DE");

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
