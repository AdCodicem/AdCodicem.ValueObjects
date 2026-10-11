using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Verifies, on a real database engine, that a key typed as a value object is generated the way the provider generates
/// a key of its underlying type, and that one over a <see cref="Guid"/> that nothing generates refuses an entity added
/// without it.
/// </summary>
/// <remarks>
/// A <see cref="Guid"/> key is SQL Server's sequential GUID on SQL Server, which a <c>uniqueidentifier</c> sorts in the
/// order it was generated, and a version 7 GUID on PostgreSQL, which a <c>uuid</c> sorts so. An integer key is the
/// engine's identity column.
/// </remarks>
/// <typeparam name="TFixture">Database under test.</typeparam>
public abstract class KeyGenerationTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    /// <summary>The table of each fixture's parcels and numbered entities, created once, which the sample's model does not hold.</summary>
    private static readonly ConditionalWeakTable<DatabaseFixture, Task> Tables = new();

    /// <summary>Gets the version of the GUIDs the provider generates for a <see cref="Guid"/> key.</summary>
    protected abstract int GuidVersion { get; }

    /// <summary>
    /// Gets the query telling whether a column is an identity, as 1 or 0 in a column named <c>Value</c>, taking the table
    /// and the column, in this order.
    /// </summary>
    protected abstract string IdentitySql { get; }

    /// <summary>Gets the name the catalog gives the column type of an <see cref="int"/>.</summary>
    protected abstract string Int32Type { get; }

    /// <summary>
    /// Generated keys are the provider's own, distinct, and sorted by the engine in the order they were generated, so that
    /// a clustered index on SQL Server and a B-tree on PostgreSQL take each new key at their end. A version 7 GUID orders by
    /// the millisecond, so the parcels are added a few milliseconds apart.
    /// </summary>
    [Fact]
    public async Task Generated_Guid_keys_are_the_providers_own_and_the_engine_sorts_them_as_they_were_generated()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnsureTablesAsync();
        var batch = Guid.NewGuid().ToString("N");
        var parcels = new List<Parcel>();

        await using (var write = CreateContext())
        {
            for (var rank = 0; rank < 5; rank++)
            {
                var parcel = new Parcel { Batch = batch, Rank = rank };
                write.Parcels.Add(parcel);
                parcels.Add(parcel);
                await Task.Delay(3, ct);
            }

            await write.SaveChangesAsync(ct);
        }

        parcels.Select(static parcel => parcel.Id.Value).Should().OnlyHaveUniqueItems().And.NotContain(Guid.Empty);
        parcels.Should().OnlyContain(parcel => parcel.Id.Value.Version == GuidVersion);

        await using var read = CreateContext();
        var ranks = await read.Parcels
            .Where(parcel => parcel.Batch == batch)
            .OrderBy(parcel => parcel.Id)
            .Select(parcel => parcel.Rank)
            .ToListAsync(ct);

        ranks.Should().Equal(0, 1, 2, 3, 4);
    }

    /// <summary>A key the domain set is the one stored, and read back.</summary>
    [Fact]
    public async Task An_assigned_Guid_key_is_kept_and_round_trips()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnsureTablesAsync();
        var key = ParcelId.Create(Guid.NewGuid());

        await using (var write = CreateContext())
        {
            write.Parcels.Add(new Parcel { Id = key, Batch = "assigned" });
            await write.SaveChangesAsync(ct);
        }

        await using var read = CreateContext();
        (await read.Parcels.SingleAsync(parcel => parcel.Id == key, ct)).Batch.Should().Be("assigned");
    }

    /// <summary>
    /// A key over a <see cref="Guid"/> that nothing generates, the sample's customer identifier, refuses a customer added
    /// without it, before anything reaches the table.
    /// </summary>
    [Fact]
    public async Task A_Guid_key_nothing_generates_refuses_an_entity_added_without_it_and_nothing_is_written()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = EmailAddress.Create("no.key@example.com");

        await using (var write = fixture.CreateContext())
        {
            var add = () => write.Customers.Add(new Customer { Email = email, Country = CountryCode.France });

            add.Should().Throw<InvalidOperationException>().WithMessage("'Customer' was added without its key: *");
            (await write.SaveChangesAsync(ct)).Should().Be(0);
        }

        await using var read = fixture.CreateContext();
        (await read.Customers.AnyAsync(customer => customer.Email == email, ct)).Should().BeFalse("nothing reached the table");
    }

    /// <summary>
    /// A key over a <see cref="short"/>, an <see cref="int"/> or a <see cref="long"/> is the engine's identity column, of
    /// the width of its type, which numbers the rows as they are inserted; a strict context reads the keys back through
    /// the converter, which a type counted from one accepts.
    /// </summary>
    [Fact]
    public async Task Integer_keys_are_the_engines_identity_and_read_back_in_a_strict_context()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnsureTablesAsync();
        Ticket[] tickets = [new() { Label = "first" }, new() { Label = "second" }];
        Ledger[] ledgers = [new(), new()];
        Shelf[] shelves = [new(), new()];

        await using (var write = CreateStrictContext())
        {
            write.AddRange(tickets);
            write.AddRange(ledgers);
            write.AddRange(shelves);
            await write.SaveChangesAsync(ct);
        }

        tickets[1].Id.Value.Should().Be(tickets[0].Id.Value + 1).And.BePositive();
        ledgers[1].Id.Value.Should().Be(ledgers[0].Id.Value + 1).And.BePositive();
        shelves[1].Id.Value.Should().Be((short)(shelves[0].Id.Value + 1)).And.BePositive();

        await using var read = CreateStrictContext();
        var second = tickets[1].Id;
        (await read.Set<Ticket>().SingleAsync(ticket => ticket.Id == second, ct)).Label.Should().Be("second");

        foreach (var table in (string[])["tickets", "ledgers", "shelves"])
        {
            (await read.Database.SqlQueryRaw<int>(IdentitySql, table, "Id").SingleAsync(ct)).Should().Be(1, $"{table}.Id is an identity");
        }

        var type = await read.Database
            .SqlQueryRaw<string>(
                "SELECT data_type AS \"Value\" FROM information_schema.columns WHERE table_name = {0} AND column_name = {1}",
                "tickets",
                "Id")
            .SingleAsync(ct);
        type.Should().Be(Int32Type);
    }

    /// <summary>Asserts what the engine does with an integer key the domain set explicitly, on an identity column.</summary>
    /// <param name="insert">Saves a ticket whose key the domain set.</param>
    /// <returns>A task that completes once the assertion ran.</returns>
    protected abstract Task AssertAnExplicitIntegerKeyAsync(Func<Task> insert);

    /// <summary>A key the domain set on an identity column: SQL Server refuses it, PostgreSQL stores it.</summary>
    [Fact]
    public async Task An_explicit_integer_key_on_an_identity_column_is_the_engines_to_take()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnsureTablesAsync();
        var explicitKey = TicketNumber.Create(1_000_000 + Random.Shared.Next(1_000_000));

        await AssertAnExplicitIntegerKeyAsync(async () =>
        {
            await using var write = CreateContext();
            write.Add(new Ticket { Id = explicitKey, Label = "explicit" });
            await write.SaveChangesAsync(ct);
        });
    }

    private static async Task CreateTablesAsync(DatabaseFixture database)
    {
        var builder = new DbContextOptionsBuilder<KeyContext>();
        database.Configure(builder, database.ConnectionString);

        await using var context = new KeyContext(builder.Options);
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    private Task EnsureTablesAsync() => Tables.GetValue(fixture, static database => CreateTablesAsync(database));

    private KeyContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<KeyContext>();
        fixture.Configure(builder, fixture.ConnectionString);

        return new KeyContext(builder.Options);
    }

    private StrictKeyContext CreateStrictContext()
    {
        var builder = new DbContextOptionsBuilder<StrictKeyContext>();
        fixture.Configure(builder, fixture.ConnectionString);

        return new StrictKeyContext(builder.Options);
    }

    /// <summary>A parcel, whose key is generated.</summary>
    private sealed class Parcel
    {
        public ParcelId Id { get; set; }

        public string Batch { get; set; } = string.Empty;

        public int Rank { get; set; }
    }

    /// <summary>A ticket, numbered by the database from one.</summary>
    private sealed class Ticket
    {
        public TicketNumber Id { get; set; }

        public string Label { get; set; } = string.Empty;
    }

    /// <summary>A ledger, numbered by the database.</summary>
    private sealed class Ledger
    {
        public LedgerNumber Id { get; set; }
    }

    /// <summary>A shelf, numbered by the database.</summary>
    private sealed class Shelf
    {
        public ShelfNumber Id { get; set; }
    }

    /// <summary>The parcels and the numbered entities, their keys generated, mapped by the convention.</summary>
    /// <param name="options">Options bound to the running container.</param>
    private abstract class NumberedContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Parcel> Parcels => Set<Parcel>();

        /// <summary>Gets a value indicating whether what is read is validated again.</summary>
        protected abstract bool Strict { get; }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(Strict, typeof(ParcelId).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Parcel>(parcel =>
            {
                parcel.ToTable("parcels");
                parcel.Property(entity => entity.Id).HasValueObjectKeyGeneration();
            });
            modelBuilder.Entity<Ticket>(ticket =>
            {
                ticket.ToTable("tickets");
                ticket.Property(entity => entity.Id).HasValueObjectKeyGeneration();
            });
            modelBuilder.Entity<Ledger>(ledger =>
            {
                ledger.ToTable("ledgers");
                ledger.Property(entity => entity.Id).HasValueObjectKeyGeneration();
            });
            modelBuilder.Entity<Shelf>(shelf =>
            {
                shelf.ToTable("shelves");
                shelf.Property(entity => entity.Id).HasValueObjectKeyGeneration();
            });
        }
    }

    /// <summary>The model, trusting what it reads.</summary>
    /// <param name="options">Options bound to the running container.</param>
    private sealed class KeyContext(DbContextOptions<KeyContext> options) : NumberedContext(options)
    {
        protected override bool Strict => false;
    }

    /// <summary>The same model, validating what it reads.</summary>
    /// <param name="options">Options bound to the running container.</param>
    private sealed class StrictKeyContext(DbContextOptions<StrictKeyContext> options) : NumberedContext(options)
    {
        protected override bool Strict => true;
    }
}

