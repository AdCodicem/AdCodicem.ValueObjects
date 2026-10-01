namespace AdCodicem.ValueObjects.UnitTests.Domain;

// Declarations only the OpenAPI document reads differently from the rest of the domain: bounds written with an
// exponent, which the attribute accepts for decimal, double and float.

/// <summary>A mass, in kilograms, from an electron's to a star's: bounds an exponent writes legibly.</summary>
[ValueObject<double>(Minimum = "9.1e-31", Maximum = "2e32")]
public readonly partial struct Mass;

/// <summary>The most a single transfer may move, in euros.</summary>
[ValueObject<decimal>(Minimum = "0", Maximum = "1e6")]
public readonly partial struct TransferLimit;

/// <summary>The luminance of a screen, in nits.</summary>
[ValueObject<float>(Minimum = "0", Maximum = "1.5e3")]
public readonly partial struct Luminance;
