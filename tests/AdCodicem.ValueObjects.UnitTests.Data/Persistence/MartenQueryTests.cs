using System.Linq.Expressions;
using AdCodicem.ValueObjects.Marten;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using AdCodicem.ValueObjects.UnitTests.TestData;
using Marten;
using Marten.Exceptions;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// What Marten's LINQ sends PostgreSQL for a query over value objects under <c>UseValueObjects()</c>: the stored text,
/// cast to the underlying type's column type, compared with a parameter holding the underlying value. Marten writes the
/// SQL with no connection; the integration suite runs the same shapes against a server and against the documents in
/// memory.
/// </summary>
public sealed class MartenQueryTests
{
    private const string Where = "select d.data from public.mt_doc_martenorder as d where ";

    private const string ValidIban = "FR7630006000011234567890189";

    private static readonly Iban Fr = Iban.Create(ValidIban);

    private static readonly PageNumber Three = PageNumber.Create(3);

    private static readonly List<CountryCode> Codes = [CountryCode.France, CountryCode.Belgium];

    private static readonly CountryCode[] CodeArray = [.. Codes];

    private static readonly List<CountryCode?> CodesAndNull = [CountryCode.France, null];

    private static readonly Label Tag = Label.Create("tag");

    private static readonly List<OccurredAt> Moments = [OccurredAt.Create(new DateTimeOffset(2026, 1, 1, 2, 0, 0, TimeSpan.FromHours(2)))];

    private static readonly int[] Indexes = [1, 2];

    private static readonly string[] Notes = ["a", "b"];

    private static readonly List<Declared<MartenTypedOnlyDeclaration, int>> Counts = [Declared<MartenTypedOnlyDeclaration, int>.Create(2)];

    private static readonly List<Reference<MartenFirstMetOwner>> FirstMetReferences = [Reference<MartenFirstMetOwner>.Create("PO-1"), Reference<MartenFirstMetOwner>.Create("PO-2")];

    // Held in fields, which C# does not check against the range of the value compared with them, as it checks a literal.
    private static readonly int BeyondAByte = 300;

    private static readonly int BeyondAChar = char.MaxValue + 1;

