using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// What <c>.Value</c> of a value object becomes in an Entity Framework Core query under <c>UseValueObjectQueries()</c>,
/// on SQLite in memory and in Npgsql's query text, for the samples of <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class ValueQueryParity
{
    /// <summary>
    /// Checks that a query comparing <c>.Value</c> of the value object with a value translates as the same query over a
    /// property of the underlying type does, holding that same value: the same SQL, the column's name apart, on SQLite and
    /// on PostgreSQL, and the same rows on SQLite, or the same refusal where the provider translates no such comparison. A
    /// value object over a 128-bit integer, which the convention leaves to a converter of the application's own, is left
    /// out.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="small">An instance.</param>
    /// <param name="large">Another instance.</param>
    public static void Check<TSelf, TValue>(TSelf small, TSelf large)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        if (typeof(TValue) == typeof(Int128) || typeof(TValue) == typeof(UInt128))
        {
            return;
        }

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var name = typeof(TSelf).Name;

        using (var write = new ValueContext<TSelf, TValue>(connection))
        {
            write.Database.EnsureCreated();
            write.Add(new ValueHolder<TSelf, TValue> { Id = 1, Item = small, Raw = small.Value });
            write.Add(new ValueHolder<TSelf, TValue> { Id = 2, Item = large, Raw = large.Value });
            write.SaveChanges();
        }

        using var read = new ValueContext<TSelf, TValue>(connection);
        using var postgreSql = new PostgreSqlValueContext<TSelf, TValue>();
        foreach (var instance in (TSelf[])[small, large])
        {
            var compared = new StrongBox<TValue>(instance.Value);

            Outcome(read, Filter<TSelf, TValue>(compared, onValue: true), execute: true)
                .Should().Be(Outcome(read, Filter<TSelf, TValue>(compared, onValue: false), execute: true), "{0} compares its value on SQLite as its underlying type does", name);
            Outcome(postgreSql, Filter<TSelf, TValue>(compared, onValue: true), execute: false)
                .Should().Be(Outcome(postgreSql, Filter<TSelf, TValue>(compared, onValue: false), execute: false), "{0} compares its value on PostgreSQL as its underlying type does", name);
        }
    }

    /// <summary>
    /// Writes <c>holder =&gt; holder.Item.Value == compared.Value</c>, or the same over <c>holder.Raw</c>, through the
    /// expression API, since generic code has no <c>==</c> over <typeparamref name="TValue"/>; the value is read off a
    /// box, so that the query takes it as a parameter.
    /// </summary>
    private static Expression<Func<ValueHolder<TSelf, TValue>, bool>> Filter<TSelf, TValue>(StrongBox<TValue> compared, bool onValue)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var holder = Expression.Parameter(typeof(ValueHolder<TSelf, TValue>), "holder");
        Expression read = onValue
            ? Expression.Property(Expression.Property(holder, nameof(ValueHolder<,>.Item)), nameof(IValueObject<,>.Value))
            : Expression.Property(holder, nameof(ValueHolder<,>.Raw));

        return Expression.Lambda<Func<ValueHolder<TSelf, TValue>, bool>>(
            Expression.Equal(read, Expression.Field(Expression.Constant(compared), nameof(StrongBox<>.Value))),
            holder);
    }

    /// <summary>
    /// Gives the SQL a filter translates to, with the column's name made the same for both properties, and, when it is
    /// run, the identifiers of the rows it finds, or the type of the exception Entity Framework Core refuses it with.
    /// </summary>
    private static string Outcome<TSelf, TValue>(DbContext context, Expression<Func<ValueHolder<TSelf, TValue>, bool>> filter, bool execute)
        where TSelf : struct
    {
        var query = context.Set<ValueHolder<TSelf, TValue>>().Where(filter).Select(static holder => holder.Id);
        try
        {
            var sql = query.ToQueryString().Replace("\"Item\"", "\"Raw\"", StringComparison.Ordinal);

            return execute ? $"{sql}{Environment.NewLine}rows: {string.Join(", ", query.ToList())}" : sql;
        }
        catch (InvalidOperationException refusal)
        {
            return refusal.GetType().Name;
        }
    }

    /// <summary>An entity holding a value object, and its underlying value in a property of its own.</summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    private sealed class ValueHolder<TSelf, TValue>
        where TSelf : struct
    {
        public int Id { get; set; }

        public TSelf Item { get; set; }

        public TValue Raw { get; set; } = default!;
    }

    /// <summary>A model of its own for each value object, on SQLite, translating the value of value objects.</summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="connection">The database.</param>
    private sealed class ValueContext<TSelf, TValue>(SqliteConnection connection) : DbContext
        where TSelf : struct
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite(connection).UseValueObjectQueries();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(typeof(Iban).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ValueHolder<TSelf, TValue>>().Property(static holder => holder.Id).ValueGeneratedNever();
    }

    /// <summary>A model of its own for each value object, on PostgreSQL, translating the value of value objects.</summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    private sealed class PostgreSqlValueContext<TSelf, TValue> : DbContext
        where TSelf : struct
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused").UseValueObjectQueries();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(typeof(Iban).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ValueHolder<TSelf, TValue>>().Property(static holder => holder.Id).ValueGeneratedNever();
    }
}
