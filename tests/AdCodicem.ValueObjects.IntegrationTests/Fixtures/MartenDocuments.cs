using System.Linq.Expressions;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.UnitTests.Domain;
using Marten;
using Marten.Events.CodeGeneration;
using Marten.Linq;

namespace AdCodicem.ValueObjects.IntegrationTests.Fixtures;

// The documents, events and compiled queries MartenTests stores and runs on PostgreSQL.

/// <summary>A document holding a value object, its nullable type and a member of its underlying type, the oracle.</summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
public sealed class MartenTwinDocument<TSelf, TValue>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    public Guid Id { get; set; }

    public int Index { get; set; }

    public TSelf Item { get; set; }

    public TSelf? Optional { get; set; }

    public TValue Raw { get; set; } = default!;
}

/// <summary>A shipment holding a value object of each family.</summary>
public sealed class MartenShipment
{
    public Guid Id { get; set; }

    public int Index { get; set; }

    public Iban Iban { get; set; }

    public Iban? Backup { get; set; }

    public PageNumber Page { get; set; }

    public PageNumber? MaybePage { get; set; }

    public Amount Amount { get; set; }

    public CustomerId Customer { get; set; }

    public Consent Consent { get; set; }

    public EffectiveDate Effective { get; set; }

    public OccurredAt At { get; set; }

    public Grade Grade { get; set; }

    public SequenceNumber Sequence { get; set; }

    public CountryCode Country { get; set; }

    public Label Label { get; set; }

    public List<CountryCode> Countries { get; set; } = [];

    public List<MartenParcel> Parcels { get; set; } = [];

    public string Note { get; set; } = string.Empty;
}

/// <summary>A parcel of a <see cref="MartenShipment"/>, with a value object over each type C# compares as an <c>int</c>.</summary>
public sealed class MartenParcel
{
    public Label Tag { get; set; }

    public PageNumber Page { get; set; }

    public Score Score { get; set; }

    public Grade Grade { get; set; }

    public Adjustment Adjustment { get; set; }

    public LineQuantity Quantity { get; set; }
}

/// <summary>A customer whose identity is a value object over a <see cref="Guid"/>.</summary>
public sealed class MartenCustomer
{
    public CustomerId Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>An account whose identity is a value object over text.</summary>
public sealed class MartenAccount
{
    public Label Id { get; set; }

    public string Holder { get; set; } = string.Empty;
}

/// <summary>An account whose identity is the sample's IBAN, whose own <c>CountryCode</c> is text too.</summary>
public sealed class MartenIbanAccount
{
    public Iban Id { get; set; }
}

/// <summary>A payment whose identity is an entity identifier, which Marten takes for no identity.</summary>
public sealed class MartenPayment
{
    public PaymentId Id { get; set; }
}

/// <summary>A shipment was sent.</summary>
/// <param name="ShipmentId">The stream.</param>
/// <param name="Iban">The account billed.</param>
/// <param name="Page">The page of the manifest.</param>
/// <param name="Country">The destination.</param>
public sealed record MartenShipped(Guid ShipmentId, Iban Iban, PageNumber Page, CountryCode Country);

/// <summary>A shipment moved to another page of the manifest.</summary>
/// <param name="Page">The new page.</param>
public sealed record MartenRepaged(PageNumber Page);

/// <summary>The inline snapshot of a shipment's stream.</summary>
public sealed class MartenShipmentView
{
    public Guid Id { get; set; }

    public Iban Iban { get; set; }

    public PageNumber Page { get; set; }

    public static MartenShipmentView Create(MartenShipped shipped) => new() { Id = shipped.ShipmentId, Iban = shipped.Iban, Page = shipped.Page };

    public void Apply(MartenRepaged repaged) => Page = repaged.Page;
}

/// <summary>A compiled list query over <see cref="MartenShipment"/>.</summary>
public abstract class MartenShipmentQuery : ICompiledListQuery<MartenShipment>
{
    public abstract Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs();

    /// <summary>Gets whether a shipment answers this query, in memory.</summary>
    /// <param name="shipment">The shipment.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public abstract bool Answers(MartenShipment shipment);
}

public sealed class MartenShipmentsByIbanText : MartenShipmentQuery
{
    public string Iban { get; set; } = string.Empty;

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => x.Iban.Value == Iban);

    public override bool Answers(MartenShipment shipment) => shipment.Iban.Value == Iban;
}

public sealed class MartenShipmentsAbovePageNumber : MartenShipmentQuery
{
    public int Minimum { get; set; }

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => x.Page.Value > Minimum);

    public override bool Answers(MartenShipment shipment) => shipment.Page.Value > Minimum;
}

// A value object held by a member Marten is told to ignore, which neither Marten nor the package reads: beside the .Value
// form, a constant collection of the same value object and a collection.

public sealed class MartenShipmentsByIbanTextBesideIgnored : MartenShipmentQuery
{
    [MartenIgnore]
    public Iban Noted { get; set; }

    public string Iban { get; set; } = string.Empty;

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => x.Iban.Value == Iban);

    public override bool Answers(MartenShipment shipment) => shipment.Iban.Value == Iban;
}

public sealed class MartenShipmentsInBeneluxBesideIgnored : MartenShipmentQuery
{
#pragma warning disable CA1051 // A field of the query type is what the test is about.
    [MartenIgnore]
    public List<CountryCode> Ignored = [];
#pragma warning restore CA1051

