using System.Linq.Expressions;
using System.Reflection;

namespace AdCodicem.ValueObjects.Marten;

/// <summary>Where the values of a <c>Contains</c> or an <c>IsOneOf</c> are read from: a chain of members off an object.</summary>
/// <param name="RootType">The type of the object the chain starts from: the query type in a compiled query's plan.</param>
/// <param name="Chain">The fields and properties read, in order.</param>
internal sealed record ValuesSource(Type RootType, MemberInfo[] Chain)
{
    /// <summary>Reads the members an expression reads off a captured object, when it does nothing else.</summary>
    /// <param name="expression">The expression of the values, as Marten hands it to the parser, before reducing it.</param>
    /// <returns>The source, or <see langword="null"/> for any other expression.</returns>
    public static ValuesSource? Of(Expression expression)
    {
        var chain = new List<MemberInfo>();
        var current = expression;
        while (current is MemberExpression { Member: PropertyInfo or FieldInfo } member)
        {
            chain.Insert(0, member.Member);
            current = member.Expression;
        }

        return current is ConstantExpression { Value: { } root } && chain.Count > 0 ? new ValuesSource(root.GetType(), [.. chain]) : null;
    }

    /// <summary>Tells whether the last member's type holds nullable values of a value object rather than values of it.</summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <returns><see langword="true"/> for an enumerable of <c>TSelf?</c>.</returns>
    public bool HoldsNullable<TSelf>()
        where TSelf : struct
    {
        var type = Chain[^1] is PropertyInfo property ? property.PropertyType : ((FieldInfo)Chain[^1]).FieldType;
        return !typeof(IEnumerable<TSelf>).IsAssignableFrom(type) && typeof(IEnumerable<TSelf?>).IsAssignableFrom(type);
    }

    /// <summary>Reads the values off an object of <see cref="RootType"/>.</summary>
    /// <param name="root">The object.</param>
    /// <returns>The values, or <see langword="null"/> where a member on the way holds none.</returns>
    public object? Read(object root)
    {
        object? current = root;
        foreach (var member in Chain)
        {
            if (current is null)
            {
                return null;
            }

            current = member is PropertyInfo property ? property.GetValue(current) : ((FieldInfo)member).GetValue(current);
        }

        return current;
    }
}
