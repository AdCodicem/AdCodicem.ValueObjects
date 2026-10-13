using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.TestData;

// HotChocolate infers the fields of a query type from the instance members of its class.
#pragma warning disable CA1822

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>An account, whose fields are value objects, two of them optional.</summary>
/// <param name="Iban">The account's IBAN.</param>
/// <param name="Qty">A quantity.</param>
/// <param name="Amount">An amount, or none.</param>
/// <param name="Country">A country, or none.</param>
/// <param name="Id">The customer.</param>
public sealed record Account(Iban Iban, Quantity Qty, Amount? Amount, CountryCode? Country, CustomerId Id);

/// <summary>A transfer, read as an input object whose fields are value objects.</summary>
/// <param name="From">The account debited.</param>
/// <param name="Qty">A quantity.</param>
public sealed record Transfer(Iban From, Quantity Qty);

/// <summary>A struct that is no value object, which HotChocolate infers an object type from.</summary>
/// <param name="X">The abscissa.</param>
/// <param name="Y">The ordinate.</param>
public readonly record struct Point(int X, int Y);

/// <summary>Fields taking value objects of the domain as arguments, alone, optional, in lists and in an input object.</summary>
public class ShopQuery
{
    /// <summary>The customer every account belongs to.</summary>
    public static readonly CustomerId Customer = CustomerId.Create(Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"));

    public Account Account(Iban iban, Quantity qty, Amount? amount, CountryCode? country) => new(iban, qty, amount, country, Customer);

    public Iban? Lookup(Iban? iban) => iban;

    public IReadOnlyList<Iban> Many(IReadOnlyList<Iban> ibans) => ibans;

    public IReadOnlyList<Iban?> MaybeMany(IReadOnlyList<Iban?> ibans) => ibans;

    public Iban?[] MaybeArray(Iban?[] ibans) => ibans;

    public Iban[] ArrayOf(Iban[] ibans) => ibans;

    public Transfer Transfer(Transfer input) => input;

    public PhoneNumber Phone(PhoneNumber p) => p;

    public CountryCode Country(CountryCode c) => c;

    public Ordering.OrderReference Order(Ordering.OrderReference r) => r;
}

/// <summary>Fields returning instances a scalar writes, or refuses to.</summary>
public class OutputQuery
{
#pragma warning disable VO0010 // The uninitialized instances are what the scalars refuse, or write when the type accepts them.
    public Iban Unset() => default;

    public SequenceNumber Start() => default;
#pragma warning restore VO0010

    public Tolerance Infinite() => Tolerance.Create(double.NegativeInfinity);

    public Point Origin() => new(0, 0);

    public Latitude Large() => Latitude.Create(1e-30);

    public Ratio Third() => Ratio.Create(0.1f);

    public Floor Floor() => Domain.Floor.Create(3);

    public Celsius Celsius() => Domain.Celsius.Create(21);

    public PhoneNumber Phone() => PhoneNumber.Create("+33123456789");

    public GroupedReference Grouped() => GroupedReference.Create("ABCD EFGH");

    public GroupedReference GroupedEcho(GroupedReference g) => g;

    public Declared<GraphQLLink, Uri> Link(Declared<GraphQLLink, Uri> l) => l;

    public Declared<GraphQLTally, int> Tally(Declared<GraphQLTally, int> t) => t;
}

/// <summary>A field echoing a value object, alone, optional and in lists, for any value object.</summary>
/// <typeparam name="TSelf">The value object.</typeparam>
public class EchoQuery<TSelf>
    where TSelf : struct
{
    public TSelf Echo(TSelf value) => value;

    public TSelf? Optional(TSelf? value) => value;

    public IReadOnlyList<TSelf> Many(IReadOnlyList<TSelf> values) => values;

    public IReadOnlyList<TSelf?> MaybeMany(IReadOnlyList<TSelf?> values) => values;
}

/// <summary>A value object the handler maps beside one written by hand that a scalar added for its type maps.</summary>
public class RatingQuery
{
    public Iban Account(Iban iban) => iban;

    public HandWrittenRating Rate(HandWrittenRating rating) => rating;
}

/// <summary>Two value objects of one name.</summary>
public class CodeQuery
{
    public Billing.Code BillingCode(Billing.Code code) => code;

    public Shipping.Code ShippingCode(Shipping.Code code) => code;
}
