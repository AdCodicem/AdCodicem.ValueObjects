using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.XmlSerialization.Billing;

// Value objects named like those of the Freight namespace, which one XML namespace cannot hold apart: each pair but Code
// differs from its namesake by one thing, its underlying type, its rules or its description.

/// <summary>A billing code, three characters at most.</summary>
[ValueObject<string>(MaxLength = 3)]
public readonly partial struct Code;

/// <summary>A reference, which another namespace names alike.</summary>
[ValueObject<string>]
public readonly partial struct Reference;

/// <summary>A grade, which another namespace names alike.</summary>
[ValueObject<int>]
public readonly partial struct Grade : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    /// <inheritdoc />
    public static int Minimum => 1;

    /// <inheritdoc />
    public static int Maximum => 5;
}

/// <summary>A note on a bill.</summary>
[ValueObject<string>]
public readonly partial struct Note;
