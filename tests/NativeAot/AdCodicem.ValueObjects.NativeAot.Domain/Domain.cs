using System.Text.RegularExpressions;

// Every value object of the domain implements IXmlSerializable, which the application never calls: the guard that the
// emission adds no trimming or AOT warning to a native binary, and changes nothing it does.
[assembly: ValueObjectXmlSerialization]

namespace AdCodicem.ValueObjects.NativeAot.Domain;

// What the unit suite's UnderlyingTypes.cs, linked beside this file, leaves to the rest of its domain: the three
// underlying types it does not use (short, decimal and Guid), a pattern hook, an entity identifier, a closed set of
// numbers and a generic value object, which the application registers by hand, as native AOT asks.

/// <summary>A quantity of items ordered.</summary>
[ValueObject<short>(Arithmetic = true)]
public readonly partial struct Quantity : IValueObjectMinimum<short>, IValueObjectMaximum<short>
{
    public static short Minimum => 1;

    public static short Maximum => 1000;
}

/// <summary>A monetary amount, never negative, rounded to the cent.</summary>
[ValueObject<decimal>(Arithmetic = true)]
public readonly partial struct Amount : IValueObjectNormalizer<decimal>, IValueObjectMinimum<decimal>, IValueObjectExample<Amount>
{
    public static Amount Example => Create(1250.00m);

    public static decimal Minimum => 0m;

    public static decimal NormalizeValue(decimal value) => decimal.Round(value, 2, MidpointRounding.ToEven) + 0.00m;
}

/// <summary>A value-added tax rate, in percent, from a closed set.</summary>
[ValueObject<decimal>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct VatRate
{
    [KnownValue]
    public static readonly VatRate Standard = Known(20.0m);

    [KnownValue(Description = "Food, books and medicine.")]
    public static readonly VatRate Reduced = Known(5.5m);
}

/// <summary>The identifier of a customer, never empty.</summary>
[ValueObject<Guid>]
public readonly partial struct CustomerId : IValueObjectValidator<Guid>
{
    public static ValidationResult ValidateValue(in Guid value)
        => value == Guid.Empty
            ? ValidationResult.Required("A customer identifier must not be empty.")
            : ValidationResult.Success;
}

/// <summary>An email address, in lower case, whose shape a source-generated regular expression checks.</summary>
[ValueObject<string>(MaxLength = 254, SchemaFormat = "email")]
public readonly partial struct EmailAddress : IValueObjectNormalizer<string>, IValueObjectPatternValidator, IValueObjectExample<EmailAddress>
{
    public static EmailAddress Example => Create("ada@example.com");

    /// <summary>Gets the shape of an address: something, an at sign, and a domain with a dot in it.</summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    public static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();
}

/// <summary>The public identifier of an order.</summary>
[EntityId("ord", Description = "Identifies an order.")]
public readonly partial struct OrderId;

/// <summary>The number of a document, whose kind is part of its type: a purchase order's and an invoice's do not mix.</summary>
/// <typeparam name="TOwner">The kind of document numbered.</typeparam>
/// <remarks>
/// Not named Reference, as the unit suite's is: the code `dotnet ef dbcontext optimize` writes for native AOT imports
/// Microsoft.EntityFrameworkCore.Metadata.Internal, whose Reference&lt;T&gt; it then cannot tell from one of the domain.
/// </remarks>
[ValueObject<string>(MaxLength = 12)]
public readonly partial struct DocumentNumber<TOwner> : IValueObjectNormalizer<string>, IValueObjectExample<DocumentNumber<TOwner>>
    where TOwner : class
{
#pragma warning disable CA1000 // A hook is a static member, and the generic type is the point of the declaration.
    public static DocumentNumber<TOwner> Example => Create("PO-1042");

    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
#pragma warning restore CA1000
}

/// <summary>A purchase order, as the kind of document a number belongs to.</summary>
public sealed class PurchaseOrder;
