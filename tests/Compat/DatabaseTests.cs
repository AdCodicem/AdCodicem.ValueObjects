using System.Data.Common;
using AdCodicem.ValueObjects.Dapper;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using Dapper;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// A real engine in a container, reached through the Entity Framework Core 11 provider for it. These need Docker, which
/// GitHub's Ubuntu runners have; where it is missing they fail, they do not skip, so that CI cannot pass by running
/// nothing. Without Docker, leave them out on purpose: <c>--filter-not-trait "Requires=Docker"</c>.
/// </summary>
public abstract class DatabaseFixture : IAsyncLifetime
{
    /// <summary>Gets the connection string of the running container.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Gets the name this provider gives a bounded text column in its catalog.</summary>
    public abstract string BoundedTextType { get; }

    /// <summary>Gets the name this provider gives a fixed width text column in its catalog.</summary>
    public abstract string FixedTextType { get; }

    /// <summary>Gets the name this provider gives the column of a value object declaring <c>Unicode = false</c>.</summary>
    public abstract string NonUnicodeTextType { get; }

    /// <summary>Gets what the query text says of a parameter compared with such a column, named <c>sender</c>.</summary>
    public abstract string NonUnicodeParameterSql { get; }

    /// <summary>Gets the query reading back the type of the Dapper parameter <c>@sender</c>, in a column named Value.</summary>
    public abstract string ParameterTypeSql { get; }

    /// <summary>Gets the type Dapper's parameter of such a value object reaches the engine as.</summary>
    public abstract string NonUnicodeParameterType { get; }

    /// <summary>Gets the name this provider gives a Guid column in its catalog.</summary>
    public abstract string GuidType { get; }

    /// <summary>Gets the byte-wise collation of this provider, as its catalog reports it.</summary>
    public abstract string BinaryCollation { get; }

    /// <summary>Gets the SQL this provider reads the elements of a primitive collection through.</summary>
    public abstract string ElementsSql { get; }

    /// <summary>Gets the predicate a prefix search on the value of an IBAN translates to, under UseValueObjectQueries().</summary>
    public abstract string PrefixSql { get; }

    /// <summary>Gets the version of the GUIDs this provider generates for a Guid key.</summary>
    public abstract int GuidVersion { get; }

    /// <summary>Gets the query telling whether a column is an identity, 1 or 0 in a column named Value, taking the table and the column.</summary>
    public abstract string IdentitySql { get; }

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await StartAsync();

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public abstract ValueTask DisposeAsync();

    /// <summary>Creates a context bound to the running container.</summary>
    /// <param name="strict">Whether values read are validated again.</param>
    /// <param name="valueObjectQueries">Whether .Value of a value object is translated in a query.</param>
    /// <returns>A new context.</returns>
    public ShopContext CreateContext(bool strict = false, bool valueObjectQueries = false)
    {
        var builder = new DbContextOptionsBuilder();
        Configure(builder);
        if (valueObjectQueries)
        {
            builder.UseValueObjectQueries();
        }

        return strict ? new StrictShopContext(builder.Options) : new ShopContext(builder.Options);
    }

    /// <summary>Opens a raw connection to the running container.</summary>
    /// <returns>A connection.</returns>
    public abstract DbConnection CreateConnection();

    /// <summary>Quotes an identifier the way this provider expects.</summary>
    /// <param name="identifier">Identifier to quote.</param>
    /// <returns>The quoted identifier.</returns>
    public abstract string Quote(string identifier);

    /// <summary>Configures the provider under test.</summary>
    /// <param name="builder">Options builder.</param>
    protected abstract void Configure(DbContextOptionsBuilder builder);

    /// <summary>Starts the container.</summary>
    /// <returns>The connection string.</returns>
    protected abstract Task<string> StartAsync();
}

