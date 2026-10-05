using System.Globalization;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Generators.Internal;
using AdCodicem.ValueObjects.Generators.Model;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// The names a value object cannot take, which <c>VO0019</c> refuses, and the ones it can.
/// </summary>
/// <remarks>
/// The generated code writes members on the value object, and statements inside them. A type named after one of
/// those members would turn what the generator wrote into errors in a file the author cannot edit. The type is
/// reported instead, and nothing is generated for it, while the rest of the compilation still is. The statements name
/// every type they use, so no other name is taken.
/// </remarks>
public sealed class TypeNameTests
{
    private const string MemberReason = "takes the name of a member the generated code writes on it";

    private const string MemberRemedy = "Rename it: C# does not let a member take the name of the type that declares it.";

    /// <summary>
    /// The generated statements name the type of every local and discard nothing, so a value object may take the name
    /// <c>var</c> or <c>_</c>, or be nested in a type that does, and still generates code that compiles.
    /// </summary>
    /// <param name="declaration">The value object, and the types around it.</param>
    [Theory]
    [InlineData(
        """
        [ValueObject<string>]
        public readonly partial struct var;
        """)]
    [InlineData(
        """
        [EntityId("acc")]
        public readonly partial struct _;
        """)]
    [InlineData(
        """
        public partial class var
        {
            [ValueObject<int>(Arithmetic = true)]
            public readonly partial struct Code;
        }
        """)]
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
        """)]
    public void A_value_object_named_or_nested_in_a_type_named_var_or_the_discard_is_generated(string declaration)
    {
        var run = GeneratorHarness.Run(declaration);

        run.Diagnostics.Should().BeEmpty();
        run.Files.Should().HaveCount(2);
        Errors(run).Should().BeEmpty();
    }

    /// <summary>
    /// A type named <c>var</c> or <c>_</c> beside the value objects, a namespace segment named <c>_</c>, and global
    /// aliases taking either name all change what <c>var x</c>, <c>out var x</c> or <c>out _</c> would mean in the
    /// generated statements. None of them is written, by any emitter, for any underlying type or option.
    /// </summary>
    [Fact]
    public void Every_value_object_compiles_beside_types_and_aliases_named_var_or_the_discard()
    {
        var numeric = new HashSet<string>(StringComparer.Ordinal)
        {
            "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong", "System.Int128", "System.UInt128",
            "decimal", "double", "float",
        };
        var everyType = string.Concat(UnderlyingType.SupportedNames.Select((name, index) => $$"""

                [ValueObject<{{name}}>{{(numeric.Contains(name) ? "(Arithmetic = true)" : string.Empty)}}]
                public readonly partial struct Wrapper{{index}};

            """));

        var run = GeneratorHarness.Run(
            $$"""
            global using var = System.Object;
            global using _ = System.Object;
            using System;
            using System.Text.RegularExpressions;
            using AdCodicem.ValueObjects;
            using AdCodicem.ValueObjects.Annotations;
            using AdCodicem.ValueObjects.Identifiers;

            namespace Test._
            {
                public class var;

                public class _;
            {{everyType}}
                [ValueObject<string>(ValueSet = ValueSetKind.Closed, ImplicitConversionToValue = true, ExplicitConversionFromValue = true)]
                public readonly partial struct Code
                    : IValueObjectNormalizer<string>, IValueObjectSpanNormalizer, IValueObjectValidator<string>, IValueObjectFormatter<string>
                {
                    [KnownValue]
                    public static readonly Code Kept = Known("K");

                    public static string NormalizeValue(string value) => NormalizeValue(value.AsSpan());

                    public static string NormalizeValue(ReadOnlySpan<char> value) => value.Trim().ToString();

                    public static ValidationResult ValidateValue(in string value) => ValidationResult.Success;

                    public static bool TryFormatValue(
                        in string value,
                        Span<char> destination,
                        out int charsWritten,
                        ReadOnlySpan<char> format,
                        IFormatProvider? provider)
                        => destination.TryWrite(provider, $"{value}", out charsWritten);
                }

