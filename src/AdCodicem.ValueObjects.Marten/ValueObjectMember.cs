using System.Linq.Expressions;
using System.Reflection;
using JasperFx.Core.Reflection;
using Marten.Exceptions;
using Marten.Linq.Members;
using Marten.Linq.SqlGeneration.Filters;
using Weasel.Core;
using Weasel.Postgresql.SqlGeneration;

namespace AdCodicem.ValueObjects.Marten;

/// <summary>
/// Marten's member for a value object over anything but text: <c>CAST(d.data -&gt;&gt; 'Quantity' as integer)</c>, or, over
/// a date or a time, the immutable function Marten's own member of that type reads it through.
/// </summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
/// <remarks>
/// The member reports <c>TSelf?</c> as its <see cref="QueryableMember.MemberType"/>, the one value type of the value object
/// Marten's list holds (<see cref="MartenValueObject{TSelf, TValue}.AddTo"/>), which Marten's duplicated field looks up by
/// that type.
/// </remarks>
internal sealed class ValueObjectMember<TSelf, TValue> : ValueTypeMember<TSelf, TValue>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>The type Marten's member reports, which Marten's refusal of a member it does not read names.</summary>
    private readonly Type _declared;

    /// <summary>Initializes the member.</summary>
    /// <param name="parent">The member holding it.</param>
    /// <param name="casing">The serializer's casing.</param>
    /// <param name="member">The member.</param>
    /// <param name="valueType">The value type.</param>
    /// <param name="schema">The store's schema, which holds Marten's immutable functions.</param>
    public ValueObjectMember(IQueryableMember parent, Casing casing, MemberInfo member, ValueTypeInfo valueType, string schema)
        : base(parent, casing, member, valueType)
    {
        _declared = MemberType;
        MemberType = typeof(TSelf?);

        // As Marten's own members of these types write it: PostgreSQL's cast from text to a date or a time depends on the
        // session's settings, so a computed index refuses it, and takes Marten's immutable function, which a query must
        // then read the value through for the index to serve it.
        if (ImmutableFunction is { } function)
        {
            TypedLocator = $"{schema}.{function}({RawLocator})";
        }
    }

    /// <summary>Gets the function of Marten's schema that reads the underlying type immutably, if it is a date or a time.</summary>
    private static string? ImmutableFunction
        => typeof(TValue) == typeof(DateOnly) ? "mt_immutable_date"
            : typeof(TValue) == typeof(TimeOnly) ? "mt_immutable_time"
            : typeof(TValue) == typeof(DateTime) ? "mt_immutable_timestamp"
            : typeof(TValue) == typeof(DateTimeOffset) ? "mt_immutable_timestamptz"
            : null;

    /// <inheritdoc />
    /// <remarks>
    /// C# compares the <c>.Value</c> of a value object over a <c>short</c>, a <c>byte</c>, an <c>sbyte</c> or a <c>char</c>
    /// as an <c>int</c>, and hands an <c>int</c> constant, which Marten's member would cast to the value object: it is
    /// compared as it stands, as Marten compares the same member of the underlying type.
    /// </remarks>
    public override ISqlFragment CreateComparison(string op, ConstantExpression constant)
        => constant.Value is null or TSelf or TValue
            ? MartenValueObject<TSelf, TValue>.Guard(this, base.CreateComparison(op, constant))
            : new MemberComparisonFilter(this, new CommandParameter(constant.Value), op);

    /// <inheritdoc />
    /// <remarks>
    /// A filter of a child collection (<c>x.Lines.Any(l =&gt; l.Score.Value == 5)</c>) places the same promoted <c>int</c>,
    /// which Marten's member drops, so that the filter would match every document: it is placed as the underlying value it
    /// stands for, which matches the stored JSON, a <c>char</c> included, or as it stands where no value of the underlying
    /// type equals it.
    /// </remarks>
    public override void PlaceValueInDictionaryForContainment(Dictionary<string, object> dict, ConstantExpression constant)
    {
        if (constant.Value is TSelf or TValue)
        {
            base.PlaceValueInDictionaryForContainment(dict, constant);
        }
        else
        {
            dict[MemberName] = constant.Value is int number && typeof(TValue) == typeof(char) && number is >= char.MinValue and <= char.MaxValue
                ? (char)number
                : constant.Value!;
        }
    }

    /// <inheritdoc />
    /// <remarks>Marten's refusal of a member it does not read names the type its member reported, not <c>TSelf?</c>.</remarks>
    public override IQueryableMember FindMember(MemberInfo member)
    {
        try
        {
            return base.FindMember(member);
        }
        catch (BadLinqExpressionException)
        {
            throw MartenValueObject.NotRead(_declared, member);
        }
    }
}
