using System.Globalization;
using AdCodicem.ValueObjects.Generators.Internal;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// The diagnostics the generator reports, and the mistakes that must produce them.
/// </summary>
/// <remarks>
/// A generator that stays silent on bad input is worse than one that fails: the author gets a type missing half
/// its members and no explanation. Each of these covers a mistake someone will actually make.
/// </remarks>
public sealed class DiagnosticTests
{
    [Fact]
    public void A_value_object_that_is_not_partial_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly struct Code;
            """);

        run.Ids.Should().Contain("VO0001");
    }

    [Fact]
    public void A_class_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public partial class Code;
            """);

        run.Ids.Should().Contain("VO0002");
    }

    [Fact]
    public void A_record_struct_is_reported_because_with_would_bypass_validation()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial record struct Code;
            """);

        run.Ids.Should().Contain("VO0002");
    }

    [Fact]
    public void A_mutable_struct_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public partial struct Code;
            """);

        run.Ids.Should().Contain("VO0002");
    }

    /// <summary>
    /// A ref struct can be neither boxed nor a type argument, and the generated code makes it both: the type
    /// implements <c>IValueObject&lt;TSelf, TValue&gt;</c> over itself and registers a descriptor that boxes it.
    /// </summary>
    [Fact]
    public void A_ref_struct_is_reported_instead_of_generating_code_that_cannot_compile()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly ref partial struct Code;
            """);

        run.Ids.Should().Equal("VO0002");
        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Contain("ref struct");
        run.Files.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void An_unsupported_underlying_type_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<System.Uri>]
            public readonly partial struct Address;
            """);

        run.Ids.Should().Contain("VO0003");
    }

    [Fact]
    public void A_closed_set_with_no_known_value_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            public readonly partial struct Country;
            """);

        run.Ids.Should().Contain("VO0005");
    }

    [Fact]
    public void Arithmetic_on_a_non_numeric_type_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(Arithmetic = true)]
            public readonly partial struct Code;
            """);

        run.Ids.Should().Contain("VO0007");
    }

    [Fact]
    public void Length_constraints_on_a_non_string_are_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>(MaxLength = 10)]
            public readonly partial struct Count;
            """);

        run.Ids.Should().Contain("VO0008");
    }

    [Fact]
    public void A_containing_type_that_is_not_partial_is_reported()
    {
        var run = GeneratorHarness.Run("""
            public static class Outer
            {
                [ValueObject<string>]
                public readonly partial struct Code;
            }
            """);

        run.Ids.Should().Contain("VO0009");
    }

    /// <summary>
    /// The prefix of an entity identifier names one type. A generic identifier, or one nested in a generic type, would
    /// claim it for every construction, so an identifier of one construction would parse as another. The declaration is
    /// reported instead, and nothing is generated for it, while the rest of the compilation still is.
    /// </summary>
    [Theory]
    [InlineData(
        """
        [EntityId("acc")]
        public readonly partial struct Code<TKey, TValue>;
        """,
        "public readonly partial struct Code<TKey, TValue>;",
        "is generic")]
    [InlineData(
        """
        public partial class Outer<T>
        {
            public partial record Middle
            {
                [EntityId("acc")]
                public readonly partial struct Code;
            }
        }
        """,
        "public readonly partial struct Code;",
        "is nested in the generic type 'Outer<T>'")]
    [InlineData(
        """
        public partial interface IOuter<T>
        {
            [EntityId("acc")]
            public readonly partial struct Code;
        }
        """,
        "public readonly partial struct Code;",
        "is nested in the generic type 'IOuter<T>'")]
    public void A_generic_entity_identifier_is_reported_and_not_generated(string declaration, string line, string reason)
    {
        var run = GeneratorHarness.Run($"""
            {declaration}

            [ValueObject<string>]
            public readonly partial struct Other;
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0019");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        run.Locate(diagnostic).Should().Be(("Code", line));
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"'Code' {reason}, which the generator does not support. Declare it without type parameters, outside any "
            + "generic type: its prefix identifies one type, and every construction of a generic identifier would claim "
            + "the same one.");

        run.Files.Select(file => file.HintName).Should().BeEquivalentTo(HintNames.For("Test.Other"), "ValueObjectRegistration.g.cs");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A file-local type is out of reach of the generated file, which would reopen another type of the same name and
    /// leave the author's empty. A private or protected type is reached through a step of the registration written on
    /// each type around it, which the registration of the assembly can only call on a type it names without type
    /// arguments: one inside a generic type is out of its reach. Each is reported, and nothing is generated for it, while
    /// the rest of the compilation still is.
    /// </summary>
    [Theory]
    [InlineData(
        """
        [ValueObject<string>]
        file readonly partial struct Code;
        """,
        "file readonly partial struct Code;",
        "is file-local")]
    [InlineData(
        """
        file partial class Outer
        {
            [EntityId("acc")]
            public readonly partial struct Code;
        }
        """,
        "public readonly partial struct Code;",
        "is nested in the file-local type 'Outer'")]
    [InlineData(
        """
        public partial class Outer<T>
        {
            [ValueObject<string>]
            private readonly partial struct Code;
        }
        """,
        "private readonly partial struct Code;",
        "is private, inside the generic type 'Outer<T>'")]
    [InlineData(
        """
        public partial class Outer<T>
        {
            protected partial record Inner
            {
                [ValueObject<int>]
                internal readonly partial struct Code;
            }
        }
        """,
        "internal readonly partial struct Code;",
        "is nested in the protected type 'Inner', inside the generic type 'Outer<T>'")]
    [InlineData(
        """
        public partial class Outer<T>
        {
            [ValueObject<string>]
            private protected readonly partial struct Code;
        }
        """,
        "private protected readonly partial struct Code;",
        "is private protected, inside the generic type 'Outer<T>'")]
    public void A_value_object_the_generated_code_cannot_reach_is_reported_and_not_generated(
        string declaration,
        string line,
        string reason)
    {
        var run = GeneratorHarness.Run($"""
            {declaration}

            [ValueObject<string>]
            public readonly partial struct Other;
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0019");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        run.Locate(diagnostic).Should().Be(("Code", line));
        var remedy = reason.Contains("file-local", StringComparison.Ordinal)
            ? "Declare it, and every type around it, without the file modifier: the generated code reopens them in a "
              + "file of its own, where a file-local type is out of reach."
            : "Declare the private or protected type internal or public, or move it out of the generic type: the "
              + "registration reaches it from the type around it, which it cannot name without type arguments.";
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"'Code' {reason}, which the generator does not support. {remedy}");

        run.Files.Select(file => file.HintName).Should().BeEquivalentTo(HintNames.For("Test.Other"), "ValueObjectRegistration.g.cs");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// Internal and protected internal both reach the whole assembly, the generated registration included.
    /// </summary>
    [Theory]
    [InlineData("[ValueObject<string>]", "internal")]
    [InlineData("[ValueObject<string>]", "protected internal")]
    [InlineData("[EntityId(\"acc\")]", "protected internal")]
    public void A_value_object_the_assembly_can_reach_is_generated(string attribute, string accessibility)
    {
        var run = GeneratorHarness.Run($$"""
            internal partial class Outer
            {
                {{attribute}}
                {{accessibility}} readonly partial struct Code;
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.Files.Select(file => file.HintName).Should().BeEquivalentTo(HintNames.For("Test.Outer.Code"), "ValueObjectRegistration.g.cs");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A cast makes any number a constant of an enum type, so an option can hold a value its enum does not define.
    /// Generating the type with a default in its place would drop what the author wrote without a word: a closed
    /// value set would accept any value, and an identifier would take a granularity nobody chose.
    /// </summary>
    [Theory]
    [InlineData("[ValueObject<string>(Comparison = (StringComparison)42)]", "Comparison", "42", "StringComparison")]
    [InlineData("[ValueObject<string>(Comparison = (StringComparison)(-1))]", "Comparison", "-1", "StringComparison")]
    [InlineData("[ValueObject<string>(ValueSet = (ValueSetKind)5)]", "ValueSet", "5", "ValueSetKind")]
    [InlineData("[EntityId(\"acc\", Granularity = (IdGranularity)9)]", "Granularity", "9", "IdGranularity")]
    public void An_option_set_to_a_value_its_enum_does_not_define_is_reported_and_not_generated(
        string attribute,
        string option,
        string value,
        string enumName)
    {
        var run = GeneratorHarness.Run($"""
            {attribute}
            public readonly partial struct Code;

            [ValueObject<string>]
            public readonly partial struct Other;
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0020");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        run.Locate(diagnostic).Should().Be(("Code", "public readonly partial struct Code;"));
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"'Code' sets {option} to {value}, which '{enumName}' does not define. Use one of its named members.");

        run.Files.Select(file => file.HintName).Should().BeEquivalentTo(HintNames.For("Test.Other"), "ValueObjectRegistration.g.cs");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Every_option_set_to_an_undefined_value_is_reported_along_with_the_other_mistakes()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(Comparison = (StringComparison)42, ValueSet = (ValueSetKind)5, Arithmetic = true)]
            public readonly partial struct Code;
            """);

        run.Ids.Should().BeEquivalentTo("VO0020", "VO0020", "VO0007");
        run.Diagnostics.Where(diagnostic => diagnostic.Id == "VO0020")
            .Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))
            .Should().BeEquivalentTo(
                "'Code' sets Comparison to 42, which 'StringComparison' does not define. Use one of its named members.",
                "'Code' sets ValueSet to 5, which 'ValueSetKind' does not define. Use one of its named members.");
        run.Files.Should().BeEmpty();
    }

    /// <summary>
    /// A generator runs on the text as the author types it: an option holding a constant of the wrong type is the
    /// compiler's error to report on the declaration, and the generator reads the option as absent meanwhile.
    /// </summary>
    [Theory]
    [InlineData("int", "Arithmetic = 1", "operator +")]
    [InlineData("string", "MaxLength = \"3\"", "MaxLength =")]
    [InlineData("string", "Description = 3", "Description =")]
    public void An_option_holding_a_constant_of_the_wrong_type_is_read_as_absent(
        string underlying,
        string option,
        string absent)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>({{option}})]
            public readonly partial struct Code;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().ContainSingle()
            .Which.Location.SourceTree!.FilePath.Should().BeEmpty("the compiler reports the declaration, not the generated code");
        run.SingleValueObject.Should().NotContain(absent);
    }

    [Fact]
    public void A_well_formed_value_object_reports_nothing()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(MinLength = 2, MaxLength = 8)]
            public readonly partial struct Code : IValueObjectPatternValidator
            {
                [GeneratedRegex("^[A-Z]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
                public static partial Regex Pattern { get; }
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
    }
}
