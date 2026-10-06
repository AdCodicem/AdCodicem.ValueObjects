using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.XmlSerialization.Freight;

// Value objects named like those of the Billing namespace: each pair but Code differs from its namesake by one thing.

/// <summary>A freight code, a number from 1 to 10.</summary>
[ValueObject<int>]
public readonly partial struct Code : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    /// <inheritdoc />
    public static int Minimum => 1;

    /// <inheritdoc />
    public static int Maximum => 10;
}

/// <summary>A reference, which another namespace names alike.</summary>
[ValueObject<int>]
public readonly partial struct Reference;

/// <summary>A grade, which another namespace names alike.</summary>
[ValueObject<int>]
public readonly partial struct Grade : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    /// <inheritdoc />
    public static int Minimum => 1;

    /// <inheritdoc />
    public static int Maximum => 9;
}

/// <summary>A note on a consignment.</summary>
[ValueObject<string>]
public readonly partial struct Note;
