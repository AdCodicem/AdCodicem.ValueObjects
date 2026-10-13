using System.Data.Common;
using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using AdCodicem.ValueObjects.UnitTests.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Verifies, on a real database engine, that the CHECK constraints <c>AddValueObjectCheckConstraints</c> writes accept
/// every value the value objects accept, and refuse another writer's row breaking a rule.
/// </summary>
/// <remarks>
/// The constraints live on tables of their own, which the sample's model does not hold: its tests store refused rows on
/// purpose, to read them strictly. Every context here calls <c>UseValueObjectCheckErrors()</c>, so that each refusal is
/// also checked to carry the code of its rule, but where a test says otherwise.
/// </remarks>
/// <typeparam name="TFixture">Database under test.</typeparam>
public abstract class CheckConstraintTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    /// <summary>The tables of each fixture, created once.</summary>
    private static readonly ConditionalWeakTable<DatabaseFixture, Task> Tables = new();

    private static int _lastId;

    /// <summary>Gets a value indicating whether the engine stores an instant with its offset, which Npgsql writes in UTC only.</summary>
    protected abstract bool KeepsOffsets { get; }

    /// <summary>Gets a value indicating whether the engine stores a duration outside a day, which SQL Server's <c>time</c> cannot.</summary>
    protected abstract bool StoresAnyDuration { get; }

    /// <summary>Gets the kind of <see cref="DateTime"/> the provider writes.</summary>
    protected abstract DateTimeKind InstantKind { get; }

    /// <summary>Gets a value indicating whether a text column of the database tells case by default.</summary>
    protected abstract bool ComparesCase { get; }

    /// <summary>
    /// Gets a value indicating whether the provider reports the refusal of a statement returning rows only while they are
    /// read, as SqlClient does for SQL Server's <c>OUTPUT</c>.
    /// </summary>
    protected abstract bool RefusesWhileReading { get; }

    /// <summary>Gets the database under test.</summary>
    protected TFixture Fixture => fixture;

    /// <summary>
    /// Every value each value object accepts is written: the boundaries of its rules, a tick past them, text at its minimum
    /// length holding a character outside the Basic Multilingual Plane or ending with a space, another spelling a closed set
    /// normalizes, an instant at another offset, a decimal at another scale. None is refused by a constraint stricter than
    /// the type.
    /// </summary>
    [Fact]
    public async Task Every_value_the_value_objects_accept_is_written()
    {
        await EnsureTablesAsync();
        var refused = new List<string>();

        foreach (var (label, set) in Accepted())
        {
            var row = new CheckedValues { Id = Interlocked.Increment(ref _lastId) };
            set(row);
            await using var write = CreateContext<CheckedContext>();
            write.Rows.Add(row);
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
    }

    /// <summary>
    /// A row another writer stores with a value a rule refuses is refused by the engine, the constraint named: a length,
    /// each kind of bound, a closed set of text, decimals and floats.
    /// </summary>
    /// <param name="column">The column written.</param>
    /// <param name="literal">The value, as SQL.</param>
    /// <param name="rule">The rule it breaks.</param>
    /// <param name="code">The code of the rule, which the provider's error carries.</param>
    [Theory]
    [InlineData("Iban", "'FR7630006000'", "MinLength", ValueObjectErrorCodes.TooShort)]
    [InlineData("Code", "'abcd'", "MinLength", ValueObjectErrorCodes.TooShort)]
    [InlineData("Adjustment", "-11", "Minimum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("Score", "101", "Maximum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("Counter", "0", "Minimum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("Fee", "0.0005", "Minimum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("Fraction", "0.75", "Maximum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("Latitude", "90.5", "Maximum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("Initial", "'|'", "Maximum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("Opening", "'05:59:59'", "Minimum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("Effective", "'1999-12-31'", "Minimum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("Recorded", "'1999-12-31 23:59:59'", "Minimum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("Deadline", "'1999-12-31T21:59:59+00:00'", "Minimum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("Country", "'GB'", "KnownValues", ValueObjectErrorCodes.NotAKnownValue)]
    [InlineData("Rate", "0.25", "KnownValues", ValueObjectErrorCodes.NotAKnownValue)]
    [InlineData("Factor", "0.2", "KnownValues", ValueObjectErrorCodes.NotAKnownValue)]
    public async Task Another_writers_row_breaking_a_rule_is_refused_by_the_engine(string column, string literal, string rule, string code)
    {
        await EnsureTablesAsync();
        await using var context = CreateContext<CheckedContext>();
        var insert = $"INSERT INTO {Q("checked_values")} ({Q("Id")}, {Q(column)}) VALUES ({Interlocked.Increment(ref _lastId)}, {literal})";

        var write = () => context.Database.ExecuteSqlRawAsync(insert, TestContext.Current.CancellationToken);

        var refusal = (await write.Should().ThrowAsync<DbException>()).Which;
        Refused(refusal, $"CK_checked_values_{column}_{rule}", code);
    }

    /// <summary>
    /// A closed set of text compares as the column does: another spelling of a known value, which the type would have
    /// normalized, is refused under PostgreSQL's collation, which tells case, and accepted under SQL Server's default one,
    /// which does not. The constraint is then laxer than the type, never stricter.
    /// </summary>
    [Fact]
    public async Task A_closed_set_of_text_compares_as_the_column_does()
    {
        await EnsureTablesAsync();
        await using var context = CreateContext<CheckedContext>();
        var insert = $"INSERT INTO {Q("checked_values")} ({Q("Id")}, {Q("Unit")}) VALUES ({Interlocked.Increment(ref _lastId)}, 'kg')";

        var write = () => context.Database.ExecuteSqlRawAsync(insert, TestContext.Current.CancellationToken);

        if (ComparesCase)
        {
            Refused((await write.Should().ThrowAsync<DbException>()).Which, "CK_checked_values_Unit_KnownValues", ValueObjectErrorCodes.NotAKnownValue);
        }
        else
        {
            (await write()).Should().Be(1);
        }
    }

    /// <summary>
    /// The engine's catalog lists the constraints of the model, no more: the table was created from it.
    /// </summary>
    [Fact]
    public async Task The_catalog_lists_the_constraints_of_the_model()
    {
        await EnsureTablesAsync();
        await using var context = CreateContext<CheckedContext>();
        var sql = $"SELECT constraint_name AS {Q("Value")} FROM information_schema.check_constraints WHERE constraint_name LIKE 'CK_checked_values%'";

        var listed = await context.Database.SqlQueryRaw<string>(sql).ToListAsync(TestContext.Current.CancellationToken);

        var modelled = context.GetService<IDesignTimeModel>().Model.GetRelationalModel()
            .FindTable("checked_values", null)!.CheckConstraints.Select(static check => check.Name);
        listed.Should().BeEquivalentTo(modelled);
        listed.Should().HaveCountGreaterThan(30);
    }

    /// <summary>
    /// A table already holding a row a rule refuses cannot take the constraint: the migration that adds it fails, until
    /// the row is corrected. The guide's audit query counts such rows, constraint by constraint, beforehand, each once in
    /// the table it lies in, over a model holding a table-per-hierarchy, whose derived type shares its base type's table,
    /// and a table-per-type hierarchy, whose derived type has a table of its own.
    /// </summary>
    [Fact]
    public async Task A_table_holding_a_refused_row_cannot_take_the_constraint_and_the_audit_query_finds_the_row()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnsureTablesAsync();
        await using var legacy = CreateContext<LegacyContext>();
        await using var checkedLegacy = CreateContext<CheckedLegacyContext>();
        var legacyRow = $"INSERT INTO {Q("legacy_values")} ({Q("Id")}, {Q("Adjustment")}, {Q("Score")}) VALUES (1, -11, 100)";
        await legacy.Database.ExecuteSqlRawAsync(legacyRow, ct);
        legacy.AddRange(
            new LegacyAnimal { Id = 1, Score = Score.CreateUnchecked(101) },
            new LegacyDog { Id = 2, Score = Score.Create(100), Adjustment = Adjustment.CreateUnchecked(11) },
            new LegacyVehicle { Id = 1, Score = Score.Create(50) },
            new LegacyTruck { Id = 2, Score = Score.Create(50), Adjustment = Adjustment.CreateUnchecked(-11) });
        await legacy.SaveChangesAsync(ct);

        var differ = checkedLegacy.GetService<IMigrationsModelDiffer>();
        var after = checkedLegacy.GetService<IDesignTimeModel>().Model;
        var added = differ.GetDifferences(legacy.GetService<IDesignTimeModel>().Model.GetRelationalModel(), after.GetRelationalModel())
            .OfType<AddCheckConstraintOperation>()
            .Single(static add => add.Name == "CK_legacy_values_Adjustment_Minimum");
        var alter = checkedLegacy.GetService<IMigrationsSqlGenerator>().Generate([added], after).Single();

        var migrate = () => checkedLegacy.Database.ExecuteSqlRawAsync(alter.CommandText, ct);

        var refusal = (await migrate.Should().ThrowAsync<DbException>()).Which;
        Refused(refusal, "CK_legacy_values_Adjustment_Minimum", ValueObjectErrorCodes.OutOfRange);
        (await AuditAsync(checkedLegacy)).Should().BeEquivalentTo(
            "CK_legacy_values_Adjustment_Minimum: 1",
            "CK_legacy_values_Adjustment_Maximum: 0",
            "CK_legacy_values_Score_Maximum: 0",
            "CK_legacy_animals_Score_Maximum: 1",
            "CK_legacy_animals_Adjustment_Minimum: 0",
            "CK_legacy_animals_Adjustment_Maximum: 1",
            "CK_legacy_vehicles_Score_Maximum: 0",
            "CK_legacy_trucks_Adjustment_Minimum: 1",
            "CK_legacy_trucks_Adjustment_Maximum: 0");
    }

    /// <summary>
    /// Tables whose constraint names read alike, <c>clash_a_b</c> with column <c>C</c> and <c>clash_a</c> with column
    /// <c>B_C</c>, a complex property's column <c>Line_Score</c> and a table <c>clash_orders_Line</c>, are created side
    /// by side, SQL Server keeping a constraint name per schema, and each refuses another writer's row under its own
    /// constraint; the catalog lists the model's names.
    /// </summary>
    [Fact]
    public async Task Tables_whose_constraint_names_read_alike_are_created_side_by_side()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = CreateContext<ClashContext>();
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(ct);
        var tables = context.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables
            .ToDictionary(static table => table.Name, static table => table.CheckConstraints.Single().Name!);

        var sql = $"SELECT constraint_name AS {Q("Value")} FROM information_schema.check_constraints WHERE constraint_name LIKE 'CK_clash%'";
        var listed = await context.Database.SqlQueryRaw<string>(sql).ToListAsync(ct);

        listed.Should().BeEquivalentTo(tables.Values);
        foreach (var (table, column) in ((string Table, string Column)[])[("clash_a_b", "C"), ("clash_a", "B_C"), ("clash_orders", "Line_Score"), ("clash_orders_Line", "Score")])
        {
            var row = $"INSERT INTO {Q(table)} ({Q("Id")}, {Q(column)}) VALUES (1, 101)";

            var insert = () => context.Database.ExecuteSqlRawAsync(row, ct);

            Refused((await insert.Should().ThrowAsync<DbException>()).Which, tables[table], ValueObjectErrorCodes.OutOfRange);
        }
    }

    /// <summary>
    /// <c>SaveChanges</c> of an instance a lax read let in, which breaks a rule, fails with a <c>DbUpdateException</c>
    /// around the provider's error, which carries the code of the rule, read through the <c>DbUpdateException</c>.
    /// </summary>
    /// <param name="async">Whether <c>SaveChangesAsync</c> is called.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_lax_instance_written_back_fails_SaveChanges_with_the_code_on_the_providers_error(bool async)
    {
        await EnsureTablesAsync();
        await using var context = CreateContext<CheckedContext>();
        context.Rows.Add(new CheckedValues { Id = NextId(), Score = Score.CreateUnchecked(101) });

        var update = (await Save(context, async).Should().ThrowAsync<DbUpdateException>()).Which;

        Refused(update.InnerException.Should().BeAssignableTo<DbException>().Which, "CK_checked_values_Score_Maximum", ValueObjectErrorCodes.OutOfRange);
        ValueObjectErrors.TryGetCode(update, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// A refusal in a statement after the first of a batch, one that returns rows, reaches no command interceptor: the
    /// provider reports it while Entity Framework Core reads the results, a key the database generates for each row here,
    /// in SQL Server's <c>MERGE</c> of two rows of a table or in its second <c>INSERT … OUTPUT</c>, and in PostgreSQL's
    /// second <c>INSERT … RETURNING</c>. The provider's error carries the code all the same, which the save interceptor
    /// adds.
    /// </summary>
    /// <param name="twoTables">Whether the rows lie in two tables rather than one.</param>
    /// <param name="async">Whether <c>SaveChangesAsync</c> is called.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task A_refusal_in_a_later_statement_of_a_batch_carries_the_code_through_the_save_interceptor(bool twoTables, bool async)
    {
        await EnsureTablesAsync();
        var recorder = new FailureRecorder();
        await using var context = Create<BatchContext>(fixture, interceptors: recorder);
        context.Add(new BatchFirst { Score = Score.Create(1) });
        if (twoTables)
        {
            context.Add(new BatchSecond { Adjustment = Adjustment.CreateUnchecked(11) });
        }
        else
        {
            context.Add(new BatchFirst { Score = Score.CreateUnchecked(101) });
        }

        var update = (await Save(context, async).Should().ThrowAsync<DbUpdateException>()).Which;

        recorder.Failures.Should().Be(0, "the provider reports the refusal while the results are read");
        var constraint = twoTables ? "CK_batch_seconds_Adjustment_Maximum" : "CK_batch_firsts_Score_Maximum";
        Refused(update.InnerException.Should().BeAssignableTo<DbException>().Which, constraint, ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>An <c>ExecuteUpdate</c> setting a column to a value a rule refuses fails with the provider's error as it is, which carries the code.</summary>
    [Fact]
    public async Task An_ExecuteUpdate_breaking_a_rule_fails_with_the_providers_error_carrying_the_code()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnsureTablesAsync();
        await using var context = CreateContext<CheckedContext>();
        var id = NextId();
        context.Rows.Add(new CheckedValues { Id = id, Score = Score.Create(50) });
        await context.SaveChangesAsync(ct);

        var update = () => context.Rows.Where(row => row.Id == id).ExecuteUpdateAsync(set => set.SetProperty(static row => row.Score, Score.CreateUnchecked(101)), ct);

        Refused((await update.Should().ThrowAsync<DbException>()).Which, "CK_checked_values_Score_Maximum", ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// A raw SQL query that writes a row and reads it back, <c>SqlQueryRaw</c> of its key or <c>FromSqlRaw</c> of the
    /// whole row, fails with the provider's error as it is. PostgreSQL refuses the row as the command runs, where Entity
    /// Framework Core hands the interceptor the error, which carries the code. SqlClient reports the refusal while the
    /// rows of SQL Server's <c>OUTPUT</c> are read, where Entity Framework Core hands no interceptor the error, which
    /// carries none, as the guide says: <c>ValueObjectCheckConstraints.TryGetCode</c> reads the code off it. An Entity
    /// Framework Core that comes to report it would fail here.
    /// </summary>
    /// <param name="wholeRow">Whether the query reads back the whole row, as entities, rather than its key.</param>
    /// <param name="async">Whether the asynchronous methods are called.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task A_raw_query_reading_back_the_rows_it_writes_carries_the_code_unless_they_are_refused_while_read(bool wholeRow, bool async)
    {
        var ct = TestContext.Current.CancellationToken;
        await EnsureTablesAsync();
        var recorder = new FailureRecorder();
        await using var context = Create<CheckedContext>(fixture, interceptors: recorder);
        var sql = InsertReturning(NextId(), "Score", "101", wholeRow);

        Func<Task> query = wholeRow
            ? async ? () => context.Rows.FromSqlRaw(sql).ToListAsync(ct) : () => Task.FromResult(context.Rows.FromSqlRaw(sql).ToList())
            : async ? () => context.Database.SqlQueryRaw<int>(sql).ToListAsync(ct) : () => Task.FromResult(context.Database.SqlQueryRaw<int>(sql).ToList());

        var refusal = (await query.Should().ThrowAsync<DbException>()).Which;

        if (RefusesWhileReading)
        {
            Refused(refusal, "CK_checked_values_Score_Maximum");
            recorder.Failures.Should().Be(0, "SqlClient reports the refusal while the rows are read");
            refusal.Data.Contains(ValueObjectErrors.ErrorCodeKey).Should().BeFalse();
            ValueObjectErrors.TryGetCode(refusal, out _).Should().BeFalse();
            ValueObjectCheckConstraints.TryGetCode(refusal, out var code).Should().BeTrue();
            code.Should().Be(ValueObjectErrorCodes.OutOfRange);
        }
        else
        {
            Refused(refusal, "CK_checked_values_Score_Maximum", ValueObjectErrorCodes.OutOfRange);
            recorder.Failures.Should().Be(1, "the provider refuses the row as the command runs");
        }
    }

    /// <summary>
    /// Without <c>UseValueObjectCheckErrors()</c>, the provider's error is left as it is, and
    /// <c>ValueObjectCheckConstraints.TryGetCode</c> reads the code off it in a <c>catch</c>.
    /// </summary>
    [Fact]
    public async Task Without_the_registration_the_helper_reads_the_code_off_the_providers_error()
    {
        await EnsureTablesAsync();
        await using var context = Create<CheckedContext>(fixture, checkErrors: false);
        var insert = $"INSERT INTO {Q("checked_values")} ({Q("Id")}, {Q("Iban")}) VALUES ({NextId()}, 'FR7630006000')";

        var write = () => context.Database.ExecuteSqlRawAsync(insert, TestContext.Current.CancellationToken);

        var refusal = (await write.Should().ThrowAsync<DbException>()).Which;
        Refused(refusal, "CK_checked_values_Iban_MinLength");
        refusal.Data.Contains(ValueObjectErrors.ErrorCodeKey).Should().BeFalse();
        ValueObjectErrors.TryGetCode(refusal, out _).Should().BeFalse();
        ValueObjectCheckConstraints.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.TooShort);
    }

    /// <summary>
    /// An error naming a constraint for another reason carries no code: adding a constraint of the convention a second
    /// time, as a migration run twice would, fails with SQL Server's 2714 or PostgreSQL's 42710, which name it.
    /// </summary>
    [Fact]
    public async Task An_error_naming_a_constraint_for_another_reason_carries_no_code()
    {
        await EnsureTablesAsync();
        await using var context = CreateContext<CheckedContext>();
        var add = $"ALTER TABLE {Q("checked_values")} ADD CONSTRAINT {Q("CK_checked_values_Score_Maximum")} CHECK ({Q("Score")} <= 100)";

        var migrate = () => context.Database.ExecuteSqlRawAsync(add, TestContext.Current.CancellationToken);

        var refusal = (await migrate.Should().ThrowAsync<DbException>()).Which;
        Duplicated(refusal);
        refusal.Message.Should().Contain("CK_checked_values_Score_Maximum");
        refusal.Data.Contains(ValueObjectErrors.ErrorCodeKey).Should().BeFalse();
        ValueObjectCheckConstraints.TryGetCode(refusal, out _).Should().BeFalse();
    }

    /// <summary>Asserts that the engine refused a row on the constraint named, as the provider reports it.</summary>
    /// <param name="refusal">What the provider threw.</param>
    /// <param name="constraint">The constraint.</param>
    protected abstract void Refused(DbException refusal, string constraint);

    /// <summary>Asserts that the engine refused to create an object it already holds, as the provider reports it.</summary>
    /// <param name="refusal">What the provider threw.</param>
    protected abstract void Duplicated(DbException refusal);

    /// <summary>Writes an <c>INSERT</c> of one row of <c>checked_values</c> that returns the row, as the engine writes one.</summary>
    /// <param name="id">The key of the row.</param>
    /// <param name="column">The column set.</param>
    /// <param name="literal">Its value, as SQL.</param>
    /// <param name="wholeRow">Whether every column of the row is returned, rather than its key as <c>Value</c>.</param>
    /// <returns>The statement.</returns>
    protected abstract string InsertReturning(int id, string column, string literal, bool wholeRow);

    /// <summary>Creates the tables once per fixture.</summary>
    /// <returns>A task that completes once the tables exist.</returns>
    protected Task EnsureTablesAsync() => Tables.GetValue(fixture, static database => CreateTablesAsync(database));

    /// <summary>Gets a key no row of the tables holds yet.</summary>
    /// <returns>The key.</returns>
    protected static int NextId() => Interlocked.Increment(ref _lastId);

    /// <summary>
    /// Asserts that the engine refused a row on the constraint named, and that the provider's error carries the code of
    /// its rule, which <c>UseValueObjectCheckErrors()</c> adds and both readers read.
    /// </summary>
    private void Refused(DbException refusal, string constraint, string code)
    {
        Refused(refusal, constraint);
        refusal.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(code);
        ValueObjectErrors.TryGetCode(refusal, out var read).Should().BeTrue();
        read.Should().Be(code);
        ValueObjectCheckConstraints.TryGetCode(refusal, out var named).Should().BeTrue();
        named.Should().Be(code);
    }

    private static Func<Task> Save(DbContext context, bool async)
        => async
            ? () => context.SaveChangesAsync(TestContext.Current.CancellationToken)
            : () => Task.FromResult(context.SaveChanges());

    /// <summary>
    /// The audit query of the guide: how many rows of each table each constraint refuses, read from the relational model
    /// the constraints are written in, one line per constraint, as the guide prints them.
    /// </summary>
    private static async Task<List<string>> AuditAsync(DbContext db)
    {
        var refusals = new List<string>();
        var sql = db.GetService<ISqlGenerationHelper>();
        foreach (var table in db.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables)
        {
            foreach (var check in table.CheckConstraints)
            {
                var count = $"SELECT COUNT(*) AS {sql.DelimitIdentifier("Value")} FROM {sql.DelimitIdentifier(table.Name, table.Schema)} WHERE NOT ({check.Sql})";
                var refused = await db.Database.SqlQueryRaw<int>(count).SingleAsync(TestContext.Current.CancellationToken);
                refusals.Add($"{check.Name}: {refused}");
            }
        }

        return refusals;
    }

    private IEnumerable<(string Label, Action<CheckedValues> Set)> Accepted()
    {
        var kind = InstantKind;
        yield return ("Iban", static row => row.Iban = Iban.Create("FR7630006000011234567890189"));
        yield return ("Iban at its minimum", static row => row.Iban = Iban.Create("NO9386011117947"));
        yield return ("Code", static row => row.Code = TrackingCode.Create("abcde"));
        yield return ("Code ending with a space", static row => row.Code = TrackingCode.Create("abcd "));
        yield return ("Code with an emoji", static row => row.Code = TrackingCode.Create("abc\U0001F600"));
        yield return ("Code of emojis", static row => row.Code = TrackingCode.Create("\U0001F600\U0001F600\U0001F600"));
        yield return ("Payment", static row => row.Payment = PaymentId.New());
        yield return ("Label", static row => row.Label = Label.Create(string.Empty));
        yield return ("Grade A", static row => row.Grade = Grade.Create('A'));
        yield return ("Grade F", static row => row.Grade = Grade.Create('F'));
        yield return ("Grade f", static row => row.Grade = Grade.Create('f'));
        yield return ("Adjustment -10", static row => row.Adjustment = Adjustment.Create(-10));
        yield return ("Adjustment 10", static row => row.Adjustment = Adjustment.Create(10));
        yield return ("Score 0", static row => row.Score = Score.Create(0));
        yield return ("Score 100", static row => row.Score = Score.Create(100));
        yield return ("Port 1", static row => row.Port = Port.Create(1));
        yield return ("Port 65535", static row => row.Port = Port.Create(ushort.MaxValue));
        yield return ("Page 1", static row => row.Page = PageNumber.Create(1));
        yield return ("Page max", static row => row.Page = PageNumber.Create(int.MaxValue));
        yield return ("FileSize 0", static row => row.FileSize = FileSize.Create(0));
        yield return ("FileSize max", static row => row.FileSize = FileSize.Create(long.MaxValue));
        yield return ("Counter 1", static row => row.Counter = Counter.Create(1));
        yield return ("Counter max - 1", static row => row.Counter = Counter.Create(ulong.MaxValue - 1));
        yield return ("Latitude -90", static row => row.Latitude = Latitude.Create(-90));
        yield return ("Latitude 90", static row => row.Latitude = Latitude.Create(90));
        yield return ("Tolerance 1", static row => row.Tolerance = Tolerance.Create(1));
        yield return ("Tolerance far below", static row => row.Tolerance = Tolerance.Create(-1e300));
        yield return ("Ratio 0", static row => row.Ratio = Ratio.Create(0));
        yield return ("Ratio 0.1", static row => row.Ratio = Ratio.Create(0.1f));
        yield return ("Ratio 1", static row => row.Ratio = Ratio.Create(1));
        yield return ("Fraction 0.1", static row => row.Fraction = Fraction.Create(0.1f));
        yield return ("Fraction 0.7", static row => row.Fraction = Fraction.Create(0.7f));
        yield return ("Amount 0", static row => row.Amount = Amount.Create(0m));
        yield return ("Fee 0.001", static row => row.Fee = Fee.Create(0.001m));
        yield return ("Fee 9.995", static row => row.Fee = Fee.Create(9.995m));
        yield return ("Fee 9.9950", static row => row.Fee = Fee.Create(9.9950m));
        yield return ("Initial space", static row => row.Initial = Initial.Create(' '));
        yield return ("Initial z", static row => row.Initial = Initial.Create('z'));
        yield return ("Initial A", static row => row.Initial = Initial.Create('A'));
        yield return ("Opening 06:00", static row => row.Opening = OpeningTime.Create(new TimeOnly(6, 0)));
        yield return ("Opening 12:00", static row => row.Opening = OpeningTime.Create(new TimeOnly(12, 0)));
        yield return ("Opening 06:00 and a tick", static row => row.Opening = OpeningTime.Create(new TimeOnly(6, 0).Add(TimeSpan.FromTicks(1))));
        yield return ("Effective 2000-01-01", static row => row.Effective = EffectiveDate.Create(new DateOnly(2000, 1, 1)));
        yield return ("Recorded 2000-01-01", row => row.Recorded = RecordedAt.Create(new DateTime(2000, 1, 1, 0, 0, 0, kind)));
        yield return ("Recorded 2099-12-31", row => row.Recorded = RecordedAt.Create(new DateTime(2099, 12, 31, 0, 0, 0, kind)));
        yield return ("Recorded 2000-01-01 and a tick", row => row.Recorded = RecordedAt.Create(new DateTime(2000, 1, 1, 0, 0, 0, kind).AddTicks(1)));
        yield return ("Stamp at its minimum", row => row.Stamp = Stamp.Create(new DateTime(2000, 1, 1, 0, 0, 0, kind).AddTicks(1)));
        yield return ("Stamp a second later", row => row.Stamp = Stamp.Create(new DateTime(2000, 1, 1, 0, 0, 1, kind)));
        yield return ("Occurred 2000-01-01Z", static row => row.Occurred = OccurredAt.Create(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        yield return ("Deadline at its minimum", static row => row.Deadline = Deadline.Create(new DateTimeOffset(1999, 12, 31, 22, 0, 0, TimeSpan.Zero)));
        yield return ("Deadline at its maximum", static row => row.Deadline = Deadline.Create(new DateTimeOffset(2030, 1, 1, 5, 0, 0, TimeSpan.Zero)));
        yield return ("Duration 0", static row => row.Duration = Duration.Create(TimeSpan.Zero));
        yield return ("Duration 23:59:59", static row => row.Duration = Duration.Create(new TimeSpan(23, 59, 59)));
        yield return ("Window 1h", static row => row.Window = Window.Create(TimeSpan.FromHours(1)));
        yield return ("Window 10h", static row => row.Window = Window.Create(TimeSpan.FromHours(10)));
        yield return ("Country FR", static row => row.Country = CountryCode.France);
        yield return ("Country fr", static row => row.Country = CountryCode.Create("fr"));
        yield return ("Status draft", static row => row.Status = DocumentStatus.Create("draft"));
        yield return ("Status DRAFT", static row => row.Status = DocumentStatus.Create("DRAFT"));
        yield return ("Unit kg", static row => row.Unit = MeasureUnit.Create("kg"));
        yield return ("Rate 0.5", static row => row.Rate = Rate.Create(0.5m));
        yield return ("Rate 1.00", static row => row.Rate = Rate.Create(1.00m));
        yield return ("Rate -0.0", static row => row.Rate = Rate.Create(-0.0m));
        yield return ("Factor 0.1", static row => row.Factor = Factor.Create(0.1f));
        yield return ("Factor 0.5", static row => row.Factor = Factor.Create(0.5f));
        yield return ("Milestone 2020Z", static row => row.Milestone = Milestone.Create(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        if (KeepsOffsets)
        {
            yield return ("Occurred at +01:00", static row => row.Occurred = OccurredAt.Create(new DateTimeOffset(2000, 1, 1, 1, 0, 0, TimeSpan.FromHours(1))));
            yield return ("Deadline at +09:00", static row => row.Deadline = Deadline.Create(new DateTimeOffset(2030, 1, 1, 14, 0, 0, TimeSpan.FromHours(9))));
            yield return ("Milestone at +01:00", static row => row.Milestone = Milestone.Create(new DateTimeOffset(2020, 1, 1, 1, 0, 0, TimeSpan.FromHours(1))));
        }

        if (StoresAnyDuration)
        {
            yield return ("Duration 1 day", static row => row.Duration = Duration.Create(TimeSpan.FromDays(1)));
            yield return ("Window -2h", static row => row.Window = Window.Create(TimeSpan.FromHours(-2)));
            yield return ("Window 2 days", static row => row.Window = Window.Create(TimeSpan.FromDays(2)));
        }
    }

    private static async Task CreateTablesAsync(DatabaseFixture database)
    {
        await using var values = Create<CheckedContext>(database);
        await values.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        await using var legacy = Create<LegacyContext>(database);
        await legacy.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        await using var batch = Create<BatchContext>(database);
        await batch.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    private static TContext Create<TContext>(DatabaseFixture database, bool checkErrors = true, params IInterceptor[] interceptors)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        database.Configure(builder, database.ConnectionString);
        builder.AddInterceptors(interceptors);
        if (checkErrors)
        {
            builder.UseValueObjectCheckErrors();
        }

        return (TContext)Activator.CreateInstance(typeof(TContext), builder.Options)!;
    }

    private TContext CreateContext<TContext>()
        where TContext : DbContext
        => Create<TContext>(fixture);

    private string Q(string identifier) => fixture.Quote(identifier);

    /// <summary>A value object of each kind the rules concern, each optional, so that a row sets one at a time.</summary>
    protected sealed class CheckedValues
    {
        public int Id { get; set; }

        public Iban? Iban { get; set; }

        public TrackingCode? Code { get; set; }

        public PaymentId? Payment { get; set; }

        public Label? Label { get; set; }

        public Grade? Grade { get; set; }

        public Adjustment? Adjustment { get; set; }

        public Score? Score { get; set; }

        public Port? Port { get; set; }

        public PageNumber? Page { get; set; }

        public FileSize? FileSize { get; set; }

        public Counter? Counter { get; set; }

        public Latitude? Latitude { get; set; }

        public Tolerance? Tolerance { get; set; }

        public Ratio? Ratio { get; set; }

        public Fraction? Fraction { get; set; }

        public Amount? Amount { get; set; }

        public Fee? Fee { get; set; }

        public Initial? Initial { get; set; }

        public OpeningTime? Opening { get; set; }

        public EffectiveDate? Effective { get; set; }

        public RecordedAt? Recorded { get; set; }

        public Stamp? Stamp { get; set; }

        public OccurredAt? Occurred { get; set; }

        public Deadline? Deadline { get; set; }

        public Duration? Duration { get; set; }

        public Window? Window { get; set; }

        public CountryCode? Country { get; set; }

        public DocumentStatus? Status { get; set; }

        public MeasureUnit? Unit { get; set; }

        public Rate? Rate { get; set; }

        public Factor? Factor { get; set; }

        public Milestone? Milestone { get; set; }
    }

    /// <summary>A table written before the constraints existed.</summary>
    private sealed class LegacyValues
    {
        public int Id { get; set; }

        public Adjustment Adjustment { get; set; }

        public Score Score { get; set; }
    }

    /// <summary>The base type of a table-per-hierarchy, written before the constraints existed.</summary>
    private class LegacyAnimal
    {
        public int Id { get; set; }

        public Score Score { get; set; }
    }

    /// <summary>A derived type sharing its base type's table.</summary>
    private sealed class LegacyDog : LegacyAnimal
    {
        public Adjustment Adjustment { get; set; }
    }

    /// <summary>The base type of a table-per-type hierarchy, written before the constraints existed.</summary>
    private class LegacyVehicle
    {
        public int Id { get; set; }

        public Score Score { get; set; }
    }

    /// <summary>A derived type in a table of its own, holding no column of its base type's but the key.</summary>
    private sealed class LegacyTruck : LegacyVehicle
    {
        public Adjustment Adjustment { get; set; }
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

    /// <summary>The values, mapped by the conventions, with the check constraints.</summary>
    /// <param name="options">Options bound to the running container.</param>
    protected sealed class CheckedContext(DbContextOptions<CheckedContext> options) : DbContext(options)
    {
        public DbSet<CheckedValues> Rows => Set<CheckedValues>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder
                .ConfigureValueObjects(typeof(Iban).Assembly, typeof(Grade).Assembly)
                .ConfigureEntityIds(typeof(PaymentId).Assembly)
                .AddValueObjectCheckConstraints();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CheckedValues>(values =>
            {
                values.ToTable("checked_values");
                values.Property(static entity => entity.Id).ValueGeneratedNever();
                values.Property(static entity => entity.Amount).HasPrecision(18, 2);
                values.Property(static entity => entity.Fee).HasPrecision(18, 4);
                values.Property(static entity => entity.Rate).HasPrecision(18, 2);
            });
    }

    /// <summary>Tables whose constraint names read alike.</summary>
    /// <param name="options">Options bound to the running container.</param>
    private sealed class ClashContext(DbContextOptions<ClashContext> options) : DbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(typeof(Grade).Assembly).AddValueObjectCheckConstraints();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClashAB>(clash =>
            {
                clash.ToTable("clash_a_b");
                clash.Property(static entity => entity.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<ClashA>(clash =>
            {
                clash.ToTable("clash_a");
                clash.Property(static entity => entity.Id).ValueGeneratedNever();
                clash.Property(static entity => entity.BC).HasColumnName("B_C");
            });
            modelBuilder.Entity<ClashOrder>(order =>
            {
                order.ToTable("clash_orders");
                order.Property(static entity => entity.Id).ValueGeneratedNever();
                order.ComplexProperty(static entity => entity.Line);
            });
            modelBuilder.Entity<ClashOrderLine>(line =>
            {
                line.ToTable("clash_orders_Line");
                line.Property(static entity => entity.Id).ValueGeneratedNever();
            });
        }
    }

    /// <summary>A score, its key generated by the database.</summary>
    private sealed class BatchFirst
    {
        public int Id { get; set; }

        public Score Score { get; set; }
    }

    /// <summary>An adjustment, its key generated by the database, in a table that sorts after <see cref="BatchFirst"/>'s.</summary>
    private sealed class BatchSecond
    {
        public int Id { get; set; }

        public Adjustment Adjustment { get; set; }
    }

    /// <summary>Tables whose keys the database generates, so that each statement of a batch returns rows.</summary>
    /// <param name="options">Options bound to the running container.</param>
    private sealed class BatchContext(DbContextOptions<BatchContext> options) : DbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(typeof(Grade).Assembly).AddValueObjectCheckConstraints();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BatchFirst>().ToTable("batch_firsts");
            modelBuilder.Entity<BatchSecond>().ToTable("batch_seconds");
        }
    }

    /// <summary>Counts the errors Entity Framework Core hands a command interceptor.</summary>
    private sealed class FailureRecorder : DbCommandInterceptor
    {
        public int Failures { get; private set; }

        public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) => Failures++;

        public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            Failures++;

            return Task.CompletedTask;
        }
    }

    /// <summary>The legacy table, as it was mapped before the constraints.</summary>
    /// <param name="options">Options bound to the running container.</param>
    private class LegacyContext(DbContextOptions options) : DbContext(options)
    {
        protected virtual bool Checked => false;

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ConfigureValueObjects(typeof(Grade).Assembly);
            if (Checked)
            {
                configurationBuilder.AddValueObjectCheckConstraints();
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LegacyValues>(values =>
            {
                values.ToTable("legacy_values");
                values.Property(static entity => entity.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<LegacyAnimal>(animal =>
            {
                animal.ToTable("legacy_animals");
                animal.Property(static entity => entity.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<LegacyDog>();
            modelBuilder.Entity<LegacyVehicle>(vehicle =>
            {
                vehicle.UseTptMappingStrategy().ToTable("legacy_vehicles");
                vehicle.Property(static entity => entity.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<LegacyTruck>().ToTable("legacy_trucks");
        }
    }

    /// <summary>The legacy table, with the check constraints.</summary>
    /// <param name="options">Options bound to the running container.</param>
    private sealed class CheckedLegacyContext(DbContextOptions options) : LegacyContext(options)
    {
        protected override bool Checked => true;
    }
}

/// <summary>Runs the CHECK constraints against PostgreSQL.</summary>
/// <param name="fixture">PostgreSQL container.</param>
public sealed class PostgreSqlCheckConstraintTests(PostgreSqlFixture fixture) : CheckConstraintTests<PostgreSqlFixture>(fixture)
{
    /// <inheritdoc />
    protected override bool KeepsOffsets => false;

    /// <inheritdoc />
    protected override bool StoresAnyDuration => true;

    /// <inheritdoc />
    protected override DateTimeKind InstantKind => DateTimeKind.Utc;

    /// <inheritdoc />
    protected override bool ComparesCase => true;

    /// <inheritdoc />
    protected override bool RefusesWhileReading => false;

    /// <inheritdoc />
    protected override void Refused(DbException refusal, string constraint)
    {
        var postgres = refusal.Should().BeOfType<PostgresException>().Which;
        postgres.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        postgres.ConstraintName.Should().Be(constraint);
    }

    /// <inheritdoc />
    protected override void Duplicated(DbException refusal)
        => refusal.Should().BeOfType<PostgresException>().Which.SqlState.Should().Be(PostgresErrorCodes.DuplicateObject);

    /// <inheritdoc />
    protected override string InsertReturning(int id, string column, string literal, bool wholeRow)
        => $"INSERT INTO \"checked_values\" (\"Id\", \"{column}\") VALUES ({id}, {literal}) RETURNING {(wholeRow ? "*" : "\"Id\" AS \"Value\"")}";
}

/// <summary>Runs the CHECK constraints against SQL Server.</summary>
/// <param name="fixture">SQL Server container.</param>
public sealed class SqlServerCheckConstraintTests(SqlServerFixture fixture) : CheckConstraintTests<SqlServerFixture>(fixture)
{
    /// <inheritdoc />
    protected override bool KeepsOffsets => true;

    /// <inheritdoc />
    protected override bool StoresAnyDuration => false;

    /// <inheritdoc />
    protected override DateTimeKind InstantKind => DateTimeKind.Unspecified;

    /// <inheritdoc />
    protected override bool ComparesCase => false;

    /// <inheritdoc />
    protected override bool RefusesWhileReading => true;

    /// <inheritdoc />
    protected override void Refused(DbException refusal, string constraint)
    {
        var sqlServer = refusal.Should().BeOfType<SqlException>().Which;
        sqlServer.Number.Should().Be(547);
        sqlServer.Message.Should().Contain($"CHECK constraint \"{constraint}\"");
    }

    /// <inheritdoc />
    protected override void Duplicated(DbException refusal) => refusal.Should().BeOfType<SqlException>().Which.Number.Should().Be(2714);

    /// <inheritdoc />
    protected override string InsertReturning(int id, string column, string literal, bool wholeRow)
        => $"INSERT INTO [checked_values] ([Id], [{column}]) OUTPUT {(wholeRow ? "INSERTED.*" : "INSERTED.[Id] AS [Value]")} VALUES ({id}, {literal})";

    /// <summary>
    /// SQL Server writes its messages in the language of the login, which the connection names: the name of the
    /// constraint is read off error 547 in each language SQL Server translates it into, quoted with ASCII or typographic
    /// quotation marks, or not quoted and followed by a full stop, and the provider's error carries the code of its rule.
    /// </summary>
    [Fact]
    public async Task The_code_is_read_in_every_language_SQL_Server_writes_its_messages_in()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnsureTablesAsync();
        var languages = new List<string>();
        await using (var connection = new SqlConnection(Fixture.ConnectionString))
        {
            await connection.OpenAsync(ct);
            await using var list = new SqlCommand(
                "SELECT l.name FROM sys.syslanguages AS l WHERE EXISTS (SELECT 1 FROM sys.messages AS m WHERE m.message_id = 547 AND m.language_id = l.msglangid)",
                connection);
            await using var reader = await list.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                languages.Add(reader.GetString(0));
            }
        }

        var messages = new List<string>();
        var unread = new List<string>();
        foreach (var language in languages)
        {
            var connectionString = new SqlConnectionStringBuilder(Fixture.ConnectionString) { CurrentLanguage = language }.ConnectionString;
            await using var context = new CheckedContext(new DbContextOptionsBuilder<CheckedContext>().UseSqlServer(connectionString).UseValueObjectCheckErrors().Options);
            var insert = $"INSERT INTO [checked_values] ([Id], [Score]) VALUES ({NextId()}, 101)";

            var write = () => context.Database.ExecuteSqlRawAsync(insert, ct);

            var refusal = (await write.Should().ThrowAsync<SqlException>()).Which;
            messages.Add(refusal.Message);
            if (refusal.Number != 547 || !ValueObjectErrors.TryGetCode(refusal, out var code) || code != ValueObjectErrorCodes.OutOfRange)
            {
                unread.Add($"{language}: {refusal.Message}");
            }
        }

        languages.Should().HaveCountGreaterThanOrEqualTo(20);
        messages.Distinct().Should().HaveCountGreaterThanOrEqualTo(20, "each language writes its own message, but British English and US English");
        unread.Should().BeEmpty();
    }
}
