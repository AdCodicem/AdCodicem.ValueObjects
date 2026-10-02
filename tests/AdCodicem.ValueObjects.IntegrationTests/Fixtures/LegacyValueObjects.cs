using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.IntegrationTests.Fixtures;

// Value objects a legacy schema stores in a column of another type than their underlying one.

/// <summary>A transfer reference, which a legacy schema keeps in a uuid or uniqueidentifier column.</summary>
[ValueObject<string>(MinLength = 36, MaxLength = 36)]
public readonly partial struct TransferReference;
