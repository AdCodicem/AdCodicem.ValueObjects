using System.Globalization;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// <c>.Value</c> of a value object in a LINQ query, which <c>UseValueObjectQueries()</c> translates to its column, typed as
/// the underlying value.
/// </summary>
/// <remarks>
/// SQLite in memory runs each query, with no server: each test opens a database of its own. Npgsql, whose query
/// compilation opens no connection, writes the PostgreSQL text, where a collection is an array. Entity Framework Core
/// builds a model once per context type, so each model shape has a context type of its own. What SQL Server makes of a
/// query, and the queries on a real PostgreSQL, are checked in the integration suite.
/// </remarks>
public sealed class ValueObjectQueryTests : IDisposable
{
    private static readonly Iban French = Iban.Create("FR7630006000011234567890189");
    private static readonly Iban Belgian = Iban.Create("BE68539007547034");
    private static readonly HandWrittenLink Site = HandWrittenLink.Create(new Uri("https://example.com/a"));

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ValueObjectQueryTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// Every value object of the domain, over each of the underlying types, translates <c>.Value</c> in a query as the same
    /// query over a property of its underlying type translates, on SQLite and on PostgreSQL; one over a 128-bit integer,
    /// which the convention leaves to a converter of the application's own, is left out.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_translates_its_value_as_its_underlying_value(string type)
        => Samples.All[type].TranslatesItsValueInAQueryAsItsUnderlyingValue();

