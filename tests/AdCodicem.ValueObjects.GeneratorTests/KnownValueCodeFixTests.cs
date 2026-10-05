using AdCodicem.ValueObjects.CodeFixes;
using AdCodicem.ValueObjects.Generators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// The code fix that rewrites a known value declared on the type, <c>[KnownValue("France", "FR")]</c>, which is
/// <c>VO0034</c>, as the member that declares it now, <c>[KnownValue] public static readonly CountryCode France =
/// Known("FR");</c>.
/// </summary>
/// <remarks>
/// The value becomes an expression of the underlying type, read as the generator read it: a constant through its
/// invariant text, and text in the one form of its type (<see cref="LiteralFormTests"/>). The fixed declaration is then
/// compiled through the generator, which must accept it as written.
/// </remarks>
public sealed class KnownValueCodeFixTests
{
    [Theory]
    [InlineData("string", "\"FR\"", "\"FR\"")]
    [InlineData("Guid", "\"6F9619FF-8B86-D011-B42D-00C04FC964FF\"", "new Guid(\"6f9619ff-8b86-d011-b42d-00c04fc964ff\")")]
    [InlineData("bool", "true", "true")]
    [InlineData("bool", "\"false\"", "false")]
    [InlineData("char", "'\\''", "'\\''")]
    [InlineData("byte", "\"7\"", "7")]
    [InlineData("int", "0", "0")]
    [InlineData("long", "\"-42\"", "-42")]
    [InlineData("ulong", "18446744073709551615UL", "18446744073709551615")]
    [InlineData("UInt128", "7", "7")]
    [InlineData("decimal", "\"19.990\"", "19.990m")]
    [InlineData("double", "0.25", "0.25d")]
    [InlineData("float", "1.5f", "1.5f")]
    [InlineData("DateOnly", "\"2020-02-29\"", "new DateOnly(2020, 2, 29)")]
    [InlineData("TimeOnly", "\"08:30\"", "new TimeOnly(8, 30)")]
    [InlineData("TimeSpan", "\"1.00:00:00\"", "new TimeSpan(1, 0, 0, 0)")]
    [InlineData("DateTime", "\"2020-01-01\"", "new DateTime(2020, 1, 1)")]
    [InlineData("DateTimeOffset", "\"2020-01-01T00:00:00-05:00\"", "new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan(-5, 0, 0))")]
    public async Task A_known_value_of_every_underlying_type_becomes_a_member_initialized_with_an_expression_of_that_type(
        string underlying,
        string value,
        string expression)
    {
        var fixedSource = await FixAsync($$"""
            [ValueObject<{{underlying}}>]
            [KnownValue("Named", {{value}})]
            public readonly partial struct Wrapper;
            """);

        fixedSource.Should().Contain($$"""
            public readonly partial struct Wrapper
            {
                [KnownValue]
                public static readonly Wrapper Named = Known({{expression}});
            }
            """);
        ShouldCompile(fixedSource);
    }

    /// <summary>
    /// A double or a float given as a constant is read through its round-trip form, as the generator read it, so that
    /// the member holds the value the attribute held, digit for digit.
    /// </summary>
    [Theory]
    [InlineData("double", "1d / 3d", "0.3333333333333333d")]
    [InlineData("double", "0.1 + 0.2", "0.30000000000000004d")]
    [InlineData("float", "1f / 3f", "0.33333334f")]
    [InlineData("float", "1d / 3d", "0.33333334f")]
    public async Task A_floating_point_known_value_given_as_a_constant_keeps_every_digit_of_its_value(
        string underlying,
        string value,
        string expression)
    {
        var fixedSource = await FixAsync($$"""
            [ValueObject<{{underlying}}>]
            [KnownValue("Named", {{value}})]
            public readonly partial struct Wrapper;
            """);

        fixedSource.Should().Contain($"public static readonly Wrapper Named = Known({expression});");
        ShouldCompile(fixedSource);
    }

