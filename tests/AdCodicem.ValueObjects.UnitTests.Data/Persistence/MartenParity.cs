using System.Linq.Expressions;
using Marten;
using Marten.Exceptions;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// What a value object becomes in Marten's LINQ under <c>UseValueObjects()</c>, beside a member of its underlying type
/// holding the same value, for the samples of <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class MartenParity
{
    /// <summary>
    /// Checks that a comparison of the value object, of its nullable type and a <c>Contains</c> over values of it, each
    /// translates as the same query over a member of the underlying type does: the same SQL, the member's name apart, and
    /// parameters of the same values and database types; or, over an underlying type Marten maps to no column, that both
    /// are refused the same way.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="small">An instance.</param>
    /// <param name="large">Another instance.</param>
    public static void Check<TSelf, TValue>(TSelf small, TSelf large)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var name = typeof(TSelf).Name;
        foreach (var instance in (TSelf[])[small, large])
        {
            foreach (var comparison in (ExpressionType[])[ExpressionType.Equal, ExpressionType.NotEqual, ExpressionType.GreaterThan, ExpressionType.LessThanOrEqual])
            {
                // Text and booleans have no ordering operator in C#, where the value object has one: no oracle there.
                if (Compare<TSelf, TValue>(nameof(MartenTwin<,>.Raw), comparison, instance.Value) is { } raw)
                {
                    Outcome(Compare<TSelf, TValue>(nameof(MartenTwin<,>.Item), comparison, instance)!)
                        .Should().Be(Outcome(raw), "{0} {1} translates as its underlying type", name, comparison);
                }
            }

            Outcome(Compare<TSelf, TValue>(nameof(MartenTwin<,>.Optional), ExpressionType.Equal, (TSelf?)instance)!)
                .Should().Be(Outcome(Compare<TSelf, TValue>(nameof(MartenTwin<,>.Raw), ExpressionType.Equal, instance.Value)!), "{0}? compares as its underlying type", name);
        }

        // Marten's own Contains hands Npgsql a DateTimeOffset as it is, which Npgsql refuses for a timestamp with time zone
        // at any offset but UTC; the package hands it in UTC, as Marten's own comparison of one does.
        List<TSelf> items = [small, large];
        List<TValue> raws = [.. items.Select(static item => item.Value is DateTimeOffset moment ? (TValue)(object)moment.ToUniversalTime() : item.Value)];
        Outcome<TSelf, TValue>(twins => twins.Where(twin => items.Contains(twin.Item)))
            .Should().Be(Outcome<TSelf, TValue>(twins => twins.Where(twin => raws.Contains(twin.Raw))), "a Contains over {0} translates as one over its underlying type", name);
    }

    /// <summary>
    /// Writes <c>twin =&gt; twin.Member op value</c> through the expression API, since generic code has no operator over
    /// the type arguments; a member and a value with no such operator give <see langword="null"/>.
    /// </summary>
    private static Func<IQueryable<MartenTwin<TSelf, TValue>>, IQueryable<MartenTwin<TSelf, TValue>>>? Compare<TSelf, TValue>(string member, ExpressionType comparison, object? value)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var twin = Expression.Parameter(typeof(MartenTwin<TSelf, TValue>), "twin");
        var property = Expression.Property(twin, member);
        BinaryExpression body;
        try
        {
            body = Expression.MakeBinary(comparison, property, Expression.Constant(value, property.Type));
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        var filter = Expression.Lambda<Func<MartenTwin<TSelf, TValue>, bool>>(body, twin);
        return twins => twins.Where(filter);
    }

    /// <summary>
    /// Gives the SQL of a query, with each member's name written as the underlying member's, and its parameters; or the
    /// type of the exception Marten refuses it with, whose message names the type it refuses, the value object or its
    /// underlying type.
    /// </summary>
    private static string Outcome<TSelf, TValue>(Func<IQueryable<MartenTwin<TSelf, TValue>>, IQueryable<MartenTwin<TSelf, TValue>>> query)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        try
        {
            return MartenHarness.Sql(query).Description
                .Replace($"'{nameof(MartenTwin<,>.Item)}'", $"'{nameof(MartenTwin<,>.Raw)}'", StringComparison.Ordinal)
                .Replace($"'{nameof(MartenTwin<,>.Optional)}'", $"'{nameof(MartenTwin<,>.Raw)}'", StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is BadLinqExpressionException or NotSupportedException)
        {
            return $"refused with a {exception.GetType().Name}";
        }
    }
}

/// <summary>A document holding a value object, its nullable type and a member of its underlying type.</summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
public sealed class MartenTwin<TSelf, TValue>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    public Guid Id { get; set; }

    public TSelf Item { get; set; }

    public TSelf? Optional { get; set; }

    public TValue Raw { get; set; } = default!;
}
