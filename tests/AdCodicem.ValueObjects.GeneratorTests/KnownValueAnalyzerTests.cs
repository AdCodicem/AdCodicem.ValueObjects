using System.Globalization;
using AdCodicem.ValueObjects.Generators.Analyzers;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// <c>VO0037</c>: <c>Known</c> called anywhere but in the initializer of a member marked <c>[KnownValue]</c>, where it
/// would create an instance a closed set refuses.
/// </summary>
public sealed class KnownValueAnalyzerTests
{
    [Fact]
    public async Task Known_in_the_initializer_of_a_known_value_is_left_alone()
    {
        var diagnostics = await Analyze("""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            public readonly partial struct Country
            {
                [KnownValue]
                public static readonly Country France = Known("FR");

                [KnownValue]
                public static Country Belgium { get; } = Known("BE");
            }

            [ValueObject<string>]
            public readonly partial struct Code<TOwner>
            {
                [KnownValue]
                public static readonly Code<TOwner> First = Known("first");
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("public static Country Make(string value) => Known(value);", "Known(value)")]
    [InlineData("public static readonly Country Other = Known(\"XX\");", "Known(\"XX\")")]
    [InlineData("[KnownValue] public static readonly Country Either = Flag ? Known(\"FR\") : Known(\"BE\");", "Known(\"FR\")")]
    [InlineData("public static readonly System.Func<string, Country> Factory = Known;", "Known")]
    [InlineData("public static Country Lazy => Known(\"FR\");", "Known(\"FR\")")]
    [InlineData("public static class Aliases { [KnownValue] public static readonly Country Gaul = Known(\"FR\"); }", "Known(\"FR\")")]
    public async Task Known_anywhere_else_is_reported(string member, string reported)
    {
        var diagnostics = await Analyze($$"""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            public readonly partial struct Country
            {
                public static readonly bool Flag = true;

                [KnownValue]
                public static readonly Country France = Known("FR");

                {{member}}
            }
            """);

        diagnostics.Should().NotBeEmpty().And.OnlyContain(found => found.Id == "VO0037");
        var diagnostic = diagnostics[0];
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan)
            .Should().Be(reported);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "'Country.Known' is called outside the initializer of a member marked [KnownValue]. It skips the membership "
            + "of a closed set, which only a known value satisfies by declaration: create the value through Create instead.");
    }

    /// <summary>
    /// A method of the author's named <c>Known</c> on a type that is no value object, or that does not return the type
    /// declaring it, is not the generated factory.
    /// </summary>
    [Fact]
    public async Task A_method_named_Known_that_is_not_the_factory_is_left_alone()
    {
        var diagnostics = await Analyze("""
            public static class Registry
            {
                public static string Known(string value) => value;

                public static readonly string First = Known("first");
            }

            [ValueObject<string>]
            public readonly partial struct Code
            {
                public static int Known(int value, int other) => value + other;

                public static readonly int Total = Known(1, 2);
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The analyzer ships inside the package, but nothing stops a project from loading it without the contracts: a
    /// method named <c>Known</c> is then just a method, since no type can be a value object.
    /// </summary>
    [Fact]
    public async Task The_analyzer_says_nothing_where_the_library_is_not_referenced()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<KnownValueAnalyzer>(
            """
            namespace Plain;

            public readonly struct Code
            {
                public static Code Known(string value) => new();

                public static readonly Code First = Known("first");
            }
            """,
            GeneratorHarness.FrameworkReferences);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// An attribute of the name of <c>[ValueObject&lt;T&gt;]</c>, declared where no <c>[KnownValue]</c> is, makes no
    /// known value declarable either: the analyzer leaves the type alone.
    /// </summary>
    [Fact]
    public async Task The_analyzer_says_nothing_where_no_known_value_can_be_declared()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<KnownValueAnalyzer>(
            """
            namespace AdCodicem.ValueObjects.Annotations
            {
                public sealed class ValueObjectAttribute<T> : System.Attribute;
            }

            namespace Plain
            {
                [AdCodicem.ValueObjects.Annotations.ValueObject<string>]
                public readonly struct Code
                {
                    public static Code Known(string value) => new();

                    public static readonly Code First = Known("first");
                }
            }
            """,
            GeneratorHarness.FrameworkReferences);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The compiler never hands an analyzer a null context, so the guard is only reached by a direct call, which it
    /// tolerates.
    /// </summary>
    [Fact]
    public void The_analyzer_initialized_without_a_context_does_nothing()
    {
        var analyzer = new KnownValueAnalyzer();

        analyzer.Invoking(target => target.Initialize(null!)).Should().NotThrow();
    }

    private static Task<System.Collections.Immutable.ImmutableArray<Diagnostic>> Analyze(string source)
        => GeneratorHarness.RunAnalyzerAsync<KnownValueAnalyzer>(source);
}
