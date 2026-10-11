namespace AdCodicem.ValueObjects.IntegrationTests.Fixtures;

// The shapes the MongoDB tests store that neither the sample nor the unit suite's UnderlyingTypes.cs declares: a value
// object over a short, bounded as the issue's evidence bounds a quantity, a generic value object, a text value object
// with a minimum length and no pattern, which a validator halves, and closed sets that store another spelling of a
// known value, which a validator's enum would refuse.

/// <summary>A number of items on an order line.</summary>
[ValueObject<short>(Arithmetic = true)]
public readonly partial struct LineQuantity : IValueObjectMinimum<short>, IValueObjectMaximum<short>
{
    public static short Minimum => 1;

    public static short Maximum => 999;
}

/// <summary>A code, whose owner is part of its type.</summary>
/// <typeparam name="TOwner">The kind of thing the code names.</typeparam>
[ValueObject<string>(MaxLength = 12)]
public readonly partial struct OwnedCode<TOwner>
    where TOwner : class;

/// <summary>A nickname, of four to eight characters.</summary>
[ValueObject<string>(MinLength = 4, MaxLength = 8)]
public readonly partial struct Nickname;

/// <summary>A unit of mass, from a closed set looked up ignoring case and stored in lower case.</summary>
/// <remarks>
/// The comparison takes the micro sign <c>µ</c> for the Greek <c>μ</c> of <c>μg</c>, since both have the upper case
/// <c>Μ</c>, and lower case leaves it as it is: the type stores <c>µg</c>, which neither case of the whole value reaches.
/// </remarks>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.OrdinalIgnoreCase)]
public readonly partial struct MassUnit : IValueObjectNormalizer<string>
{
    [KnownValue]
    public static readonly MassUnit Microgram = Known("\u03BCg");

    [KnownValue]
    public static readonly MassUnit Milligram = Known("mg");

    public static string NormalizeValue(string value) => value.ToLowerInvariant();
}

/// <summary>A currency, from a closed set looked up as the invariant culture compares ignoring case, three characters long.</summary>
/// <remarks>
/// The culture takes a modifier letter for its letter, <c>KʳW</c> for <c>KRW</c>, of the same length, which the type
/// stores as written.
/// </remarks>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.InvariantCultureIgnoreCase, MaxLength = 3)]
public readonly partial struct SettlementCurrency : IValueObjectNormalizer<string>
{
    [KnownValue]
    public static readonly SettlementCurrency Won = Known("KRW");

    [KnownValue]
    public static readonly SettlementCurrency Euro = Known("EUR");

    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}

/// <summary>The symbol of a temperature scale, looked up as the invariant culture compares, one character long.</summary>
/// <remarks>The culture takes the Kelvin sign for the letter <c>K</c>, which the type stores as written.</remarks>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.InvariantCulture, MaxLength = 1)]
public readonly partial struct ScaleSymbol
{
    [KnownValue]
    public static readonly ScaleSymbol Kelvin = Known("K");
}
