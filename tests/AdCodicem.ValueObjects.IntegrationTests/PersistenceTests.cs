using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Verifies that value objects reach real columns and come back unchanged, on a real database engine.
/// </summary>
/// <typeparam name="TFixture">Database under test.</typeparam>
public abstract class PersistenceTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    [Fact]
    public async Task A_value_object_round_trips_through_the_database()
    {
        var customer = NewCustomer("round.trip@example.com", CountryCode.France);
        customer.Accounts.Add(new BankAccount
        {
            Iban = Iban.Create("FR7630006000011234567890189"),
            CustomerId = customer.Id,
            Balance = Amount.Create(1250.505m),
        });

        await using (var write = fixture.CreateContext())
        {
            write.Customers.Add(customer);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = fixture.CreateContext();
        var reloaded = await read.Customers
            .Include(entity => entity.Accounts)
            .SingleAsync(entity => entity.Id == customer.Id, TestContext.Current.CancellationToken);

        reloaded.Email.Should().Be(customer.Email);
        reloaded.Country.Should().Be(CountryCode.France);
        reloaded.Accounts.Should().ContainSingle()
            .Which.Balance.Value.Should().Be(1250.50m, "the amount normalizes to the cent before it is stored");
    }

    [Fact]
    public async Task A_value_object_is_stored_as_its_underlying_value_not_as_an_object()
    {
        var customer = NewCustomer("underlying@example.com", CountryCode.Belgium);

        await using (var write = fixture.CreateContext())
        {
            write.Customers.Add(customer);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = fixture.CreateContext();
        var sql = $"SELECT {fixture.Quote("Country")} AS {fixture.Quote("Value")} "
                  + $"FROM {fixture.Quote("customers")} WHERE {fixture.Quote("Email")} = {{0}}";

        var storedCountry = await read.Database
            .SqlQueryRaw<string>(sql, customer.Email.Value)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        // Read back through raw SQL, the column holds the bare code and nothing else.
        storedCountry.Should().Be("BE");
    }

    [Fact]
    public async Task A_comparison_on_a_value_object_is_translated_to_SQL()
    {
        var target = NewCustomer("filter.match@example.com", CountryCode.Luxembourg);
        var other = NewCustomer("filter.miss@example.com", CountryCode.Germany);

        await using (var write = fixture.CreateContext())
        {
            write.Customers.AddRange(target, other);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = fixture.CreateContext();
        var query = read.Customers.Where(entity => entity.Country == CountryCode.Luxembourg);

        // If the comparison had fallen back to client evaluation, the underlying value would not appear in the SQL.
        query.ToQueryString().Should().NotContain("Value");

        var matches = await query.ToListAsync(TestContext.Current.CancellationToken);
        matches.Should().ContainSingle().Which.Email.Should().Be(target.Email);
    }

    [Fact]
    public async Task Ordering_on_a_value_object_happens_in_the_database()
    {
        var first = NewCustomer("aaa.order@example.com", CountryCode.France);
        var second = NewCustomer("zzz.order@example.com", CountryCode.France);

        await using (var write = fixture.CreateContext())
        {
            write.Customers.AddRange(second, first);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = fixture.CreateContext();
        var ordered = await read.Customers
            .Where(entity => entity.Email == first.Email || entity.Email == second.Email)
            .OrderByDescending(entity => entity.Email)
            .Select(entity => entity.Email)
            .ToListAsync(TestContext.Current.CancellationToken);

        ordered.Should().Equal(second.Email, first.Email);
    }

    [Fact]
    public async Task The_declared_maximum_length_sizes_the_column()
    {
        await using var context = fixture.CreateContext();

        var ibanColumn = context.Model
            .FindEntityType(typeof(BankAccount))!
            .FindProperty(nameof(BankAccount.Iban))!;

        // The rule lives on the value object; the schema follows from it. The provider type is carried by the
        // converter, not by the property: GetProviderClrType only reports a type set explicitly on the property.
        ibanColumn.GetMaxLength().Should().Be(34);
        ibanColumn.GetValueConverter().Should().NotBeNull();
        ibanColumn.GetValueConverter()!.ProviderClrType.Should().Be<string>();

        // What actually matters is the column the engine created, so ask the engine rather than the model.
        var sql = "SELECT data_type AS \"DataType\", character_maximum_length AS \"MaximumLength\" "
                  + "FROM information_schema.columns WHERE table_name = {0} AND column_name = {1}";

        var column = await context.Database
            .SqlQueryRaw<ColumnDefinition>(sql, "accounts", nameof(BankAccount.Iban))
            .SingleAsync(TestContext.Current.CancellationToken);

        column.DataType.Should().Be(fixture.BoundedTextType, "an IBAN is stored as bounded text, not as a blob or an object");
        column.MaximumLength.Should().Be(34, "the MaxLength declared on the value object sizes the column");
    }

    /// <summary>One row of <c>information_schema.columns</c>, as both providers expose it.</summary>
    /// <param name="DataType">Provider specific name of the column type.</param>
    /// <param name="MaximumLength">Declared maximum length, when the type is bounded.</param>
    private sealed record ColumnDefinition(string DataType, int? MaximumLength);

    [Fact]
    public async Task Change_tracking_uses_the_comparison_the_value_object_declares()
    {
        var customer = NewCustomer("tracking@example.com", CountryCode.France);

        await using (var write = fixture.CreateContext())
        {
            write.Customers.Add(customer);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var update = fixture.CreateContext();
        var tracked = await update.Customers.SingleAsync(
            entity => entity.Id == customer.Id,
            TestContext.Current.CancellationToken);

        // Assigning an equal value must not look like a change.
        tracked.Country = CountryCode.Create("fr");

        update.ChangeTracker.DetectChanges();
        update.Entry(tracked).State.Should().Be(EntityState.Unchanged);
    }

    private static Customer NewCustomer(string email, CountryCode country) => new()
    {
        Id = CustomerId.New(),
        Email = EmailAddress.Create(email),
        Country = country,
    };
}

/// <summary>Runs the persistence contract against PostgreSQL.</summary>
/// <param name="fixture">PostgreSQL container.</param>
public sealed class PostgreSqlPersistenceTests(PostgreSqlFixture fixture) : PersistenceTests<PostgreSqlFixture>(fixture);

/// <summary>Runs the persistence contract against SQL Server.</summary>
/// <param name="fixture">SQL Server container.</param>
public sealed class SqlServerPersistenceTests(SqlServerFixture fixture) : PersistenceTests<SqlServerFixture>(fixture);
