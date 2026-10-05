using System.Globalization;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// The members a value object marks <c>[KnownValue]</c>, which it initializes through <c>Known</c>, and the ones
/// <c>VO0036</c> refuses.
/// </summary>
/// <remarks>
/// A known value is the author's member, so the compiler checks its name and the type of its value. What it cannot
/// check, the generator reports where the member is declared, with the rule it breaks, and generates the type from the
/// others, so that every use of it does not fail as well.
/// </remarks>
public sealed class KnownValueMemberTests
{
    [Theory]
    [InlineData("public readonly Country France = Known(\"FR\");", "it is not static")]
    [InlineData("public static Country France = Known(\"FR\");", "it can be written: the field is not readonly")]
    [InlineData("public static Country France { get; set; } = Known(\"FR\");", "it can be written: the property has a setter")]
    [InlineData("public static Country France { get; init; } = Known(\"FR\");", "it can be written: the property has a setter")]
    [InlineData("public static readonly Country? France = Known(\"FR\");", "it is of type 'Country?' rather than 'Country'")]
    [InlineData("public static readonly string France = \"FR\";", "it is of type 'string' rather than 'Country'")]
    [InlineData("public static readonly Country France = Create(\"FR\");", "it is not initialized through Known(...), the value as its one argument")]
    [InlineData("public static readonly Country France;", "it is not initialized through Known(...), the value as its one argument")]
    [InlineData("public static Country France => Known(\"FR\");", "it is not initialized through Known(...), the value as its one argument")]
    [InlineData("public static readonly Country France = Country.Known(\"FR\");", "it is not initialized through Known(...), the value as its one argument")]
    public void A_member_that_cannot_be_a_known_value_is_reported_and_the_others_are_generated(string member, string rule)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            public readonly partial struct Country
            {
                [KnownValue]
                {{member}}

                [KnownValue]
                public static readonly Country Belgium = Known("BE");
            }
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0036");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        run.Locate(diagnostic).Text.Should().Be("France");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"'France' is marked [KnownValue] on 'Country', but {rule}. Declare it as public static readonly Country France "
            + "= Known(...);, or as a static property with a getter alone initialized the same way.");
        run.SingleValueObject.Should().Contain("ImmutableArray.Create(Belgium);");
    }

    /// <summary>
    /// A closed set whose every known value was refused is not reported as declaring none besides, and generates as an
    /// open one, so that its uses still compile.
    /// </summary>
    [Fact]
    public void A_closed_set_whose_every_known_value_is_refused_generates_as_an_open_one()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            public readonly partial struct Country
            {
                [KnownValue]
                public static readonly Country France = Create("FR");
            }
            """);

        run.Ids.Should().Equal("VO0036");
        run.SingleValueObject.Should().NotContain("KnownUnderlyingValues").And.NotContain("KnownValues");
    }

    /// <summary>
    /// A known value on the type itself, the form that took a name and a value, is the compiler's error, VO0034, which
    /// a code fix rewrites. It declares nothing, so a closed set carrying it is not reported as declaring no value too.
    /// </summary>
    [Fact]
    public void A_known_value_declared_on_the_type_is_the_compilers_error_and_declares_nothing()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            [KnownValue("France", "FR")]
            public readonly partial struct Country;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("VO0034");
        run.SingleValueObject.Should().NotContain("France").And.NotContain("KnownUnderlyingValues");
    }

    /// <summary>
    /// A known value may be a field or a get-only auto-property, of any accessibility: the closed set accepts it, and the
    /// schema publishes it, whether the type exposes it or not.
    /// </summary>
    [Fact]
    public void A_known_value_may_be_a_field_or_a_property_of_any_accessibility()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>(ValueSet = ValueSetKind.Closed)]
            public readonly partial struct Level
            {
                [KnownValue]
                public static readonly Level Low = Known(1);

                [KnownValue]
                internal static Level Middle { get; } = Known(2);

                [KnownValue]
                private static readonly Level Legacy = Known(9);

                [KnownValue]
                public static Level High { get; } = Known(3);
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("{ Low.Value, Middle.Value, Legacy.Value, High.Value }")
            .And.Contain("ImmutableArray.Create(Low, Middle, Legacy, High);");
    }

    /// <summary>
    /// The known values come in the order the compiler lists the members: declaration order within a file, the other
    /// members of the type between them changing nothing, then the order of the partial declarations.
    /// </summary>
    [Fact]
    public void The_known_values_come_in_declaration_order_across_partial_declarations()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code
            {
                [KnownValue]
                public static readonly Code Second = Known("2");

                public static readonly int Unrelated = 0;

                [KnownValue]
                public static readonly Code First = Known("1");
            }

            public readonly partial struct Code
            {
                [KnownValue]
                public static readonly Code Third = Known("3");
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("ImmutableArray.Create(Second, First, Third);");
    }

    /// <summary>
    /// A contextual keyword is an ordinary identifier outside the construct that gives it meaning, and so is the
    /// discard: the generated code refers to such a member by its name.
    /// </summary>
    [Theory]
    [InlineData("var")]
    [InlineData("value")]
    [InlineData("record")]
    [InlineData("field")]
    [InlineData("_")]
    public void A_known_value_named_after_a_contextual_keyword_or_the_discard_is_generated(string name)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            public readonly partial struct Code
            {
                [KnownValue]
                public static readonly Code {{name}} = Known("K");
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain($"{{ {name}.Value }}");
    }

    [Fact]
    public void A_known_value_of_a_generic_value_object_is_generated_for_every_construction()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            public readonly partial struct Channel<TRecord>
            {
                [KnownValue(Description = "Sent to the address on file.")]
                public static readonly Channel<TRecord> Email = Known("email");
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("KnownValueInfo(Email.Value, \"Email\", \"Sent to the address on file.\")");
    }

    /// <summary>
    /// A field declaration may declare several fields, each its own known value, documented by the comment the
    /// declaration carries.
    /// </summary>
    [Fact]
    public void A_field_declaration_of_several_variables_declares_as_many_known_values()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            public readonly partial struct Level
            {
                /// <summary>A level of the scale.</summary>
                [KnownValue]
                public static readonly Level Low = Known(1), High = Known(3);
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("KnownValueInfo(Low.Value, \"Low\", \"A level of the scale.\")")
            .And.Contain("KnownValueInfo(High.Value, \"High\", \"A level of the scale.\")");
    }
}
