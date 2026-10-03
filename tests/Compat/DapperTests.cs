using AdCodicem.ValueObjects.Dapper;
using Dapper;
using Microsoft.Data.Sqlite;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>A row read through Dapper.</summary>
public sealed class OrderLine
{
    /// <summary>Gets or sets the purchase order reference.</summary>
    public Reference<PurchaseOrder> Purchase { get; set; }

    /// <summary>Gets or sets the account.</summary>
    public Iban Account { get; set; }

    /// <summary>Gets or sets the country.</summary>
    public CountryCode Country { get; set; }

    /// <summary>Gets or sets the quantity.</summary>
    public Quantity Quantity { get; set; }

    /// <summary>Gets or sets the payment.</summary>
    public PaymentId Payment { get; set; }
}

/// <summary>
/// The Dapper type handlers, with Microsoft.Data.Sqlite from the next major: no container, so these run wherever the
/// island builds.
/// </summary>
public sealed class DapperTests
{
    static DapperTests()
    {
        ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);

        // A construction of a generic value object is known to the application only.
        ValueObjectDapper.AddValueObjectHandler<Reference<PurchaseOrder>, string>();
    }

    [Fact]
    public async Task A_value_object_is_a_parameter_and_a_column_materializes_into_one()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync("CREATE TABLE lines (Purchase TEXT, Account TEXT, Country TEXT, Quantity INTEGER, Payment TEXT)");

        var line = new OrderLine
        {
            Purchase = Reference<PurchaseOrder>.Create("po-1"),
            Account = Iban.Create("FR7630006000011234567890189"),
            Country = CountryCode.Luxembourg,
            Quantity = Quantity.Create(7),
            Payment = PaymentId.New(),
        };
        await connection.ExecuteAsync(
            "INSERT INTO lines (Purchase, Account, Country, Quantity, Payment) VALUES (@Purchase, @Account, @Country, @Quantity, @Payment)",
            line);

        var stored = await connection.QuerySingleAsync<string>("SELECT Purchase FROM lines");
        var read = await connection.QuerySingleAsync<OrderLine>(
            "SELECT * FROM lines WHERE Account = @account",
            new { account = line.Account });

        stored.Should().Be("PO-1", "the column holds the bare underlying value");
        read.Purchase.Should().Be(line.Purchase);
        read.Account.Should().Be(line.Account);
        read.Country.Should().Be(CountryCode.Luxembourg);
        read.Quantity.Should().Be(line.Quantity, "a SQLite INTEGER comes back as a long and is converted");
        read.Payment.Should().Be(line.Payment);
    }

    [Fact]
    public async Task A_column_the_value_object_could_not_have_written_is_refused()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        // Text read into a value object over a number is validated, and 101 breaks the maximum.
        var read = () => connection.QuerySingleAsync<Quantity>("SELECT '101'");

        await read.Should().ThrowAsync<System.Data.DataException>();
    }
}
