using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.WolverineHttp;

/// <summary>The identifier of an order.</summary>
[ValueObject<Guid>]
public readonly partial struct OrderId;

/// <summary>A customer's code: three capitals, a dash and three to ten digits, at most twenty characters.</summary>
[ValueObject<string>(MaxLength = 20)]
public readonly partial struct CustomerCode : IValueObjectPatternValidator
{
    [GeneratedRegex("^[A-Z]{3}-[0-9]{3,10}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>A quantity of items, from 1 to 100.</summary>
[ValueObject<int>]
public readonly partial struct Quantity : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    public static int Minimum => 1;

    public static int Maximum => 100;
}

/// <summary>An amount, from 0 up.</summary>
[ValueObject<decimal>]
public readonly partial struct Amount : IValueObjectMinimum<decimal>
{
    public static decimal Minimum => 0m;
}

/// <summary>The day an order is due.</summary>
[ValueObject<DateOnly>]
public readonly partial struct DueDate;

/// <summary>A moment, with its offset.</summary>
[ValueObject<DateTimeOffset>]
public readonly partial struct Timestamp;

/// <summary>Whether a customer consents.</summary>
[ValueObject<bool>]
public readonly partial struct Consent;

/// <summary>A country an order ships to, of a closed set, kept in upper case.</summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct CountryCode : IValueObjectNormalizer<string>
{
    [KnownValue]
    public static readonly CountryCode France = Known("FR");

    [KnownValue]
    public static readonly CountryCode Germany = Known("DE");

    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}

/// <summary>The kind of document a reference points to.</summary>
public sealed class PurchaseOrder;

/// <summary>A reference to a document, whose owner is part of its type, kept in upper case.</summary>
/// <typeparam name="TOwner">The kind of document referenced.</typeparam>
[ValueObject<string>(MaxLength = 12)]
public readonly partial struct Reference<TOwner> : IValueObjectNormalizer<string>
    where TOwner : class
{
#pragma warning disable CA1000 // A hook is a static member, and the generic type is the point of the declaration.
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
#pragma warning restore CA1000
}

/// <summary>An order a client submits as a JSON body.</summary>
/// <param name="OrderId">The order.</param>
/// <param name="Customer">The customer.</param>
/// <param name="Quantity">How many items.</param>
public sealed record OrderSubmitted(OrderId OrderId, CustomerCode Customer, Quantity Quantity);

/// <summary>The query values an <c>[AsParameters]</c> endpoint reads, which Wolverine binds itself.</summary>
public sealed class LineParameters
{
    /// <summary>Gets or sets the customer.</summary>
    [Microsoft.AspNetCore.Mvc.FromQuery]
    public CustomerCode Customer { get; set; }

    /// <summary>Gets or sets how many items.</summary>
    [Microsoft.AspNetCore.Mvc.FromQuery]
    public Quantity Quantity { get; set; }

    /// <summary>Gets or sets the country, if any.</summary>
    [Microsoft.AspNetCore.Mvc.FromQuery]
    public CountryCode? Country { get; set; }
}

/// <summary>Names the resources the messages of refused value objects are read from, French alone.</summary>
public sealed class ProblemMessages;
