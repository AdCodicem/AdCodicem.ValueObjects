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
