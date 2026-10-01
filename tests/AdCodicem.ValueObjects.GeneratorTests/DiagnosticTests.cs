using System.Globalization;
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
    public void A_bound_that_does_not_parse_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>(Minimum = "not a number")]
            public readonly partial struct Count;
            """);

        run.Ids.Should().Contain("VO0004");
    }

    /// <summary>
    /// A bound that parses as some integer but not as the underlying one must be refused by the generator, or it
    /// turns into a literal the compiler rejects inside a file the author cannot edit.
    /// </summary>
    [Theory]
    [InlineData("sbyte", "128")]
    [InlineData("sbyte", "-129")]
    [InlineData("byte", "300")]
    [InlineData("byte", "-1")]
    [InlineData("short", "32768")]
    [InlineData("ushort", "65536")]
    [InlineData("ushort", "-1")]
    [InlineData("int", "2147483648")]
    [InlineData("uint", "4294967296")]
    [InlineData("uint", "-1")]
    [InlineData("long", "9223372036854775808")]
    [InlineData("long", "-9223372036854775809")]
    [InlineData("ulong", "-1")]
    [InlineData("ulong", "18446744073709551616")]
    [InlineData("Int128", "170141183460469231731687303715884105728")]
    [InlineData("Int128", "-170141183460469231731687303715884105729")]
    [InlineData("UInt128", "-1")]
    [InlineData("UInt128", "340282366920938463463374607431768211456")]
    public void A_bound_outside_the_range_of_its_underlying_type_is_reported(string underlying, string bound)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Maximum = "{{bound}}")]
            public readonly partial struct Wrapper;
            """);

        run.Ids.Should().Contain("VO0004");
        run.CompilationDiagnostics.Should().BeEmpty("a refused bound must not reach the generated code");
    }

    /// <summary>
    /// NaN and the infinities parse as a double or a float, and text past the largest one parses as an infinity,
    /// but none has a C# literal and none can bound anything.
    /// </summary>
    [Theory]
    [InlineData("double", "NaN")]
    [InlineData("double", "Infinity")]
    [InlineData("double", "-Infinity")]
    [InlineData("double", "1e400")]
    [InlineData("float", "NaN")]
    [InlineData("float", "-Infinity")]
    [InlineData("float", "1e39")]
    public void A_floating_point_bound_that_is_not_a_finite_number_is_reported(string underlying, string bound)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Minimum = "{{bound}}")]
            public readonly partial struct Wrapper;
            """);

        run.Ids.Should().Contain("VO0004");
        run.CompilationDiagnostics.Should().BeEmpty("a refused bound must not reach the generated code");
    }

    /// <summary>
    /// A bound must mean the same instant on every machine that compiles it. A DateTime reading with an offset
    /// would be converted to the build machine's time zone, and a DateTimeOffset without one would take its offset.
    /// </summary>
    [Theory]
    [InlineData("DateTime", "2020-01-01T00:00:00Z")]
    [InlineData("DateTime", "2020-01-01T00:00:00+00:00")]
    [InlineData("DateTime", "2020-01-01T00:00:00+02:00")]
    [InlineData("DateTimeOffset", "2020-01-01")]
    [InlineData("DateTimeOffset", "2020-01-01T00:00:00")]
    public void A_date_and_time_bound_that_would_depend_on_the_build_machine_is_reported(string underlying, string bound)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Minimum = "{{bound}}")]
            public readonly partial struct Wrapper;
            """);

        run.Ids.Should().Contain("VO0004");
    }

    [Theory]
    [InlineData("double", "double.NaN")]
    [InlineData("double", "double.PositiveInfinity")]
    [InlineData("float", "float.NaN")]
    [InlineData("float", "float.NegativeInfinity")]
    public void A_floating_point_known_value_that_is_not_a_finite_number_is_reported(string underlying, string value)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            [KnownValue("Unknown", {{value}})]
            public readonly partial struct Wrapper;
            """);

        run.Ids.Should().Contain("VO0013");
        run.CompilationDiagnostics.Should().BeEmpty("a refused known value must not reach the generated code");
    }

    [Theory]
    [InlineData("byte", "300")]
    [InlineData("sbyte", "-129")]
    [InlineData("ushort", "-1")]
    [InlineData("uint", "-1")]
    [InlineData("long", "9223372036854775808")]
    [InlineData("ulong", "-1")]
    [InlineData("UInt128", "-1")]
    public void A_known_value_outside_the_range_of_its_underlying_type_is_reported(string underlying, string value)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            [KnownValue("Edge", {{value}})]
            public readonly partial struct Wrapper;
            """);

        run.Ids.Should().Contain("VO0013");
        run.CompilationDiagnostics.Should().BeEmpty("a refused known value must not reach the generated code");
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
    public void A_known_value_whose_name_is_not_an_identifier_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            [KnownValue("not an identifier", "FR")]
            public readonly partial struct Country;
            """);

        run.Ids.Should().Contain("VO0006");
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
    /// The generated code reopens the value object and every type around it, which it cannot do for a type with
    /// type parameters or for an interface: what it wrote would not compile, in a file the author cannot edit. The
    /// declaration is reported instead, and nothing is generated for it, while the rest of the compilation still is.
    /// </summary>
    [Theory]
    [InlineData(
        """
        [ValueObject<string>]
        public readonly partial struct Code<T>;
        """,
        "public readonly partial struct Code<T>;",
        "is generic")]
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
            [ValueObject<string>]
            public readonly partial struct Code;
        }
        """,
        "public readonly partial struct Code;",
        "is nested in the generic type 'Outer<T>'")]
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
        public partial interface IOuter
        {
            [ValueObject<string>]
            public readonly partial struct Code;
        }
        """,
        "public readonly partial struct Code;",
        "is nested in the interface 'IOuter'")]
    [InlineData(
        """
        public partial interface IOuter<T>
        {
            [EntityId("acc")]
            public readonly partial struct Code;
        }
        """,
        "public readonly partial struct Code;",
        "is nested in the interface 'IOuter<T>'")]
    public void A_value_object_the_generated_code_cannot_reopen_is_reported_and_not_generated(
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
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"'Code' {reason}, which the generator does not support. Declare it without type parameters, either at "
            + "namespace level or nested in non-generic classes, structs and records.");

        run.Files.Select(file => file.HintName).Should().BeEquivalentTo("Test.Other.g.cs", "ValueObjectRegistration.g.cs");
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
            [KnownValue("First", "first")]
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

        run.Files.Select(file => file.HintName).Should().BeEquivalentTo("Test.Other.g.cs", "ValueObjectRegistration.g.cs");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Every_option_set_to_an_undefined_value_is_reported_along_with_the_other_mistakes()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(Comparison = (StringComparison)42, ValueSet = (ValueSetKind)5, Pattern = "([unclosed")]
            public readonly partial struct Code;
            """);

        run.Ids.Should().BeEquivalentTo("VO0020", "VO0020", "VO0014");
        run.Diagnostics.Where(diagnostic => diagnostic.Id == "VO0020")
            .Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))
            .Should().BeEquivalentTo(
                "'Code' sets Comparison to 42, which 'StringComparison' does not define. Use one of its named members.",
                "'Code' sets ValueSet to 5, which 'ValueSetKind' does not define. Use one of its named members.");
        run.Files.Should().BeEmpty();
    }

    [Fact]
    public void A_known_value_of_the_wrong_type_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>(ValueSet = ValueSetKind.Closed)]
            [KnownValue("Wrong", "not an int")]
            public readonly partial struct Code;
            """);

        run.Ids.Should().Contain("VO0013");
    }

    /// <summary>
    /// An array is a legal argument for the <c>object</c> parameter of <c>[KnownValue]</c>, and no underlying type
    /// is one. A generator that fails on it takes the generated code of every other value object down with it.
    /// </summary>
    [Fact]
    public void A_known_value_given_as_an_array_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            [KnownValue("Pair", new[] { 1, 2 })]
            public readonly partial struct Level;

            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        run.Ids.Should().Equal("VO0013");
        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Contain("{1, 2}");
        run.Files.Select(file => file.HintName).Should().Contain(["Test.Level.g.cs", "Test.Code.g.cs"]);
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A type and an enum member are legal arguments for the <c>object</c> parameter of <c>[KnownValue]</c>, and
    /// neither is a value of any underlying type. Read as text, <c>typeof(int)</c> became the string <c>"int"</c>
    /// and an enum member its number, without a word.
    /// </summary>
    [Theory]
    [InlineData("string", "typeof(int)", "typeof(int)")]
    [InlineData("string", "typeof(string[])", "typeof(string[])")]
    [InlineData("string", "DayOfWeek.Monday", "System.DayOfWeek.Monday")]
    [InlineData("int", "DayOfWeek.Monday", "System.DayOfWeek.Monday")]
    [InlineData("int", "(DayOfWeek)42", "42")]
    public void A_known_value_given_as_a_type_or_an_enum_member_is_reported(
        string underlying,
        string value,
        string quoted)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            [KnownValue("Named", {{value}})]
            [KnownValue("Kept", "1")]
            public readonly partial struct Wrapper;
            """);

        run.Ids.Should().Equal("VO0013");
        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Contain($"'{quoted}'");
        run.SingleValueObject.Should().Contain(" Kept ").And.NotContain(" Named ");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_pattern_that_is_not_a_regular_expression_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(Pattern = "([unclosed")]
            public readonly partial struct Code;
            """);

        run.Ids.Should().Contain("VO0014");
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
            [ValueObject<string>(MinLength = 2, MaxLength = 8, Pattern = "^[A-Z]+$")]
            public readonly partial struct Code;
            """);

        run.Diagnostics.Should().BeEmpty();
    }
}
