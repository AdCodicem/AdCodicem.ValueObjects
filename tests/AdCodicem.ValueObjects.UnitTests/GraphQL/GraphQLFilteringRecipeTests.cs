using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.HotChocolate;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Data.Filters;
using HotChocolate.Execution;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>
/// The guide's recipe for HotChocolate.Data, over Entity Framework Core on SQLite in memory: without it, a filter names
/// the struct's <c>value</c> and compares raw text; bound to <c>ComparableOperationFilterInputType&lt;TSelf&gt;</c>, a
/// value object's operands are its scalar, read through its rules, and translate to SQL. Sorting needs no binding.
/// </summary>
public sealed class GraphQLFilteringRecipeTests : IDisposable
{
    private const string French = "FR7630006000011234567890189";
    private const string German = "DE89370400440532013000";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public GraphQLFilteringRecipeTests()
    {
        _connection.Open();
        using var context = new RecipeContext(new DbContextOptionsBuilder<RecipeContext>().UseSqlite(_connection).Options);
        context.Database.EnsureCreated();
        context.Rows.AddRange(
            new RecipeRow { Id = 1, Iban = Iban.Create(French), Qty = Quantity.Create(3) },
            new RecipeRow { Id = 2, Iban = Iban.Create(German), Qty = Quantity.Create(9) });
        context.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// Without the binding, HotChocolate.Data infers a filter from the struct, whose <c>value</c> Entity Framework Core
    /// translates only under <c>UseValueObjectQueries()</c>, and compares raw text the normalizer never saw.
    /// </summary>
    [Fact]
    public async Task Without_the_binding_a_filter_names_the_value_and_compares_raw_text()
    {
        var untranslated = await BuildAsync(bound: false, translated: false);
        Sdl(untranslated).Should().Contain("input IbanFilterInput { and: [IbanFilterInput!] or: [IbanFilterInput!] countryCode: StringOperationFilterInput value: StringOperationFilterInput }");
        (await untranslated.ExecuteAsync($$"""{ rows(where: { iban: { value: { eq: "{{French}}" } } }) { id } }"""))
            .Error().Message().Should().Be("Unexpected Execution Error");

        var translated = await BuildAsync(bound: false, translated: true);
        (await translated.ExecuteAsync($$"""{ rows(where: { iban: { value: { eq: "{{French}}" } } }) { id } }"""))
            .Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await translated.ExecuteAsync("""{ rows(where: { iban: { value: { eq: "fr76 3000 6000 0112 3456 7890 189" } } }) { id } }"""))
            .Data().Should().Be("""{"rows":[]}""");
    }

    /// <summary>
    /// Bound, a value object's operands are its scalar: normalized, refused with the rule's code, and translated to SQL.
    /// </summary>
    [Fact]
    public async Task Bound_a_value_objects_operands_are_its_scalar_read_through_its_rules()
    {
        var executor = await BuildAsync(bound: true, translated: false);

        Sdl(executor).Should().Contain("input ComparableIbanOperationFilterInput { eq: Iban neq: Iban in: [Iban!] nin: [Iban!]");
        (await executor.ExecuteAsync("""{ rows(where: { iban: { eq: "fr76 3000 6000 0112 3456 7890 189" } }) { id iban } }"""))
            .Data().Should().Be($$"""{"rows":[{"id":1,"iban":"{{French}}"}]}""");
        (await executor.ExecuteAsync($$"""{ rows(where: { iban: { in: ["{{German}}", "fr76 3000 6000 0112 3456 7890 189"] } }) { id } }"""))
            .Data().Should().Be("""{"rows":[{"id":1},{"id":2}]}""");
        (await executor.ExecuteAsync("{ rows(where: { qty: { gt: 5 } }) { id qty } }")).Data().Should().Be("""{"rows":[{"id":2,"qty":9}]}""");

        var error = (await executor.ExecuteAsync("""{ rows(where: { iban: { eq: "XX" } }) { id } }""")).Error();
        error.Code().Should().Be(ValueObjectErrorCodes.TooShort);
        error.Member("inputPath").Should().Be("""["eq"]""");
    }

    /// <summary>Sorting orders by the column, bound or not.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sorting_orders_by_the_column_bound_or_not(bool bound)
    {
        var executor = await BuildAsync(bound, translated: false);

        (await executor.ExecuteAsync("{ rows(order: { qty: DESC }) { qty } }")).Data().Should().Be("""{"rows":[{"qty":9},{"qty":3}]}""");
        (await executor.ExecuteAsync("{ rows(order: { iban: ASC }) { id } }")).Data().Should().Be("""{"rows":[{"id":2},{"id":1}]}""");
    }

    private static string Sdl(IRequestExecutor executor) => Regex.Replace(executor.Schema.ToString(), @"\s+", " ");

    private Task<IRequestExecutor> BuildAsync(bool bound, bool translated)
        => GraphQLHarness.BuildAsync(
            builder =>
            {
                builder.AddQueryType<RecipeQuery>().AddSorting().AddValueObjects();
                if (bound)
                {
                    builder.AddFiltering(static conventions => conventions.AddDefaults()
                        .BindRuntimeType<Iban, ComparableOperationFilterInputType<Iban>>()
                        .BindRuntimeType<Quantity, ComparableOperationFilterInputType<Quantity>>());
                }
                else
                {
                    builder.AddFiltering();
                }
            },
            services => services.AddDbContext<RecipeContext>(options =>
            {
                options.UseSqlite(_connection);
                if (translated)
                {
                    options.UseValueObjectQueries();
                }
            }));

    /// <summary>A row whose columns are value objects.</summary>
    public sealed class RecipeRow
    {
        public int Id { get; set; }

        public Iban Iban { get; set; }

        public Quantity Qty { get; set; }
    }

    /// <summary>The rows, mapped by the Entity Framework Core package's conventions.</summary>
    public sealed class RecipeContext(DbContextOptions<RecipeContext> options) : DbContext(options)
    {
        public DbSet<RecipeRow> Rows => Set<RecipeRow>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects();
    }

    /// <summary>The rows, filtered and sorted by HotChocolate.Data.</summary>
    public class RecipeQuery
    {
        [UseFiltering]
        [UseSorting]
#pragma warning disable CA1822 // HotChocolate infers the fields of a query type from the instance members of its class.
        public IQueryable<RecipeRow> Rows([Service] RecipeContext context) => context.Rows;
#pragma warning restore CA1822
    }
}
