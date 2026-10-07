using System.Globalization;
using AdCodicem.ValueObjects.Testing;

namespace AdCodicem.ValueObjects.UnitTests;

// The contract kit over one value object per underlying type and per hook the rest of the domain leaves out
// (Domain/UnderlyingTypes.cs), so that what the generator emits for each of them runs rather than only compiles.

/// <inheritdoc cref="IbanContract" />
public sealed class ConsentContract : ValueObjectContract<Consent, bool>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<bool> AcceptedValues => [true, false];

    protected override IEnumerable<bool> RejectedValues => [];
}

/// <inheritdoc cref="IbanContract" />
public sealed class GradeContract : ValueObjectContract<Grade, char>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<char> AcceptedValues => ['A', 'c', 'F'];

    protected override IEnumerable<char> RejectedValues => ['G', '1', ' '];
}

/// <inheritdoc cref="IbanContract" />
public sealed class AdjustmentContract : ValueObjectContract<Adjustment, sbyte>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<sbyte> AcceptedValues => [-10, 0, 10];

    protected override IEnumerable<sbyte> RejectedValues => [-11, 11, sbyte.MinValue, sbyte.MaxValue];
}

/// <inheritdoc cref="IbanContract" />
public sealed class ScoreContract : ValueObjectContract<Score, byte>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<byte> AcceptedValues => [0, 42, 100];

    protected override IEnumerable<byte> RejectedValues => [101, byte.MaxValue];
}

/// <inheritdoc cref="IbanContract" />
public sealed class PortContract : ValueObjectContract<Port, ushort>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<ushort> AcceptedValues => [1, 443, ushort.MaxValue];

    protected override IEnumerable<ushort> RejectedValues => [0];
}

/// <inheritdoc cref="IbanContract" />
public sealed class PageNumberContract : ValueObjectContract<PageNumber, int>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<int> AcceptedValues => [1, 2, int.MaxValue];

    protected override IEnumerable<int> RejectedValues => [0, -1, int.MinValue];
}

/// <inheritdoc cref="IbanContract" />
public sealed class SequenceNumberContract : ValueObjectContract<SequenceNumber, uint>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<uint> AcceptedValues => [0, 1, uint.MaxValue];

    protected override IEnumerable<uint> RejectedValues => [];
}

/// <inheritdoc cref="IbanContract" />
public sealed class FileSizeContract : ValueObjectContract<FileSize, long>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<long> AcceptedValues => [0, 1, long.MaxValue];

    protected override IEnumerable<long> RejectedValues => [-1, long.MinValue];
}

/// <inheritdoc cref="IbanContract" />
public sealed class ByteCountContract : ValueObjectContract<ByteCount, ulong>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<ulong> AcceptedValues => [0, 1, ulong.MaxValue];

    protected override IEnumerable<ulong> RejectedValues => [];
}

/// <inheritdoc cref="IbanContract" />
public sealed class LedgerBalanceContract : ValueObjectContract<LedgerBalance, Int128>
{
    protected override bool DerivesRejectedValues => true;

    private static readonly Int128 Bound = Int128.Parse("1000000000000000000000", CultureInfo.InvariantCulture);

    protected override IEnumerable<Int128> AcceptedValues => [-Bound, 0, Bound];

    protected override IEnumerable<Int128> RejectedValues => [Bound + 1, -Bound - 1, Int128.MaxValue, Int128.MinValue];
}

/// <inheritdoc cref="IbanContract" />
public sealed class FingerprintContract : ValueObjectContract<Fingerprint, UInt128>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<UInt128> AcceptedValues => [0, 1, UInt128.MaxValue];

    protected override IEnumerable<UInt128> RejectedValues => [];
}

/// <inheritdoc cref="IbanContract" />
public sealed class LatitudeContract : ValueObjectContract<Latitude, double>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<double> AcceptedValues => [-90d, 0d, 48.8566d, 90d];

    protected override IEnumerable<double> RejectedValues =>
        [-90.0001d, 90.0001d, double.NegativeInfinity, double.PositiveInfinity, double.NaN];
}

