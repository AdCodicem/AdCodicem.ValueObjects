using System.Linq.Expressions;
using AdCodicem.ValueObjects.Marten;
using Marten;
using Marten.Linq;
using Npgsql;
using AutoCreate = JasperFx.AutoCreate;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>A shipment as a Marten document.</summary>
public sealed class MartenShipment
{
    /// <summary>Gets or sets the document's identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the order of the shipment among the others.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the account charged, a duplicated field.</summary>
    public Iban Account { get; set; }

    /// <summary>Gets or sets the quantity shipped.</summary>
    public Quantity Quantity { get; set; }

    /// <summary>Gets or sets the country shipped to.</summary>
    public CountryCode Country { get; set; }

    /// <summary>Gets or sets the date of birth of the recipient.</summary>
    public BirthDate Born { get; set; }

    /// <summary>Gets or sets the account to fall back on.</summary>
    public Iban? Backup { get; set; }
}

/// <summary>A compiled query over a collection of value objects, which the package binds on every call.</summary>
public sealed class MartenShipmentsTo : ICompiledListQuery<MartenShipment>
{
    /// <summary>Gets or sets the countries asked for.</summary>
    public List<CountryCode> Countries { get; set; } = [];

    /// <inheritdoc />
    public Expression<Func<IMartenQueryable<MartenShipment>, IEnumerable<MartenShipment>>> QueryIs()
        => shipments => shipments.Where(x => x.Country.IsOneOf(Countries)).OrderBy(x => x.Index);
}

/// <summary>
/// Marten, at the floor the package declares, on the next major and PostgreSQL 17: value objects in its LINQ as their
/// underlying values, a duplicated field as a column of the underlying type, a compiled query's collection bound on every
/// call, and a stored value the rules refuse refused with its code however it is read.
/// </summary>
[Trait("Requires", "Docker")]
public sealed class MartenTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private static readonly List<CountryCode> Benelux = [CountryCode.Belgium, CountryCode.Luxembourg];

    [Fact]
    public async Task Value_objects_are_queried_duplicated_and_bound_as_their_values_and_read_back_through_their_rules()
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var store = DocumentStore.For(options =>
        {
            options.Connection(fixture.ConnectionString);
            options.DatabaseSchemaName = "compat_marten";
            options.AutoCreateSchemaObjects = AutoCreate.All;
            options.UseValueObjects();
            options.Schema.For<MartenShipment>().Duplicate(x => x.Account);
        });
        await using (var write = store.LightweightSession())
        {
            write.Store(
                Shipment(0, "FR7630006000011234567890189", 3, CountryCode.France, new DateOnly(1980, 5, 1)),
                Shipment(1, "BE71096123456769", 7, CountryCode.Belgium, new DateOnly(1990, 5, 1)),
                Shipment(2, "LU280019400644750000", 12, CountryCode.Luxembourg, new DateOnly(2000, 5, 1)));
            await write.SaveChangesAsync(cancellation);
        }

        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync(cancellation);
            await using var command = new NpgsqlCommand(
                "select format_type(atttypid, atttypmod) from pg_attribute where attrelid = 'compat_marten.mt_doc_martenshipment'::regclass and attname = 'account'",
                connection);
            (await command.ExecuteScalarAsync(cancellation)).Should().Be("character varying(34)");
        }

        await using (var session = store.QuerySession())
        {
            (await Indexes(session.Query<MartenShipment>().Where(static x => x.Quantity > Quantity.Create(5)), cancellation)).Should().Equal(1, 2);
            (await Indexes(session.Query<MartenShipment>().Where(static x => x.Account.Value.StartsWith("BE")), cancellation)).Should().Equal(1);
            (await Indexes(session.Query<MartenShipment>().Where(static x => Benelux.Contains(x.Country)), cancellation)).Should().Equal(1, 2);
            (await Indexes(session.Query<MartenShipment>().Where(static x => x.Born < BirthDate.Create(new DateOnly(1995, 1, 1))), cancellation)).Should().Equal(0, 1);
            (await Indexes(session.Query<MartenShipment>().Where(static x => x.Backup == null), cancellation)).Should().Equal(0, 1, 2);
            (await session.Query<MartenShipment>().MaxAsync(static x => x.Quantity, cancellation)).Should().Be(Quantity.Create(12));

            (await session.QueryAsync(new MartenShipmentsTo { Countries = [CountryCode.France] }, cancellation)).Select(static x => x.Index).Should().Equal(0);
            (await session.QueryAsync(new MartenShipmentsTo { Countries = Benelux }, cancellation)).Select(static x => x.Index).Should().Equal(1, 2);
        }

        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync(cancellation);
            await using var command = new NpgsqlCommand(
                "update compat_marten.mt_doc_martenshipment set data = jsonb_set(data, '{Quantity}', '500')",
                connection);
            await command.ExecuteNonQueryAsync(cancellation);
        }

        await using (var refused = store.QuerySession())
        {
            foreach (var read in new Func<Task>[]
            {
                () => refused.Query<MartenShipment>().ToListAsync(cancellation),
                () => refused.Query<MartenShipment>().Select(static x => x.Quantity).ToListAsync(cancellation),
            })
            {
                var refusal = (await FluentActions.Awaiting(read).Should().ThrowAsync<ValueObjectJsonException>()).Which;
                refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
            }
        }
    }

    private static MartenShipment Shipment(int index, string account, int quantity, CountryCode country, DateOnly born) => new()
    {
        Id = Guid.CreateVersion7(),
        Index = index,
        Account = Iban.Create(account),
        Quantity = Quantity.Create(quantity),
        Country = country,
        Born = BirthDate.Create(born),
    };

    private static async Task<List<int>> Indexes(IQueryable<MartenShipment> query, CancellationToken cancellation)
        => [.. (await query.ToListAsync(cancellation)).Select(static x => x.Index).Order()];
}
