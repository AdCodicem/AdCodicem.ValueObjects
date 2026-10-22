using AdCodicem.ValueObjects.Testing;

namespace AdCodicem.ValueObjects.UnitTests;

// The contract kit over the value objects classified as personal data (Domain/ClassifiedValueObjects.cs).

/// <inheritdoc cref="IbanContract" />
public sealed class PassportNumberContract : ValueObjectContract<PassportNumber, string>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<string> AcceptedValues => ["X1234567", " ab123456 "];

    protected override IEnumerable<string> RejectedValues => [string.Empty, "X1", "X1234567890"];
}

/// <inheritdoc cref="IbanContract" />
public sealed class SalaryContract : ValueObjectContract<Salary, decimal>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<decimal> AcceptedValues => [0m, 42_000m];

    protected override IEnumerable<decimal> RejectedValues => [-1m];
}

/// <inheritdoc cref="AccountIdContract" />
public sealed class PatientIdContract : ValueObjectContract<PatientId, string>
{
    protected override bool DerivesRejectedValues => true;

    private static readonly string[] Minted = [PatientId.New().Value, PatientId.New().Value];

    protected override IEnumerable<string> AcceptedValues => Minted;

    protected override IEnumerable<string> RejectedValues => [string.Empty, "pat_nope", AccountId.New().Value];
}

/// <inheritdoc cref="IbanContract" />
public sealed class PseudonymContract : ValueObjectContract<Pseudonym<PurchaseOrder>, string>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<string> AcceptedValues => ["Ada", "Grace Hopper"];

    protected override IEnumerable<string> RejectedValues => [string.Empty, "A", new string('a', 33)];
}

/// <inheritdoc cref="IbanContract" />
public sealed class TeamNameContract : ValueObjectContract<TeamName<PurchaseOrder>, string>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<string> AcceptedValues => ["Core", "Platform"];

    protected override IEnumerable<string> RejectedValues => [string.Empty, "C", new string('c', 33)];
}
