using System.Linq.Expressions;
using Marten;
using Marten.Events.CodeGeneration;
using Marten.Linq;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

// Compiled queries over a MartenOrder: Marten plans one the first time it meets its type, from the values of that call,
// and binds each later call's values into the plan through the members of the type. Marten refuses a query type holding a
// single value object itself, the package keeping every value object out of its list of value types.

/// <summary>A compiled list query over <see cref="MartenOrder"/>.</summary>
public abstract class MartenOrderQuery : ICompiledListQuery<MartenOrder>
{
    public abstract Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs();
}

/// <summary>A single value object compared with a member: refused by Marten.</summary>
public sealed class MartenOrdersByIban : MartenOrderQuery
{
    public Iban Iban { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Iban == Iban);
}

/// <summary>A single value object over an integer, compared with a member: refused by Marten.</summary>
public sealed class MartenOrdersAbovePage : MartenOrderQuery
{
    public PageNumber Minimum { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Page > Minimum);
}

/// <summary>A single value object held by a field, beside an integer: refused by Marten, or by the package once Marten lists it as an identity.</summary>
public sealed class MartenOrdersByCustomerFrom : MartenOrderQuery
{
#pragma warning disable CA1051 // A field of the query type is what the test is about.
    public CustomerId Customer;
#pragma warning restore CA1051

    public int MinimumIndex { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Customer == Customer && x.Index >= MinimumIndex);
}

/// <summary>A single value object of a generic construction the store is taught, compared with a member: refused by Marten.</summary>
public sealed class MartenOrdersByReference : MartenOrderQuery
{
    public Reference<MartenCompiledOwner> Reference { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.CompiledReference == Reference);
}

/// <summary>
/// A single value object of a generic construction nothing taught to Marten, compared with a member: Marten's own refusal,
/// before any package code runs, as for one taught, which no other test resolves.
/// </summary>
public sealed class MartenOrdersByUnknownReference : MartenOrderQuery
{
    public Reference<MartenUnknownOwner> Reference { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.UnknownReference == Reference);
}

/// <summary>A single value object, planned from values the query type sets: refused by Marten.</summary>
public sealed class MartenOrdersByIbanPlanned : MartenOrderQuery, IQueryPlanning
{
    public Iban Iban { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Iban == Iban);

    public void SetUniqueValuesForQueryPlanning() => Iban = Domain.Iban.Create("NL91ABNA0417164300");
}

/// <summary>A nullable value object compared with a member: Marten's own refusal, as for an <c>int?</c>.</summary>
public sealed class MartenOrdersByMaybeIban : MartenOrderQuery
{
    public Iban? Iban { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Iban == Iban);
}

/// <summary>The underlying value compared with a member of the underlying type: bound on each call.</summary>
public sealed class MartenOrdersByIbanText : MartenOrderQuery
{
    public string Iban { get; set; } = string.Empty;

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Iban.Value == Iban);
}

/// <summary>The underlying number compared with a member of the underlying type: bound on each call.</summary>
public sealed class MartenOrdersAbovePageNumber : MartenOrderQuery
{
    public int Minimum { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Page.Value > Minimum);
}

/// <summary>The underlying number of a value object over a <c>short</c>, compared as C# compares it, with an integer member.</summary>
public sealed class MartenOrdersAboveQuantityNumber : MartenOrderQuery
{
    public int Minimum { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Quantity.Value > Minimum);
}

/// <summary>
/// The underlying text compared with a member of the underlying type, beside a value object held by a member Marten is
/// told to ignore: bound on each call, the ignored member read by neither Marten nor the package.
/// </summary>
public sealed class MartenOrdersByIbanTextBesideIgnored : MartenOrderQuery
{
    [MartenIgnore]
    public Iban Noted { get; set; }

    public string Iban { get; set; } = string.Empty;

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Iban.Value == Iban);
}

/// <summary>
/// A constant collection of value objects, beside an integer member and members Marten is told to ignore holding the same
/// value object, a field of values and a property of one: a constant of the plan, as without them.
/// </summary>
public sealed class MartenOrdersInBeneluxBesideIgnored : MartenOrderQuery
{
#pragma warning disable CA1051 // A field of the query type is what the test is about.
    [MartenIgnore]
    public List<CountryCode> Ignored = [];
#pragma warning restore CA1051

    [MartenIgnore]
    public CountryCode Preferred { get; set; }

    public int MinimumIndex { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Country.IsOneOf(CountryCode.Belgium, CountryCode.Luxembourg) && x.Index >= MinimumIndex);
}

