using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.Functions;

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

/// <summary>An amount, from 0.5 to 1000.25.</summary>
[ValueObject<decimal>]
public readonly partial struct Amount : IValueObjectMinimum<decimal>, IValueObjectMaximum<decimal>
{
    public static decimal Minimum => 0.5m;

    public static decimal Maximum => 1000.25m;
}

/// <summary>A rate, of any size, which the host may write in exponent form.</summary>
[ValueObject<decimal>]
public readonly partial struct Rate;

/// <summary>The identifier of an order.</summary>
[ValueObject<Guid>]
public readonly partial struct OrderId;

/// <summary>The day an order is due.</summary>
[ValueObject<DateOnly>]
public readonly partial struct DueDate;

/// <summary>A moment, with its offset.</summary>
[ValueObject<DateTimeOffset>]
public readonly partial struct Timestamp;

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

/// <summary>An order a client submits, as a request body or a queue message.</summary>
/// <param name="OrderId">The order.</param>
/// <param name="Customer">The customer.</param>
/// <param name="Quantity">How many items.</param>
public sealed record OrderSubmitted(OrderId OrderId, CustomerCode Customer, Quantity Quantity);

/// <summary>A price a client submits as a queue message, its numbers plain decimals.</summary>
/// <param name="Rate">The rate.</param>
/// <param name="Amount">The amount.</param>
public sealed record Priced(decimal Rate, decimal Amount);

/// <summary>Names the resources the messages of refused value objects are read from, French alone.</summary>
public sealed class ProblemMessages;
