using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.IntegrationTests.Fixtures;

// One value object over each type of the date and time family, which providers do not all return as such: SQL
// Server returns a DateTime for a date and a TimeSpan for a time, Npgsql a DateOnly and a TimeOnly for them, and a
// UTC DateTime for a timestamptz. The sample's domain has none of them.

/// <summary>A business day.</summary>
[ValueObject<DateOnly>]
public readonly partial struct BusinessDay;

/// <summary>The time of day a payment run closes.</summary>
[ValueObject<TimeOnly>]
public readonly partial struct CutOffTime;

/// <summary>When a payment was settled, with no zone.</summary>
[ValueObject<DateTime>]
public readonly partial struct SettledAt;

/// <summary>How long a payment took to clear.</summary>
[ValueObject<TimeSpan>]
public readonly partial struct ClearingDelay;

/// <summary>When a payment was received, with its offset.</summary>
[ValueObject<DateTimeOffset>]
public readonly partial struct ReceivedAt;
