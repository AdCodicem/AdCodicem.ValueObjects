using System.Collections;
using System.Data.Common;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.TestData;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// The code of the rule a CHECK constraint of <c>AddValueObjectCheckConstraints</c> refused a row under:
/// <c>UseValueObjectCheckErrors()</c> adds it to the provider's error, under <see cref="ValueObjectErrors.ErrorCodeKey"/>,
/// and <see cref="ValueObjectCheckConstraints.TryGetCode"/> reads it without.
/// </summary>
/// <remarks>
/// SQLite in memory runs the constraints, each test on a database of its own. The errors of PostgreSQL and SQL Server are
/// built as their providers build them: Npgsql's has a public constructor, SqlClient's none, so an error carrying the data
/// SqlClient adds to one stands for it. The integration suite reads the real ones, on both servers, a refusal only a save
/// interceptor sees among them, and SQL Server's in every language it writes its messages in.
/// </remarks>
public sealed class EntityFrameworkCoreCheckErrorTests : IDisposable
{
    /// <summary>SQL Server's message 547 for a CHECK constraint, in each language that translates it, as SQL Server 2022 writes it.</summary>
    private static readonly string[] SqlServerMessages =
    [
        "The INSERT statement conflicted with the CHECK constraint \"CK_rows_Score_Maximum\". The conflict occurred in database \"shop\", table \"dbo.rows\", column 'Score'.",
        "Příkaz INSERT způsobil konflikt s omezením CHECK s názvem CK_rows_Score_Maximum. Ke konfliktu došlo v databázi shop, tabulce dbo.rows, , column 'Score'.",
        "Sætningen INSERT kom i konflikt med CHECK-begrænsningen \"CK_rows_Score_Maximum\". Konflikten opstod i databasen \"shop\", tabellen \"dbo.rows\", column 'Score'.",
        "Die INSERT-Anweisung steht in Konflikt mit der CHECK-Einschränkung \"CK_rows_Score_Maximum\". Der Konflikt trat in der shop-Datenbank, Tabelle \"dbo.rows\", column 'Score' auf.",
        "Instrucción INSERT en conflicto con la restricción CHECK 'CK_rows_Score_Maximum'. El conflicto ha aparecido en la base de datos 'shop', tabla 'dbo.rows', column 'Score'.",
        "L'instruction INSERT est en conflit avec la contrainte CHECK \"CK_rows_Score_Maximum\". Le conflit s'est produit dans la base de données \"shop\", table \"dbo.rows\", column 'Score'.",
        "L'istruzione INSERT è in conflitto con il vincolo CHECK \"CK_rows_Score_Maximum\". Il conflitto si è verificato nella tabella \"dbo.rows\", column 'Score' del database \"shop\".",
        "A(z) INSERT utasítás ütközést eredményezett a(z) „CK_rows_Score_Maximum” CHECK megkötéssel. Az ütközés előfordulásának helye: „shop” adatbázis, „dbo.rows”, column 'Score' tábla.",
        "De instructie INSERT is strijdig met de CHECK-beperking CK_rows_Score_Maximum. Het conflict is opgetreden in database shop, tabel dbo.rows, column 'Score'.",
        "Setningen INSERT er i konflikt med begrensningen CK_rows_Score_Maximum i CHECK. Konflikten oppstod i databasen shop, tabell dbo.rows , column 'Score'.",
        "Instrukcja INSERT powoduje konflikt z ograniczeniem CHECK „CK_rows_Score_Maximum”. Konflikt występuje w bazie danych „shop” w tabeli „dbo.rows”, column 'Score'.",
        "Conflito entre a instrução INSERT e a restrição CHECK \"CK_rows_Score_Maximum\". O conflito ocorreu na base de dados \"shop\", tabela \"dbo.rows\", column 'Score'.",
        "A instrução INSERT conflitou com a restrição do CHECK \"CK_rows_Score_Maximum\". O conflito ocorreu no banco de dados \"shop\", tabela \"dbo.rows\", column 'Score'.",
        "Lauseke INSERT oli ristiriidassa kohteen CHECK rajoituksen CK_rows_Score_Maximum kanssa. Ristiriita ilmeni tietokannassa shop ja taulukossa dbo.rows, column 'Score'.",
        "En konflikt inträffade mellan instruktionen INSERT och CHECK-begränsningen \"CK_rows_Score_Maximum\". Konflikten inträffade i databasen \"shop\", tabell \"dbo.rows\", column 'Score'.",
        "INSERT deyimi CHECK kısıtlaması \"CK_rows_Score_Maximum\" ile çakıştı. \"shop\" veritabanı, \"dbo.rows\", column 'Score' tablosu içinde çakışma oluştu.",
        "Η πρόταση INSERT δημιούργησε διένεξη με τον περιορισμό CHECK \"CK_rows_Score_Maximum\". Η διένεξη παρουσιάστηκε στη βάση δεδομένων \"shop\", στον πίνακα \"dbo.rows\", column 'Score'.",
        "Конфликт инструкции INSERT с ограничением CHECK \"CK_rows_Score_Maximum\". Конфликт произошел в базе данных \"shop\", таблица \"dbo.rows\", column 'Score'.",
        "INSERT 문이 CHECK 제약 조건 \"CK_rows_Score_Maximum\"과(와) 충돌했습니다. 데이터베이스 \"shop\", 테이블 \"dbo.rows\", column 'Score'에서 충돌이 발생했습니다.",
        "INSERT ステートメントは CHECK 制約 \"CK_rows_Score_Maximum\" と競合しています。競合が発生したのは、データベース \"shop\"、テーブル \"dbo.rows\", column 'Score' です。",
        "INSERT 语句与 CHECK 约束\"CK_rows_Score_Maximum\"冲突。该冲突发生于数据库\"shop\"，表\"dbo.rows\", column 'Score'。",
        "INSERT 陳述式與 CHECK 條件約束 \"CK_rows_Score_Maximum\" 衝突。衝突發生在資料庫 \"shop\"，資料表 \"dbo.rows\", column 'Score'。",
    ];

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public EntityFrameworkCoreCheckErrorTests()
    {
        _connection.Open();
        using var context = Context();
        context.Database.EnsureCreated();
    }

