using AdCodicem.ValueObjects.Generators.Model;
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
    /// The generated code reopens each containing type with the keyword it was declared with: a record struct
    /// reopened as a struct, or a record as a class, is a different declaration the compiler refuses.
    /// </summary>
    [Theory]
    [InlineData("public partial record struct Outer", "partial record struct Outer")]
    [InlineData("public partial record Outer", "partial record Outer")]
    [InlineData("public readonly partial struct Outer", "partial struct Outer")]
    [InlineData("public static partial class Outer", "partial class Outer")]
    public void A_nested_value_object_reopens_its_container_as_declared(string container, string reopening)
    {
        var run = GeneratorHarness.Run($$"""
            {{container}}
            {
                [ValueObject<string>]
                public readonly partial struct Code;
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Split('\n').Select(line => line.Trim()).Should().Contain(reopening);
    }

    /// <summary>
    /// A combining mark is part of an identifier but no letter or digit, so it has no place in the name of the
    /// generated file and is replaced there, while the code keeps the name as declared.
    /// </summary>
    [Fact]
    public void A_name_holding_a_character_outside_the_file_name_alphabet_gets_a_hint_name_without_it()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Cafe\u0301Code;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.Files.Select(file => file.HintName).Should().BeEquivalentTo("Test.Cafe_Code.g.cs", "ValueObjectRegistration.g.cs");
        run.SingleValueObject.Should().Contain("partial struct Cafe\u0301Code : ");
        run.CompilationDiagnostics.Should().BeEmpty();
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
    [InlineData("long", "-0", "1", "(value < 0L)")]
    [InlineData("int", "007", "8", "(int)(7)")]
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
    /// XML admits neither U+FFFE nor U+FFFF, nor half of a surrogate pair, so a project producing its documentation
    /// file reports a summary holding one as malformed. A whole pair stands for one character and stays.
    /// </summary>
    [Fact]
    public void A_known_value_description_folds_the_characters_xml_cannot_hold_and_keeps_a_surrogate_pair()
    {
        var run = GeneratorHarness.Run(
            """
            [ValueObject<string>]
            [KnownValue("France", "FR", Description = "a\uFFFEb\uFFFFc\uD800d\uDC00e\uD83D\uDE00f\uDBFF")]
            public readonly partial struct Country;
            """,
            DocumentationMode.Diagnose);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("/// <summary>a b c d e\uD83D\uDE00f </summary>");
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

            [ValueObject<char>(Minimum = "\n")]
            public readonly partial struct Spaced;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        string Generated(string name) => run.Files.Single(file => file.HintName == $"Test.{name}.g.cs").Text;

        Generated("Quoted").Should()
            .Contain("""OutOfRange("The value must be greater than or equal to \".")""")
            .And.Contain("""OutOfRange("The value must be less than or equal to \\.")""");
        Generated("Angled").Should().Contain("""OutOfRange("The value must be greater than or equal to <.")""");
        Generated("Spaced").Should().Contain("""OutOfRange("The value must be greater than or equal to \n.")""");
    }

    [Fact]
    public void A_value_object_declared_outside_any_namespace_compiles()
    {
        var run = GeneratorHarness.Run("""
            using AdCodicem.ValueObjects.Annotations;
            using AdCodicem.ValueObjects.Identifiers;

            // In the global namespace: the harness wraps a snippet in a namespace only when its text never mentions one.
            [ValueObject<string>]
            public readonly partial struct GlobalCode;

            [EntityId("glb")]
            public readonly partial struct GlobalId;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.Files.Select(file => file.HintName)
            .Should().BeEquivalentTo("GlobalCode.g.cs", "GlobalId.g.cs", "ValueObjectRegistration.g.cs");
        run.Files.Where(file => file.HintName != "ValueObjectRegistration.g.cs")
            .Should().AllSatisfy(file => file.Text.Should().NotContain("namespace "));
    }

    /// <summary>
    /// A project referencing the JSON package serializes through a source-generated context, which cannot see the
    /// converters this generator writes, so each one is published to the package's registry at start-up.
    /// </summary>
    [Fact]
    public void With_the_json_package_referenced_every_converter_is_published()
    {
        const string source = """
            [ValueObject<string>]
            public readonly partial struct Code;

            [EntityId("acc")]
            public readonly partial struct AccountId;
            """;

        var with = GeneratorHarness.Run(source, referenceJsonPackage: true);
        var without = GeneratorHarness.Run(source);

        with.Diagnostics.Should().BeEmpty();
        with.CompilationDiagnostics.Should().BeEmpty("the calls bind to the package's Register<TSelf>");
        Registration(with).Should()
            .Contain("global::AdCodicem.ValueObjects.Json.ValueObjectJsonRegistry.Register(new global::Test.Code.ValueJsonConverter());")
            .And.Contain("global::AdCodicem.ValueObjects.Json.ValueObjectJsonRegistry.Register(new global::Test.AccountId.ValueJsonConverter());");
        Registration(without).Should().NotContain("ValueObjectJsonRegistry");

        static string Registration(GeneratorRun run)
            => run.Files.Single(file => file.HintName == "ValueObjectRegistration.g.cs").Text;
    }

    [Fact]
    public void The_summary_of_a_value_object_becomes_its_schema_description()
    {
        var run = GeneratorHarness.Run("""
            /// <summary>
            ///   An order reference,
            ///   as printed on the invoice.
            /// </summary>
            [ValueObject<string>]
            public readonly partial struct OrderReference;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("Description = \"An order reference, as printed on the invoice.\",");
    }

    /// <summary>
    /// A project that produces no documentation file compiles its comments as plain trivia, and most projects do:
    /// the summary is read from the trivia then.
    /// </summary>
    [Fact]
    public void The_summary_is_read_even_when_the_project_produces_no_documentation_file()
    {
        var run = GeneratorHarness.Run(
            """
            /// <summary>An order reference.</summary>
            [ValueObject<string>]
            public readonly partial struct OrderReference;
            """,
            DocumentationMode.None);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("Description = \"An order reference.\",");
    }

    [Theory]
    [InlineData("/// <remarks>Only remarks.</remarks>", DocumentationMode.Parse)]
    [InlineData("/// <remarks>Only remarks.</remarks>", DocumentationMode.None)]
    [InlineData("/// <summary>Never closed.", DocumentationMode.Parse)]
    [InlineData("/// <summary>Never closed.", DocumentationMode.None)]
    [InlineData("/// </summary>Closed first.<summary>", DocumentationMode.None)]
    [InlineData("/// <summary>   </summary>", DocumentationMode.Parse)]
    [InlineData("/// <summary>   </summary>", DocumentationMode.None)]
    public void A_doc_comment_without_a_usable_summary_publishes_no_description(string comment, DocumentationMode mode)
    {
        var run = GeneratorHarness.Run(
            $$"""
            {{comment}}
            [ValueObject<string>]
            public readonly partial struct OrderReference;
            """,
            mode);

        run.Diagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().NotContain("Description =");
    }

    [Fact]
    public void A_declared_description_wins_over_the_summary()
    {
        var run = GeneratorHarness.Run("""
            /// <summary>From the summary.</summary>
            [ValueObject<string>(Description = "From the attribute.")]
            public readonly partial struct OrderReference;
            """);

        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("Description = \"From the attribute.\",").And.NotContain("From the summary.");
    }

    [Fact]
    public void A_blank_text_option_is_treated_as_absent()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(Description = " ", Example = "", SchemaFormat = "\t")]
            public readonly partial struct Code;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().NotContain("Description =").And.NotContain("Example =").And.NotContain("Format =");
    }

    /// <summary>The supported underlying types, by the name the diagnostics give them.</summary>
    public static TheoryData<string> SupportedUnderlyingTypes => [.. UnderlyingType.SupportedNames];

    /// <summary>
    /// Every supported type compiles, and is written to JSON in the form its kind calls for: a number as a number,
    /// a narrow integer widened to one, and a 128-bit integer, a date or a time as text read back by a helper.
    /// </summary>
    [Theory]
    [MemberData(nameof(SupportedUnderlyingTypes))]
    public void Every_supported_underlying_type_produces_compiling_code(string underlying)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty("'{0}' must generate code that compiles", underlying);
        run.SingleValueObject.Should().Contain(JsonForms[underlying]);
    }

    /// <summary>The line of the JSON converter that sets each underlying type apart.</summary>
    private static readonly Dictionary<string, string> JsonForms = new(StringComparer.Ordinal)
    {
        ["string"] = "writer.WriteStringValue(value.Value);",
        ["System.Guid"] = "writer.WriteStringValue(value.Value);",
        ["bool"] = "writer.WriteBooleanValue(value.Value);",
        ["char"] = "buffer[0] = value.Value;",
        ["sbyte"] = "writer.WriteNumberValue((int)value.Value);",
        ["byte"] = "writer.WriteNumberValue((int)value.Value);",
        ["short"] = "writer.WriteNumberValue((int)value.Value);",
        ["ushort"] = "writer.WriteNumberValue((int)value.Value);",
        ["int"] = "writer.WriteNumberValue(value.Value);",
        ["uint"] = "writer.WriteNumberValue(value.Value);",
        ["long"] = "writer.WriteNumberValue(value.Value);",
        ["ulong"] = "writer.WriteNumberValue(value.Value);",
        ["System.Int128"] = "private static global::System.Int128 ReadInt128(ref",
        ["System.UInt128"] = "private static global::System.UInt128 ReadUInt128(ref",
        ["decimal"] = "writer.WriteNumberValue(value.Value);",
        ["double"] = "writer.WriteNumberValue(value.Value);",
        ["float"] = "writer.WriteNumberValue(value.Value);",
        ["System.DateOnly"] = "private static global::System.DateOnly ReadDateOnly(ref",
        ["System.TimeOnly"] = "private static global::System.TimeOnly ReadTimeOnly(ref",
        ["System.DateTime"] = "writer.WriteStringValue(value.Value);",
        ["System.DateTimeOffset"] = "writer.WriteStringValue(value.Value);",
        ["System.TimeSpan"] = "private static global::System.TimeSpan ReadTimeSpan(ref",
    };

    /// <summary>
    /// The narrow integers promote to int under arithmetic, so their results are cast back; every integral
    /// operation is checked; and an unsigned type has no negation, and is its own absolute value.
    /// </summary>
    [Theory]
    [InlineData("sbyte", "checked((sbyte)(left.Value + right.Value))", true)]
    [InlineData("byte", "checked((byte)(left.Value + right.Value))", false)]
    [InlineData("short", "checked((short)(left.Value + right.Value))", true)]
    [InlineData("ushort", "checked((ushort)(left.Value + right.Value))", false)]
    [InlineData("int", "checked(left.Value + right.Value)", true)]
    [InlineData("uint", "checked(left.Value + right.Value)", false)]
    [InlineData("long", "checked(left.Value + right.Value)", true)]
    [InlineData("ulong", "checked(left.Value + right.Value)", false)]
    [InlineData("Int128", "checked(left.Value + right.Value)", true)]
    [InlineData("UInt128", "checked(left.Value + right.Value)", false)]
    [InlineData("decimal", "left.Value + right.Value", true)]
    [InlineData("double", "left.Value + right.Value", true)]
    [InlineData("float", "left.Value + right.Value", true)]
    public void Arithmetic_compiles_for_every_numeric_underlying_type(string underlying, string sum, bool negatable)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Arithmetic = true)]
            public readonly partial struct Count;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty("arithmetic over '{0}' must compile", underlying);

        var generated = run.SingleValueObject;
        generated.Should().Contain($"operator +(global::Test.Count left, global::Test.Count right) => Create({sum});");
        generated.Contains("operator -(global::Test.Count value)", StringComparison.Ordinal)
            .Should().Be(negatable, "only a signed type is negated");
        generated.Contains("Abs(global::Test.Count value) => value;", StringComparison.Ordinal)
            .Should().Be(!negatable, "an unsigned value is its own absolute value");
    }
}
