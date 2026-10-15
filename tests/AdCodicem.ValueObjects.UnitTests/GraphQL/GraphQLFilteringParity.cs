using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.HotChocolate;
using AdCodicem.ValueObjects.HotChocolate.Data;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// HotChocolate infers the fields of a query type from the instance members of its class.
#pragma warning disable CA1822

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>
/// What HotChocolate.Data's filters make of a value object under <c>AddValueObjectFiltering()</c>, beside what its own
/// equality and order make of the same instances, for the samples of <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class GraphQLFilteringParity
{
    /// <summary>
    /// Checks that the value object's filter input is named after its scalar and holds the comparable operations, or
    /// <c>eq</c> and <c>neq</c> over <see cref="bool"/>; that <c>eq</c>, <c>in</c> and <c>gt</c> of each instance, or its
    /// <c>eq</c> alone over <see cref="bool"/>, written as the scalar's literal, find in memory the rows the instances' own
    /// equality and order find, and on SQLite the same rows, or the refusal Entity Framework Core gives the same comparison
    /// of a column of the underlying type; and that SQLite runs <c>eq</c> as it runs that comparison. A value object over a 128-bit integer, which the convention
    /// leaves to a converter of the application's own, runs in memory alone.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="small">An instance.</param>
    /// <param name="large">A larger one.</param>
    /// <returns>The check.</returns>
    public static async Task CheckAsync<TSelf, TValue>(TSelf small, TSelf large)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        List<FilterHolder<TSelf, TValue>> rows =
        [
            new() { Id = 1, Item = small, Raw = small.Value },
            new() { Id = 2, Item = large, Raw = large.Value },
        ];
        var memory = await GraphQLHarness.BuildAsync(Filtering<TSelf, TValue>, services => services.AddSingleton(rows));
        var scalar = memory.Schema.Types.OfType<ValueObjectScalarType<TSelf, TValue>>().Should().ContainSingle().Subject.Name;
        var input = scalar + "OperationFilterInput";
        var comparable = typeof(TValue) != typeof(bool);
        Regex.Replace(memory.Schema.ToString(), @"\s+", " ").Should()
            .Contain($"item: {input}")
            .And.Contain(comparable
                ? $"input {input} {{ eq: {scalar} neq: {scalar} in: [{scalar}!] nin: [{scalar}!] gt: {scalar} ngt: {scalar} gte: {scalar} ngte: {scalar} lt: {scalar} nlt: {scalar} lte: {scalar} nlte: {scalar} }}"
                : $"input {input} {{ eq: {scalar} neq: {scalar} }}");

        var kind = GraphQLParity.KindOf(typeof(TValue));
        foreach (var instance in (TSelf[])[small, large])
        {
            var literal = GraphQLParity.Literal(kind, GraphQLParity.Text(instance.Value));
            foreach (var (operation, operand, selects) in Operations<TSelf, TValue>(instance, literal, comparable))
            {
                var expected = rows.Where(selects).Select(static row => row.Id);
                (await IdsAsync(memory, operation, operand)).Should().Equal(expected, "{0}: {1} {2} in memory", typeof(TSelf).Name, operation, operand);
            }
        }

        if (typeof(TValue) == typeof(Int128) || typeof(TValue) == typeof(UInt128))
        {
            return;
        }

        await CheckOnSqliteAsync(rows, literal: value => GraphQLParity.Literal(kind, GraphQLParity.Text(value.Value)), comparable);
    }

    /// <summary>Gets the operations checked for an instance, its operand, and which rows its own equality and order select.</summary>
    private static IEnumerable<(string Operation, string Operand, Func<FilterHolder<TSelf, TValue>, bool> Selects)> Operations<TSelf, TValue>(
        TSelf instance,
        string literal,
        bool comparable)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        yield return ("eq", literal, row => row.Item.Equals(instance));
        if (comparable)
        {
            yield return ("in", $"[{literal}]", row => ((TSelf[])[instance]).Contains(row.Item));
            yield return ("gt", literal, row => row.Item.CompareTo(instance) > 0);
        }
    }

    private static async Task CheckOnSqliteAsync<TSelf, TValue>(List<FilterHolder<TSelf, TValue>> rows, Func<TSelf, string> literal, bool comparable)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        List<string> sql = [];
        List<Exception?> failures = [];
        var options = new DbContextOptionsBuilder<FilterHolderContext<TSelf, TValue>>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .LogTo(sql.Add, [RelationalEventId.CommandExecuted], LogLevel.Information, DbContextLoggerOptions.None)
            .Options;
        using (var write = new FilterHolderContext<TSelf, TValue>(options))
        {
            write.Database.EnsureCreated();
            write.AddRange(rows);
            write.SaveChanges();
        }

        sql.Clear();
        var executor = await GraphQLHarness.BuildAsync(
            builder => Filtering<TSelf, TValue>(builder.AddErrorFilter(error =>
            {
                failures.Add(error.Exception);
                return error;
            })),
            services => services.AddSingleton(options).AddScoped<FilterHolderContext<TSelf, TValue>>());

        var name = typeof(TSelf).Name;
        foreach (var row in rows)
        {
            var instance = row.Item;
            foreach (var (operation, operand, selects) in Operations<TSelf, TValue>(instance, literal(instance), comparable))
            {
                failures.Clear();
                sql.Clear();
                var result = await executor.ExecuteAsync($"{{ rows(where: {{ item: {{ {operation}: {operand} }} }}) {{ id }} }}");
                List<string> ran = [.. sql];

                using var context = new FilterHolderContext<TSelf, TValue>(options);
                sql.Clear();
                var raw = RawOutcome(context, operation, row.Raw);
                failures.Should().HaveCount(
                    operation == "gt" && (typeof(TValue) == typeof(ulong) || typeof(TValue) == typeof(TimeSpan) || typeof(TValue) == typeof(DateTimeOffset)) ? 1 : 0,
                    "{0}: SQLite translates no {1} of a ulong, a TimeSpan or a DateTimeOffset, and every other",
                    name,
                    operation);
                if (failures.Count == 0)
                {
                    Ids(result).Should().Equal(rows.Where(selects).Select(static holder => holder.Id), "{0}: {1} {2} on SQLite", name, operation, operand);
                    raw.Should().BeNull("{0}: SQLite runs {1} over the underlying type too", name, operation);
                    if (operation == "eq")
                    {
                        Normalized(Statement(ran)).Should().Be(Normalized(Statement(sql)), "{0} runs eq as its underlying type does", name);
                    }
                }
                else
                {
                    result["errors"]!.AsArray().Should().ContainSingle().Which!["message"]!.GetValue<string>().Should().Be("Unexpected Execution Error");
                    failures.Should().ContainSingle().Which.Should().NotBeNull();
                    raw.Should().Be(failures[0]!.GetType(), "{0}: SQLite refuses {1} over the underlying type alike", name, operation);
                }
            }
        }
    }

    /// <summary>
    /// Runs the comparison over the column of the underlying type, and gives the type of the exception it fails with, or
    /// <see langword="null"/> when it runs.
    /// </summary>
    private static Type? RawOutcome<TSelf, TValue>(FilterHolderContext<TSelf, TValue> context, string operation, TValue value)
        where TSelf : struct
    {
        var holder = Expression.Parameter(typeof(FilterHolder<TSelf, TValue>), "holder");
        var raw = Expression.Property(holder, nameof(FilterHolder<,>.Raw));
        var compared = Expression.Field(Expression.Constant(new StrongBox<TValue>(value)), nameof(StrongBox<>.Value));
        Expression test = operation switch
        {
            "eq" => Expression.Equal(raw, compared),
            "in" => Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Contains),
                [typeof(TValue)],
                Expression.Field(Expression.Constant(new StrongBox<TValue[]>([value])), nameof(StrongBox<>.Value)),
                raw),
            _ when typeof(TValue) == typeof(string) => Expression.GreaterThan(
                Expression.Call(typeof(string), nameof(string.Compare), null, raw, compared),
                Expression.Constant(0)),
            _ => Expression.GreaterThan(raw, compared),
        };

        try
        {
            _ = context.Holders.Where(Expression.Lambda<Func<FilterHolder<TSelf, TValue>, bool>>(test, holder)).ToList();
            return null;
        }
        catch (InvalidOperationException refusal)
        {
            return refusal.GetType();
        }
    }

    private static void Filtering<TSelf, TValue>(IRequestExecutorBuilder builder)
        where TSelf : struct
        => builder.AddQueryType<FilterHolderQuery<TSelf, TValue>>().AddFiltering().AddValueObjectFiltering();

    private static async Task<List<int>> IdsAsync(IRequestExecutor executor, string operation, string operand)
        => Ids(await executor.ExecuteAsync($"{{ rows(where: {{ item: {{ {operation}: {operand} }} }}) {{ id }} }}"));

    private static List<int> Ids(JsonObject result)
        => [.. JsonNode.Parse(result.Data())!["rows"]!.AsArray().Select(static row => row!["id"]!.GetValue<int>())];

    /// <summary>Gets the statement of the one command the log holds, without its parameters' header.</summary>
    private static string Statement(List<string> sql)
    {
        var command = sql.Should().ContainSingle().Subject;
        return command[(command.IndexOf('\n', StringComparison.Ordinal) + 1)..];
    }

    /// <summary>Gets a statement with the column compared named alike and every parameter named alike.</summary>
    private static string Normalized(string statement)
        => Regex.Replace(Regex.Replace(statement, @"@\w+", "@p"), @"\s+", " ").Replace("\"Item\"", "\"Raw\"", StringComparison.Ordinal);
}

