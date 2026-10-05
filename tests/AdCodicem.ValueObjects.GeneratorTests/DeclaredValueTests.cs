using System.Globalization;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// <c>VO0031</c>: a value the author declares on a value object — its example, a known value — that the type's own rules
/// refuse, reported at compile time wherever the generator can evaluate those rules on its own.
/// </summary>
/// <remarks>
/// A refused example is published as the OpenAPI example, which clients and mock servers take at its word, and a refused
/// known value or example throws from the type initializer, before <c>Main</c>. The generator evaluates a value the
/// compiler does: a constant passed to <c>Known</c>, or to <c>Create</c> in the getter of the example. What only runs at run
/// time — a value built by any other expression, a pattern, a validator, a bound computed by its hook, a normalization —
/// is the contract kit's to check.
/// </remarks>
public sealed class DeclaredValueTests
{
    [Fact]
    public void An_example_out_of_the_bounds_of_its_hooks_is_reported_where_it_is_written()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            public readonly partial struct Quantity : IValueObjectMinimum<int>, IValueObjectMaximum<int>, IValueObjectExample<Quantity>
            {
                public static int Minimum => 1;

                public static int Maximum => 100;

                public static Quantity Example => Create(5000);
            }
            """);

        run.Ids.Should().Equal("VO0031");
        var diagnostic = run.Diagnostics.Single();
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "The Example '5000' declared on 'Quantity' is refused by its own type (value_object.out_of_range): "
            + "The value must be less than or equal to 100.");
        run.Locate(diagnostic).Text.Should().Be("5000");
        run.Files.Should().Contain(file => file.HintName.Contains("Quantity", StringComparison.Ordinal), "the type still generates");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The example is read however its getter is written: an arrow, an accessor, a body returning it, an initializer, and
    /// an explicit implementation, which keeps it off the public surface of the type.
    /// </summary>
    [Theory]
    [InlineData("public static Quantity Example => Create(5000);")]
    [InlineData("public static Quantity Example { get => Create(5000); }")]
    [InlineData("public static Quantity Example { get { return Create(5000); } }")]
    [InlineData("public static Quantity Example { get; } = Create(5000);")]
    [InlineData("static Quantity IValueObjectExample<Quantity>.Example => Create(5000);")]
    public void An_example_created_from_a_constant_is_checked_however_its_getter_is_written(string example)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<int>]
            public readonly partial struct Quantity : IValueObjectMaximum<int>, IValueObjectExample<Quantity>
            {
                public static int Maximum => 100;

                {{example}}
            }
            """);

        run.Ids.Should().Equal("VO0031");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// An example built by any other expression runs only at run time: the type initializer creates it as the schema reads
    /// it, and the contract kit checks it. A known value returned as the example is checked as a known value.
    /// </summary>
    [Theory]
    [InlineData("public static Quantity Example => Create(int.Parse(\"5000\", System.Globalization.CultureInfo.InvariantCulture));")]
    [InlineData("public static Quantity Example => CreateUnchecked(5000);")]
    [InlineData("public static Quantity Example => Parse(\"5000\", null);")]
    [InlineData("public static Quantity Example => Ceiling;")]
    [InlineData("public static Quantity Example { get { var value = Create(5000); return value; } }")]
    public void An_example_the_compiler_does_not_evaluate_is_left_to_run_time(string example)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<int>]
            public readonly partial struct Quantity : IValueObjectMaximum<int>, IValueObjectExample<Quantity>
            {
                public static int Maximum => 100;

                [KnownValue]
                public static readonly Quantity Ceiling = Known(100);

                {{example}}
            }
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
            [ValueObject<string>({{option}})]
            public readonly partial struct Code : IValueObjectExample<Code>
            {
                public static Code Example => Create("{{example}}");
            }
            """);

        run.Ids.Should().Equal("VO0031");
        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"The Example '{example}' declared on 'Code' is refused by its own type ({code}): {rule}");
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
            [ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.{{comparison}})]
            public readonly partial struct Currency : IValueObjectExample<Currency>
            {
                [KnownValue]
                public static readonly Currency Euro = Known("EUR");

                [KnownValue]
                public static readonly Currency Dollar = Known("USD");

                public static Currency Example => Create("{{example}}");
            }
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
            [ValueObject<int>(ValueSet = ValueSetKind.Closed)]
            public readonly partial struct Tier : IValueObjectExample<Tier>
            {
                [KnownValue]
                public static readonly Tier Low = Known(1);

                [KnownValue]
                public static readonly Tier High = Known(10);

                public static Tier Example => Create({{example}});
            }
            """);

        run.Ids.Should().HaveCount(reported ? 1 : 0);
    }

    /// <summary>
    /// The membership the generator evaluates is the set of every known value, which it knows only when each is a
    /// constant: beside one built by another expression, an example is left to run time.
    /// </summary>
    [Fact]
    public void An_example_beside_a_known_value_the_compiler_does_not_evaluate_is_left_to_run_time()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>(ValueSet = ValueSetKind.Closed)]
            public readonly partial struct Tier : IValueObjectExample<Tier>
            {
                [KnownValue]
                public static readonly Tier Low = Known(1);

                [KnownValue]
                public static readonly Tier High = Known(int.Parse("10", System.Globalization.CultureInfo.InvariantCulture));

                public static Tier Example => Create(5);
            }
            """);

        run.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_known_value_its_type_refuses_is_reported_at_its_value()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed, MaxLength = 3)]
            public readonly partial struct Currency
            {
                [KnownValue]
                public static readonly Currency Eur = Known("EUR");

                [KnownValue]
                public static Currency Euro { get; } = Known("EURO");
            }
            """);

        run.Ids.Should().Equal("VO0031");
        var diagnostic = run.Diagnostics.Single();
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "The known value Euro 'EURO' declared on 'Currency' is refused by its own type (value_object.too_long): "
            + "The value must be at most 3 characters long.");
        run.Locate(diagnostic).Text.Should().Be("\"EURO\"");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "value_object.required", "The value must not be empty.")]
    [InlineData(", MinLength = 2", "value_object.required", "The value must not be empty.")]
    public void An_empty_known_value_is_reported_unless_the_type_allows_it(string options, string code, string rule)
    {
        var refused = GeneratorHarness.Run($$"""
            [ValueObject<string>(MaxLength = 3{{options}})]
            public readonly partial struct Code
            {
                [KnownValue]
                public static readonly Code None = Known("");
            }
            """);
        var allowed = GeneratorHarness.Run("""
            [ValueObject<string>(AllowEmpty = true)]
            public readonly partial struct Code
            {
                [KnownValue]
                public static readonly Code None = Known(string.Empty);
            }
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
            public readonly partial struct Quantity : IValueObjectMaximum<int>
            {
                [KnownValue]
                public static readonly Quantity Ceiling = Known(100);

                [KnownValue]
                public static readonly Quantity Overflow = Known(500);

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
            [ValueObject<{{underlying}}>]
            public readonly partial struct Quantity : IValueObjectMaximum<{{underlying}}>, IValueObjectExample<Quantity>
            {
                [KnownValue]
                public static readonly Quantity Overflow = Known(500);

                public static Quantity Example => Create(500);

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
            [ValueObject<int>]
            public readonly partial struct Quantity : IValueObjectMinimum<int>, IValueObjectExample<Quantity>
            {
                public static partial int Minimum { get; }

                public static partial int Minimum => 1000;

                public static Quantity Example => Create(500);
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
            [ValueObject<double>]
            public readonly partial struct Share : IValueObjectMaximum<double>
            {
                [KnownValue]
                public static readonly Share Above = Known(0.1000000015);

                [KnownValue]
                public static readonly Share Below = Known(0.10000000149);

                public static double Maximum => 0.1f;
            }

            [ValueObject<float>]
            public readonly partial struct Ratio : IValueObjectMaximum<float>, IValueObjectExample<Ratio>
            {
                public static float Maximum => 1.5f;

                public static Ratio Example => Create(1.6f);
            }
            """);

        run.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().BeEquivalentTo(
            "The known value Above '0.1000000015' declared on 'Share' is refused by its own type (value_object.out_of_range): "
            + "The value must be less than or equal to 0.10000000149011612.",
            "The Example '1.6' declared on 'Ratio' is refused by its own type (value_object.out_of_range): "
            + "The value must be less than or equal to 1.5.");
    }

    /// <summary>
    /// The argument of <c>Known</c> and <c>Create</c> is evaluated alone, the call not being bound, so a constant is read
    /// only where the conversion the call applies changes nothing the generator tells apart. A <c>float</c> widened to a
    /// <c>double</c> names another value than its text, a <c>char</c> converted to a number is no text of it, and a value
    /// built by a constructor is no constant: each is left to run time.
    /// </summary>
    [Fact]
    public void A_value_the_conversion_of_the_call_would_change_is_left_to_run_time()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<double>]
            public readonly partial struct Share : IValueObjectMaximum<double>
            {
                [KnownValue]
                public static readonly Share Widened = Known(0.1f);

                public static double Maximum => 0.1;
            }

            [ValueObject<int>]
            public readonly partial struct Code : IValueObjectMaximum<int>
            {
                [KnownValue]
                public static readonly Code Letter = Known('A');

                public static int Maximum => 10;
            }

            [ValueObject<DateOnly>]
            public readonly partial struct Day : IValueObjectMaximum<DateOnly>
            {
                [KnownValue]
                public static readonly Day Late = Known(new DateOnly(2100, 1, 1));

                public static DateOnly Maximum => new(2000, 1, 1);
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A normalization may turn a value the rules refuse into one they accept, so a type that normalizes is left to the
    /// type initializer and the contract kit.
    /// </summary>
    [Fact]
    public void A_type_that_normalizes_is_held_to_its_rules_only_at_run_time()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(MaxLength = 4)]
            public readonly partial struct Compact : IValueObjectNormalizer<string>, IValueObjectExample<Compact>
            {
                [KnownValue]
                public static readonly Compact Spaced = Known("a b c d e");

                public static Compact Example => Create("fr76 3000");

                public static string NormalizeValue(string value) => value.Replace(" ", string.Empty).Substring(0, 4);
            }

            [ValueObject<string>(MaxLength = 4)]
            public readonly partial struct SpanCompact : IValueObjectSpanNormalizer, IValueObjectExample<SpanCompact>
            {
                public static SpanCompact Example => Create("fr76 3000");

                public static string NormalizeValue(ReadOnlySpan<char> value) => value.Slice(0, 4).ToString();
            }

            [ValueObject<int>]
            public readonly partial struct Rounded : IValueObjectNormalizer<int>, IValueObjectMaximum<int>, IValueObjectExample<Rounded>
            {
                public static int Maximum => 10;

                public static Rounded Example => Create(50);

                public static int NormalizeValue(int value) => Math.Min(value, 10);
            }
            """);

        run.Diagnostics.Should().BeEmpty();
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

            [ValueObject<string>(MaxLength = 3)]
            public readonly partial struct Checked : IValueObjectValidator<string>, IValueObjectExample<Checked>
            {
                public static Checked Example => Create("ABCD");

                public static ValidationResult ValidateValue(in string value) => ValidationResult.Success;
            }

            [ValueObject<string>]
            public readonly partial struct Digits : IValueObjectPatternValidator, IValueObjectExample<Digits>
            {
                public static Digits Example => Create("abc");

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
            [EntityId("cus")]
            public readonly partial struct CustomerId : IValueObjectExample<CustomerId>
            {
                public static CustomerId Example => Create("not-an-identifier");
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
    }
}
