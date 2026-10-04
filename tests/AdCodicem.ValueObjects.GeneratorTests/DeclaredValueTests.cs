using System.Globalization;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// <c>VO0031</c>: a value the author declares on a value object — its example, a known value — that the type's own rules
/// refuse, reported at compile time wherever the generator can evaluate those rules on its own.
/// </summary>
/// <remarks>
/// A refused example is published as the OpenAPI example, which clients and mock servers take at its word, and a refused
/// known value throws from the type initializer, before <c>Main</c>. What only runs at run time — a pattern, a validator,
/// a bound computed by its hook, a normalization — is the contract kit's to check.
/// </remarks>
public sealed class DeclaredValueTests
{
    [Fact]
    public void An_example_out_of_the_bounds_of_its_hooks_is_reported_where_it_is_written()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>(Example = "5000")]
            public readonly partial struct Quantity : IValueObjectMinimum<int>, IValueObjectMaximum<int>
            {
                public static int Minimum => 1;

                public static int Maximum => 100;
            }
            """);

        run.Ids.Should().Equal("VO0031");
        var diagnostic = run.Diagnostics.Single();
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "The Example '5000' declared on 'Quantity' is refused by its own type (value_object.out_of_range): "
            + "The value must be less than or equal to 100.");
        run.Locate(diagnostic).Text.Should().Be("Example = \"5000\"");
        run.Files.Should().Contain(file => file.HintName.Contains("Quantity", StringComparison.Ordinal), "the type still generates");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("int", "lots", "The text is not a valid int.")]
    [InlineData("int", "1,000", "The text is not a valid int.")]
    [InlineData("sbyte", "128", "The text is not a valid sbyte.")]
    [InlineData("byte", "-1", "The text is not a valid byte.")]
    [InlineData("short", "40000", "The text is not a valid short.")]
    [InlineData("ushort", "70000", "The text is not a valid ushort.")]
    [InlineData("uint", "4294967296", "The text is not a valid uint.")]
    [InlineData("long", "9223372036854775808", "The text is not a valid long.")]
    [InlineData("ulong", "-5", "The text is not a valid ulong.")]
    [InlineData("Int128", "170141183460469231731687303715884105728", "The text is not a valid System.Int128.")]
    [InlineData("UInt128", "-1", "The text is not a valid System.UInt128.")]
    [InlineData("decimal", "12,5", "The text is not a valid decimal.")]
    [InlineData("double", "lots", "The text is not a valid double.")]
    [InlineData("double", "+-1", "The text is not a valid double.")]
    [InlineData("double", "-x", "The text is not a valid double.")]
    [InlineData("float", "+x", "The text is not a valid float.")]
    [InlineData("char", "ab", "The text is not a valid char.")]
    [InlineData("bool", "yes", "The text is not a valid bool.")]
    [InlineData("Guid", "not-a-guid", "The text is not a valid System.Guid.")]
    [InlineData("DateOnly", "2023-02-29", "The text is not a valid System.DateOnly.")]
    [InlineData("TimeOnly", "25:00", "The text is not a valid System.TimeOnly.")]
    [InlineData("DateTime", "yesterday", "The text is not a valid System.DateTime.")]
    [InlineData("DateTimeOffset", "tomorrow", "The text is not a valid System.DateTimeOffset.")]
    [InlineData("TimeSpan", "an hour", "The text is not a valid System.TimeSpan.")]
    public void An_example_no_form_of_its_type_reads_is_reported(string underlying, string example, string rule)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Example = "{{example}}")]
            public readonly partial struct Sample;
            """);

        run.Ids.Should().Equal("VO0031");
        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"The Example '{example}' declared on 'Sample' is refused by its own type (value_object.not_parsable): {rule}");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The generated <c>Parse</c> reads more forms than the one a known value is written in: white space, a sign,
    /// <c>NaN</c>, a date written another way, a date and time with an offset. Such an example is no mistake, and its
    /// rules are left to the contract kit, which parses it as the type does.
    /// </summary>
    [Theory]
    [InlineData("int", " 42")]
    [InlineData("Int128", "+5")]
    [InlineData("UInt128", " 5")]
    [InlineData("decimal", ".5")]
    [InlineData("double", "NaN")]
    [InlineData("float", "Infinity")]
    [InlineData("bool", " true")]
    [InlineData("Guid", " 6f9619ff-8b86-d011-b42d-00c04fc964ff ")]
    [InlineData("DateOnly", "01/31/2024")]
    [InlineData("TimeOnly", "8:30 AM")]
    [InlineData("DateTime", "2024-01-31T08:30:00Z")]
    [InlineData("DateTimeOffset", "2024-01-31T08:30:00")]
    [InlineData("TimeSpan", "1:30:00")]
    public void An_example_in_another_form_its_type_reads_is_left_to_run_time(string underlying, string example)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Example = "{{example}}")]
            public readonly partial struct Sample;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("MaxLength = 6", "definitely-not-a-code", "value_object.too_long", "The value must be at most 6 characters long.")]
    [InlineData("MinLength = 3", "ab", "value_object.too_short", "The value must be at least 3 characters long.")]
    public void An_example_of_a_length_its_type_refuses_is_reported(string option, string example, string code, string rule)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<string>({{option}}, Example = "{{example}}")]
            public readonly partial struct Code;
            """);

        run.Ids.Should().Equal("VO0031");
        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"The Example '{example}' declared on 'Code' is refused by its own type ({code}): {rule}");
    }

    /// <summary>
    /// A bound declared through the deprecated options is the text the generator already converts, and the message quotes
    /// it as written, as the generated one does. A <c>DateTimeOffset</c> compares the instant it names, whatever its offset.
    /// </summary>
    [Theory]
    [InlineData("DateOnly", "Minimum = \"2020-01-01\"", "2019-12-31", "The value must be greater than or equal to 2020-01-01.")]
    [InlineData("decimal", "Maximum = \"9.99\"", "10", "The value must be less than or equal to 9.99.")]
    [InlineData("DateTimeOffset", "Minimum = \"2024-01-01T00:00+00:00\"", "2024-01-01T01:00+02:00", "The value must be greater than or equal to 2024-01-01T00:00+00:00.")]
    [InlineData("TimeSpan", "Maximum = \"08:00:00\"", "1.00:00:00", "The value must be less than or equal to 08:00:00.")]
    [InlineData("char", "Minimum = \"b\"", "a", "The value must be greater than or equal to b.")]
    public void An_example_out_of_the_bounds_of_the_deprecated_options_is_reported(string underlying, string option, string example, string rule)
    {
        var run = GeneratorHarness.Run($$"""
            #pragma warning disable VO0028 // The deprecated option is what this test declares.
            [ValueObject<{{underlying}}>({{option}}, Example = "{{example}}")]
            public readonly partial struct Sample;
            #pragma warning restore VO0028
            """);

        run.Ids.Should().Equal("VO0031");
        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"The Example '{example}' declared on 'Sample' is refused by its own type (value_object.out_of_range): {rule}");
    }

    [Theory]
    [InlineData("DateOnly", "Minimum = \"2020-01-01\"", "2020-01-01")]
    [InlineData("DateTimeOffset", "Minimum = \"2024-01-01T00:00+00:00\"", "2024-01-01T02:00+02:00")]
    [InlineData("double", "Maximum = \"1.5\"", "1.5")]
    public void An_example_on_the_bound_of_its_type_is_accepted(string underlying, string option, string example)
    {
        var run = GeneratorHarness.Run($$"""
            #pragma warning disable VO0028 // The deprecated option is what this test declares.
            [ValueObject<{{underlying}}>({{option}}, Example = "{{example}}")]
            public readonly partial struct Sample;
            #pragma warning restore VO0028
            """);

        run.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Ordinal", "eur", true)]
    [InlineData("Ordinal", "EUR", false)]
    [InlineData("OrdinalIgnoreCase", "eur", false)]
    [InlineData("OrdinalIgnoreCase", "GBP", true)]
    [InlineData("InvariantCulture", "Eur", true)]
    [InlineData("InvariantCultureIgnoreCase", "eur", false)]
    [InlineData("CurrentCulture", "EUR", false)]
    [InlineData("CurrentCultureIgnoreCase", "GBP", false)]
    public void An_example_outside_a_closed_set_is_reported_under_the_comparison_of_its_type(string comparison, string example, bool reported)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.{{comparison}}, Example = "{{example}}")]
            [KnownValue("Euro", "EUR")]
            [KnownValue("Dollar", "USD")]
            public readonly partial struct Currency;
            """);

        if (reported)
        {
            run.Ids.Should().Equal("VO0031");
            run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().EndWith(
                "(value_object.not_a_known_value): The value is not one of the accepted values.");
        }
        else
        {
            run.Diagnostics.Should().BeEmpty("the current culture is the application's, which the build cannot stand for");
        }
    }

    [Theory]
    [InlineData("5", true)]
    [InlineData("10", false)]
    public void An_example_outside_a_closed_set_of_numbers_is_reported(string example, bool reported)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<int>(ValueSet = ValueSetKind.Closed, Example = "{{example}}")]
            [KnownValue("Low", 1)]
            [KnownValue("High", 10)]
            public readonly partial struct Tier;
            """);

        run.Ids.Should().HaveCount(reported ? 1 : 0);
    }

    [Fact]
    public void A_known_value_its_type_refuses_is_reported_on_its_attribute()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed, MaxLength = 3)]
            [KnownValue("Eur", "EUR")]
            [KnownValue("Euro", "EURO")]
            public readonly partial struct Currency;
            """);

        run.Ids.Should().Equal("VO0031");
        var diagnostic = run.Diagnostics.Single();
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "The known value Euro 'EURO' declared on 'Currency' is refused by its own type (value_object.too_long): "
            + "The value must be at most 3 characters long.");
        run.Locate(diagnostic).Text.Should().Be("KnownValue(\"Euro\", \"EURO\")");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "value_object.required", "The value must not be empty.")]
    [InlineData(", MinLength = 2", "value_object.required", "The value must not be empty.")]
    public void An_empty_known_value_is_reported_unless_the_type_allows_it(string options, string code, string rule)
    {
        var refused = GeneratorHarness.Run($$"""
            [ValueObject<string>(MaxLength = 3{{options}})]
            [KnownValue("None", "")]
            public readonly partial struct Code;
            """);
        var allowed = GeneratorHarness.Run("""
            [ValueObject<string>(AllowEmpty = true)]
            [KnownValue("None", "")]
            public readonly partial struct Code;
            """);

        refused.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"The known value None '' declared on 'Code' is refused by its own type ({code}): {rule}");
        allowed.Diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A bound returned as a constant is evaluated however the getter is written, through an explicit implementation too,
    /// and from a constant field. Its text is the constant's invariant form, as the generated message quotes it.
    /// </summary>
    [Theory]
    [InlineData("public static int Maximum => 100;")]
    [InlineData("public static int Maximum { get => 100; }")]
    [InlineData("public static int Maximum { get { return 100; } }")]
    [InlineData("static int IValueObjectMaximum<int>.Maximum => 100;")]
    [InlineData("private const int Limit = 100; public static int Maximum => Limit;")]
    public void A_known_value_out_of_a_bound_returned_as_a_constant_is_reported(string bound)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<int>]
            [KnownValue("Ceiling", 100)]
            [KnownValue("Overflow", 500)]
            public readonly partial struct Quantity : IValueObjectMaximum<int>
            {
                {{bound}}
            }
            """);

        run.Ids.Should().Equal("VO0031");
        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "The known value Overflow '500' declared on 'Quantity' is refused by its own type (value_object.out_of_range): "
            + "The value must be less than or equal to 100.");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// Any other getter runs only at run time, a body of more than a return included, and so does an initialized
    /// property, which a static constructor may assign again. A constant the form of the type has no text for, an
    /// infinity, bounds nothing a value can break. A hook the type does not implement, or implements with a getter that
    /// returns nothing, or with an accessor list beside an expression body, whose arrow the compiler never binds, leaves
    /// the type uncompiled, and its bound unread.
    /// </summary>
    [Theory]
    [InlineData("int", "public static int Maximum { get; } = 100;", "")]
    [InlineData("int", "public static int Maximum => int.Parse(\"100\", System.Globalization.CultureInfo.InvariantCulture);", "")]
    [InlineData("int", "public static int Maximum { get { const int Limit = 100; return Limit; } }", "")]
    [InlineData("int", "public static int Maximum { get { throw new NotSupportedException(); } }", "")]
    [InlineData("double", "public static double Maximum => double.PositiveInfinity;", "")]
    [InlineData("int", "", "CS0535")]
    [InlineData("int", "public static int Maximum { get { return; } }", "CS0126")]
    [InlineData("int", "public static int Maximum { set { } }", "CS0535")]
    [InlineData("int", "public static int Maximum { get; } => 100;", "CS8057")]
    public void A_known_value_out_of_a_bound_computed_at_run_time_is_left_to_run_time(string underlying, string bound, string compilerError)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Example = "500")]
            [KnownValue("Overflow", 500)]
            public readonly partial struct Quantity : IValueObjectMaximum<{{underlying}}>
            {
                {{bound}}
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Select(diagnostic => diagnostic.Id).Should().Equal(
            compilerError.Length == 0 ? [] : [compilerError]);
    }

    /// <summary>
    /// A partial property has two declarations, the one without a body first: the bound is read off the other.
    /// </summary>
    [Fact]
    public void A_bound_declared_as_a_partial_property_is_read_off_its_implementation()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>(Example = "500")]
            public readonly partial struct Quantity : IValueObjectMinimum<int>
            {
                public static partial int Minimum { get; }

                public static partial int Minimum => 1000;
            }
            """);

        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().EndWith(
            "(value_object.out_of_range): The value must be greater than or equal to 1000.");
    }

    /// <summary>
    /// The bound is the constant the getter returns, converted to the type of the property: <c>=&gt; 0.1f</c> bounds a
    /// <c>double</c> with the float it names, slightly above 0.1, as the generated check reads it.
    /// </summary>
    [Fact]
    public void A_bound_converted_by_its_getter_is_compared_as_converted()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<double>(Example = "0.10000000149")]
            [KnownValue("Above", 0.1000000015)]
            public readonly partial struct Share : IValueObjectMaximum<double>
            {
                public static double Maximum => 0.1f;
            }

            [ValueObject<float>(Example = "1.6")]
            public readonly partial struct Ratio : IValueObjectMaximum<float>
            {
                public static float Maximum => 1.5f;
            }
            """);

        run.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().BeEquivalentTo(
            "The known value Above '0.1000000015' declared on 'Share' is refused by its own type (value_object.out_of_range): "
            + "The value must be less than or equal to 0.10000000149011612.",
            "The Example '1.6' declared on 'Ratio' is refused by its own type (value_object.out_of_range): "
            + "The value must be less than or equal to 1.5.");
    }

    /// <summary>
    /// A normalization may turn a value the rules refuse into one they accept, so a type that normalizes is left to the
    /// contract kit. Text no form of the type reads is refused before any normalization, and stays reported.
    /// </summary>
    [Fact]
    public void A_type_that_normalizes_is_held_to_its_rules_only_at_run_time()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(MaxLength = 4, Example = "fr76 3000")]
            [KnownValue("Spaced", "a b c d e")]
            public readonly partial struct Compact : IValueObjectNormalizer<string>
            {
                public static string NormalizeValue(string value) => value.Replace(" ", string.Empty).Substring(0, 4);
            }

            [ValueObject<string>(MaxLength = 4, Example = "fr76 3000")]
            public readonly partial struct SpanCompact : IValueObjectSpanNormalizer
            {
                public static string NormalizeValue(ReadOnlySpan<char> value) => value.Slice(0, 4).ToString();
            }

            [ValueObject<int>(Example = "lots")]
            public readonly partial struct Rounded : IValueObjectNormalizer<int>, IValueObjectMaximum<int>
            {
                public static int Maximum => 10;

                public static int NormalizeValue(int value) => Math.Min(value, 10);
            }
            """);

        run.Ids.Should().Equal("VO0031");
        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Contain("'Rounded'").And.Contain("value_object.not_parsable");
    }

    /// <summary>
    /// A validator only adds refusals, so a rule broken here is broken whatever it says. A pattern runs the author's
    /// regular expression, which the generator leaves to run time.
    /// </summary>
    [Fact]
    public void A_validator_does_not_save_a_refused_example_and_a_pattern_is_left_to_run_time()
    {
        var run = GeneratorHarness.Run("""
            using System.Text.RegularExpressions;

            [ValueObject<string>(MaxLength = 3, Example = "ABCD")]
            public readonly partial struct Checked : IValueObjectValidator<string>
            {
                public static ValidationResult ValidateValue(in string value) => ValidationResult.Success;
            }

            [ValueObject<string>(Example = "abc")]
            public readonly partial struct Digits : IValueObjectPatternValidator
            {
                [GeneratedRegex("^[0-9]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
                public static partial Regex Pattern { get; }
            }
            """);

        run.Ids.Should().Equal("VO0031");
        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Contain("'Checked'");
    }

    /// <summary>
    /// An identifier normalizes and validates its own format, which the generator writes but does not run: its example
    /// is the contract kit's to check.
    /// </summary>
    [Fact]
    public void The_example_of_an_entity_identifier_is_left_to_run_time()
    {
        var run = GeneratorHarness.Run("""
            [EntityId("cus", Example = "not-an-identifier")]
            public readonly partial struct CustomerId;
            """);

        run.Diagnostics.Should().BeEmpty();
    }
}
