using AdCodicem.ValueObjects.Identifiers;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// The <c>[EntityId]</c> emission path, its diagnostics, and the invariant that keeps the compile-time layout
/// and the runtime layout in step.
/// </summary>
public sealed class EntityIdTests
{
    [Fact]
    public void An_identifier_compiles_and_implements_the_contract()
    {
        var run = GeneratorHarness.Run("""
            [EntityId("acc")]
            public readonly partial struct AccountId;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("IEntityId<global::Test.AccountId>");
    }

    [Fact]
    public void An_identifier_gets_its_minting_members()
    {
        var generated = GeneratorHarness.Run("""
            [EntityId("acc")]
            public readonly partial struct AccountId;
            """).SingleValueObject;

        generated.Should().Contain("public static string Prefix => \"acc\";");
        generated.Should().Contain("public static int Length => 30;");
        generated.Should().Contain("New()");
        generated.Should().Contain("New(global::System.TimeProvider timeProvider");
    }

    /// <summary>
    /// The whole point of a fixed alphabet at a fixed length: the check is a scan, so an identifier type costs
    /// no compiled regular expression at start-up — unlike a Pattern-constrained value object, which the
    /// [GeneratedRegex] limitation forces into one.
    /// </summary>
    [Fact]
    public void An_identifier_compiles_no_regular_expression()
    {
        var generated = GeneratorHarness.Run("""
            [EntityId("acc")]
            public readonly partial struct AccountId;
            """).SingleValueObject;

        generated.Should().NotContain("RegularExpressions.Regex");
        generated.Should().Contain("EntityIdFormat.SchemaPattern(Prefix, Granularity)");
    }

    [Fact]
    public void An_identifier_publishes_its_prefix_to_the_registry()
    {
        var run = GeneratorHarness.Run("""
            [EntityId("acc")]
            public readonly partial struct AccountId;
            """);

        var registration = run.Files.Single(file => file.HintName.Contains("Registration", StringComparison.Ordinal));

        registration.Text.Should().Contain("EntityIdRegistry.Register<global::Test.AccountId>();");
        registration.Text.Should().Contain("ValueObjectRegistry.Register<global::Test.AccountId,");
    }

    /// <summary>
    /// The compile-time layout table restates what the runtime holds, because an analyzer targets
    /// netstandard2.0 and cannot reference the package it generates calls into. This is what stops the pair
    /// from drifting into a column width the runtime then refuses to fill.
    /// </summary>
    [Theory]
    [InlineData("Minute", IdGranularity.Minute)]
    [InlineData("Hour", IdGranularity.Hour)]
    [InlineData("Day", IdGranularity.Day)]
    public void The_emitted_length_matches_the_runtime_layout(string granularity, IdGranularity expected)
    {
        var generated = GeneratorHarness.Run($$"""
            [EntityId("acc", Granularity = IdGranularity.{{granularity}})]
            public readonly partial struct AccountId;
            """).SingleValueObject;

        var runtimeLength = EntityIdFormat.TotalLength("acc", expected);

        generated.Should().Contain($"public static int Length => {runtimeLength};");
        generated.Should().Contain($"MinLength = {runtimeLength},");
        generated.Should().Contain($"MaxLength = {runtimeLength},");
    }

    [Fact]
    public void The_default_granularity_matches_the_attribute_default()
    {
        var generated = GeneratorHarness.Run("""
            [EntityId("acc")]
            public readonly partial struct AccountId;
            """).SingleValueObject;

        var declared = new EntityIdAttribute("acc").Granularity;

        generated.Should().Contain($"IdGranularity.{declared};");
    }

    /// <summary>
    /// Whether the generator accepts a prefix and whether the runtime accepts it must never disagree: one
    /// rejects at build time, the other at the first call, and only one of those is a good place to find out.
    /// </summary>
    [Theory]
    [InlineData("acc")]
    [InlineData("sk_live")]
    [InlineData("a")]
    [InlineData("")]
    [InlineData("Acc")]
    [InlineData("acc_")]
    [InlineData("_acc")]
    [InlineData("ac-c")]
    [InlineData("1acc")]
    [InlineData("acc__x")]
    [InlineData("verylongsegmentname")]
    [InlineData("aaaaaaaa_bbbbbbbb_cc")]
    public void The_generator_and_the_runtime_agree_on_which_prefixes_are_valid(string prefix)
    {
        var run = GeneratorHarness.Run($$"""
            [EntityId("{{prefix}}")]
            public readonly partial struct AccountId;
            """);

        var acceptedByGenerator = !run.Ids.Contains("VO0015");
        var acceptedByRuntime = EntityIdPrefix.IsValid(prefix, out _);

        acceptedByGenerator.Should().Be(acceptedByRuntime, "the prefix '{0}'", prefix);
    }

    [Fact]
    public void A_malformed_prefix_says_which_rule_it_broke()
    {
        var run = GeneratorHarness.Run("""
            [EntityId("Acc")]
            public readonly partial struct AccountId;
            """);

        run.Ids.Should().Contain("VO0015");
        run.Diagnostics.Single(d => d.Id == "VO0015")
            .GetMessage()
            .Should().Contain("lowercase letter");
    }

    [Fact]
    public void Two_types_claiming_one_prefix_are_reported()
    {
        var run = GeneratorHarness.Run("""
            [EntityId("acc")]
            public readonly partial struct AccountId;

            [EntityId("acc")]
            public readonly partial struct BillingAccountId;
            """);

        run.Ids.Should().Contain("VO0016");
        run.Diagnostics.Single(d => d.Id == "VO0016").GetMessage().Should().Contain("'acc'");
    }

    [Fact]
    public void Distinct_prefixes_are_not_reported()
    {
        var run = GeneratorHarness.Run("""
            [EntityId("acc")]
            public readonly partial struct AccountId;

            [EntityId("cus")]
            public readonly partial struct CustomerId;
            """);

        run.Ids.Should().NotContain("VO0016");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The generator owns the canonical spelling of the format, so a normalizer would be written and never
    /// called — the silent failure the hook interfaces exist to prevent.
    /// </summary>
    [Fact]
    public void A_normalization_hook_on_an_identifier_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [EntityId("acc")]
            public readonly partial struct AccountId : IValueObjectNormalizer<string>
            {
                public static string NormalizeValue(string value) => value;
            }
            """);

        run.Ids.Should().Contain("VO0017");
    }

