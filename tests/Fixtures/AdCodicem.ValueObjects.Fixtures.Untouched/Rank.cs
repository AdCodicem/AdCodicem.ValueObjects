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

/// <summary>A rank, from 1 to 10, among the competitors of one kind.</summary>
/// <typeparam name="TCompetitor">The kind of competitor ranked.</typeparam>
[ValueObject<int>]
public readonly partial struct Standing<TCompetitor> : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
#pragma warning disable CA1000 // The hooks are static members, and the generic type is the point of the declaration.
    /// <summary>Gets the lowest rank.</summary>
    public static int Minimum => 1;

    /// <summary>Gets the highest rank.</summary>
    /// <remarks>
    /// Initialized rather than expression-bodied: the first check reading it initializes the construction, whose schema
    /// then reads it while that check is still under way.
    /// </remarks>
    public static int Maximum { get; } = 10;
#pragma warning restore CA1000
}
