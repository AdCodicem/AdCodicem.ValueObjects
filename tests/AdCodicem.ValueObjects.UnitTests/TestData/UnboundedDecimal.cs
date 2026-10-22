namespace AdCodicem.ValueObjects.UnitTests.TestData;

/// <summary>A decimal with no bound, drawn across the whole type, whose extremes a GraphQL scalar carries too.</summary>
[ValueObject<decimal>]
public readonly partial struct UnboundedDecimal;
