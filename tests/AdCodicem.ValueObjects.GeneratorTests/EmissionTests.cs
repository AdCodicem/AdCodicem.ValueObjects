using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// What the generator emits for a well-formed value object.
/// </summary>
/// <remarks>
/// The unit tests exercise generated code by using it, which proves it behaves but not that it exists: a member
/// silently not emitted looks identical to one that was never asked for. These tests read the output.
/// </remarks>
public sealed class EmissionTests
{
    [Fact]
    public void A_string_value_object_compiles_with_no_diagnostics()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.Files.Should().HaveCount(2, "the value object and the assembly registration");
    }

    [Fact]
    public void The_struct_holds_exactly_its_underlying_value()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        // The whole memory argument for a struct value object rests on this single field.
        run.SingleValueObject.Should().Contain("private readonly global::System.String? _value;");
    }

    [Fact]
    public void The_contract_members_are_all_emitted()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        var generated = run.SingleValueObject;

        foreach (var member in new[]
        {
            "public static global::Test.Code Create(",
            "public static bool TryCreate(",
            "public static global::Test.Code CreateUnchecked(",
            "public static global::Test.Code Parse(",
            "public static bool TryParse(",
            "public bool Equals(global::Test.Code",
            "public override int GetHashCode()",
            "public int CompareTo(global::Test.Code",
            "public bool TryFormat(",
            "public override string ToString()",
            "public bool IsDefault",
        })
        {
            generated.Should().Contain(member);
        }
    }

    [Fact]
    public void A_value_object_serializes_as_its_underlying_value()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        var generated = run.SingleValueObject;

        // Writing the bare value, rather than an object wrapper, is the point of the whole library.
        generated.Should().Contain("writer.WriteStringValue(value.Value);");
        generated.Should().Contain("public sealed class ValueJsonConverter");
    }

    [Fact]
    public void Arithmetic_is_emitted_only_when_asked_for()
    {
        var without = GeneratorHarness.Run("""
            [ValueObject<decimal>]
            public readonly partial struct Money;
            """);

        var with = GeneratorHarness.Run("""
            [ValueObject<decimal>(Arithmetic = true)]
            public readonly partial struct Money;
            """);

        without.SingleValueObject.Should().NotContain("operator +");
        with.SingleValueObject.Should().Contain("operator +");
        with.SingleValueObject.Should().Contain("public static global::Test.Money Zero");
    }

    [Fact]
    public void Integral_arithmetic_is_checked_so_an_overflow_throws_rather_than_wraps()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>(Arithmetic = true)]
            public readonly partial struct Count;
            """);

        run.SingleValueObject.Should().Contain("checked(");
    }

    [Fact]
    public void A_closed_value_set_emits_named_constants_and_a_frozen_lookup()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            [KnownValue("France", "FR")]
            [KnownValue("Belgium", "BE")]
            public readonly partial struct Country;
            """);

        var generated = run.SingleValueObject;

        run.CompilationDiagnostics.Should().BeEmpty();
        generated.Should().Contain("public static global::Test.Country France { get; } = Create(\"FR\");");
        generated.Should().Contain("public static global::Test.Country Belgium { get; } = Create(\"BE\");");
        generated.Should().Contain("FrozenSet");
    }

    [Fact]
    public void A_nested_value_object_reopens_every_containing_type()
    {
        var run = GeneratorHarness.Run("""
            public static partial class Outer
            {
                public static partial class Inner
                {
                    [ValueObject<string>]
                    public readonly partial struct Code;
                }
            }
            """);

        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("partial class Outer").And.Contain("partial class Inner");
    }

    /// <summary>
    /// A keyword is a legal name for a type or a namespace once escaped, so the generated code has to write it
    /// escaped wherever it names one: the namespace, the containing types, the value object, its constructor, and
    /// the documentation references, which only a project producing its documentation file resolves.
    /// </summary>
    [Fact]
    public void A_value_object_named_after_a_keyword_is_written_with_its_escape()
    {
        var run = GeneratorHarness.Run(
            """
            using AdCodicem.ValueObjects.Annotations;

            namespace @class.@namespace;

            public static partial class @static
            {
                [ValueObject<string>]
                public readonly partial struct @event;
            }
            """,
            DocumentationMode.Diagnose);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().OnlyContain(
            diagnostic => diagnostic.Id == "CS8981" && diagnostic.Location.SourceTree!.FilePath.Length == 0,
            "a lower-case type name is the author's to answer for, on their declaration and nowhere else");

        var generated = run.SingleValueObject;
        generated.Should().Contain("namespace @class.@namespace");
        generated.Should().Contain("partial class @static");
        generated.Should().Contain("partial struct @event : ");
        generated.Should().Contain("private @event(");
        generated.Should().Contain("cref=\"@event\"");
    }

    [Fact]
    public void Conversions_are_opt_in()
    {
        var without = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        var with = GeneratorHarness.Run("""
            [ValueObject<string>(ImplicitConversionToValue = true, ExplicitConversionFromValue = true)]
            public readonly partial struct Code;
            """);

        without.SingleValueObject.Should().NotContain("operator global::System.String");
        with.SingleValueObject.Should().Contain("implicit operator global::System.String");
        with.SingleValueObject.Should().Contain("explicit operator global::Test.Code");
    }

    /// <summary>
    /// The complement of the range diagnostics: the extremes of every integer type are bounds like any other.
    /// </summary>
    [Theory]
    [InlineData("sbyte", "-128", "127", "(sbyte)(127)")]
    [InlineData("byte", "0", "255", "(byte)(255)")]
    [InlineData("short", "-32768", "32767", "(short)(-32768)")]
    [InlineData("ushort", "0", "65535", "(ushort)(65535)")]
    [InlineData("int", "-2147483648", "2147483647", "(int)(-2147483648)")]
    [InlineData("uint", "0", "4294967295", "4294967295U")]
    [InlineData("long", "-9223372036854775808", "9223372036854775807", "-9223372036854775808L")]
    [InlineData("ulong", "0", "18446744073709551615", "18446744073709551615UL")]
    [InlineData(
        "Int128",
        "-170141183460469231731687303715884105728",
        "170141183460469231731687303715884105727",
        "global::System.Int128.Parse(\"170141183460469231731687303715884105727\", global::System.Globalization.CultureInfo.InvariantCulture)")]
    [InlineData(
        "UInt128",
        "0",
        "340282366920938463463374607431768211455",
        "global::System.UInt128.Parse(\"340282366920938463463374607431768211455\", global::System.Globalization.CultureInfo.InvariantCulture)")]
    [InlineData("ulong", "-0", "1", "(value < 0UL)")]
    [InlineData("int", " +7 ", "8", "(int)(7)")]
    public void An_integer_bound_at_the_extremes_of_its_type_compiles(
        string underlying,
        string minimum,
        string maximum,
        string literal)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Minimum = "{{minimum}}", Maximum = "{{maximum}}")]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(literal);
    }

    [Theory]
    [InlineData("double", "0.5", "1e3", "1000d")]
    [InlineData("double", "-1.7976931348623157E+308", "1.7976931348623157E+308", "1.7976931348623157E+308d")]
    [InlineData("float", "0.5", "2.5", "2.5f")]
    [InlineData("float", "-3.4028235E+38", "3.4028235E+38", "3.4028235E+38f")]
    public void A_finite_floating_point_bound_compiles(string underlying, string minimum, string maximum, string literal)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Minimum = "{{minimum}}", Maximum = "{{maximum}}")]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(literal);
    }

    /// <summary>
    /// The ticks a date and time bound compiles to are those written, whatever the time zone of the machine
    /// running the compiler.
    /// </summary>
    [Theory]
    [InlineData(
        "DateTime",
        "2020-01-01T08:30:00",
        "new global::System.DateTime(637134642000000000L, global::System.DateTimeKind.Unspecified)")]
    [InlineData(
        "DateTimeOffset",
        "2020-01-01T00:00:00+02:00",
        "new global::System.DateTimeOffset(637134336000000000L, new global::System.TimeSpan(72000000000L))")]
    [InlineData(
        "DateTimeOffset",
        "2020-01-01T00:00:00Z",
        "new global::System.DateTimeOffset(637134336000000000L, new global::System.TimeSpan(0L))")]
    public void A_date_and_time_bound_compiles_to_the_instant_written(string underlying, string bound, string literal)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Minimum = "{{bound}}")]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(literal);
    }

    /// <summary>
    /// C# ends a line at U+0085, U+2028 and U+2029 as well as at a line feed, and a regular string or character
    /// literal cannot hold any of them raw.
    /// </summary>
    [Fact]
    public void A_unicode_line_terminator_in_author_text_is_escaped_in_every_literal()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(Description = "One\u2028two", Example = "a\u0085b", Pattern = "^[^\u2029]+$")]
            [KnownValue("Separated", "a\u2028b\u2029c\u0085d")]
            public readonly partial struct Token;

            [ValueObject<char>]
            [KnownValue("LineSeparator", '\u2028')]
            [KnownValue("NextLine", "\u0085")]
            public readonly partial struct Separator;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        var token = run.Files.Single(file => file.HintName.Contains("Token", StringComparison.Ordinal)).Text;
        token.Should().Contain("Create(\"a\\u2028b\\u2029c\\u0085d\")");
        token.Should().Contain("Description = \"One\\u2028two\"");
        token.Should().Contain("Example = \"a\\u0085b\"");
        token.Should().Contain("\"^[^\\u2029]+$\"");

        var separator = run.Files.Single(file => file.HintName.Contains("Separator", StringComparison.Ordinal)).Text;
        separator.Should().Contain("Create('\\u2028')");
        separator.Should().Contain("Create('\\u0085')");
    }

    /// <summary>
    /// The description of a known value becomes a one-line documentation comment: a line break would end the
    /// comment and leave the rest of the text as code.
    /// </summary>
    [Fact]
    public void A_known_value_description_spanning_several_lines_is_folded_into_one_summary_line()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue("France", "FR", Description = "The French Republic,\nmainland\r\nand\u2028overseas\u0001<&>")]
            public readonly partial struct Country;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(
            "/// <summary>The French Republic, mainland and overseas &lt;&amp;&gt;</summary>");
    }

    [Fact]
    public void A_known_value_description_keeps_its_tabs_and_folds_every_other_line_break()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue("France", "FR", Description = "French\rRépublique\tFR\u0085mainland\u2029overseas\u20AC\r")]
            public readonly partial struct Country;
            """);

        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("/// <summary>French République\tFR mainland overseas€ </summary>");
    }

    /// <summary>
    /// The message of a violated bound quotes the bound as written, inside a string literal of the generated code,
    /// so the text has to be escaped for C#, not for XML.
    /// </summary>
    [Fact]
    public void A_bound_is_quoted_in_its_message_as_written()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<char>(Minimum = "\"", Maximum = "\\")]
            public readonly partial struct Quoted;

            [ValueObject<char>(Minimum = "<")]
            public readonly partial struct Angled;

            [ValueObject<int>(Minimum = "1\n")]
            public readonly partial struct Spaced;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        string Generated(string name) => run.Files.Single(file => file.HintName == $"Test.{name}.g.cs").Text;

        Generated("Quoted").Should()
            .Contain("""OutOfRange("The value must be greater than or equal to \".")""")
            .And.Contain("""OutOfRange("The value must be less than or equal to \\.")""");
        Generated("Angled").Should().Contain("""OutOfRange("The value must be greater than or equal to <.")""");
        Generated("Spaced").Should().Contain("""OutOfRange("The value must be greater than or equal to 1\n.")""");
    }

    [Theory]
    [InlineData("string")]
    [InlineData("int")]
    [InlineData("long")]
    [InlineData("decimal")]
    [InlineData("double")]
    [InlineData("Guid")]
    [InlineData("DateOnly")]
    [InlineData("TimeOnly")]
    [InlineData("DateTimeOffset")]
    [InlineData("TimeSpan")]
    [InlineData("bool")]
    [InlineData("char")]
    [InlineData("Int128")]
    public void Every_supported_underlying_type_produces_compiling_code(string underlying)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty("'{0}' must generate code that compiles", underlying);
    }
}
