using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
    /// The compiler hands an analyzer the default value of a member's parameter as an operation, and not that of a
    /// lambda's or a local function's parameter: each is reported all the same, once, on the expression written, with the
    /// message a method's parameter gets. A call leaving the argument out is not reported, nor is a <c>default</c> in a
    /// lambda's body reported twice.
    /// </summary>
    [Fact]
    public async Task A_parameter_of_a_lambda_or_a_local_function_defaulting_to_default_is_reported_as_a_method_parameter_is()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>(MaxLength = 10)]
            public readonly partial struct Sku;

            public static class Cases
            {
                public static string Method(Sku sku = default) => sku.ToString();

                public static string MethodNew(Sku sku = new()) => sku.ToString();

                public static string MethodTyped(Sku sku = default(Sku)) => sku.ToString();

                public static object Run()
                {
                    var lambda = string (Sku sku = default) => sku.ToString();
                    var lambdaNew = string (Sku sku = new()) => sku.ToString();
                    var lambdaTyped = string (Sku sku = default(Sku)) => sku.ToString();
                    static string Local(Sku sku = default) => sku.ToString();
                    static string LocalNew(Sku sku = new()) => sku.ToString();
                    var inBody = string () => { Sku sku = default; return sku.ToString(); };

                    return (lambda(), lambdaNew(), lambdaTyped(), Local(), LocalNew(), inBody());
                }
            }
            """);

        Located(diagnostics).Should().Equal(
            ("default", "public static string Method(Sku sku = default) => sku.ToString();"),
            ("new()", "public static string MethodNew(Sku sku = new()) => sku.ToString();"),
            ("default(Sku)", "public static string MethodTyped(Sku sku = default(Sku)) => sku.ToString();"),
            ("default", "var lambda = string (Sku sku = default) => sku.ToString();"),
            ("new()", "var lambdaNew = string (Sku sku = new()) => sku.ToString();"),
            ("default(Sku)", "var lambdaTyped = string (Sku sku = default(Sku)) => sku.ToString();"),
            ("default", "static string Local(Sku sku = default) => sku.ToString();"),
            ("new()", "static string LocalNew(Sku sku = new()) => sku.ToString();"),
            ("default", "var inBody = string () => { Sku sku = default; return sku.ToString(); };"));

        diagnostics.Should().AllSatisfy(diagnostic =>
        {
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
            diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
                "'Sku' produced here never went through validation. Build it with Create or TryCreate, or declare it "
                + "with AllowDefault when its default state is meaningful.");
        });
    }

    /// <summary>
    /// A minimal API handler is where a lambda's parameter usually takes a default value, and the Request Delegate
    /// Generator hands <c>(Sku sku = default) =&gt; …</c> a default instance for a request that leaves the value out. An
    /// optional parameter is <c>Sku?</c>, which binds <c>null</c>. <c>Map</c> stands for <c>MapGet</c>, which takes the
    /// handler as a <see cref="Delegate"/>.
    /// </summary>
    [Fact]
    public async Task A_handler_defaulting_a_value_object_is_reported_and_an_optional_one_is_not()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>(MaxLength = 10)]
            public readonly partial struct Sku;

            public static class Endpoints
            {
                public static void Map(string pattern, Delegate handler)
                {
                }

                public static void MapAll()
                {
                    Map("/search", (Sku sku = default) => sku.ToString());
                    Map("/optional", (Sku? sku) => sku?.ToString());
                    Map("/absent", (Sku? sku = null) => sku?.ToString());
                }
            }
            """);

        Located(diagnostics).Should().Equal(("default", "Map(\"/search\", (Sku sku = default) => sku.ToString());"));
    }

    /// <summary>
    /// What stays quiet on a method's parameter stays quiet on a lambda's or a local function's: a nullable value object
    /// defaulting to <c>default</c> or <c>null</c> is null, a value object declared with <c>AllowDefault = true</c> means its
    /// default, and a number is no value object.
    /// </summary>
    [Fact]
    public async Task A_lambda_or_local_function_parameter_defaulting_to_null_or_to_an_allowed_default_is_not_reported()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>(MaxLength = 10)]
            public readonly partial struct Sku;

            [ValueObject<int>(AllowDefault = true)]
            public readonly partial struct Sequence;

            public static class Use
            {
                public static object Run()
                {
                    var nullable = (Sku? sku = default) => sku?.ToString();
                    var absent = (Sku? sku = null) => sku?.ToString();
                    var allowed = (Sequence sequence = default) => sequence.ToString();
                    var allowedNew = (Sequence sequence = new()) => sequence.ToString();
                    var number = (int count = default) => count;
                    static string? Local(Sku? sku = default) => sku?.ToString();
                    static string Allowed(Sequence sequence = new()) => sequence.ToString();

                    return (nullable(), absent(), allowed(), allowedNew(), number(), Local(), Allowed());
                }
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The default value is reported wherever the lambda or the local function is declared - a field initializer, another
    /// lambda, a lambda's body - over a construction of a generic value object and an entity identifier alike, written
    /// in parentheses too, and only on the parameter that declares it. The semantic model binds a parenthesized
    /// expression to no operation, so the whole initializer is bound, as a method's parameter is handed it.
    /// </summary>
    [Fact]
    public async Task The_default_value_of_a_lambda_or_a_local_function_parameter_is_reported_wherever_it_is_declared()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>]
            public readonly partial struct Code<T>;

            [EntityId("acc")]
            public readonly partial struct AccountId;

            public static class Use
            {
                public static readonly Delegate InField = (AccountId id = new()) => id.ToString();

                public static object Run()
                {
                    var construction = (Code<int> code = default) => code.ToString();
                    var parenthesized = (AccountId id = (default)) => id.ToString();
                    var nested = () => (AccountId inner = default) => inner.ToString();
                    var second = (int count, AccountId id = new AccountId()) => id.ToString() + count;
                    static string Generic<T>(Code<T> code = default) => code.ToString();
                    var enclosing = () =>
                    {
                        string Inner(AccountId id = default(AccountId)) => id.ToString();
                        return Inner();
                    };

                    return (construction(), parenthesized(), nested()(), second(1), Generic<int>(), enclosing());
                }
            }
            """);

        Located(diagnostics).Should().Equal(
            ("new()", "public static readonly Delegate InField = (AccountId id = new()) => id.ToString();"),
            ("default", "var construction = (Code<int> code = default) => code.ToString();"),
            ("default", "var parenthesized = (AccountId id = (default)) => id.ToString();"),
            ("default", "var nested = () => (AccountId inner = default) => inner.ToString();"),
            ("new AccountId()", "var second = (int count, AccountId id = new AccountId()) => id.ToString() + count;"),
            ("default", "static string Generic<T>(Code<T> code = default) => code.ToString();"),
            ("default(AccountId)", "string Inner(AccountId id = default(AccountId)) => id.ToString();"));
    }

    /// <summary>
    /// Generated code is left alone, lambdas included: the Request Delegate Generator writes, for each handler whose
    /// parameter has a default value, a lambda declaring the same default, <c>(global::Sku arg0= default) =&gt; throw
    /// null!</c>, and the default is reported once, in the handler the author wrote. A member carrying
    /// <c>[GeneratedCode]</c> stands for the generated file here.
    /// </summary>
    [Fact]
    public async Task A_lambda_in_generated_code_is_not_analyzed()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>]
            public readonly partial struct Sku;

            public static class Interceptors
            {
                [System.CodeDom.Compiler.GeneratedCode("Microsoft.AspNetCore.Http.RequestDelegateGenerator", "10.0.0.0")]
                public static Delegate Cast() => string (Sku arg0 = default) => throw null!;

                public static Delegate Written() => string (Sku sku = default) => sku.ToString();
            }
            """);

        Located(diagnostics).Should().Equal(
            ("default", "public static Delegate Written() => string (Sku sku = default) => sku.ToString();"));
    }

    /// <summary>
    /// The absent value object is null, which is how absence is meant to be written, and a construction with an
    /// argument goes through the generated constructor, whose tag is no value object for <c>default</c> to leave
    /// uninitialized. A type parameter, an array, a primitive and a plain object are not value objects, and an array of
    /// value objects is a boundary the analyzer does not see: its elements are reported by <c>IsDefault</c> at run time,
    /// as documented.
    /// </summary>
    [Fact]
    public async Task Only_an_uninitialized_value_object_itself_is_reported()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>]
            public readonly partial struct Code
            {
                public static Code Of(string value) => new(value, default);
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

    /// <summary>
    /// A construction of a generic value object, or of one nested in a generic type, carries the annotation of its
    /// definition, and is no more validated by <c>default</c> than any other value object. A nullable one is still null.
    /// </summary>
    [Fact]
    public async Task An_uninitialized_construction_of_a_generic_value_object_is_reported()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>]
            public readonly partial struct Code<T>;

            public partial class Outer<T>
            {
                [ValueObject<int>]
                public readonly partial struct Count;
            }

            public static class Use
            {
                public static Code<int> Construction() => default(Code<int>);

                public static Outer<string>.Count Nested() => new Outer<string>.Count();

                public static Code<int>? Absent() => default(Code<int>?);
            }
            """);

        Located(diagnostics).Should().Equal(
            ("default(Code<int>)", "public static Code<int> Construction() => default(Code<int>);"),
            ("new Outer<string>.Count()", "public static Outer<string>.Count Nested() => new Outer<string>.Count();"));
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
    /// The length of a fixed-size buffer must be a constant, so a lambda written there is an error the compiler reports,
    /// and the semantic model binds the default values of its local function's parameters to no operation. An analyzer
    /// runs on the text as the author types it: there is nothing to report there, and it must not fail.
    /// </summary>
    [Fact]
    public async Task A_default_value_the_semantic_model_binds_to_no_operation_is_left_alone()
    {
        const string Source = """
            using System;
            using AdCodicem.ValueObjects.Annotations;

            namespace Test;

            [ValueObject<string>]
            public readonly partial struct Code;

            public unsafe struct Buffer
            {
                public fixed int Data[((Func<int>)(() => { static int Size(Code code = new()) => 1; return Size(); }))()];
            }
            """;

        var compilation = CSharpCompilationOf(Source, GeneratorHarness.LibraryReferences);
        var tree = compilation.SyntaxTrees.Single();
        var initializer = tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<ParameterSyntax>()
            .Single()
            .Default!;
        compilation.GetSemanticModel(tree).GetOperation(initializer, TestContext.Current.CancellationToken)
            .Should().BeNull("a fixed-size buffer's length is bound to no operation");

        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<UninitializedValueObjectAnalyzer>(Source);

        diagnostics.Should().BeEmpty();
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
            public readonly partial struct Currency
            {
                [KnownValue]
                public static readonly Currency Eur = Known("EUR");
            }

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
    /// A referenced assembly may hold a copy of an annotation under the same full name, internal to it - a library that
    /// embeds the annotations, a mismatched package. <c>GetTypeByMetadataName</c> then answers null, while the
    /// generator, matching attributes by name, still generates: the analyzer reads every type of the name.
    /// </summary>
    [Fact]
    public async Task A_value_object_is_reported_when_another_assembly_defines_the_annotations_too()
    {
        var copy = GeneratorHarness.Emit(AnnotationCopies, "Copies", GeneratorHarness.FrameworkReferences);

        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<UninitializedValueObjectAnalyzer>(
            """
            [ValueObject<string>]
            public readonly partial struct Code;

            [EntityId("acc")]
            public readonly partial struct AccountId;

            public static class Use
            {
                public static Code Missing() => default;

                public static AccountId Fresh() => new();
            }
            """,
            GeneratorHarness.LibraryReferences.Add(MetadataReference.CreateFromImage(copy)));

        Located(diagnostics).Should().Equal(
            ("default", "public static Code Missing() => default;"),
            ("new()", "public static AccountId Fresh() => new();"));
    }

    /// <summary>
    /// Copies of the annotations, internal to the assembly that declares them, under the full names of the real ones.
    /// </summary>
    internal const string AnnotationCopies = """
        namespace AdCodicem.ValueObjects.Annotations
        {
            [System.AttributeUsage(System.AttributeTargets.Struct)]
            internal sealed class ValueObjectAttribute<TValue> : System.Attribute;
        }

        namespace AdCodicem.ValueObjects.Identifiers
        {
            [System.AttributeUsage(System.AttributeTargets.Struct)]
            internal sealed class EntityIdAttribute(string prefix) : System.Attribute
            {
                public string Prefix { get; } = prefix;
            }
        }
        """;

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
