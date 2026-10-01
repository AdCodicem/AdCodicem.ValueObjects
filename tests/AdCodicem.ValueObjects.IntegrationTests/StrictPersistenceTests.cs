using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Verifies what the strict convention does to a row another writer stored, on a real database engine.
/// </summary>
/// <remarks>
/// The default convention trusts what it reads, because this application validated it when it wrote it. A table
/// shared with another writer - a legacy application, an ETL job - may hold what the domain would refuse, and only
/// a row written past the value objects shows what the strict convention makes of it.
/// </remarks>
/// <typeparam name="TFixture">Database under test.</typeparam>
public abstract class StrictPersistenceTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    [Fact]
    public async Task A_strict_context_normalizes_a_value_another_writer_stored()
    {
        var owner = await StoreAccountAsync("fr49 3000 6000 0112 3456 7890 190");

        await using var strict = CreateStrictContext();
        var normalized = await strict.Accounts.SingleAsync(
            account => account.CustomerId == owner,
            TestContext.Current.CancellationToken);

        await using var lenient = fixture.CreateContext();
        var trusted = await lenient.Accounts.SingleAsync(
            account => account.CustomerId == owner,
            TestContext.Current.CancellationToken);

        normalized.Iban.Value.Should().Be("FR4930006000011234567890190");
        trusted.Iban.Value.Should().Be("fr49 3000 6000 0112 3456 7890 190", "the default convention trusts the column");
    }

    [Fact]
    public async Task A_strict_context_refuses_a_value_the_domain_would_reject()
    {
        var owner = await StoreAccountAsync("FR0030006000011234567890190");

        await using var strict = CreateStrictContext();
        var read = () => strict.Accounts.SingleAsync(
            account => account.CustomerId == owner,
            TestContext.Current.CancellationToken);

        // Entity Framework Core may wrap what a converter throws while it materializes a row.
        var thrown = await read.Should().ThrowAsync<Exception>();
        Chain(thrown.Which).Should().ContainItemsAssignableTo<ValueObjectException>();
    }

    /// <summary>
    /// Stores an account the way another writer would, past the value objects, under a new customer.
    /// </summary>
    /// <param name="iban">The account number, as that writer stores it.</param>
    /// <returns>The customer holding the account.</returns>
    private async Task<CustomerId> StoreAccountAsync(string iban)
    {
        var owner = CustomerId.New();
        var insertCustomer = $"INSERT INTO {Q("customers")} ({Q("Id")}, {Q("Email")}, {Q("Country")}) "
                             + "VALUES ({0}, {1}, {2})";
        var insertAccount = $"INSERT INTO {Q("accounts")} ({Q("Iban")}, {Q("CustomerId")}, {Q("Balance")}) "
                            + "VALUES ({0}, {1}, {2})";

        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            insertCustomer,
            [owner.Value, $"{owner.Value:N}@example.com", "FR"],
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            insertAccount,
            [iban, owner.Value, 10m],
            TestContext.Current.CancellationToken);

        return owner;
    }

    private StrictBankingDbContext CreateStrictContext()
    {
        var builder = new DbContextOptionsBuilder<StrictBankingDbContext>();
        fixture.Configure(builder, fixture.ConnectionString);

        return new StrictBankingDbContext(builder.Options);
    }

    private string Q(string identifier) => fixture.Quote(identifier);

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    /// <summary>
    /// The sample's accounts, read with validation turned back on.
    /// </summary>
    /// <param name="options">Options bound to the running container.</param>
    private sealed class StrictBankingDbContext(DbContextOptions<StrictBankingDbContext> options) : DbContext(options)
    {
        public DbSet<BankAccount> Accounts => Set<BankAccount>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(strict: true, typeof(Iban).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<BankAccount>(account =>
            {
                account.ToTable("accounts");
                account.HasKey(entity => entity.Iban);
                account.Property(entity => entity.Balance).HasPrecision(18, 2);
            });
    }
}

/// <summary>Runs the strict read contract against PostgreSQL.</summary>
/// <param name="fixture">PostgreSQL container.</param>
public sealed class PostgreSqlStrictPersistenceTests(PostgreSqlFixture fixture)
    : StrictPersistenceTests<PostgreSqlFixture>(fixture);

/// <summary>Runs the strict read contract against SQL Server.</summary>
/// <param name="fixture">SQL Server container.</param>
public sealed class SqlServerStrictPersistenceTests(SqlServerFixture fixture)
    : StrictPersistenceTests<SqlServerFixture>(fixture);
