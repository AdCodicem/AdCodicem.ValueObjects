using System.Globalization;
using AdCodicem.ValueObjects.CsvHelper;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.TestData;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace AdCodicem.ValueObjects.UnitTests.Csv;

/// <summary>The owner of a <see cref="Reference{TOwner}"/> the tests of Csv/ alone construct, which nothing registers.</summary>
public sealed class CsvOwner;

/// <summary>
/// The owner of a <see cref="Reference{TOwner}"/> named only where dynamic code is off, so that nothing registers it before
/// a test registers it by hand.
/// </summary>
public sealed class UnregisteredCsvOwner;

/// <summary>
/// A ticket written by hand over <see cref="string"/>, read from <c>T-</c> followed by anything, which nothing registers:
/// the registry describes it the first time a CsvHelper context meets it.
/// </summary>
public sealed class CsvTicket : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { MaxLength = 8 };

    public static ValidationResult Validate(string value)
        => value is null ? ValidationResult.Required()
            : value.Length <= 8 ? ValidationResult.Success
            : ValidationResult.Failure(ValueObjectErrorCodes.TooLong, "A ticket is at most 8 characters long.");

    public static bool TryRead(string text, out string value)
    {
        value = text.Trim();
        return value.StartsWith("T-", StringComparison.Ordinal);
    }
}

/// <summary>
/// A code written by hand whose rule refuses <c>BAD</c>, and its default, with a code and an empty message, as a rule
/// written by hand may.
/// </summary>
public sealed class CsvSilent : IDeclaration<string>
{
    /// <summary>The code it refuses with.</summary>
    public const string Refused = "csv.silent";

    public static ValueObjectSchema Schema { get; } = new();

    public static ValidationResult Validate(string value)
        => value is null or "BAD" ? ValidationResult.Failure(Refused, string.Empty) : ValidationResult.Success;

    public static bool TryRead(string text, out string value)
    {
        value = text;
        return true;
    }
}

/// <summary>One column, named <c>Value</c>, of a value object or of its nullable type.</summary>
/// <typeparam name="T">The value object, or its nullable type.</typeparam>
public sealed class CsvRow<T>
{
    public T Value { get; set; } = default!;
}

/// <summary>
/// A column of a value object or of its nullable type after the row's position, so that a row whose value is empty text
/// is no blank line, which CsvHelper skips.
/// </summary>
/// <typeparam name="T">The value object, or its nullable type.</typeparam>
public sealed class CsvLine<T>
{
    public int Line { get; set; }

    public T Value { get; set; } = default!;
}

/// <summary>A collection of value objects after the row's position, which CsvHelper hands to its own collection converters.</summary>
/// <typeparam name="T">The value object.</typeparam>
public sealed class CsvList<T>
{
    public int Line { get; set; }

    public List<T> Values { get; set; } = [];
}

/// <summary>A class map naming a collection of value objects, which CsvHelper's auto-map leaves out of a file with a header.</summary>
/// <typeparam name="T">The value object.</typeparam>
public sealed class CsvListMap<T> : ClassMap<CsvList<T>>
{
    public CsvListMap()
    {
        Map(list => list.Line);
        Map(list => list.Values);
    }
}

/// <summary>An order, its value objects in the columns of their members' names.</summary>
public sealed class CsvOrder
{
    public CustomerId Id { get; set; }

    public Iban Iban { get; set; }

    public Quantity Quantity { get; set; }

    public Amount Amount { get; set; }

    public BirthDate Due { get; set; }

    public CountryCode Country { get; set; }

    public Quantity? Optional { get; set; }

    /// <summary>Gets a valid order, with nothing in <see cref="Optional"/>.</summary>
    public static CsvOrder Valid => new()
    {
        Id = CustomerId.Create(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e")),
        Iban = Iban.Create("FR7630006000011234567890189"),
        Quantity = Quantity.Create(3),
        Amount = Amount.Create(12.5m),
        Due = BirthDate.Create(new DateOnly(2026, 10, 3)),
        Country = CountryCode.France,
    };

    /// <summary>The header CsvHelper writes for an order.</summary>
    public const string Header = "Id,Iban,Quantity,Amount,Due,Country,Optional";

    /// <summary>The row CsvHelper writes for <see cref="Valid"/>, in the invariant culture.</summary>
    public const string ValidRow = "0f8fad5b-d9cb-469f-a165-70867728950e,FR7630006000011234567890189,3,12.50,2026-10-03,FR,";
}

/// <summary>An order as a positional record, read through its constructor.</summary>
/// <param name="Iban">The account.</param>
/// <param name="Quantity">How many.</param>
/// <param name="Optional">How many more, if any.</param>
public sealed record CsvOrderRecord(Iban Iban, Quantity Quantity, Quantity? Optional);

/// <summary>Two amounts, one of which is read and written in French whatever the configuration's culture.</summary>
public sealed class CsvCultured
{
    [CultureInfo("fr-FR")]
    public Amount French { get; set; }