/// <summary>PostgreSQL, through Npgsql's provider for Entity Framework Core 11.</summary>
public sealed class PostgreSqlFixture : DatabaseFixture
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public override string BoundedTextType => "character varying";

    public override string FixedTextType => "character";

    public override string NonUnicodeTextType => "character varying";

    public override string NonUnicodeParameterSql => "p.\"Sender\" = @sender";

    public override string ParameterTypeSql => "SELECT pg_typeof(@sender)::text AS \"Value\"";

    public override string NonUnicodeParameterType => "text";

    public override string GuidType => "uuid";

    public override string BinaryCollation => "C";

    public override string ElementsSql => "= ANY (";

    public override string PrefixSql => "a.\"Iban\" LIKE @prefix_startswith";

    public override int GuidVersion => 7;

    public override string IdentitySql
        => "SELECT CASE WHEN is_identity = 'YES' THEN 1 ELSE 0 END AS \"Value\" FROM information_schema.columns "
           + "WHERE table_name = {0} AND column_name = {1}";

    public override DbConnection CreateConnection() => new NpgsqlConnection(ConnectionString);

    public override string Quote(string identifier) => $"\"{identifier}\"";

    public override async ValueTask DisposeAsync() => await _container.DisposeAsync();

    protected override void Configure(DbContextOptionsBuilder builder) => builder.UseNpgsql(ConnectionString);

    protected override async Task<string> StartAsync()
    {
        await _container.StartAsync();

        return _container.GetConnectionString();
    }
}

/// <summary>SQL Server, through Microsoft's provider for Entity Framework Core 11.</summary>
/// <remarks>
/// SQL Server's images crash on a few starts in a hundred ("Failed to load LSA", microsoft/mssql-docker#974, fixed in
/// SQL Server 2022 CU29 and 2025 CU12): a container whose server exits while it starts is replaced by a new one, up to
/// three starts in all, as the integration suite's fixture does.
/// </remarks>
public sealed class SqlServerFixture : DatabaseFixture
{
    private MsSqlContainer _container = Build();

    public override string BoundedTextType => "nvarchar";

    public override string FixedTextType => "char";

    public override string NonUnicodeTextType => "varchar";

    public override string NonUnicodeParameterSql => "DECLARE @sender varchar(11)";

    public override string ParameterTypeSql
        => "SELECT CAST(SQL_VARIANT_PROPERTY(@sender, 'BaseType') AS varchar(16)) "
           + "+ '(' + CAST(SQL_VARIANT_PROPERTY(@sender, 'MaxLength') AS varchar(8)) + ')' AS [Value]";

    public override string NonUnicodeParameterType => "varchar(11)";

    public override string GuidType => "uniqueidentifier";

    public override string BinaryCollation => "Latin1_General_BIN2";

    public override string ElementsSql => "OPENJSON(";

    public override string PrefixSql => "[a].[Iban] LIKE @prefix_startswith";

    public override int GuidVersion => 4;

    public override string IdentitySql => "SELECT COLUMNPROPERTY(OBJECT_ID({0}), {1}, 'IsIdentity') AS [Value]";

    public override DbConnection CreateConnection() => new SqlConnection(ConnectionString);

    public override string Quote(string identifier) => $"[{identifier}]";

    public override async ValueTask DisposeAsync() => await _container.DisposeAsync();

    protected override void Configure(DbContextOptionsBuilder builder) => builder.UseSqlServer(ConnectionString);

    protected override async Task<string> StartAsync()
    {
        for (var start = 1; ; start++)
        {
            try
            {
                await _container.StartAsync();

                return _container.GetConnectionString();
            }
            catch (ContainerNotRunningException) when (start < 3)
            {
                await _container.DisposeAsync();
                _container = Build();
            }
        }
    }

    private static MsSqlContainer Build() => new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
}

