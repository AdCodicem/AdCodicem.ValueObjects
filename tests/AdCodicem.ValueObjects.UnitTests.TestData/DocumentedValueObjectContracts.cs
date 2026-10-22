using AdCodicem.ValueObjects.Testing;

namespace AdCodicem.ValueObjects.UnitTests;

// The contract kit over the declarations the OpenAPI document reads differently from the rest of the domain
// (Domain/DocumentedValueObjects.cs): bounds written with an exponent, and closed sets over underlying types other
// than string. A closed set accepts its known values and nothing else.

/// <inheritdoc cref="IbanContract" />
public sealed class MassContract : ValueObjectContract<Mass, double>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<double> AcceptedValues => [9.1e-31, 70.5, 2e32];

    protected override IEnumerable<double> RejectedValues => [0, 9e-31, 2.1e32, double.NaN, double.PositiveInfinity];
}

/// <inheritdoc cref="IbanContract" />
public sealed class TransferLimitContract : ValueObjectContract<TransferLimit, decimal>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<decimal> AcceptedValues => [0m, 250.75m, 1_000_000m];

    protected override IEnumerable<decimal> RejectedValues => [-0.01m, 1_000_000.01m];
}

/// <inheritdoc cref="IbanContract" />
public sealed class LuminanceContract : ValueObjectContract<Luminance, float>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<float> AcceptedValues => [0f, 350.5f, 1500f];

    protected override IEnumerable<float> RejectedValues => [-1f, 1500.5f, float.NaN];
}

/// <inheritdoc cref="IbanContract" />
public sealed class PriorityContract : ValueObjectContract<Priority, int>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<int> AcceptedValues => [1, 3];

    protected override IEnumerable<int> RejectedValues => [0, 2, 4];
}

/// <inheritdoc cref="IbanContract" />
public sealed class StorageQuotaContract : ValueObjectContract<StorageQuota, long>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<long> AcceptedValues => [1_000_000_000L, 10_000_000_000L];

    protected override IEnumerable<long> RejectedValues => [0L, 9_999_999_999L];
}

/// <inheritdoc cref="IbanContract" />
public sealed class VatRateContract : ValueObjectContract<VatRate, decimal>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<decimal> AcceptedValues => [20.0m, 5.5m, 20m];

    protected override IEnumerable<decimal> RejectedValues => [19.6m, 0m];
}

/// <inheritdoc cref="IbanContract" />
public sealed class VoteWeightContract : ValueObjectContract<VoteWeight, double>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<double> AcceptedValues => [0.5, 1.0];

    protected override IEnumerable<double> RejectedValues => [0.75, 0, double.NaN];
}

/// <inheritdoc cref="IbanContract" />
public sealed class OpacityContract : ValueObjectContract<Opacity, float>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<float> AcceptedValues => [0.25f, 1f];

    protected override IEnumerable<float> RejectedValues => [0.5f, 0f];
}

/// <inheritdoc cref="IbanContract" />
public sealed class TermsAcceptedContract : ValueObjectContract<TermsAccepted, bool>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<bool> AcceptedValues => [true];

    protected override IEnumerable<bool> RejectedValues => [false];
}

/// <inheritdoc cref="IbanContract" />
public sealed class HttpStatusContract : ValueObjectContract<HttpStatus, short>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<short> AcceptedValues => [200, 404];

    protected override IEnumerable<short> RejectedValues => [500, 0];
}

/// <inheritdoc cref="IbanContract" />
public sealed class BlockSizeContract : ValueObjectContract<BlockSize, uint>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<uint> AcceptedValues => [4096u, 65536u];

    protected override IEnumerable<uint> RejectedValues => [512u, 0u];
}

/// <inheritdoc cref="IbanContract" />
public sealed class CutOffDateContract : ValueObjectContract<CutOffDate, DateOnly>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<DateOnly> AcceptedValues => [new(2000, 1, 1), new(2001, 1, 1)];

    protected override IEnumerable<DateOnly> RejectedValues => [new(2000, 1, 2), DateOnly.MinValue];
}

/// <inheritdoc cref="IbanContract" />
public sealed class ShiftStartContract : ValueObjectContract<ShiftStart, TimeOnly>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<TimeOnly> AcceptedValues => [new(6, 0), new(14, 0)];

    protected override IEnumerable<TimeOnly> RejectedValues => [new(6, 0, 1), TimeOnly.MinValue];
}

/// <inheritdoc cref="IbanContract" />
public sealed class LaunchMomentContract : ValueObjectContract<LaunchMoment, DateTimeOffset>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<DateTimeOffset> AcceptedValues =>
        [new(2000, 1, 1, 9, 0, 0, TimeSpan.FromHours(1)), new(2001, 1, 1, 9, 0, 0, TimeSpan.FromHours(1))];

    protected override IEnumerable<DateTimeOffset> RejectedValues =>
        [new(2000, 1, 1, 9, 0, 0, TimeSpan.Zero), DateTimeOffset.MinValue];
}

/// <inheritdoc cref="IbanContract" />
public sealed class AnswerContract : ValueObjectContract<Answer, char>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<char> AcceptedValues => ['Y', 'N'];

    protected override IEnumerable<char> RejectedValues => ['y', 'M', ' '];
}