/// <summary>Runs the key generation contract against PostgreSQL.</summary>
/// <param name="fixture">PostgreSQL container.</param>
public sealed class PostgreSqlKeyGenerationTests(PostgreSqlFixture fixture)
    : KeyGenerationTests<PostgreSqlFixture>(fixture)
{
    /// <inheritdoc />
    protected override int GuidVersion => 7;

    /// <inheritdoc />
    protected override string IdentitySql
        => "SELECT CASE WHEN is_identity = 'YES' THEN 1 ELSE 0 END AS \"Value\" FROM information_schema.columns "
           + "WHERE table_name = {0} AND column_name = {1}";

    /// <inheritdoc />
    protected override string Int32Type => "integer";

    /// <inheritdoc />
    /// <remarks>A <c>GENERATED BY DEFAULT AS IDENTITY</c> column takes a value the insert names.</remarks>
    protected override async Task AssertAnExplicitIntegerKeyAsync(Func<Task> insert) => await insert();
}

/// <summary>Runs the key generation contract against SQL Server.</summary>
/// <param name="fixture">SQL Server container.</param>
public sealed class SqlServerKeyGenerationTests(SqlServerFixture fixture)
    : KeyGenerationTests<SqlServerFixture>(fixture)
{
    /// <inheritdoc />
    protected override int GuidVersion => 4;

    /// <inheritdoc />
    protected override string IdentitySql => "SELECT COLUMNPROPERTY(OBJECT_ID({0}), {1}, 'IsIdentity') AS [Value]";

    /// <inheritdoc />
    protected override string Int32Type => "int";

    /// <inheritdoc />
    /// <remarks>An <c>IDENTITY</c> column refuses a value the insert names while <c>IDENTITY_INSERT</c> is off.</remarks>
    protected override async Task AssertAnExplicitIntegerKeyAsync(Func<Task> insert)
        => (await insert.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<Microsoft.Data.SqlClient.SqlException>()
            .WithMessage("*IDENTITY_INSERT*");
}