/// <summary>
/// What only an engine that created the columns can confirm: the column types the value objects claim, the SQL the
/// provider of the next major writes for them, and what its ADO.NET driver hands back to Dapper.
/// </summary>
/// <typeparam name="TFixture">Database under test.</typeparam>
/// <param name="fixture">Database container.</param>
public abstract class DatabaseTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    static DatabaseTests()
    {
        ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);
        ValueObjectDapper.AddValueObjectHandler<Reference<PurchaseOrder>, string>();
    }

    [Fact]
    public async Task Value_objects_round_trip_and_a_comparison_on_one_is_translated()
    {
        var customer = NewCustomer("round.trip@example.com");
        customer.Accounts.Add(new BankAccount { Iban = Iban.Create("FR7630006000011234567890189"), CustomerId = customer.Id, Balance = Amount.Create(1250.505m) });

        await using (var write = fixture.CreateContext())
        {
            write.Customers.Add(customer);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = fixture.CreateContext();
        var query = read.Customers.Include(entity => entity.Accounts).Where(entity => entity.Email == customer.Email);

        query.ToQueryString().Should().NotContain("Value");
        var reloaded = await query.SingleAsync(TestContext.Current.CancellationToken);
        reloaded.Id.Should().Be(customer.Id);
        reloaded.Accounts.Should().ContainSingle().Which.Balance.Value.Should().Be(1250.50m);
    }

    [Theory]
    [InlineData("accounts", nameof(BankAccount.Iban), 34)]
    [InlineData("customers", nameof(Customer.Email), 254)]
    [InlineData("orders", nameof(Order.Purchase), 12)]
    public async Task A_declared_maximum_length_sizes_the_column(string table, string column, int length)
    {
        var definition = await ColumnAsync(table, column);

        definition.DataType.Should().Be(fixture.BoundedTextType);
        definition.MaximumLength.Should().Be(length);
    }

    /// <summary>
    /// A value object declaring <c>Unicode = false</c> lands in non-Unicode text of its length, <c>varchar</c> on SQL
    /// Server, where Entity Framework Core 11 and Dapper declare their parameters so too.
    /// </summary>
    [Fact]
    public async Task A_value_object_declaring_its_text_non_Unicode_lands_in_non_Unicode_text()
    {
        var definition = await ColumnAsync("parcels", nameof(Parcel.Sender));
        definition.DataType.Should().Be(fixture.NonUnicodeTextType);
        definition.MaximumLength.Should().Be(11);

        var sender = Bic.Create("DEUTDEFF500");
        var parcel = new Parcel { Label = "sent", Sender = sender };
        await using (var write = fixture.CreateContext())
        {
            write.Parcels.Add(parcel);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = fixture.CreateContext();
        var query = read.Parcels.Where(entity => entity.Sender == sender);
        query.ToQueryString().Should().Contain(fixture.NonUnicodeParameterSql);
        (await query.SingleAsync(TestContext.Current.CancellationToken)).Id.Should().Be(parcel.Id);

        await using var connection = fixture.CreateConnection();
        (await connection.QuerySingleAsync<string>(fixture.ParameterTypeSql, new { sender })).Should().Be(fixture.NonUnicodeParameterType);
    }

    [Fact]
    public async Task A_Guid_value_object_lands_in_the_native_identifier_type()
        => (await ColumnAsync("customers", nameof(Customer.Id))).DataType.Should().Be(fixture.GuidType);

    [Fact]
    public async Task An_entity_identifier_lands_in_a_fixed_width_column_with_a_binary_collation()
    {
        var definition = await ColumnAsync("payments", nameof(Payment.Id));

        definition.DataType.Should().Be(fixture.FixedTextType);
        definition.MaximumLength.Should().Be(PaymentId.Length);
        definition.Collation.Should().Be(fixture.BinaryCollation);
    }

    [Fact]
    public async Task A_construction_of_a_generic_value_object_round_trips()
    {
        var id = Random.Shared.Next();
        await using (var write = fixture.CreateContext())
        {
            write.Orders.Add(new Order { Id = id, Purchase = Reference<PurchaseOrder>.Create("po-9"), Invoice = Reference<SalesInvoice>.Create("inv-9"), Quantity = Quantity.Create(2) });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = fixture.CreateContext();
        var order = await read.Orders.SingleAsync(entity => entity.Id == id && entity.Purchase == Reference<PurchaseOrder>.Create("PO-9"), TestContext.Current.CancellationToken);

        order.Invoice.Should().Be(Reference<SalesInvoice>.Create("INV-9"));
    }

    [Fact]
    public async Task A_collection_of_value_objects_round_trips_and_a_query_over_its_elements_is_translated()
    {
        var id = Random.Shared.Next();
        var belgian = Iban.Create("BE68539007547034");
        await using (var write = fixture.CreateContext())
        {
            write.Portfolios.Add(new Portfolio
            {
                Id = id,
                Ibans = [Iban.Create("FR7630006000011234567890189"), belgian],
                Previous = [null, belgian],
                Quantities = [Quantity.Create(2)],
                Orders = [Reference<PurchaseOrder>.Create("po-1")],
            });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = fixture.CreateContext();
        var query = read.Portfolios.Where(entity => entity.Id == id && entity.Ibans.Contains(belgian));
        query.ToQueryString().Should().Contain(fixture.ElementsSql, "the elements are read on the database");
        var reloaded = await query.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        reloaded.Ibans.Should().Equal(Iban.Create("FR7630006000011234567890189"), belgian);
        reloaded.Previous.Should().Equal(null, belgian);
        reloaded.Quantities.Should().Equal(Quantity.Create(2));
        reloaded.Orders.Should().Equal(Reference<PurchaseOrder>.Create("PO-1"));
    }

    [Fact]
    public async Task The_value_of_a_value_object_is_read_from_its_column_under_UseValueObjectQueries()
    {
        var customer = NewCustomer("value.query@example.com");
        var british = Iban.Create("GB82WEST12345698765432");
        customer.Accounts.Add(new BankAccount { Iban = british, CustomerId = customer.Id, Balance = Amount.Create(10m) });
        customer.Accounts.Add(new BankAccount { Iban = Iban.Create("NL91ABNA0417164300"), CustomerId = customer.Id, Balance = Amount.Create(20m) });
        var portfolio = new Portfolio { Id = Random.Shared.Next(), Ibans = [british], Quantities = [Quantity.Create(5)] };

        await using (var write = fixture.CreateContext())
        {
            write.Customers.Add(customer);
            write.Portfolios.Add(portfolio);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = fixture.CreateContext(valueObjectQueries: true);
        var id = customer.Id;
        var portfolioId = portfolio.Id;
        var prefix = "GB";
        short least = 2;

        var byPrefix = read.Accounts.Where(account => account.CustomerId == id && account.Iban.Value.StartsWith(prefix)).Select(account => account.Iban);
        byPrefix.ToQueryString().Should().Contain(fixture.PrefixSql).And.NotContain("CAST").And.NotContain("::");
        (await byPrefix.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(british);
        (await read.Portfolios.CountAsync(entity => entity.Id == portfolioId && entity.Ibans.Any(iban => iban.Value.StartsWith(prefix)), TestContext.Current.CancellationToken))
            .Should().Be(1);
        (await read.Portfolios.CountAsync(entity => entity.Id == portfolioId && entity.Quantities.Any(quantity => quantity.Value > least), TestContext.Current.CancellationToken))
            .Should().Be(1);
    }

    [Fact]
    public async Task A_strict_context_refuses_a_value_the_domain_would_reject()
    {
        var customer = NewCustomer("strict@example.com");
        var insert = $"INSERT INTO {Q("accounts")} ({Q("Iban")}, {Q("CustomerId")}, {Q("Balance")}) VALUES ({{0}}, {{1}}, {{2}})";
        await using (var write = fixture.CreateContext())
        {
            write.Customers.Add(customer);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Another writer stores an IBAN whose check digits are wrong.
            await write.Database.ExecuteSqlRawAsync(
                insert,
                ["FR0030006000011234567890190", customer.Id.Value, 1m],
                TestContext.Current.CancellationToken);
        }

        await using var strict = fixture.CreateContext(strict: true);
        var read = () => strict.Accounts.SingleAsync(account => account.CustomerId == customer.Id, TestContext.Current.CancellationToken);

        var thrown = await read.Should().ThrowAsync<Exception>();
        Chain(thrown.Which).Should().ContainItemsAssignableTo<ValueObjectException>();
    }

    [Fact]
    public async Task A_generated_Guid_value_object_key_is_the_one_the_provider_generates_for_a_Guid()
    {
        var parcels = new[] { new Parcel { Label = "first" }, new Parcel { Label = "second" } };
        await using (var write = fixture.CreateContext())
        {
            write.Parcels.AddRange(parcels);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        parcels.Should().OnlyContain(parcel => parcel.Id.Value.Version == fixture.GuidVersion);
        await using var read = fixture.CreateContext();
        var key = parcels[1].Id;
        (await read.Parcels.SingleAsync(parcel => parcel.Id == key, TestContext.Current.CancellationToken)).Label.Should().Be("second");
    }

    [Fact]
    public async Task An_integer_value_object_key_is_an_identity_column()
    {
        var tickets = new[] { new Ticket { Label = "first" }, new Ticket { Label = "second" } };
        await using (var write = fixture.CreateContext(strict: true))
        {
            write.Tickets.AddRange(tickets);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        tickets[1].Id.Value.Should().Be(tickets[0].Id.Value + 1).And.BePositive();
        await using var read = fixture.CreateContext(strict: true);
        var key = tickets[1].Id;
        (await read.Tickets.SingleAsync(ticket => ticket.Id == key, TestContext.Current.CancellationToken)).Label.Should().Be("second");
        (await read.Database.SqlQueryRaw<int>(fixture.IdentitySql, "tickets", "Id").SingleAsync(TestContext.Current.CancellationToken))
            .Should().Be(1);
    }

    [Fact]
    public async Task Dapper_writes_and_reads_value_objects_through_the_driver_of_the_next_major()
    {
        var customer = NewCustomer("dapper@example.com");
        var iban = Iban.Create("FR4930006000011234567890190");

        await using (var connection = fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                $"INSERT INTO {Q("customers")} ({Q("Id")}, {Q("Email")}, {Q("Country")}) VALUES (@Id, @Email, @Country)",
                new { customer.Id, customer.Email, customer.Country });
            await connection.ExecuteAsync(
                $"INSERT INTO {Q("accounts")} ({Q("Iban")}, {Q("CustomerId")}, {Q("Balance")}) VALUES (@Iban, @CustomerId, @Balance)",
                new BankAccount { Iban = iban, CustomerId = customer.Id, Balance = Amount.Create(10m) });
        }

        await using var read = fixture.CreateConnection();
        var account = await read.QuerySingleAsync<BankAccount>(
            $"SELECT {Q("Iban")}, {Q("CustomerId")}, {Q("Balance")} FROM {Q("accounts")} WHERE {Q("Iban")} = @iban",
            new { iban });

        account.Iban.Should().Be(iban);
        account.CustomerId.Should().Be(customer.Id);
        account.Balance.Should().Be(Amount.Create(10m));
    }

    private string Q(string identifier) => fixture.Quote(identifier);

    private async Task<ColumnDefinition> ColumnAsync(string table, string column)
    {
        await using var context = fixture.CreateContext();

        return await context.Database
            .SqlQueryRaw<ColumnDefinition>(
                "SELECT data_type AS \"DataType\", character_maximum_length AS \"MaximumLength\", collation_name AS \"Collation\" "
                + "FROM information_schema.columns WHERE table_name = {0} AND column_name = {1}",
                table,
                column)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static Customer NewCustomer(string email) => new()
    {
        Id = CustomerId.New(),
        Email = EmailAddress.Create($"{Guid.NewGuid():N}.{email}"),
        Country = CountryCode.France,
    };

    private static IEnumerable<Exception> Chain(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            yield return exception;
        }
    }

    /// <summary>One row of <c>information_schema.columns</c>, as both engines expose it.</summary>
    /// <param name="DataType">Provider specific name of the column type.</param>
    /// <param name="MaximumLength">Declared maximum length, when the type is bounded.</param>
    /// <param name="Collation">Collation, when the column has one.</param>
    private sealed record ColumnDefinition(string DataType, int? MaximumLength, string? Collation);
}

[Trait("Requires", "Docker")]
public sealed class PostgreSqlTests(PostgreSqlFixture fixture) : DatabaseTests<PostgreSqlFixture>(fixture);

[Trait("Requires", "Docker")]
public sealed class SqlServerTests(SqlServerFixture fixture) : DatabaseTests<SqlServerFixture>(fixture);
