using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.TestData;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Sqlite.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// The rules value objects declare, written as CHECK constraints on their columns once
/// <c>AddValueObjectCheckConstraints</c> is called: each only where the database refuses no value the type accepts.
/// </summary>
/// <remarks>
/// SQLite in memory runs the constraints, with no server: each test opens a database of its own. PostgreSQL's are read
/// off a model built on Npgsql, which opens no connection to build one. Entity Framework Core builds a model once per
/// context type and provider, so each model shape, and each set of options, has a context type of its own. SQL Server's
/// constraints, and those of PostgreSQL and SQL Server refusing another writer's rows, are checked in the integration
/// suite.
/// </remarks>
public sealed partial class EntityFrameworkCoreCheckConstraintTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public EntityFrameworkCoreCheckConstraintTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// Without the call, the model holds no check constraint, so that no existing migration changes; with it, the
    /// migration differ adds exactly the constraints the model holds, and drops them back.
    /// </summary>
    [Fact]
    public void Nothing_is_written_unless_asked_and_a_migration_adds_the_constraints_then_drops_them_back()
    {
        using var without = Sqlite<UncheckedLedgerContext>();
        using var with = Sqlite<LedgerContext>();
        var before = RelationalModel(without);
        var after = RelationalModel(with);

        before.Tables.SelectMany(static table => table.CheckConstraints).Should().BeEmpty();
        var expected = after.Tables.Single().CheckConstraints.Select(static check => check.Name).ToList();
        expected.Should().NotBeEmpty();

        var differ = with.GetService<IMigrationsModelDiffer>();
        differ.GetDifferences(before, after).Should().AllBeOfType<AddCheckConstraintOperation>()
            .And.Subject.Cast<AddCheckConstraintOperation>().Select(static add => add.Name).Should().BeEquivalentTo(expected);
        differ.GetDifferences(after, before).Should().AllBeOfType<DropCheckConstraintOperation>()
            .And.Subject.Cast<DropCheckConstraintOperation>().Select(static drop => drop.Name).Should().BeEquivalentTo(expected);
        differ.HasDifferences(before, RelationalModel(without)).Should().BeFalse();
    }

    /// <summary>
    /// SQLite: a minimum length counted in bytes, which a character never takes fewer of than UTF-16 code units; the bounds
    /// of integers, floating-point numbers, characters compared by code point, dates, times and instants with no offset;
    /// closed sets, of text when the type stores no other spelling of a known value, and of decimals, which Entity
    /// Framework Core writes as their canonical text. Left out: the bounds of a decimal, of an unsigned 64-bit integer,
    /// of a duration and of an instant with its offset, which SQLite stores as text, or as a negative integer past
    /// <see cref="long.MaxValue"/>, that does not sort as they do; a closed set of instants with their offset, a closed set
    /// holding a value that is not a number, a closed set looked up ignoring case or as a culture compares, a minimum
    /// length of zero, and the rules of a type mapped by a converter of the application's own.
    /// </summary>
    [Fact]
    public void SQLite_gets_each_rule_it_compares_as_dotnet_does_and_none_of_the_others()
    {
        using var context = Sqlite<SpecimenContext>();

        Checks(context, "specimens").Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["CK_specimens_Account_MinLength"] = """length(CAST("Account" AS BLOB)) >= 25""",
            ["CK_specimens_Adjustment_Maximum"] = "\"Adjustment\" <= 10",
            ["CK_specimens_Adjustment_Minimum"] = "\"Adjustment\" >= -10",
            ["CK_specimens_Code_MinLength"] = """length(CAST("Code" AS BLOB)) >= 5""",
            ["CK_specimens_Country_KnownValues"] = "\"Country\" IN ('FR', 'BE', 'LU')",
            ["CK_specimens_Country_MinLength"] = """length(CAST("Country" AS BLOB)) >= 2""",
            ["CK_specimens_Effective_Minimum"] = "\"Effective\" >= '2000-01-01'",
            ["CK_specimens_Factor_KnownValues"] = "\"Factor\" IN (0.10000000149011612, 0.5)",
            ["CK_specimens_FileSize_Minimum"] = "\"FileSize\" >= 0",
            ["CK_specimens_Fraction_Maximum"] = "\"Fraction\" <= 0.69999998807907104",
            ["CK_specimens_Fraction_Minimum"] = "\"Fraction\" >= 0.10000000149011612",
            ["CK_specimens_Grade_Maximum"] = "\"Grade\" COLLATE BINARY <= 'F'",
            ["CK_specimens_Grade_Minimum"] = "\"Grade\" COLLATE BINARY >= 'A'",
            ["CK_specimens_Iban_MinLength"] = """length(CAST("Iban" AS BLOB)) >= 15""",
            ["CK_specimens_Indent_Maximum"] = "\"Indent\" COLLATE BINARY <= '~'",
            ["CK_specimens_Indent_Minimum"] = "\"Indent\" COLLATE BINARY >= '\t'",
            ["CK_specimens_Initial_Maximum"] = "\"Initial\" COLLATE BINARY <= 'z'",
            ["CK_specimens_Initial_Minimum"] = "\"Initial\" COLLATE BINARY >= ' '",
            ["CK_specimens_Latitude_Maximum"] = "\"Latitude\" <= 90.0",
            ["CK_specimens_Latitude_Minimum"] = "\"Latitude\" >= -90.0",
            ["CK_specimens_Opening_Maximum"] = "\"Opening\" <= '12:00:00'",
            ["CK_specimens_Opening_Minimum"] = "\"Opening\" >= '06:00:00'",
            ["CK_specimens_OpeningInMilliseconds_Maximum"] = "\"OpeningInMilliseconds\" <= '12:00:00'",
            ["CK_specimens_OpeningInMilliseconds_Minimum"] = "\"OpeningInMilliseconds\" >= '06:00:00'",
            ["CK_specimens_Page_Minimum"] = "\"Page\" >= 1",
            ["CK_specimens_Port_Minimum"] = "\"Port\" >= 1",
            ["CK_specimens_Quantity_Maximum"] = "\"Quantity\" <= 1000",
            ["CK_specimens_Quantity_Minimum"] = "\"Quantity\" >= 0",
            ["CK_specimens_Rate_KnownValues"] = "\"Rate\" IN ('0.0', '0.5', '1.0')",
            ["CK_specimens_Ratio_Maximum"] = "\"Ratio\" <= 1.0",
            ["CK_specimens_Ratio_Minimum"] = "\"Ratio\" >= 0.0",
            ["CK_specimens_Recorded_Maximum"] = "\"Recorded\" <= '2099-12-31 00:00:00'",
            ["CK_specimens_Recorded_Minimum"] = "\"Recorded\" >= '2000-01-01 00:00:00'",
            ["CK_specimens_RecordedLocally_Maximum"] = "\"RecordedLocally\" <= '2099-12-31 00:00:00'",
            ["CK_specimens_RecordedLocally_Minimum"] = "\"RecordedLocally\" >= '2000-01-01 00:00:00'",
            ["CK_specimens_Score_Maximum"] = "\"Score\" <= 100",
            ["CK_specimens_Stamp_Minimum"] = "\"Stamp\" >= '2000-01-01 00:00:00.0000001'",
            ["CK_specimens_Stock_Minimum"] = "\"Stock\" >= 0",
            ["CK_specimens_Tenant_KnownValues"] = "\"Tenant\" IN ('6F9619FF-8B86-D011-B42D-00C04FC964FF')",
            ["CK_specimens_Tolerance_Maximum"] = "\"Tolerance\" <= 1.0",
            ["CK_specimens_Unit_KnownValues"] = "\"Unit\" IN ('KG', 'L')",
            ["CK_specimens_Words_Maximum"] = "\"Words\" <= 9",
            ["CK_specimens_Words_Minimum"] = "\"Words\" >= 5",
        });
    }

    /// <summary>
    /// PostgreSQL: a minimum length in bytes, <c>octet_length</c>, which never counts fewer characters than .NET does;
    /// every bound and closed set SQLite leaves out, a decimal, an unsigned 64-bit integer, a duration as an interval and
    /// an instant with its offset as the UTC instant; an instant with no offset as the UTC instant a
    /// <c>timestamp with time zone</c> compares with; floats as the exact double the column holds. Left out: a character's
    /// minimum at or below a space, a tab's say, since <c>character(1)</c> drops a space, which would then compare below
    /// it; a decimal bound finer than the column's scale, which the column would round the application's own write to; an
    /// instant's bound finer than the microsecond the column keeps.
    /// </summary>
    [Fact]
    public void PostgreSQL_gets_each_rule_it_compares_as_dotnet_does_and_none_of_the_others()
    {
        using var context = Npgsql<SpecimenContext>();

        Checks(context, "specimens").Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["CK_specimens_Account_MinLength"] = "octet_length(\"Account\") >= 25",
            ["CK_specimens_Adjustment_Maximum"] = "\"Adjustment\" <= 10",
            ["CK_specimens_Adjustment_Minimum"] = "\"Adjustment\" >= -10",
            ["CK_specimens_Balance_Minimum"] = "\"Balance\" >= 0.0",
            ["CK_specimens_Code_MinLength"] = "octet_length(\"Code\") >= 5",
            ["CK_specimens_Counter_Maximum"] = "\"Counter\" <= 18446744073709551614.0",
            ["CK_specimens_Counter_Minimum"] = "\"Counter\" >= 1.0",
            ["CK_specimens_Country_KnownValues"] = "\"Country\" IN ('FR', 'BE', 'LU')",
            ["CK_specimens_Country_MinLength"] = "octet_length(\"Country\") >= 2",
            ["CK_specimens_Deadline_Maximum"] = "\"Deadline\" <= TIMESTAMPTZ '2030-01-01T05:00:00+00:00'",
            ["CK_specimens_Deadline_Minimum"] = "\"Deadline\" >= TIMESTAMPTZ '1999-12-31T22:00:00+00:00'",
            ["CK_specimens_Duration_Maximum"] = "\"Duration\" <= INTERVAL '1 00:00:00'",
            ["CK_specimens_Duration_Minimum"] = "\"Duration\" >= INTERVAL '00:00:00'",
            ["CK_specimens_Effective_Minimum"] = "\"Effective\" >= DATE '2000-01-01'",
            ["CK_specimens_Factor_KnownValues"] = "\"Factor\" IN (0.10000000149011612, 0.5)",
            ["CK_specimens_Fee_Maximum"] = "\"Fee\" <= 9.995",
            ["CK_specimens_Fee_Minimum"] = "\"Fee\" >= 0.001",
            ["CK_specimens_FileSize_Minimum"] = "\"FileSize\" >= 0",
            ["CK_specimens_Fraction_Maximum"] = "\"Fraction\" <= 0.69999998807907104",
            ["CK_specimens_Fraction_Minimum"] = "\"Fraction\" >= 0.10000000149011612",
            ["CK_specimens_Grade_Maximum"] = "\"Grade\" COLLATE \"C\" <= 'F'",
            ["CK_specimens_Grade_Minimum"] = "\"Grade\" COLLATE \"C\" >= 'A'",
            ["CK_specimens_Iban_MinLength"] = "octet_length(\"Iban\") >= 15",
            ["CK_specimens_Indent_Maximum"] = "\"Indent\" COLLATE \"C\" <= '~'",
            ["CK_specimens_Initial_Maximum"] = "\"Initial\" COLLATE \"C\" <= 'z'",
            ["CK_specimens_Latitude_Maximum"] = "\"Latitude\" <= 90.0",
            ["CK_specimens_Latitude_Minimum"] = "\"Latitude\" >= -90.0",
            ["CK_specimens_Milestone_KnownValues"] = "\"Milestone\" IN (TIMESTAMPTZ '2020-01-01T00:00:00+00:00')",
            ["CK_specimens_Occurred_Minimum"] = "\"Occurred\" >= TIMESTAMPTZ '2000-01-01T00:00:00+00:00'",
            ["CK_specimens_Opening_Maximum"] = "\"Opening\" <= TIME '12:00:00'",
            ["CK_specimens_Opening_Minimum"] = "\"Opening\" >= TIME '06:00:00'",
            ["CK_specimens_OpeningInMilliseconds_Maximum"] = "\"OpeningInMilliseconds\" <= TIME '12:00:00'",
            ["CK_specimens_OpeningInMilliseconds_Minimum"] = "\"OpeningInMilliseconds\" >= TIME '06:00:00'",
            ["CK_specimens_Page_Minimum"] = "\"Page\" >= 1",
            ["CK_specimens_Port_Minimum"] = "\"Port\" >= 1",
            ["CK_specimens_Quantity_Maximum"] = "\"Quantity\" <= 1000",
            ["CK_specimens_Quantity_Minimum"] = "\"Quantity\" >= 0",
            ["CK_specimens_Rate_KnownValues"] = "\"Rate\" IN (0.0, 0.5, 1.0)",
            ["CK_specimens_Ratio_Maximum"] = "\"Ratio\" <= 1.0",
            ["CK_specimens_Ratio_Minimum"] = "\"Ratio\" >= 0.0",
            ["CK_specimens_Recorded_Maximum"] = "\"Recorded\" <= TIMESTAMPTZ '2099-12-31T00:00:00Z'",
            ["CK_specimens_Recorded_Minimum"] = "\"Recorded\" >= TIMESTAMPTZ '2000-01-01T00:00:00Z'",
            ["CK_specimens_RecordedLocally_Maximum"] = "\"RecordedLocally\" <= TIMESTAMP '2099-12-31T00:00:00'",
            ["CK_specimens_RecordedLocally_Minimum"] = "\"RecordedLocally\" >= TIMESTAMP '2000-01-01T00:00:00'",
            ["CK_specimens_Score_Maximum"] = "\"Score\" <= 100",
            ["CK_specimens_Share_Maximum"] = "\"Share\" <= 100.0",
            ["CK_specimens_Share_Minimum"] = "\"Share\" >= 0.0",
            ["CK_specimens_Stock_Minimum"] = "\"Stock\" >= 0",
            ["CK_specimens_Tenant_KnownValues"] = "\"Tenant\" IN ('6f9619ff-8b86-d011-b42d-00c04fc964ff')",
            ["CK_specimens_Tolerance_Maximum"] = "\"Tolerance\" <= 1.0",
            ["CK_specimens_Unit_KnownValues"] = "\"Unit\" IN ('KG', 'L')",
            ["CK_specimens_Window_Maximum"] = "\"Window\" <= INTERVAL '2 00:00:00'",
            ["CK_specimens_Window_Minimum"] = "\"Window\" >= INTERVAL '-02:00:00'",
            ["CK_specimens_Words_Maximum"] = "\"Words\" <= 9",
            ["CK_specimens_Words_Minimum"] = "\"Words\" >= 5",
        });
    }

    /// <summary>
    /// On SQLite, every value each rule accepts is written, at its boundaries: a text at its minimum length holding a
    /// character outside the Basic Multilingual Plane, which .NET counts as two code units, or ending with a space, a known
    /// value spelt another way that the type normalizes, a decimal closed set's zero written negative. Another writer's
    /// row breaking a rule is refused, the constraint named.
    /// </summary>
    [Fact]
    public async Task SQLite_runs_the_constraints_accepting_every_value_the_types_accept_and_refusing_another_writers_row()
    {
        await using (var write = Sqlite<LedgerContext>())
        {
            write.Database.EnsureCreated();
            write.Ledgers.AddRange(
                new Ledger
                {
                    Id = 1,
                    Iban = Iban.Create("BE68539007547034"),
                    Quantity = Quantity.Create(0),
                    Backorder = Quantity.Create(1000),
                    Country = CountryCode.Create(" lu "),
                    Code = Code.Create("abc\U0001F600"),
                    Rate = Rate.Create(-0.0m),
                },
                new Ledger
                {
                    Id = 2,
                    Iban = Iban.Create("FR7630006000011234567890189"),
                    Quantity = Quantity.Create(1000),
                    Backorder = null,
                    Country = CountryCode.France,
                    Code = Code.Create("abcd "),
                    Rate = Rate.Create(1.00m),
                });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await RefusedAsync(Row(iban: "'FR76300060000'"), "CK_ledgers_Iban_MinLength");
        await RefusedAsync(Row(quantity: "-1"), "CK_ledgers_Quantity_Minimum");
        await RefusedAsync(Row(backorder: "1001"), "CK_ledgers_Backorder_Maximum");
        await RefusedAsync(Row(country: "'GB'"), "CK_ledgers_Country_KnownValues");
        await RefusedAsync(Row(country: "'fr'"), "CK_ledgers_Country_KnownValues");
        await RefusedAsync(Row(code: "'abcd'"), "CK_ledgers_Code_MinLength");
        await RefusedAsync(Row(rate: "'0.25'"), "CK_ledgers_Rate_KnownValues");

        // Each rule accepts the row the refusals above start from.
        await using (var command = _connection.CreateCommand())
        {
            command.CommandText = Row();
            (await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        }

        await using var read = Sqlite<LedgerContext>();
        var stored = await read.Ledgers.OrderBy(static ledger => ledger.Id).ToListAsync(TestContext.Current.CancellationToken);
        stored.Select(static ledger => ledger.Code.Value).Should().Equal("abc\U0001F600", "abcd ", "abcde");
        stored.Select(static ledger => ledger.Country).Should().Equal(CountryCode.Luxembourg, CountryCode.France, CountryCode.France);

        static string Row(
            string iban = "'FR7630006000011234567890189'",
            string quantity = "1",
            string backorder = "NULL",
            string country = "'FR'",
            string code = "'abcde'",
            string rate = "'0.5'")
            => $"""
                INSERT INTO "ledgers" ("Id", "Iban", "Quantity", "Backorder", "Country", "Code", "Rate")
                VALUES (3, {iban}, {quantity}, {backorder}, {country}, {code}, {rate})
                """;
    }

    /// <summary>
    /// A column that takes a <c>NULL</c> wraps each of its checks, so that a <c>NULL</c> passes them: an optional value
    /// object's, and a required one's in a table it shares with other types of its hierarchy, which leave it
    /// <c>NULL</c>.
    /// </summary>
    [Fact]
    public void A_column_that_takes_a_NULL_wraps_its_checks()
    {
        using var context = Npgsql<HierarchyContext>();

        Checks(context, "ledgers").Should().Contain(
            "CK_ledgers_Backorder_Maximum", "\"Backorder\" IS NULL OR (\"Backorder\" <= 1000)");
        var parties = Checks(context, "parties");
        parties.Should().Contain("CK_parties_Staff_Minimum", "\"Staff\" IS NULL OR (\"Staff\" >= 0)");
        parties.Should().Contain("CK_parties_Legs_Minimum", "\"Legs\" >= 0");
    }

    /// <summary>
    /// <see cref="ValueObjectCheckOptions.Rules"/> chooses the families written: bounds alone, or none.
    /// </summary>
    [Fact]
    public void The_options_choose_the_rules_written()
    {
        using var bounds = Npgsql<BoundsOnlyContext>();
        using var none = Npgsql<NoRuleContext>();

        Checks(bounds, "ledgers").Keys.Should().BeEquivalentTo(
            "CK_ledgers_Quantity_Minimum", "CK_ledgers_Quantity_Maximum", "CK_ledgers_Backorder_Minimum", "CK_ledgers_Backorder_Maximum");
        RelationalModel(none).Tables.SelectMany(static table => table.CheckConstraints).Should().BeEmpty();
        new ValueObjectCheckOptions().Rules.Should().Be(ValueObjectChecks.Lengths | ValueObjectChecks.Bounds | ValueObjectChecks.KnownValues);
    }

    /// <summary>
    /// A second call replaces the first, as a context deriving from another calls it: its options alone apply, each
    /// constraint is written once, under its plain name, and a dialect of patterns the first call named neither writes a
    /// pattern nor fails the model on one it would read apart.
    /// </summary>
    [Fact]
    public void A_second_call_replaces_the_options_of_the_first()
    {
        using var narrowed = Npgsql<NarrowedLedgerContext>();
        using var unpatterned = Sqlite<UnpatternedEmailContext>();

        Checks(narrowed, "ledgers").Keys.Should().BeEquivalentTo(
            "CK_ledgers_Quantity_Minimum", "CK_ledgers_Quantity_Maximum", "CK_ledgers_Backorder_Minimum", "CK_ledgers_Backorder_Maximum");
        Checks(unpatterned, "mailed").Keys.Should().BeEquivalentTo("CK_mailed_Iban_MinLength");
    }

    /// <summary>
    /// The columns of each table are constrained under its own name: those of a type of a table-per-hierarchy in the
    /// root's table, those of a table-per-type hierarchy in each type's, those an owned type or a complex type shares a
    /// table with, nested ones named by their column's path, and those of an owned type in a table of its own. A type of
    /// a table-per-concrete-type hierarchy with no derived type constrains the columns it inherits; one other types derive
    /// from gets none, since Entity Framework Core would copy its constraints into their tables under the same name.
    /// What lives in a JSON document or an array gets none, and neither does a view nor a property split into another
    /// table.
    /// </summary>
    [Fact]
    public void Each_table_constrains_its_own_columns_whatever_maps_them_there()
    {
        using var context = Npgsql<ShapesContext>();
        var model = RelationalModel(context);

        Names(model, "animals").Should().BeEquivalentTo(
            "CK_animals_Legs_Minimum", "CK_animals_Legs_Maximum", "CK_animals_Chip_MinLength", "CK_animals_Lives_KnownValues");
        Names(model, "fruits").Should().BeEquivalentTo("CK_fruits_Seeds_Minimum", "CK_fruits_Seeds_Maximum");
        Names(model, "apples").Should().BeEquivalentTo("CK_apples_Variety_MinLength");
        Names(model, "saws").Should().BeEquivalentTo(
            "CK_saws_Weight_Minimum", "CK_saws_Weight_Maximum", "CK_saws_Teeth_Minimum");
        Names(model, "hammers").Should().BeEmpty("other tools derive from a hammer");
        Names(model, "sledges").Should().BeEquivalentTo(
            "CK_sledges_Weight_Minimum", "CK_sledges_Weight_Maximum", "CK_sledges_Head_MinLength", "CK_sledges_Swing_Maximum");
        Names(model, "orders").Should().BeEquivalentTo(
            "CK_orders_Contact_Account_MinLength",
            "CK_orders_Contact_Floor_Minimum",
            "CK_orders_Contact_Floor_Maximum",
            "CK_orders_Shipping_Country_KnownValues",
            "CK_orders_Shipping_Country_MinLength",
            "CK_orders_Shipping_City_Zip_MinLength");
        Names(model, "notes").Should().BeEquivalentTo("CK_notes_Code_MinLength");
        Names(model, "visits").Should().BeEquivalentTo("CK_visits_Code_MinLength");
        Names(model, "visit_details").Should().BeEmpty("a property split into another table gets none");
        model.Views.Should().ContainSingle().Which.Name.Should().Be("customer_view");
    }

    /// <summary>
    /// A column several properties share is constrained once, when the package maps each of them through the same value
    /// object, a key and the key an owned type shares it through included; not when two types of a hierarchy store two
    /// value objects there, or a value object and another type, since the constraint of one would refuse the rows of the
    /// other.
    /// </summary>
    [Fact]
    public void A_column_properties_share_is_constrained_only_when_they_hold_the_same_value_object()
    {
        using var context = Npgsql<SharedColumnContext>();

        Checks(context, "pets").Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["CK_pets_Chip_MinLength"] = "\"Chip\" IS NULL OR (octet_length(\"Chip\") >= 5)",
        });
        Checks(context, "accounts").Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["CK_accounts_Id_MinLength"] = "octet_length(\"Id\") >= 25",
            ["CK_accounts_Holder_Code_MinLength"] = "octet_length(\"Holder_Code\") >= 5",
        });
    }

    /// <summary>
    /// <see cref="ValueObjectCheckOptions.Patterns"/> naming PostgreSQL writes each pattern its <c>~</c> reads as .NET
    /// does, under the C collation, its final <c>$</c> written <c>\n?$</c>: a generated hook's, one implemented
    /// explicitly, one whose regular expression sets the options its text keeps its meaning under, explicit captures, no
    /// backtracking and compiled, and a pattern a value object written by hand states in its schema alone, translated
    /// from its text alone. A fixed-length column, an
    /// entity identifier's <c>character(n)</c>, gets none, since PostgreSQL drops the spaces that pad it. Its other rules
    /// are written as before, and with no rule at all the patterns alone.
    /// </summary>
    [Fact]
    public void PostgreSQL_gets_each_pattern_in_its_own_dialect_when_the_options_name_it()
    {
        using var context = Npgsql<PostgreSqlPatternContext>();
        using var patternsOnly = Npgsql<PatternOnlyContext>();

        var checks = Checks(context, "patterned");
        checks.Should().Contain(new Dictionary<string, string>
        {
            ["CK_patterned_Iban_Pattern"] = "\"Iban\" COLLATE \"C\" ~ '^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}\\n?$'",
            ["CK_patterned_Phone_Pattern"] = "\"Phone\" IS NULL OR (\"Phone\" COLLATE \"C\" ~ '^\\+[0-9]{6,15}\\n?$')",
            ["CK_patterned_Currency_Pattern"] = "\"Currency\" COLLATE \"C\" ~ '^[A-Z]{3}\\n?$'",
            ["CK_patterned_Lowered_Pattern"] = "\"Lowered\" COLLATE \"C\" ~ '^[a-z]{3}\\n?$'",
            ["CK_patterned_Portable_Pattern"] = "\"Portable\" COLLATE \"C\" ~ '^[a-z]{3}\\n?$'",
            ["CK_patterned_Stated_Pattern"] = "\"Stated\" COLLATE \"C\" ~ '^[0-9]{4}\\n?$'",
            ["CK_patterned_Iban_MinLength"] = "octet_length(\"Iban\") >= 15",
        });
        checks.Keys.Should().NotContain("CK_patterned_Account_Pattern", "a fixed-length column gets no pattern");
        checks.Should().ContainKey("CK_patterned_Account_MinLength");
        Checks(patternsOnly, "patterned").Keys.Should().OnlyContain(static name => name.EndsWith("_Pattern", StringComparison.Ordinal))
            .And.HaveCount(6);
    }

    /// <summary>
    /// A dialect another provider than the one at hand runs writes no pattern: PostgreSQL's under SQLite, as in tests,
    /// and SQL Server 2025's under Npgsql. The other rules are written as before.
    /// </summary>
    [Fact]
    public void A_dialect_the_provider_does_not_run_writes_no_pattern()
    {
        using var sqlite = Sqlite<PostgreSqlPatternContext>();
        using var postgres = Npgsql<SqlServerPatternContext>();

        Checks(sqlite, "patterned").Should().NotContainKey("CK_patterned_Iban_Pattern").And.ContainKey("CK_patterned_Iban_MinLength");
        Checks(postgres, "patterned").Keys.Should().NotContain(static name => name.EndsWith("_Pattern", StringComparison.Ordinal))
            .And.Contain("CK_patterned_Iban_MinLength");
    }

    /// <summary>
    /// Naming a dialect checks every pattern of a column the convention constrains, under SQLite as under the database it
    /// names: an email address's negated class, which .NET matches over one UTF-16 code unit, fails the model, naming the
    /// value object, the construct and the dialect, and the remedies, <c>ExcludePattern</c> among them.
    /// </summary>
    [Fact]
    public void A_pattern_the_dialect_reads_apart_fails_the_model_naming_the_value_object_the_construct_and_the_remedy()
    {
        using var context = Sqlite<EmailPatternContext>();

        var build = () => context.Model;

        build.Should().Throw<InvalidOperationException>().WithMessage(
            "AddValueObjectCheckConstraints cannot write the pattern of EmailAddress for PostgreSql: it holds a negated class, "
            + "[^…], which .NET matches over one UTF-16 code unit and the database over a whole character: list the characters it "
            + "accepts. Write the pattern within what .NET and the database read alike, leave it out with "
            + "ExcludePattern<EmailAddress>(), or leave Patterns at None.");
    }

    /// <summary>
    /// A hook whose regular expression sets an option its text cannot carry fails the model, whatever the provider at
    /// hand: <c>IgnoreCase</c>, implemented explicitly, with which the text alone would refuse a value the type accepts,
    /// and <c>ECMAScript</c>, which VO0025 does not report.
    /// </summary>
    [Fact]
    public void A_pattern_matched_under_an_option_its_text_cannot_carry_fails_the_model()
    {
        using var context = Npgsql<CaseFreePatternContext>();
        using var ecma = Sqlite<EcmaPatternContext>();

        var build = () => context.Model;
        var buildEcma = () => ecma.Model;

        build.Should().Throw<InvalidOperationException>().WithMessage(
            "AddValueObjectCheckConstraints cannot write the pattern of ShapedCode<EntityFrameworkCoreCheckConstraintTests.CaseFreeShape> "
            + "for SqlServer2025: its regular expression sets IgnoreCase, which the text of the pattern does not carry, and the "
            + "database would read the text as .NET reads it with no option. Write the pattern so that it means the same without "
            + "them, leave it out with ExcludePattern<ShapedCode<EntityFrameworkCoreCheckConstraintTests.CaseFreeShape>>(), "
            + "or leave Patterns at None.");
        buildEcma.Should().Throw<InvalidOperationException>().WithMessage("*for PostgreSql: its regular expression sets ECMAScript, *");
    }

    /// <summary>
    /// <c>ExcludePattern</c> leaves one value object's pattern out, written by no constraint and checked by nothing, its
    /// other rules written as before; the other patterns are still written.
    /// </summary>
    [Fact]
    public void An_excluded_pattern_is_neither_written_nor_checked()
    {
        using var context = Npgsql<ExcludedPatternContext>();

        var checks = Checks(context, "mailed");
        checks.Keys.Should().NotContain(static name => name.Contains("Email", StringComparison.Ordinal));
        checks.Keys.Should().BeEquivalentTo("CK_mailed_Iban_MinLength", "CK_mailed_Iban_Pattern", "CK_mailed_Shouted_MinLength");
        new ValueObjectCheckOptions().ExcludePattern<EmailAddress>().Should().NotBeNull();
    }

    /// <summary>
    /// A dialect <see cref="ValueObjectPatternDialect"/> does not define is refused when the conventions are configured,
    /// and another provider gets no constraint, and so no check of its patterns.
    /// </summary>
    [Fact]
    public void An_undefined_dialect_is_refused_and_another_provider_checks_no_pattern()
    {
        using var undefined = Npgsql<UndefinedDialectContext>();
        using var renamed = Create<EmailPatternContext>(builder => builder
            .UseSqlite(_connection)
            .ReplaceService<IDatabaseProvider, RenamedProvider>());

        var build = () => undefined.Model;

        build.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("configure");
        RelationalModel(renamed).Tables.SelectMany(static table => table.CheckConstraints).Should().BeEmpty();
    }

    /// <summary>
    /// A name longer than the provider takes, 63 bytes on PostgreSQL, keeps its start and ends with the rule, whole, and
    /// eight hexadecimal digits of the hash of its table and its column, so that two names alike at their start stay
    /// apart and each still says its rule; SQLite takes them whole.
    /// </summary>
    [Fact]
    public void A_name_the_provider_would_truncate_keeps_its_start_and_its_rule_and_ends_with_a_hash()
    {
        using var postgres = Npgsql<LongNameContext>();
        using var sqlite = Sqlite<LongNameContext>();

        var truncated = RelationalModel(postgres).Tables.Single().CheckConstraints.Select(static check => check.Name!).ToList();
        truncated.Should().HaveCount(4).And.OnlyHaveUniqueItems();
        truncated.Should().AllSatisfy(static name =>
        {
            System.Text.Encoding.UTF8.GetByteCount(name).Should().Be(63);
            name.Should().StartWith($"CK_{LongTable}_AQuantityWhose");
            name.Should().MatchRegex("_(Minimum|Maximum)_[0-9A-F]{8}$");
        });
        truncated.Select(static name => name[^8..]).Distinct().Should().HaveCount(2, "the rules of a column share the hash of its table and its column");
        RelationalModel(sqlite).Tables.Single().CheckConstraints.Select(static check => check.Name).Should().BeEquivalentTo(
            $"CK_{LongTable}_{nameof(LongNamed.AQuantityWhoseNameKeepsGoingPastWhatPostgreSqlTakesAsAnIdentifierOne)}_Minimum",
            $"CK_{LongTable}_{nameof(LongNamed.AQuantityWhoseNameKeepsGoingPastWhatPostgreSqlTakesAsAnIdentifierOne)}_Maximum",
            $"CK_{LongTable}_{nameof(LongNamed.AQuantityWhoseNameKeepsGoingPastWhatPostgreSqlTakesAsAnIdentifierTwo)}_Minimum",
            $"CK_{LongTable}_{nameof(LongNamed.AQuantityWhoseNameKeepsGoingPastWhatPostgreSqlTakesAsAnIdentifierTwo)}_Maximum");
    }

    /// <summary>
    /// The budget of a shortened name is counted in UTF-8 bytes, the rule and the hash kept whole, and a character outside
    /// the Basic Multilingual Plane is never split from its other half. The hash reads the table and the column apart,
    /// so that two that read alike once joined, <c>a_b</c> and <c>c</c>, <c>a</c> and <c>b_c</c>, stay apart.
    /// </summary>
    [Fact]
    public void A_shortened_name_is_counted_in_bytes_keeps_its_rule_and_never_splits_a_surrogate_pair()
    {
        ValueObjectCheckConstraintConvention.Name("t", "c", "Minimum", 14, distinct: false).Should().Be("CK_t_c_Minimum");
        var distinct = ValueObjectCheckConstraintConvention.Name("t", "c", "Minimum", 128, distinct: true);
        distinct.Should().MatchRegex("^CK_t_c_Minimum_[0-9A-F]{8}$");
        ValueObjectCheckConstraintConvention.Name("t", "c", "Maximum", 128, distinct: true).Should().Be($"CK_t_c_Maximum_{distinct[^8..]}");
        ValueObjectCheckConstraintConvention.Name("a_b", "c", "Maximum", 128, distinct: true)
            .Should().NotBe(ValueObjectCheckConstraintConvention.Name("a", "b_c", "Maximum", 128, distinct: true))
            .And.StartWith("CK_a_b_c_Maximum_");

        // Thirteen bytes are left for the start, "CK_" and five accented letters of two bytes each.
        var accented = ValueObjectCheckConstraintConvention.Name("éééééééééé", "c", "Minimum", 30, distinct: false);
        accented.Should().MatchRegex("^CK_é{5}_Minimum_[0-9A-F]{8}$");
        System.Text.Encoding.UTF8.GetByteCount(accented).Should().Be(30);

        // Seven bytes are left for the start: "CK_" and the emoji, four bytes. Six would fit "CK_" and the first half of
        // the emoji, which UTF-8 counts as three bytes alone, and it is cut before the emoji all the same.
        ValueObjectCheckConstraintConvention.Name("\U0001F600", "c", "Minimum", 26, distinct: true).Should().MatchRegex("^CK_\U0001F600_c_Minimum_[0-9A-F]{8}$");
        ValueObjectCheckConstraintConvention.Name("\U0001F600", "c", "Minimum", 24, distinct: true).Should().MatchRegex("^CK_\U0001F600_Minimum_[0-9A-F]{8}$");
        ValueObjectCheckConstraintConvention.Name("\U0001F600", "c", "Minimum", 23, distinct: true).Should().MatchRegex("^CK__Minimum_[0-9A-F]{8}$");

        // A budget shorter than the rule and the hash keeps them, and nothing of the start.
        ValueObjectCheckConstraintConvention.Name("t", "c", "Minimum", 9, distinct: false).Should().MatchRegex("^_Minimum_[0-9A-F]{8}$");
    }

    /// <summary>
    /// PostgreSQL and SQLite keep a constraint name per table, so two tables whose names read alike once joined with a
    /// column's, <c>a_b</c> with <c>C</c> and <c>a</c> with <c>B_C</c>, or a complex property's column and a table named
    /// after it, keep the plain names; a name another check constraint of the same table already holds, one the
    /// application declared, ends with the hash. SQL Server, which keeps them per schema, is checked in the integration
    /// suite.
    /// </summary>
    [Fact]
    public void A_name_is_kept_apart_from_the_names_its_table_already_holds()
    {
        using var context = Npgsql<ClashContext>();
        var model = RelationalModel(context);

        Names(model, "clash_a_b").Should().BeEquivalentTo("CK_clash_a_b_C_Maximum");
        Names(model, "clash_a").Should().BeEquivalentTo("CK_clash_a_B_C_Maximum");
        Names(model, "clash_orders").Should().BeEquivalentTo("CK_clash_orders_Line_Score_Maximum");
        Names(model, "clash_orders_Line").Should().BeEquivalentTo("CK_clash_orders_Line_Score_Maximum");
        var declared = Checks(context, "clash_declared");
        declared.Should().HaveCount(2).And.Contain("CK_clash_declared_Score_Maximum", "\"Score\" <> 13");
        declared.Keys.Should().ContainSingle(static name => name.StartsWith("CK_clash_declared_Score_Maximum_", StringComparison.Ordinal))
            .Which.Should().MatchRegex("_Maximum_[0-9A-F]{8}$");
    }

    /// <summary>
    /// A closed set of text whose type stores another spelling of a known value gets no <c>IN</c>, which would refuse the
    /// application's own write: a set looked up with <c>OrdinalIgnoreCase</c> and normalized to lower case, which stores
    /// the micro sign <c>µg</c> for its Greek <c>μg</c> and <c>ςa</c> for <c>σa</c>, since the two share their upper case
    /// and no lower case reaches the other; one with no normalizer, which stores <c>kg</c> for <c>KG</c>, or the
    /// lower or the upper case of a letter outside the Basic Multilingual Plane. SQLite writes each of them.
    /// </summary>
    [Fact]
    public async Task A_closed_set_whose_type_stores_another_spelling_of_a_known_value_gets_no_IN()
    {
        using (var postgres = Npgsql<SpellingContext>())
        {
            Checks(postgres, "spellings").Should().BeEmpty();
        }

        await using (var write = Sqlite<SpellingContext>())
        {
            Checks(write, "spellings").Should().BeEmpty();
            write.Database.EnsureCreated();
            write.Spellings.AddRange(
                new Spelling { Id = 1, Phase = Phase.Create("\u00B5g"), Weight = Weight.Create("kg"), Letter = Letter.Create("\U00010428"), LowerLetter = LowerLetter.Create("\U00010400") },
                new Spelling { Id = 2, Phase = Phase.Create("\u03C2a"), Weight = Weight.Create("KG"), Letter = Letter.Create("\U00010400"), LowerLetter = LowerLetter.Create("\U00010428") });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = Sqlite<SpellingContext>();
        var stored = await read.Spellings.OrderBy(static spelling => spelling.Id).ToListAsync(TestContext.Current.CancellationToken);
        stored.Select(static spelling => spelling.Phase.Value).Should().Equal("\u00B5g", "\u03C2a");
        stored.Select(static spelling => spelling.Weight.Value).Should().Equal("kg", "KG");
        stored.Select(static spelling => spelling.Letter.Value).Should().Equal("\U00010428", "\U00010400");
    }

    /// <summary>
    /// In a table-per-concrete-type hierarchy, a type with no derived type constrains the complex property it declares,
    /// and none it inherits, which Entity Framework Core 10 maps to no column of its table: a constraint on it would fail
    /// the table's creation.
    /// </summary>
    [Fact]
    public void A_complex_property_a_table_per_concrete_type_inherits_gets_no_constraint()
    {
        using var context = Sqlite<VesselContext>();

        RelationalModel(context).FindTable("ferries", null)!.Columns.Select(static column => column.Name).Should().NotContain("Zip");
        Names(RelationalModel(context), "ferries").Should().BeEquivalentTo(
            "CK_ferries_Cars_Minimum", "CK_ferries_Cars_Maximum", "CK_ferries_Berth_Zip_MinLength");
        context.Database.EnsureCreated().Should().BeTrue();
    }

    /// <summary>
    /// The script of each provider creates the constraints with the table, as a migration does: SQLite's and PostgreSQL's
    /// here, SQL Server's in the integration suite.
    /// </summary>
    [Fact]
    public void The_create_script_of_each_provider_holds_the_constraints()
    {
        using var sqlite = Sqlite<LedgerContext>();
        using var postgres = Npgsql<LedgerContext>();

        sqlite.Database.GenerateCreateScript().Should()
            .Contain("CONSTRAINT \"CK_ledgers_Quantity_Minimum\" CHECK (\"Quantity\" >= 0)")
            .And.Contain("CONSTRAINT \"CK_ledgers_Backorder_Maximum\" CHECK (\"Backorder\" IS NULL OR (\"Backorder\" <= 1000))")
            .And.Contain("CONSTRAINT \"CK_ledgers_Iban_MinLength\" CHECK (length(CAST(\"Iban\" AS BLOB)) >= 15)");
        postgres.Database.GenerateCreateScript().Should()
            .Contain("CONSTRAINT \"CK_ledgers_Quantity_Minimum\" CHECK (\"Quantity\" >= 0)")
            .And.Contain("CONSTRAINT \"CK_ledgers_Country_KnownValues\" CHECK (\"Country\" IN ('FR', 'BE', 'LU'))")
            .And.Contain("CONSTRAINT \"CK_ledgers_Iban_MinLength\" CHECK (octet_length(\"Iban\") >= 15)");
    }

    /// <summary>
    /// On SQLite, each bound and closed set it writes takes every value the type accepts, one row at a time: each bound,
    /// a tick or a step inside it, characters compared by code point from a tab or a space on, floats at their nearest
    /// doubles, instants and times at the hundred nanoseconds SQLite's text keeps, and the known values of each closed
    /// set, a decimal at another scale, a GUID and a float included.
    /// </summary>
    [Fact]
    public async Task SQLite_takes_every_value_at_the_boundaries_of_the_rules_it_writes()
    {
        await using (var create = Sqlite<BoundaryContext>())
        {
            create.Database.EnsureCreated();
            Checks(create, "boundaries").Should().HaveCount(35);
        }

        var refused = new List<string>();
        var id = 0;
        foreach (var (label, set) in Boundaries())
        {
            var row = new Boundary { Id = ++id };
            set(row);
            await using var write = Sqlite<BoundaryContext>();
            write.Add(row);
            try
            {
                await write.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
            catch (DbUpdateException exception)
            {
                refused.Add($"{label}: {exception.InnerException?.Message}");
            }
        }

        refused.Should().BeEmpty();
        id.Should().BeGreaterThan(50);

        static IEnumerable<(string Label, Action<Boundary> Set)> Boundaries()
        {
            yield return ("Grade A", static row => row.Grade = Grade.Create('A'));
            yield return ("Grade F", static row => row.Grade = Grade.Create('F'));
            yield return ("Grade f", static row => row.Grade = Grade.Create('f'));
            yield return ("Initial space", static row => row.Initial = Initial.Create(' '));
            yield return ("Initial z", static row => row.Initial = Initial.Create('z'));
            yield return ("Indent tab", static row => row.Indent = Indent.Create('\t'));
            yield return ("Indent space", static row => row.Indent = Indent.Create(' '));
            yield return ("Indent tilde", static row => row.Indent = Indent.Create('~'));
            yield return ("Adjustment -10", static row => row.Adjustment = Adjustment.Create(-10));
            yield return ("Adjustment 10", static row => row.Adjustment = Adjustment.Create(10));
            yield return ("Score 100", static row => row.Score = Score.Create(100));
            yield return ("Port 1", static row => row.Port = Port.Create(1));
            yield return ("Page 1", static row => row.Page = PageNumber.Create(1));
            yield return ("FileSize 0", static row => row.FileSize = FileSize.Create(0));
            yield return ("FileSize max", static row => row.FileSize = FileSize.Create(long.MaxValue));
            yield return ("Quantity 0", static row => row.Quantity = Quantity.Create(0));
            yield return ("Quantity 1000", static row => row.Quantity = Quantity.Create(1000));
            yield return ("Counter 1", static row => row.Counter = Counter.Create(1));
            yield return ("Counter max - 1", static row => row.Counter = Counter.Create(ulong.MaxValue - 1));
            yield return ("Latitude -90", static row => row.Latitude = Latitude.Create(-90));
            yield return ("Latitude 90", static row => row.Latitude = Latitude.Create(90));
            yield return ("Tolerance 1", static row => row.Tolerance = Tolerance.Create(1));
            yield return ("Ratio 0", static row => row.Ratio = Ratio.Create(0f));
            yield return ("Ratio 1", static row => row.Ratio = Ratio.Create(1f));
            yield return ("Fraction 0.1f", static row => row.Fraction = Fraction.Create(0.1f));
            yield return ("Fraction 0.7f", static row => row.Fraction = Fraction.Create(0.7f));
            yield return ("Effective 2000-01-01", static row => row.Effective = EffectiveDate.Create(new DateOnly(2000, 1, 1)));
            yield return ("Opening 06:00", static row => row.Opening = OpeningTime.Create(new TimeOnly(6, 0)));
            yield return ("Opening 06:00 and a tick", static row => row.Opening = OpeningTime.Create(new TimeOnly(6, 0).Add(TimeSpan.FromTicks(1))));
            yield return ("Opening 12:00 less a tick", static row => row.Opening = OpeningTime.Create(new TimeOnly(12, 0).Add(TimeSpan.FromTicks(-1))));
            yield return ("Opening 12:00", static row => row.Opening = OpeningTime.Create(new TimeOnly(12, 0)));
            yield return ("Recorded 2000-01-01", static row => row.Recorded = RecordedAt.Create(new DateTime(2000, 1, 1)));
            yield return ("Recorded 2000-01-01 and a tick", static row => row.Recorded = RecordedAt.Create(new DateTime(2000, 1, 1).AddTicks(1)));
            yield return ("Recorded 2099-12-31 less a tick", static row => row.Recorded = RecordedAt.Create(new DateTime(2099, 12, 31).AddTicks(-1)));
            yield return ("Recorded 2099-12-31", static row => row.Recorded = RecordedAt.Create(new DateTime(2099, 12, 31)));
            yield return ("Recorded 2000-01-01 in UTC", static row => row.Recorded = RecordedAt.Create(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            yield return ("Stamp minimum", static row => row.Stamp = Stamp.Create(new DateTime(2000, 1, 1).AddTicks(1)));
            yield return ("Stamp half a second", static row => row.Stamp = Stamp.Create(new DateTime(2000, 1, 1).AddMilliseconds(500)));
            yield return ("Stamp a second", static row => row.Stamp = Stamp.Create(new DateTime(2000, 1, 1, 0, 0, 1)));
            yield return ("Country FR", static row => row.Country = CountryCode.France);
            yield return ("Country lu", static row => row.Country = CountryCode.Create(" lu "));
            yield return ("Unit kg", static row => row.Unit = Unit.Create("kg"));
            yield return ("Unit L", static row => row.Unit = Unit.Litre);
            yield return ("Rate 0", static row => row.Rate = Rate.Create(0m));
            yield return ("Rate -0.0", static row => row.Rate = Rate.Create(-0.0m));
            yield return ("Rate 0.50", static row => row.Rate = Rate.Create(0.50m));
            yield return ("Rate 1.000", static row => row.Rate = Rate.Create(1.000m));
            yield return ("Factor 0.1f", static row => row.Factor = Factor.Tenth);
            yield return ("Factor 0.5f", static row => row.Factor = Factor.Half);
            yield return ("Tenant", static row => row.Tenant = Tenant.Main);
            yield return ("Code at its minimum length", static row => row.Code = Code.Create("abcde"));
            yield return ("Code holding an emoji", static row => row.Code = Code.Create("abc\U0001F600"));
            yield return ("Iban at its minimum length", static row => row.Iban = Iban.Create("NO9386011117947"));
        }
    }

    /// <summary>
    /// A column one of this package's converters maps is constrained whichever call set the converter: the convention of
    /// a generic value object's construction, one property at a time, an entity identifier's convention. A value object
    /// the application maps through a converter of its own gets none, nor does a property of another type the application
    /// converts, nor a value object written by hand whose bound is text no parser reads, whose closed set holds a value
    /// of another type or none; its readable bound is written.
    /// </summary>
    [Fact]
    public void Every_converter_of_the_package_is_constrained_and_a_converter_of_the_applications_own_is_not()
    {
        using var context = Npgsql<SpecimenContext>();
        var model = context.GetService<IDesignTimeModel>().Model;
        var specimen = model.FindEntityType(typeof(Specimen))!;

        specimen.FindProperty(nameof(Specimen.Stock))!.GetValueConverter().Should().BeOfType<ValueObjectConverter<Catalog<Specimen>.Stock, int>>();
        specimen.FindProperty(nameof(Specimen.Words))!.GetValueConverter().Should().BeOfType<ValueObjectConverter<Declared<Words, int>, int>>();
        specimen.FindProperty(nameof(Specimen.Account))!.GetValueConverter().Should().BeOfType<ValueObjectConverter<AccountId, string>>();
        specimen.FindProperty(nameof(Specimen.Balance128))!.GetValueConverter().Should().BeOfType<LedgerBalanceConverter>();

        var checks = Checks(context, "specimens");
        checks.Keys.Should().NotContain(static name => name.Contains("Balance128", StringComparison.Ordinal));
        checks.Keys.Should().NotContain(static name => name.Contains("Unread", StringComparison.Ordinal));
        checks.Keys.Should().NotContain(static name => name.Contains("Instant", StringComparison.Ordinal));
        checks.Keys.Should().NotContain(static name => name.Contains("Mixed", StringComparison.Ordinal));
        checks.Keys.Should().NotContain(static name => name.Contains("Empty", StringComparison.Ordinal));
        checks.Keys.Should().NotContain(static name => name.Contains("Plain", StringComparison.Ordinal));
        Words.Schema.Minimum.Should().Be("5", "a value object written by hand states its bound as text");
    }

    /// <summary>
    /// A constraint states the rules the type declares, which its <c>Validate</c> enforces, not the schema a registration
    /// made by hand gives its descriptor, which only sizes the column: a constraint stricter than the type would refuse
    /// what the application writes. The value object is this test's own, since the registry is shared by the process.
    /// </summary>
    [Fact]
    public void A_constraint_states_the_rules_of_the_type_not_of_a_registration_made_by_hand()
    {
        ValueObjectRegistry.Register<CheckedMark, string>(new ValueObjectSchema { MinLength = 8, MaxLength = 20 });

        using var context = Npgsql<MarkContext>();

        context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Marked))!.FindProperty(nameof(Marked.Mark))!
            .GetMaxLength().Should().Be(20, "the descriptor sizes the column");
        Checks(context, "marked").Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["CK_marked_Mark_MinLength"] = "octet_length(\"Mark\") >= 3",
        });
    }

    /// <summary>
    /// Only SQL Server, PostgreSQL and SQLite are known: another provider, here SQLite under another name, gets no
    /// constraint.
    /// </summary>
    [Fact]
    public void Another_provider_gets_no_constraint()
    {
        using var context = Create<LedgerContext>(builder => builder
            .UseSqlite(_connection)
            .ReplaceService<IDatabaseProvider, RenamedProvider>());

        context.Database.ProviderName.Should().Be(RenamedProvider.Renamed);
        RelationalModel(context).Tables.SelectMany(static table => table.CheckConstraints).Should().BeEmpty();
    }

    /// <summary>
    /// A value object over a 128-bit integer mapped explicitly through the package's converter has no column type, and
    /// fails the model with Entity Framework Core's own error, which comes after the convention, rather than any of the
    /// convention's.
    /// </summary>
    [Fact]
    public void A_value_object_no_column_maps_is_left_to_Entity_Framework_Cores_own_error()
    {
        using var context = Npgsql<UnmappableContext>();

        var build = () => context.Model;

        build.Should().Throw<InvalidOperationException>().WithMessage("*'Int128'*could not be mapped*");
    }

    [Fact]
    public void A_null_builder_is_refused()
    {
        var add = () => ValueObjectConventionExtensions.AddValueObjectCheckConstraints(null!);

        add.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    private const string LongTable = "long_named";

    private static IRelationalModel RelationalModel(DbContext context)
        => context.GetService<IDesignTimeModel>().Model.GetRelationalModel();

    private static Dictionary<string, string> Checks(DbContext context, string table)
        => RelationalModel(context).FindTable(table, null)!.CheckConstraints.ToDictionary(static check => check.Name!, static check => check.Sql);

    private static IEnumerable<string> Names(IRelationalModel model, string table)
        => model.FindTable(table, null)!.CheckConstraints.Select(static check => check.Name!);

    private static TContext Npgsql<TContext>()
        where TContext : DbContext
        => Create<TContext>(static builder => builder.UseNpgsql("Host=unused"));

    private static TContext Create<TContext>(Action<DbContextOptionsBuilder> configure)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        configure(builder);

        return (TContext)Activator.CreateInstance(typeof(TContext), builder.Options)!;
    }

    private TContext Sqlite<TContext>()
        where TContext : DbContext
        => Create<TContext>(builder => builder.UseSqlite(_connection));

    private async Task RefusedAsync(string insert, string constraint)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = insert;

        var run = () => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        var refusal = (await run.Should().ThrowAsync<SqliteException>()).Which;
        refusal.SqliteErrorCode.Should().Be(19);
        refusal.Message.Should().Contain($"CHECK constraint failed: {constraint}");
    }

    /// <summary>A ledger, whose value objects each break a rule of their own when another writer stores them.</summary>
    private sealed class Ledger
    {
        public int Id { get; set; }

        public Iban Iban { get; set; }

        public Quantity Quantity { get; set; }

        public Quantity? Backorder { get; set; }

        public CountryCode Country { get; set; }

        public Code Code { get; set; }

        public Rate Rate { get; set; }
    }

    /// <summary>A value object of each kind the rules concern, required.</summary>
    private sealed class Specimen
    {
        public int Id { get; set; }

        public Iban Iban { get; set; }

        public Code Code { get; set; }

        public Blank Blank { get; set; }

        public Label Label { get; set; }

        public AccountId Account { get; set; }

        public Quantity Quantity { get; set; }

        public Adjustment Adjustment { get; set; }

        public Score Score { get; set; }

        public Port Port { get; set; }

        public PageNumber Page { get; set; }

        public FileSize FileSize { get; set; }

        public Counter Counter { get; set; }

        public Latitude Latitude { get; set; }

        public Tolerance Tolerance { get; set; }

        public Ratio Ratio { get; set; }

        public Fraction Fraction { get; set; }

        public Amount Balance { get; set; }

        public Percentage Share { get; set; }

        public Fee Fee { get; set; }

        public Fee FeeInCents { get; set; }

        public Grade Grade { get; set; }

        public Initial Initial { get; set; }

        public Indent Indent { get; set; }

        public EffectiveDate Effective { get; set; }

        public OpeningTime Opening { get; set; }

        public OpeningTime OpeningInMilliseconds { get; set; }

        public RecordedAt Recorded { get; set; }

        public RecordedAt RecordedLocally { get; set; }

        public Stamp Stamp { get; set; }

        public OccurredAt Occurred { get; set; }

        public Deadline Deadline { get; set; }

        public Duration Duration { get; set; }

        public Window Window { get; set; }

        public CountryCode Country { get; set; }

        public DocumentStatus Status { get; set; }

        public Colour Colour { get; set; }

        public Unit Unit { get; set; }

        public Rate Rate { get; set; }

        public Factor Factor { get; set; }

        public Reading Reading { get; set; }

        public Milestone Milestone { get; set; }

        public Tenant Tenant { get; set; }

        public Consent Consent { get; set; }

        public CustomerId Customer { get; set; }

        public Catalog<Specimen>.Stock Stock { get; set; }

        public Declared<Words, int> Words { get; set; }

        public Declared<UnreadBound, int> Unread { get; set; }

        public Declared<MixedKnownValues, int> Mixed { get; set; }

        public Declared<EmptyClosedSet, int> Empty { get; set; }

        public Declared<UnreadInstant, DateTime> Instant { get; set; }

        public int Plain { get; set; }

        public LedgerBalance Balance128 { get; set; }
    }

    private class Party
    {
        public int Id { get; set; }

        public Quantity Legs { get; set; }
    }

    private sealed class Firm : Party
    {
        public Quantity Staff { get; set; }
    }

    private abstract class Animal
    {
        public int Id { get; set; }

        public Quantity Legs { get; set; }
    }

    private sealed class Dog : Animal
    {
        public Code Chip { get; set; }
    }

    private sealed class Cat : Animal
    {
        public Rate Lives { get; set; }
    }

    private class Fruit
    {
        public int Id { get; set; }

        public Quantity Seeds { get; set; }
    }

    private sealed class Apple : Fruit
    {
        public Code Variety { get; set; }
    }

    private abstract class Tool
    {
        public int Id { get; set; }

        public Quantity Weight { get; set; }
    }

    private sealed class Saw : Tool
    {
        public PageNumber Teeth { get; set; }
    }

    private class Hammer : Tool
    {
        public Code Head { get; set; }
    }

    private sealed class Sledge : Hammer
    {
        public Score Swing { get; set; }
    }

    private sealed class Order
    {
        public int Id { get; set; }

        public Address Shipping { get; set; } = new();

        public Address Billing { get; set; } = new();

        public List<Address> Stops { get; set; } = [];

        public Contact Contact { get; set; } = new();

        public Contact Archived { get; set; } = new();

        public Note Note { get; set; } = new();

        public List<Iban> Ibans { get; set; } = [];
    }

    private sealed class Address
    {
        public CountryCode Country { get; set; }

        public Locality City { get; set; } = new();
    }

    private sealed class Locality
    {
        public Code Zip { get; set; }
    }

    private sealed class Contact
    {
        public Iban Account { get; set; }

        public Quantity Floor { get; set; }
    }

    private sealed class Note
    {
        public Code Code { get; set; }
    }

    private sealed class Visit
    {
        public int Id { get; set; }

        public Code Code { get; set; }

        public Code Detail { get; set; }
    }

    private sealed class Viewed
    {
        public Code Code { get; set; }
    }

    private abstract class Pet
    {
        public int Id { get; set; }
    }

    private sealed class Parrot : Pet
    {
        public Code Chip { get; set; }

        public Quantity Tag { get; set; }

        public Score Band { get; set; }
    }

    private sealed class Canary : Pet
    {
        public Code Chip { get; set; }

        public Score Tag { get; set; }

        public short Band { get; set; }
    }

    private sealed class Holding
    {
        public AccountId Id { get; set; }

        public Note Holder { get; set; } = new();
    }

    private sealed class LongNamed
    {
        public int Id { get; set; }

        public Quantity AQuantityWhoseNameKeepsGoingPastWhatPostgreSqlTakesAsAnIdentifierOne { get; set; }

        public Quantity AQuantityWhoseNameKeepsGoingPastWhatPostgreSqlTakesAsAnIdentifierTwo { get; set; }
    }

    private sealed class Patterned
    {
        public int Id { get; set; }

        public Iban Iban { get; set; }

        public PhoneNumber? Phone { get; set; }

        public CurrencyCode Currency { get; set; }

        public AccountId Account { get; set; }

        public ShapedCode<LowerCaseShape> Lowered { get; set; }

        public ShapedCode<PortableShape> Portable { get; set; }

        public Declared<StatedPattern, string> Stated { get; set; }
    }

    private sealed class Mailed
    {
        public int Id { get; set; }

        public EmailAddress Email { get; set; }

        public ShapedCode<CaseFreeShape> Shouted { get; set; }

        public Iban Iban { get; set; }
    }

    /// <summary>A code whose pattern is read under an option VO0025 does not report.</summary>
    private sealed class Ruled
    {
        public int Id { get; set; }

        public ShapedCode<EcmaShape> Code { get; set; }
    }

    private sealed class Marked
    {
        public int Id { get; set; }

        public CheckedMark Mark { get; set; }
    }

    private sealed class Unmappable
    {
        public int Id { get; set; }

        public LedgerBalance Balance { get; set; }
    }

    /// <summary>Closed sets of text whose types store other spellings of their known values.</summary>
    private sealed class Spelling
    {
        public int Id { get; set; }

        public Phase Phase { get; set; }

        public Weight Weight { get; set; }

        public Letter Letter { get; set; }

        public LowerLetter LowerLetter { get; set; }
    }

    /// <summary>The root of a table-per-concrete-type hierarchy, whose complex property its leaf inherits.</summary>
    private abstract class Vessel
    {
        public int Id { get; set; }

        public Locality Port { get; set; } = new();
    }

    private sealed class Ferry : Vessel
    {
        public Quantity Cars { get; set; }

        public Locality Berth { get; set; } = new();
    }

    /// <summary>A score in column <c>C</c> of table <c>clash_a_b</c>.</summary>
    private sealed class ClashAB
    {
        public int Id { get; set; }

        public Score C { get; set; }
    }

    /// <summary>A score in column <c>B_C</c> of table <c>clash_a</c>, whose constraint reads as <see cref="ClashAB"/>'s.</summary>
    private sealed class ClashA
    {
        public int Id { get; set; }

        public Score BC { get; set; }
    }

    /// <summary>An order whose complex line has its score in column <c>Line_Score</c>.</summary>
    private sealed class ClashOrder
    {
        public int Id { get; set; }

        public ClashLine Line { get; set; } = new();
    }

    private sealed class ClashLine
    {
        public Score Score { get; set; }
    }

    /// <summary>A score in table <c>clash_orders_Line</c>, whose constraint reads as <see cref="ClashOrder"/>'s.</summary>
    private sealed class ClashOrderLine
    {
        public int Id { get; set; }

        public Score Score { get; set; }
    }

    /// <summary>A score whose table already holds a constraint of the application's own under the name its own takes.</summary>
    private sealed class ClashDeclared
    {
        public int Id { get; set; }

        public Score Score { get; set; }
    }

    /// <summary>A value object of each kind SQLite writes a rule for, optional, so that a row sets one at a time.</summary>
    private sealed class Boundary
    {
        public int Id { get; set; }

        public Grade? Grade { get; set; }

        public Initial? Initial { get; set; }

        public Indent? Indent { get; set; }

        public Adjustment? Adjustment { get; set; }

        public Score? Score { get; set; }

        public Port? Port { get; set; }

        public PageNumber? Page { get; set; }

        public FileSize? FileSize { get; set; }

        public Quantity? Quantity { get; set; }

        public Counter? Counter { get; set; }

        public Latitude? Latitude { get; set; }

        public Tolerance? Tolerance { get; set; }

        public Ratio? Ratio { get; set; }

        public Fraction? Fraction { get; set; }

        public EffectiveDate? Effective { get; set; }

        public OpeningTime? Opening { get; set; }

        public RecordedAt? Recorded { get; set; }

        public Stamp? Stamp { get; set; }

        public CountryCode? Country { get; set; }

        public Unit? Unit { get; set; }

        public Rate? Rate { get; set; }

        public Factor? Factor { get; set; }

        public Tenant? Tenant { get; set; }

        public Code? Code { get; set; }

        public Iban? Iban { get; set; }
    }

    /// <summary>The options of the contexts here: the conventions, and the check constraints.</summary>
    /// <param name="options">The provider.</param>
    private abstract class CheckContext(DbContextOptions options) : DbContext(options)
    {
        protected virtual bool Checked => true;

        protected virtual Action<ValueObjectCheckOptions>? Choose => null;

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder
                .ConfigureValueObjects(typeof(Iban).Assembly)
                .ConfigureEntityIds(typeof(AccountId).Assembly);
            if (Checked)
            {
                configurationBuilder.AddValueObjectCheckConstraints(Choose);
            }
        }
    }

    private class LedgerContext(DbContextOptions options) : CheckContext(options)
    {
        public DbSet<Ledger> Ledgers => Set<Ledger>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Ledger>(ledger =>
            {
                ledger.ToTable("ledgers");
                ledger.Property(static entity => entity.Id).ValueGeneratedNever();
            });
    }

    private sealed class UncheckedLedgerContext(DbContextOptions options) : LedgerContext(options)
    {
        protected override bool Checked => false;
    }

    private sealed class BoundsOnlyContext(DbContextOptions options) : LedgerContext(options)
    {
        protected override Action<ValueObjectCheckOptions>? Choose => static checks => checks.Rules = ValueObjectChecks.Bounds;
    }

    private sealed class NoRuleContext(DbContextOptions options) : LedgerContext(options)
    {
        protected override Action<ValueObjectCheckOptions>? Choose => static checks => checks.Rules = ValueObjectChecks.None;
    }

    /// <summary>Every rule written by the first call, the bounds alone by the second.</summary>
    /// <param name="options">The provider.</param>
    private sealed class NarrowedLedgerContext(DbContextOptions options) : LedgerContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.AddValueObjectCheckConstraints(static checks => checks.Rules = ValueObjectChecks.Bounds);
        }
    }

    /// <summary>The patterns of an email address, which PostgreSQL's dialect would read apart, named by the first call alone.</summary>
    /// <param name="options">The provider.</param>
    private sealed class UnpatternedEmailContext(DbContextOptions options) : CheckContext(options)
    {
        protected override Action<ValueObjectCheckOptions>? Choose => static checks => checks.Patterns = ValueObjectPatternDialect.PostgreSql;

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.AddValueObjectCheckConstraints();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Mailed>(mailed =>
            {
                mailed.ToTable("mailed");
                mailed.Ignore(static entity => entity.Shouted);
            });
    }

    private sealed class SpecimenContext(DbContextOptions options) : CheckContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Specimen>(specimen =>
            {
                specimen.ToTable("specimens");
                specimen.Property(static entity => entity.FeeInCents).HasPrecision(18, 2);
                if (Database.IsNpgsql())
                {
                    specimen.Property(static entity => entity.RecordedLocally).HasColumnType("timestamp without time zone");
                }

                specimen.Property(static entity => entity.Words).HasValueObjectConversion<Declared<Words, int>, int>();
                specimen.Property(static entity => entity.Unread).HasValueObjectConversion<Declared<UnreadBound, int>, int>();
                specimen.Property(static entity => entity.Mixed).HasValueObjectConversion<Declared<MixedKnownValues, int>, int>();
                specimen.Property(static entity => entity.Empty).HasValueObjectConversion<Declared<EmptyClosedSet, int>, int>();
                specimen.Property(static entity => entity.Instant).HasValueObjectConversion<Declared<UnreadInstant, DateTime>, DateTime>();
                specimen.Property(static entity => entity.Plain).HasConversion<NumberToStringConverter<int>>();
                specimen.Property(static entity => entity.Share).HasPrecision(5, 2);
                specimen.Property(static entity => entity.OpeningInMilliseconds).HasPrecision(3);
                specimen.Property(static entity => entity.Balance128).HasConversion<LedgerBalanceConverter>();
            });
    }

    private sealed class HierarchyContext(DbContextOptions options) : LedgerContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Party>().ToTable("parties");
            modelBuilder.Entity<Firm>();
        }
    }

    private sealed class ShapesContext(DbContextOptions options) : CheckContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Animal>().ToTable("animals").UseTphMappingStrategy();
            modelBuilder.Entity<Dog>();
            modelBuilder.Entity<Cat>();

            modelBuilder.Entity<Fruit>().ToTable("fruits").UseTptMappingStrategy();
            modelBuilder.Entity<Apple>().ToTable("apples");

            modelBuilder.Entity<Tool>().UseTpcMappingStrategy().Property(static tool => tool.Id).ValueGeneratedNever();
            modelBuilder.Entity<Saw>().ToTable("saws");
            modelBuilder.Entity<Hammer>().ToTable("hammers");
            modelBuilder.Entity<Sledge>().ToTable("sledges");

            modelBuilder.Entity<Order>(order =>
            {
                order.ToTable("orders");
                order.ComplexProperty(static entity => entity.Shipping, static shipping => shipping.ComplexProperty(static address => address.City));
                order.ComplexProperty(static entity => entity.Billing, static billing => billing.ToJson());
                order.ComplexCollection(static entity => entity.Stops, static stops => stops.ToJson());
                order.OwnsOne(static entity => entity.Contact);
                order.OwnsOne(static entity => entity.Archived, static archived => archived.ToJson());
                order.OwnsOne(static entity => entity.Note, static note => note.ToTable("notes"));
            });

            modelBuilder.Entity<Visit>(visit =>
            {
                visit.ToTable("visits");
                visit.SplitToTable("visit_details", static details => details.Property(static entity => entity.Detail));
            });

            modelBuilder.Entity<Viewed>().HasNoKey().ToView("customer_view");
        }
    }

    private sealed class SharedColumnContext(DbContextOptions options) : CheckContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Pet>().ToTable("pets");
            modelBuilder.Entity<Parrot>(parrot =>
            {
                parrot.Property(static entity => entity.Chip).HasColumnName("Chip");
                parrot.Property(static entity => entity.Tag).HasColumnName("Tag");
                parrot.Property(static entity => entity.Band).HasColumnName("Band");
            });
            modelBuilder.Entity<Canary>(canary =>
            {
                canary.Property(static entity => entity.Chip).HasColumnName("Chip");
                canary.Property(static entity => entity.Tag).HasColumnName("Tag");
                canary.Property(static entity => entity.Band).HasColumnName("Band");
            });
            modelBuilder.Entity<Holding>(holder =>
            {
                holder.ToTable("accounts");
                holder.OwnsOne(static entity => entity.Holder);
            });
        }
    }

    private sealed class LongNameContext(DbContextOptions options) : CheckContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<LongNamed>().ToTable(LongTable);
    }

    private class PostgreSqlPatternContext(DbContextOptions options) : CheckContext(options)
    {
        protected override Action<ValueObjectCheckOptions>? Choose => static checks => checks.Patterns = ValueObjectPatternDialect.PostgreSql;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Patterned>(patterned =>
            {
                patterned.ToTable("patterned");
                patterned.Property(static entity => entity.Lowered).HasValueObjectConversion<ShapedCode<LowerCaseShape>, string>();
                patterned.Property(static entity => entity.Portable).HasValueObjectConversion<ShapedCode<PortableShape>, string>();
                patterned.Property(static entity => entity.Stated).HasValueObjectConversion<Declared<StatedPattern, string>, string>();
            });
    }

    private sealed class SqlServerPatternContext(DbContextOptions options) : PostgreSqlPatternContext(options)
    {
        protected override Action<ValueObjectCheckOptions>? Choose => static checks => checks.Patterns = ValueObjectPatternDialect.SqlServer2025;
    }

    private sealed class PatternOnlyContext(DbContextOptions options) : PostgreSqlPatternContext(options)
    {
        protected override Action<ValueObjectCheckOptions>? Choose => static checks =>
        {
            checks.Rules = ValueObjectChecks.None;
            checks.Patterns = ValueObjectPatternDialect.PostgreSql;
        };
    }

    private sealed class EmailPatternContext(DbContextOptions options) : CheckContext(options)
    {
        protected override Action<ValueObjectCheckOptions>? Choose => static checks => checks.Patterns = ValueObjectPatternDialect.PostgreSql;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Mailed>(mailed =>
            {
                mailed.ToTable("mailed");
                mailed.Ignore(static entity => entity.Shouted);
            });
    }

    private sealed class EcmaPatternContext(DbContextOptions options) : CheckContext(options)
    {
        protected override Action<ValueObjectCheckOptions>? Choose => static checks => checks.Patterns = ValueObjectPatternDialect.PostgreSql;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Ruled>(ruled =>
            {
                ruled.ToTable("ruled");
                ruled.Property(static entity => entity.Code).HasValueObjectConversion<ShapedCode<EcmaShape>, string>();
            });
    }

    private sealed class CaseFreePatternContext(DbContextOptions options) : CheckContext(options)
    {
        protected override Action<ValueObjectCheckOptions>? Choose => static checks => checks.Patterns = ValueObjectPatternDialect.SqlServer2025;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Mailed>(mailed =>
            {
                mailed.ToTable("mailed");
                mailed.Ignore(static entity => entity.Email);
                mailed.Property(static entity => entity.Shouted).HasValueObjectConversion<ShapedCode<CaseFreeShape>, string>();
            });
    }

    private sealed class ExcludedPatternContext(DbContextOptions options) : CheckContext(options)
    {
        protected override Action<ValueObjectCheckOptions>? Choose => static checks =>
        {
            checks.Patterns = ValueObjectPatternDialect.PostgreSql;
            checks.ExcludePattern<EmailAddress>().ExcludePattern<ShapedCode<CaseFreeShape>>();
        };

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Mailed>(mailed =>
            {
                mailed.ToTable("mailed");
                mailed.Property(static entity => entity.Shouted).HasValueObjectConversion<ShapedCode<CaseFreeShape>, string>();
            });
    }

    private sealed class UndefinedDialectContext(DbContextOptions options) : LedgerContext(options)
    {
        protected override Action<ValueObjectCheckOptions>? Choose => static checks => checks.Patterns = (ValueObjectPatternDialect)7;
    }

    private sealed class MarkContext(DbContextOptions options) : CheckContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Marked>().ToTable("marked");
    }

    private sealed class UnmappableContext(DbContextOptions options) : CheckContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Unmappable>().Property(static entity => entity.Balance).HasValueObjectConversion<LedgerBalance, Int128>();
    }

    private sealed class SpellingContext(DbContextOptions options) : CheckContext(options)
    {
        public DbSet<Spelling> Spellings => Set<Spelling>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Spelling>(spelling =>
            {
                spelling.ToTable("spellings");
                spelling.Property(static entity => entity.Id).ValueGeneratedNever();
            });
    }

    private sealed class VesselContext(DbContextOptions options) : CheckContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Vessel>(vessel =>
            {
                vessel.UseTpcMappingStrategy();
                vessel.ComplexProperty(static entity => entity.Port);
            });
            modelBuilder.Entity<Ferry>(ferry =>
            {
                ferry.ToTable("ferries");
                ferry.ComplexProperty(static entity => entity.Berth);
            });
        }
    }

    private sealed class ClashContext(DbContextOptions options) : CheckContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClashAB>().ToTable("clash_a_b");
            modelBuilder.Entity<ClashA>(clash =>
            {
                clash.ToTable("clash_a");
                clash.Property(static entity => entity.BC).HasColumnName("B_C");
            });
            modelBuilder.Entity<ClashOrder>(order =>
            {
                order.ToTable("clash_orders");
                order.ComplexProperty(static entity => entity.Line);
            });
            modelBuilder.Entity<ClashOrderLine>().ToTable("clash_orders_Line");
            modelBuilder.Entity<ClashDeclared>(declared => declared
                .ToTable("clash_declared", static table => table.HasCheckConstraint("CK_clash_declared_Score_Maximum", "\"Score\" <> 13")));
        }
    }

    private sealed class BoundaryContext(DbContextOptions options) : CheckContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Boundary>(boundary =>
            {
                boundary.ToTable("boundaries");
                boundary.Property(static entity => entity.Id).ValueGeneratedNever();
            });
    }

    /// <summary>A converter of the application's own, which may store anything: its column gets no constraint.</summary>
    private sealed class LedgerBalanceConverter() : ValueConverter<LedgerBalance, decimal>(
        static balance => (decimal)balance.Value,
        static stored => LedgerBalance.Create((Int128)stored));

    /// <summary>SQLite under a name the convention does not know.</summary>
    /// <param name="dependencies">The services of a provider.</param>