    /// <summary>Gets SQL Server's message 547 in each language that translates it.</summary>
    public static TheoryData<int> SqlServerLanguages => [.. Enumerable.Range(0, SqlServerMessages.Length)];

    /// <summary>
    /// Gets messages naming <c>CK_rows_Score_Maximum</c> past each separator the reader knows: between each quotation mark,
    /// before each mark of punctuation that may end a sentence, between each kind of white space, and at the end.
    /// </summary>
    public static TheoryData<string> NamesInMessages =>
    [
        .. "\"'`«»‹›‘’‚“”„「」『』＂".Select(static quote => $"violates check constraint {quote}CK_rows_Score_Maximum{quote} on rows"),
        .. ".,;:!?。，、".Select(static punctuation => $"violates check constraint CK_rows_Score_Maximum{punctuation} on rows"),
        .. "\t\n\r\u00A0\u202F\u3000".Select(static space => $"violates check constraint{space}CK_rows_Score_Maximum{space}on rows"),
        "violates check constraint CK_rows_Score_Maximum",
    ];

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// <c>SaveChanges</c> of an instance a lax read let in, which breaks a rule, fails with a <c>DbUpdateException</c>
    /// around SQLite's error, which carries the code of the rule the constraint's name ends with, read through the
    /// <c>DbUpdateException</c>: the error is neither replaced nor wrapped, its message unchanged, and the
    /// <c>DbUpdateException</c> holds no entry of its own.
    /// </summary>
    /// <param name="broken">The member broken.</param>
    /// <param name="constraint">The constraint that refuses it.</param>
    /// <param name="code">The code of its rule.</param>
    /// <param name="async">Whether <c>SaveChangesAsync</c> is called.</param>
    [Theory]
    [InlineData("Iban", "CK_refusals_Iban_MinLength", ValueObjectErrorCodes.TooShort, false)]
    [InlineData("Iban", "CK_refusals_Iban_MinLength", ValueObjectErrorCodes.TooShort, true)]
    [InlineData("Quantity below", "CK_refusals_Quantity_Minimum", ValueObjectErrorCodes.OutOfRange, false)]
    [InlineData("Quantity above", "CK_refusals_Quantity_Maximum", ValueObjectErrorCodes.OutOfRange, true)]
    [InlineData("Country", "CK_refusals_Country_KnownValues", ValueObjectErrorCodes.NotAKnownValue, false)]
    [InlineData("Country", "CK_refusals_Country_KnownValues", ValueObjectErrorCodes.NotAKnownValue, true)]
    [InlineData("Backorder", "CK_refusals_Backorder_Maximum", ValueObjectErrorCodes.OutOfRange, false)]
    [InlineData("Backorder", "CK_refusals_Backorder_Maximum", ValueObjectErrorCodes.OutOfRange, true)]
    public async Task A_row_a_constraint_refuses_fails_SaveChanges_with_the_code_of_its_rule_on_the_providers_error(
        string broken,
        string constraint,
        string code,
        bool async)
    {
        await using var context = Context();
        var row = Valid();
        switch (broken)
        {
            case "Iban":
                row.Iban = Iban.CreateUnchecked("FR7630006000");
                break;
            case "Quantity below":
                row.Quantity = Quantity.CreateUnchecked(-1);
                break;
            case "Quantity above":
                row.Quantity = Quantity.CreateUnchecked(1001);
                break;
            case "Country":
                row.Country = CountryCode.CreateUnchecked("GB");
                break;
            default:
                row.Backorder = Quantity.CreateUnchecked(1001);
                break;
        }

        context.Refusals.Add(row);

        var update = (await Save(context, async).Should().ThrowAsync<DbUpdateException>()).Which;

        var refusal = update.InnerException.Should().BeOfType<SqliteException>().Which;
        refusal.SqliteErrorCode.Should().Be(19);
        refusal.Message.Should().Be($"SQLite Error 19: 'CHECK constraint failed: {constraint}'.");
        refusal.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(code);
        update.Data.Contains(ValueObjectErrors.ErrorCodeKey).Should().BeFalse();
        ValueObjectErrors.TryGetCode(update, out var read).Should().BeTrue();
        read.Should().Be(code);
        ValueObjectCheckConstraints.TryGetCode(update, out var named).Should().BeTrue();
        named.Should().Be(code);
    }

