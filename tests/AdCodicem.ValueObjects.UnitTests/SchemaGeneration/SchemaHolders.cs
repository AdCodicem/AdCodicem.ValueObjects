using System.ComponentModel.DataAnnotations;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.TestData;
using NJsonSchema;
using NJsonSchema.Annotations;

namespace AdCodicem.ValueObjects.UnitTests.SchemaGeneration;

/// <summary>
/// A message holding one value object of each kind of rule, optional ones, collections and dictionaries of them, and a
/// construction of a generic one, as a Kafka topic carries or an API takes in a body.
/// </summary>
public sealed class SchemaOrder
{
    public Iban Iban { get; set; }

    public Iban? OptionalIban { get; set; }

    public Quantity Quantity { get; set; }

    public Quantity? OptionalQuantity { get; set; }

    public Amount Amount { get; set; }

    public CustomerId Customer { get; set; }

    public CountryCode Country { get; set; }

    public CountryCode? OptionalCountry { get; set; }

    public BirthDate Born { get; set; }

    public Consent Consent { get; set; }

    public Grade Grade { get; set; }

    public ByteCount Bytes { get; set; }

    public LedgerBalance Balance { get; set; }

    public Latitude Latitude { get; set; }

    public OpeningTime Opens { get; set; }

    public RecordedAt Recorded { get; set; }

    public OccurredAt Occurred { get; set; }

    public Duration Took { get; set; }

    public DocumentStatus Status { get; set; }

    public Reference<PurchaseOrder> Order { get; set; }

    public Reference<PurchaseOrder>? OptionalOrder { get; set; }

    public List<Iban> Ibans { get; set; } = [];

    public Quantity?[] OptionalQuantities { get; set; } = [];

    public Dictionary<CountryCode, Amount> AmountByCountry { get; set; } = [];

    public Dictionary<string, Quantity> QuantityByName { get; set; } = [];

    /// <summary>Gets an order every member of which its type accepts.</summary>
    /// <returns>The order.</returns>
    public static SchemaOrder Sample() => new()
    {
        Iban = Iban.Create("DE89370400440532013000"),
        Quantity = Quantity.Create(7),
        Amount = Amount.Create(12.5m),
        Customer = CustomerId.Create(Guid.Parse("3f2504e0-4f89-41d3-9a0c-0305e82c3301")),
        Country = CountryCode.France,
        Born = BirthDate.Create(new DateOnly(1990, 5, 17)),
        Consent = Consent.Create(true),
        Grade = Grade.Create('b'),
        Bytes = ByteCount.Create(10),
        Balance = LedgerBalance.Create(12345),
        Latitude = Latitude.Create(48.85),
        Opens = OpeningTime.Create(new TimeOnly(8, 30)),
        Recorded = RecordedAt.Create(new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc)),
        Occurred = OccurredAt.Create(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.FromHours(2))),
        Took = Duration.Create(TimeSpan.FromMinutes(90)),
        Status = DocumentStatus.Draft,
        Order = Reference<PurchaseOrder>.Create("po-1042"),
        OptionalOrder = Reference<PurchaseOrder>.Create("po-7"),
        Ibans = [Iban.Create("DE89370400440532013000")],
        OptionalQuantities = [Quantity.Create(1), null],
        AmountByCountry = new() { [CountryCode.France] = Amount.Create(1m) },
        QuantityByName = new() { ["a"] = Quantity.Create(2) },
    };
}

/// <summary>A value object alone, optional and in a list, which every sample is described through.</summary>
/// <typeparam name="TSelf">The value object.</typeparam>
public sealed class SchemaHolder<TSelf>
    where TSelf : struct
{
    public TSelf Value { get; set; }

    public TSelf? Optional { get; set; }

    public List<TSelf> Values { get; set; } = [];
}

/// <summary>
/// Text held to the rules of value objects, in either form of the attribute, optional, required, in a collection, and
/// beside the member's own validation attributes, stricter and looser; and two misuses described as they are.
/// </summary>
public sealed class SchemaTextForm
{
    [ParsesAs(typeof(Iban))]
    public string? Account { get; set; }