    /// <summary>
    /// A date, a time or a duration is written with the parts of it a constructor takes, as far as they are needed, and
    /// with its ticks where a part finer than a microsecond leaves no constructor but that one.
    /// </summary>
    [Theory]
    [InlineData("TimeOnly", "08:30:15", "new global::System.TimeOnly(8, 30, 15)")]
    [InlineData("TimeOnly", "08:30:15.25", "new global::System.TimeOnly(8, 30, 15, 250)")]
    [InlineData("TimeOnly", "08:30:00.000005", "new global::System.TimeOnly(8, 30, 0, 0, 5)")]
    [InlineData("TimeOnly", "08:30:00.0000001", "new global::System.TimeOnly(306000000001L)")]
    [InlineData("DateTime", "2024-01-31T08:30", "new global::System.DateTime(2024, 1, 31, 8, 30, 0)")]
    [InlineData("DateTime", "2024-01-31T08:30:00.0000001", "new global::System.DateTime(638422866000000001L, global::System.DateTimeKind.Unspecified)")]
    [InlineData("DateTimeOffset", "2024-01-31T08:30Z", "new global::System.DateTimeOffset(2024, 1, 31, 8, 30, 0, global::System.TimeSpan.Zero)")]
    [InlineData("DateTimeOffset", "2024-01-31T08:30:01.5-09:30", "new global::System.DateTimeOffset(2024, 1, 31, 8, 30, 1, 500, new global::System.TimeSpan(-9, -30, 0))")]
    [InlineData("DateTimeOffset", "2024-01-31T08:30:00.0000001Z", "new global::System.DateTimeOffset(638422866000000001L, new global::System.TimeSpan(0L))")]
    [InlineData("TimeSpan", "00:30:00", "new global::System.TimeSpan(0, 30, 0)")]
    [InlineData("TimeSpan", "-1.12:00:00.001", "-new global::System.TimeSpan(1, 12, 0, 0, 1)")]
    [InlineData("TimeSpan", "00:00:00.0000001", "new global::System.TimeSpan(1L)")]
    [InlineData("TimeSpan", "-10675199.02:48:05.4775808", "new global::System.TimeSpan(-9223372036854775808L)")]
    public void A_date_a_time_or_a_duration_is_written_with_the_parts_a_constructor_takes(string underlying, string text, string expression)
        => KnownValueExpression.Write(Resolve(underlying), text).Should().Be(expression);

    /// <summary>
    /// An integer wider than the widest literal C# has is written as the generator wrote it, from its two halves; any
    /// other converts from its digits, whatever the width of its type.
    /// </summary>
    [Theory]
    [InlineData("Int128", "-9223372036854775808", "-9223372036854775808")]
    [InlineData("UInt128", "18446744073709551615", "18446744073709551615")]
    [InlineData("UInt128", "18446744073709551616", "new global::System.UInt128(1UL, 0UL)")]
    [InlineData("sbyte", "-128", "-128")]
    public void An_integer_is_written_as_its_digits_while_a_literal_holds_them(string underlying, string text, string expression)
        => KnownValueExpression.Write(Resolve(underlying), text).Should().Be(expression);

    [Fact]
    public void A_value_that_does_not_read_as_its_type_has_no_expression()
    {
        KnownValueExpression.Write(Resolve("TimeOnly"), "25:00").Should().BeNull();
        KnownValueExpression.Write(Resolve("int"), 1.5).Should().BeNull();
        KnownValueExpression.Write(Resolve("string"), null).Should().BeNull();
    }

    /// <summary>
    /// One fix rewrites every known value declared on the type, at the top of its body, in the order they were written,
    /// which is the order <c>KnownValues</c> and the OpenAPI <c>enum</c> list them in. A description and any other named
    /// argument stay on the attribute, written as they were, and the name of the attribute is shortened as far as it can be.
    /// </summary>
    [Fact]
    public async Task Every_known_value_of_a_type_moves_into_its_body_in_the_order_it_was_written()
    {
        var fixedSource = await FixAsync("""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            [KnownValue("France", "FR", Description = "Metropolitan France.")]
            [KnownValueAttribute("Belgium", "BE")]
            [KnownValue("Luxembourg", "LU")]
            public readonly partial struct CountryCode : IValueObjectNormalizer<string>
            {
                public static string NormalizeValue(string value) => value.ToUpperInvariant();
            }
            """);

        fixedSource.Should().Contain("""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            public readonly partial struct CountryCode : IValueObjectNormalizer<string>
            {
                [KnownValue(Description = "Metropolitan France.")]
                public static readonly CountryCode France = Known("FR");

                [KnownValue]
                public static readonly CountryCode Belgium = Known("BE");

                [KnownValue]
                public static readonly CountryCode Luxembourg = Known("LU");

                public static string NormalizeValue(string value) => value.ToUpperInvariant();
            }
            """);
        ShouldCompile(fixedSource);
    }

