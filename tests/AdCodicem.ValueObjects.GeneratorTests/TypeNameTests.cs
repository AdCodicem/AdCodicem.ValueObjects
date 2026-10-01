using System.Globalization;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// The names a value object cannot take, which <c>VO0019</c> refuses.
/// </summary>
/// <remarks>
/// The generated code writes members on the value object, and statements inside them. A type named after one of
/// those members, or after <c>var</c> or the discard <c>_</c> those statements write, would turn what the generator
/// wrote into errors in a file the author cannot edit. The type is reported instead, and nothing is generated for it,
/// while the rest of the compilation still is.
/// </remarks>
public sealed class TypeNameTests
{
    private const string MemberReason = "takes the name of a member the generated code writes on it";

    private const string MemberRemedy = "Rename it: C# does not let a member take the name of the type that declares it.";

    [Theory]
    [InlineData(
        """
        [ValueObject<string>]
        public readonly partial struct var;
        """,
        "var",
        "public readonly partial struct var;",
        "takes the name var",
        "var")]
    [InlineData(
        """
        [EntityId("acc")]
        public readonly partial struct _;
        """,
        "_",
        "public readonly partial struct _;",
        "takes the name _",
        "_")]
    [InlineData(
        """
        public partial class var
        {
            [ValueObject<int>]
            public readonly partial struct Code;
        }
        """,
        "Code",
        "public readonly partial struct Code;",
        "is nested in the type 'var'",
        "var")]
    [InlineData(
        """
        public partial class _
        {
            public partial record Inner
            {
                [EntityId("acc")]
                public readonly partial struct Code;
            }
        }
        """,
        "Code",
        "public readonly partial struct Code;",
        "is nested in the type '_'",
        "_")]
    public void A_value_object_named_or_nested_in_a_type_named_after_what_the_generated_statements_write_is_reported(
        string declaration,
        string typeName,
        string line,
        string reason,
        string captured)
    {
        // The other value object is declared in a namespace of its own: a type named var or _ is in the scope of every
        // value object of its namespace, and their generated statements would refer to it too.
        var run = GeneratorHarness.Run($$"""
            using System;
            using AdCodicem.ValueObjects;
            using AdCodicem.ValueObjects.Annotations;
            using AdCodicem.ValueObjects.Identifiers;

            namespace Test
            {
            {{declaration}}
            }

            namespace Elsewhere
            {
                [ValueObject<string>]
                public readonly partial struct Other;
            }
            """);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0019");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        run.Locate(diagnostic).Should().Be((typeName, line));
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"'{typeName}' {reason}, which the generator does not support. Rename the type '{captured}': the generated "
            + $"code writes {captured} in its statements, where it would refer to that type instead.");

        run.Files.Select(file => file.HintName).Should().BeEquivalentTo("Elsewhere.Other.g.cs", "ValueObjectRegistration.g.cs");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// Reads the members out of the generated code rather than out of a list, so that a member added to an emitter and
    /// forgotten by the check fails here. A value object of each of those names is then declared, with the same options,
    /// beside one whose name is free. Each configuration turns on every option that adds a member.
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
        """)]
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
        """)]
    [InlineData(
        """
        [EntityId("acc")]
        public readonly partial struct Code : IValueObjectFormatter<string>
        {
            public static bool TryFormatValue(
                in string value,
                Span<char> destination,
                out int charsWritten,
                ReadOnlySpan<char> format,
                IFormatProvider? provider)
                => destination.TryWrite(provider, $"{value}", out charsWritten);
        }
        """)]
    public void A_value_object_named_after_a_member_the_generator_writes_on_it_is_reported(string declaration)
    {
        var members = GeneratedMembers.WrittenOn(GeneratorHarness.Run(declaration).SingleValueObject, "Code")
            .Where(name => name is not ("Code" or "Kept" or "get_Kept"))
            .ToList();

        members.Should().Contain(
            ["Value", "get_Value", "Schema", "Create", "Parse", "TryParse", "op_Equality", "ValueJsonConverter"],
            "the reading must keep finding them");

        // The free name comes last, so that an identifier type claims its prefix once and no other type claims it.
        var source = string.Concat(members.Select(name => declaration.Replace(
            "partial struct Code",
            $"partial struct {name}",
            StringComparison.Ordinal) + "\n")) + declaration;
        var run = GeneratorHarness.Run(source);

        run.Diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == "VO0019");
        run.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().BeEquivalentTo(
            members.Select(name => $"'{name}' {MemberReason}, which the generator does not support. {MemberRemedy}"));
        run.Files.Select(file => file.HintName).Should().BeEquivalentTo("Test.Code.g.cs", "ValueObjectRegistration.g.cs");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The members follow the options, and so do the names they take: without arithmetic there is no <c>Zero</c> on
    /// the type to collide with it, and only an identifier has a <c>Prefix</c>.
    /// </summary>
    [Theory]
    [InlineData("[ValueObject<int>]", "Zero")]
    [InlineData("[ValueObject<int>]", "op_Addition")]
    [InlineData("[ValueObject<int>]", "op_Implicit")]
    [InlineData("[ValueObject<string>]", "DeclaredPattern")]
    [InlineData("[ValueObject<string>]", "TryCreateFrom")]
    [InlineData("[ValueObject<string>]", "FormatWithPooledBuffer")]
    [InlineData("[ValueObject<string>]", "Prefix")]
    [InlineData("[ValueObject<string>]", "New")]
    [InlineData("[EntityId(\"acc\")]", "Zero")]
    public void A_type_name_the_options_leave_free_is_generated(string attribute, string name)
    {
        var run = GeneratorHarness.Run($"""
            {attribute}
            public readonly partial struct {name};
            """);

        run.Diagnostics.Should().BeEmpty();
        run.Files.Select(file => file.HintName).Should().BeEquivalentTo($"Test.{name}.g.cs", "ValueObjectRegistration.g.cs");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The retry loop of a span formatting hook is written only where that hook answers. When a string formatting
    /// hook answers in its place, on a value object as on an identifier, a type may take the loop's name.
    /// </summary>
    [Theory]
    [InlineData("[ValueObject<string>]")]
    [InlineData("[EntityId(\"acc\")]")]
    public void A_type_whose_string_formatter_answers_may_take_the_name_of_the_retry_loop(string attribute)
    {
        var run = GeneratorHarness.Run($$"""
            {{attribute}}
            public readonly partial struct FormatWithPooledBuffer : IValueObjectFormatter<string>, IValueObjectStringFormatter<string>
            {
                public static string FormatValue(in string value, ReadOnlySpan<char> format, IFormatProvider? provider) => value;

                public static bool TryFormatValue(
                    in string value,
                    Span<char> destination,
                    out int charsWritten,
                    ReadOnlySpan<char> format,
                    IFormatProvider? provider)
                    => destination.TryWrite(provider, $"{value}", out charsWritten);
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.Files.Select(file => file.HintName).Should().BeEquivalentTo(
            "Test.FormatWithPooledBuffer.g.cs",
            "ValueObjectRegistration.g.cs");
        run.CompilationDiagnostics.Should().BeEmpty();
    }
}