    [ParsesAs(typeof(Iban)), StringLength(20)]
    public string? ShortAccount { get; set; }

    [ParsesAs(typeof(Iban)), StringLength(40)]
    public string? LooseAccount { get; set; }

    [ParsesAs(typeof(Iban)), StringLength(30, MinimumLength = 20)]
    public string? NarrowAccount { get; set; }

    [ParsesAs(typeof(Reference<PurchaseOrder>)), RegularExpression("^PO-[0-9]+$")]
    public string? Order { get; set; }

    [ParsesAs(typeof(Declared<UndescribedCount, int>))]
    public string? Count { get; set; }

    [ParsesAs<Quantity, short>]
    public string? Quantity { get; set; }

    [ParsesAs(typeof(CountryCode))]
    public string? Country { get; set; }

    [ParsesAs(typeof(CountryCode)), Required]
    public string RequiredCountry { get; set; } = "";

    [ParsesAs(typeof(Iban))]
    public string[] Accounts { get; set; } = [];

    [ParsesAs(typeof(BirthDate))]
    public string? Born { get; set; }

    [ParsesAs(typeof(Consent))]
    public string? Consent { get; set; }

#pragma warning disable CA1051 // Fields, which Newtonsoft.Json writes and System.Text.Json leaves out.
    [ParsesAs(typeof(Quantity))]
    public string? Written;

    [ParsesAs(typeof(Iban)), StringLength(20)]
    public string? WrittenAccount;
#pragma warning restore CA1051

#pragma warning disable VO0039, VO0040 // The misuses DataAnnotations refuses as it validates, described as they are.
    [ParsesAs(typeof(Iban))]
    public int Number { get; set; }

    [ParsesAs(typeof(Guid))]
    public string? NoValueObject { get; set; }
#pragma warning restore VO0039, VO0040
}

/// <summary>
/// A record whose constructor parameters carry the attribute, where MVC requires it, one of whose properties carries a
/// validation attribute of its own.
/// </summary>
/// <param name="Account">The text of an account.</param>
/// <param name="Country">The text of a country, if any.</param>
/// <param name="ShortAccount">The text of an account of at most 20 characters, if any.</param>
public sealed record SchemaTextRecord(
    [ParsesAs(typeof(Iban))] string Account,
    [ParsesAs(typeof(CountryCode))] string? Country,
    [ParsesAs(typeof(Iban))][property: StringLength(20)] string? ShortAccount);

/// <summary>A count from one to five, written by hand with no description, as text holds it.</summary>
public sealed class UndescribedCount : IDeclaration<int>
{
    public static ValueObjectSchema Schema { get; } = new() { Minimum = "1", Maximum = "5" };
}

/// <summary>Value-object members carrying validation attributes, and one NJsonSchema's own attribute describes.</summary>
public sealed class SchemaAnnotated
{
    [Required]
    public Iban Iban { get; set; }

    [Range(1, 5)]
    public Quantity Ranged { get; set; }

    [JsonSchemaType(typeof(string))]
    public Quantity Forced { get; set; }

    [JsonSchema(JsonObjectType.Integer)]
    public Quantity Stated { get; set; }
}

/// <summary>A form whose members a derived form hides or inherits.</summary>
public class SchemaTextBase
{
    [ParsesAs(typeof(Quantity))]
    public string? Page { get; set; }

    [ParsesAs(typeof(CountryCode))]
    public string? Country { get; set; }
}

/// <summary>A form hiding a member of its base with a collection of text, held to no value object.</summary>
public sealed class SchemaHidingText : SchemaTextBase
{
    public new string[]? Page { get; set; }
}

/// <summary>
/// A form whose own text members, beside its base's, carry validation attributes of their own, stricter and looser than
/// the value object's: NJsonSchema's inheritance describes them in a part of <c>allOf</c> apart from the base.
/// </summary>
public sealed class SchemaDerivedTextForm : SchemaTextBase
{
    [ParsesAs(typeof(Iban)), StringLength(20)]
    public string? Account { get; set; }

    [ParsesAs(typeof(Reference<PurchaseOrder>)), MaxLength(40)]
    public string? Order { get; set; }
}
