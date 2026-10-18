using AdCodicem.ValueObjects.Shared;
using Marten.Internal.CompiledQueries;
using Marten.Linq.Members;
using Weasel.Postgresql;
using Weasel.Postgresql.SqlGeneration;

namespace AdCodicem.ValueObjects.Marten;

/// <summary>
/// <c>locator = ANY(:p0)</c>, the values of a <c>Contains</c> or an <c>IsOneOf</c> unwrapped, which a compiled query binds
/// to the member of its query type the values are read from.
/// </summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
/// <param name="member">The member tested.</param>
/// <param name="values">The underlying values, as a list.</param>
/// <param name="orNull">Whether a <see langword="null"/> was among the values.</param>
/// <param name="source">Where the values are read from, when the expression only reads members off an object.</param>
internal sealed class ValueObjectInFilter<TSelf, TValue>(IQueryableMember member, List<TValue> values, bool orNull, ValuesSource? source) : ISqlFragment
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// A compiled query plans the fragment from values it does not read off its own members, while one of its members holds
    /// values of the value object, or the value object itself; or from a member holding nullable values of it.
    /// </exception>
    public void Apply(ICommandBuilder builder)
    {
        var plan = builder as CompiledQueryPlan;
        var bound = plan is not null && source is not null && source.RootType == plan.QueryType;
        if (plan is not null && !bound)
        {
            // Values the query states itself are a constant of the plan, as for Marten; but a member of the query type holding
            // values of the value object may be where they come from, through a local: Marten would freeze them, so the query
            // is refused. A member holding a single one is refused by Marten itself, unless Marten listed the value object as
            // an identity: IsOneOf(First, Second) over such members would be frozen too.
            if (CompiledQueryBinding.MemberOf(plan, CompiledQueryBinding.HoldsValuesOf<TSelf>) is { } values)
            {
                throw CompiledQueryBinding.ReadsOtherwise(plan, typeof(TSelf), values);
            }

            if (CompiledQueryBinding.MemberOf(plan, static type => type == typeof(TSelf)) is { } single)
            {
                throw CompiledQueryBinding.TakesOne(plan, typeof(TSelf), single);
            }
        }

        if (bound && source!.HoldsNullable<TSelf>())
        {
            // Whether a null is among the values decides the SQL below: the plan would keep the first call's answer.
            throw new NotSupportedException(
                $"The compiled query {TypeNames.Of(plan!.QueryType)} takes nullable {TypeNames.Of(typeof(TSelf))} values as a "
                + $"parameter, through its member {source.Chain[^1].Name}, whose null test Marten would plan from the first call. "
                + $"Declare the member as a collection of {TypeNames.Of(typeof(TSelf))}.");
        }

        // As Marten's own parameter of a list of primitives: Npgsql writes a char[] as text and a byte[] as bytea.
        var typed = new CommandParameter(values);
        builder.Append(orNull ? "(" : string.Empty);
        builder.Append(member.TypedLocator);
        builder.Append(" = ANY(");
        var parameter = builder.AppendParameter(typed.Value, typed.DbType);
        builder.Append(")");
        if (orNull)
        {
            // A null among the values matches a member holding none, as it does in memory.
            builder.Append(" or ");
            builder.Append(member.NullTestLocator);
            builder.Append(" is null)");
        }

        if (bound)
        {
            CompiledQueryBinding.Bind(plan!, new ValueObjectsQueryMember<TSelf, TValue>(parameter, source!));
        }
    }
}
