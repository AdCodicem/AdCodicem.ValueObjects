using System.Globalization;
using AdCodicem.ValueObjects.Generators.Analyzers;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// The example a value object declares through <c>IValueObjectExample&lt;TSelf&gt;</c>, an instance of its own type the
/// schema publishes as the type writes it, and the forms the compiler accepts that would never be published.
/// </summary>
public sealed class ExampleHookTests
{
    [Theory]
    [InlineData("public static Percentage Example => Create(42);")]
    [InlineData("static Percentage IValueObjectExample<Percentage>.Example => Create(42);")]
    public void The_schema_reads_the_example_through_the_bridge_however_the_hook_is_implemented(string example)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<int>]
            public readonly partial struct Percentage : IValueObjectExample<Percentage>
            {
                {{example}}
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(
            "Example = global::AdCodicem.ValueObjects.ValueObjectExample.Of<global::Test.Percentage>().Value,");
    }

    [Fact]
    public void Without_the_hook_the_schema_publishes_no_example()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            public readonly partial struct Percentage;
            """);

        run.SingleValueObject.Should().NotContain("Example =");
    }

    /// <summary>
    /// While the hook is being written, the type lists the interface before it declares the member: the compiler reports
    /// the member missing, and the generator generates the type as it will be once it is there.
    /// </summary>
    [Fact]
    public void A_hook_whose_member_is_not_written_yet_is_left_to_the_compiler()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            public readonly partial struct Percentage : IValueObjectExample<Percentage>;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("CS0535");
        run.SingleValueObject.Should().Contain("ValueObjectExample.Of<global::Test.Percentage>().Value,");
    }

    [Fact]
    public void A_known_value_returned_as_the_example_is_published_as_any_other()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            public readonly partial struct Country : IValueObjectExample<Country>
            {
                [KnownValue]
                public static readonly Country France = Known("FR");

                public static Country Example => France;
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("ValueObjectExample.Of<global::Test.Country>().Value,");
    }

    [Fact]
    public void The_example_of_a_generic_value_object_is_read_off_each_construction()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Reference<TOwner> : IValueObjectExample<Reference<TOwner>>
            {
                public static Reference<TOwner> Example => Create("REF-1");
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("ValueObjectExample.Of<global::Test.Reference<TOwner>>().Value,");
    }

    /// <summary>
    /// The compiler accepts the interface over any type, and an example over another type would be declared and never
    /// published: the generator refuses it where the type is declared, and generates the type without it.
    /// </summary>
    [Fact]
    public void An_example_hook_over_another_type_is_reported_and_publishes_nothing()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            public readonly partial struct Percentage : IValueObjectExample<Ratio>
            {
                public static Ratio Example => Ratio.Create(0.5);
            }

            [ValueObject<double>]
            public readonly partial struct Ratio;
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0038");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        run.Locate(diagnostic).Text.Should().Be("Percentage");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "'Percentage' implements IValueObjectExample<Ratio>, but an example is an instance of the value object "
            + "itself: implement IValueObjectExample<Percentage> instead");
        run.CompilationDiagnostics.Should().BeEmpty();
        run.Files.Single(file => file.HintName.Contains("Percentage", StringComparison.Ordinal)).Text.Should().NotContain("Example =");
    }

    /// <summary>
    /// The example of an identifier is the author's when it implements the hook, and otherwise an identifier of the
    /// right shape the profile derives, so that regenerating a document produces the same bytes.
    /// </summary>
    [Theory]
    [InlineData(": IValueObjectExample<CustomerId> { public static CustomerId Example => Parse(\"cus_0000000000000000000000000\", null); }", "ValueObjectExample.Of<global::Test.CustomerId>().Value,")]
    [InlineData(";", "EntityIdFormat.Example(Prefix, Granularity),")]
    public void The_example_of_an_identifier_is_the_hooks_or_derived_from_its_profile(string body, string example)
    {
        var run = GeneratorHarness.Run($$"""
            [EntityId("cus")]
            public readonly partial struct CustomerId {{body}}
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(example).And.Contain("Example =", Exactly.Once());
    }

    [Theory]
    [InlineData("[ValueObject<int>(Example = \"42\")]", "public readonly partial struct Percentage;")]
    [InlineData("[EntityId(\"cus\", Example = \"cus_0000000000000000000000000\")]", "public readonly partial struct CustomerId;")]
    public void The_example_written_as_text_is_the_compilers_error(string attribute, string declaration)
    {
        var run = GeneratorHarness.Run($"""
            {attribute}
            {declaration}
            """);

        run.Diagnostics.Should().BeEmpty();
        var error = run.CompilationDiagnostics.Should().ContainSingle().Subject;
        error.Id.Should().Be("VO0035");
        error.Severity.Should().Be(DiagnosticSeverity.Error);
        run.SingleValueObject.Should().NotContain("Example = global::AdCodicem.ValueObjects.ValueObjectExample");
    }

    [Theory]
    [InlineData("[ValueObject<int>]")]
    [InlineData("[EntityId(\"pct\")]")]
    public async Task An_example_property_without_its_interface_is_reported(string attribute)
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<ValueObjectHookAnalyzer>($$"""
            {{attribute}}
            public readonly partial struct Percentage
            {
                public static Percentage Example => Parse("0", null);
            }
            """);

        var diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0011");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "'Example' looks like a value object rule but 'Percentage' does not implement 'IValueObjectExample<Percentage>'. "
            + "Declare the interface, or the rule will never run.");
    }

    /// <summary>
    /// Only a public static property of the value object's own type reads as a forgotten hook: a field could not
    /// implement it, and a property of another type, an instance one or a private one is something else.
    /// </summary>
    [Theory]
    [InlineData("public static readonly Percentage Example = Parse(\"0\", null);")]
    [InlineData("public static int Example => 42;")]
    [InlineData("public Percentage Example => this;")]
    [InlineData("private static Percentage Example => Parse(\"0\", null);")]
    public async Task Another_member_named_Example_is_left_alone(string member)
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<ValueObjectHookAnalyzer>($$"""
            [ValueObject<int>]
            public readonly partial struct Percentage
            {
                {{member}}
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task An_example_property_on_a_type_that_is_no_value_object_is_left_alone()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<ValueObjectHookAnalyzer>("""
            public readonly struct Percentage
            {
                public static Percentage Example => default;
            }
            """);

        diagnostics.Should().BeEmpty();
    }
}
