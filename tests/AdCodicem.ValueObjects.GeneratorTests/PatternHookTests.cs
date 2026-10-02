using System.Globalization;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// <c>IValueObjectPatternValidator</c>: a pattern a consumer declares as a <c>[GeneratedRegex]</c> property, which the
/// generated code runs where the <c>Pattern</c> option ran, and publishes as that option published.
/// </summary>
public sealed class PatternHookTests
{
    private const string Email = """
        [ValueObject<string>(MaxLength = 254)]
        public readonly partial struct Email : IValueObjectPatternValidator
        {
            [GeneratedRegex(@"^[^@\s]+@[^@\s]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
            public static partial Regex Pattern { get; }
        }
        """;

    /// <summary>
    /// The hook runs in the option's place, after the lengths, and rejects as the option did, so moving from one to
    /// the other changes nothing a caller can observe. Nothing is compiled at run time: no field holds a pattern.
    /// </summary>
    [Fact]
    public void The_pattern_runs_after_the_lengths_and_rejects_as_the_option_did()
    {
        var run = GeneratorHarness.Run(Email);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        var generated = run.SingleValueObject;
        generated.Should().NotContain("DeclaredPattern");

        var validate = generated[generated.IndexOf("Validate(in global::System.String value)", StringComparison.Ordinal)..];
        var length = validate.IndexOf("if (value.Length > 254)", StringComparison.Ordinal);
        var pattern = validate.IndexOf("if (!global::AdCodicem.ValueObjects.ValueObjectPattern.Of<global::Test.Email>().IsMatch(value))", StringComparison.Ordinal);
        length.Should().BePositive();
        pattern.Should().BeGreaterThan(length, "the lengths are checked first, as they were for the option");
        validate[pattern..].Should().Contain(
            "InvalidFormat(\"The value does not match the expected format.\")",
            "the code and the message are the option's");
    }

    /// <summary>
    /// The rule is declared once: the text of the <c>[GeneratedRegex]</c> is the schema's pattern, written as a
    /// literal when the type compiles, so describing the type builds no regular expression.
    /// </summary>
    [Fact]
    public void The_schema_publishes_the_text_of_the_generated_regex_as_a_literal()
    {
        var generated = GeneratorHarness.Run(Email).SingleValueObject;

        generated.Should().Contain("""Pattern = "^[^@\\s]+@[^@\\s]+$",""");
        generated.Should().NotContain("ValueObjectPattern.Of<global::Test.Email>().ToString()");
    }

    /// <summary>
    /// A pattern written without the attribute gives no text to read when the type compiles, so the schema asks the
    /// regular expression for it, and nothing can be said about its options or its timeout.
    /// </summary>
    [Fact]
    public void A_pattern_without_the_attribute_is_asked_for_its_text_at_run_time()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code : IValueObjectPatternValidator
            {
                public static Regex Pattern => Shapes.Code;
            }

            public static partial class Shapes
            {
                [GeneratedRegex("^[A-Z]+$", RegexOptions.IgnoreCase)]
                public static partial Regex Code { get; }
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("Pattern = global::AdCodicem.ValueObjects.ValueObjectPattern.Of<global::Test.Code>().ToString(),");
    }

    /// <summary>
    /// The interface allows an explicit implementation, which the type's own name does not reach. The generated code
    /// goes through a type parameter instead, which reaches it, so it compiles either way.
    /// </summary>
    [Fact]
    public void A_pattern_implemented_explicitly_is_reached()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code : IValueObjectPatternValidator
            {
                static Regex IValueObjectPatternValidator.Pattern => Shapes.Code;
            }

            public static partial class Shapes
            {
                [GeneratedRegex("^[A-Z]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
                public static partial Regex Code { get; }
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_pattern_declared_both_ways_is_reported_and_the_hook_wins()
    {
        var run = GeneratorHarness.Run("""
            #pragma warning disable VO0021 // The deprecated option is what this test declares.
            [ValueObject<string>(Pattern = "^[a-z]+$")]
            public readonly partial struct Code : IValueObjectPatternValidator
            {
                [GeneratedRegex("^[A-Z]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
                public static partial Regex Pattern { get; }
            }
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0022");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().StartWith(
            "'Code' sets the Pattern option and implements IValueObjectPatternValidator.");

        run.CompilationDiagnostics.Should().BeEmpty("the type still generates, so its uses do not fail as well");
        run.SingleValueObject.Should().NotContain("DeclaredPattern").And.Contain("""Pattern = "^[A-Z]+$",""");
    }

    [Fact]
    public void A_pattern_on_a_value_object_that_is_not_a_string_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            public readonly partial struct Floor : IValueObjectPatternValidator
            {
                [GeneratedRegex("^[0-9]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
                public static partial Regex Pattern { get; }
            }
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0023");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().StartWith(
            "'Floor' implements IValueObjectPatternValidator, but its underlying type 'int' is not a string");
        run.SingleValueObject.Should().NotContain(".Pattern");
    }

    [Fact]
    public void A_pattern_on_an_entity_identifier_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [EntityId("acc")]
            public readonly partial struct AccountId : IValueObjectPatternValidator
            {
                [GeneratedRegex("^acc_[0-9a-z]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
                public static partial Regex Pattern { get; }
            }
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0024");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().StartWith(
            "'AccountId' implements IValueObjectPatternValidator, but [EntityId] validates its format itself");
    }

    /// <summary>
    /// The published pattern is the regular expression's text, which carries no options, so a client checking a
    /// value against the document would disagree with the validation. Options that do not change what matches,
    /// such as CultureInvariant, are not reported.
    /// </summary>
    [Fact]
    public void Options_the_published_pattern_cannot_carry_are_reported_on_the_attribute()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code : IValueObjectPatternValidator
            {
                [GeneratedRegex("^[a-z]+$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
                public static partial Regex Pattern { get; }
            }
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0025");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().StartWith(
            "The [GeneratedRegex] behind 'Code.Pattern' sets IgnoreCase, Multiline, which the OpenAPI pattern cannot carry");
        run.Locate(diagnostic).Text.Should().StartWith("GeneratedRegex(");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("""[GeneratedRegex("^[A-Z]+$")]""")]
    [InlineData("""[GeneratedRegex("^[A-Z]+$", RegexOptions.CultureInvariant)]""")]
    [InlineData("""[GeneratedRegex("^[A-Z]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: -1)]""")]
    [InlineData("""[GeneratedRegex("^[A-Z]+$", RegexOptions.CultureInvariant, "en-US")]""")]
    public void A_pattern_that_can_match_without_a_timeout_is_reported(string attribute)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<string>]
            public readonly partial struct Code : IValueObjectPatternValidator
            {
                {{attribute}}
                public static partial Regex Pattern { get; }
            }
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0026");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().StartWith(
            "The [GeneratedRegex] behind 'Code.Pattern' sets no matchTimeoutMilliseconds");
    }

    /// <summary>
    /// The author's <c>Pattern</c> is a member they declared, so a known value cannot take its name either.
    /// </summary>
    [Fact]
    public void A_known_value_cannot_take_the_name_of_the_pattern()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue("Pattern", "P")]
            public readonly partial struct Code : IValueObjectPatternValidator
            {
                [GeneratedRegex("^[A-Z]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
                public static partial Regex Pattern { get; }
            }
            """);

        run.Ids.Should().Equal("VO0006");
    }
}
