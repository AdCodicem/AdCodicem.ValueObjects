using System.Data;
using AdCodicem.ValueObjects.Dapper;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Verifies that value objects travel through Dapper as their underlying value, on a real database engine.
/// </summary>
/// <remarks>
/// The handler's behaviour is tested in the unit suite over an in-memory table. What only a real engine shows is
/// what its provider hands back for each column type, and that a value object parameter reaches the column it is
/// compared with.
/// </remarks>
/// <typeparam name="TFixture">Database under test.</typeparam>
public abstract class DapperTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    static DapperTests() => ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly, typeof(BusinessDay).Assembly);

    [Fact]
    public async Task A_value_object_is_a_Dapper_parameter_and_lands_as_its_underlying_value()
    {
        var customer = NewCustomer("dapper.write@example.com");
        var account = new BankAccount
        {
            Iban = Iban.Create("FR4930006000011234567890190"),
            CustomerId = customer.Id,
            Balance = Amount.Create(10m),
        };

        await using (var connection = fixture.CreateConnection())
        {
            await connection.ExecuteAsync(Command(
                $"INSERT INTO {Q("customers")} ({Q("Id")}, {Q("Email")}, {Q("Country")}) VALUES (@Id, @Email, @Country)",
                new { customer.Id, customer.Email, customer.Country }));

            await connection.ExecuteAsync(Command(
                $"INSERT INTO {Q("accounts")} ({Q("Iban")}, {Q("CustomerId")}, {Q("Balance")}) "
                + "VALUES (@Iban, @CustomerId, @Balance)",
                account));
        }

        await using var read = fixture.CreateContext();
        var sql = $"SELECT {Q("Iban")} AS {Q("Value")} FROM {Q("accounts")} WHERE {Q("CustomerId")} = {{0}}";
        var storedIban = await read.Database
            .SqlQueryRaw<string>(sql, customer.Id.Value)
            .SingleAsync(TestContext.Current.CancellationToken);

        // Read back through raw SQL, the column holds the bare value; read back through the model, every column
        // holds what the value object carried.
        storedIban.Should().Be("FR4930006000011234567890190");

        var reloaded = await read.Customers
            .Include(entity => entity.Accounts)
            .SingleAsync(entity => entity.Id == customer.Id, TestContext.Current.CancellationToken);

        reloaded.Email.Should().Be(customer.Email);
        reloaded.Country.Should().Be(customer.Country);
        reloaded.Accounts.Should().ContainSingle().Which.Balance.Should().Be(Amount.Create(10m));
    }

    [Fact]
    public async Task A_row_read_through_Dapper_materializes_its_value_objects()
    {
        var customer = NewCustomer("dapper.read@example.com");
        var iban = Iban.Create("FR2230006000011234567890191");
        customer.Accounts.Add(new BankAccount { Iban = iban, CustomerId = customer.Id, Balance = Amount.Create(1250.5m) });

        await using (var write = fixture.CreateContext())
        {
            write.Customers.Add(customer);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var connection = fixture.CreateConnection();
        var account = await connection.QuerySingleAsync<BankAccount>(Command(
            $"SELECT {Q("Iban")}, {Q("CustomerId")}, {Q("Balance")} FROM {Q("accounts")} WHERE {Q("Iban")} = @iban",
            new { iban }));
        var single = await connection.QuerySingleAsync<Iban>(Command(
            $"SELECT {Q("Iban")} FROM {Q("accounts")} WHERE {Q("CustomerId")} = @id",
            new { id = customer.Id }));

        account.Iban.Should().Be(iban);
        account.CustomerId.Should().Be(customer.Id);
        account.Balance.Should().Be(Amount.Create(1250.50m));
        single.Should().Be(iban);
    }

    [Fact]
    public async Task A_NULL_column_reads_as_no_value_object_and_is_refused_for_a_required_one()
    {
        var customer = NewCustomer("dapper.null@example.com");

        await using (var write = fixture.CreateContext())
        {
            write.Customers.Add(customer);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // The customer holds no account, so the join finds none and the column is NULL.
        var sql = $"SELECT a.{Q("Iban")} FROM {Q("customers")} c "
                  + $"LEFT JOIN {Q("accounts")} a ON a.{Q("CustomerId")} = c.{Q("Id")} WHERE c.{Q("Id")} = @id";

        await using var connection = fixture.CreateConnection();
        var optional = await connection.QuerySingleAsync<Iban?>(Command(sql, new { id = customer.Id }));
        var required = () => connection.QuerySingleAsync<Iban>(Command(sql, new { id = customer.Id }));

        optional.Should().BeNull();
        await required.Should().ThrowAsync<DataException>().WithMessage("*NULL*Iban*");
    }

    /// <summary>
    /// For a mapped member, Dapper checks for a NULL before it calls the handler, and never calls it: the member
    /// keeps the uninitialized value object, and nothing throws.
    /// </summary>
    [Fact]
    public async Task A_NULL_column_leaves_a_required_member_uninitialized()
    {
        var customer = NewCustomer("dapper.member@example.com");

        await using (var write = fixture.CreateContext())
        {
            write.Customers.Add(customer);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // The customer holds no account, so the join finds none and the account's columns are NULL.
        var sql = $"SELECT a.{Q("Iban")}, c.{Q("Id")} AS {Q("CustomerId")}, a.{Q("Balance")} "
                  + $"FROM {Q("customers")} c LEFT JOIN {Q("accounts")} a ON a.{Q("CustomerId")} = c.{Q("Id")} "
                  + $"WHERE c.{Q("Id")} = @id";

        await using var connection = fixture.CreateConnection();
        var account = await connection.QuerySingleAsync<BankAccount>(Command(sql, new { id = customer.Id }));

        account.Iban.IsDefault.Should().BeTrue();
        account.Balance.IsDefault.Should().BeTrue();
        account.CustomerId.Should().Be(customer.Id);
    }

    [Fact]
    public async Task A_guid_value_object_returned_as_text_is_parsed_and_validated()
    {
        var id = CustomerId.New();

        await using var connection = fixture.CreateConnection();
        var parsed = await connection.QuerySingleAsync<CustomerId>(Command("SELECT CAST(@id AS varchar(36))", new { id }));
        var refused = () => connection.QuerySingleAsync<CustomerId>(
            Command("SELECT CAST('00000000-0000-0000-0000-000000000000' AS varchar(36))"));

        parsed.Should().Be(id);
        await refused.Should().ThrowAsync<DataException>().WithMessage("*not a valid CustomerId*must not be empty*");
    }

    [Fact]
    public async Task A_decimal_value_object_returned_as_a_float_is_converted()
    {
        await using var connection = fixture.CreateConnection();

        var amount = await connection.QuerySingleAsync<Amount>(Command("SELECT CAST(12.5 AS float)"));

        amount.Should().Be(Amount.Create(12.5m));
    }

    /// <summary>
    /// Each provider returns its own type for some of these columns - SQL Server a DateTime for a date and a
    /// TimeSpan for a time, Npgsql a DateOnly and a TimeOnly for them - and the handler reads either.
    /// </summary>
    [Fact]
    public async Task Dates_and_times_read_into_their_value_objects_whatever_type_the_provider_returns()
    {
        await using var connection = fixture.CreateConnection();

        var day = await connection.QuerySingleAsync<BusinessDay>(Command("SELECT CAST('2024-05-17' AS date)"));
        var settled = await connection.QuerySingleAsync<SettledAt>(Command("SELECT CAST('2024-05-17' AS date)"));
        var cutOff = await connection.QuerySingleAsync<CutOffTime>(Command("SELECT CAST('09:30:00' AS time)"));
        var delay = await connection.QuerySingleAsync<ClearingDelay>(Command("SELECT CAST('09:30:00' AS time)"));

        day.Should().Be(BusinessDay.Create(new DateOnly(2024, 5, 17)));
        settled.Should().Be(SettledAt.Create(new DateTime(2024, 5, 17)));
        cutOff.Should().Be(CutOffTime.Create(new TimeOnly(9, 30)));
        delay.Should().Be(ClearingDelay.Create(new TimeSpan(9, 30, 0)));
    }

    /// <summary>
    /// SQL Server returns a <c>datetimeoffset</c> as a DateTimeOffset; Npgsql returns a <c>timestamptz</c> as a UTC
    /// DateTime, which the handler reads as the same instant at offset zero.
    /// </summary>
    [Fact]
    public async Task An_instant_with_a_zone_reads_into_a_DateTimeOffset_value_object()
    {
        var sql = fixture.ProviderName == "PostgreSql"
            ? "SELECT CAST('2024-05-17T12:00:00+02:00' AS timestamptz)"
            : "SELECT CAST('2024-05-17T10:00:00+00:00' AS datetimeoffset)";

        await using var connection = fixture.CreateConnection();
        var received = await connection.QuerySingleAsync<ReceivedAt>(Command(sql));

        received.Value.Should().Be(new DateTimeOffset(2024, 5, 17, 10, 0, 0, TimeSpan.Zero));
        received.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    /// <summary>
    /// SQL Server returns a <c>datetime2</c>, and Npgsql a <c>timestamp</c>, as a DateTime that names no zone: no
    /// offset can be taken from it without guessing one, and the handler refuses it.
    /// </summary>
    [Fact]
    public async Task An_instant_without_a_zone_is_refused_for_a_DateTimeOffset_value_object()
    {
        var sql = fixture.ProviderName == "PostgreSql"
            ? "SELECT CAST('2024-05-17T10:00:00' AS timestamp)"
            : "SELECT CAST('2024-05-17T10:00:00' AS datetime2)";

        await using var connection = fixture.CreateConnection();
        var read = () => connection.QuerySingleAsync<ReceivedAt>(Command(sql));

        await read.Should().ThrowAsync<DataException>()
            .WithMessage("The DateTime read names no zone*ReceivedAt, a value object over DateTimeOffset.");
    }

    private static Customer NewCustomer(string email) => new()
    {
        Id = CustomerId.New(),
        Email = EmailAddress.Create(email),
        Country = CountryCode.France,
    };

    private static CommandDefinition Command(string sql, object? parameters = null)
        => new(sql, parameters, cancellationToken: TestContext.Current.CancellationToken);

    private string Q(string identifier) => fixture.Quote(identifier);
}

/// <summary>Runs the Dapper contract against PostgreSQL.</summary>
/// <param name="fixture">PostgreSQL container.</param>
public sealed class PostgreSqlDapperTests(PostgreSqlFixture fixture) : DapperTests<PostgreSqlFixture>(fixture);

/// <summary>Runs the Dapper contract against SQL Server.</summary>
/// <param name="fixture">SQL Server container.</param>
public sealed class SqlServerDapperTests(SqlServerFixture fixture) : DapperTests<SqlServerFixture>(fixture);