    public static TheoryData<string, string> Filters => new()
    {
        { "x.Iban == iban", Where + $"d.data ->> 'Iban' = :p0; | {ValidIban} String Text" },
        { "x.Iban != iban", Where + $"d.data ->> 'Iban' != :p0; | {ValidIban} String Text" },
        { "x.Iban.Value == text", Where + $"d.data ->> 'Iban' = :p0; | {ValidIban} String Text" },
        { "x.Iban.Value == refused text", Where + "d.data ->> 'Iban' = :p0; | not an iban String Text" },
        { "x.Iban.Value.StartsWith", Where + "d.data ->> 'Iban' LIKE :p0; | FR% String Text" },
        { "x.Iban.Value.EndsWith", Where + "d.data ->> 'Iban' LIKE :p0; | %89 String Text" },
        { "x.Iban.Value.Contains", Where + "d.data ->> 'Iban' LIKE :p0; | %3000% String Text" },
        { "x.Iban.Value.Equals(text, OrdinalIgnoreCase)", Where + "d.data ->> 'Iban' ILIKE :p0; | fr76 String Text" },
        { "string.IsNullOrEmpty(x.Label.Value)", Where + "(d.data ->> 'Label' IS NULL OR d.data ->> 'Label' = '');" },
        { "x.Page > page", Where + "CAST(d.data ->> 'Page' as integer) > :p0; | 3 Int32 Integer" },
        { "x.Page.Value > 3", Where + "CAST(d.data ->> 'Page' as integer) > :p0; | 3 Int32 Integer" },
        { "!(x.Page > page)", Where + "CAST(d.data ->> 'Page' as integer) <= :p0; | 3 Int32 Integer" },
        { "x.Quantity <= q", Where + "CAST(d.data ->> 'Quantity' as smallint) <= :p0; | 7 Int16 Smallint" },
        { "x.Quantity.Value <= 20, promoted to int", Where + "CAST(d.data ->> 'Quantity' as smallint) <= :p0; | 20 Int32 Integer" },
        { "x.Count <= 20, the primitive promoted to int", Where + "CAST(d.data ->> 'Count' as smallint) <= :p0; | 20 Int32 Integer" },
        { "x.Grade.Value == 'B', promoted to int", Where + "CAST(d.data ->> 'Grade' as text) = :p0; | 66 Int32 Integer" },
        { "x.Letter == 'B', the primitive promoted to int", Where + "CAST(d.data ->> 'Letter' as text) = :p0; | 66 Int32 Integer" },
        { "x.Size >= size", Where + "CAST(d.data ->> 'Size' as bigint) >= :p0; | 10 Int64 Bigint" },
        { "x.Amount >= amount", Where + "CAST(d.data ->> 'Amount' as decimal) >= :p0; | 2.50 Decimal Numeric" },
        { "x.Latitude < latitude", Where + "CAST(d.data ->> 'Latitude' as double precision) < :p0; | 45.5 Double Double" },
        { "x.Customer == id", Where + "CAST(d.data ->> 'Customer' as uuid) = :p0; | 6f9619ff-8b86-d011-b42d-00c04fc964ff Guid Uuid" },
        { "x.Consent == consent", Where + "CAST(d.data ->> 'Consent' as boolean) = :p0; | True Boolean Boolean" },
        { "x.Born < date", Where + "public.mt_immutable_date(d.data ->> 'Born') < :p0; | 01/01/2000 DateOnly Date" },
        { "x.At > moment", Where + "public.mt_immutable_timestamptz(d.data ->> 'At') > :p0; | 01/01/2026 00:00:00 +00:00 DateTimeOffset TimestampTz" },
        { "x.At > moment at an offset", Where + "public.mt_immutable_timestamptz(d.data ->> 'At') > :p0; | 01/01/2026 00:00:00 +00:00 DateTimeOffset TimestampTz" },
        { "list.Contains(x.At) at an offset", Where + "public.mt_immutable_timestamptz(d.data ->> 'At') = ANY(:p0); | [01/01/2026 00:00:00 +00:00] DateTimeOffset Array|TimestampTz" },
        { "x.Grade == grade", Where + "CAST(d.data ->> 'Grade' as text) = :p0; | B Char Text" },
        { "x.Country == known value", Where + "d.data ->> 'Country' = :p0; | FR String Text" },
        { "list.Contains(x.Country)", Where + "d.data ->> 'Country' = ANY(:p0); | [FR, BE] String Array|Text" },
        { "array.Contains(x.Country)", Where + "d.data ->> 'Country' = ANY(:p0); | [FR, BE] String Array|Text" },
        { "Enumerable.Contains(array, x.Country)", Where + "d.data ->> 'Country' = ANY(:p0); | [FR, BE] String Array|Text" },
        { "x.Country.IsOneOf(array)", Where + "d.data ->> 'Country' = ANY(:p0); | [FR, BE] String Array|Text" },
        { "x.Country.IsOneOf(list)", Where + "d.data ->> 'Country' = ANY(:p0); | [FR, BE] String Array|Text" },
        { "x.Country.In(array)", Where + "d.data ->> 'Country' = ANY(:p0); | [FR, BE] String Array|Text" },
        { "x.Country.IsOneOf(null list)", Where + "d.data ->> 'Country' = ANY(:p0); | [] String Array|Text" },
        { "list with null.Contains(x.Country)", Where + "(d.data ->> 'Country' = ANY(:p0) or d.data ->> 'Country' is null); | [FR] String Array|Text" },
        { "x.Countries.Contains(code)", Where + """d.data @> :p0; | {"Countries":["FR"]} String Jsonb""" },
        { "x.CountryArray.Contains(code)", Where + """d.data @> :p0; | {"CountryArray":["FR"]} String Jsonb""" },
        { "x.CountrySet.Contains(code)", Where + """d.data @> :p0; | {"CountrySet":["FR"]} String Jsonb""" },
        { "x.MaybeCountries.Contains(code)", Where + """d.data @> :p0; | {"MaybeCountries":["FR"]} String Jsonb""" },
        { "x.Stock.Keys.Contains(code)", Where + """d.data #> '{Stock, "FR"}' is not null;""" },
        { "x.Countries.Any(c => c == known value)", Where + """d.data @> :p0; | {"Countries":["FR"]} String Jsonb""" },
        { "x.CountryArray.Any(c => c == known value)", Where + """d.data @> :p0; | {"CountryArray":["FR"]} String Jsonb""" },
        { "x.Countries.All(c => c == known value)", Where + """NOT(jsonb_path_exists(d.data, '$.Countries[*] ? (!(@ == $val1))', :p0)); | {"val1":"FR"} String Jsonb""" },
        { "x.Countries.Count > 1", Where + "jsonb_array_length(CAST(d.data ->> 'Countries' as jsonb)) > :p0; | 1 Int32 Integer" },
        { "x.Countries.Any()", Where + "d.data @? '$ ? (@.Countries != null && @.Countries.size() > 0)';" },
        { "x.Lines.Any(l => l.Tag == tag)", Where + """d.data -> 'Lines' @> :p0; | [{"Tag":"tag"}] String Jsonb""" },
        { "x.Lines.Any(l => l.Tag.Value == text)", Where + """d.data -> 'Lines' @> :p0; | [{"Tag":"tag"}] String Jsonb""" },
        { "x.Lines.Any(l => l.Country == known value)", Where + """d.data -> 'Lines' @> :p0; | [{"Country":"FR"}] String Jsonb""" },
        { "x.Lines.Any(l => l.Quantity > q)", Where + """jsonb_path_exists(d.data, '$.Lines[*] ? (@.Quantity > $val1)', :p0); | {"val1":7} String Jsonb""" },
        { "x.Lines.Any(l => l.Quantity.Value == 3), promoted to int", Where + """d.data -> 'Lines' @> :p0; | [{"Quantity":3}] String Jsonb""" },
        { "x.Lines.Any(l => l.Small == 3), the primitive promoted to int", Where + """d.data -> 'Lines' @> :p0; | [{"Small":3}] String Jsonb""" },
        { "x.Lines.Any(l => l.Score.Value == 3), promoted to int", Where + """d.data -> 'Lines' @> :p0; | [{"Score":3}] String Jsonb""" },
        { "x.Lines.Any(l => l.Points == 3), the primitive promoted to int", Where + """d.data -> 'Lines' @> :p0; | [{"Points":3}] String Jsonb""" },
        { "x.Lines.Any(l => l.Adjustment.Value == -3), promoted to int", Where + """d.data -> 'Lines' @> :p0; | [{"Adjustment":-3}] String Jsonb""" },
        { "x.Lines.Any(l => l.Degrees == -3), the primitive promoted to int", Where + """d.data -> 'Lines' @> :p0; | [{"Degrees":-3}] String Jsonb""" },
        { "x.Lines.Any(l => l.Grade.Value == 'B'), promoted to int", Where + """d.data -> 'Lines' @> :p0; | [{"Grade":"B"}] String Jsonb""" },
        { "x.Lines.Any(l => l.Letter == 'B'), the primitive promoted to int", Where + """d.data -> 'Lines' @> :p0; | [{"Letter":66}] String Jsonb""" },
        { "x.Lines.Any(l => l.Score.Value == 300), beyond a byte", Where + """d.data -> 'Lines' @> :p0; | [{"Score":300}] String Jsonb""" },
        { "x.Lines.Any(l => l.Grade.Value == 65536), beyond a char", Where + """d.data -> 'Lines' @> :p0; | [{"Grade":65536}] String Jsonb""" },
        { "x.Lines.Any(l => l.Adjustment.Value == 3 && l.Score == s)", Where + """d.data -> 'Lines' @> :p0; | [{"Adjustment":3,"Score":4}] String Jsonb""" },
        { "x.Backup == null", Where + "d.data ->> 'Backup' is null;" },
        { "x.Backup == iban", Where + $"d.data ->> 'Backup' = :p0; | {ValidIban} String Text" },
        { "x.MaybePage > page", Where + "CAST(d.data ->> 'MaybePage' as integer) > :p0; | 3 Int32 Integer" },
        { "x.MaybePage == null", Where + "d.data ->> 'MaybePage' is null;" },
        { "x.MaybePage.HasValue", Where + "d.data ->> 'MaybePage' is not null;" },
        { "x.Reference == construction", Where + "d.data ->> 'Reference' = :p0; | PO-1 String Text" },
        { "x.Code == written by hand", Where + "d.data ->> 'Code' = :p0; | ABC String Text" },
    };

