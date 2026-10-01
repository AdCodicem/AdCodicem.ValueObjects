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

    [Fact]
    public void A_pattern_that_is_not_a_regular_expression_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(Pattern = "([unclosed")]
            public readonly partial struct Code;
            """);

        run.Ids.Should().Contain("VO0014");
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