/// <inheritdoc cref="IbanContract" />
public sealed class RatioContract : ValueObjectContract<Ratio, float>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<float> AcceptedValues => [0f, 0.5f, 1f];

    protected override IEnumerable<float> RejectedValues => [-0.1f, 1.1f, float.NaN];
}

/// <inheritdoc cref="IbanContract" />
public sealed class OpeningTimeContract : ValueObjectContract<OpeningTime, TimeOnly>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<TimeOnly> AcceptedValues => [new(6, 0), new(9, 30, 15), new(12, 0)];

    protected override IEnumerable<TimeOnly> RejectedValues => [new(5, 59), new(12, 0, 1), TimeOnly.MinValue];
}

/// <inheritdoc cref="IbanContract" />
public sealed class RecordedAtContract : ValueObjectContract<RecordedAt, DateTime>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<DateTime> AcceptedValues =>
    [
        new(2000, 1, 1),
        new(2024, 6, 1, 12, 30, 45, DateTimeKind.Utc),
        new(2099, 12, 31),
    ];

    protected override IEnumerable<DateTime> RejectedValues => [new(1999, 12, 31), DateTime.MaxValue];
}

/// <inheritdoc cref="IbanContract" />
public sealed class OccurredAtContract : ValueObjectContract<OccurredAt, DateTimeOffset>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<DateTimeOffset> AcceptedValues =>
    [
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new(2024, 6, 1, 12, 30, 0, TimeSpan.FromHours(2)),
    ];

    // Midnight on 1 January 2000 at UTC+2 is still 1999 at UTC: the bound compares instants, not wall clocks.
    protected override IEnumerable<DateTimeOffset> RejectedValues =>
        [new(2000, 1, 1, 1, 0, 0, TimeSpan.FromHours(2)), DateTimeOffset.MinValue];
}

/// <inheritdoc cref="IbanContract" />
public sealed class DurationContract : ValueObjectContract<Duration, TimeSpan>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<TimeSpan> AcceptedValues => [TimeSpan.Zero, TimeSpan.FromMinutes(90), TimeSpan.FromDays(1)];

    protected override IEnumerable<TimeSpan> RejectedValues => [TimeSpan.FromSeconds(-1), TimeSpan.FromDays(1) + TimeSpan.FromTicks(1)];
}

/// <inheritdoc cref="IbanContract" />
public sealed class EffectiveDateContract : ValueObjectContract<EffectiveDate, DateOnly>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<DateOnly> AcceptedValues => [new(2000, 1, 1), new(2024, 2, 29), DateOnly.MaxValue];

    protected override IEnumerable<DateOnly> RejectedValues => [new(1999, 12, 31), DateOnly.MinValue];
}

/// <inheritdoc cref="IbanContract" />
public sealed class ToleranceContract : ValueObjectContract<Tolerance, double>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<double> AcceptedValues => [double.MinValue, 0d, 0.05d, 1d];

    protected override IEnumerable<double> RejectedValues => [1.0001d, double.PositiveInfinity, double.NaN];
}

/// <inheritdoc cref="IbanContract" />
public sealed class PhoneNumberContract : ValueObjectContract<PhoneNumber, string>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<string> AcceptedValues => ["+33123456789", "+4930123456"];

    protected override IEnumerable<string> RejectedValues => [string.Empty, "0123456789", "+12"];
}

/// <inheritdoc cref="IbanContract" />
public sealed class LabelContract : ValueObjectContract<Label, string>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<string> AcceptedValues => [string.Empty, "Urgent", new string('x', 200)];

    protected override IEnumerable<string> RejectedValues => [new string('x', 201)];
}

/// <inheritdoc cref="IbanContract" />
public sealed class DocumentStatusContract : ValueObjectContract<DocumentStatus, string>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<string> AcceptedValues => ["draft", "FINAL"];

    protected override IEnumerable<string> RejectedValues => [string.Empty, "archived"];
}