/// <summary>A row holding a value object, and its underlying value in a column of its own, which GraphQL never sees.</summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
public sealed class FilterHolder<TSelf, TValue>
    where TSelf : struct
{
    public int Id { get; set; }

    public TSelf Item { get; set; }

    [GraphQLIgnore]
    public TValue Raw { get; set; } = default!;
}

/// <summary>The rows holding a value object, read from SQLite when the application registers a context, and else from memory.</summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
public class FilterHolderQuery<TSelf, TValue>
    where TSelf : struct
{
    [UseFiltering]
    public IQueryable<FilterHolder<TSelf, TValue>> Rows(IResolverContext context)
        => context.Services.GetService<FilterHolderContext<TSelf, TValue>>()?.Holders
            ?? context.Services.GetRequiredService<List<FilterHolder<TSelf, TValue>>>().AsQueryable();
}

/// <summary>A model of its own for each value object, on SQLite.</summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
/// <param name="options">The options of the test's database.</param>
public sealed class FilterHolderContext<TSelf, TValue>(DbContextOptions<FilterHolderContext<TSelf, TValue>> options) : DbContext(options)
    where TSelf : struct
{
    public DbSet<FilterHolder<TSelf, TValue>> Holders => Set<FilterHolder<TSelf, TValue>>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ConfigureValueObjects(typeof(Iban).Assembly);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<FilterHolder<TSelf, TValue>>().Property(static holder => holder.Id).ValueGeneratedNever();
}