    public Amount Other { get; set; }
}

/// <summary>A nullable amount, whose options a test sets for <see cref="Amount"/> or for <see cref="Nullable{T}"/>.</summary>
public sealed class CsvOptionalAmount
{
    public Amount? Amount { get; set; }
}

/// <summary>Country codes with defaults written as text, which CsvHelper reads through the converter for an empty field.</summary>
public sealed class CsvDefaulted
{
    [Default("BE")]
    public CountryCode Country { get; set; }

    [Default("XX")]
    public CountryCode Wrong { get; set; }
}

/// <summary>An account whose converter the member names, which a context with nothing registered honours.</summary>
public sealed class CsvAttributed
{
    [TypeConverter(typeof(ValueObjectTypeConverter<Iban, string>))]
    public Iban Iban { get; set; }
}

/// <summary>
/// A nullable quantity whose converter the member names, around which CsvHelper puts no <c>NullableConverter</c>, after the
/// row's position.
/// </summary>
public sealed class CsvAttributedOptional
{
    public int Line { get; set; }

    [TypeConverter(typeof(ValueObjectTypeConverter<Quantity, short>))]
    public Quantity? Optional { get; set; }
}

/// <summary>
/// A positional record whose nullable parameter names its converter, which reaches the converter with no member, and so
/// with no type.
/// </summary>
/// <param name="Line">The row's position.</param>
/// <param name="Optional">A quantity, if any.</param>
public sealed record CsvAttributedOptionalRecord(int Line, [TypeConverter(typeof(ValueObjectTypeConverter<Quantity, short>))] Quantity? Optional);

/// <summary>An order whose nullable quantity may be missing from the header.</summary>
public sealed class CsvOptionalColumn
{
    public Iban Iban { get; set; }

    [Optional]
    public Quantity? Optional { get; set; }
}

/// <summary>A row of every family of underlying type the text of a value object differs in from CsvHelper's own.</summary>
public sealed class CsvFamilies
{
    public BirthDate Date { get; set; }

    public RecordedAt Recorded { get; set; }

    public OccurredAt Occurred { get; set; }

    public OpeningTime Opening { get; set; }

    public Duration Duration { get; set; }

    public Consent Consent { get; set; }

    public Latitude Latitude { get; set; }

    public Fingerprint Fingerprint { get; set; }

    public Celsius Celsius { get; set; }

    public Floor Floor { get; set; }

    public Label Label { get; set; }
}

/// <summary>A class map calling <c>AutoMap(CultureInfo)</c> in its constructor: the trap, a context of its own.</summary>
public sealed class CsvAutoMapCultureMap : ClassMap<CsvOrder>
{
    public CsvAutoMapCultureMap() => AutoMap(CultureInfo.InvariantCulture);
}

/// <summary>A class map calling <c>AutoMap(context)</c> over a context it gives the factory.</summary>
public sealed class CsvAutoMapContextMap : ClassMap<CsvOrder>
{
    public CsvAutoMapContextMap()
    {
        var context = new CsvContext(new CsvConfiguration(CultureInfo.InvariantCulture));
        context.TypeConverterCache.AddValueObjects();
        AutoMap(context);
    }
}

/// <summary>A class map naming each member, the account's converter among them.</summary>
public sealed class CsvNamedMap : ClassMap<CsvOrder>
{
    public CsvNamedMap()
    {
        Map(order => order.Iban).TypeConverter<ValueObjectTypeConverter<Iban, string>>();
        Map(order => order.Quantity).TypeConverter<ValueObjectTypeConverter<Quantity, short>>();
    }
}

/// <summary>A class map naming each member, with the converters the context holds.</summary>
public sealed class CsvPlainMap : ClassMap<CsvOrder>
{
    public CsvPlainMap()
    {
        Map(order => order.Iban);
        Map(order => order.Quantity);
    }
}

/// <summary>A class map naming the converter of a nullable value object, around which CsvHelper puts no <c>NullableConverter</c>.</summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
public sealed class CsvNamedOptionalMap<TSelf, TValue> : ClassMap<CsvLine<TSelf?>>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    public CsvNamedOptionalMap()
    {
        Map(line => line.Line);
        Map(line => line.Value).TypeConverter<ValueObjectTypeConverter<TSelf, TValue>>();
    }
}

/// <summary>A class map of one quantity, which a test configures.</summary>
public sealed class CsvQuantityMap : ClassMap<CsvRow<Quantity>>
{
    public CsvQuantityMap(Action<MemberMap<CsvRow<Quantity>, Quantity>> configure) => configure(Map(row => row.Value));
}

/// <summary>A class map of one nullable quantity, which a test configures.</summary>
public sealed class CsvOptionalQuantityMap : ClassMap<CsvRow<Quantity?>>
{
    public CsvOptionalQuantityMap(Action<MemberMap<CsvRow<Quantity?>, Quantity?>> configure) => configure(Map(row => row.Value));
}
