using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// What <c>VO0010</c> reports: <c>default</c> and parameterless construction of a value object, the two expressions
/// that bring one into existence without validation, and nothing else.
/// </summary>
/// <remarks>
/// The diagnostic is an error, so a false positive breaks a consumer's build and a false negative lets an
/// unvalidated instance through without a word. Each test therefore pins the exact diagnostics — how many, and
/// on which expression — rather than only that one fired: an exact count is also what proves the code the
/// generator writes, whose rejection paths assign <c>default</c>, is left alone.
/// </remarks>
public sealed class UninitializedValueObjectTests
{
    private const string MessageForCode = "'Code' produced here never went through validation. Build it with "
        + "Create or TryCreate, or declare it with AllowDefault when its default state is meaningful.";

    [Fact]
    public async Task A_default_expression_and_a_default_literal_are_reported_where_they_are_written()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>]
            public readonly partial struct Code;

            public static class Use
            {
                public static Code Explicit() => default(Code);

                public static Code Literal()
                {
                    Code code = default;
                    return code;
                }
            }
            """);

        Located(diagnostics).Should().Equal(
            ("default(Code)", "public static Code Explicit() => default(Code);"),
            ("default", "Code code = default;"));

        diagnostics.Should().AllSatisfy(diagnostic =>
        {
            diagnostic.Id.Should().Be("VO0010");
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
            diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(MessageForCode);
        });
    }

    [Fact]
    public async Task A_parameterless_construction_is_reported_in_every_spelling()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>]
            public readonly partial struct Code;

            public static class Use
            {
                public static Code Explicit() => new Code();

                public static Code TargetTyped() => new();

                public static Code WithAnEmptyInitializer() => new Code { };
            }
            """);

        Located(diagnostics).Should().Equal(
            ("new Code()", "public static Code Explicit() => new Code();"),
            ("new()", "public static Code TargetTyped() => new();"),
            ("new Code { }", "public static Code WithAnEmptyInitializer() => new Code { };"));
    }

    /// <summary>
    /// A parameter defaulting to <c>default</c> or <c>new Code()</c> is reported once, where that is written: a call
    /// omitting the argument writes neither, and fixing the declaration fixes every call. An argument written out
    /// as <c>default</c> is reported on its own.
    /// </summary>
    [Fact]
    public async Task A_parameter_defaulting_to_default_is_reported_where_it_is_declared_and_not_where_it_is_omitted()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>]
            public readonly partial struct Code;

            public static class Use
            {
                public static int Count(Code code = default) => 0;

                public static int Measure(Code code = new Code()) => 0;

                public static int Omitted() => Count() + Measure();

                public static int Written() => Count(default);

                public static int Supplied() => Count(Code.Create("A"));
            }
            """);

        Located(diagnostics).Should().Equal(
            ("default", "public static int Count(Code code = default) => 0;"),
            ("new Code()", "public static int Measure(Code code = new Code()) => 0;"),
            ("default", "public static int Written() => Count(default);"));
    }

    /// <summary>
    /// The absent value object is null, which is how absence is meant to be written, and a construction with an
    /// argument goes through validation. A type parameter, an array, a primitive and a plain object are not value
    /// objects, and an array of value objects is a boundary the
    /// analyzer does not see: its elements are reported by <c>IsDefault</c> at run time, as documented.
    /// </summary>
    [Fact]
    public async Task Only_an_uninitialized_value_object_itself_is_reported()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>]
            public readonly partial struct Code
            {
                public static Code Of(string value) => new(value);
            }

            public static class Use
            {
                public static Code? Absent() => default(Code?);

                public static Code? AbsentLiteral() => default;

                public static Code? NoValue() => new Code?();

                public static T Generic<T>() => default!;

                public static Code[]? NoArray() => default;

                public static Code[] Elements() => new Code[3];

                public static int Number() => default(int);

                public static object Plain() => new object();
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task AllowDefault_silences_VO0010()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<int>(AllowDefault = true)]
            public readonly partial struct Sequence;

            public static class Use
            {
                public static Sequence Start() => default;

                public static Sequence Fresh() => new();
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task AllowDefault_set_to_false_still_reports_VO0010()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>(MaxLength = 34, AllowDefault = false)]
            public readonly partial struct Iban;

            public static class Use
            {
                public static Iban Missing() => default(Iban);
            }
            """);

        Located(diagnostics).Should().Equal(("default(Iban)", "public static Iban Missing() => default(Iban);"));
        diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().StartWith("'Iban' produced here");
    }

    /// <summary>
    /// An identifier is a value object like any other, generated with an <c>IsDefault</c> and an empty
    /// <c>Value</c> for the instance that skipped validation: the analyzer forbids producing it, as it does for
    /// <c>[ValueObject&lt;T&gt;]</c>.
    /// </summary>
    [Fact]
    public async Task An_uninitialized_entity_identifier_is_reported()
    {
        var diagnostics = await RunAsync("""
            [EntityId("acc")]
            public readonly partial struct AccountId;

            public static class Use
            {
                public static AccountId Missing() => default;

                public static AccountId Fresh() => new AccountId();

                public static AccountId? Absent() => default;
            }
            """);

        Located(diagnostics).Should().Equal(
            ("default", "public static AccountId Missing() => default;"),
            ("new AccountId()", "public static AccountId Fresh() => new AccountId();"));
        diagnostics.Should().AllSatisfy(diagnostic =>
            diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().StartWith("'AccountId' produced here"));
    }

    [Fact]
    public async Task AllowDefault_silences_VO0010_on_an_entity_identifier()
    {
        var diagnostics = await RunAsync("""
            [EntityId("acc", Granularity = IdGranularity.Day, AllowDefault = true)]
            public readonly partial struct AccountId;

            public static class Use
            {
                public static AccountId Missing() => default;

                public static AccountId Fresh() => new();
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task AllowDefault_set_to_false_still_reports_VO0010_on_an_entity_identifier()
    {
        var diagnostics = await RunAsync("""
            [EntityId("acc", Granularity = IdGranularity.Day, AllowDefault = false)]
            public readonly partial struct AccountId;

            public static class Use
            {
                public static AccountId Fresh() => new();
            }
            """);

        Located(diagnostics).Should().Equal(("new()", "public static AccountId Fresh() => new();"));
    }

    /// <summary>
    /// An analyzer runs on the text as the author types it. <c>AllowDefault = 1</c> is the compiler's error to
    /// report on the declaration; meanwhile the option is read as absent, so it opts nothing out.
    /// </summary>
    [Fact]
    public async Task AllowDefault_holding_a_constant_of_the_wrong_type_does_not_silence_VO0010()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<UninitializedValueObjectAnalyzer>("""
            [ValueObject<string>(AllowDefault = 1)]
            public readonly partial struct Code;

            public static class Use
            {
                public static Code Missing() => default(Code);
            }
            """);

        Located(diagnostics).Should().Equal(("default(Code)", "public static Code Missing() => default(Code);"));
    }

    /// <summary>
    /// The rejection paths of the generated <c>TryCreate</c> and <c>TryParse</c> assign <c>default</c> to their
    /// <c>out</c> parameter. That is generated code, which the analyzer is configured not to look at.
    /// </summary>
    [Fact]
    public async Task The_code_the_generator_writes_is_not_analyzed()
    {
        const string Source = """
            [ValueObject<string>]
            public readonly partial struct Code;

            [ValueObject<int>(Arithmetic = true)]
            public readonly partial struct Quantity;

            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            [KnownValue("Eur", "EUR")]
            public readonly partial struct Currency;

            [EntityId("acc")]
            public readonly partial struct AccountId;
            """;

        var generated = GeneratorHarness.Run(Source).Files;
        generated.Where(file => file.Text.Contains("result = default;", StringComparison.Ordinal))
            .Should().HaveCount(4, "every value object and identifier assigns default on its rejection paths");

        var diagnostics = await RunAsync(Source);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A value object declared in a referenced assembly reaches the analyzer as metadata, where its annotation is
    /// read from the assembly rather than from the source.
    /// </summary>
    [Fact]
    public async Task A_value_object_from_a_referenced_assembly_is_reported()
    {
        var domain = GeneratorHarness.Emit(
            """
            [ValueObject<string>]
            public readonly partial struct Code;
            """,
            "Domain");

        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<UninitializedValueObjectAnalyzer>(
            """
            namespace Consumer;

            public static class Use
            {
                public static Test.Code Missing() => default;
            }
            """,
            GeneratorHarness.LibraryReferences.Add(MetadataReference.CreateFromImage(domain)));

        Located(diagnostics).Should().Equal(("default", "public static Test.Code Missing() => default;"));
    }

    [Fact]
    public async Task An_entity_identifier_from_a_referenced_assembly_is_reported()
    {
        var domain = GeneratorHarness.Emit(
            """
            [EntityId("acc")]
            public readonly partial struct AccountId;
            """,
            "Domain");

        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<UninitializedValueObjectAnalyzer>(
            """
            namespace Consumer;

            public static class Use
            {
                public static Test.AccountId Fresh() => new();
            }
            """,
            GeneratorHarness.LibraryReferences.Add(MetadataReference.CreateFromImage(domain)));

        Located(diagnostics).Should().Equal(("new()", "public static Test.AccountId Fresh() => new();"));
    }

    /// <summary>
    /// An assembly can carry an attribute whose constructor the compiler cannot resolve. Roslyn still loads the
    /// type and lists the attribute, with no class: the analyzer must take that for what it is, an attribute that
    /// is not the value object annotation, rather than fail.
    /// </summary>
    [Fact]
    public async Task A_type_carrying_an_attribute_the_compiler_cannot_read_is_not_reported()
    {
        var image = GeneratorHarness.Emit(
            """
            namespace Broken;

            [System.Diagnostics.DebuggerDisplay("opaque")]
            public struct Opaque;
            """,
            "Broken");

        // The attribute points at its constructor by name; renaming ".ctor" in the string heap leaves a reference
        // the compiler cannot resolve, at the same length, so nothing else in the image moves.
        var constructorName = Encoding.ASCII.GetBytes("\0.ctor\0");
        var at = image.AsSpan().IndexOf(constructorName);
        at.Should().BePositive("the string heap of the image holds the name of every constructor it references");
        image[at + 5] = (byte)'x';

        var references = GeneratorHarness.LibraryReferences.Add(MetadataReference.CreateFromImage(image));
        const string Consumer = """
            namespace Consumer;

            public static class Use
            {
                public static Broken.Opaque Missing() => default;
            }
            """;

        var opaque = CSharpCompilationOf(Consumer, references).GetTypeByMetadataName("Broken.Opaque");
        opaque.Should().NotBeNull();
        opaque!.GetAttributes().Should().ContainSingle().Which.AttributeClass.Should().BeNull();

        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<UninitializedValueObjectAnalyzer>(Consumer, references);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The analyzer ships inside the package, but nothing stops a project from loading it without the contracts:
    /// with no annotation to look for, it has nothing to report.
    /// </summary>
    [Fact]
    public async Task Nothing_is_reported_where_the_library_is_not_referenced()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<UninitializedValueObjectAnalyzer>(
            """
            namespace Plain;

            public readonly struct Point
            {
                public static Point Origin() => default;

                public static Point Fresh() => new();
            }
            """,
            GeneratorHarness.FrameworkReferences);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The compiler never hands an analyzer a null context, so the guard is only reached by a direct call, which
    /// it tolerates.
    /// </summary>
    [Fact]
    public void The_analyzer_initialized_without_a_context_does_nothing()
    {
        var analyzer = new UninitializedValueObjectAnalyzer();

        analyzer.Invoking(target => target.Initialize(null!)).Should().NotThrow();
    }

    /// <summary>
    /// Runs the analyzer over a snippet that is otherwise valid C#, so that what it reports is never a side effect
    /// of code the compiler would refuse anyway.
    /// </summary>
    private static Task<ImmutableArray<Diagnostic>> RunAsync(string source)
    {
        GeneratorHarness.Run(source).CompilationDiagnostics.Should().BeEmpty();

        return GeneratorHarness.RunAnalyzerAsync<UninitializedValueObjectAnalyzer>(source);
    }

    /// <summary>
    /// Gets each diagnostic as the text it spans and the line it starts on, trimmed, in source order. Every one is
    /// expected to be <c>VO0010</c>, which is checked here once for all the tests.
    /// </summary>
    private static List<(string Text, string Line)> Located(ImmutableArray<Diagnostic> diagnostics)
    {
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == "VO0010");

        return
        [
            .. diagnostics
                .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
                .Select(diagnostic =>
                {
                    var text = diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken);
                    var span = diagnostic.Location.SourceSpan;

                    return (text.ToString(span), text.Lines.GetLineFromPosition(span.Start).ToString().Trim());
                }),
        ];
    }

    private static CSharpCompilation CSharpCompilationOf(string source, ImmutableArray<MetadataReference> references)
        => CSharpCompilation.Create(
            "Inspection",
            [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
            references);
}