    /// <summary>
    /// <c>.Value</c> of a value object over text reads the column itself: a prefix search is a <c>LIKE</c> on the column,
    /// which an index on it serves, and an equality compares the column, with no <c>CAST</c>, and with no test for a
    /// <c>NULL</c> the column cannot hold; on PostgreSQL too.
    /// </summary>
    [Fact]
    public async Task The_value_of_a_value_object_over_text_is_its_column()
    {
        await SeedAsync();
        await using var context = Open<OrderContext>();
        var prefix = "FR";
        var text = Belgian.Value;

        var byPrefix = context.Orders.Where(order => order.Iban.Value.StartsWith(prefix)).Select(order => order.Id);
        byPrefix.ToQueryString().Should().Contain("""WHERE "o"."Iban" LIKE @prefix_startswith ESCAPE '\'""").And.NotContain("CAST");
        (await byPrefix.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);

        var byText = context.Orders.Where(order => order.Iban.Value == text).Select(order => order.Id);
        byText.ToQueryString().Should().Contain("""WHERE "o"."Iban" = @text""").And.NotContain("CAST");
        (await byText.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(2);

        var byOtherText = context.Orders.Where(order => order.Iban.Value != text).Select(order => order.Id);
        byOtherText.ToQueryString().Should().Contain("""WHERE "o"."Iban" <> @text""").And.NotContain("IS NULL", "the column holds no NULL");
        (await byOtherText.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);

        await using var postgreSql = new PostgreSqlOrderContext();
        postgreSql.Orders.Where(order => order.Iban.Value.StartsWith(prefix)).ToQueryString()
            .Should().Contain("""WHERE o."Iban" LIKE @prefix_startswith""").And.NotContain("CAST").And.NotContain("::");
    }

    /// <summary>
    /// <c>.Value</c> of a value object over a number reads the column as that number, in a comparison, in arithmetic and in
    /// an ordering, as the same query over a property of the number does.
    /// </summary>
    /// <remarks>
    /// Run in the invariant culture: EF Core 10's SQLite provider orders decimals through its <c>EF_DECIMAL</c> collation,
    /// which reads the stored text with <c>decimal.Parse(string)</c>, in the current culture, so that under fr-FR the
    /// ordering throws a <see cref="FormatException"/> on <c>25.0</c>, whether or not a value object holds the column
    /// (dotnet/efcore#37432, fixed in EF Core 11).
    /// </remarks>
    [Fact]
    public async Task The_value_of_a_value_object_over_a_number_is_its_column()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            await SeedAsync();
            await using var context = Open<OrderContext>();
            short least = 2;

            var compared = context.Orders.Where(order => order.Quantity.Value > least && order.Total.Value * 2 > 10).Select(order => order.Id);
            compared.ToQueryString().Should().Contain("""WHERE "o"."Quantity" > @least AND ef_compare(ef_multiply("o"."Total", '2.0'), '10.0') > 0""");
            (await compared.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);

            var ordered = context.Orders.OrderBy(order => order.Total.Value * 2).Select(order => order.Id);
            ordered.ToQueryString().Should().Contain("""ORDER BY ef_multiply("o"."Total", '2.0')""");
            (await ordered.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(2, 1);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    /// <summary>
    /// <c>.Value</c> of an optional value object, over text or over a number, reads its column as it stands, as nullable as
    /// it is: the row holding a <c>NULL</c> is not returned by a comparison, and is by an inequality, as for a column of the
    /// underlying type.
    /// </summary>
    [Fact]
    public async Task The_value_of_an_optional_value_object_is_its_column()
    {
        await SeedAsync();
        await using var context = Open<OrderContext>();
        var prefix = "BE";
        short least = 2;

        var byPrevious = context.Orders.Where(order => order.Previous!.Value.Value.StartsWith(prefix)).Select(order => order.Id);
        byPrevious.ToQueryString().Should().Contain("""WHERE "o"."Previous" LIKE @prefix_startswith""").And.NotContain("CAST");
        (await byPrevious.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);

        var text = Belgian.Value;
        var byOtherPrevious = context.Orders.Where(order => order.Previous!.Value.Value != text).Select(order => order.Id);
        byOtherPrevious.ToQueryString().Should().Contain("""WHERE "o"."Previous" <> @text OR "o"."Previous" IS NULL""");
        (await byOtherPrevious.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(2);

        var byBackorder = context.Orders
            .Where(order => order.Backorder.HasValue && order.Backorder.Value.Value > least)
            .Select(order => order.Id);
        byBackorder.ToQueryString().Should().Contain("""WHERE "o"."Backorder" IS NOT NULL AND "o"."Backorder" > @least""");
        (await byBackorder.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);
    }

    /// <summary>
    /// <c>.Value</c> of a value object inside an owned type or a complex type mapped to a JSON column reads the value at
    /// its path, typed as the underlying value, as nullable as it was: an inequality on an optional one returns the row
    /// whose member is <c>null</c>, as on a member of the underlying type.
    /// </summary>
    [Fact]
    public async Task The_value_of_a_value_object_inside_a_JSON_column_is_read_at_its_path()
    {
        await SeedAsync();
        await using var context = Open<OrderContext>();
        var prefix = "FR";
        short least = 2;

        var byContact = context.Orders.Where(order => order.Contact.Account.Value.StartsWith(prefix)).Select(order => order.Id);
        byContact.ToQueryString().Should().Contain("""WHERE "o"."Contact" ->> 'Account' LIKE @prefix_startswith""").And.NotContain("CAST");
        (await byContact.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);

        var text = French.Value;
        var byOtherContact = context.Orders.Where(order => order.Contact.Account.Value != text).Select(order => order.Id);
        byOtherContact.ToQueryString().Should().Contain("""WHERE "o"."Contact" ->> 'Account' <> @text""").And.NotContain("IS NULL");
        (await byOtherContact.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(2);

        var byAddress = context.Orders.Where(order => order.Address.Floor.Value > least).Select(order => order.Id);
        byAddress.ToQueryString().Should().Contain("""WHERE "o"."Address" ->> 'Floor' > @least""").And.NotContain("CAST");
        (await byAddress.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);

        var previous = Belgian.Value;
        var byOtherPreviousContact = context.Orders.Where(order => order.Contact.Previous!.Value.Value != previous).Select(order => order.Id);
        byOtherPreviousContact.ToQueryString()
            .Should().Contain("""WHERE "o"."Contact" ->> 'Previous' <> @previous OR "o"."Contact" ->> 'Previous' IS NULL""");
        (await byOtherPreviousContact.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(2);

        var byOtherPreviousAddress = context.Orders.Where(order => order.Address.Previous!.Value.Value != previous).Select(order => order.Id);
        byOtherPreviousAddress.ToQueryString()
            .Should().Contain("""WHERE "o"."Address" ->> 'Previous' <> @previous OR "o"."Address" ->> 'Previous' IS NULL""");
        (await byOtherPreviousAddress.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(2);

        await using var postgreSql = new PostgreSqlOrderContext();
        postgreSql.Orders.Where(order => order.Contact.Account.Value.StartsWith(prefix)).ToQueryString()
            .Should().Contain("""o."Contact" ->> 'Account' LIKE @prefix_startswith""");
    }

    /// <summary>
    /// <c>.Value</c> of an element of a collection of value objects reads the element, converted to the underlying type
    /// by a <c>CAST</c> to its own store type: on SQLite over <c>json_each</c>, and on PostgreSQL over <c>unnest</c>, whose
    /// array type Npgsql infers back from the element, which a re-typed element would make it fail to rebuild.
    /// </summary>
    [Fact]
    public async Task The_value_of_an_element_of_a_collection_is_the_element_cast_to_its_own_store_type()
    {
        await SeedAsync();
        await using var context = Open<OrderContext>();
        var prefix = "FR";
        short least = 2;

        // The analyzer takes a lambda nested in an expression tree for code to run, where it is a query, whose comparison
        // the column's collation decides.
#pragma warning disable CA1310
        var byIbans = context.Orders.Where(order => order.Ibans.Any(iban => iban.Value.StartsWith(prefix))).Select(order => order.Id);
        byIbans.ToQueryString().Should().Contain("""json_each("o"."Ibans")""").And.Contain("""WHERE CAST("i"."value" AS TEXT) LIKE @prefix_startswith""");
        (await byIbans.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);

        var byOptional = context.Orders
            .Where(order => order.OptionalIbans.Any(iban => iban!.Value.Value.StartsWith(prefix)))
            .Select(order => order.Id);
        (await byOptional.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);

        var byQuantities = context.Orders.Where(order => order.Quantities.Any(quantity => quantity.Value > least)).Select(order => order.Id);
        byQuantities.ToQueryString().Should().Contain("""WHERE CAST("q"."value" AS INTEGER) > @least""");
        (await byQuantities.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);

        await using var postgreSql = new PostgreSqlOrderContext();
        postgreSql.Orders.Where(order => order.Ibans.Any(iban => iban.Value.StartsWith(prefix))).ToQueryString()
            .Should().Contain("""FROM unnest(o."Ibans") AS i(value)""")
            .And.Contain("""WHERE i.value::character varying(34) LIKE @prefix_startswith""");
        postgreSql.Orders.Where(order => order.OptionalIbans.Any(iban => iban!.Value.Value.StartsWith(prefix))).ToQueryString()
            .Should().Contain("""FROM unnest(o."OptionalIbans") AS o0(value)""");
        postgreSql.Orders.Where(order => order.Quantities.Any(quantity => quantity.Value > least)).ToQueryString()
            .Should().Contain("""WHERE q.value::smallint > @least""");
#pragma warning restore CA1310
    }

    /// <summary>
    /// <c>.Value</c> of a column an optional navigation joins in, and of an expression, a <c>COALESCE</c>, reads it through
    /// a <c>CAST</c> to its own store type, and runs.
    /// </summary>
    [Fact]
    public async Task The_value_of_a_joined_column_or_an_expression_is_cast_to_its_own_store_type()
    {
        await SeedAsync();
        await using var context = Open<OrderContext>();
        var prefix = "FR";

        var byParent = context.Orders.Where(order => order.Parent!.Iban.Value.StartsWith(prefix)).Select(order => order.Id);
        byParent.ToQueryString().Should().Contain("LEFT JOIN").And.Contain("""CAST("o0"."Iban" AS TEXT) LIKE @prefix_startswith""");
        (await byParent.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(2);

        var coalesced = context.Orders.Where(order => (order.Previous ?? order.Iban).Value.StartsWith(prefix)).Select(order => order.Id);
        coalesced.ToQueryString().Should().Contain("""CAST(COALESCE("o"."Previous", "o"."Iban") AS TEXT) LIKE @prefix_startswith""");
        (await coalesced.ToListAsync(TestContext.Current.CancellationToken)).Should().BeEmpty("the first order's previous IBAN is Belgian");
    }

    /// <summary>
    /// <c>.Value</c> of a construction of a generic value object, and of a value object nested in a generic type, reads its
    /// column, whose converter the convention closed over the construction.
    /// </summary>
    [Fact]
    public async Task The_value_of_a_construction_of_a_generic_value_object_is_its_column()
    {
        await SeedAsync();
        await using var context = Open<OrderContext>();
        var prefix = "PO";
        var least = 1;

        var byReference = context.Orders.Where(order => order.Reference.Value.StartsWith(prefix)).Select(order => order.Id);
        byReference.ToQueryString().Should().Contain("""WHERE "o"."Reference" LIKE @prefix_startswith""");
        (await byReference.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);

        var byStock = context.Orders.Where(order => order.Stock.Value > least).Select(order => order.Id);
        byStock.ToQueryString().Should().Contain("""WHERE "o"."Stock" > @least""");
        (await byStock.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);
    }

    /// <summary>
    /// <c>.Value</c> of an entity identifier, which <c>ConfigureEntityIds</c> maps through this package's converter, reads
    /// its column.
    /// </summary>
    [Fact]
    public async Task The_value_of_an_entity_identifier_is_its_column()
    {
        await SeedAsync();
        await using var context = Open<OrderContext>();
        var account = (await context.Orders.SingleAsync(order => order.Id == 1, TestContext.Current.CancellationToken)).Account.Value;
        var prefix = AccountId.Prefix + "_";

        var byAccount = context.Orders.Where(order => order.Account.Value == account).Select(order => order.Id);
        byAccount.ToQueryString().Should().Contain("""WHERE "o"."Account" = @account""");
        (await byAccount.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);
        (await context.Orders.CountAsync(order => order.Account.Value.StartsWith(prefix), TestContext.Current.CancellationToken)).Should().Be(2);
    }

    /// <summary>
    /// A property whose converter Entity Framework Core composes with its own conversion of the underlying type, a value
    /// object written by hand over <see cref="Uri"/> on SQLite, or one over <see cref="ushort"/> on PostgreSQL, reads its
    /// column as the underlying value too, which the provider converts as it converts a property of that type.
    /// </summary>
    [Fact]
    public async Task The_value_of_a_value_object_whose_converter_is_composed_is_its_column()
    {
        await SeedAsync();
        await using var context = Open<OrderContext>();
        var link = Site.Value;
        var port = (ushort)80;

        var byLink = context.Orders.Where(order => order.Link!.Value.Value == link).Select(order => order.Id);
        byLink.ToQueryString().Should().Contain("""WHERE "o"."Link" = @link""");
        (await byLink.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);

        await using var postgreSql = new PostgreSqlOrderContext();
        postgreSql.Orders.Where(order => order.Port.Value > port).ToQueryString().Should().Contain("""WHERE o."Port" > @port""");
    }

    /// <summary>
    /// A property mapped through a converter of the application's own, written inline, as a class Entity Framework Core
    /// composes with its own conversion, or composed around this package's converter where the provider stores the
    /// underlying type as it stands, keeps Entity Framework Core's refusal: what its column holds may not be the value,
    /// and a projected value is still read through the converter. A property the convention maps, in the same model, is
    /// translated.
    /// </summary>
    [Fact]
    public void A_converter_of_the_applications_own_is_left_alone()
    {
        using var context = Open<BespokeContext>();
        var text = French.Value;

        context.Bespokes.Where(bespoke => bespoke.Iban.Value == text).ToQueryString().Should().Contain("""WHERE "b"."Iban" = @text""");
        context.Invoking(c => c.Bespokes.Where(bespoke => bespoke.Lowered.Value == text).ToQueryString())
            .Should().Throw<InvalidOperationException>().WithMessage("*could not be translated*");
        context.Invoking(c => c.Bespokes.Where(bespoke => bespoke.Linked.Value == text).ToQueryString())
            .Should().Throw<InvalidOperationException>().WithMessage("*could not be translated*");
        context.Invoking(c => c.Bespokes.Where(bespoke => bespoke.Sealed.Value == text).ToQueryString())
            .Should().Throw<InvalidOperationException>().WithMessage("*could not be translated*");

        context.Bespokes.Add(new Bespoke { Id = 1, Iban = French, Lowered = French, Linked = French, Sealed = French });
        context.SaveChanges();
        context.Bespokes.Select(bespoke => bespoke.Sealed.Value).Single()
            .Should().Be(French.Value, "a projected value is read through the converter, not as the column holds it");
    }

    /// <summary>
    /// A strict context translates <c>.Value</c> too, of a required and of an optional value object, over text and over
    /// a number, whose strict converters differ. A projected <c>.Value</c> is then read from the column as it stands, a
    /// value another writer stored included, which the context validates only in the value object it materializes.
    /// </summary>
    [Fact]
    public async Task A_strict_context_translates_the_value_and_reads_a_projected_value_as_stored()
    {
        await SeedAsync();
        await ExecuteAsync("""UPDATE "Orders" SET "Iban" = 'fr76 3000 6000 0112 3456 7890 189' WHERE "Id" = 1""");
        await using var context = Open<StrictOrderContext>();
        var prefix = "BE";
        short least = 2;

        (await context.Orders.Where(order => order.Iban.Value.StartsWith(prefix)).Select(order => order.Id).ToListAsync(TestContext.Current.CancellationToken))
            .Should().Equal(2);
        (await context.Orders.Where(order => order.Quantity.Value > least).Select(order => order.Id).ToListAsync(TestContext.Current.CancellationToken))
            .Should().Equal(1);
        (await context.Orders.Where(order => order.Previous!.Value.Value.StartsWith(prefix)).Select(order => order.Id).ToListAsync(TestContext.Current.CancellationToken))
            .Should().Equal(1);
        (await context.Orders.Where(order => order.Backorder!.Value.Value > least).Select(order => order.Id).ToListAsync(TestContext.Current.CancellationToken))
            .Should().Equal(1);

        (await context.Orders.Where(order => order.Id == 1).Select(order => order.Iban.Value).SingleAsync(TestContext.Current.CancellationToken))
            .Should().Be("fr76 3000 6000 0112 3456 7890 189", "a projected value is read from the column, unchecked");
        (await context.Orders.Where(order => order.Id == 1).Select(order => order.Iban).SingleAsync(TestContext.Current.CancellationToken))
            .Should().Be(French, "a projected value object is normalized and validated");
    }

    /// <summary>
    /// Without <c>UseValueObjectQueries()</c>, <c>.Value</c> is not translated, as before. With it, nothing but
    /// <c>.Value</c> is: <c>ToString()</c>, another member of the value object, <c>EF.Property</c> and <c>.Value</c> of an
    /// element of a collection passed as a parameter keep Entity Framework Core's refusal, and a conversion operator its
    /// cast.
    /// </summary>
    [Fact]
    public void Nothing_but_the_value_is_translated_and_only_once_asked_for()
    {
        using var untranslated = Open<UntranslatedOrderContext>();
        using var context = Open<OrderContext>();
        var prefix = "FR";
        List<Iban> ibans = [French];

        untranslated.Invoking(c => c.Orders.Where(order => order.Iban.Value.StartsWith(prefix)).ToQueryString())
            .Should().Throw<InvalidOperationException>().WithMessage("*could not be translated*");

        context.Invoking(c => c.Orders.Where(order => order.Iban.ToString().StartsWith(prefix)).ToQueryString())
            .Should().Throw<InvalidOperationException>().WithMessage("*could not be translated*");
        context.Invoking(c => c.Orders.Where(order => order.Iban.CountryCode == prefix).ToQueryString())
            .Should().Throw<InvalidOperationException>().WithMessage("*could not be translated*");
        context.Orders.Where(order => ((string)order.Iban).StartsWith(prefix)).ToQueryString()
            .Should().Contain("""CAST("o"."Iban" AS TEXT) LIKE""", "a conversion operator is Entity Framework Core's cast, as before");
        context.Invoking(c => c.Orders.Where(order => EF.Property<string>(order, nameof(Order.Iban)).StartsWith(prefix)).ToList())
            .Should().Throw<InvalidCastException>();
#pragma warning disable CA1310 // A query, whose comparison the column's collation decides, not code to run.
        context.Invoking(c => c.Orders.Where(order => ibans.Any(iban => iban.Value.StartsWith(prefix))).ToQueryString())
            .Should().Throw<InvalidOperationException>().WithMessage("*could not be translated*");
#pragma warning restore CA1310
    }

    /// <summary>
    /// The rest of a query is Entity Framework Core's as before: a member of the underlying value, a member of another
    /// type, a bulk update setting a column from <c>.Value</c>, a bulk delete filtered on it and a compiled query.
    /// </summary>
    [Fact]
    public async Task The_rest_of_a_query_is_translated_as_before()
    {
        await SeedAsync();
        await using var context = Open<OrderContext>();
        var prefix = "BE";

        var byLength = context.Orders.Where(order => order.Iban.Value.Length > 20 && order.Recorded.Value < DateTime.UtcNow).Select(order => order.Id);
        byLength.ToQueryString().Should().Contain("""length("o"."Iban") > 20""");
        (await byLength.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);

        (await context.Orders.Where(order => order.Id == 2).ExecuteUpdateAsync(
            setters => setters.SetProperty(order => order.Raw, order => order.Iban.Value), TestContext.Current.CancellationToken)).Should().Be(1);
        (await context.Orders.Where(order => order.Id == 2).Select(order => order.Raw).SingleAsync(TestContext.Current.CancellationToken))
            .Should().Be(Belgian.Value);

        var count = EF.CompileQuery((OrderContext compiled, string start) => compiled.Orders.Count(order => order.Iban.Value.StartsWith(start)));
        count(context, prefix).Should().Be(1);

        (await context.Orders.Where(order => order.Iban.Value.StartsWith(prefix)).ExecuteDeleteAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        (await context.Orders.Select(order => order.Id).ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);
    }

    /// <summary>
    /// <c>UseValueObjectQueries()</c> adds one extension to the options however often it is called, keeps the type of the
    /// context on a typed builder, and refuses a null builder; contexts built from like options share Entity Framework
    /// Core's service provider, and the extension says what it does where Entity Framework Core logs the options.
    /// </summary>
    [Fact]
    public void UseValueObjectQueries_adds_one_extension_to_the_options()
    {
        var builder = new DbContextOptionsBuilder().UseSqlite(_connection);
        builder.UseValueObjectQueries().Should().BeSameAs(builder);
        builder.UseValueObjectQueries();
        builder.Options.Extensions.OfType<ValueObjectQueryOptionsExtension>().Should().ContainSingle();

        var typed = new DbContextOptionsBuilder<OrderContext>();
        typed.UseValueObjectQueries().Should().BeSameAs(typed);
        typed.Options.Should().BeOfType<DbContextOptions<OrderContext>>();

        FluentActions.Invoking(() => ((DbContextOptionsBuilder)null!).UseValueObjectQueries()).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ((DbContextOptionsBuilder<OrderContext>)null!).UseValueObjectQueries()).Should().Throw<ArgumentNullException>();

        var info = builder.Options.FindExtension<ValueObjectQueryOptionsExtension>()!.Info;
        info.Should().BeSameAs(builder.Options.FindExtension<ValueObjectQueryOptionsExtension>()!.Info);
        info.IsDatabaseProvider.Should().BeFalse();
        info.LogFragment.Should().Be("using value object queries ");
        info.GetServiceProviderHashCode().Should().Be(0);
        info.ShouldUseSameServiceProvider(new ValueObjectQueryOptionsExtension().Info).Should().BeTrue();
        info.ShouldUseSameServiceProvider(builder.Options.FindExtension<CoreOptionsExtension>()!.Info).Should().BeFalse();
        var debug = new Dictionary<string, string>();
        info.PopulateDebugInfo(debug);
        debug.Should().Equal(new Dictionary<string, string> { ["AdCodicem.ValueObjects:UseValueObjectQueries"] = "1" });

        using var first = new DbContext(builder.Options);
        using var second = new DbContext(new DbContextOptionsBuilder().UseSqlite(_connection).UseValueObjectQueries().Options);
        second.GetService<IRelationalTypeMappingSource>()
            .Should().BeSameAs(first.GetService<IRelationalTypeMappingSource>(), "like options share Entity Framework Core's service provider");
        second.GetService<IEnumerable<IMemberTranslatorPlugin>>().Should().ContainSingle(plugin => plugin is ValueObjectMemberTranslatorPlugin);
    }

    /// <summary>
    /// The context logs, as it starts, that it translates the value of value objects.
    /// </summary>
    [Fact]
    public void A_context_logs_that_it_translates_the_value_of_value_objects()
    {
        var log = new List<string>();
        using var context = new DbContext(new DbContextOptionsBuilder()
            .UseSqlite(_connection)
            .UseValueObjectQueries()
            .LogTo(log.Add, [CoreEventId.ContextInitialized], LogLevel.Debug)
            .Options);

        _ = context.Model;

        log.Should().ContainSingle().Which.Should().Contain("using value object queries");
    }

    /// <summary>
    /// A context started on a service provider of the application's own, to which Entity Framework Core adds no service
    /// of an options extension, is refused, naming both calls, rather than leave <c>.Value</c> untranslated without a
    /// word. One whose provider holds the services of the extension starts, each time, and translates. Options naming
    /// no provider at all get Entity Framework Core's own refusal.
    /// </summary>
    [Fact]
    public async Task A_service_provider_of_the_applications_own_is_refused_unless_it_holds_the_translator()
    {
        await using (var unconfigured = new DbContext(new DbContextOptionsBuilder().UseValueObjectQueries().Options))
        {
            unconfigured.Invoking(c => c.Model).Should().Throw<InvalidOperationException>().WithMessage("No database provider has been configured*");
        }

        await SeedAsync();
        await using (var bare = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider())
        {
            await using var refused = new OwnProviderOrderContext(_connection, bare);
            refused.Invoking(c => c.Model).Should().Throw<InvalidOperationException>()
                .WithMessage("UseValueObjectQueries() cannot translate .Value on this context*UseInternalServiceProvider*ApplyServices*");
        }

        var services = new ServiceCollection().AddEntityFrameworkSqlite();
        new ValueObjectQueryOptionsExtension().ApplyServices(services);
        await using var provider = services.BuildServiceProvider();
        var prefix = "FR";
        for (var start = 0; start < 2; start++)
        {
            await using var context = new OwnProviderOrderContext(_connection, provider);
            var byPrefix = context.Orders.Where(order => order.Iban.Value.StartsWith(prefix)).Select(order => order.Id);
            byPrefix.ToQueryString().Should().Contain("""WHERE "o"."Iban" LIKE @prefix_startswith""");
            (await byPrefix.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);
        }
    }

    /// <summary>
    /// The translator answers nothing for a column whose store type the provider has no mapping of the underlying type
    /// for, which no model holds: Entity Framework Core then refuses the query as it did.
    /// </summary>
    [Fact]
    public void The_translator_answers_nothing_without_a_mapping_of_the_underlying_type_for_the_store_type()
    {
        using var context = Open<OrderContext>();
        var mapping = (RelationalTypeMapping)context.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(string))!
            .WithComposedConverter(new ValueObjectConverter<Iban, string>());
        var translator = new ValueObjectMemberTranslator(
            Substitute.For<IRelationalTypeMappingSource>(), context.GetService<ISqlExpressionFactory>());

        translator.Translate(
                new ColumnExpression("Iban", "o", typeof(Iban), mapping, nullable: false),
                typeof(Iban).GetProperty(nameof(Iban.Value))!,
                typeof(string),
                Substitute.For<IDiagnosticsLogger<DbLoggerCategory.Query>>())
            .Should().BeNull();
    }

    private TContext Open<TContext>()
        where TContext : QueryContext
    {
        var context = (TContext)Activator.CreateInstance(typeof(TContext), _connection)!;
        context.Database.EnsureCreated();

        return context;
    }

    private async Task SeedAsync()
    {
        await using var context = Open<OrderContext>();
        context.Orders.AddRange(
            new Order
            {
                Id = 1,
                Iban = French,
                Previous = Belgian,
                Quantity = Quantity.Create(5),
                Backorder = Quantity.Create(7),
                Total = Amount.Create(12.5m),
                Recorded = RecordedAt.Create(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                Reference = Reference<Order>.Create("po-1"),
                Stock = Catalog<Order>.Stock.Create(3),
                Account = AccountId.New(),
                Link = Site,
                Port = Port.Create(443),
                Ibans = [French, Belgian],
                OptionalIbans = [null, French],
                Quantities = [Quantity.Create(1), Quantity.Create(5)],
                Contact = new Contact { Account = French, Previous = Belgian, Floor = Quantity.Create(3) },
                Address = new Address { Account = French, Previous = Belgian, Floor = Quantity.Create(3) },
            },
            new Order
            {
                Id = 2,
                Iban = Belgian,
                Quantity = Quantity.Create(1),
                Total = Amount.Create(2m),
                Recorded = RecordedAt.Create(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                Reference = Reference<Order>.Create("inv-2"),
                Stock = Catalog<Order>.Stock.Create(0),
                Account = AccountId.New(),
                Port = Port.Create(80),
                Ibans = [Belgian],
                OptionalIbans = [null],
                Quantities = [Quantity.Create(1)],
                Contact = new Contact { Account = Belgian, Floor = Quantity.Create(1) },
                Address = new Address { Account = Belgian, Floor = Quantity.Create(1) },
                ParentId = 1,
            });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>An order, holding value objects in every shape a query reaches the value of.</summary>
    private sealed class Order
    {
        public int Id { get; set; }

        public Iban Iban { get; set; }

        public Iban? Previous { get; set; }

        public Quantity Quantity { get; set; }

        public Quantity? Backorder { get; set; }

        public Amount Total { get; set; }

        public RecordedAt Recorded { get; set; }

        public Reference<Order> Reference { get; set; }

        public Catalog<Order>.Stock Stock { get; set; }

        public AccountId Account { get; set; }

        public HandWrittenLink? Link { get; set; }

        public Port Port { get; set; }

        public List<Iban> Ibans { get; set; } = [];

        public List<Iban?> OptionalIbans { get; set; } = [];

        public List<Quantity> Quantities { get; set; } = [];

        public Contact Contact { get; set; } = new();

        public Address Address { get; set; } = new();

        public string Raw { get; set; } = string.Empty;

        public Order? Parent { get; set; }

        public int? ParentId { get; set; }
    }

    /// <summary>An owned type, mapped to a JSON column.</summary>
    private sealed class Contact
    {
        public Iban Account { get; set; }

        public Iban? Previous { get; set; }

        public Quantity Floor { get; set; }
    }

    /// <summary>A complex type, mapped to a JSON column.</summary>
    private sealed class Address
    {
        public Iban Account { get; set; }

        public Iban? Previous { get; set; }

        public Quantity Floor { get; set; }
    }

    /// <summary>An entity whose value objects the application maps through converters of its own.</summary>
    private sealed class Bespoke
    {
        public int Id { get; set; }

        public Iban Iban { get; set; }

        public Iban Lowered { get; set; }

        public Iban Linked { get; set; }

        public Iban Sealed { get; set; }
    }

    /// <summary>Stores an IBAN as a link, which Entity Framework Core then stores as text.</summary>
    private sealed class IbanToUri() : ValueConverter<Iban, Uri>(
        iban => new Uri("iban:" + iban.Value),
        uri => Iban.Create(uri.AbsolutePath));

    /// <summary>Stores a text sealed, behind a prefix: what the column holds is not the value.</summary>
    private sealed class Sealing() : ValueConverter<string, string>(
        text => "sealed:" + text,
        text => text.Substring("sealed:".Length));

    /// <summary>A context over the connection of a test, mapping the value objects and the identifiers of the unit domain.</summary>
    /// <param name="connection">The database of the test.</param>
    private abstract class QueryContext(SqliteConnection connection) : DbContext
    {
        protected virtual bool Strict => false;

        protected virtual bool TranslatesValues => true;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(connection);
            if (TranslatesValues)
            {
                optionsBuilder.UseValueObjectQueries();
            }
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // The value object written by hand, which nothing registers.
            ValueObjectRegistry.TryResolve(typeof(HandWrittenLink), out _).Should().BeTrue();
            configurationBuilder.ConfigureValueObjects(Strict, typeof(Iban).Assembly);
            configurationBuilder.ConfigureEntityIds(Strict, typeof(AccountId).Assembly);
        }
    }

    /// <summary>Maps the orders, translating the value of their value objects.</summary>
    private class OrderContext(SqliteConnection connection) : QueryContext(connection)
    {
        public DbSet<Order> Orders => Set<Order>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) => MapOrders(modelBuilder);

        internal static void MapOrders(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Order>(order =>
            {
                order.ToTable("Orders");
                order.Property(entity => entity.Id).ValueGeneratedNever();
                order.HasIndex(entity => entity.Iban);
                order.OwnsOne(entity => entity.Contact, contact => contact.ToJson());
                order.ComplexProperty(entity => entity.Address, address => address.ToJson());
                order.HasOne(entity => entity.Parent).WithMany().HasForeignKey(entity => entity.ParentId);
            });
    }

    /// <summary>Maps the orders, validating what it reads.</summary>
    private sealed class StrictOrderContext(SqliteConnection connection) : OrderContext(connection)
    {
        protected override bool Strict => true;
    }

    /// <summary>Maps the orders, without translating the value of their value objects.</summary>
    private sealed class UntranslatedOrderContext(SqliteConnection connection) : OrderContext(connection)
    {
        protected override bool TranslatesValues => false;
    }

    /// <summary>Maps the orders on a service provider of the application's own.</summary>
    /// <param name="connection">The database of the test.</param>
    /// <param name="provider">The service provider the context runs on.</param>
    private sealed class OwnProviderOrderContext(SqliteConnection connection, IServiceProvider provider) : OrderContext(connection)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseInternalServiceProvider(provider);
        }
    }

    /// <summary>Maps the bespoke entities, three of whose IBANs the application converts itself.</summary>
    private sealed class BespokeContext(SqliteConnection connection) : QueryContext(connection)
    {
        public DbSet<Bespoke> Bespokes => Set<Bespoke>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Bespoke>(bespoke =>
            {
                bespoke.Property(entity => entity.Lowered).HasConversion(iban => iban.Value.ToLowerInvariant(), text => Iban.Create(text));
                bespoke.Property(entity => entity.Linked).HasConversion(new IbanToUri());
                bespoke.Property(entity => entity.Sealed).HasConversion(new ValueObjectConverter<Iban, string>().ComposeWith(new Sealing()));
            });
    }

    /// <summary>Maps the orders on PostgreSQL, whose query compilation opens no connection.</summary>
    private sealed class PostgreSqlOrderContext : DbContext
    {
        public DbSet<Order> Orders => Set<Order>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused").UseValueObjectQueries();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            ValueObjectRegistry.TryResolve(typeof(HandWrittenLink), out _).Should().BeTrue();
            configurationBuilder.ConfigureValueObjects(typeof(Iban).Assembly);
            configurationBuilder.ConfigureEntityIds(typeof(AccountId).Assembly);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) => OrderContext.MapOrders(modelBuilder);
    }
}
