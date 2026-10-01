namespace AdCodicem.ValueObjects.UnitTests.Domain;

// One value object for each underlying type the rest of the domain does not use, and for each option or hook it
// does not declare, so that the code the generator emits for each of the 22 underlying types runs at least once
// rather than only compiling (docs/adr/0006-coverage-is-a-signal-not-a-goal.md). The bounds and options are
// chosen to reach the emitted branches; the names only keep the tests readable.

/// <summary>A latitude, in degrees.</summary>
[ValueObject<double>(Arithmetic = true, Minimum = "-90", Maximum = "90")]
public readonly partial struct Latitude;

/// <summary>A ratio between zero and one.</summary>
[ValueObject<float>(Arithmetic = true, Minimum = "0", Maximum = "1")]
public readonly partial struct Ratio;

/// <summary>The time a shop opens.</summary>
[ValueObject<TimeOnly>(Minimum = "06:00", Maximum = "12:00")]
public readonly partial struct OpeningTime;

/// <summary>When a record was written.</summary>
[ValueObject<DateTime>(Minimum = "2000-01-01", Maximum = "2099-12-31")]
public readonly partial struct RecordedAt;

/// <summary>When an event occurred, with the offset it occurred at.</summary>
[ValueObject<DateTimeOffset>(Minimum = "2000-01-01T00:00:00+00:00")]
public readonly partial struct OccurredAt;
