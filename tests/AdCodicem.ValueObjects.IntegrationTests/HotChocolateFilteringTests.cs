using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.HotChocolate.Data;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using AdCodicem.ValueObjects.UnitTests.Domain;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Verifies, on a real database engine, that HotChocolate.Data's filters over value objects, under
/// <c>AddValueObjectFiltering()</c>, translate to their columns: an operand read through the rules is compared with the
/// column, a list of value objects is searched as the provider stores it, and the text operations compare the column's
/// text under its collation, through <c>UseValueObjectQueries()</c>.
/// </summary>
/// <typeparam name="TFixture">Database under test.</typeparam>
public abstract class HotChocolateFilteringTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    private const string French = "FR7630006000011234567890189";
    private const string German = "DE89370400440532013000";

    /// <summary>The table of accounts each fixture creates and fills once, which the sample's model does not hold.</summary>
    private static readonly ConditionalWeakTable<DatabaseFixture, Task> Accounts = new();

    private readonly List<string> _sql = [];

    /// <summary>Gets the predicate <c>eq</c> on an IBAN translates to.</summary>
    protected abstract string EqualSql { get; }

    /// <summary>Gets the predicate <c>in</c> on an IBAN translates to.</summary>
    protected abstract string InSql { get; }

    /// <summary>Gets the predicate <c>some</c> with <c>eq</c> on a list of IBANs translates to.</summary>
    protected abstract string SomeSql { get; }

    /// <summary>Gets the predicate <c>startsWith</c> on an IBAN translates to.</summary>
    protected abstract string StartsWithSql { get; }

    /// <summary>Gets the rows <c>contains: "fr76"</c> finds, under the column's default collation.</summary>
    protected abstract string LowerCaseContains { get; }

    /// <summary>
    /// The comparable operations read their operands through the scalar, normalized, and compare them with the column
    /// itself, a nullable one and a list of value objects included, with no translator of the value.
    /// </summary>
    [Fact]
    public async Task The_comparable_operations_compare_the_operand_with_the_column()
    {
        var executor = await ExecutorAsync(text: false, translated: false);

        (await executor.RunAsync("""{ accounts(where: { iban: { eq: "fr76 3000 6000 0112 3456 7890 189" } }) { id } }""")).Should().Be("""{"accounts":[{"id":1}]}""");
        Sql.Should().Contain(EqualSql).And.Contain(French);
        (await executor.RunAsync($$"""{ accounts(where: { iban: { in: ["{{German}}", "fr76 3000 6000 0112 3456 7890 189"] } }) { id } }"""))
            .Should().Be("""{"accounts":[{"id":1},{"id":2}]}""");
        Sql.Should().Contain(InSql);
        (await executor.RunAsync("{ accounts(where: { balance: { gt: 100 } }) { id } }")).Should().Be("""{"accounts":[{"id":2}]}""");
        (await executor.RunAsync("{ accounts(where: { backup: { eq: null } }) { id } }")).Should().Be("""{"accounts":[{"id":2}]}""");
        Sql.Should().Contain("IS NULL");
        (await executor.RunAsync("""{ accounts(where: { previous: { some: { eq: "de89 3704 0044 0532 0130 00" } } }) { id } }""")).Should().Be("""{"accounts":[{"id":1}]}""");
        Sql.Should().Contain(SomeSql);
        (await executor.RunAsync("{ accounts(order: { iban: ASC }) { id } }")).Should().Be("""{"accounts":[{"id":2},{"id":1}]}""");
    }

    /// <summary>
    /// The text operations compare the column's text with the fragment as typed, as for a <c>String</c> column, under the
    /// column's collation: PostgreSQL's is case-sensitive, SQL Server's default is not.
    /// </summary>
    [Fact]
    public async Task The_text_operations_compare_the_column_under_its_collation()
    {
        var executor = await ExecutorAsync(text: true, translated: true);

        (await executor.RunAsync("""{ accounts(where: { iban: { startsWith: "FR" } }) { id } }""")).Should().Be("""{"accounts":[{"id":1}]}""");
        Sql.Should().Contain(StartsWithSql);
        (await executor.RunAsync("""{ accounts(where: { iban: { contains: "7630" } }) { id } }""")).Should().Be("""{"accounts":[{"id":1}]}""");
        (await executor.RunAsync("""{ accounts(where: { backup: { ncontains: "backup" } }) { id } }""")).Should().Be("""{"accounts":[{"id":2}]}""");
        (await executor.RunAsync("""{ accounts(where: { previous: { some: { startsWith: "DE" } } }) { id } }""")).Should().Be("""{"accounts":[{"id":1}]}""");
        (await executor.RunAsync("""{ accounts(where: { iban: { contains: "fr76" } }) { id } }""")).Should().Be(LowerCaseContains);
    }

    /// <summary>Without the translator of the value, a text operation fails at its first query.</summary>
    [Fact]
    public async Task Without_the_translator_a_text_operation_fails_when_it_runs()
    {
        var executor = await ExecutorAsync(text: true, translated: false);

        (await executor.RunAsync("""{ accounts(where: { iban: { startsWith: "FR" } }) { id } }""")).Should().Contain("Unexpected Execution Error");
    }

    /// <summary>Gets the SQL run since the last read, on one line.</summary>
    private string Sql
    {
        get
        {
            var sql = Regex.Replace(string.Join(" ", _sql), @"\s+", " ");
            _sql.Clear();
            return sql;
        }
    }

    private static async Task CreateAccountsAsync(DatabaseFixture database)
    {
        var builder = new DbContextOptionsBuilder<FilteredContext>();
        database.Configure(builder, database.ConnectionString);

        await using var context = new FilteredContext(builder.Options);
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        context.Accounts.AddRange(
            new FilteredAccount
            {
                Id = 1,
                Iban = Iban.Create(French),
                Balance = Amount.Create(10m),
                Backup = EmailAddress.Create("ada@backup.org"),
                Previous = [Iban.Create(German)],
            },
            new FilteredAccount { Id = 2, Iban = Iban.Create(German), Balance = Amount.Create(250m), Backup = null, Previous = [] });
        await context.SaveChangesAsync();
    }

    private async Task<IRequestExecutor> ExecutorAsync(bool text, bool translated)
    {
        await Accounts.GetValue(fixture, static database => CreateAccountsAsync(database));

        var services = new ServiceCollection();
        services.AddDbContext<FilteredContext>(options =>
        {
            fixture.Configure(options, fixture.ConnectionString);
            options.EnableSensitiveDataLogging().LogTo(_sql.Add, [RelationalEventId.CommandExecuted], LogLevel.Information, DbContextLoggerOptions.None);
            if (translated)
            {
                options.UseValueObjectQueries();
            }
        });
        services.AddGraphQL()
            .AddQueryType<FilteredQuery>()
            .AddSorting()
            .AddFiltering()
            .AddValueObjectFiltering(options =>
            {
                if (text)
                {
                    options.IncludeTextOperations();
                }
            });

        return await services.BuildServiceProvider().GetRequestExecutorAsync(cancellationToken: TestContext.Current.CancellationToken);
    }
}

