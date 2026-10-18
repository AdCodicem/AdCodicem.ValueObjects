using System.Collections;
using System.Linq.Expressions;
using Marten;
using Marten.Exceptions;
using Marten.Linq.Members;
using Marten.Linq.Parsing;
using Weasel.Postgresql.SqlGeneration;

namespace AdCodicem.ValueObjects.Marten;

/// <summary>
/// Reads <c>values.Contains(x.Member)</c> and <c>x.Member.IsOneOf(values)</c> over a value-object member as
/// <c>= ANY(:p0)</c>, the values unwrapped to the underlying ones.
/// </summary>
/// <param name="members">The store's member source, which tells the value objects it knows.</param>
/// <remarks>
/// <para>
/// Marten's own <c>Contains</c> hands the value objects to Npgsql as they are, which fails at execution, and its
/// <c>IsOneOf</c> unwraps them by reflection, which a native image lacks. Marten asks the parsers of its options first.
/// </para>
/// <para>
/// Marten keeps the parser it found for a method for every later call of that method, whatever its arguments:
/// <c>List&lt;CountryCode&gt;.Contains</c> is the same method in <c>codes.Contains(x.Country)</c> and in
/// <c>x.Countries.Contains(code)</c>. The second is handed back to the collection member, as Marten's own parser does.
/// </para>
/// </remarks>
internal sealed class ValueObjectContainsParser(ValueObjectMembers members) : IMethodCallParser
{
    /// <inheritdoc />
    public bool Matches(MethodCallExpression expression)
    {
        var shape = Shape.Of(expression);
        return shape is not null && members.Knows(Nullable.GetUnderlyingType(shape.Item.Type) ?? shape.Item.Type);
    }

    /// <inheritdoc />
    public ISqlFragment Parse(IQueryableMemberCollection memberCollection, IReadOnlyStoreOptions options, MethodCallExpression expression)
    {
        var shape = Shape.Of(expression)!;
        if (ParameterFinder.Finds(shape.Values))
        {
            // x.Countries.Contains(code): the collection member answers, or Marten's refusal is thrown, as under its own parser.
            return memberCollection.MemberFor(shape.Values) is ICollectionMember collection
                ? collection.ParseWhereForContains(expression, options)
                : throw new BadLinqExpressionException($"Marten is not (yet) able to parse '{expression}' as part of a Contains() query for this member");
        }

        // The member may be the package's, a duplicated field or an identity: each locates the underlying value.
        var member = memberCollection.MemberFor(shape.Item);
        var values = shape.Values.ReduceToConstant().Value as IEnumerable ?? Array.Empty<object>();
        _ = members.TryFind(Nullable.GetUnderlyingType(shape.Item.Type) ?? shape.Item.Type, out var valueObject);

        return valueObject!.In(member, values, ValuesSource.Of(shape.Values));
    }

    /// <summary>The two sides of a <c>Contains</c> or an <c>IsOneOf</c>.</summary>
    /// <param name="Item">The member tested.</param>
    /// <param name="Values">The values it is tested against.</param>
    private sealed record Shape(Expression Item, Expression Values)
    {
        public static Shape? Of(MethodCallExpression expression)
        {
            if (expression.Method is { Name: "IsOneOf" or "In" } method && method.DeclaringType == typeof(LinqExtensions))
            {
                return new Shape(expression.Arguments[0], expression.Arguments[1]);
            }

            if (expression.Method.Name != nameof(Enumerable.Contains))
            {
                return null;
            }

            var values = expression.Object ?? (expression.Arguments.Count == 2 ? expression.Arguments[0] : null);

            // C# 14 binds array.Contains(x) to MemoryExtensions.Contains over the span the array converts to.
            return values switch
            {
                null => null,
                MethodCallExpression { Method.Name: "op_Implicit", Arguments: [var array] } => new Shape(expression.Arguments[^1], array),
                _ => new Shape(expression.Arguments[^1], values),
            };
        }
    }

    /// <summary>Tells whether an expression reads the document, rather than a value captured from outside the query.</summary>
    private sealed class ParameterFinder : ExpressionVisitor
    {
        private bool _found;

        public static bool Finds(Expression expression)
        {
            var finder = new ParameterFinder();
            finder.Visit(expression);
            return finder._found;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            _found = true;
            return node;
        }
    }
}