    [MartenIgnore]
    public CountryCode Preferred { get; set; }

    public int MinimumIndex { get; set; }

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => x.Country.IsOneOf(CountryCode.Belgium, CountryCode.Luxembourg) && x.Index >= MinimumIndex);

    public override bool Answers(MartenShipment shipment)
        => (shipment.Country == CountryCode.Belgium || shipment.Country == CountryCode.Luxembourg) && shipment.Index >= MinimumIndex;
}

public sealed class MartenShipmentsOnPagesBesideIgnored : MartenShipmentQuery
{
    [MartenIgnore]
    public PageNumber Ignored { get; set; }

    public List<PageNumber> Pages { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => Pages.Contains(x.Page));

    public override bool Answers(MartenShipment shipment) => Pages.Contains(shipment.Page);
}

public sealed class MartenShipmentsOnPages : MartenShipmentQuery
{
    public PageNumber[] Pages { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => Pages.Contains(x.Page));

    public override bool Answers(MartenShipment shipment) => Pages.Contains(shipment.Page);
}

public sealed class MartenShipmentsInCountries : MartenShipmentQuery
{
    public List<CountryCode> Countries { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => x.Country.IsOneOf(Countries));

    public override bool Answers(MartenShipment shipment) => Countries.Contains(shipment.Country);
}

public sealed class MartenShipmentsOfAmounts : MartenShipmentQuery
{
    public List<Amount> Amounts { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => Amounts.Contains(x.Amount));

    public override bool Answers(MartenShipment shipment) => Amounts.Contains(shipment.Amount);
}

public sealed class MartenShipmentsOfCustomers : MartenShipmentQuery
{
    public List<CustomerId> Customers { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => Customers.Contains(x.Customer));

    public override bool Answers(MartenShipment shipment) => Customers.Contains(shipment.Customer);
}

public sealed class MartenShipmentsEffectiveOn : MartenShipmentQuery
{
    public List<EffectiveDate> Dates { get; set; } = [];

    public int MinimumIndex { get; set; }

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => x.Effective.IsOneOf(Dates) && x.Index >= MinimumIndex);

    public override bool Answers(MartenShipment shipment) => Dates.Contains(shipment.Effective) && shipment.Index >= MinimumIndex;
}

public sealed class MartenShipmentsAt : MartenShipmentQuery
{
    public List<OccurredAt> Moments { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => Moments.Contains(x.At));

    public override bool Answers(MartenShipment shipment) => Moments.Contains(shipment.At);
}

public sealed class MartenShipmentsOfConsents : MartenShipmentQuery
{
    public List<Consent> Consents { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => Consents.Contains(x.Consent));

    public override bool Answers(MartenShipment shipment) => Consents.Contains(shipment.Consent);
}

// A single value object, whatever the query does with it: refused by Marten, which binds none.

public sealed class MartenShipmentsByIban : MartenShipmentQuery
{
    public Iban Iban { get; set; }

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => x.Iban == Iban);

    public override bool Answers(MartenShipment shipment) => shipment.Iban == Iban;
}

public sealed class MartenShipmentsByParcelTag : MartenShipmentQuery
{
    public Label Tag { get; set; }

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => x.Parcels.Any(p => p.Tag == Tag));

    public override bool Answers(MartenShipment shipment) => shipment.Parcels.Any(p => p.Tag == Tag);
}

public sealed class MartenShipmentsWithParcelsAbove : MartenShipmentQuery
{
    public PageNumber Page { get; set; }

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => x.Parcels.Any(p => p.Page > Page));

    public override bool Answers(MartenShipment shipment) => shipment.Parcels.Any(p => p.Page > Page);
}

public sealed class MartenShipmentsHoldingCountry : MartenShipmentQuery
{
    public CountryCode Country { get; set; }

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => x.Countries.Contains(Country));

    public override bool Answers(MartenShipment shipment) => shipment.Countries.Contains(Country);
}

public sealed class MartenShipmentsNotedWithIban : MartenShipmentQuery
{
    public Iban Iban { get; set; }

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => x.Note == Iban.Value);

    public override bool Answers(MartenShipment shipment) => shipment.Note == Iban.Value;
}

public sealed class MartenShipmentsWithIbanPrefix : MartenShipmentQuery
{
    public Iban Prefix { get; set; }

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => x.Iban.Value.StartsWith(Prefix.Value));

    public override bool Answers(MartenShipment shipment) => shipment.Iban.Value.StartsWith(Prefix.Value, StringComparison.Ordinal);
}

public sealed class MartenShipmentsAtPageIndex : MartenShipmentQuery
{
    public PageNumber Page { get; set; }

    public override Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => x.Index == Page.Value);

    public override bool Answers(MartenShipment shipment) => shipment.Index == Page.Value;
}

/// <summary>A compiled query comparing a value-object identity with a member.</summary>
public sealed class MartenCustomersById : ICompiledListQuery<MartenCustomer>
{
    public CustomerId Id { get; set; }

    public Expression<Func<IMartenQueryable<MartenCustomer>, IEnumerable<MartenCustomer>>> QueryIs()
        => customers => customers.Where(x => x.Id == Id);
}

/// <summary>A source-generated context naming the converter factory, as the JSON guide declares one.</summary>
[JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
[JsonSerializable(typeof(MartenShipment))]
internal sealed partial class MartenJsonContext : JsonSerializerContext;
