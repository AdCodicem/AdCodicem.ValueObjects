using System.Runtime.Loader;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Testing;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Xunit.Sdk;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The contract kit on what the contracts of this assembly never meet: a value object it cannot fully check, and one
/// from a module nothing has used yet.
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
    /// A consumer's contracts usually live in a test assembly of their own, so a registry check may be the first test
    /// to need the assembly declaring the type - run alone, through a filter. The kit does not count on the module
    /// initializer of that assembly having run: it runs the generated registration itself.
    /// </summary>
    [Fact]
    public void The_registry_checks_find_a_type_from_a_module_nothing_has_used_yet()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "AdCodicem.ValueObjects.Fixtures.Untouched.dll");
        var assembly = new AssemblyLoadContext("A copy of the module nothing has used").LoadFromAssemblyPath(path);
        var rank = assembly.GetType("AdCodicem.ValueObjects.Fixtures.Untouched.Rank", throwOnError: true)!;
        ValueObjectRegistry.TryGet(rank, out _).Should().BeFalse("nothing has used this copy of the assembly yet");

        var contract = (IRegistryChecks)Activator.CreateInstance(typeof(RankContract<>).MakeGenericType(rank))!;
        contract.The_type_is_discoverable_at_run_time();
        contract.Declared_length_limits_hold_for_every_accepted_value();

        ValueObjectRegistry.TryGet(rank, out var descriptor).Should().BeTrue();
        descriptor!.Schema.Description.Should().Be("A rank, from 1 to 10.", "the generated registration describes it");
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

    /// <summary>The two checks of the kit that read the registry.</summary>
    private interface IRegistryChecks
    {
        void Declared_length_limits_hold_for_every_accepted_value();

        void The_type_is_discoverable_at_run_time();
    }

    /// <summary>
    /// A contract over a rank, from 1 to 10, closed at run time over the copy of the type a test loaded.
    /// </summary>
    /// <typeparam name="TRank">The rank type of that copy.</typeparam>
    private sealed class RankContract<TRank> : ValueObjectContract<TRank, int>, IRegistryChecks
        where TRank : struct, IValueObject<TRank, int>
    {
        protected override IEnumerable<int> AcceptedValues => [1, 10];

        protected override IEnumerable<int> RejectedValues => [0, 11];
    }
}
