using System.Linq.Expressions;
using System.Reflection;
using JasperFx.Core.Reflection;
using Marten.Exceptions;
using Marten.Linq.Members;
using Weasel.Core;
using Weasel.Postgresql.SqlGeneration;

namespace AdCodicem.ValueObjects.Marten;

/// <summary>
/// Marten's member for a value object over text, <c>d.data -&gt;&gt; 'Iban'</c>, with Marten's string methods, which also
/// takes the text itself on the other side of a comparison and in a containment document.
/// </summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <remarks>
/// The member reports <c>TSelf?</c> as its <see cref="QueryableMember.MemberType"/>, the one value type of the value object
/// Marten's list holds (<see cref="MartenValueObject{TSelf, TValue}.AddTo"/>), which Marten's duplicated field looks up by
/// that type.
/// </remarks>
internal sealed class ValueObjectStringMember<TSelf> : StringValueTypeMember<TSelf>
    where TSelf : struct
{
    private readonly Func<string, TSelf> _wrap;
    private readonly Func<IQueryableMember, ISqlFragment, ISqlFragment> _guard;

    /// <summary>The type Marten's member reports, which Marten's refusal of a member it does not read names.</summary>
    private readonly Type _declared;

    /// <summary>Initializes the member.</summary>
    /// <param name="parent">The member holding it.</param>
    /// <param name="casing">The serializer's casing.</param>
    /// <param name="member">The member.</param>
    /// <param name="valueType">The value type.</param>
    /// <param name="wrap">Builds the value object over text with no rule applied.</param>
    /// <param name="guard">Puts the package's parameter in place of Marten's.</param>
    public ValueObjectStringMember(
        IQueryableMember parent,
        Casing casing,
        MemberInfo member,
        ValueTypeInfo valueType,
        Func<string, TSelf> wrap,
        Func<IQueryableMember, ISqlFragment, ISqlFragment> guard)
        : base(parent, casing, member, valueType)
    {
        _wrap = wrap;
        _guard = guard;
        _declared = MemberType;
        MemberType = typeof(TSelf?);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>x.Code.Value == "ABC"</c> hands the text, which Marten's member would cast to the value object: it is compared as
    /// it stands, with no rule applied, as <c>.Value</c> reads the stored text.
    /// </remarks>
    public override ISqlFragment CreateComparison(string op, ConstantExpression constant)
        => _guard(this, base.CreateComparison(op, constant.Value is string text ? Expression.Constant(_wrap(text)) : constant));

    /// <inheritdoc />
    /// <remarks>
    /// Marten hands a filter of a child collection the text, which its member drops, and the filter then matches every
    /// document: the constant is placed as it is, which the store's serializer writes as it writes the member.
    /// </remarks>
    public override void PlaceValueInDictionaryForContainment(Dictionary<string, object> dict, ConstantExpression constant)
        => dict[MemberName] = constant.Value!;

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