/// <summary>An array of value objects beside a member Marten is told to ignore holding one: bound on each call.</summary>
public sealed class MartenOrdersOnPagesBesideIgnored : MartenOrderQuery
{
    [MartenIgnore]
    public PageNumber Ignored { get; set; }

    public PageNumber[] Pages { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => Pages.Contains(x.Page));
}

/// <summary>A constant value object, beside an integer member: a constant of the plan.</summary>
public sealed class MartenOrdersInFranceFrom : MartenOrderQuery
{
    public int MinimumIndex { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Country == CountryCode.France && x.Index >= MinimumIndex);
}

/// <summary>An array of value objects over an integer: bound on each call.</summary>
public sealed class MartenOrdersOnPages : MartenOrderQuery
{
    public PageNumber[] Pages { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => Pages.Contains(x.Page));
}

/// <summary>A list of value objects over text, through <c>IsOneOf</c>: bound on each call.</summary>
public sealed class MartenOrdersInCountries : MartenOrderQuery
{
    public List<CountryCode> Countries { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Country.IsOneOf(Countries));
}

/// <summary>An array of value objects over a decimal held by a field: bound on each call.</summary>
public sealed class MartenOrdersOfAmounts : MartenOrderQuery
{
#pragma warning disable CA1051 // A field of the query type is what the test is about.
#pragma warning disable CA1819 // An array, as an application's query type holds it.
    public Amount[] Amounts = [];
#pragma warning restore CA1819
#pragma warning restore CA1051

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => Amounts.Contains(x.Amount));
}

/// <summary>A list of value objects over a date, beside an integer member: both bound on each call.</summary>
public sealed class MartenOrdersBornOnFrom : MartenOrderQuery
{
    public List<BirthDate> Dates { get; set; } = [];

    public int MinimumIndex { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Born.IsOneOf(Dates) && x.Index >= MinimumIndex);
}

/// <summary>A list of value objects over a <see cref="Guid"/>: bound on each call.</summary>
public sealed class MartenOrdersOfCustomers : MartenOrderQuery
{
    public List<CustomerId> Customers { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => Customers.Contains(x.Customer));
}

/// <summary>An array of value objects over a <see cref="bool"/>: bound on each call.</summary>
public sealed class MartenOrdersOfConsents : MartenOrderQuery
{
    public Consent[] Consents { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => Consents.Contains(x.Consent));
}

/// <summary>A list of value objects over a <see cref="double"/>: bound on each call.</summary>
public sealed class MartenOrdersAtLatitudes : MartenOrderQuery
{
    public List<Latitude> Latitudes { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Latitude.IsOneOf(Latitudes));
}

/// <summary>A list of value objects over a <see cref="DateTimeOffset"/>: bound on each call.</summary>
public sealed class MartenOrdersAtMoments : MartenOrderQuery
{
    public List<OccurredAt> Moments { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => Moments.Contains(x.At));
}

/// <summary>A list of value objects of a generic construction: bound on each call.</summary>
public sealed class MartenOrdersOfReferences : MartenOrderQuery
{
    public List<Reference<MartenOwner>> References { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => References.Contains(x.Reference));
}

/// <summary>Two collections of value objects of two types: each bound on each call.</summary>
public sealed class MartenOrdersOnPagesInCountries : MartenOrderQuery
{
    public PageNumber[] Pages { get; set; } = [];

    public List<CountryCode> Countries { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => Pages.Contains(x.Page) && Countries.Contains(x.Country));
}

/// <summary>One collection read twice: two parameters, each bound on each call.</summary>
public sealed class MartenOrdersOnPagesTwice : MartenOrderQuery
{
    public PageNumber[] Pages { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => Pages.Contains(x.Page) || x.Page.IsOneOf(Pages));
}

/// <summary>A constant collection of value objects, beside an integer member: a constant of the plan.</summary>
public sealed class MartenOrdersInBeneluxFrom : MartenOrderQuery
{
    public int MinimumIndex { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Country.IsOneOf(CountryCode.Belgium, CountryCode.Luxembourg) && x.Index >= MinimumIndex);
}

/// <summary>A collection read through a local of <c>QueryIs</c>: refused, Marten would freeze it.</summary>
public sealed class MartenOrdersOnPagesThroughALocal : MartenOrderQuery
{
    public PageNumber[] Pages { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
    {
        var pages = Pages;
        return orders => orders.Where(x => pages.Contains(x.Page));
    }
}

/// <summary><c>IsOneOf</c> over two single members: refused by Marten.</summary>
public sealed class MartenOrdersOnEitherPage : MartenOrderQuery
{
    public PageNumber First { get; set; }

