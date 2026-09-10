using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Verifies that an entity identifier reaches the column it claims, on a real database engine.
/// </summary>
/// <remarks>
/// The claims made about entity identifiers are claims about storage — fixed width, non-Unicode, binary
/// collation, chronological index order — and none of them can be checked anywhere but against an engine that
/// actually created the column.
/// </remarks>
/// <typeparam name="TFixture">Database under test.</typeparam>
public abstract class EntityIdPersistenceTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    [Fact]
    public async Task An_identifier_round_trips_through_the_database()
    {
        var payment = NewPayment();

        await using (var write = fixture.CreateContext())
        {
            write.Payments.Add(payment);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = fixture.CreateContext();
        var reloaded = await read.Payments.SingleAsync(
            entity => entity.Id == payment.Id,
            TestContext.Current.CancellationToken);

        reloaded.Id.Should().Be(payment.Id);
        reloaded.Amount.Should().Be(payment.Amount);
    }

    /// <summary>
    /// The prefix is stored, not reconstructed on read. That is what stops a join between two tables holding
    /// bare bodies from succeeding silently in a query the application never sees.
    /// </summary>
    [Fact]
    public async Task The_column_holds_the_prefix_too()
    {
        var payment = NewPayment();

        await using (var write = fixture.CreateContext())
        {
            write.Payments.Add(payment);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = fixture.CreateContext();
        var sql = $"SELECT {fixture.Quote("Account")} AS {fixture.Quote("Value")} "
                  + $"FROM {fixture.Quote("payments")} WHERE {fixture.Quote("Id")} = {{0}}";

        var stored = await read.Database
            .SqlQueryRaw<string>(sql, payment.Id.Value)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        stored.Should().NotBeNull("the row is found by an identifier carrying its prefix");
        payment.Id.Value.Should().StartWith("pay_");
    }

    [Fact]
    public async Task The_identifier_lands_in_a_fixed_width_non_unicode_column()
    {
        await using var context = fixture.CreateContext();

        var sql = "SELECT data_type AS \"DataType\", character_maximum_length AS \"MaximumLength\" "
                  + "FROM information_schema.columns WHERE table_name = {0} AND column_name = {1}";

        var column = await context.Database
            .SqlQueryRaw<ColumnDefinition>(sql, "payments", "Id")
            .SingleAsync(TestContext.Current.CancellationToken);

        // Both bounds are equal, so the engine created CHAR rather than VARCHAR: no length prefix per row, and
        // a width that gives nothing away.
        column.DataType.Should().Be(fixture.FixedTextType);
        column.MaximumLength.Should().Be(PaymentId.Length);
        PaymentId.Length.Should().Be(25);
    }

    [Fact]
    public async Task The_identifier_column_carries_the_binary_collation()
    {
        await using var context = fixture.CreateContext();

        var sql = "SELECT collation_name AS \"Value\" FROM information_schema.columns "
                  + "WHERE table_name = {0} AND column_name = {1}";

        var collation = await context.Database
            .SqlQueryRaw<string?>(sql, "payments", "Id")
            .SingleAsync(TestContext.Current.CancellationToken);

        collation.Should().Be(fixture.BinaryCollation);
    }

    /// <summary>
    /// The monotonic time bucket only buys index locality if the column orders the way the bucket does. That
    /// holds because the Crockford alphabet ascends in ASCII and the collation is byte-wise.
    /// </summary>
    [Fact]
    public async Task Ordering_by_the_column_reproduces_the_order_they_were_minted_in()
    {
        var clock = new StoppedClock(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var minted = new List<PaymentId>();

        await using (var write = fixture.CreateContext())
        {
            for (var hour = 0; hour < 24; hour++)
            {
                var id = PaymentId.New(clock.Advance(TimeSpan.FromHours(3)), IdEntropySource.System);
                minted.Add(id);

                write.Payments.Add(new Payment
                {
                    Id = id,
                    Account = Iban.Create("FR7630006000011234567890189"),
                    Amount = Amount.Create(hour + 1m),
                });
            }

            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = fixture.CreateContext();
        var ordered = await read.Payments
            .Where(entity => minted.Contains(entity.Id))
            .OrderBy(entity => entity.Id)
            .Select(entity => entity.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        ordered.Should().Equal(minted, "the engine must sort them the way the clock produced them");
    }

    [Fact]
    public async Task A_comparison_on_an_identifier_is_translated_to_SQL()
    {
        var payment = NewPayment();

        await using (var write = fixture.CreateContext())
        {
            write.Payments.Add(payment);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = fixture.CreateContext();
        var query = read.Payments.Where(entity => entity.Id == payment.Id);

        query.ToQueryString().Should().NotContain("Value", "a client-side comparison would not name the column");
        (await query.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    private static Payment NewPayment() => new()
    {
        Id = PaymentId.New(),
        Account = Iban.Create("FR7630006000011234567890189"),
        Amount = Amount.Create(42.50m),
    };

    /// <summary>One row of <c>information_schema.columns</c>, as both providers expose it.</summary>
    /// <param name="DataType">Provider specific name of the column type.</param>
    /// <param name="MaximumLength">Declared maximum length, when the type is bounded.</param>
    private sealed record ColumnDefinition(string DataType, int? MaximumLength);

    private sealed class StoppedClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public StoppedClock Advance(TimeSpan by)
        {
            _now = _now.Add(by);

            return this;
        }
    }
}

/// <summary>Runs the identifier persistence contract against PostgreSQL.</summary>
/// <param name="fixture">PostgreSQL container.</param>
public sealed class PostgreSqlEntityIdPersistenceTests(PostgreSqlFixture fixture)
    : EntityIdPersistenceTests<PostgreSqlFixture>(fixture);

/// <summary>Runs the identifier persistence contract against SQL Server.</summary>
/// <param name="fixture">SQL Server container.</param>
public sealed class SqlServerEntityIdPersistenceTests(SqlServerFixture fixture)
    : EntityIdPersistenceTests<SqlServerFixture>(fixture);