    [Fact]
    public async Task An_attribute_written_beside_a_known_value_in_one_list_stays_on_the_type()
    {
        var fixedSource = await FixAsync("""
            [ValueObject<int>]
            [KnownValue("One", 1), Serializable]
            public readonly partial struct Counter;
            """);

        fixedSource.Should().Contain("""
            [ValueObject<int>]
            [Serializable]
            public readonly partial struct Counter
            """);
        fixedSource.Should().Contain("public static readonly Counter One = Known(1);");
    }

    /// <summary>
    /// A known value the generator refused, for a name that is no identifier a member can take or a value that does not
    /// read as its type, is left as it was written: no fix is offered for it, and the others are rewritten.
    /// </summary>
    [Fact]
    public async Task A_known_value_the_generator_refused_stays_as_it_was_written()
    {
        const string Source = """
            [ValueObject<TimeOnly>]
            [KnownValue("class", "08:00")]
            [KnownValue("Late", "25:00")]
            [KnownValue("Noon", "12:00")]
            public readonly partial struct Alarm;
            """;

        var fixedSource = await FixAsync(Source);

        fixedSource.Should().Contain("""[KnownValue("class", "08:00")]""")
            .And.Contain("""[KnownValue("Late", "25:00")]""")
            .And.Contain("public static readonly Alarm Noon = Known(new TimeOnly(12, 0));");
        (await TitlesAsync(Source, "[KnownValue(\"Late\"")).Should().BeEmpty();
        (await TitlesAsync(Source, "[KnownValue(\"Noon\"")).Should().Equal("Declare the known values of 'Alarm' as members");
    }

    /// <summary>
    /// What the generator never read as a known value is no more rewritten: an attribute whose name is no identifier a
    /// member can take, on a value object or not.
    /// </summary>
    [Theory]
    [InlineData("[ValueObject<string>]", "[KnownValue(null, \"FR\")]")]
    [InlineData("[ValueObject<string>]", "[KnownValue(\"France Nord\", \"FR\")]")]
    [InlineData("[ValueObject<object>]", "[KnownValue(\"France\", \"FR\")]")]
    [InlineData("[Serializable]", "[KnownValue(\"France\", \"FR\")]")]
    public async Task No_fix_is_offered_for_a_known_value_the_generator_never_read(string annotation, string attribute)
    {
        var source = $"""
            {annotation}
            {attribute}
            public readonly partial struct Region;
            """;

        (await TitlesAsync(source, attribute)).Should().BeEmpty();
        (await FixAsync(source)).Should().Contain(attribute);
    }

    /// <summary>
    /// The compiler reports the constructor that takes a name and a value wherever it is used, on a member as well as on
    /// the type, and in an expression: only an attribute on a type declares a known value the fix can move.
    /// </summary>
    [Fact]
    public async Task No_fix_is_offered_where_the_constructor_declares_no_known_value_of_the_type()
    {
        const string Source = """
            [ValueObject<string>]
            public readonly partial struct Region
            {
                [KnownValue("Brittany", "BZH")]
                public static readonly Region Brittany = Known("BZH");

                public static object Annotation() => new KnownValueAttribute("Alsace", "ALS");
            }
            """;

        (await TitlesAsync(Source, "[KnownValue(\"Brittany\"")).Should().BeEmpty();
        (await GeneratorHarness.OfferedFixTitlesAsync(
                new KnownValueMemberCodeFixProvider(),
                Source,
                Obsolete,
                text => new TextSpan(text.IndexOf("new KnownValueAttribute", StringComparison.Ordinal), "new KnownValueAttribute".Length)))
            .Should().BeEmpty();
        (await FixAsync(Source)).Should().Contain("[KnownValue(\"Brittany\", \"BZH\")]");
    }