/// <summary>Runs HotChocolate.Data's filters over value objects against PostgreSQL.</summary>
/// <param name="fixture">PostgreSQL container.</param>
public sealed class PostgreSqlHotChocolateFilteringTests(PostgreSqlFixture fixture) : HotChocolateFilteringTests<PostgreSqlFixture>(fixture)
{
    /// <inheritdoc />
    protected override string EqualSql => "WHERE f.\"Iban\" = @p";

    /// <inheritdoc />
    protected override string InSql => "WHERE f.\"Iban\" = ANY (@p)";

    /// <inheritdoc />
    protected override string SomeSql => "WHERE @p = ANY (f.\"Previous\")";

    /// <inheritdoc />
    protected override string StartsWithSql => "WHERE f.\"Iban\" LIKE @p_startswith";

    /// <inheritdoc />
    protected override string LowerCaseContains => """{"accounts":[]}""";
}

/// <summary>Runs HotChocolate.Data's filters over value objects against SQL Server.</summary>
/// <param name="fixture">SQL Server container.</param>
public sealed class SqlServerHotChocolateFilteringTests(SqlServerFixture fixture) : HotChocolateFilteringTests<SqlServerFixture>(fixture)
{
    /// <inheritdoc />
    protected override string EqualSql => "WHERE [f].[Iban] = @p";

    /// <inheritdoc />
    protected override string InSql => "WHERE [f].[Iban] IN (@p1, @p2)";

    /// <inheritdoc />
    protected override string SomeSql => "OPENJSON([f].[Previous]) WITH ([value] nvarchar(34) '$')";

    /// <inheritdoc />
    protected override string StartsWithSql => "WHERE [f].[Iban] LIKE @p_startswith ESCAPE N'\\'";

    /// <inheritdoc />
    protected override string LowerCaseContains => """{"accounts":[{"id":1}]}""";
}

/// <summary>Runs operations and reads their results as JSON.</summary>
internal static class GraphQLExecution
{
    /// <summary>Executes an operation.</summary>
    /// <param name="executor">The executor.</param>
    /// <param name="document">The operation.</param>
    /// <returns>Its data as compact JSON, or its errors.</returns>
    public static async Task<string> RunAsync(this IRequestExecutor executor, string document)
    {
        var result = await executor.ExecuteAsync(document, TestContext.Current.CancellationToken);
        var json = JsonNode.Parse(result.ToJson())!.AsObject();
        return json["errors"] is { } errors ? errors.ToJsonString() : json["data"]!.ToJsonString();
    }
}

/// <summary>An account, holding value objects of each shape a filter meets.</summary>
public sealed class FilteredAccount
{
    public int Id { get; set; }

    public Iban Iban { get; set; }

    public Amount Balance { get; set; }

    public EmailAddress? Backup { get; set; }

    public List<Iban> Previous { get; set; } = [];
}

/// <summary>The accounts, mapped by the convention.</summary>
/// <param name="options">Options bound to the running container.</param>
public sealed class FilteredContext(DbContextOptions<FilteredContext> options) : DbContext(options)
{
    public DbSet<FilteredAccount> Accounts => Set<FilteredAccount>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ConfigureValueObjects(typeof(Iban).Assembly);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<FilteredAccount>(account =>
        {
            account.ToTable("filtered_accounts");
            account.Property(static entity => entity.Id).ValueGeneratedNever();
        });
}

/// <summary>The accounts, filtered and sorted by HotChocolate.Data.</summary>
public sealed class FilteredQuery
{
#pragma warning disable CA1822 // HotChocolate infers the fields of a query type from the instance members of its class.
    [UseFiltering]
    [UseSorting]
    public IQueryable<FilteredAccount> Accounts([Service] FilteredContext context) => context.Accounts;
#pragma warning restore CA1822
}
