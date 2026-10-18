using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AdCodicem.ValueObjects.Shared;
using Marten.Events.CodeGeneration;
using Marten.Internal.CompiledQueries;

namespace AdCodicem.ValueObjects.Marten;

/// <summary>
/// What the package does to a compiled query's plan while Marten writes its command: it binds a parameter of values read
/// off a member of the query type, and refuses a value object it cannot bind.
/// </summary>
/// <remarks>
/// <para>
/// Marten writes a compiled query's command into the <see cref="CompiledQueryPlan"/> itself, then hands each parameter to
/// the first member of <see cref="CompiledQueryPlan.QueryMembers"/> that claims it, and writes it on every call through the
/// setter of the filter that member answers with, ahead of any descriptor's binder.
/// </para>
/// <para>
/// Marten itself refuses a query type holding a value type its list of value types does not hold, before any of the
/// package's code runs: the package keeps every value object out of that list (only its nullable type is listed, which
/// Marten never accepts in a compiled query), so that a single value object is refused whatever the query does with it.
/// What reaches the package is a collection, or a value object Marten listed itself, for a document's identity.
/// </para>
/// </remarks>
internal static class CompiledQueryBinding
{
    private const string MartenReadsThem =
        "Marten reads the same public members of the query type to plan a compiled query (CompiledQueryPlan), so they are "
        + "kept wherever Marten can plan it.";

    /// <summary>Adds a query member to the plan, first, so that none of Marten's claims its parameter.</summary>
    /// <param name="plan">The plan Marten builds.</param>
    /// <param name="member">The member, whose <see cref="IQueryMember.Member"/> is the member of the query type it reads.</param>
    public static void Bind(CompiledQueryPlan plan, IQueryMember member)
    {
        // An array or a list of value objects closes IList<>, which Marten's planning takes for an Include, whose reader
        // would fail on the first call: it is the package's parameter instead.
        plan.IncludeMembers.Remove(member.Member);
        plan.QueryMembers.Insert(0, member);
    }

    /// <summary>Tells whether a type holds values of a value object, nullable or not, for a <c>Contains</c>.</summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for an enumerable of the value object.</returns>
    public static bool HoldsValuesOf<TSelf>(Type type)
        where TSelf : struct
        => typeof(IEnumerable<TSelf>).IsAssignableFrom(type) || typeof(IEnumerable<TSelf?>).IsAssignableFrom(type);

    /// <summary>
    /// Finds the first member of the query type Marten reads as a parameter whose type the predicate accepts: a public
    /// instance field or property, but one marked <see cref="MartenIgnoreAttribute"/>, as <see cref="CompiledQueryPlan"/>
    /// reads them.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="takes">Tells whether a member's type is the one looked for.</param>
    /// <returns>The member, or <see langword="null"/> when none is.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = MartenReadsThem)]
    public static MemberInfo? MemberOf(CompiledQueryPlan plan, Func<Type, bool> takes)
    {
        const BindingFlags Members = BindingFlags.Public | BindingFlags.Instance;
        return plan.QueryType.GetProperties(Members).Cast<MemberInfo>().Concat(plan.QueryType.GetFields(Members))
            .FirstOrDefault(member => !member.IsDefined(typeof(MartenIgnoreAttribute), inherit: true) && takes(TypeOf(member)));
    }

    /// <summary>Gets the type of a field or a property.</summary>
    /// <param name="member">The member.</param>
    /// <returns>Its type.</returns>
    public static Type TypeOf(MemberInfo member) => member is PropertyInfo property ? property.PropertyType : ((FieldInfo)member).FieldType;

    /// <summary>
    /// The refusal of a compiled query that takes a single value object through a member, which Marten would bind to the
    /// value of the first call for good.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="valueObjectType">The value object.</param>
    /// <param name="member">The member of the query type holding it.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException TakesOne(CompiledQueryPlan plan, Type valueObjectType, MemberInfo member)
        => new(
            $"The compiled query {TypeNames.Of(plan.QueryType)} takes {TypeNames.Of(valueObjectType)} as a parameter, through its "
            + $"member {member.Name}, which Marten cannot bind: it binds no parameter of a value type, and would answer every later "
            + "call with the value of the first. Declare the member as the underlying type and compare the value object's "
            + "Value with it: x.Member.Value == Parameter.");

    /// <summary>
    /// The refusal of a compiled query that reads values of a value object other than straight off the member of the query
    /// type holding them, which Marten would plan once.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="valueObjectType">The value object.</param>
    /// <param name="member">The member of the query type holding values of it.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException ReadsOtherwise(CompiledQueryPlan plan, Type valueObjectType, MemberInfo member)
        => new(
            $"The compiled query {TypeNames.Of(plan.QueryType)} reads values of {TypeNames.Of(valueObjectType)} other than "
            + $"straight off its member {member.Name}, which Marten would plan once and answer every later call with: read them "
            + $"in QueryIs as {member.Name}.Contains(x.Member) or x.Member.IsOneOf({member.Name}), which the package binds on "
            + "every call.");
}
