namespace AdCodicem.ValueObjects.IntegrationTests.Fixtures;

// The two shapes MongoDbTests stores that neither the sample nor the unit suite's UnderlyingTypes.cs declares: a value
// object over a short, bounded as the issue's evidence bounds a quantity, and a generic value object.

/// <summary>A number of items on an order line.</summary>
[ValueObject<short>(Arithmetic = true)]
public readonly partial struct LineQuantity : IValueObjectMinimum<short>, IValueObjectMaximum<short>
{
    public static short Minimum => 1;

    public static short Maximum => 999;
}

/// <summary>A code, whose owner is part of its type.</summary>
/// <typeparam name="TOwner">The kind of thing the code names.</typeparam>
[ValueObject<string>(MaxLength = 12)]
public readonly partial struct OwnedCode<TOwner>
    where TOwner : class;
