using System.Data.Common;
using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.Dapper;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using AdCodicem.ValueObjects.UnitTests.Domain;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Verifies, on a real database engine, what <c>Unicode = false</c> does to a value object's column and parameters: SQL
/// Server stores it in <c>varchar</c> and receives <c>varchar</c> parameters from Entity Framework Core and Dapper alike,
/// while PostgreSQL stores text as it always does.
/// </summary>
/// <typeparam name="TFixture">Database under test.</typeparam>
public abstract class NonUnicodeTextTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    /// <summary>The table each fixture creates once, which the sample's model does not hold.</summary>
    private static readonly ConditionalWeakTable<DatabaseFixture, Task> Tables = new();

    private static int _lastId;

    static NonUnicodeTextTests() => ValueObjectDapper.AddValueObjectHandlers(typeof(Bic).Assembly);

    /// <summary>Gets the database under test.</summary>
    protected TFixture Fixture => fixture;

    /// <summary>Gets the name the catalog gives the column of a value object declaring <c>Unicode = false</c>.</summary>
    protected abstract string NonUnicodeTextType { get; }

    /// <summary>Gets the name the catalog gives the column of one declaring no length besides.</summary>
    protected abstract string UnboundedNonUnicodeTextType { get; }

    /// <summary>Gets what the query text says of the parameter a BIC is compared through.</summary>
    protected abstract string EqualitySql { get; }

    /// <summary>Gets what the query text says of the pattern of a prefix search on the value of a BIC.</summary>
    protected abstract string PrefixSql { get; }

    /// <summary>Gets what the query text says of the elements of a collection of BICs a query reads.</summary>
    protected abstract string ElementSql { get; }

    /// <summary>Gets what the query text says of the parameter a routing tag, which declares no length, is compared through.</summary>
    protected abstract string UnboundedSql { get; }

    /// <summary>Gets the type Dapper's parameter of a BIC reaches the database as.</summary>
    protected abstract string DapperParameterType { get; }

    /// <summary>
    /// The columns of the value objects declaring the option are non-Unicode text of their declared length, an optional
    /// one included, and of no length for one declaring none.
    /// </summary>
    [Fact]
    public async Task The_columns_are_non_Unicode_text_of_the_declared_length()
    {
        await EnsureTablesAsync();
        await using var context = CreateContext();

        var sql = $"SELECT column_name AS {Q("Name")}, data_type AS {Q("Type")}, character_maximum_length AS {Q("Length")} "
                  + "FROM information_schema.columns WHERE table_name = 'remittances'";
        var columns = await context.Database
            .SqlQueryRaw<CatalogColumn>(sql)
            .ToDictionaryAsync(static column => column.Name, TestContext.Current.CancellationToken);

        columns["Bic"].Should().Be(new CatalogColumn("Bic", NonUnicodeTextType, 11));
        columns["Intermediary"].Should().Be(new CatalogColumn("Intermediary", NonUnicodeTextType, 11));
        columns["Shelf"].Type.Should().Be(UnboundedNonUnicodeTextType, "a closed set declares no length");
        columns["Tag"].Type.Should().Be(UnboundedNonUnicodeTextType);
    }

    /// <summary>
    /// A comparison, a prefix search under <c>UseValueObjectQueries()</c>, a query over the elements of a collection and a
    /// comparison with a value object declaring no length each declare their parameter as the column is, and run.
    /// </summary>
    [Fact]
    public async Task A_parameter_compared_with_the_column_is_declared_as_the_column_is()
    {
        await EnsureTablesAsync();
        var bic = Bic.Create("COBADEFF");
        var tag = RoutingTag.Create("north-7");
        var id = Next();
        await using (var write = CreateContext())
        {
            write.Remittances.Add(new Remittance
            {
                Id = id,
                Bic = bic,
                Intermediary = Bic.Create("DEUTDEFF500"),
                Correspondents = [bic, Bic.Create("NEDSZAJJXXX")],
                Shelf = ShelfLevel.Top,
                Tag = tag,
            });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = CreateContext();
        var prefix = "COBA";

        var byBic = read.Remittances.Where(remittance => remittance.Bic == bic).Select(static remittance => remittance.Id);
        var byPrefix = read.Remittances.Where(remittance => remittance.Bic.Value.StartsWith(prefix)).Select(static remittance => remittance.Id);
        var byElement = read.Remittances.Where(remittance => remittance.Correspondents.Contains(bic)).Select(static remittance => remittance.Id);
        var byTag = read.Remittances.Where(remittance => remittance.Tag == tag).Select(static remittance => remittance.Id);

        byBic.ToQueryString().Should().Contain(EqualitySql);
        byPrefix.ToQueryString().Should().Contain(PrefixSql);
        byElement.ToQueryString().Should().Contain(ElementSql);
        byTag.ToQueryString().Should().Contain(UnboundedSql);
        (await byBic.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(id);
        (await byPrefix.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(id);
        (await byElement.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(id);
        (await byTag.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(id);
    }

    /// <summary>Dapper's parameter of a value object declaring the option reaches the database as non-Unicode text.</summary>
    [Fact]
    public async Task Dapper_sends_a_value_object_declaring_the_option_as_non_Unicode_text()
    {
        await EnsureTablesAsync();
        var bic = Bic.Create("BNPAFRPP");
        var id = Next();
        await using (var write = CreateContext())
        {
            write.Remittances.Add(new Remittance { Id = id, Bic = bic, Shelf = ShelfLevel.Bottom, Tag = RoutingTag.Create("south") });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var connection = fixture.CreateConnection();
        var type = await connection.QuerySingleAsync<string>(Command(ParameterTypeSql, new { bic }));
        var found = await connection.QuerySingleAsync<int>(Command(
            $"SELECT {Q("Id")} FROM {Q("remittances")} WHERE {Q("Bic")} = @bic",
            new { bic }));

        type.Should().Be(DapperParameterType);
        found.Should().Be(id);
    }

    /// <summary>
    /// A character the column's code page lacks: PostgreSQL keeps it, and SQL Server writes it as a look-alike, with no
    /// error, which a read trusts: it is why <c>VO0047</c> asks the rules to hold the text to ASCII.
    /// </summary>
    [Fact]
    public async Task A_character_outside_ASCII_is_stored_as_the_engine_stores_text()
    {
        await EnsureTablesAsync();
        var id = Next();
        await using (var write = CreateContext())
        {
            write.Remittances.Add(new Remittance
            {
                Id = id,
                Bic = Bic.CreateUnchecked("DEUTDŁFF"),
                Shelf = ShelfLevel.Top,
                Tag = RoutingTag.Create("west"),
            });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = CreateContext();
        var stored = await read.Remittances.Where(remittance => remittance.Id == id)
            .Select(static remittance => remittance.Bic)
            .SingleAsync(TestContext.Current.CancellationToken);

        stored.Value.Should().Be(StoredAs("DEUTDŁFF"));
    }

    /// <summary>Gets what the engine stores of a text holding a character outside ASCII.</summary>
    /// <param name="text">The text written.</param>
    /// <returns>The text read back.</returns>
    protected abstract string StoredAs(string text);

    /// <summary>Gets the query reading back the type of the parameter <c>@bic</c>.</summary>
    protected abstract string ParameterTypeSql { get; }

    /// <summary>Creates a context bound to the running container.</summary>
    /// <returns>A new context.</returns>
    protected RemittanceContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<RemittanceContext>();
        fixture.Configure(builder, fixture.ConnectionString);
        builder.UseValueObjectQueries();

        return new RemittanceContext(builder.Options);
    }

    /// <summary>Wraps a query and its parameters with the test's cancellation token.</summary>
    /// <param name="sql">The query.</param>
    /// <param name="parameters">Its parameters.</param>
    /// <returns>The command.</returns>
    protected static CommandDefinition Command(string sql, object? parameters = null)
        => new(sql, parameters, cancellationToken: TestContext.Current.CancellationToken);

    private static int Next() => Interlocked.Increment(ref _lastId);

    private string Q(string identifier) => fixture.Quote(identifier);

    private Task EnsureTablesAsync() => Tables.GetValue(fixture, _ => CreateTablesAsync());

    private async Task CreateTablesAsync()
    {
        await using var context = CreateContext();
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    /// <summary>A column as the catalog describes it.</summary>
    /// <param name="Name">The column.</param>
    /// <param name="Type">Its data type.</param>
    /// <param name="Length">Its maximum length, if it has one.</param>
    protected sealed record CatalogColumn(string Name, string Type, int? Length);

    /// <summary>A payment order, holding value objects declaring their text non-Unicode.</summary>
    protected sealed class Remittance
    {
        public int Id { get; set; }

        public Bic Bic { get; set; }

        public Bic? Intermediary { get; set; }

        public List<Bic> Correspondents { get; set; } = [];

        public ShelfLevel Shelf { get; set; }

        public RoutingTag Tag { get; set; }
    }

    /// <summary>The payment orders, mapped by the conventions.</summary>
    /// <param name="options">Options bound to the running container.</param>
    protected sealed class RemittanceContext(DbContextOptions<RemittanceContext> options) : DbContext(options)
    {
        public DbSet<Remittance> Remittances => Set<Remittance>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(typeof(Bic).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Remittance>(remittance =>
            {
                remittance.ToTable("remittances");
                remittance.Property(static entity => entity.Id).ValueGeneratedNever();
            });
    }
}

/// <summary>Runs the non-Unicode text against PostgreSQL, which stores text alike whatever the option.</summary>
/// <param name="fixture">PostgreSQL container.</param>
public sealed class PostgreSqlNonUnicodeTextTests(PostgreSqlFixture fixture) : NonUnicodeTextTests<PostgreSqlFixture>(fixture)
{
    /// <inheritdoc />
    protected override string NonUnicodeTextType => "character varying";

    /// <inheritdoc />
    protected override string UnboundedNonUnicodeTextType => "text";

    /// <inheritdoc />
    protected override string EqualitySql => "WHERE r.\"Bic\" = @bic";

    /// <inheritdoc />
    protected override string PrefixSql => "r.\"Bic\" LIKE @prefix_startswith";

    /// <inheritdoc />
    protected override string ElementSql => "= ANY (r.\"Correspondents\")";

    /// <inheritdoc />
    protected override string UnboundedSql => "WHERE r.\"Tag\" = @tag";

    /// <inheritdoc />
    protected override string DapperParameterType => "text";

    /// <inheritdoc />
    protected override string ParameterTypeSql => "SELECT pg_typeof(@bic)::text";

    /// <inheritdoc />
    protected override string StoredAs(string text) => text;
}

/// <summary>
/// Runs the non-Unicode text against SQL Server, where it is <c>varchar</c>, and what only SQL Server does with it:
/// Always Encrypted, the audit of the rows a migration converts, and the migration of a key.
/// </summary>
/// <param name="fixture">SQL Server container.</param>
public sealed class SqlServerNonUnicodeTextTests(SqlServerFixture fixture) : NonUnicodeTextTests<SqlServerFixture>(fixture)
{
    /// <summary>The name the keys of the Always Encrypted tests are registered under, on each connection.</summary>
    private const string KeyStore = "TEST_KEYSTORE";

    /// <inheritdoc />
    protected override string NonUnicodeTextType => "varchar";

    /// <inheritdoc />
    protected override string UnboundedNonUnicodeTextType => "varchar";

    /// <inheritdoc />
    protected override string EqualitySql => "DECLARE @bic varchar(11)";

    /// <inheritdoc />
    protected override string PrefixSql => "DECLARE @prefix_startswith varchar(11)";

    /// <inheritdoc />
    protected override string ElementSql => "OPENJSON([r].[Correspondents]) WITH ([value] varchar(11) '$')";

    /// <inheritdoc />
    protected override string UnboundedSql => "DECLARE @tag varchar(8000)";

    /// <inheritdoc />
    protected override string DapperParameterType => "varchar(11)";

    /// <inheritdoc />
    protected override string ParameterTypeSql
        => "SELECT CAST(SQL_VARIANT_PROPERTY(@bic, 'BaseType') AS varchar(16)) "
           + "+ '(' + CAST(SQL_VARIANT_PROPERTY(@bic, 'MaxLength') AS varchar(8)) + ')'";

    /// <summary>
    /// An Always Encrypted <c>varchar</c> column takes a value object declaring the option, through Entity Framework Core
    /// and through Dapper, written and compared with deterministic encryption, the keys registered on each connection.
    /// </summary>
    [Fact]
    public async Task An_Always_Encrypted_varchar_column_takes_a_value_object_declaring_the_option()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await EncryptedDatabaseAsync();
        var bic = Bic.Create("DEUTDEFF");

        await using (var connection = Encrypted(database))
        {
            await using (var write = new EncryptedContext(connection))
            {
                write.Payments.Add(new EncryptedPayment { Bic = bic });
                await write.SaveChangesAsync(ct);
            }

            await using var read = new EncryptedContext(connection);
            (await read.Payments.CountAsync(payment => payment.Bic == bic, ct)).Should().Be(1);
        }

        await using (var connection = Encrypted(database))
        {
            await connection.ExecuteAsync(Command("INSERT INTO [encrypted_payments] ([Bic]) VALUES (@bic)", new { bic }));
            var found = await connection.QueryAsync<Bic>(Command("SELECT [Bic] FROM [encrypted_payments] WHERE [Bic] = @bic", new { bic }));
            found.Should().HaveCount(2).And.AllBeEquivalentTo(bic);
        }
    }

    /// <summary>
    /// The same column refuses a value object over Unicode text, whose parameter is <c>nvarchar</c>: the option is what
    /// makes the column usable.
    /// </summary>
    [Fact]
    public async Task An_Always_Encrypted_varchar_column_refuses_a_value_object_over_Unicode_text()
    {
        var database = await EncryptedDatabaseAsync();
        var iban = Iban.Create("DE89370400440532013000");

        await using var connection = Encrypted(database);
        var insert = () => connection.ExecuteAsync(Command("INSERT INTO [encrypted_transfers] ([Iban]) VALUES (@iban)", new { iban }));

        (await insert.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("Operand type clash: nvarchar(34) encrypted with");
    }

    /// <summary>
    /// The guide's audit query counts, before a migration declares the option, exactly the rows the conversion of the
    /// column to its code page changes; the migration Entity Framework Core writes then converts them silently, and the
    /// query counts none.
    /// </summary>
    [Fact]
    public async Task The_audit_query_counts_the_rows_a_migration_to_non_Unicode_text_changes()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await NewDatabaseAsync("audit");
        await using (var unicode = new AuditContext(connectionString, unicode: true))
        {
            await unicode.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(ct);
            await unicode.Database.ExecuteSqlRawAsync(
                "INSERT INTO [audited] ([Id], [Code]) VALUES (1, N'DEUTDEFF'), (2, N'DEUTDŁFF'), (3, N'DEUTDÉFF')", ct);
        }

        var audit = "SELECT COUNT(*) AS [Value] FROM [audited] "
                    + "WHERE CAST(CAST([Code] AS varchar(11)) AS nvarchar(11)) COLLATE Latin1_General_100_BIN2 "
                    + "<> [Code] COLLATE Latin1_General_100_BIN2";

        await using (var before = new AuditContext(connectionString, unicode: true))
        {
            (await before.Database.SqlQueryRaw<int>(audit).SingleAsync(ct)).Should().Be(1, "Ł is no character of Latin-1, É is");
        }

        await using (var before = new AuditContext(connectionString, unicode: true))
        await using (var after = new AuditContext(connectionString, unicode: false))
        {
            var operations = Differences(before, after);
            operations.Should().ContainSingle().Which.Should().BeOfType<AlterColumnOperation>();
            await ApplyAsync(after, operations, ct);
        }

        await using var read = new AuditContext(connectionString, unicode: false);
        var codes = await read.Database.SqlQueryRaw<string>("SELECT [Code] AS [Value] FROM [audited] ORDER BY [Id]").ToListAsync(ct);
        codes.Should().Equal("DEUTDEFF", "DEUTDLFF", "DEUTDÉFF");
        (await read.Database.SqlQueryRaw<int>(audit).SingleAsync(ct)).Should().Be(0);
        (await read.Database.SqlQueryRaw<string>(
                "SELECT data_type AS [Value] FROM information_schema.columns WHERE table_name = 'audited' AND column_name = 'Code'")
            .SingleAsync(ct)).Should().Be("varchar");
    }

    /// <summary>
    /// A key declaring the option: the migration Entity Framework Core writes for it and its foreign key fails, the primary
    /// key depending on the column, and the guide's sequence, which drops both constraints around the columns it alters
    /// and adds them back, applies; the relationship still holds.
    /// </summary>
    [Fact]
    public async Task A_key_declaring_the_option_migrates_once_its_constraints_are_dropped_around_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await NewDatabaseAsync("catalog");
        var sku = Sku.Create("AB-1");
        await using (var unicode = new CatalogContext(connectionString, unicode: true))
        {
            await unicode.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(ct);
            unicode.Products.Add(new Product { Id = sku, Name = "first" });
            unicode.Lines.Add(new Line { Id = 1, ProductId = sku });
            await unicode.SaveChangesAsync(ct);
        }

        await using (var before = new CatalogContext(connectionString, unicode: true))
        await using (var after = new CatalogContext(connectionString, unicode: false))
        {
            var altered = Differences(before, after);
            altered.Should().HaveCount(2).And.AllBeOfType<AlterColumnOperation>();

            var asWritten = () => ApplyAsync(after, altered, ct);
            (await asWritten.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("is dependent on column");

            var model = after.GetService<IDesignTimeModel>().Model;
            var product = model.FindEntityType(typeof(Product))!;
            var line = model.FindEntityType(typeof(Line))!;
            var primaryKey = product.FindPrimaryKey()!.GetName()!;
            var foreignKey = line.GetForeignKeys().Single().GetConstraintName()!;

            await ApplyAsync(
                after,
                [
                    new DropForeignKeyOperation { Name = foreignKey, Table = "lines" },
                    new DropPrimaryKeyOperation { Name = primaryKey, Table = "products" },
                    .. altered,
                    new AddPrimaryKeyOperation { Name = primaryKey, Table = "products", Columns = ["Id"] },
                    new AddForeignKeyOperation
                    {
                        Name = foreignKey,
                        Table = "lines",
                        Columns = ["ProductId"],
                        PrincipalTable = "products",
                        PrincipalColumns = ["Id"],
                        OnDelete = ReferentialAction.Cascade,
                    },
                ],
                ct);
        }

        await using var read = new CatalogContext(connectionString, unicode: false);
        (await read.Lines.Where(line => line.ProductId == sku).Select(static line => line.Product!.Name).SingleAsync(ct)).Should().Be("first");
        (await read.Database.SqlQueryRaw<string>(
                "SELECT table_name + '.' + column_name + ' ' + data_type AS [Value] FROM information_schema.columns "
                + "WHERE column_name IN ('Id', 'ProductId') AND table_name IN ('products', 'lines') AND data_type LIKE '%char%' "
                + "ORDER BY table_name")
            .ToListAsync(ct)).Should().Equal("lines.ProductId varchar", "products.Id varchar");
    }

    /// <inheritdoc />
    protected override string StoredAs(string text) => text.Replace('Ł', 'L');

    private static IReadOnlyList<MigrationOperation> Differences(DbContext before, DbContext after)
        => after.GetService<IMigrationsModelDiffer>().GetDifferences(
            before.GetService<IDesignTimeModel>().Model.GetRelationalModel(),
            after.GetService<IDesignTimeModel>().Model.GetRelationalModel());

    private static async Task ApplyAsync(DbContext context, IReadOnlyList<MigrationOperation> operations, CancellationToken cancellationToken)
    {
        var commands = context.GetService<IMigrationsSqlGenerator>().Generate(operations, context.GetService<IDesignTimeModel>().Model);
        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            foreach (var migration in commands)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = migration.CommandText;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    /// <summary>Creates a database of the test's own.</summary>
    /// <param name="name">What the database holds.</param>
    /// <returns>Its connection string.</returns>
    private async Task<string> NewDatabaseAsync(string name)
    {
        var database = $"{name}_{Guid.NewGuid():N}";
        await using (var master = new SqlConnection(Fixture.ConnectionString))
        {
            await master.OpenAsync(TestContext.Current.CancellationToken);
            await master.ExecuteAsync(Command($"CREATE DATABASE [{database}]"));
        }

        return new SqlConnectionStringBuilder(Fixture.ConnectionString) { InitialCatalog = database }.ConnectionString;
    }

    /// <summary>
    /// Creates a database of the test's own with a column master key and a column encryption key the in-memory store
    /// reads, and two deterministic encrypted <c>varchar</c> columns.
    /// </summary>
    /// <returns>Its connection string.</returns>
    private async Task<string> EncryptedDatabaseAsync()
    {
        var connectionString = await NewDatabaseAsync("encrypted");
        var key = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        const string Encryption = "COLLATE Latin1_General_BIN2 ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = CEK1, "
            + "ENCRYPTION_TYPE = DETERMINISTIC, ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256') NOT NULL";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync(Command($"CREATE COLUMN MASTER KEY CMK1 WITH (KEY_STORE_PROVIDER_NAME = '{KeyStore}', KEY_PATH = 'cmk1')"));
        await connection.ExecuteAsync(Command(
            $"CREATE COLUMN ENCRYPTION KEY CEK1 WITH VALUES (COLUMN_MASTER_KEY = CMK1, ALGORITHM = 'RSA_OAEP', ENCRYPTED_VALUE = 0x{key})"));
        await connection.ExecuteAsync(Command(
            $"CREATE TABLE [encrypted_payments] ([Id] int IDENTITY PRIMARY KEY, [Bic] varchar(11) {Encryption})"));
        await connection.ExecuteAsync(Command(
            $"CREATE TABLE [encrypted_transfers] ([Id] int IDENTITY PRIMARY KEY, [Iban] varchar(34) {Encryption})"));

        return connectionString;
    }

    /// <summary>
    /// Opens a connection that encrypts its parameters, the keys registered on it alone: the process-wide registration
    /// can be made only once.
    /// </summary>
    /// <param name="connectionString">The database.</param>
    /// <returns>The connection, not yet open.</returns>
    private static SqlConnection Encrypted(string connectionString)
    {
        var connection = new SqlConnection(new SqlConnectionStringBuilder(connectionString)
        {
            ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled,
        }.ConnectionString);
        connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(
            new Dictionary<string, SqlColumnEncryptionKeyStoreProvider> { [KeyStore] = new InMemoryKeyStore() });

        return connection;
    }

    /// <summary>
    /// A column master key store that hands the column encryption key back as the database holds it, so that the test
    /// needs no vault: the key is never protected, which only a test may do.
    /// </summary>
    private sealed class InMemoryKeyStore : SqlColumnEncryptionKeyStoreProvider
    {
        public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
            => encryptedColumnEncryptionKey;

        public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
            => columnEncryptionKey;

        public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations) => [];

        public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature) => true;
    }

    /// <summary>A payment whose BIC is encrypted.</summary>
    private sealed class EncryptedPayment
    {
        public int Id { get; set; }

        public Bic Bic { get; set; }
    }

    /// <summary>The encrypted payments, on a connection holding the keys.</summary>
    /// <param name="connection">The connection.</param>
    private sealed class EncryptedContext(SqlConnection connection) : DbContext
    {
        public DbSet<EncryptedPayment> Payments => Set<EncryptedPayment>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlServer(connection);

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(typeof(Bic).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<EncryptedPayment>().ToTable("encrypted_payments");
    }

    /// <summary>A row the audit reads.</summary>
    private sealed class Audited
    {
        public int Id { get; set; }

        public Bic Code { get; set; }
    }

    /// <summary>
    /// The audited rows, mapped before the BIC declared the option, <c>AreUnicode(true)</c> standing for that, or after.
    /// </summary>
    /// <param name="connectionString">The database.</param>
    /// <param name="unicode">Whether the BIC is still Unicode text.</param>
    private sealed class AuditContext(string connectionString, bool unicode) : DbContext
    {
        public bool Unicode => unicode;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlServer(connectionString).ReplaceService<IModelCacheKeyFactory, UnicodeCacheKeyFactory>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ConfigureValueObjects(typeof(Bic).Assembly);
            if (unicode)
            {
                configurationBuilder.Properties<Bic>().AreUnicode(true);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Audited>(audited =>
            {
                audited.ToTable("audited");
                audited.Property(static entity => entity.Id).ValueGeneratedNever();
            });
    }

    /// <summary>A product, keyed by its SKU.</summary>
    private sealed class Product
    {
        public Sku Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>A line of an order, referencing a product by its SKU.</summary>
    private sealed class Line
    {
        public int Id { get; set; }

        public Sku ProductId { get; set; }

        public Product? Product { get; set; }
    }

    /// <summary>The catalog, mapped before the SKU declared the option, <c>AreUnicode(true)</c> standing for that, or after.</summary>
    /// <param name="connectionString">The database.</param>
    /// <param name="unicode">Whether the SKU is still Unicode text.</param>
    private sealed class CatalogContext(string connectionString, bool unicode) : DbContext
    {
        public DbSet<Product> Products => Set<Product>();

        public DbSet<Line> Lines => Set<Line>();

        public bool Unicode => unicode;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlServer(connectionString).ReplaceService<IModelCacheKeyFactory, UnicodeCacheKeyFactory>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.ConfigureValueObjects(typeof(Sku).Assembly);
            if (unicode)
            {
                configurationBuilder.Properties<Sku>().AreUnicode(true);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(product =>
            {
                product.ToTable("products");
                product.Property(static entity => entity.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<Line>(line =>
            {
                line.ToTable("lines");
                line.Property(static entity => entity.Id).ValueGeneratedNever();
                line.HasOne(static entity => entity.Product).WithMany().HasForeignKey(static entity => entity.ProductId);
            });
        }
    }

    /// <summary>Keeps a model per context type and per Unicode facet, so that both models of a migration are built.</summary>
    private sealed class UnicodeCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), context switch
            {
                AuditContext audit => audit.Unicode,
                _ => ((CatalogContext)context).Unicode,
            }, designTime);
    }
}
