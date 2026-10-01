using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Testing;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Xunit.Sdk;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The contract kit on a value object it cannot fully check, which the contracts of this assembly never are.
/// </summary>
public class ContractKitTests
{
    /// <summary>
    /// The declared limits live in the registry's schema. A type that did not register itself has none to read,
    /// and a check that cannot run must say so rather than pass.
    /// </summary>
    [Fact]
    public void The_length_check_reports_itself_skipped_for_a_type_the_registry_does_not_know()
    {
        ValueObjectRegistry.TryGet(typeof(UnregisteredCode), out _).Should().BeFalse();

        var act = () => new UnregisteredContract().Declared_length_limits_hold_for_every_accepted_value();

        act.Should().Throw<SkipException>().WithMessage("*'UnregisteredCode' did not register itself*");
    }

    /// <summary>
    /// Private, so the runner does not discover it: run whole, it would fail The_type_is_discoverable_at_run_time,
    /// which is exactly right for an unregistered type and not what this class is checking.
    /// </summary>
    private sealed class UnregisteredContract : ValueObjectContract<UnregisteredCode, string>
    {
        protected override IEnumerable<string> AcceptedValues => ["one", "two"];

        protected override IEnumerable<string> RejectedValues => [string.Empty];
    }
}
