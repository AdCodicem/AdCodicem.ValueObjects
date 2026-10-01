using System.Globalization;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// The names a known value may take, and the ones <c>VO0006</c> refuses.
/// </summary>
/// <remarks>
/// The first argument of <c>[KnownValue]</c> becomes a static property of the value object. A name the C# compiler
/// would not accept there must be reported by the generator, or the author is handed an error inside a file they
/// cannot edit. The message ends with the rule the name broke.
/// </remarks>
public sealed class KnownValueNameTests
{
    private const string NotAnIdentifier = "it is not a C# identifier";
    private const string Keyword = "it is a C# keyword";
    private const string Generated = "the generated code already uses that name";
    private const string Declared = "the type already has a member of that name";
    private const string Duplicate = "another known value already takes that name";

    [Theory]
    [InlineData("class")]
    [InlineData("default")]
    [InlineData("event")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("int")]
    [InlineData("__arglist")]
    public void A_known_value_named_after_a_keyword_is_reported_and_the_others_are_generated(string name)
        => AssertRefusedAlone(name, Keyword);

    [Theory]
    [InlineData("not an identifier")]
    [InlineData("1st")]
    [InlineData("Fr-Be")]
    [InlineData("")]
    public void A_known_value_whose_name_is_not_an_identifier_is_reported(string name)
        => AssertRefusedAlone(name, NotAnIdentifier);

    /// <summary>
    /// None of these is a member the generator writes, but a static property of that name would still break the
    /// generated code: <c>_</c> would capture the discard it writes as <c>out _</c>, and the others would hide a
    /// member of <see cref="object"/>, with a warning inside the generated file.
    /// </summary>
    [Theory]
    [InlineData("_", Generated)]
    [InlineData("GetType", Declared)]
    [InlineData("MemberwiseClone", Declared)]
    [InlineData("ReferenceEquals", Declared)]
    public void A_known_value_named_after_the_discard_or_an_inherited_member_is_reported(string name, string reason)
        => AssertRefusedAlone(name, reason);

