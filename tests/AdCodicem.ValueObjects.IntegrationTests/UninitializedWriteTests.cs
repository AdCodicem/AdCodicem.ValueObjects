using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Verifies that a value object that never went through <c>Create</c> stores no value its type rejects, on a real
/// database engine.
/// </summary>
/// <remarks>
/// Such an instance holds the default value, and a read trusts what it finds: written as it is, an empty IBAN would
/// come back as an instance holding it. A column that takes no <c>NULL</c> refuses it before anything is sent, one that
/// takes a <c>NULL</c> stores one, and a value object that accepts its zero stores the zero.
/// </remarks>
/// <typeparam name="TFixture">Database under test.</typeparam>
public abstract class UninitializedWriteTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    /// <summary>The table of statements each fixture creates once, which the sample's model does not hold.</summary>
    private static readonly ConditionalWeakTable<DatabaseFixture, Task> Statements = new();

    [Fact]
    public async Task A_required_value_object_never_created_is_refused_and_nothing_is_written()
    {
        // The country is left as it was never set.
        var customer = new Customer { Id = CustomerId.New(), Email = EmailAddress.Create("never.created@example.com") };

        await using (var write = fixture.CreateContext())
        {
            write.Customers.Add(customer);
            var save = () => write.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Entity Framework Core wraps what a converter throws while it saves.
            var refusal = (await save.Should().ThrowAsync<DbUpdateException>()).WithInnerException<ValueObjectException>().Which;
            refusal.ValueObjectType.Should().Be<CountryCode>();
            refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
            refusal.Message.Should().StartWith("The value to write is not a valid CountryCode: ");
        }

        await using var read = fixture.CreateContext();
        var stored = await read.Customers.AnyAsync(entity => entity.Id == customer.Id, TestContext.Current.CancellationToken);

        stored.Should().BeFalse("nothing reached the table");
    }

    /// <summary>
    /// Over a value type, nothing tells an instance that was never set from a constructed zero, and an amount accepts
    /// zero: it is stored, and read back.
    /// </summary>
    [Fact]
    public async Task A_value_object_that_accepts_its_zero_stores_it()
    {
        var customer = new Customer
        {
            Id = CustomerId.New(),
            Email = EmailAddress.Create("zero.balance@example.com"),
            Country = CountryCode.France,
        };

        // The balance is left as it was never set.
        customer.Accounts.Add(new BankAccount { Iban = Iban.Create("BE68539007547034"), CustomerId = customer.Id });

        await using (var write = fixture.CreateContext())
        {
            write.Customers.Add(customer);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = fixture.CreateContext();
        var account = await read.Accounts.SingleAsync(
            entity => entity.CustomerId == customer.Id,
            TestContext.Current.CancellationToken);

        account.Balance.Should().Be(Amount.Create(0m));
    }

    /// <summary>
    /// An optional column takes a <c>NULL</c>, which reads back as no value object: the value object that was never set
    /// is stored as one, an identifier included, where its type rejects its default. A zero the type accepts is stored.
    /// </summary>
    [Fact]
    public async Task An_optional_value_object_never_created_is_stored_as_NULL()
    {
        await Statements.GetValue(fixture, static database => CreateTableAsync(database));

#pragma warning disable VO0010 // An optional value object holding an uninitialized one is what the test stores.
        var statement = new Statement
        {
            Account = Iban.Create("FR7630006000011234567890189"),
            Savings = default(Iban),
            Referrer = default(CustomerId),
            Refund = default(PaymentId),
            Overdraft = default(Amount),
        };
#pragma warning restore VO0010

        await using (var write = CreateStatementContext())
        {
            write.Statements.Add(statement);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = CreateStatementContext();
        var sql = $"SELECT {Q("Savings")}, {Q("Referrer")}, {Q("Refund")}, {Q("Overdraft")} "
                  + $"FROM {Q("statements")} WHERE {Q("Id")} = {{0}}";
        var stored = await read.Database
            .SqlQueryRaw<StoredStatement>(sql, statement.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        var reloaded = await read.Statements.SingleAsync(
            entity => entity.Id == statement.Id,
            TestContext.Current.CancellationToken);

        stored.Should().Be(new StoredStatement(null, null, null, 0m));
        reloaded.Account.Should().Be(statement.Account);
        reloaded.Savings.Should().BeNull();
        reloaded.Referrer.Should().BeNull();
        reloaded.Refund.Should().BeNull();
        reloaded.Overdraft.Should().Be(Amount.Create(0m), "zero is an amount");
    }

    private static async Task CreateTableAsync(DatabaseFixture database)
    {
        var builder = new DbContextOptionsBuilder<StatementContext>();
        database.Configure(builder, database.ConnectionString);

        await using var context = new StatementContext(builder.Options);
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    private StatementContext CreateStatementContext()
    {
        var builder = new DbContextOptionsBuilder<StatementContext>();
        fixture.Configure(builder, fixture.ConnectionString);

        return new StatementContext(builder.Options);
    }

    private string Q(string identifier) => fixture.Quote(identifier);

    /// <summary>The optional columns of a statement, as the engine holds them.</summary>
    /// <param name="Savings">The savings account.</param>
    /// <param name="Referrer">The customer who referred the holder.</param>
    /// <param name="Refund">The payment refunded.</param>
    /// <param name="Overdraft">The authorized overdraft.</param>
    private sealed record StoredStatement(string? Savings, Guid? Referrer, string? Refund, decimal? Overdraft);

    /// <summary>A statement, whose value objects but one are optional.</summary>
    private sealed class Statement
    {
        public int Id { get; set; }

        public Iban Account { get; set; }

        public Iban? Savings { get; set; }

        public CustomerId? Referrer { get; set; }

        public PaymentId? Refund { get; set; }

        public Amount? Overdraft { get; set; }
    }

    /// <summary>The statements, mapped by the conventions as the sample maps its model.</summary>
    /// <param name="options">Options bound to the running container.</param>
    private sealed class StatementContext(DbContextOptions<StatementContext> options) : DbContext(options)
    {
        public DbSet<Statement> Statements => Set<Statement>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder
                .ConfigureValueObjects(typeof(Iban).Assembly)
                .ConfigureEntityIds(typeof(PaymentId).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Statement>(statement =>
            {
                statement.ToTable("statements");
                statement.Property(entity => entity.Overdraft).HasPrecision(18, 2);
            });
    }
}

/// <summary>Runs the uninitialized write contract against PostgreSQL.</summary>
/// <param name="fixture">PostgreSQL container.</param>
public sealed class PostgreSqlUninitializedWriteTests(PostgreSqlFixture fixture)
    : UninitializedWriteTests<PostgreSqlFixture>(fixture);

/// <summary>Runs the uninitialized write contract against SQL Server.</summary>
/// <param name="fixture">SQL Server container.</param>
public sealed class SqlServerUninitializedWriteTests(SqlServerFixture fixture)
    : UninitializedWriteTests<SqlServerFixture>(fixture);
