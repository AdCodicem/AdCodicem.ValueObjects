using AdCodicem.ValueObjects.UnitTests.GraphQL;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The graphql family's part of Sample (GeneratedSurface/Sample.cs): the check that holds every sample to HotChocolate.

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
}

public partial class Sample<TSelf, TValue>
{
    public override Task AnswersARefusedGraphQLValueWithItsRuleAsync() => GraphQLParity.CheckAsync<TSelf, TValue>(Small, Large, Refused);
}