    private static readonly Dictionary<string, Expression<Func<MartenOrder, bool>>> FilterExpressions = new()
    {
        ["x.Iban == iban"] = x => x.Iban == Fr,
        ["x.Iban != iban"] = x => x.Iban != Fr,
        ["x.Iban.Value == text"] = x => x.Iban.Value == ValidIban,
        ["x.Iban.Value == refused text"] = x => x.Iban.Value == "not an iban",
        ["x.Iban.Value.StartsWith"] = x => x.Iban.Value.StartsWith("FR"),
        ["x.Iban.Value.EndsWith"] = x => x.Iban.Value.EndsWith("89"),
        ["x.Iban.Value.Contains"] = x => x.Iban.Value.Contains("3000"),
        ["x.Iban.Value.Equals(text, OrdinalIgnoreCase)"] = x => x.Iban.Value.Equals("fr76", StringComparison.OrdinalIgnoreCase),
        ["string.IsNullOrEmpty(x.Label.Value)"] = x => string.IsNullOrEmpty(x.Label.Value),
        ["x.Page > page"] = x => x.Page > Three,
        ["x.Page.Value > 3"] = x => x.Page.Value > 3,
        ["!(x.Page > page)"] = x => !(x.Page > Three),
        ["x.Quantity <= q"] = x => x.Quantity <= Quantity.Create(7),
        ["x.Quantity.Value <= 20, promoted to int"] = x => x.Quantity.Value <= 20,
        ["x.Count <= 20, the primitive promoted to int"] = x => x.Count <= 20,
        ["x.Grade.Value == 'B', promoted to int"] = x => x.Grade.Value == 'B',
        ["x.Letter == 'B', the primitive promoted to int"] = x => x.Letter == 'B',
        ["x.Size >= size"] = x => x.Size >= FileSize.Create(10),
        ["x.Amount >= amount"] = x => x.Amount >= Amount.Create(2.50m),
        ["x.Latitude < latitude"] = x => x.Latitude < Latitude.Create(45.5),
        ["x.Customer == id"] = x => x.Customer == CustomerId.Create(Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff")),
        ["x.Consent == consent"] = x => x.Consent == Consent.Create(true),
        ["x.Born < date"] = x => x.Born < BirthDate.Create(new DateOnly(2000, 1, 1)),
        ["x.At > moment"] = x => x.At > OccurredAt.Create(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        ["x.At > moment at an offset"] = x => x.At > OccurredAt.Create(new DateTimeOffset(2026, 1, 1, 2, 0, 0, TimeSpan.FromHours(2))),
        ["list.Contains(x.At) at an offset"] = x => Moments.Contains(x.At),
        ["x.Grade == grade"] = x => x.Grade == Grade.Create('B'),
        ["x.Country == known value"] = x => x.Country == CountryCode.France,
        ["list.Contains(x.Country)"] = x => Codes.Contains(x.Country),
        ["array.Contains(x.Country)"] = x => CodeArray.Contains(x.Country),
        ["Enumerable.Contains(array, x.Country)"] = x => Enumerable.Contains(CodeArray, x.Country),
        ["x.Country.IsOneOf(array)"] = x => x.Country.IsOneOf(CodeArray),
        ["x.Country.IsOneOf(list)"] = x => x.Country.IsOneOf(Codes),
        ["x.Country.In(array)"] = x => x.Country.In(CodeArray),
        ["x.Country.IsOneOf(null list)"] = x => x.Country.IsOneOf((List<CountryCode>)null!),
        ["list with null.Contains(x.Country)"] = x => CodesAndNull.Contains(x.Country),
        ["x.Countries.Contains(code)"] = x => x.Countries.Contains(CountryCode.France),
        ["x.CountryArray.Contains(code)"] = x => x.CountryArray.Contains(CountryCode.France),
        ["x.CountrySet.Contains(code)"] = x => x.CountrySet.Contains(CountryCode.France),
        ["x.MaybeCountries.Contains(code)"] = x => x.MaybeCountries.Contains(CountryCode.France),
        ["x.Stock.Keys.Contains(code)"] = x => x.Stock.Keys.Contains(CountryCode.France),
        ["x.Countries.Any(c => c == known value)"] = x => x.Countries.Any(c => c == CountryCode.France),
        ["x.CountryArray.Any(c => c == known value)"] = x => x.CountryArray.Any(c => c == CountryCode.France),
        ["x.Countries.All(c => c == known value)"] = x => x.Countries.All(c => c == CountryCode.France),
        ["x.Countries.Count > 1"] = x => x.Countries.Count > 1,
        ["x.Countries.Any()"] = x => x.Countries.Any(),
        ["x.Lines.Any(l => l.Tag == tag)"] = x => x.Lines.Any(l => l.Tag == Tag),
        ["x.Lines.Any(l => l.Tag.Value == text)"] = x => x.Lines.Any(l => l.Tag.Value == "tag"),
        ["x.Lines.Any(l => l.Country == known value)"] = x => x.Lines.Any(l => l.Country == CountryCode.France),
        ["x.Lines.Any(l => l.Quantity > q)"] = x => x.Lines.Any(l => l.Quantity > Quantity.Create(7)),
        ["x.Lines.Any(l => l.Quantity.Value == 3), promoted to int"] = x => x.Lines.Any(l => l.Quantity.Value == 3),
        ["x.Lines.Any(l => l.Small == 3), the primitive promoted to int"] = x => x.Lines.Any(l => l.Small == 3),
        ["x.Lines.Any(l => l.Score.Value == 3), promoted to int"] = x => x.Lines.Any(l => l.Score.Value == 3),
        ["x.Lines.Any(l => l.Points == 3), the primitive promoted to int"] = x => x.Lines.Any(l => l.Points == 3),
        ["x.Lines.Any(l => l.Adjustment.Value == -3), promoted to int"] = x => x.Lines.Any(l => l.Adjustment.Value == -3),
        ["x.Lines.Any(l => l.Degrees == -3), the primitive promoted to int"] = x => x.Lines.Any(l => l.Degrees == -3),
        ["x.Lines.Any(l => l.Grade.Value == 'B'), promoted to int"] = x => x.Lines.Any(l => l.Grade.Value == 'B'),
        ["x.Lines.Any(l => l.Letter == 'B'), the primitive promoted to int"] = x => x.Lines.Any(l => l.Letter == 'B'),
        ["x.Lines.Any(l => l.Score.Value == 300), beyond a byte"] = x => x.Lines.Any(l => l.Score.Value == BeyondAByte),
        ["x.Lines.Any(l => l.Grade.Value == 65536), beyond a char"] = x => x.Lines.Any(l => l.Grade.Value == BeyondAChar),
        ["x.Lines.Any(l => l.Adjustment.Value == 3 && l.Score == s)"] = x => x.Lines.Any(l => l.Adjustment.Value == 3 && l.Score == Score.Create(4)),
        ["x.Backup == null"] = x => x.Backup == null,
        ["x.Backup == iban"] = x => x.Backup == Fr,
        ["x.MaybePage > page"] = x => x.MaybePage > Three,
        ["x.MaybePage == null"] = x => x.MaybePage == null,
        ["x.MaybePage.HasValue"] = x => x.MaybePage.HasValue,
        ["x.Reference == construction"] = x => x.Reference == Reference<MartenOwner>.Create("PO-1"),
        ["x.Code == written by hand"] = x => x.Code == Declared<MartenCodeDeclaration, string>.Create("ABC"),
    };

    /// <summary>
    /// Every value object of the domain, over each of the underlying types, is compared and listed as a member of its
    /// underlying type holding the same value is.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_is_queried_as_the_value_it_carries(string type)
        => Samples.All[type].QueriesInADocumentStoreAsItsUnderlyingValue();

    [Theory]
    [MemberData(nameof(Filters))]
    public void A_filter_over_a_value_object_reads_its_stored_value_as_its_underlying_type(string shape, string expected)
        => MartenHarness.Sql<MartenOrder>(orders => orders.Where(FilterExpressions[shape])).Description.Should().Be(expected);

    [Fact]
    public void An_order_casts_a_value_object_as_its_underlying_type()
        => MartenHarness.Sql<MartenOrder>(static orders => orders.OrderBy(x => x.Page).ThenByDescending(x => x.Iban)).Text
            .Should().Be("select d.data from public.mt_doc_martenorder as d order by CAST(d.data ->> 'Page' as integer), d.data ->> 'Iban' desc;");

    [Fact]
    public void A_value_object_over_a_type_Marten_maps_to_no_column_is_refused_as_that_type_is()
    {
        var valueObject = FluentActions.Invoking(static () => MartenHarness.Sql<MartenOrder>(static orders => orders.Where(x => x.Port == Port.Create(8080))));
        var primitive = FluentActions.Invoking(static () => MartenHarness.Sql<MartenOrder>(static orders => orders.Where(x => x.PortNumber == 8080)));

        var refusal = primitive.Should().Throw<BadLinqExpressionException>().Which.Message;
        valueObject.Should().Throw<BadLinqExpressionException>().WithMessage(refusal);
    }

    [Fact]
    public void What_Marten_does_not_translate_is_refused_rather_than_answered_with_no_rows()
    {
        FluentActions.Invoking(static () => MartenHarness.Sql<MartenOrder>(static orders => orders.Where(x => x.Iban.ToString() == ValidIban)))
            .Should().Throw<BadLinqExpressionException>().WithMessage("Marten does not (yet) support member Iban.ToString*");
        FluentActions.Invoking(static () => MartenHarness.Sql<MartenOrder>(static orders => orders.Where(x => x.Iban.CountryCode == "FR")))
            .Should().Throw<BadLinqExpressionException>().WithMessage("Marten does not (yet) support member Iban.CountryCode*");
        FluentActions.Invoking(static () => MartenHarness.Sql<MartenOrder>(static orders => orders.Where(x => x.Page.ToString() == "3")))
            .Should().Throw<BadLinqExpressionException>().WithMessage("Marten does not (yet) support member PageNumber.ToString*");
        FluentActions.Invoking(static () => MartenHarness.Sql<MartenOrder>(static orders => orders.Where(x => x.Backup!.Value.CountryCode == "FR")))
            .Should().Throw<BadLinqExpressionException>().WithMessage("Marten does not (yet) support member Nullable<Iban>.CountryCode*", "Marten names a nullable member over text as its member's type");
        FluentActions.Invoking(static () => MartenHarness.Sql<MartenOrder>(static orders => orders.Where(x => x.Iban.Equals(Fr))))
            .Should().Throw<NotSupportedException>().WithMessage("Marten does not (yet) support Linq queries using the *Iban.Equals() method*");
    }

    /// <summary>
    /// <c>CompareTo</c> is Marten's own: it hands the value object to Npgsql as the parameter, which no column type takes,
    /// so the query fails when it runs; <c>&gt;</c> is the form that translates.
    /// </summary>
    [Fact]
    public void CompareTo_hands_the_value_object_itself_to_the_parameter()
        => MartenHarness.Sql<MartenOrder>(static orders => orders.Where(x => x.Page.CompareTo(Three) > 0)).Parameters
            .Should().ContainSingle().Which.Value.Should().Be(Three);

    [Fact]
    public void A_store_taught_one_value_object_translates_it_and_leaves_the_others_to_Marten()
    {
        using var store = MartenHarness.Offline(static options => options.UseValueObject<PageNumber, int>());

        MartenHarness.Sql<MartenTypedOnlyOrder>(store, static orders => orders.Where(x => x.Page > Three)).Description
            .Should().Be("select d.data from public.mt_doc_martentypedonlyorder as d where CAST(d.data ->> 'Page' as integer) > :p0; | 3 Int32 Integer");
        MartenHarness.Sql<MartenTypedOnlyOrder>(store, static orders => orders.Where(x => new[] { Three }.Contains(x.Page))).Description
            .Should().Be("select d.data from public.mt_doc_martentypedonlyorder as d where CAST(d.data ->> 'Page' as integer) = ANY(:p0); | [3] Int32 Array|Integer");
        FluentActions.Invoking(() => MartenHarness.Sql<MartenTypedOnlyOrder>(store, static orders => orders.Where(x => x.Count == Declared<MartenTypedOnlyDeclaration, int>.Create(2))))
            .Should().Throw<BadLinqExpressionException>().WithMessage("Marten cannot support custom value types in Linq expression*");
        MartenHarness.Sql<MartenTypedOnlyOrder>(store, static orders => orders.Where(x => Indexes.Contains(x.Index))).Text
            .Should().Be("select d.data from public.mt_doc_martentypedonlyorder as d where CAST(d.data ->> 'Index' as integer) = ANY(:p0);");
        FluentActions.Invoking(() => MartenHarness.Sql<MartenTypedOnlyOrder>(store, static orders => orders.Where(x => Counts.Contains(x.Count))))
            .Should().Throw<NotSupportedException>().WithMessage("Can't infer NpgsqlDbType for type *Declared*", "Marten's own Contains hands the value objects on, which Npgsql cannot type");
    }

    [Fact]
    public void A_method_named_as_Marten_s_own_but_declared_elsewhere_is_left_to_Marten()
    {
        FluentActions.Invoking(static () => MartenHarness.Sql<MartenOrder>(static orders => orders.Where(x => x.Country.IsOneOf(CountryCode.France, CountryCode.Belgium, CountryCode.Luxembourg, CountryCode.France))))
            .Should().NotThrow("Marten's own IsOneOf over params is read by the package");
        FluentActions.Invoking(static () => MartenHarness.Sql<MartenOrder>(static orders => orders.Where(x => MartenLookalikes.IsOneOf(x.Country, CountryCode.France))))
            .Should().Throw<NotSupportedException>().WithMessage("Marten does not (yet) support Linq queries using the *MartenLookalikes.IsOneOf() method*");
        MartenHarness.Sql<MartenOrder>(static orders => orders.Where(x => Codes.Contains(x.Country, EqualityComparer<CountryCode>.Default))).Description
            .Should().EndWith("| [FR, BE] CountryCode Array|Text", "a comparer changes what equal means, so the package reads no Contains taking one, and Marten's own parser hands the value objects on unconverted");
    }

    [Fact]
    public void A_Contains_over_anything_but_a_value_object_is_left_to_Marten()
    {
        MartenHarness.Sql<MartenOrder>(static orders => orders.Where(x => Indexes.Contains(x.Index))).Description
            .Should().Be(Where + "CAST(d.data ->> 'Index' as integer) = ANY(:p0); | [1, 2] Int32 Array|Integer");
        MartenHarness.Sql<MartenOrder>(static orders => orders.Where(x => Notes.Contains(x.Note))).Description
            .Should().Be(Where + "d.data ->> 'Note' = ANY(:p0); | [a, b] String Array|Text");
    }

    /// <summary>
    /// A construction nothing registered is not among the value objects <c>UseValueObjects()</c> describes up front: a
    /// <c>Contains</c> meeting it before any comparison does is claimed on the registry's word, its underlying type being
    /// one Marten maps, before Marten resolves the member.
    /// </summary>
    [Fact]
    public void A_Contains_meeting_a_construction_nothing_registered_yet_reads_it_as_its_underlying_values()
    {
        ValueObjectRegistry.TryGet(typeof(Reference<MartenFirstMetOwner>), out _)
            .Should().BeFalse("no other test closes the construction, so the store has not described it when the query meets it");
        using var store = MartenHarness.Offline(static options => options.UseValueObjects());

        MartenHarness.Sql<MartenFirstMetOrder>(store, static orders => orders.Where(x => FirstMetReferences.Contains(x.Reference))).Description
            .Should().Be("select d.data from public.mt_doc_martenfirstmetorder as d where d.data ->> 'Reference' = ANY(:p0); | [PO-1, PO-2] String Array|Text");
    }

    [Fact]
    public void A_Contains_over_a_duplicated_field_reads_its_column()
    {
        using var store = MartenHarness.Offline(static options =>
        {
            options.UseValueObjects();
            options.Schema.For<MartenOrder>().Duplicate(x => x.Iban).Duplicate(x => x.Page);
        });

        MartenHarness.Sql<MartenOrder>(store, static orders => orders.Where(x => new[] { Fr }.Contains(x.Iban))).Description
            .Should().Be(Where + $"d.iban = ANY(:p0); | [{ValidIban}] String Array|Text");
        MartenHarness.Sql<MartenOrder>(store, static orders => orders.Where(x => x.Page.IsOneOf(Three))).Description
            .Should().Be(Where + "d.page = ANY(:p0); | [3] Int32 Array|Integer");
        MartenHarness.Sql<MartenOrder>(store, static orders => orders.Where(x => x.Iban.Value.StartsWith("FR"))).Text
            .Should().Be(Where + "d.iban LIKE :p0;", "Marten's string methods read the duplicated column");
        MartenHarness.Sql<MartenOrder>(store, static orders => orders.Where(x => x.Iban == Fr)).Text
            .Should().Be(Where + "d.data ->> 'Iban' = :p0;", "Marten's duplicated field compares through the document");
    }

    [Fact]
    public void An_identity_declared_with_UseValueObjectId_is_compared_and_listed_on_its_column()
    {
        using var store = MartenHarness.Offline(static options =>
        {
            options.UseValueObjects();
            options.UseValueObjectId<MartenAccount, CustomerId, Guid>();
        });
        var id = CustomerId.Create(Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff"));

        MartenHarness.Sql<MartenAccount>(store, orders => orders.Where(x => x.Id == id)).Description
            .Should().Be("select d.data from public.mt_doc_martenaccount as d where d.id = :p0; | 6f9619ff-8b86-d011-b42d-00c04fc964ff Guid Uuid");
        MartenHarness.Sql<MartenAccount>(store, orders => orders.Where(x => x.Id.IsOneOf(new[] { id }))).Description
            .Should().Be("select d.data from public.mt_doc_martenaccount as d where d.id = ANY(:p0); | [6f9619ff-8b86-d011-b42d-00c04fc964ff] Guid Array|Uuid");
    }

    /// <summary>
    /// A member of a generic type taking a value object as its one type argument is a collection only when it holds values
    /// of the value object: a child document of such a type is Marten's, read through its own members.
    /// </summary>
    [Fact]
    public void A_child_document_of_a_generic_type_over_a_value_object_is_no_collection_of_it()
        => MartenHarness.Sql<MartenBoxed>(static boxed => boxed.Where(x => x.Box.Item == Fr)).Description
            .Should().Be($"select d.data from public.mt_doc_martenboxed as d where d.data -> 'Box' ->> 'Item' = :p0; | {ValidIban} String Text");

    [Fact]
    public void A_Contains_over_values_the_document_holds_is_left_to_Marten()
    {
        // Marten keeps the parser it found for a method: List<CountryCode>.Contains claims both shapes, in either order.
        using var store = MartenHarness.Offline(static options => options.UseValueObjects());

        MartenHarness.Sql<MartenOrder>(store, static orders => orders.Where(x => x.Countries.Contains(CountryCode.France))).Text
            .Should().Be(Where + "d.data @> :p0;");
        MartenHarness.Sql<MartenOrder>(store, static orders => orders.Where(x => Codes.Contains(x.Country))).Text
            .Should().Be(Where + "d.data ->> 'Country' = ANY(:p0);");
        MartenHarness.Sql<MartenOrder>(store, static orders => orders.Where(x => x.Countries.Contains(CountryCode.Belgium))).Text
            .Should().Be(Where + "d.data @> :p0;");
    }
}

/// <summary>An order a store taught one value object queries.</summary>
public sealed class MartenTypedOnlyOrder
{
    public Guid Id { get; set; }

    public PageNumber Page { get; set; }

    public Label Label { get; set; }

    public Declared<MartenTypedOnlyDeclaration, int> Count { get; set; }

    public int Index { get; set; }
}

/// <summary>An order holding a construction of a generic value object that a <c>Contains</c> is the first to meet.</summary>
public sealed class MartenFirstMetOrder
{
    public Guid Id { get; set; }

    public Reference<MartenFirstMetOwner> Reference { get; set; }
}

/// <summary>The owner of the references a <see cref="MartenFirstMetOrder"/> holds, which no other test closes over.</summary>
public sealed class MartenFirstMetOwner;

/// <summary>A document holding a child document of a generic type over a value object.</summary>
public sealed class MartenBoxed
{
    public Guid Id { get; set; }

    public MartenBox<Iban> Box { get; set; } = new();
}

/// <summary>A child document holding one value of its type argument.</summary>
/// <typeparam name="T">The type of the value.</typeparam>
public sealed class MartenBox<T>
{
    public T Item { get; set; } = default!;
}

/// <summary>A value object written by hand nothing but <see cref="MartenQueryTests"/> meets, and never through the registry.</summary>
public sealed class MartenTypedOnlyDeclaration : IDeclaration<int>
{
    public static ValueObjectSchema Schema { get; } = new();
}

/// <summary>An account whose identity is a value object.</summary>
public sealed class MartenAccount
{
    public CustomerId Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>A method named as Marten's own <c>IsOneOf</c>, declared elsewhere, which neither the package nor Marten reads.</summary>
public static class MartenLookalikes
{
    public static bool IsOneOf(CountryCode code, CountryCode other) => code == other;
}