    /// <summary>
    /// An <c>ExecuteUpdate</c> setting a column to an invalid instance, which the converter writes as it is, raw SQL, which
    /// skips the converters, and a raw query reading back the row it writes, which SQLite refuses as the command runs, fail
    /// with SQLite's error as it is, which carries the code; a column that takes a <c>NULL</c>, the column of a
    /// construction of a generic value object, of an entity identifier and of a value object written by hand carry theirs.
    /// </summary>
    /// <param name="async">Whether the asynchronous methods are called.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_ExecuteUpdate_or_raw_SQL_breaking_a_rule_fails_with_the_providers_error_carrying_the_code(bool async)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = Context();
        var row = Valid();
        context.Refusals.Add(row);
        await context.SaveChangesAsync(ct);
        var id = row.Id;
        var rows = context.Refusals.Where(refusal => refusal.Id == id);
        var model = context.GetService<IDesignTimeModel>().Model.GetRelationalModel().FindTable("shapes", null)!;
        model.CheckConstraints.Select(static check => check.Name).Should().BeEquivalentTo(
            "CK_shapes_Stock_Minimum",
            "CK_shapes_Account_MinLength",
            "CK_shapes_Chapters_Minimum",
            "CK_shapes_Chapters_Maximum");

        Func<Task> update = async
            ? () => rows.ExecuteUpdateAsync(set => set.SetProperty(static refusal => refusal.Quantity, Quantity.CreateUnchecked(1001)), ct)
            : () => Task.FromResult(rows.ExecuteUpdate(set => set.SetProperty(static refusal => refusal.Quantity, Quantity.CreateUnchecked(1001))));
        Carries((await update.Should().ThrowAsync<SqliteException>()).Which, ValueObjectErrorCodes.OutOfRange);

        foreach (var (backorder, country, code) in ((string Backorder, string Country, string Code)[])
        [
            ("NULL", "'DE'", ValueObjectErrorCodes.NotAKnownValue),
            ("-1", "'FR'", ValueObjectErrorCodes.OutOfRange),
        ])
        {
            var insert = $"INSERT INTO \"refusals\" (\"Iban\", \"Quantity\", \"Backorder\", \"Country\") VALUES ('FR7630006000011234567890189', 1, {backorder}, {country})";
            Carries((await Raw(context, insert, async).Should().ThrowAsync<SqliteException>()).Which, code);
        }

        foreach (var (column, value, code) in ((string Column, string Value, string Code)[])
        [
            ("Stock", "-1", ValueObjectErrorCodes.OutOfRange),
            ("Account", "'acc_0123456789'", ValueObjectErrorCodes.TooShort),
            ("Chapters", "4", ValueObjectErrorCodes.OutOfRange),
            ("Chapters", "10", ValueObjectErrorCodes.OutOfRange),
        ])
        {
            var columns = column == "Chapters" ? "\"Chapters\"" : $"\"{column}\", \"Chapters\"";
            var values = column == "Chapters" ? value : $"{value}, 7";
            var insert = $"INSERT INTO \"shapes\" ({columns}) VALUES ({values})";
            Carries((await Raw(context, insert, async).Should().ThrowAsync<SqliteException>()).Which, code);
        }

        // SQLite makes every change of a statement returning rows on its first step, which runs as the command executes: a
        // raw query reading back the row it writes is refused there, where Entity Framework Core hands the interceptor the
        // error, unlike SQL Server's OUTPUT, which the integration suite runs.
        const string Inserted = "INSERT INTO \"refusals\" (\"Iban\", \"Quantity\", \"Country\") VALUES ('FR7630006000011234567890189', 1001, 'FR')";
        Func<Task> scalar = async
            ? () => context.Database.SqlQueryRaw<int>($"{Inserted} RETURNING \"Id\" AS \"Value\"").ToListAsync(ct)
            : () => Task.FromResult(context.Database.SqlQueryRaw<int>($"{Inserted} RETURNING \"Id\" AS \"Value\"").ToList());
        Func<Task> entities = async
            ? () => context.Refusals.FromSqlRaw($"{Inserted} RETURNING *").ToListAsync(ct)
            : () => Task.FromResult(context.Refusals.FromSqlRaw($"{Inserted} RETURNING *").ToList());
        Carries((await scalar.Should().ThrowAsync<SqliteException>()).Which, ValueObjectErrorCodes.OutOfRange);
        Carries((await entities.Should().ThrowAsync<SqliteException>()).Which, ValueObjectErrorCodes.OutOfRange);

