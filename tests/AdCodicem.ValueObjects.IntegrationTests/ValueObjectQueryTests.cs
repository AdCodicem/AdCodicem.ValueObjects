using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using AdCodicem.ValueObjects.UnitTests.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Verifies, on a real database engine, that <c>UseValueObjectQueries()</c> translates <c>.Value</c> of a value object
/// to its column, typed as the underlying value: a prefix search is a <c>LIKE</c> on the column, which its index serves,
/// and a parameter compared with it is declared as the column is.
/// </summary>
/// <typeparam name="TFixture">Database under test.</typeparam>
public abstract class ValueObjectQueryTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    /// <summary>The table of gauges each fixture creates once, which the sample's model does not hold.</summary>
    private static readonly ConditionalWeakTable<DatabaseFixture, Task> Gauges = new();

    /// <summary>Gets the predicate a prefix search on the value of an IBAN translates to.</summary>
    protected abstract string PrefixSql { get; }

    /// <summary>Gets what the query text says of a parameter compared with the value of an IBAN.</summary>
    protected abstract string TextParameterSql { get; }

    /// <summary>
    /// Gives what the query text says of the parameters compared with the value of a payment's identifier: one compared
    /// with it, and the pattern of a prefix search.
    /// </summary>
    /// <param name="paymentText">The value of the identifier compared with.</param>
    /// <returns>What the query text says of each parameter.</returns>
    protected abstract IEnumerable<string> IdParameterSql(string paymentText);

    /// <summary>
    /// A prefix search, and a comparison of a number, on the value of a value object run on the database, on the column
    /// itself, with no <c>CAST</c>.
    /// </summary>
    [Fact]
    public async Task A_prefix_search_on_the_value_reads_the_column_itself()
    {
        var customer = NewCustomer("value.prefix@example.com");
        var french = Iban.Create("FR7630006000011234567890189");
        var belgian = Iban.Create("BE68539007547034");
        customer.Accounts.Add(new BankAccount { Iban = french, CustomerId = customer.Id, Balance = Amount.Create(10m) });
        customer.Accounts.Add(new BankAccount { Iban = belgian, CustomerId = customer.Id, Balance = Amount.Create(250m) });

        await using (var write = CreateContext())
        {
            write.Customers.Add(customer);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = CreateContext();
        var id = customer.Id;
        var prefix = "FR";
        var limit = 100m;

        var byPrefix = read.Accounts.Where(account => account.CustomerId == id && account.Iban.Value.StartsWith(prefix)).Select(account => account.Iban);
        byPrefix.ToQueryString().Should().Contain(PrefixSql).And.NotContain("CAST").And.NotContain("::");
        (await byPrefix.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(french);

        var byBalance = read.Accounts.Where(account => account.CustomerId == id && account.Balance.Value > limit).Select(account => account.Iban);
        (await byBalance.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(belgian);
    }

    /// <summary>
    /// A parameter compared with the value of a value object takes the type mapping of its column, sized by the rule
    /// the value object declares, and one compared with the value of an entity identifier the type of its column, fixed
    /// width and not Unicode, where the pattern of a prefix search is not Unicode either; both queries run.
    /// </summary>
    [Fact]
    public async Task A_parameter_compared_with_the_value_takes_the_declaration_of_the_column()
    {
        var customer = NewCustomer("value.parameter@example.com");
        var iban = Iban.Create("DE89370400440532013000");
        customer.Accounts.Add(new BankAccount { Iban = iban, CustomerId = customer.Id, Balance = Amount.Create(10m) });
        var payment = new Payment { Id = PaymentId.New(), Account = iban, Amount = Amount.Create(5m) };

        await using (var write = CreateContext())
        {
            write.Customers.Add(customer);
            write.Payments.Add(payment);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = CreateContext();
        var text = iban.Value;
        var paymentText = payment.Id.Value;
        var prefix = PaymentId.Prefix + "_";

        var byText = read.Accounts.Where(account => account.Iban.Value == text).Select(account => account.CustomerId);
        byText.ToQueryString().Should().Contain(TextParameterSql);
        (await byText.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(customer.Id);

        var byId = read.Payments.Where(entity => entity.Id.Value == paymentText && entity.Id.Value.StartsWith(prefix)).Select(entity => entity.Account);
        byId.ToQueryString().Should().ContainAll(IdParameterSql(paymentText)).And.NotContain("CAST").And.NotContain("::");
        (await byId.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(iban);
    }

    /// <summary>
    /// The value of a value object whose converter Entity Framework Core composes with its own conversion of the
    /// underlying type, an <see cref="sbyte"/>, a <see cref="ushort"/>, a <see cref="uint"/> or a <see cref="ulong"/>,
    /// and on SQL Server a <see cref="char"/>, reads the column, as the provider converts a property of that type, with
    /// no <c>CAST</c>, and the query runs.
    /// </summary>
    [Fact]
    public async Task The_value_of_a_value_object_whose_converter_is_composed_reads_the_column()
    {
        await Gauges.GetValue(fixture, static database => CreateGaugesAsync(database));
        var gauge = new Gauge
        {
            Id = Random.Shared.Next(),
            Adjustment = Adjustment.Create(-3),
            Port = Port.Create(8080),
            Sequence = SequenceNumber.Create(7),
            Bytes = ByteCount.Create(5_000_000_000),
            Grade = Grade.Create('B'),
        };

        await using (var write = CreateGaugeContext())
        {
            write.Gauges.Add(gauge);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = CreateGaugeContext();
        var id = gauge.Id;
        sbyte below = 0;
        ushort port = 443;
        uint sequence = 7;
        ulong bytes = 4_000_000_000;
        var grade = 'B';

        var composed = read.Gauges
            .Where(entity => entity.Id == id
                && entity.Adjustment.Value < below
                && entity.Port.Value > port
                && entity.Sequence.Value == sequence
                && entity.Bytes.Value > bytes
                && entity.Grade.Value == grade)
            .Select(entity => entity.Id);
        // The statement alone: SQL Server declares a parameter's value as a CAST.
        var sql = composed.ToQueryString();
        sql[sql.IndexOf("SELECT", StringComparison.Ordinal)..].Should().NotContain("CAST").And.NotContain("::");
        (await composed.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(id);
    }

    private static Customer NewCustomer(string email) => new()
    {
        Id = CustomerId.New(),
        Email = EmailAddress.Create(email),
        Country = CountryCode.France,
    };

    private static async Task CreateGaugesAsync(DatabaseFixture database)
    {
        var builder = new DbContextOptionsBuilder<GaugeContext>();
        database.Configure(builder, database.ConnectionString);

        await using var context = new GaugeContext(builder.Options);
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    private BankingDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<BankingDbContext>();
        fixture.Configure(builder, fixture.ConnectionString);

        return new BankingDbContext(builder.UseValueObjectQueries().Options);
    }

    private GaugeContext CreateGaugeContext()
    {
        var builder = new DbContextOptionsBuilder<GaugeContext>();
        fixture.Configure(builder, fixture.ConnectionString);

        return new GaugeContext(builder.UseValueObjectQueries().Options);
    }

    /// <summary>A gauge, holding value objects over the types a provider converts to store them.</summary>
    private sealed class Gauge
    {
        public int Id { get; set; }

        public Adjustment Adjustment { get; set; }

        public Port Port { get; set; }

        public SequenceNumber Sequence { get; set; }

        public ByteCount Bytes { get; set; }

        public Grade Grade { get; set; }
    }

    /// <summary>The gauges, mapped by the convention.</summary>
    /// <param name="options">Options bound to the running container.</param>
    private sealed class GaugeContext(DbContextOptions<GaugeContext> options) : DbContext(options)
    {
        public DbSet<Gauge> Gauges => Set<Gauge>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(typeof(Port).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Gauge>(gauge =>
            {
                gauge.ToTable("gauges");
                gauge.Property(entity => entity.Id).ValueGeneratedNever();
            });
    }
}

/// <summary>Runs the queries on the value of value objects against PostgreSQL.</summary>
/// <param name="fixture">PostgreSQL container.</param>
public sealed class PostgreSqlValueObjectQueryTests(PostgreSqlFixture fixture) : ValueObjectQueryTests<PostgreSqlFixture>(fixture)
{
    /// <inheritdoc />
    protected override string PrefixSql => "a.\"Iban\" LIKE @prefix_startswith";

    /// <inheritdoc />
    protected override string TextParameterSql => "WHERE a.\"Iban\" = @text";

    /// <inheritdoc />
    protected override IEnumerable<string> IdParameterSql(string paymentText)
        => [$"-- @paymentText='{paymentText}' (DbType = StringFixedLength)", "-- @prefix_startswith='pay\\_%' (DbType = StringFixedLength)"];
}

/// <summary>Runs the queries on the value of value objects against SQL Server.</summary>
/// <param name="fixture">SQL Server container.</param>
public sealed class SqlServerValueObjectQueryTests(SqlServerFixture fixture) : ValueObjectQueryTests<SqlServerFixture>(fixture)
{
    /// <inheritdoc />
    protected override string PrefixSql => "[a].[Iban] LIKE @prefix_startswith";

    /// <inheritdoc />
    protected override string TextParameterSql => "DECLARE @text nvarchar(34)";

    /// <inheritdoc />
    protected override IEnumerable<string> IdParameterSql(string paymentText)
        => [$"DECLARE @paymentText char({PaymentId.Length}) = '{paymentText}'", $"DECLARE @prefix_startswith varchar({PaymentId.Length})"];
}
