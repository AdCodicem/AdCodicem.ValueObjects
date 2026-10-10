using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Verifies, on a real database engine, that a collection of value objects is stored as the primitive collection the
/// convention maps it to: an array of the element's column type on PostgreSQL, a JSON array on SQL Server, whose
/// elements a query reads as the element's type.
/// </summary>
/// <typeparam name="TFixture">Database under test.</typeparam>
public abstract class PrimitiveCollectionTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    /// <summary>The table of portfolios each fixture creates once, which the sample's model does not hold.</summary>
    private static readonly ConditionalWeakTable<DatabaseFixture, Task> Portfolios = new();

    /// <summary>Gets the SQL the provider reads the elements of a collection through, in a query over them.</summary>
    protected abstract string ElementsSql { get; }

    /// <summary>
    /// A collection round-trips, null elements included, and a query over its elements runs on the database, which reads
    /// them as the value object's column type.
    /// </summary>
    [Fact]
    public async Task A_collection_round_trips_and_a_query_over_its_elements_is_translated()
    {
        await EnsureTableAsync();
        var iban = Iban.Create("BE68539007547034");
        var portfolio = new Portfolio
        {
            Id = Random.Shared.Next(),
            Ibans = [Iban.Create("FR7630006000011234567890189"), iban],
            Countries = [CountryCode.France, CountryCode.Belgium],
            Previous = [null, iban],
            Holders = [CustomerId.New()],
            Limits = [Amount.Create(10m), null],
        };

        await using (var write = CreateContext())
        {
            write.Portfolios.Add(portfolio);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = CreateContext();
        var id = portfolio.Id;
        var reloaded = await read.Portfolios.AsNoTracking().SingleAsync(entity => entity.Id == id, TestContext.Current.CancellationToken);
        reloaded.Ibans.Should().Equal(portfolio.Ibans);
        reloaded.Countries.Should().Equal(portfolio.Countries);
        reloaded.Previous.Should().Equal(portfolio.Previous);
        reloaded.Holders.Should().Equal(portfolio.Holders);
        reloaded.Limits.Should().Equal(portfolio.Limits);

        Iban? previous = iban;
        var byIban = read.Portfolios.Where(entity => entity.Id == id && entity.Ibans.Contains(iban));
        byIban.ToQueryString().Should().Contain(ElementsSql);
        (await byIban.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        (await read.Portfolios.CountAsync(entity => entity.Id == id && entity.Previous.Contains(previous), TestContext.Current.CancellationToken))
            .Should().Be(1);
    }

    /// <summary>
    /// An element the value object rejects is refused in a collection of the value object, before anything reaches the
    /// table, and stored as a null in a collection of the optional value object over text.
    /// </summary>
    [Fact]
    public async Task An_element_the_value_object_rejects_is_refused_or_stored_as_a_null()
    {
        await EnsureTableAsync();
#pragma warning disable VO0010 // The uninitialized instances are what the converters refuse, or store as a null.
        var refused = new Portfolio { Id = Random.Shared.Next(), Holders = [default] };
        var stored = new Portfolio { Id = Random.Shared.Next(), Previous = [default(Iban)] };
#pragma warning restore VO0010

        await using (var write = CreateContext())
        {
            write.Portfolios.Add(refused);
            var save = () => write.SaveChangesAsync(TestContext.Current.CancellationToken);

            var thrown = await save.Should().ThrowAsync<DbUpdateException>();
            ValueObjectErrors.TryGetCode(thrown.Which, out var code).Should().BeTrue();
            code.Should().Be(ValueObjectErrorCodes.Required);
            thrown.WithInnerException<ValueObjectException>().Which.ValueObjectType.Should().Be<CustomerId>();
        }

        await using (var write = CreateContext())
        {
            write.Portfolios.Add(stored);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = CreateContext();
        (await read.Portfolios.AnyAsync(entity => entity.Id == refused.Id, TestContext.Current.CancellationToken))
            .Should().BeFalse("nothing reached the table");
        var reloaded = await read.Portfolios.AsNoTracking().SingleAsync(entity => entity.Id == stored.Id, TestContext.Current.CancellationToken);
        reloaded.Previous.Should().ContainSingle().Which.Should().BeNull();
    }

    /// <summary>Reads the column type of a collection, as the engine's catalog holds it.</summary>
    /// <param name="sql">The query, taking the table and the column, in this order.</param>
    /// <param name="column">The column.</param>
    /// <returns>The row the catalog holds for it.</returns>
    protected async Task<TRow> CatalogAsync<TRow>(string sql, string column)
    {
        await EnsureTableAsync();
        await using var context = CreateContext();

        return await context.Database.SqlQueryRaw<TRow>(sql, "portfolios", column).SingleAsync(TestContext.Current.CancellationToken);
    }

    private static async Task CreateTableAsync(DatabaseFixture database)
    {
        var builder = new DbContextOptionsBuilder<PortfolioContext>();
        database.Configure(builder, database.ConnectionString);

        await using var context = new PortfolioContext(builder.Options);
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    private Task EnsureTableAsync() => Portfolios.GetValue(fixture, static database => CreateTableAsync(database));

    private PortfolioContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<PortfolioContext>();
        fixture.Configure(builder, fixture.ConnectionString);

        return new PortfolioContext(builder.Options);
    }

    /// <summary>A portfolio, holding collections of value objects.</summary>
    protected sealed class Portfolio
    {
        public int Id { get; set; }

        public List<Iban> Ibans { get; set; } = [];

        public CountryCode[] Countries { get; set; } = [];

        public List<Iban?> Previous { get; set; } = [];

        public List<CustomerId> Holders { get; set; } = [];

        public List<Amount?> Limits { get; set; } = [];
    }

    /// <summary>The portfolios, mapped by the convention as the sample maps its model.</summary>
    /// <param name="options">Options bound to the running container.</param>
    private sealed class PortfolioContext(DbContextOptions<PortfolioContext> options) : DbContext(options)
    {
        public DbSet<Portfolio> Portfolios => Set<Portfolio>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(typeof(Iban).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Portfolio>(portfolio =>
            {
                portfolio.ToTable("portfolios");
                portfolio.Property(entity => entity.Id).ValueGeneratedNever();
            });
    }
}

/// <summary>Runs the primitive collection contract against PostgreSQL, whose arrays have an element type.</summary>
/// <param name="fixture">PostgreSQL container.</param>
public sealed class PostgreSqlPrimitiveCollectionTests(PostgreSqlFixture fixture)
    : PrimitiveCollectionTests<PostgreSqlFixture>(fixture)
{
    /// <inheritdoc />
    protected override string ElementsSql => "= ANY (";

    /// <summary>
    /// A collection is an array of the element's column type, sized by the value object. <c>information_schema</c> shows
    /// an array of <c>character varying</c>, whose length PostgreSQL reports only through <c>format_type</c>.
    /// </summary>
    [Fact]
    public async Task A_collection_is_an_array_of_the_column_type_of_its_element()
    {
        var column = await CatalogAsync<ArrayColumn>(
            "SELECT data_type AS \"DataType\", udt_name AS \"ElementType\" FROM information_schema.columns "
            + "WHERE table_name = {0} AND column_name = {1}",
            nameof(Portfolio.Ibans));
        var type = await CatalogAsync<FormattedType>(
            "SELECT format_type(a.atttypid, a.atttypmod) AS \"Type\" FROM pg_attribute a "
            + "WHERE a.attrelid = {0}::regclass AND a.attname = {1}",
            nameof(Portfolio.Ibans));
        var countries = await CatalogAsync<FormattedType>(
            "SELECT format_type(a.atttypid, a.atttypmod) AS \"Type\" FROM pg_attribute a "
            + "WHERE a.attrelid = {0}::regclass AND a.attname = {1}",
            nameof(Portfolio.Countries));

        column.Should().Be(new ArrayColumn("ARRAY", "_varchar"));
        type.Type.Should().Be("character varying(34)[]", "the MaxLength declared on the value object sizes each element");
        countries.Type.Should().Be("character varying(2)[]");
    }

    /// <summary>One row of <c>information_schema.columns</c> for an array.</summary>
    /// <param name="DataType">The type of the column.</param>
    /// <param name="ElementType">The name PostgreSQL gives the array type.</param>
    private sealed record ArrayColumn(string DataType, string ElementType);

    /// <summary>The type of a column, as <c>format_type</c> writes it.</summary>
    /// <param name="Type">The type.</param>
    private sealed record FormattedType(string Type);
}

/// <summary>Runs the primitive collection contract against SQL Server, which stores a collection as JSON.</summary>
/// <param name="fixture">SQL Server container.</param>
public sealed class SqlServerPrimitiveCollectionTests(SqlServerFixture fixture)
    : PrimitiveCollectionTests<SqlServerFixture>(fixture)
{
    /// <inheritdoc />
    protected override string ElementsSql => "OPENJSON([p].[Ibans]) WITH ([value] nvarchar(34) '$')";

    /// <summary>
    /// A collection is a JSON array in an unbounded text column; a query reads its elements as the column type of the
    /// value object, sized by it (<see cref="ElementsSql"/>).
    /// </summary>
    [Fact]
    public async Task A_collection_is_a_JSON_array_in_an_unbounded_text_column()
    {
        var column = await CatalogAsync<TextColumn>(
            "SELECT data_type AS \"DataType\", character_maximum_length AS \"MaximumLength\" FROM information_schema.columns "
            + "WHERE table_name = {0} AND column_name = {1}",
            nameof(Portfolio.Ibans));

        column.Should().Be(new TextColumn("nvarchar", -1), "the array is nvarchar(max) JSON");
    }

    /// <summary>One row of <c>information_schema.columns</c> for a text column.</summary>
    /// <param name="DataType">The type of the column.</param>
    /// <param name="MaximumLength">Its length, <c>-1</c> for <c>max</c>.</param>
    private sealed record TextColumn(string DataType, int? MaximumLength);
}
