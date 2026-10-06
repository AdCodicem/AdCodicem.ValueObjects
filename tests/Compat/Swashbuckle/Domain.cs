using System.Text.RegularExpressions;

namespace AdCodicem.ValueObjects.CompatTests;

// The value objects a consumer writes, compiled by the generator the packed AdCodicem.ValueObjects carries, in the
// compiler of the next SDK; a project of their own, apart from the main one's, which map to Entity Framework Core.

/// <summary>An international bank account number, with lengths, a pattern, a format and an example.</summary>
[ValueObject<string>(MinLength = 15, MaxLength = 34, SchemaFormat = "iban")]
public readonly partial struct Iban : IValueObjectPatternValidator, IValueObjectExample<Iban>
{
    /// <inheritdoc />
    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    /// <inheritdoc />
    public static Iban Example => Create("FR7630006000011234567890189");
}

/// <summary>A quantity ordered, with typed bounds and an example.</summary>
[ValueObject<int>]
public readonly partial struct Quantity : IValueObjectMinimum<int>, IValueObjectMaximum<int>, IValueObjectExample<Quantity>
{
    /// <inheritdoc />
    public static int Minimum => 1;

    /// <inheritdoc />
    public static int Maximum => 100;

    /// <inheritdoc />
    public static Quantity Example => Create(3);
}

/// <summary>A closed set of reference data.</summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct CountryCode
{
    [KnownValue]
    public static readonly CountryCode France = Known("FR");

    [KnownValue]
    public static readonly CountryCode Belgium = Known("BE");
}

/// <summary>The kind of document a reference is to.</summary>
public sealed class PurchaseOrder;

/// <summary>The channel a notice about a record of one kind goes out on, a construction of a generic closed set.</summary>
/// <typeparam name="TRecord">The kind of record.</typeparam>
[ValueObject<string>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct NoticeChannel<TRecord>
{
    [KnownValue]
    public static readonly NoticeChannel<TRecord> Email = Known("email");

    [KnownValue]
    public static readonly NoticeChannel<TRecord> Sms = Known("sms");
}

/// <summary>A body holding value objects whose rules the document carries.</summary>
/// <param name="Iban">Lengths, a pattern, a format and an example.</param>
/// <param name="Alternate">A nullable value object, which a reference could not say may be null.</param>
/// <param name="Quantity">Typed bounds on a number.</param>
/// <param name="Country">A closed set.</param>
/// <param name="Notice">A construction of a generic closed set.</param>
/// <param name="PerCountry">A dictionary keyed by a value object.</param>
public sealed record OpenedAccount(
    Iban Iban,
    Iban? Alternate,
    Quantity Quantity,
    CountryCode Country,
    NoticeChannel<PurchaseOrder> Notice,
    Dictionary<CountryCode, Quantity> PerCountry);