    /// <summary>
    /// The author's own members are in the type the generator reopens, and a static property of the same name would
    /// collide with each of them in the generated file, whatever its kind: a hook, a field, a property, a nested
    /// type.
    /// </summary>
    [Fact]
    public void A_known_value_named_after_a_member_the_type_declares_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue("NormalizeValue", "N")]
            [KnownValue("Fallback", "B")]
            [KnownValue("Region", "R")]
            [KnownValue("get_Region", "G")]
            [KnownValue("Formats", "S")]
            [KnownValue("France", "FR")]
            public readonly partial struct Country : IValueObjectNormalizer<string>
            {
                public static readonly string Fallback = "FR";

                public string Region => Value[..1];

                public static string NormalizeValue(string value) => value.ToUpperInvariant();

                public static class Formats
                {
                    public const string Short = "S";
                }
            }
            """);

        run.Diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == "VO0006");
        run.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().Equal(
            Message("NormalizeValue", "Country", Declared),
            Message("Fallback", "Country", Declared),
            Message("Region", "Country", Declared),
            Message("get_Region", "Country", Declared),
            Message("Formats", "Country", Declared));
        run.SingleValueObject.Should().Contain("public static global::Test.Country France { get; }");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A known value is a property, whose getter the compiler names <c>get_</c> followed by its name. A member the type
    /// declares under that name collides with the getter, whatever its kind, unless it is a method taking parameters,
    /// which the getter overloads: a field or a nested type is a second definition of the name (CS0102), and a method
    /// without parameters, generic or not, static or not, reserves the getter's signature (CS0082).
    /// </summary>
    [Fact]
    public void A_known_value_whose_getter_takes_the_name_of_a_member_the_type_declares_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue("France", "FR")]
            [KnownValue("Belgium", "BE")]
            [KnownValue("Austria", "AT")]
            [KnownValue("Spain", "ES")]
            [KnownValue("Italy", "IT")]
            [KnownValue("Germany", "DE")]
            public readonly partial struct Country
            {
                public static readonly string get_Spain = "ES";

                public static string get_France() => "FR";

                public int get_Belgium() => 0;

                public static T get_Austria<T>() => default!;

                public static string get_Germany(int index) => "DE";

                public static class get_Italy
                {
                }
            }
            """);

        run.Diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == "VO0006");
        run.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().Equal(
            Message("France", "Country", Getter("France")),
            Message("Belgium", "Country", Getter("Belgium")),
            Message("Austria", "Country", Getter("Austria")),
            Message("Spain", "Country", Getter("Spain")),
            Message("Italy", "Country", Getter("Italy")));
        run.SingleValueObject.Should().Contain("public static global::Test.Country Germany { get; }");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A known value is a property, whose getter the compiler names <c>get_</c> followed by its name: another known
    /// value of that name would collide with it, whichever is declared first.
    /// </summary>
    [Fact]
    public void A_known_value_named_after_the_getter_of_another_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue("get_France", "FX")]
            [KnownValue("France", "FR")]
            [KnownValue("get_Belgium", "BX")]
            public readonly partial struct Country;
            """);

        run.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().Equal(
            Message("get_France", "Country", Generated));
        run.SingleValueObject.Should().Contain("public static global::Test.Country get_Belgium { get; }");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_second_known_value_of_the_same_name_is_reported()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue("France", "FR")]
            [KnownValue("France", "FX")]
            public readonly partial struct Country;
            """);

        run.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().Equal(
            Message("France", "Country", Duplicate));
        run.SingleValueObject.Should().Contain("public static global::Test.Country France { get; } = Create(\"FR\");");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A contextual keyword is an ordinary identifier outside the construct that gives it meaning, so refusing one
    /// would refuse a name the compiler accepts.
    /// </summary>
    [Theory]
    [InlineData("var")]
    [InlineData("value")]
    [InlineData("record")]
    [InlineData("async")]
    [InlineData("await")]
    [InlineData("nameof")]
    [InlineData("field")]
    [InlineData("dynamic")]
    [InlineData("when")]
    public void A_known_value_named_after_a_contextual_keyword_is_generated(string name)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<string>]
            [KnownValue("{{name}}", "K")]
            public readonly partial struct Country;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain($"public static global::Test.Country {name} {{ get; }}");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// Reads the members out of the generated code rather than out of a list, so that a member added to an emitter
    /// and forgotten by the check fails here. An operator counts under its metadata name, and a property under the
    /// name of its getter too, which the compiler reserves in the type as it does any other member's. Each
    /// configuration turns on every option that adds a member.
    /// </summary>
    [Theory]
    [InlineData(
        """
        [ValueObject<string>(Pattern = "^[A-Z]+$", ValueSet = ValueSetKind.Closed, ImplicitConversionToValue = true, ExplicitConversionFromValue = true)]
        [KnownValue("Kept", "K")]
        public readonly partial struct Code : IValueObjectNormalizer<string>, IValueObjectSpanNormalizer, IValueObjectFormatter<string>
        {
            public static string NormalizeValue(string value) => NormalizeValue(value.AsSpan());

            public static string NormalizeValue(ReadOnlySpan<char> value) => value.Trim().ToString();

            public static bool TryFormatValue(
                in string value,
                Span<char> destination,
                out int charsWritten,
                ReadOnlySpan<char> format,
                IFormatProvider? provider)
                => destination.TryWrite(provider, $"{value}", out charsWritten);
        }
        """,
        "\"K\"")]
    [InlineData(
        """
        [ValueObject<int>(Arithmetic = true, ValueSet = ValueSetKind.Closed, ImplicitConversionToValue = true, ExplicitConversionFromValue = true)]
        [KnownValue("Kept", 1)]
        public readonly partial struct Code : IValueObjectFormatter<int>
        {
            public static bool TryFormatValue(
                in int value,
                Span<char> destination,
                out int charsWritten,
                ReadOnlySpan<char> format,
                IFormatProvider? provider)
                => destination.TryWrite(provider, $"{value}", out charsWritten);
        }
        """,
        "1")]
    public void Every_member_the_generator_writes_is_refused_as_a_known_value_name(string declaration, string literal)
    {
        var members = GeneratedMembers.WrittenOn(GeneratorHarness.Run(declaration).SingleValueObject, "Code")
            .Where(name => name is not ("Kept" or "get_Kept"))
            .ToList();

        members.Should().Contain(
            ["Code", "Value", "get_Value", "Schema", "Create", "KnownValues", "op_Equality", "op_Implicit"],
            "the reading must keep finding them");

        var attributes = string.Concat(members.Select(name => $"[KnownValue(\"{name}\", {literal})]\n"));
        var run = GeneratorHarness.Run(attributes + declaration);

        run.Diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == "VO0006");
        run.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().BeEquivalentTo(
            members.Select(name => Message(name, "Code", Generated)));
        GeneratedMembers.WrittenOn(run.SingleValueObject, "Code").Should().BeEquivalentTo([.. members, "Kept", "get_Kept"]);
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The members follow the options, and so do the names they take: without arithmetic there is no <c>Zero</c>
    /// to collide with, nor an addition operator, and without a conversion no conversion operator.
    /// </summary>
    [Theory]
    [InlineData("int", "Zero", "0")]
    [InlineData("int", "One", "1")]
    [InlineData("int", "Sum", "2")]
    [InlineData("int", "op_Addition", "3")]
    [InlineData("int", "op_Implicit", "4")]
    [InlineData("int", "op_Explicit", "5")]
    [InlineData("string", "DeclaredPattern", "\"P\"")]
    [InlineData("string", "KnownUnderlyingValues", "\"K\"")]
    [InlineData("string", "TryCreateFrom", "\"T\"")]
    [InlineData("string", "FormatWithPooledBuffer", "\"F\"")]
    [InlineData("int", "FormatWithPooledBuffer", "6")]
    public void A_member_name_the_options_leave_free_is_usable_as_a_known_value_name(
        string underlying,
        string name,
        string literal)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            [KnownValue("{{name}}", {{literal}})]
            public readonly partial struct Code;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain($"public static global::Test.Code {name} {{ get; }}");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The retry loop of a span formatting hook is written only where that hook answers. When a string formatting
    /// hook answers in its place, its name stays free.
    /// </summary>
    [Fact]
    public void The_retry_loop_takes_its_name_only_where_the_span_formatting_hook_answers()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            [KnownValue("FormatWithPooledBuffer", 1)]
            public readonly partial struct Floor : IValueObjectFormatter<int>, IValueObjectStringFormatter<int>
            {
                public static string FormatValue(in int value, ReadOnlySpan<char> format, IFormatProvider? provider)
                    => "floor " + value.ToString(provider);

                public static bool TryFormatValue(
                    in int value,
                    Span<char> destination,
                    out int charsWritten,
                    ReadOnlySpan<char> format,
                    IFormatProvider? provider)
                    => value.TryFormat(destination, out charsWritten, format, provider);
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("public static global::Test.Floor FormatWithPooledBuffer { get; }");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_known_value_without_a_name_is_reported_under_a_question_mark()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue(null, "FR")]
            public readonly partial struct Country;
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0006");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        run.Locate(diagnostic).Should().Be(("Country", "public readonly partial struct Country;"));
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(Message("?", "Country", NotAnIdentifier));
        run.CompilationDiagnostics.Should().OnlyContain(
            compiled => compiled.Id == "CS8625",
            "the null literal is the author's to answer for, and nothing generated may add to it");
    }

    /// <summary>
    /// A negation is written only over a signed type, so its name stays free on an unsigned one.
    /// </summary>
    [Fact]
    public void The_negation_operator_takes_its_name_only_where_it_is_written()
    {
        var signed = GeneratorHarness.Run("""
            [ValueObject<int>(Arithmetic = true)]
            [KnownValue("op_UnaryNegation", 1)]
            public readonly partial struct Code;
            """);
        var unsigned = GeneratorHarness.Run("""
            [ValueObject<uint>(Arithmetic = true)]
            [KnownValue("op_UnaryNegation", 1u)]
            public readonly partial struct Code;
            """);

        signed.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().Equal(
            Message("op_UnaryNegation", "Code", Generated));
        signed.CompilationDiagnostics.Should().BeEmpty();
        unsigned.Diagnostics.Should().BeEmpty();
        unsigned.SingleValueObject.Should().Contain("public static global::Test.Code op_UnaryNegation { get; }");
        unsigned.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// Asserts that a known value of that name is reported, on the declaration and with the rule it broke, while a
    /// second one is generated and the generated code compiles.
    /// </summary>
    private static void AssertRefusedAlone(string name, string reason)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<string>]
            [KnownValue("{{name}}", "K")]
            [KnownValue("France", "FR")]
            public readonly partial struct Country;
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0006");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        run.Locate(diagnostic).Should().Be(("Country", "public readonly partial struct Country;"));
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(Message(name, "Country", reason));

        run.SingleValueObject.Should().Contain("public static global::Test.Country France { get; }");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    private static string Getter(string name)
        => $"the type already has a member named get_{name}, which the property's getter would take";

    private static string Message(string name, string typeName, string reason)
        => $"'{name}' is not usable as the name of a generated member on '{typeName}': {reason}";
}