                [ValueObject<int>(Arithmetic = true, ValueSet = ValueSetKind.Closed)]
                public readonly partial struct Floor : IValueObjectStringFormatter<int>
                {
                    [KnownValue]
                    public static readonly Floor Ground = Known(0);

                    public static string FormatValue(in int value, ReadOnlySpan<char> format, IFormatProvider? provider)
                        => $"floor {value}";
                }

                [ValueObject<string>]
                public readonly partial struct Shape : IValueObjectPatternValidator
                {
                    [GeneratedRegex("^[A-Z]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
                    public static partial Regex Pattern { get; }
                }

                [ValueObject<string>]
                public readonly partial struct Word : IValueObjectPatternValidator
                {
                    [GeneratedRegex("^[a-z]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
                    public static partial Regex Pattern { get; }
                }

                [EntityId("acc")]
                public readonly partial struct AccountId : IValueObjectStringFormatter<string>
                {
                    public static string FormatValue(in string value, ReadOnlySpan<char> format, IFormatProvider? provider) => value;
                }
            }
            """,
            referenceJsonPackage: true);

        run.Diagnostics.Should().BeEmpty();
        run.Files.Should().HaveCount(UnderlyingType.SupportedNames.Count() + 6);
        Errors(run).Should().BeEmpty();
    }

    /// <summary>
    /// The errors of compiling the generated code. A type named <c>var</c> also draws CS8981, a warning that its
    /// lower-case name may one day be a keyword, which is the author's to weigh.
    /// </summary>
    private static IEnumerable<Diagnostic> Errors(GeneratorRun run)
        => run.CompilationDiagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// Reads the members out of the generated code rather than out of a list, so that a member added to an emitter and
    /// forgotten by the check fails here. A value object of each of those names is then declared, with the same options,
    /// beside one whose name is free. Each configuration turns on every option that adds a member.
    /// </summary>
    [Theory]
    [InlineData(
        """
        [ValueObject<string>(ValueSet = ValueSetKind.Closed, ImplicitConversionToValue = true, ExplicitConversionFromValue = true)]
        public readonly partial struct Code : IValueObjectNormalizer<string>, IValueObjectSpanNormalizer, IValueObjectFormatter<string>, IValueObjectPatternValidator
        {
            [KnownValue]
            public static readonly Code Kept = Known("K");

            [GeneratedRegex("^[A-Z]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
            public static partial Regex Pattern { get; }

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
        public readonly partial struct Code : IValueObjectFormatter<int>
        {
            [KnownValue]
            public static readonly Code Kept = Known(1);

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

        // The free name comes last, so that an identifier type claims its prefix once and no other type claims it. A type
        // that is not generated has no Known to create a known value through, so the others declare none.
        var source = string.Concat(members.Select(name => Regex.Replace(
            declaration.Replace("partial struct Code", $"partial struct {name}", StringComparison.Ordinal),
            @"\s*\[KnownValue\]\s*public static readonly Code Kept = Known\([^)]*\);",
            string.Empty) + "\n")) + declaration;
        var run = GeneratorHarness.Run(source);

        run.Diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == "VO0019");
        run.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().BeEquivalentTo(
            members.Select(name => $"'{name}' {MemberReason}, which the generator does not support. {MemberRemedy}"));
        run.Files.Select(file => file.HintName).Should().BeEquivalentTo(HintNames.For("Test.Code"), "ValueObjectRegistration.g.cs");
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
        run.Files.Select(file => file.HintName).Should().BeEquivalentTo(HintNames.For($"Test.{name}"), "ValueObjectRegistration.g.cs");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// <c>IsDefault</c> is implemented explicitly, which takes no name in the type: a value object may be named after
    /// it or after its getter, over a reference type and a value type alike.
    /// </summary>
    [Theory]
    [InlineData("[ValueObject<string>]", "IsDefault")]
    [InlineData("[ValueObject<int>]", "IsDefault")]
    [InlineData("[EntityId(\"acc\")]", "get_IsDefault")]
    public void A_type_may_take_the_name_of_the_explicit_IsDefault(string attribute, string name)
        => A_type_name_the_options_leave_free_is_generated(attribute, name);

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
            HintNames.For("Test.FormatWithPooledBuffer"),
            "ValueObjectRegistration.g.cs");
        run.CompilationDiagnostics.Should().BeEmpty();
    }
}
