using System.Globalization;
using System.Text.Json.Nodes;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.HotChocolate;
using AdCodicem.ValueObjects.HotChocolate.Data;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Execution;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>An account HotChocolate.Data filters, holding value objects.</summary>
public sealed class FilteredAccount
{
    /// <summary>Gets or sets the key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the IBAN.</summary>
    public Iban Iban { get; set; }

    /// <summary>Gets or sets a quantity.</summary>
    public Quantity Qty { get; set; }

    /// <summary>Gets or sets the IBANs the account held.</summary>
    public List<Iban> Previous { get; set; } = [];
}

/// <summary>The accounts, mapped by the convention, their values translated in queries.</summary>
/// <param name="options">The options of the database.</param>
public sealed class FilteredContext(DbContextOptions<FilteredContext> options) : DbContext(options)
{
    /// <summary>Gets the accounts.</summary>
    public DbSet<FilteredAccount> Accounts => Set<FilteredAccount>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ConfigureValueObjects(typeof(Iban).Assembly);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<FilteredAccount>().Property(static account => account.Id).ValueGeneratedNever();
}

/// <summary>The accounts, filtered and sorted by HotChocolate.Data.</summary>
public sealed class FilteredQuery
{
    /// <summary>Gets the accounts.</summary>
    [UseFiltering]
    [UseSorting]
#pragma warning disable CA1822 // HotChocolate infers the fields of a query type from the instance members of its class.
    public IQueryable<FilteredAccount> Accounts([Service] FilteredContext context) => context.Accounts;
#pragma warning restore CA1822
}

/// <summary>
/// HotChocolate.Data's filters over value objects on the next major: the package's HotChocolate.Data floor in a schema
/// built in process over Entity Framework Core 11 on SQLite, an operand read through the rules and translated to its
/// column, a refused one answered with its code, quoted by its scalar's name in the language of the execution.
/// </summary>
public sealed class GraphQLFilteringTests : IDisposable
{
    private const string French = "FR7630006000011234567890189";
    private const string German = "DE89370400440532013000";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public GraphQLFilteringTests()
    {
        _connection.Open();
        using var context = new FilteredContext(new DbContextOptionsBuilder<FilteredContext>().UseSqlite(_connection).Options);
        context.Database.EnsureCreated();
        context.Accounts.AddRange(
            new FilteredAccount { Id = 1, Iban = Iban.Create(French), Qty = Quantity.Create(3), Previous = [Iban.Create(German)] },
            new FilteredAccount { Id = 2, Iban = Iban.Create(German), Qty = Quantity.Create(9), Previous = [] });
        context.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task A_value_object_is_filtered_through_its_scalar_and_translated_by_Entity_Framework_Core()
    {
        var executor = await BuildAsync();

        Data(await ExecuteAsync(executor, """{ accounts(where: { iban: { eq: "fr76 3000 6000 0112 3456 7890 189" } }) { id } }"""))
            .Should().Be("""{"accounts":[{"id":1}]}""");
        Data(await ExecuteAsync(executor, $$"""{ accounts(where: { iban: { in: ["{{German}}", "{{French}}"] } }) { id } }"""))
            .Should().Be("""{"accounts":[{"id":1},{"id":2}]}""");
        Data(await ExecuteAsync(executor, "{ accounts(where: { qty: { gt: 5 } }) { id } }")).Should().Be("""{"accounts":[{"id":2}]}""");
        Data(await ExecuteAsync(executor, """{ accounts(where: { previous: { some: { eq: "de89 3704 0044 0532 0130 00" } } }) { id } }"""))
            .Should().Be("""{"accounts":[{"id":1}]}""");
        Data(await ExecuteAsync(executor, """{ accounts(where: { iban: { startsWith: "FR" } }) { id } }""")).Should().Be("""{"accounts":[{"id":1}]}""");
        Data(await ExecuteAsync(executor, "{ accounts(order: { iban: ASC }) { id } }")).Should().Be("""{"accounts":[{"id":2},{"id":1}]}""");

        var refused = Error(await ExecuteAsync(executor, """{ accounts(where: { iban: { eq: "XX" } }) { id } }"""));
        refused["extensions"]!["code"]!.GetValue<string>().Should().Be(ValueObjectErrorCodes.TooShort);
        refused["extensions"]!["inputPath"]!.ToJsonString().Should().Be("""["eq"]""");
    }

    [Fact]
    public async Task A_refused_operand_is_quoted_by_its_scalars_name_in_the_language_of_the_execution()
    {
        var executor = await BuildAsync();
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            Error(await ExecuteAsync(executor, """{ accounts(where: { iban: { eq: "XX" } }) { id } }"""))["message"]!.GetValue<string>()
                .Should().Be("« Iban » : au moins 15 caractères.");
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture);
        }
    }

    private async Task<IRequestExecutor> BuildAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        services.AddDbContext<FilteredContext>(options => options.UseSqlite(_connection).UseValueObjectQueries());
        services.AddGraphQL()
            .AddQueryType<FilteredQuery>()
            .AddSorting()
            .AddFiltering()
            .AddValueObjectFiltering(static options => options.IncludeTextOperations())
            .AddValueObjects(static options => options.UseResource<Messages>());
        return await services.BuildServiceProvider().GetRequestExecutorAsync(cancellationToken: TestContext.Current.CancellationToken);
    }

    private static async Task<JsonObject> ExecuteAsync(IRequestExecutor executor, string document)
    {
        var result = await executor.ExecuteAsync(OperationRequestBuilder.New().SetDocument(document).Build(), TestContext.Current.CancellationToken);
        return JsonNode.Parse(result.ToJson())!.AsObject();
    }

    private static string Data(JsonObject result)
    {
        result.ContainsKey("errors").Should().BeFalse("the operation succeeds: {0}", result.ToJsonString());
        return result["data"]!.ToJsonString();
    }

    private static JsonObject Error(JsonObject result) => result["errors"]!.AsArray().Should().ContainSingle().Subject!.AsObject();
}
