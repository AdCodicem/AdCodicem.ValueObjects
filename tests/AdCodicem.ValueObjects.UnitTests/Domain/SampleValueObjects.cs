namespace AdCodicem.ValueObjects.UnitTests.Domain;

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
    /// <summary>Gets the domain part of the address.</summary>
    public ReadOnlySpan<char> Domain => Value.AsSpan()[(Value.IndexOf('@') + 1)..];

    public static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();
}

/// <summary>
/// A monetary amount in the ambient currency, never negative.
/// </summary>
[ValueObject<decimal>(Arithmetic = true, Minimum = "0", Example = "1250.00")]
public readonly partial struct Amount : IValueObjectNormalizer<decimal>
{
    /// <summary>
    /// Rounds to the cent, the only precision a monetary amount is allowed to carry, and pins the scale so that
    /// every amount reads and serializes with two decimals. Adding a zero of scale two is what pins it: decimal
    /// addition keeps the larger of the two scales.
    /// </summary>
    public static decimal NormalizeValue(decimal value) => decimal.Round(value, 2, MidpointRounding.ToEven) + 0.00m;
}

/// <summary>
/// A share of a whole, between 0 and 100.
/// </summary>
[ValueObject<decimal>(Arithmetic = true, Minimum = "0", Maximum = "100", SchemaFormat = "percentage")]
public readonly partial struct Percentage
{
    /// <summary>Applies this percentage to an amount.</summary>
    /// <param name="amount">Amount to take a share of.</param>
    /// <returns>The share of <paramref name="amount"/>.</returns>
    public Amount Of(Amount amount) => Amount.Create(amount.Value * Value / 100m);
}

/// <summary>
/// The identifier of a customer.
/// </summary>
[ValueObject<Guid>]
public readonly partial struct CustomerId : IValueObjectValidator<Guid>
{
    /// <summary>Creates a new identifier, sequential enough to keep a clustered index happy.</summary>
    /// <returns>A new identifier.</returns>
    public static CustomerId New() => CreateUnchecked(Guid.CreateVersion7());

    public static ValidationResult ValidateValue(in Guid value)
        => value == Guid.Empty
            ? ValidationResult.Required("A customer identifier must not be empty.")
            : ValidationResult.Success;
}

/// <summary>
/// An ISO 3166-1 alpha-2 country code restricted to the countries the application serves.
/// </summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, MinLength = 2, MaxLength = 2)]
[KnownValue("France", "FR", Description = "France")]
[KnownValue("Belgium", "BE", Description = "Belgium")]
[KnownValue("Luxembourg", "LU", Description = "Luxembourg")]
public readonly partial struct CountryCode : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}

/// <summary>
/// A date of birth, which must be in the past and within a plausible human lifespan.
/// </summary>
[ValueObject<DateOnly>(Minimum = "1900-01-01", Maximum = "2100-12-31")]
public readonly partial struct BirthDate
{
    /// <summary>Computes the age reached at a given date.</summary>
    /// <param name="on">Date to compute the age at.</param>
    /// <returns>The number of full years elapsed.</returns>
    public int AgeOn(DateOnly on)
    {
        var age = on.Year - Value.Year;
        return on < Value.AddYears(age) ? age - 1 : age;
    }
}

/// <summary>
/// A quantity of items, tested to cover the narrow integer promotion path.
/// </summary>
[ValueObject<short>(Arithmetic = true, Minimum = "0", Maximum = "1000")]
public readonly partial struct Quantity;

/// <summary>
/// A value object nested in another type, to cover the containing-type emission path.
/// </summary>
public static partial class Ordering
{
    /// <summary>The human-readable reference of an order.</summary>
    [ValueObject<string>(MinLength = 3, MaxLength = 20, Comparison = StringComparison.OrdinalIgnoreCase)]
    public readonly partial struct OrderReference : IValueObjectNormalizer<string>
    {
        public static string NormalizeValue(string value) => value.Trim();
    }
}
