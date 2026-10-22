// Two value objects of one name, in namespaces of their own: HotChocolate refuses the second scalar of a name.
#pragma warning disable IDE0161 // Two namespaces cannot share one file-scoped declaration.
namespace AdCodicem.ValueObjects.UnitTests.GraphQL.Billing
{
    /// <summary>A billing code, which shares its name with the shipping one.</summary>
    [ValueObject<string>(MinLength = 2)]
    public readonly partial struct Code;
}

namespace AdCodicem.ValueObjects.UnitTests.GraphQL.Shipping
{
    /// <summary>A shipping code, which shares its name with the billing one.</summary>
    [ValueObject<int>]
    public readonly partial struct Code : IValueObjectMinimum<int>
    {
        public static int Minimum => 1;
    }
}
