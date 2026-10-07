namespace AdCodicem.ValueObjects.IntegrationTests.Fixtures;

// The shapes the MongoDB tests store that neither the sample nor the unit suite's UnderlyingTypes.cs declares: a value
// object over a short, bounded as the issue's evidence bounds a quantity, a generic value object, and a text value object
// with a minimum length and no pattern, which a validator halves.

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

/// <summary>A nickname, of four to eight characters.</summary>
[ValueObject<string>(MinLength = 4, MaxLength = 8)]
public readonly partial struct Nickname;
