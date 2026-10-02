using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.Untouched;

/// <summary>A rank, from 1 to 10.</summary>
[ValueObject<int>(Minimum = "1", Maximum = "10")]
public readonly partial struct Rank;