    [Fact]
    public void A_validation_hook_on_an_identifier_is_allowed()
    {
        var run = GeneratorHarness.Run("""
            [EntityId("acc")]
            public readonly partial struct AccountId : IValueObjectValidator<string>
            {
                public static ValidationResult ValidateValue(in string value) => ValidationResult.Success;
            }
            """);

        run.Ids.Should().NotContain("VO0017");
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("return ValidateValue(in value);");
    }

    [Fact]
    public void Both_annotations_on_one_type_are_reported_once()
    {
        var run = GeneratorHarness.Run("""
            [EntityId("acc")]
            [ValueObject<string>]
            public readonly partial struct AccountId;
            """);

        run.Ids.Should().ContainSingle().Which.Should().Be("VO0018");
        run.Files.Should().BeEmpty("neither path may emit, or the two would collide on the hint name");
    }

    [Theory]
    [InlineData("public partial struct AccountId;", "VO0002")]
    [InlineData("public readonly struct AccountId;", "VO0001")]
    [InlineData("public readonly partial record struct AccountId;", "VO0002")]
    [InlineData("public readonly partial class AccountId;", "VO0002")]
    public void An_identifier_declared_wrongly_reaches_the_same_diagnostics_as_a_value_object(
        string declaration,
        string expected)
    {
        var run = GeneratorHarness.Run($"""
            [EntityId("acc")]
            {declaration}
            """);

        run.Ids.Should().Contain(expected);
    }

    /// <summary>
    /// Losing this breaks nothing visible while making every keystroke in the IDE re-run the whole pipeline.
    /// </summary>
    [Fact]
    public void An_unrelated_edit_does_not_re_run_the_identifier_pipeline()
    {
        const string First = """
            [EntityId("acc")]
            public readonly partial struct AccountId;

            public static class Unrelated { public const int Answer = 41; }
            """;

        const string Second = """
            [EntityId("acc")]
            public readonly partial struct AccountId;

            public static class Unrelated { public const int Answer = 42; }
            """;

        var reasons = GeneratorHarness.RunTwice(First, Second, "EntityIds");

        reasons.Should().NotBeEmpty().And.AllSatisfy(reason =>
            reason.Should().BeOneOf(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged));
    }
}
