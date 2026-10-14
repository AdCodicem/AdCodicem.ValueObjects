using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.Blazor;

// The value objects the pages bind, declared beside them, as an application's domain declares its own.

/// <summary>An account number in the shape of an IBAN, typed in groups of four and stored without them.</summary>
[ValueObject<string>(MinLength = 15, MaxLength = 34)]
public readonly partial struct AccountNumber : IValueObjectNormalizer<string>, IValueObjectPatternValidator
{
    /// <summary>Gets the shape of an account number: a country code, two check digits, then up to thirty characters.</summary>
    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    /// <inheritdoc />
    public static string NormalizeValue(string value) => value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}

/// <summary>A quantity ordered, from one to a hundred.</summary>
[ValueObject<int>]
public readonly partial struct OrderQuantity : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    /// <inheritdoc />
    public static int Minimum => 1;

    /// <inheritdoc />
    public static int Maximum => 100;
}

/// <summary>An amount, from a cent to a thousand.</summary>
[ValueObject<decimal>]
public readonly partial struct OrderAmount : IValueObjectMinimum<decimal>, IValueObjectMaximum<decimal>
{
    /// <inheritdoc />
    public static decimal Minimum => 0.01m;

    /// <inheritdoc />
    public static decimal Maximum => 1000m;
}

/// <summary>A count of items, from none.</summary>
[ValueObject<int>]
public readonly partial struct ItemCount : IValueObjectMinimum<int>
{
    /// <inheritdoc />
    public static int Minimum => 0;
}

/// <summary>The day an order is due.</summary>
[ValueObject<DateOnly>]
public readonly partial struct DueDate;

/// <summary>The time of day an order is picked up.</summary>
[ValueObject<TimeOnly>]
public readonly partial struct PickupTime;

/// <summary>When an order was placed.</summary>
[ValueObject<DateTime>]
public readonly partial struct PlacedAt;

/// <summary>When an order was confirmed, with its offset.</summary>
[ValueObject<DateTimeOffset>]
public readonly partial struct ConfirmedAt;

/// <summary>How long an order is held.</summary>
[ValueObject<TimeSpan>]
public readonly partial struct HoldTime;
