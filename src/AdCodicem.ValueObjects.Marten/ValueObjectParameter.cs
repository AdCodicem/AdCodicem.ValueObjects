using Marten.Internal.CompiledQueries;
using Weasel.Postgresql;
using Weasel.Postgresql.SqlGeneration;

namespace AdCodicem.ValueObjects.Marten;

/// <summary>
/// The parameter a value object's underlying value is compared through, which refuses to be planned into a compiled query
/// taking the value object as a parameter.
/// </summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
/// <remarks>
/// <para>
/// Marten binds the parameters of a compiled query to the members of its query type through finders of its own, none of
/// which takes a value type: a value-object parameter keeps the value of the first call in the plan, and every later call
/// answers with it, for a type registered with Marten's <c>RegisterValueType</c> as for this package's.
/// </para>
/// <para>
/// Marten itself refuses a query type holding a value object the package taught it, which its list of value types never
/// holds; the refusal here covers a value object Marten listed itself, as the identity of a document. The package cannot
/// bind it either: Marten hands a comparison its value already read off the query, and tells its own parameters apart by
/// their values in the query it planned from, which a package never sees, and which a later call may bind before the
/// planning one does. The parameter is still a <see cref="CommandParameter"/>, so that a containment or a JSON path filter
/// reads its value as Marten's own.
/// </para>
/// </remarks>
internal sealed class ValueObjectParameter<TSelf, TValue> : CommandParameter, ISqlFragment
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>Initializes the parameter from the one Marten's member wrote, a <see cref="DateTimeOffset"/> in UTC.</summary>
    /// <param name="parameter">Marten's parameter.</param>
    public ValueObjectParameter(CommandParameter parameter)
        : base(MartenValueObject<TSelf, TValue>.Parameter((TValue)parameter.Value!))
        => DbType = parameter.DbType;

    /// <inheritdoc cref="CommandParameter.Apply(ICommandBuilder)" />
    /// <exception cref="NotSupportedException">A compiled query plans the parameter, and takes the value object as one.</exception>
    void ISqlFragment.Apply(ICommandBuilder builder)
    {
        if (builder is CompiledQueryPlan plan && CompiledQueryBinding.MemberOf(plan, static type => type == typeof(TSelf)) is { } member)
        {
            throw CompiledQueryBinding.TakesOne(plan, typeof(TSelf), member);
        }

        builder.AppendParameter(Value, DbType);
    }
}
