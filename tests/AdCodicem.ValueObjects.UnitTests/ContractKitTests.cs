using System.Runtime.Loader;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Annotations;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Testing;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Xunit.Sdk;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The contract kit on what the contracts of this assembly never meet: a value object it cannot fully check, and one
/// from a module nothing has used yet.
/// </summary>
public partial class ContractKitTests
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
    /// A construction of a generic value object is never registered as such, and in a module nothing has used yet,
    /// nothing resolved it either: the kit resolves it before checking it is discoverable.
    /// </summary>
    [Fact]
    public void The_registry_checks_find_a_construction_from_a_module_nothing_has_used_yet()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "AdCodicem.ValueObjects.Fixtures.Untouched.dll");
        var assembly = new AssemblyLoadContext("Another copy of the module nothing has used").LoadFromAssemblyPath(path);
        var standing = assembly.GetType("AdCodicem.ValueObjects.Fixtures.Untouched.Standing`1", throwOnError: true)!
            .MakeGenericType(typeof(string));
        ValueObjectRegistry.TryGet(standing, out _).Should().BeFalse("nothing has used this copy of the assembly yet");

        var contract = (IRegistryChecks)Activator.CreateInstance(typeof(RankContract<>).MakeGenericType(standing))!;
        contract.The_type_is_discoverable_at_run_time();
        contract.Declared_length_limits_hold_for_every_accepted_value();

        ValueObjectRegistry.TryGet(standing, out var descriptor).Should().BeTrue();
        descriptor!.Schema.Minimum.Should().Be("1", "the generated schema describes the construction");
    }

    /// <summary>
    /// The declared example and known values live in the registry's schema as well, and a check that cannot run says so.
    /// </summary>
    [Fact]
    public void The_declaration_checks_report_themselves_skipped_for_a_type_the_registry_does_not_know()
    {
        var contract = new UnregisteredContract();

        contract.Invoking(checks => checks.The_declared_example_is_accepted()).Should().Throw<SkipException>()
            .WithMessage("*'UnregisteredCode' did not register itself, so it has no declared example to check.*");
        contract.Invoking(checks => checks.Every_declared_known_value_is_accepted()).Should().Throw<SkipException>()
            .WithMessage("*'UnregisteredCode' did not register itself, so it has no declared known value to check.*");
    }

    /// <summary>
    /// The OpenAPI document publishes the example as it is declared. A pattern runs only at run time, so the generator
    /// lets this one through, and the kit is what catches it, with the rule it broke.
    /// </summary>
    [Fact]
    public void An_example_its_type_refuses_fails_the_contract_with_the_rule_it_broke()
    {
        var act = () => new DigitsContract().The_declared_example_is_accepted();

        act.Should().Throw<XunitException>().WithMessage(
            "The example 'abc' declared on 'Digits' is refused (value_object.invalid_format): The value does not match the expected format.*");
    }

    /// <summary>
    /// A refused known value throws from the type initializer, and every check of the type fails with a
    /// <see cref="TypeInitializationException"/>. A construction of a generic value object initializes when first used,
    /// which the kit does itself, so it reports the rule behind the initializer, as often as it is asked.
    /// </summary>
    [Fact]
    public void A_known_value_its_type_refuses_fails_the_contract_with_the_rule_it_broke()
    {
        var contract = new ShadeContract();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            contract.Invoking(checks => checks.Every_declared_known_value_is_accepted()).Should().Throw<XunitException>()
                .WithMessage(
                    "'Shade' cannot initialize: a value it creates as it does, a known value most likely, is refused "
                    + "(value_object.invalid_format): 'Shade`1' rejected the supplied value: The shade is not lower case.");
            contract.Invoking(checks => checks.The_declared_example_is_accepted()).Should().Throw<XunitException>()
                .WithMessage("'Shade' cannot initialize*");
        }
    }

    /// <summary>
    /// A type that fails to initialize for any other reason than a refused value is no matter of the kit's: the runner
    /// reports the exception as it is.
    /// </summary>
    [Fact]
    public void A_type_failing_to_initialize_for_another_reason_is_left_to_the_runner()
    {
        var act = () => new BrokenContract().Every_declared_known_value_is_accepted();

        act.Should().Throw<TypeInitializationException>().WithInnerException<InvalidOperationException>();
    }

    /// <summary>
    /// A type whose example and known values it accepts passes; one that declares neither has nothing to check.
    /// </summary>
    [Fact]
    public void The_declaration_checks_pass_on_what_a_type_accepts_and_skip_what_it_does_not_declare()
    {
        var declaring = new DigitsContract();
        var silent = new SilentContract();

        declaring.Every_declared_known_value_is_accepted();
        silent.Invoking(checks => checks.The_declared_example_is_accepted()).Should().Throw<SkipException>()
            .WithMessage("*'Silent' declares no example.");
        silent.Invoking(checks => checks.Every_declared_known_value_is_accepted()).Should().Throw<SkipException>()
            .WithMessage("*'Silent' declares no known value.");
    }

    /// <summary>A code of digits, whose example is not one, and whose known value is.</summary>
    [ValueObject<string>(Example = "abc")]
    [KnownValue("Zero", "0")]
    public readonly partial struct Digits : IValueObjectPatternValidator
    {
        [GeneratedRegex("^[0-9]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
        public static partial Regex Pattern { get; }
    }

    /// <summary>A value object that declares neither an example nor a known value.</summary>
    [ValueObject<string>]
    public readonly partial struct Silent;

    /// <summary>Private, so the runner does not discover it: its example is wrong on purpose.</summary>
    private sealed class DigitsContract : ValueObjectContract<Digits, string>
    {
        protected override IEnumerable<string> AcceptedValues => ["0", "42"];

        protected override IEnumerable<string> RejectedValues => ["abc"];
    }

    /// <summary>Private, so the runner does not discover it: the checks it is used for skip.</summary>
    private sealed class SilentContract : ValueObjectContract<Silent, string>
    {
        protected override IEnumerable<string> AcceptedValues => ["a", "b"];

        protected override IEnumerable<string> RejectedValues => [string.Empty];
    }

    /// <summary>
    /// Private, so the runner does not discover it: its type cannot initialize, on purpose.
    /// </summary>
    private sealed class ShadeContract : ValueObjectContract<Shade<ContractKitTests>, string>
    {
        protected override IEnumerable<string> AcceptedValues => ["light", "pale"];

        protected override IEnumerable<string> RejectedValues => ["DARK"];
    }

    /// <summary>
    /// Private, so the runner does not discover it: its type cannot initialize, on purpose.
    /// </summary>
    private sealed class BrokenContract : ValueObjectContract<Broken<ContractKitTests>, string>
    {
        protected override IEnumerable<string> AcceptedValues => ["a", "b"];

        protected override IEnumerable<string> RejectedValues => [string.Empty];
    }

    /// <summary>
    /// A shade of some surface, written in lower case, one of whose known values breaks that rule. Generic, so that
    /// nothing initializes a construction but the test asking for it: the registration of the module only registers
    /// the definition.
    /// </summary>
    /// <typeparam name="TSurface">The surface shaded.</typeparam>
    [ValueObject<string>]
    [KnownValue("Light", "light")]
    [KnownValue("Dark", "DARK")]
    private readonly partial struct Shade<TSurface> : IValueObjectValidator<string>
    {
        public static ValidationResult ValidateValue(in string value)
            => string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal)
                ? ValidationResult.Success
                : ValidationResult.InvalidFormat("The shade is not lower case.");
    }

    /// <summary>A value object whose initializer fails for a reason of its own. Generic, as <see cref="Shade{TSurface}"/>.</summary>
    /// <typeparam name="TOwner">The owner of the value.</typeparam>
    [ValueObject<string>]
    private readonly partial struct Broken<TOwner>
    {
        static Broken() => throw new InvalidOperationException("Broken on purpose.");
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
