using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.Untouched;

/// <summary>A rank, from 1 to 10.</summary>
[ValueObject<int>]
public readonly partial struct Rank : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    /// <summary>Gets the lowest rank.</summary>
    public static int Minimum => 1;

    /// <summary>Gets the highest rank.</summary>
    public static int Maximum => 10;
}