#pragma warning disable EF1001 // SQLite's options extension is internal to its provider; only its name is replaced.
    private sealed class RenamedProvider(DatabaseProviderDependencies dependencies)
        : DatabaseProvider<SqliteOptionsExtension>(dependencies)
#pragma warning restore EF1001
    {
        public const string Renamed = "AdCodicem.Tests.Unknown";

        public override string Name => Renamed;
    }

    /// <summary>A pattern a value object written by hand states in its schema, with no hook.</summary>
    private sealed class StatedPattern : IDeclaration<string>
    {
        public static ValueObjectSchema Schema { get; } = new() { Pattern = "^[0-9]{4}$" };
    }

    /// <summary>A count, its bounds stated as text, as a value object written by hand states them.</summary>
    private sealed class Words : IDeclaration<int>
    {
        public static ValueObjectSchema Schema { get; } = new() { Minimum = "5", Maximum = "9" };
    }

    /// <summary>Bounds no parser reads.</summary>
    private sealed class UnreadBound : IDeclaration<int>
    {
        public static ValueObjectSchema Schema { get; } = new() { Minimum = "five", Maximum = "nine" };
    }

    /// <summary>An instant whose bound no parser reads.</summary>
    private sealed class UnreadInstant : IDeclaration<DateTime>
    {
        public static ValueObjectSchema Schema { get; } = new() { Minimum = "never" };
    }

    /// <summary>A closed set with no value, which no <c>IN</c> can state.</summary>
    private sealed class EmptyClosedSet : IDeclaration<int>
    {
        public static ValueObjectSchema Schema { get; } = new() { IsClosedValueSet = true };
    }

    /// <summary>A closed set holding a value of another type than the underlying one.</summary>
    private sealed class MixedKnownValues : IDeclaration<int>
    {
        public static ValueObjectSchema Schema { get; } = new() { IsClosedValueSet = true, KnownValues = [1, "two"] };
    }

    /// <summary>Three lower-case letters, under options its text carries.</summary>
    public sealed partial class LowerCaseShape : ICodeShape
    {
        [GeneratedRegex("^[a-z]{3}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
        public static partial Regex Shape { get; }
    }

    /// <summary>
    /// Three lower-case letters, under the options the text keeps its meaning under: explicit captures, no backtracking
    /// and compiled, which no generated regular expression of a test sets.
    /// </summary>
    public sealed class PortableShape : ICodeShape
    {
        public static Regex Shape { get; } = new(
            "^[a-z]{3}$",
            RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }

    /// <summary>Three lower-case letters, read as ECMAScript reads them, which VO0025 does not report.</summary>
    public sealed class EcmaShape : ICodeShape
    {
        public static Regex Shape { get; } = new("^[a-z]{3}$", RegexOptions.ECMAScript, TimeSpan.FromSeconds(1));
    }

    /// <summary>Three letters in either case, which the text alone refuses in upper case.</summary>
    public sealed partial class CaseFreeShape : ICodeShape
    {
        [GeneratedRegex("^[a-z]{3}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
        public static partial Regex Shape { get; }
    }

    /// <summary>A mark, which one test registers by hand with a schema of its own.</summary>
    [ValueObject<string>(MinLength = 3, MaxLength = 12)]
    public readonly partial struct CheckedMark;

    /// <summary>A code, counted as .NET counts it: no normalizer trims it.</summary>
    [ValueObject<string>(MinLength = 5, MaxLength = 10)]
    public readonly partial struct Code;

    /// <summary>A text that may be empty, stated so.</summary>
    [ValueObject<string>(MinLength = 0, MaxLength = 10, AllowEmpty = true)]
    public readonly partial struct Blank;

    /// <summary>An unsigned 64-bit counter, past what SQLite holds as a positive integer.</summary>
    [ValueObject<ulong>]
    public readonly partial struct Counter : IValueObjectMinimum<ulong>, IValueObjectMaximum<ulong>
    {
        public static ulong Minimum => 1;

        public static ulong Maximum => ulong.MaxValue - 1;
    }

    /// <summary>A fraction, whose bounds are no float exactly.</summary>
    [ValueObject<float>]
    public readonly partial struct Fraction : IValueObjectMinimum<float>, IValueObjectMaximum<float>
    {
        public static float Minimum => 0.1f;

        public static float Maximum => 0.7f;
    }

    /// <summary>A fee, whose bounds are finer than a cent.</summary>
    [ValueObject<decimal>]
    public readonly partial struct Fee : IValueObjectMinimum<decimal>, IValueObjectMaximum<decimal>
    {
        public static decimal Minimum => 0.001m;

        public static decimal Maximum => 9.995m;
    }

    /// <summary>An initial, from a space on.</summary>
    [ValueObject<char>]
    public readonly partial struct Initial : IValueObjectMinimum<char>, IValueObjectMaximum<char>
    {
        public static char Minimum => ' ';

        public static char Maximum => 'z';
    }

    /// <summary>An instant whose minimum is a tick past a second.</summary>
    [ValueObject<DateTime>]
    public readonly partial struct Stamp : IValueObjectMinimum<DateTime>
    {
        public static DateTime Minimum => new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(1);
    }

    /// <summary>A deadline, its bounds at offsets of their own.</summary>
    [ValueObject<DateTimeOffset>]
    public readonly partial struct Deadline : IValueObjectMinimum<DateTimeOffset>, IValueObjectMaximum<DateTimeOffset>
    {
        public static DateTimeOffset Minimum => new(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));

        public static DateTimeOffset Maximum => new(2030, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));
    }

    /// <summary>A window, negative and past a day.</summary>
    [ValueObject<TimeSpan>]
    public readonly partial struct Window : IValueObjectMinimum<TimeSpan>, IValueObjectMaximum<TimeSpan>
    {
        public static TimeSpan Minimum => TimeSpan.FromHours(-2);

        public static TimeSpan Maximum => TimeSpan.FromDays(2);
    }

    /// <summary>A closed set of colours, looked up as the invariant culture compares.</summary>
    [ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.InvariantCulture)]
    public readonly partial struct Colour
    {
        [KnownValue]
        public static readonly Colour Red = Known("red");
    }

    /// <summary>A closed set of units, looked up ignoring case and stored in upper case.</summary>
    [ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.OrdinalIgnoreCase)]
    public readonly partial struct Unit : IValueObjectNormalizer<string>
    {
        [KnownValue]
        public static readonly Unit Kilogram = Known("KG");

        [KnownValue]
        public static readonly Unit Litre = Known("L");

        public static string NormalizeValue(string value) => value.ToUpperInvariant();
    }

    /// <summary>A closed set of rates, written at scales of their own.</summary>
    [ValueObject<decimal>(ValueSet = ValueSetKind.Closed)]
    public readonly partial struct Rate
    {
        [KnownValue]
        public static readonly Rate Nil = Known(0m);

        [KnownValue]
        public static readonly Rate Half = Known(0.5m);

        [KnownValue]
        public static readonly Rate Whole = Known(1.0m);
    }

    /// <summary>A closed set of floats, no decimal fraction exactly.</summary>
    [ValueObject<float>(ValueSet = ValueSetKind.Closed)]
    public readonly partial struct Factor
    {
        [KnownValue]
        public static readonly Factor Tenth = Known(0.1f);

        [KnownValue]
        public static readonly Factor Half = Known(0.5f);
    }

    /// <summary>A closed set holding a value that is not a number, which no column compares as .NET does.</summary>
    [ValueObject<double>(ValueSet = ValueSetKind.Closed)]
    public readonly partial struct Reading
    {
        [KnownValue]
        public static readonly Reading Half = Known(0.5);

        [KnownValue]
        public static readonly Reading Unknown = Known(double.NaN);
    }

    /// <summary>A closed set of instants with their offset.</summary>
    [ValueObject<DateTimeOffset>(ValueSet = ValueSetKind.Closed)]
    public readonly partial struct Milestone
    {
        [KnownValue]
        public static readonly Milestone Start = Known(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    /// <summary>A closed set of identifiers.</summary>
    [ValueObject<Guid>(ValueSet = ValueSetKind.Closed)]
    public readonly partial struct Tenant
    {
        [KnownValue]
        public static readonly Tenant Main = Known(new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"));
    }

    /// <summary>An indent, from a tab on, below the space PostgreSQL's <c>character(1)</c> drops.</summary>
    [ValueObject<char>]
    public readonly partial struct Indent : IValueObjectMinimum<char>, IValueObjectMaximum<char>
    {
        public static char Minimum => '\t';

        public static char Maximum => '~';
    }

    /// <summary>
    /// Units looked up ignoring case and stored in lower case, the Greek <c>μ</c> among them, for which the comparison
    /// takes the micro sign <c>µ</c>, which no case of the whole value reaches.
    /// </summary>
    [ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.OrdinalIgnoreCase)]
    public readonly partial struct Phase : IValueObjectNormalizer<string>
    {
        [KnownValue]
        public static readonly Phase Microgram = Known("\u03BCg");

        [KnownValue]
        public static readonly Phase Milligram = Known("mg");

        [KnownValue]
        public static readonly Phase Sigma = Known("\u03C3a");

        public static string NormalizeValue(string value) => value.ToLowerInvariant();
    }

    /// <summary>Units looked up ignoring case, with no normalizer: <c>kg</c> is stored as it is written.</summary>
    [ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.OrdinalIgnoreCase)]
    public readonly partial struct Weight
    {
        [KnownValue]
        public static readonly Weight Kilogram = Known("KG");

        [KnownValue]
        public static readonly Weight Litre = Known("L");
    }

    /// <summary>A Deseret capital looked up ignoring case, with no normalizer: its small letter is stored as it is.</summary>
    [ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.OrdinalIgnoreCase)]
    public readonly partial struct Letter
    {
        [KnownValue]
        public static readonly Letter LongI = Known("\U00010400");
    }

    /// <summary>A Deseret small letter looked up ignoring case, with no normalizer: its capital is stored as it is.</summary>
    [ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.OrdinalIgnoreCase)]
    public readonly partial struct LowerLetter
    {
        [KnownValue]
        public static readonly LowerLetter LongI = Known("\U00010428");
    }
}