        // Each rule accepts the rows the refusals above start from, a NULL in an optional column included.
        (await Raw(context, "INSERT INTO \"shapes\" (\"Stock\", \"Account\", \"Chapters\") VALUES (NULL, NULL, 7)", async)()).Should().Be(1);
        (await Raw(context, "INSERT INTO \"refusals\" (\"Iban\", \"Quantity\", \"Backorder\", \"Country\") VALUES ('FR7630006000011234567890189', 1, NULL, 'LU')", async)()).Should().Be(1);
    }

    /// <summary>
    /// A refusal Entity Framework Core hands no command interceptor, one a provider reports while the results of a batch
    /// are read, still carries the code: the save interceptor sees it inside the <c>DbUpdateException</c>. A command
    /// interceptor throws SQLite's error there, as SQL Server and PostgreSQL report a statement after the first of a batch
    /// that returns rows, which the integration suite runs for real.
    /// </summary>
    /// <param name="async">Whether <c>SaveChangesAsync</c> is called.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_refusal_reported_while_a_batch_is_read_carries_the_code_through_the_save_interceptor(bool async)
    {
        var recorder = new FailureRecorder();
        await using var context = Context(interceptors: [recorder, new RefusalWhileReading()]);
        context.Refusals.Add(Valid());

        var update = (await Save(context, async).Should().ThrowAsync<DbUpdateException>()).Which;

        recorder.Failures.Should().Be(0);
        Carries(update.InnerException.Should().BeOfType<SqliteException>().Which, ValueObjectErrorCodes.OutOfRange);
        ValueObjectErrors.TryGetCode(update, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// A default instance is refused by the converter before the database: the <c>DbUpdateException</c> wraps the
    /// <see cref="ValueObjectException"/>, whose code stands, and no error of a provider is in the chain; an error of the
    /// provider for another constraint, a duplicate key, carries no code.
    /// </summary>
    [Fact]
    public async Task A_refusal_of_another_kind_is_left_as_it_is()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = Context();
        context.Refusals.Add(new Refusal { Quantity = Quantity.Create(1), Country = CountryCode.France });

        var unset = (await context.Invoking(static db => db.SaveChangesAsync(TestContext.Current.CancellationToken)).Should().ThrowAsync<DbUpdateException>()).Which;

        unset.InnerException.Should().BeOfType<ValueObjectException>();
        ValueObjectErrors.TryGetCode(unset, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.Required);
        ValueObjectCheckConstraints.TryGetCode(unset, out var named).Should().BeFalse();
        named.Should().BeNull();

        const string Insert = "INSERT INTO \"refusals\" (\"Id\", \"Iban\", \"Quantity\", \"Country\") VALUES (100, 'FR7630006000011234567890189', 1, 'FR')";
        await context.Database.ExecuteSqlRawAsync(Insert, ct);
        var duplicate = (await Raw(context, Insert, async: true).Should().ThrowAsync<SqliteException>()).Which;

        duplicate.Message.Should().Contain("UNIQUE constraint failed");
        duplicate.Data.Contains(ValueObjectErrors.ErrorCodeKey).Should().BeFalse();
        ValueObjectErrors.TryGetCode(duplicate, out _).Should().BeFalse();
    }

    /// <summary>
    /// Without <c>UseValueObjectCheckErrors()</c>, nothing changes: the provider's error holds no entry, and
    /// <see cref="ValueObjectErrors.TryGetCode"/> finds none; <see cref="ValueObjectCheckConstraints.TryGetCode"/> reads the
    /// code in a <c>catch</c>, from the <c>DbUpdateException</c> or from the provider's error itself.
    /// </summary>
    [Fact]
    public async Task Without_the_registration_the_error_is_left_alone_and_the_helper_reads_the_code()
    {
        await using var context = Context(checkErrors: false);
        var row = Valid();
        row.Quantity = Quantity.CreateUnchecked(-1);
        context.Refusals.Add(row);

        var update = (await Save(context, async: true).Should().ThrowAsync<DbUpdateException>()).Which;

        update.InnerException!.Data.Contains(ValueObjectErrors.ErrorCodeKey).Should().BeFalse();
        ValueObjectErrors.TryGetCode(update, out _).Should().BeFalse();
        ValueObjectCheckConstraints.TryGetCode(update, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
        ValueObjectCheckConstraints.TryGetCode(update.InnerException!, out var inner).Should().BeTrue();
        inner.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// SQL Server's message 547 names the constraint in every language SQL Server writes it in, quoted with ASCII,
    /// typographic or no quotation marks, the last followed by a full stop, which the name does not end with: each reads
    /// as the code of the rule, a hashed name in Czech included. The real <c>SqlException</c> is read in the integration
    /// suite, in each of those languages.
    /// </summary>
    /// <param name="language">The index of the message.</param>
    [Theory]
    [MemberData(nameof(SqlServerLanguages))]
    public void SQL_Servers_message_is_read_in_every_language_it_is_written_in(int language)
    {
        var message = SqlServerMessages[language];

        Code(new SqlServerError(message, "547")).Should().Be(ValueObjectErrorCodes.OutOfRange);
        Code(new SqlServerError(message.Replace("CK_rows_Score_Maximum", "CK_rows_Score_MinLength_0A1B2C3D", StringComparison.Ordinal), "547"))
            .Should().Be(ValueObjectErrorCodes.TooShort);
    }

    /// <summary>
    /// A name is told apart from the rest of a message by any quotation mark a language puts around it, ASCII, typographic
    /// or CJK, by any white space, and keeps none of the punctuation a sentence may put right after it, in SQL Server's
    /// error and in PostgreSQL's when Npgsql names no constraint; it may end the message too.
    /// </summary>
    /// <param name="message">A message naming <c>CK_rows_Score_Maximum</c>.</param>
    [Theory]
    [MemberData(nameof(NamesInMessages))]
    public void A_name_is_read_past_each_separator_a_message_may_put_around_it(string message)
    {
        Code(new SqlServerError(message, "547")).Should().Be(ValueObjectErrorCodes.OutOfRange);
        Code(new PostgresException(message, "ERROR", "ERROR", PostgresErrorCodes.CheckViolation)).Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// System.Data.SQLite, an ADO.NET provider <see cref="ValueObjectCheckConstraints.TryGetCode"/> reads without Entity
    /// Framework Core, writes SQLite's words on a line of their own, and ends its message with the name.
    /// </summary>
    [Fact]
    public void System_Data_SQLites_message_which_the_name_ends_is_read()
        => Code(new ProviderError("constraint failed\nCHECK constraint failed: CK_rows_Code_MinLength")).Should().Be(ValueObjectErrorCodes.TooShort);

    /// <summary>
    /// A name of the convention reads whole, with the hash a shortened name or one kept apart ends with, and nothing else
    /// of its shape does: a hash in lower case, of seven digits, or with no rule before it, a name with no table and
    /// column, another prefix, a rule in another case. PostgreSQL names the constraint apart from its message, so a name
    /// holding a space is read there.
    /// </summary>
    /// <param name="name">The name of the violated constraint.</param>
    /// <param name="code">The code it reads as, or <see langword="null"/>.</param>
    [Theory]
    [InlineData("CK_t_c_MinLength", ValueObjectErrorCodes.TooShort)]
    [InlineData("CK_t_c_Minimum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("CK_t_c_Maximum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("CK_t_c_KnownValues", ValueObjectErrorCodes.NotAKnownValue)]
    [InlineData("CK_t_c_Pattern", ValueObjectErrorCodes.InvalidFormat)]
    [InlineData("CK_long_named_table_with_a_long_named_col_Maximum_7304B65E", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("CK_t_Minimum_Maximum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("CK_t_c_Minimum_Pattern", ValueObjectErrorCodes.InvalidFormat)]
    [InlineData("CK_t__MinLength", ValueObjectErrorCodes.TooShort)]
    [InlineData("CK_Order Lines_Qty_Minimum", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("CK_t_c_Maximum_7304b65e", null)]
    [InlineData("CK_t_c_Maximum_7304B65", null)]
    [InlineData("CK_t_c_Maximum_7304B65EA", null)]
    [InlineData("CK_t_c_Maximum_7304G65E", null)]
    [InlineData("CK_t_DEADBEEF", null)]
    [InlineData("CK_t_c_Other", null)]
    [InlineData("CK__Minimum", null)]
    [InlineData("CK_Minimum", null)]
    [InlineData("CK_", null)]
    [InlineData("FK_t_c_Minimum", null)]
    [InlineData("ck_t_c_Minimum", null)]
    [InlineData("CK_t_c_minimum", null)]
    public void A_name_reads_whole_with_its_hash_and_nothing_else_of_its_shape_does(string name, string? code)
        => Code(new PostgresException("new row for relation violates check constraint", "ERROR", "ERROR", PostgresErrorCodes.CheckViolation, constraintName: name))
            .Should().Be(code);

    /// <summary>
    /// The constraint Npgsql names wins over the message, even to say it is none of the convention's; an error with no
    /// name of the constraint is read off its message, in the quotation marks of PostgreSQL's French, the first name in
    /// it winning over a value of the refused row its detail quotes.
    /// </summary>
    [Fact]
    public void The_constraint_Npgsql_names_wins_over_the_message()
    {
        const string Message = "new row for relation \"t\" violates check constraint \"CK_t_c_Minimum\"";

        Code(new PostgresException(Message, "ERROR", "ERROR", PostgresErrorCodes.CheckViolation, constraintName: "CK_t_c_Pattern"))
            .Should().Be(ValueObjectErrorCodes.InvalidFormat);
        Code(new PostgresException(Message, "ERROR", "ERROR", PostgresErrorCodes.CheckViolation, constraintName: "ck_declared"))
            .Should().BeNull();
        Code(new PostgresException("la nouvelle ligne de la relation « rows » viole la contrainte de vérification « CK_rows_Score_Maximum »", "ERREUR", "ERROR", PostgresErrorCodes.CheckViolation))
            .Should().Be(ValueObjectErrorCodes.OutOfRange);
        Code(new PostgresException("new row for relation \"t\" violates check constraint", "ERROR", "ERROR", PostgresErrorCodes.CheckViolation))
            .Should().BeNull();
        Code(new PostgresException(Message, "ERROR", "ERROR", PostgresErrorCodes.CheckViolation, detail: "Failing row contains (7, CK_t_c_Pattern, 101)."))
            .Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// An error that names a constraint for another reason carries no code: a migration's "There is already an object
    /// named …", "… is not a constraint" or "The object … is dependent on column", a foreign key conflicting, an SQL
    /// Server message with no error number, PostgreSQL's duplicate or missing object, SQLite's other constraints, and an
    /// exception that is not a provider's error; a provider's error wrapped is read through what wraps it.
    /// </summary>
    [Fact]
    public void An_error_naming_a_constraint_for_another_reason_carries_no_code()
    {
        Code(new SqlServerError("There is already an object named 'CK_rows_Score_Maximum' in the database.", "2714")).Should().BeNull();
        Code(new SqlServerError("'CK_rows_Qty_Maximum' is not a constraint.", "3728")).Should().BeNull();
        Code(new SqlServerError("The object 'CK_rows_Score_Maximum' is dependent on column 'Score'.", "5074")).Should().BeNull();
        Code(new SqlServerError("The INSERT statement conflicted with the FOREIGN KEY constraint \"FK_rows_parents_ParentId\".", "547")).Should().BeNull();
        Code(new SqlServerError("The INSERT statement conflicted with the CHECK constraint \"CK_rows_Score_Maximum\".", number: null)).Should().BeNull();
        Code(new PostgresException("constraint \"CK_rows_Score_Maximum\" for relation \"rows\" already exists", "ERROR", "ERROR", PostgresErrorCodes.DuplicateObject, constraintName: "CK_rows_Score_Maximum")).Should().BeNull();
        Code(new PostgresException("constraint \"CK_rows_Score_Maximum\" of relation \"rows\" does not exist", "ERROR", "ERROR", PostgresErrorCodes.UndefinedObject)).Should().BeNull();
        Code(new SqliteException("SQLite Error 19: 'UNIQUE constraint failed: CK_rows_Score_Maximum.Id'.", 19, 2067)).Should().BeNull();
        Code(new InvalidOperationException("The check constraint 'CK_rows_Score_Maximum' cannot be added.")).Should().BeNull();

        Code(new SqliteException("SQLite Error 19: 'CHECK constraint failed: CK_rows_Code_MinLength'.", 19, 275)).Should().Be(ValueObjectErrorCodes.TooShort);
        Code(new DbUpdateException("Failed.", new SqliteException("SQLite Error 19: 'CHECK constraint failed: CK_rows_Code_MinLength'.", 19, 275)))
            .Should().Be(ValueObjectErrorCodes.TooShort);
        Code(new DbUpdateException("Failed.", new InvalidOperationException("Failed."))).Should().BeNull();
    }

    /// <summary>
    /// A constraint the application declares under a name of the convention's shape reads as the convention's would, since
    /// its name says the same rule.
    /// </summary>
    [Fact]
    public async Task A_constraint_declared_under_a_name_of_the_same_shape_is_read_as_the_conventions()
    {
        await using var context = new DeclaringContext(new DbContextOptionsBuilder<DeclaringContext>().UseSqlite(_connection).UseValueObjectCheckErrors().Options);
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(TestContext.Current.CancellationToken);
        context.Add(new Counted { Count = 0 });

        var update = (await context.Invoking(static db => db.SaveChangesAsync(TestContext.Current.CancellationToken)).Should().ThrowAsync<DbUpdateException>()).Which;

        update.InnerException!.Message.Should().Contain("CHECK constraint failed: CK_counted_Count_Minimum");
        ValueObjectErrors.TryGetCode(update, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// An error whose data takes no new entry, read-only or of a fixed size, is left as it is: the very error the provider
    /// threw reaches the caller, rather than the one a write to its data would throw.
    /// </summary>
    /// <param name="fixedSize">Whether the data is of a fixed size rather than read-only.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_error_whose_data_takes_no_new_entry_is_left_as_it_is(bool fixedSize)
    {
        var error = new SealedError(fixedSize);
        await using var context = Context(interceptors: [new Throwing(error)]);

        var run = (await Raw(context, "SELECT 1", async: !fixedSize).Should().ThrowAsync<SealedError>()).Which;

        run.Should().BeSameAs(error);
        run.Data.Count.Should().Be(0);
        ValueObjectCheckConstraints.TryGetCode(run, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// The interceptor is one of the options, so it also runs on a service provider of the application's own, named by
    /// <c>UseInternalServiceProvider</c>.
    /// </summary>
    [Fact]
    public async Task The_code_is_added_on_a_service_provider_of_the_applications_own()
    {
        await using var services = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        var options = new DbContextOptionsBuilder<RefusalContext>()
            .UseInternalServiceProvider(services)
            .UseSqlite(_connection)
            .UseValueObjectCheckErrors()
            .Options;
        await using var context = new RefusalContext(options);

        var insert = () => context.Database.ExecuteSqlRawAsync("INSERT INTO \"shapes\" (\"Stock\", \"Chapters\") VALUES (-1, 7)", TestContext.Current.CancellationToken);

        Carries((await insert.Should().ThrowAsync<SqliteException>()).Which, ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// Both overloads register one interceptor, however many times they are called: on a builder holding no option yet,
    /// on one holding options but no interceptor, beside an interceptor of the application's own; the generic one keeps
    /// the type of the context.
    /// </summary>
    [Fact]
    public void One_interceptor_is_registered_however_many_times_it_is_asked()
    {
        var other = new FailureRecorder();
        var fresh = new DbContextOptionsBuilder();
        var beside = new DbContextOptionsBuilder().UseSqlite(_connection).AddInterceptors(other);
        var typed = new DbContextOptionsBuilder<RefusalContext>().UseSqlite(_connection).EnableSensitiveDataLogging();

        fresh.UseValueObjectCheckErrors().Should().BeSameAs(fresh);
        fresh.UseSqlite(_connection).UseValueObjectCheckErrors();
        beside.UseValueObjectCheckErrors().UseValueObjectCheckErrors();
        typed.UseValueObjectCheckErrors().Should().BeSameAs(typed);
        typed.UseValueObjectCheckErrors();

        Interceptors(fresh.Options).Should().Equal(ValueObjectCheckErrorInterceptor.Instance);
        Interceptors(beside.Options).Should().Equal(other, ValueObjectCheckErrorInterceptor.Instance);
        Interceptors(typed.Options).Should().Equal(ValueObjectCheckErrorInterceptor.Instance);

        static IEnumerable<IInterceptor> Interceptors(DbContextOptions options) => options.FindExtension<CoreOptionsExtension>()!.Interceptors!;
    }

    [Fact]
    public void A_missing_argument_is_refused()
    {
        var untyped = static () => ((DbContextOptionsBuilder)null!).UseValueObjectCheckErrors();
        var typed = static () => ((DbContextOptionsBuilder<RefusalContext>)null!).UseValueObjectCheckErrors();
        var read = static () => ValueObjectCheckConstraints.TryGetCode(null!, out _);

        untyped.Should().Throw<ArgumentNullException>().WithParameterName("optionsBuilder");
        typed.Should().Throw<ArgumentNullException>().WithParameterName("optionsBuilder");
        read.Should().Throw<ArgumentNullException>().WithParameterName("exception");
    }

    private static string? Code(Exception exception) => ValueObjectCheckConstraints.TryGetCode(exception, out var code) ? code : null;

    /// <summary>Asserts that the provider's error carries the code, as the interceptor stores it and as both readers read it.</summary>
    private static void Carries(DbException refusal, string code)
    {
        refusal.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(code);
        ValueObjectErrors.TryGetCode(refusal, out var read).Should().BeTrue();
        read.Should().Be(code);
    }

    private static Func<Task> Save(DbContext context, bool async)
        => async
            ? () => context.SaveChangesAsync(TestContext.Current.CancellationToken)
            : () => Task.FromResult(context.SaveChanges());

    private static Func<Task<int>> Raw(DbContext context, string sql, bool async)
        => async
            ? () => context.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken)
            : () => Task.FromResult(context.Database.ExecuteSqlRaw(sql));

    private static Refusal Valid() => new()
    {
        Iban = Iban.Create("FR7630006000011234567890189"),
        Quantity = Quantity.Create(1),
        Country = CountryCode.France,
    };

    private RefusalContext Context(bool checkErrors = true, IInterceptor[]? interceptors = null)
    {
        var builder = new DbContextOptionsBuilder<RefusalContext>().UseSqlite(_connection);
        if (interceptors is not null)
        {
            builder.AddInterceptors(interceptors);
        }

        return new RefusalContext(checkErrors ? builder.UseValueObjectCheckErrors().Options : builder.Options);
    }

    /// <summary>A row whose value objects each break a rule of their own when an invalid instance of it is written.</summary>
    private sealed class Refusal
    {
        public int Id { get; set; }

        public Iban Iban { get; set; }

        public Quantity Quantity { get; set; }

        public Quantity? Backorder { get; set; }

        public CountryCode Country { get; set; }
    }

    /// <summary>The columns of a construction of a generic value object, an entity identifier and a value object written by hand.</summary>
    private sealed class Shape
    {
        public int Id { get; set; }

        public Catalog<Shape>.Stock? Stock { get; set; }

        public AccountId? Account { get; set; }

        public Declared<Chapters, int> Chapters { get; set; }
    }

    /// <summary>A count under a constraint the application declares, named as the convention names one.</summary>
    private sealed class Counted
    {
        public int Id { get; set; }

        public int Count { get; set; }
    }

    /// <summary>A count of chapters, its bounds stated as a value object written by hand states them.</summary>
    private sealed class Chapters : IDeclaration<int>
    {
        public static ValueObjectSchema Schema { get; } = new() { Minimum = "5", Maximum = "9" };
    }

    /// <summary>The rows, with the check constraints.</summary>
    /// <param name="options">The provider, with or without the interceptor.</param>
    private sealed class RefusalContext(DbContextOptions<RefusalContext> options) : DbContext(options)
    {
        public DbSet<Refusal> Refusals => Set<Refusal>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder
                .ConfigureValueObjects(typeof(Iban).Assembly)
                .ConfigureEntityIds(typeof(AccountId).Assembly)
                .AddValueObjectCheckConstraints();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Refusal>().ToTable("refusals");
            modelBuilder.Entity<Shape>(shape =>
            {
                shape.ToTable("shapes");
                shape.Property(static entity => entity.Chapters).HasValueObjectConversion<Declared<Chapters, int>, int>();
            });
        }
    }

    /// <summary>A table whose constraint the application declares.</summary>
    /// <param name="options">The provider, with the interceptor.</param>
    private sealed class DeclaringContext(DbContextOptions<DeclaringContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Counted>().ToTable("counted", static table => table.HasCheckConstraint("CK_counted_Count_Minimum", "\"Count\" >= 1"));
    }

    /// <summary>An error of SQL Server's shape: SqlClient states the number of the first error in the data, in every language.</summary>
    /// <param name="message">The message.</param>
    /// <param name="number">The error number, or <see langword="null"/> for none.</param>
    private sealed class SqlServerError(string message, string? number) : DbException(message)
    {
        public override IDictionary Data { get; } = number is null ? new Hashtable() : new Hashtable { ["HelpLink.EvtID"] = number };
    }

    /// <summary>An error of an ADO.NET provider that states nothing but its message, as System.Data.SQLite's.</summary>
    /// <param name="message">The message.</param>
    private sealed class ProviderError(string message) : DbException(message);

    /// <summary>An error reporting a violated constraint, whose data takes no new entry.</summary>
    /// <param name="fixedSize">Whether the data is of a fixed size rather than read-only.</param>
    private sealed class SealedError(bool fixedSize) : DbException("SQLite Error 19: 'CHECK constraint failed: CK_shapes_Stock_Minimum'.")
    {
        public override IDictionary Data { get; } = new SealedData(fixedSize);
    }

    /// <summary>A dictionary that refuses any write, as a read-only or fixed-size one may.</summary>
    /// <param name="fixedSize">Whether it states a fixed size rather than read-only.</param>
    private sealed class SealedData(bool fixedSize) : Hashtable
    {
        public override bool IsReadOnly => !fixedSize;

        public override bool IsFixedSize => fixedSize;

        public override object? this[object key]
        {
            get => base[key];
            set => throw new NotSupportedException("The data takes no new entry.");
        }
    }

    /// <summary>Throws an error before a command runs, which Entity Framework Core hands the command interceptors.</summary>
    /// <param name="error">The error.</param>
    private sealed class Throwing(DbException error) : DbCommandInterceptor
    {
        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
            => throw error;

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
            => throw error;
    }

    /// <summary>
    /// Throws SQLite's error for a violated constraint once, while Entity Framework Core reads the results of a batch,
    /// where it hands no command interceptor the error.
    /// </summary>
    private sealed class RefusalWhileReading : DbCommandInterceptor
    {
        private bool _thrown;

        public override InterceptionResult DataReaderClosing(DbCommand command, DataReaderClosingEventData eventData, InterceptionResult result)
            => Refuse(result);

        public override ValueTask<InterceptionResult> DataReaderClosingAsync(DbCommand command, DataReaderClosingEventData eventData, InterceptionResult result)
            => ValueTask.FromResult(Refuse(result));

        private InterceptionResult Refuse(InterceptionResult result)
        {
            if (_thrown)
            {
                return result;
            }

            _thrown = true;
            throw new SqliteException("SQLite Error 19: 'CHECK constraint failed: CK_refusals_Quantity_Maximum'.", 19, 275);
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
}