    /// <summary>
    /// Another attribute taking two arguments stays on the type, as any other attribute does.
    /// </summary>
    [Fact]
    public async Task Another_attribute_of_two_arguments_stays_on_the_type()
    {
        var fixedSource = await FixAsync("""
            [ValueObject<int>]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0001")]
            [KnownValue("One", 1)]
            public readonly partial struct Counter;
            """);

        fixedSource.Should().Contain("""
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0001")]
            public readonly partial struct Counter
            """);
        fixedSource.Should().Contain("public static readonly Counter One = Known(1);");
        ShouldCompile(fixedSource);
    }

    [Fact]
    public async Task A_known_value_of_a_generic_value_object_is_a_member_of_its_own_type()
    {
        var fixedSource = await FixAsync("""
            [ValueObject<string>]
            [KnownValue("First", "1")]
            public readonly partial struct Code<TOwner>;
            """);

        fixedSource.Should().Contain("public static readonly Code<TOwner> First = Known(\"1\");");
        ShouldCompile(fixedSource);
    }

    [Fact]
    public async Task Fixing_all_in_a_document_rewrites_the_known_values_of_each_type()
    {
        var fixedSource = await GeneratorHarness.FixAllCompilerDiagnosticsAsync<KnownValueMemberCodeFixProvider>("""
            [ValueObject<string>]
            [KnownValue("Euro", "EUR")]
            [KnownValue("UsDollar", "USD")]
            public readonly partial struct CurrencyCode;

            [ValueObject<int>]
            [KnownValue("Low", 1)]
            public readonly partial struct Priority;
            """);

        fixedSource.Should().NotContain("[KnownValue(\"")
            .And.Contain("public static readonly CurrencyCode Euro = Known(\"EUR\");")
            .And.Contain("public static readonly CurrencyCode UsDollar = Known(\"USD\");")
            .And.Contain("public static readonly Priority Low = Known(1);");
        ShouldCompile(fixedSource);
    }

    [Fact]
    public void The_fix_answers_the_obsolete_constructor_and_fixes_all_of_a_document()
    {
        var fix = new KnownValueMemberCodeFixProvider();

        fix.FixableDiagnosticIds.Should().Equal("VO0034");
        fix.GetFixAllProvider().Should().NotBeNull();
    }

    private static Task<string> FixAsync(string source)
        => GeneratorHarness.FixCompilerDiagnosticsAsync<KnownValueMemberCodeFixProvider>(source);

    private static Task<IReadOnlyList<string>> TitlesAsync(string source, string attribute)
        => GeneratorHarness.OfferedFixTitlesAsync(
            new KnownValueMemberCodeFixProvider(),
            source,
            Obsolete,
            text => new TextSpan(text.IndexOf(attribute, StringComparison.Ordinal) + 1, attribute.Length - 1));

    // The compiler reports VO0034, through the [Obsolete] on the constructor: this descriptor only stands in for it, to
    // ask the fix what it offers at a location of the test's choosing, and belongs to no analyzer release.
#pragma warning disable RS2008
    private static readonly DiagnosticDescriptor Obsolete = new(
        KnownValueMemberCodeFixProvider.DiagnosticId,
        "Obsolete",
        "Obsolete",
        "Obsolete",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
#pragma warning restore RS2008

    private static void ShouldCompile(string source)
    {
        var run = GeneratorHarness.Run(source);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    private static UnderlyingType Resolve(string underlying)
    {
        var name = underlying switch
        {
            "string" => "String",
            "int" => "Int32",
            "sbyte" => "SByte",
            _ => underlying,
        };

        UnderlyingType.TryResolve("global::System." + name, out var resolved).Should().BeTrue();
        return resolved!;
    }
}