    public PageNumber Second { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Page.IsOneOf(First, Second));
}

/// <summary>A collection of nullable value objects: refused, its null test would be planned from the first call.</summary>
public sealed class MartenOrdersInMaybeCountries : MartenOrderQuery
{
    public List<CountryCode?> Countries { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => Countries.Contains((CountryCode?)x.Country));
}

/// <summary>A collection read through a local of <c>QueryIs</c> from a list of nullable value objects: refused.</summary>
public sealed class MartenOrdersInMaybeCountriesThroughALocal : MartenOrderQuery
{
    public List<CountryCode?> Countries { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
    {
        var countries = Countries;
        return orders => orders.Where(x => countries.Contains((CountryCode?)x.Country));
    }
}

/// <summary>A collection read through a member Marten is told to ignore, a filter object: bound on each call.</summary>
public sealed class MartenOrdersOnFilteredPages : MartenOrderQuery
{
    [MartenIgnore]
    public MartenPageFilter? Filter { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => Filter!.Pages.Contains(x.Page));
}

/// <summary>The pages a <see cref="MartenOrdersOnFilteredPages"/> asks for.</summary>
public sealed class MartenPageFilter
{
    public List<PageNumber> Pages { get; set; } = [];
}

/// <summary>A single value object compared with a member of a child collection: refused by Marten.</summary>
public sealed class MartenOrdersByLineTag : MartenOrderQuery
{
    public Label Tag { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Lines.Any(l => l.Tag == Tag));
}

/// <summary>A single value object ordered against a member of a child collection: refused by Marten.</summary>
public sealed class MartenOrdersWithLinesAbove : MartenOrderQuery
{
    public Quantity Minimum { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Lines.Any(l => l.Quantity > Minimum));
}

/// <summary>A single value object sought in a collection the document holds: refused by Marten.</summary>
public sealed class MartenOrdersHoldingCountry : MartenOrderQuery
{
    public CountryCode Country { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Countries.Contains(Country));
}

/// <summary>The <c>Value</c> of a single value object compared with a member of another type: refused by Marten.</summary>
public sealed class MartenOrdersNotedWithIban : MartenOrderQuery
{
    public Iban Iban { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Note == Iban.Value);
}

/// <summary>The <c>Value</c> of a single value object handed to a string method: refused by Marten.</summary>
public sealed class MartenOrdersWithIbanPrefix : MartenOrderQuery
{
    public Iban Prefix { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Iban.Value.StartsWith(Prefix.Value));
}

/// <summary>The <c>Value</c> of a single value object over an integer compared with an integer member: refused by Marten.</summary>
public sealed class MartenOrdersAtPageIndex : MartenOrderQuery
{
    public PageNumber Page { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Index == Page.Value);
}

/// <summary><c>IsOneOf</c> over two single members of a value object Marten may list as an identity.</summary>
public sealed class MartenOrdersOfEitherCustomer : MartenOrderQuery
{
    public CustomerId First { get; set; }

    public CustomerId Second { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Customer.IsOneOf(First, Second));
}

/// <summary>
/// A constant collection beside a collection member of the same value object: refused, the package cannot tell the
/// constant from values read through a local.
/// </summary>
public sealed class MartenOrdersInBeneluxAndCountries : MartenOrderQuery
{
    public List<CountryCode> Countries { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Country.IsOneOf(CountryCode.Belgium, CountryCode.Luxembourg) && x.Country.IsOneOf(Countries));
}

/// <summary>A single constant beside a collection member of the same value object: a constant of the plan, the collection bound.</summary>
public sealed class MartenOrdersInFranceAmongCountries : MartenOrderQuery
{
    public List<CountryCode> Countries { get; set; } = [];

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Origin == CountryCode.France && Countries.Contains(x.Country));
}

/// <summary>A compiled query with no value object at all.</summary>
public sealed class MartenOrdersFrom : MartenOrderQuery
{
    public int MinimumIndex { get; set; }

    public override Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Index >= MinimumIndex);
}

/// <summary>The owner of the references a compiled query compares one by one, which no other test closes over.</summary>
public sealed class MartenCompiledOwner;

/// <summary>The owner of the references a compiled query compares before Marten knows them, which nothing resolves.</summary>
public sealed class MartenUnknownOwner;
