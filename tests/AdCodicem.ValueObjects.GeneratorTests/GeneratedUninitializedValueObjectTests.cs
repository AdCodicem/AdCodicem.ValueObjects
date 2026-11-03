using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// What <c>VO0032</c> reports: a value object created uninitialized, by <c>default</c> or a parameterless
/// <c>new</c>, in code that a listed generator wrote, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// Source generators never see each other's output, so Riok.Mapperly and the configuration binding generator,
/// finding a value object without the members this generator adds to it, write <c>new T()</c>. The shapes below are
/// copied from what Mapperly 4.3.1 and Microsoft.Extensions.Configuration.Binder 10.0 write, attribute included:
/// Mapperly marks each method with <c>[GeneratedCode]</c>, the binder the class holding them. The System.Text.Json
/// generator, whose output must stay silent, is the real one, taken from the targeting pack.
/// </para>
/// <para>
/// As for <c>VO0010</c>, the diagnostic is an error, so each test pins the exact diagnostics and where they point.
/// </para>
/// </remarks>
public sealed class GeneratedUninitializedValueObjectTests
{
    private const string ToolsKey = "adcodicem_value_objects.generated_code_tools";

    private const string BinderTool = "Microsoft.Extensions.Configuration.Binder.SourceGeneration";

    [Fact]
    public async Task What_Mapperly_writes_for_a_value_object_it_cannot_map_is_reported()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>(MaxLength = 2)]
            public readonly partial struct CountryCode;

            [ValueObject<int>]
            public readonly partial struct Quantity;

            public sealed class Order
            {
                public CountryCode Country { get; set; }

                public Quantity Quantity { get; set; }
            }

            public sealed class OrderDto
            {
                public string Country { get; set; } = "";

                public int Quantity { get; set; }
            }

            public partial class OrderMapper
            {
                public partial Order ToDomain(OrderDto dto);
            }

            public partial class OrderMapper
            {
                [global::System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "4.3.1.0")]
                public partial global::Test.Order ToDomain(global::Test.OrderDto dto)
                {
                    var target = new global::Test.Order();
                    target.Country = MapToCountryCode(dto.Country);
                    target.Quantity = MapToQuantity(dto.Quantity);
                    return target;
                }

                [global::System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "4.3.1.0")]
                private global::Test.CountryCode MapToCountryCode(string source)
                {
                    var target = new global::Test.CountryCode();
                    return target;
                }

                [global::System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "4.3.1.0")]
                private global::Test.Quantity MapToQuantity(int source)
                {
                    var target = new global::Test.Quantity();
                    return target;
                }
            }
            """);

        Located(diagnostics).Should().Equal(
            ("new global::Test.CountryCode()", "var target = new global::Test.CountryCode();"),
            ("new global::Test.Quantity()", "var target = new global::Test.Quantity();"));

        diagnostics.Should().AllSatisfy(diagnostic =>
        {
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
            diagnostic.Descriptor.Title.ToString(CultureInfo.InvariantCulture)
                .Should().Be("Value object created uninitialized by generated code");
        });
        // The analyzer runs concurrently, so the diagnostics come in no particular order.
        var first = diagnostics.MinBy(diagnostic => diagnostic.Location.SourceSpan.Start)!;
        first.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "'CountryCode' is created uninitialized by code that 'Riok.Mapperly' generated. Map it through a method "
            + "that calls Create, or keep the underlying type in what that generator binds.");
    }

    /// <summary>
    /// The binding generator marks the class it writes, not its methods, so the lookup walks out to it.
    /// </summary>
    [Fact]
    public async Task What_the_configuration_binding_generator_writes_is_reported_through_the_class_it_marks()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<int>]
            public readonly partial struct Quantity;

            public sealed class ShopOptions
            {
                public Quantity Quantity { get; set; }
            }

            [global::System.CodeDom.Compiler.GeneratedCode("Microsoft.Extensions.Configuration.Binder.SourceGeneration", "10.0.14.42308")]
            file static class BindingExtensions
            {
                public static void BindCore(ref global::Test.ShopOptions instance)
                {
                    global::Test.Quantity temp6 = instance.Quantity;
                    var temp7 = new global::Test.Quantity();
                    instance.Quantity = temp7;
                    temp6 = temp7;
                }

                public static global::Test.Quantity Unbound() => default(global::Test.Quantity);
            }
            """);

        Located(diagnostics).Should().Equal(
            ("new global::Test.Quantity()", "var temp7 = new global::Test.Quantity();"),
            ("default(global::Test.Quantity)", "public static global::Test.Quantity Unbound() => default(global::Test.Quantity);"));
        diagnostics.Should().AllSatisfy(diagnostic =>
            diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Contain($"by code that '{BinderTool}' generated"));
    }

    /// <summary>
    /// The System.Text.Json generator writes <c>ObjectCreator = () =&gt; new global::Test.Sku()</c> into the metadata of a
    /// context, legitimately, and marks the context with its own name, which the list does not hold. Naming it in the
    /// configuration reports that very line, which proves the silence is the list's doing, not a context left unread.
    /// </summary>
    [Fact]
    public async Task What_the_System_Text_Json_generator_writes_is_left_alone_unless_it_is_listed()
    {
        const string Source = """
            [ValueObject<string>]
            public readonly partial struct Sku;

            [global::System.Text.Json.Serialization.JsonSerializable(typeof(Sku))]
            internal partial class Catalog : global::System.Text.Json.Serialization.JsonSerializerContext
            {
            }
            """;

        var run = GeneratorHarness.Run(Source, withJsonGenerator: true);
        run.CompilationDiagnostics.Should().BeEmpty();

        var silent = await GeneratorHarness.RunAnalyzerAsync<GeneratedUninitializedValueObjectAnalyzer>(
            Source,
            withJsonGenerator: true);

        silent.Should().BeEmpty();

        var listed = await GeneratorHarness.RunAnalyzerAsync<GeneratedUninitializedValueObjectAnalyzer>(
            Source,
            options: Global("System.Text.Json.SourceGeneration"),
            withJsonGenerator: true);

        Located(listed).Should().Equal(("new global::Test.Sku()", "ObjectCreator = () => new global::Test.Sku(),"));
        listed.Single().GetMessage(CultureInfo.InvariantCulture)
            .Should().StartWith("'Sku' is created uninitialized by code that 'System.Text.Json.SourceGeneration' generated.");
    }

    /// <summary>
    /// The rejection paths of the code this generator writes assign <c>default</c>, and that code carries no
    /// <c>[GeneratedCode]</c>: whatever the configuration names, nothing in it is reported.
    /// </summary>
    [Fact]
    public async Task The_code_this_generator_writes_is_left_alone()
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

        GeneratorHarness.Run(Source).Files
            .Where(file => file.Text.Contains("result = default;", StringComparison.Ordinal))
            .Should().HaveCount(4, "every value object and identifier assigns default on its rejection paths");

        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<GeneratedUninitializedValueObjectAnalyzer>(
            Source,
            options: Global("AdCodicem.ValueObjects.Generators, AdCodicem.ValueObjects"));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task The_configuration_adds_tools_to_the_list_and_keeps_the_defaults()
    {
        const string Source = """
            [ValueObject<string>]
            public readonly partial struct Code;

            public static class Generated
            {
                [System.CodeDom.Compiler.GeneratedCode("My.Generator", "1.0")]
                public static Code Mine() => new Code();

                [System.CodeDom.Compiler.GeneratedCode("Other.Generator", "2.0")]
                public static Code Other() => default;

                [System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "4.3.1.0")]
                public static Code Mapped() => new();

                [System.CodeDom.Compiler.GeneratedCode("Unlisted.Generator", "1.0")]
                public static Code Unlisted() => new Code();
            }
            """;

        Located(await RunAsync(Source)).Should().Equal(("new()", "public static Code Mapped() => new();"));

        // Entries are trimmed, and an empty one, a trailing comma or a doubled one, is no tool.
        Located(await RunAsync(Source, Global(" My.Generator ,Other.Generator,, "))).Should().Equal(
            ("new Code()", "public static Code Mine() => new Code();"),
            ("default", "public static Code Other() => default;"),
            ("new()", "public static Code Mapped() => new();"));
    }

    /// <summary>
    /// A section of an <c>.editorconfig</c> reaches a generated file only when the intermediate output directory lies
    /// beneath it, so the key is read from the global options, which a <c>.globalconfig</c> sets, and from nowhere else.
    /// </summary>
    [Fact]
    public async Task The_configuration_is_read_from_the_global_options_only()
    {
        const string Source = """
            [ValueObject<string>]
            public readonly partial struct Code;

            public static class Generated
            {
                [System.CodeDom.Compiler.GeneratedCode("My.Generator", "1.0")]
                public static Code Mine() => new Code();
            }
            """;

        var fromASection = new AnalyzerConfiguration(perFile: new Dictionary<string, string> { [ToolsKey] = "My.Generator" });

        (await RunAsync(Source, fromASection)).Should().BeEmpty();
        Located(await RunAsync(Source, Global("My.Generator")))
            .Should().Equal(("new Code()", "public static Code Mine() => new Code();"));
    }

    [Fact]
    public async Task A_tool_is_matched_in_full_and_in_its_own_case()
    {
        var diagnostics = await RunAsync(
            """
            [ValueObject<string>]
            public readonly partial struct Code;

            public static class Generated
            {
                [System.CodeDom.Compiler.GeneratedCode("riok.mapperly", "4.3.1.0")]
                public static Code Lower() => new Code();

                [System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly.Extra", "1.0")]
                public static Code Longer() => new Code();

                [System.CodeDom.Compiler.GeneratedCode("Riok", "1.0")]
                public static Code Prefix() => new Code();

                [System.CodeDom.Compiler.GeneratedCode(" Riok.Mapperly", "1.0")]
                public static Code Spaced() => new Code();

                [System.CodeDom.Compiler.GeneratedCode("My.Generator", "1.0")]
                public static Code NotAPrefixEither() => new Code();
            }
            """,
            Global("My"));

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The lookup walks from the member out through the types containing it, and the first <c>[GeneratedCode]</c> it
    /// meets decides: an accessor answers for its property, a lambda or a local function for the member around it, and
    /// a member another tool marked, inside a type a listed one marked, is that other tool's.
    /// </summary>
    [Fact]
    public async Task The_nearest_generated_code_attribute_names_the_tool()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>]
            public readonly partial struct Code;

            [System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "4.3.1.0")]
            public static class Marked
            {
                public static readonly Code Field = new Code();

                public static Code Property => new Code();

                public static Code Method() => default(Code);

                public static Func<Code> Lambda() => () => new Code();

                public static class Nested
                {
                    public static Code Deep() => new Code();
                }

                [System.CodeDom.Compiler.GeneratedCode("System.Text.Json.SourceGeneration", "10.0.14.42308")]
                public static Code Json() => new Code();
            }

            public static class Members
            {
                [System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "4.3.1.0")]
                public static Code Property => new Code();

                [System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "4.3.1.0")]
                public static Code Local()
                {
                    return Make();

                    static Code Make() => default;
                }

                public static Code Unmarked() => new Code();
            }
            """);

        Located(diagnostics).Should().Equal(
            ("new Code()", "public static readonly Code Field = new Code();"),
            ("new Code()", "public static Code Property => new Code();"),
            ("default(Code)", "public static Code Method() => default(Code);"),
            ("new Code()", "public static Func<Code> Lambda() => () => new Code();"),
            ("new Code()", "public static Code Deep() => new Code();"),
            ("new Code()", "public static Code Property => new Code();"),
            ("default", "static Code Make() => default;"));
    }

    /// <summary>
    /// What <c>VO0010</c> leaves alone in user code, this leaves alone in generated code: a type declaring
    /// <c>AllowDefault</c>, an absent value object, a construction with an argument, a type that is not a value object,
    /// and the <c>default</c> the compiler supplies for an omitted argument.
    /// </summary>
    [Fact]
    public async Task Only_an_uninitialized_value_object_refusing_its_default_is_reported()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<int>(AllowDefault = true)]
            public readonly partial struct Sequence;

            [ValueObject<string>]
            public readonly partial struct Code;

            public sealed class Order;

            public static class Mapper
            {
                public static int Count(Code code = default) => 0;

                [System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "4.3.1.0")]
                public static int Map()
                {
                    var sequence = new Sequence();
                    var initial = default(Sequence);
                    var absent = default(Code?);
                    var none = new Code?();
                    var order = new Order();
                    var codes = new Code[3];
                    var number = default(int);
                    var code = Code.Create("A");
                    var generic = Make<Code>();

                    return Count() + sequence.Value + initial.Value + (absent ?? code).Value.Length + none.GetHashCode()
                        + order.GetHashCode() + codes.Length + number + generic.GetHashCode();
                }

                [System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "4.3.1.0")]
                private static T Make<T>() where T : new() => new T();
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task An_entity_identifier_and_a_construction_of_a_generic_value_object_are_reported()
    {
        var diagnostics = await RunAsync("""
            [EntityId("acc")]
            public readonly partial struct AccountId;

            [EntityId("dev", AllowDefault = true)]
            public readonly partial struct DeviceId;

            [ValueObject<string>]
            public readonly partial struct Label<TOwner>;

            [System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "4.3.1.0")]
            public static class Mapper
            {
                public static AccountId Account() => new AccountId();

                public static DeviceId Device() => new DeviceId();

                public static Label<int> Label() => default(Label<int>);
            }
            """);

        Located(diagnostics).Should().Equal(
            ("new AccountId()", "public static AccountId Account() => new AccountId();"),
            ("default(Label<int>)", "public static Label<int> Label() => default(Label<int>);"));
    }

    /// <summary>
    /// Where the two diagnostics meet: user code is <c>VO0010</c>'s, and code a listed tool marked is this one's. Roslyn
    /// takes a member carrying <c>[GeneratedCode]</c> for generated code, which <c>VO0010</c> does not analyze, so a
    /// value object is never reported twice.
    /// </summary>
    [Fact]
    public async Task User_code_is_left_to_VO0010_and_no_expression_is_reported_twice()
    {
        const string Source = """
            [ValueObject<string>]
            public readonly partial struct Code;

            public static class Use
            {
                public static Code Written() => new Code();

                [System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "4.3.1.0")]
                public static Code Mapped() => default(Code);
            }
            """;

        var generated = await RunAsync(Source);
        var written = await GeneratorHarness.RunAnalyzerAsync<UninitializedValueObjectAnalyzer>(Source);

        Located(generated).Should().Equal(("default(Code)", "public static Code Mapped() => default(Code);"));
        written.Should().ContainSingle().Which.Id.Should().Be("VO0010");
        written.Single().Location.SourceSpan.Should().NotBe(generated.Single().Location.SourceSpan);
    }

    /// <summary>
    /// The attribute is matched by its full name, wherever it is declared. One the compiler cannot bind, missing an
    /// argument, names no tool, and neither does one naming none, nor an attribute of the same name in another namespace.
    /// </summary>
    [Fact]
    public async Task The_attribute_is_matched_by_its_full_name_and_must_name_a_tool()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<GeneratedUninitializedValueObjectAnalyzer>("""
            namespace System.CodeDom.Compiler
            {
                [System.AttributeUsage(System.AttributeTargets.All)]
                internal sealed class GeneratedCodeAttribute(string tool, string version) : System.Attribute
                {
                    public string Tool { get; } = tool;

                    public string Version { get; } = version;
                }
            }

            namespace Elsewhere
            {
                [System.AttributeUsage(System.AttributeTargets.All)]
                internal sealed class GeneratedCodeAttribute(string tool) : System.Attribute
                {
                    public string Tool { get; } = tool;
                }
            }

            namespace Test
            {
                [AdCodicem.ValueObjects.Annotations.ValueObject<string>]
                public readonly partial struct Code;

                public static class Generated
                {
                    [System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "4.3.1.0")]
                    public static Code Copy() => new Code();

                    [System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly")]
                    public static Code Unbound() => new Code();

                    [System.CodeDom.Compiler.GeneratedCode(null, "1.0")]
                    public static Code Nameless() => new Code();

                    [Elsewhere.GeneratedCode("Riok.Mapperly")]
                    public static Code Namesake() => new Code();
                }
            }
            """);

        Located(diagnostics).Should().Equal(("new Code()", "public static Code Copy() => new Code();"));
    }

    [Fact]
    public async Task Nothing_is_reported_where_the_library_is_not_referenced()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<GeneratedUninitializedValueObjectAnalyzer>(
            """
            namespace Plain;

            public readonly struct Point;

            public static class Mapper
            {
                [System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "4.3.1.0")]
                public static Point Map() => new Point();
            }
            """,
            GeneratorHarness.FrameworkReferences);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The diagnostic is reported in a file the consumer cannot edit, so its link must land on the fix: the section of
    /// the diagnostics reference its anchor names.
    /// </summary>
    [Fact]
    public void The_help_link_lands_on_the_section_of_the_diagnostics_reference_that_holds_the_fix()
    {
        var link = new Uri(GeneratedUninitializedValueObjectAnalyzer.CreatedByGeneratedCode.HelpLinkUri);

        link.GetLeftPart(UriPartial.Path)
            .Should().Be("https://adcodicem.github.io/AdCodicem.ValueObjects/docs/reference/diagnostics");

        var page = File.ReadAllLines(Path.Combine(RepositoryRoot(), "website", "docs", "reference", "diagnostics.md"));
        page.Should().Contain("slug: /reference/diagnostics");
        page.Select(Anchor).Should().Contain(link.Fragment.TrimStart('#'));
    }

    /// <summary>
    /// The compiler never hands an analyzer a null context, so the guard is only reached by a direct call, which
    /// it tolerates.
    /// </summary>
    [Fact]
    public void The_analyzer_initialized_without_a_context_does_nothing()
    {
        var analyzer = new GeneratedUninitializedValueObjectAnalyzer();

        analyzer.Invoking(target => target.Initialize(null!)).Should().NotThrow();
    }

    private static AnalyzerConfiguration Global(string tools)
        => new(global: new Dictionary<string, string> { [ToolsKey] = tools });

    /// <summary>
    /// Runs the analyzer over a snippet that is otherwise valid C#, so that what it reports is never a side effect of
    /// code the compiler would refuse anyway.
    /// </summary>
    private static Task<ImmutableArray<Diagnostic>> RunAsync(string source, AnalyzerConfiguration? options = null)
    {
        GeneratorHarness.Run(source).CompilationDiagnostics.Should().BeEmpty();

        return GeneratorHarness.RunAnalyzerAsync<GeneratedUninitializedValueObjectAnalyzer>(source, options: options);
    }

    /// <summary>
    /// Gets each diagnostic as the text it spans and the line it starts on, trimmed, in source order. Every one is
    /// expected to be <c>VO0032</c>, which is checked here once for all the tests.
    /// </summary>
    private static List<(string Text, string Line)> Located(ImmutableArray<Diagnostic> diagnostics)
    {
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == "VO0032");

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

    /// <summary>
    /// Gets the anchor Docusaurus gives a heading: lower case, punctuation dropped, spaces as hyphens. Any other line
    /// gives none.
    /// </summary>
    private static string Anchor(string line)
    {
        if (!line.StartsWith("## ", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var heading = line[3..].Trim().ToLowerInvariant();

        return string.Concat(heading.Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-'))
            .Replace(' ', '-');
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AdCodicem.ValueObjects.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
