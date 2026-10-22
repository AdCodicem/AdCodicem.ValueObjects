using AdCodicem.ValueObjects.UnitTests.GraphQL;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The graphql family's part of Sample (GeneratedSurface/Sample.cs): the checks that hold every sample to HotChocolate
// and HotChocolate.Data.

public abstract partial class Sample
{
    /// <summary>
    /// Builds a HotChocolate schema echoing the value object, alone, optional and in a list, with <c>AddValueObjects()</c>,
    /// and checks that it is a scalar named by the conventions, described by its schema, serialized by its underlying type
    /// and carrying its pattern; that the two instances and every value at an edge of its rules are read as literals and
    /// variables and written back as the underlying value; and that every value its schema rules out, and the refused text
    /// of a String scalar, are answered with the code its rules give.
    /// </summary>
    /// <returns>The check.</returns>
    public abstract Task AnswersARefusedGraphQLValueWithItsRuleAsync();

    /// <summary>
    /// Filters rows holding the value object through HotChocolate.Data with <c>AddValueObjectFiltering()</c>, in memory
    /// and on Entity Framework Core over SQLite, and checks that its input is named after its scalar and holds the
    /// comparable operations, or <c>eq</c> and <c>neq</c> over <see cref="bool"/>; that <c>eq</c>, <c>in</c> and <c>gt</c>
    /// of each instance, or its <c>eq</c> alone over <see cref="bool"/>, written as the scalar's literal, find the rows the
    /// instances' own equality and order find; and that SQLite, which a value object over a 128-bit integer skips, runs
    /// <c>eq</c> as it runs the same comparison of a column of the underlying type, or refuses a comparison where it
    /// refuses that one.
    /// </summary>
    /// <returns>The check.</returns>
    public abstract Task FiltersThroughItsScalarAsItsUnderlyingValueAsync();
}

public partial class Sample<TSelf, TValue>
{
    public override Task AnswersARefusedGraphQLValueWithItsRuleAsync() => GraphQLParity.CheckAsync<TSelf, TValue>(Small, Large, Refused);

    public override Task FiltersThroughItsScalarAsItsUnderlyingValueAsync() => GraphQLFilteringParity.CheckAsync<TSelf, TValue>(Small, Large);
}
