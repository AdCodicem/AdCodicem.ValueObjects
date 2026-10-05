using System.Collections.Immutable;
using System.IO;
using System.Xml.Linq;
using AdCodicem.ValueObjects.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// What <c>VO0033</c> reports, a value object the Request Delegate Generator cannot see as parsable, and what its code
/// fix writes.
/// </summary>
/// <remarks>
/// <para>
/// The RDG, a source generator, reads a value object of its own project without the partial this generator adds, so
/// without <c>IParsable&lt;T&gt;</c>, and binds it from the request body. An interface listed on the consumer's own
/// declaration is seen: a probe built with the RDG on classified a parameter listing <c>IValueObject</c>,
/// <c>INumericValueObject</c>, <c>IEntityId</c> or <c>ISpanParsable</c>, generic and nested ones included, as
/// <c>IsParsable = True, Source = RouteOrQuery</c>, and the bare declaration as <c>JsonBodyOrService</c>.
/// </para>
/// <para>
/// The analyzer runs over the compilation with the generator's output, as the compiler does, so every test also shows
/// that the generated partial, which does list the contract, is not counted. The gate, the build property the
/// package's props make visible and ASP.NET Core's endpoint routing, is given here by a <c>.globalconfig</c> entry and
/// a stand-in assembly declaring <c>IEndpointRouteBuilder</c>.
/// </para>
/// </remarks>
public sealed class RequestDelegateGeneratorTests
{
    private const string EnabledKey = "build_property.EnableRequestDelegateGenerator";

    /// <summary>A stand-in for Microsoft.AspNetCore.Routing, which a project mapping minimal APIs references.</summary>
    private static readonly MetadataReference Routing = MetadataReference.CreateFromImage(GeneratorHarness.Emit(
        """
        namespace Microsoft.AspNetCore.Routing
        {
            public interface IEndpointRouteBuilder
            {
            }
        }
        """,
        "Microsoft.AspNetCore.Routing",
        GeneratorHarness.FrameworkReferences));

    private static readonly ImmutableArray<MetadataReference> WithRouting = GeneratorHarness.LibraryReferences.Add(Routing);

    private static readonly AnalyzerConfiguration Enabled = Property("true");

    [Fact]
    public async Task A_value_object_listing_no_parsable_interface_is_reported_where_the_RDG_runs()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>(MaxLength = 10)]
            public readonly partial struct Sku;
            """);

        var diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0033");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Should().Be(
            "The Request Delegate Generator cannot see what the generator adds to 'Sku', so a minimal API would bind it "
            + "from the request body rather than from a route value or the query string. List 'IValueObject<Sku, string>' "
            + "on its declaration, or declare it in another project.");
        Located(diagnostic).Should().Be(("Sku", "public readonly partial struct Sku;"));
    }

    /// <summary>
    /// The SDK sets <c>EnableRequestDelegateGenerator</c> under <c>PublishAot</c> and <c>PublishTrimmed</c>, in a
    /// console application too, where no RDG runs: the compilation must also reference endpoint routing.
    /// </summary>
    [Theory]
    [InlineData("true", true, true)]
    [InlineData("True", true, true)]
    [InlineData(" true ", true, true)]
    [InlineData("false", true, false)]
    [InlineData("", true, false)]
    [InlineData(null, true, false)]
    [InlineData("true", false, false)]
    public async Task It_is_reported_only_where_the_property_is_true_and_endpoint_routing_is_referenced(
        string? property,
        bool routing,
        bool reported)
    {
        var options = property is null ? new AnalyzerConfiguration() : Property(property);

        var diagnostics = await RunAsync(
            """
            [ValueObject<string>]
            public readonly partial struct Sku;
            """,
            options,
            routing ? WithRouting : GeneratorHarness.LibraryReferences);

        diagnostics.Should().HaveCount(reported ? 1 : 0);
    }

    /// <summary>
    /// Any interface that is, or extends, <c>IParsable</c> of the type itself is enough for the RDG, on any declaration
    /// of the type the consumer writes.
    /// </summary>
    [Theory]
    [InlineData("""
        [ValueObject<string>]
        public readonly partial struct Sku : IValueObject<Sku, string>;
        """)]
    [InlineData("""
        [ValueObject<string>]
        public readonly partial struct Sku : ISpanParsable<Sku>;
        """)]
    [InlineData("""
        [ValueObject<string>]
        public readonly partial struct Sku : IParsable<Sku>;
        """)]
    [InlineData("""
        [ValueObject<string>]
        public readonly partial struct Sku : IValueObjectValidator<string>, IValueObject<Sku, string>
        {
            public static ValidationResult ValidateValue(in string value) => ValidationResult.Success;
        }
        """)]
    [InlineData("""
        [ValueObject<string>]
        public readonly partial struct Sku;

        public readonly partial struct Sku : IValueObject<Sku, string>;
        """)]
    [InlineData("""
        [ValueObject<int>(Arithmetic = true)]
        public readonly partial struct Quantity : INumericValueObject<Quantity, int>;
        """)]
    [InlineData("""
        [EntityId("cus")]
        public readonly partial struct CustomerId : IEntityId<CustomerId>;
        """)]
    [InlineData("""
        public sealed class Order;

        [ValueObject<string>]
        public readonly partial struct Code<TOwner> : IValueObject<Code<TOwner>, string>;
        """)]
    [InlineData("""
        public static partial class Catalog
        {
            [ValueObject<string>]
            public readonly partial struct Sku : IValueObject<Sku, string>;
        }
        """)]
    [InlineData("""
        public interface ISkuLike<TSelf> : IParsable<TSelf>
            where TSelf : IParsable<TSelf>;

        [ValueObject<string>]
        public readonly partial struct Sku : ISkuLike<Sku>;
        """)]
    public async Task An_interface_bringing_IParsable_of_the_type_on_its_own_declaration_silences_it(string source)
    {
        var diagnostics = await RunAsync(source);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// What a declaration lists counts only if it brings <c>IParsable</c> of the type itself: another interface the
    /// generator implements anyway does not, nor an interface of the same name in another namespace, nor one listed on
    /// a struct nested in the value object.
    /// </summary>
    [Theory]
    [InlineData("""
        [ValueObject<string>]
        public readonly partial struct Sku : IEquatable<Sku>, ISpanFormattable;
        """)]
    [InlineData("""
        [ValueObject<string>]
        public readonly partial struct Sku : IValueObjectValidator<string>
        {
            public static ValidationResult ValidateValue(in string value) => ValidationResult.Success;
        }
        """)]
    [InlineData("""
        public interface IParsable<TSelf>;

        [ValueObject<string>]
        public readonly partial struct Sku : IParsable<Sku>;
        """)]
    [InlineData("""
        [ValueObject<string>]
        public readonly partial struct Sku
        {
            public readonly struct Reader : IParsable<Sku>
            {
                public static Sku Parse(string s, IFormatProvider? provider) => Sku.Parse(s, provider);

                public static bool TryParse(string? s, IFormatProvider? provider, out Sku result)
                    => Sku.TryParse(s, provider, out result);
            }
        }
        """)]
    public async Task An_interface_that_brings_no_IParsable_of_the_type_leaves_it_reported(string source)
    {
        var diagnostics = await RunAsync(source);

        diagnostics.Should().ContainSingle().Which.Id.Should().Be("VO0033");
    }

    /// <summary>
    /// The message names the contract the generator implements, which is what the code fix lists.
    /// </summary>
    [Theory]
    [InlineData("""
        [ValueObject<Guid>]
        public readonly partial struct OrderId;
        """, "OrderId", "IValueObject<OrderId, Guid>")]
    [InlineData("""
        [ValueObject<int>(Arithmetic = true)]
        public readonly partial struct Quantity;
        """, "Quantity", "INumericValueObject<Quantity, int>")]
    [InlineData("""
        [ValueObject<int>(Arithmetic = false)]
        public readonly partial struct Quantity : IValueObjectExample<Quantity>
        {
            public static Quantity Example => Create(5);
        }
        """, "Quantity", "IValueObject<Quantity, int>")]
    [InlineData("""
        [EntityId("cus")]
        public readonly partial struct CustomerId;
        """, "CustomerId", "IEntityId<CustomerId>")]
    [InlineData("""
        [ValueObject<string>]
        public readonly partial struct Code<TOwner>;
        """, "Code<TOwner>", "IValueObject<Code<TOwner>, string>")]
    [InlineData("""
        public static partial class Catalog
        {
            [ValueObject<string>]
            public readonly partial struct Sku;
        }
        """, "Catalog.Sku", "IValueObject<Catalog.Sku, string>")]
    public async Task The_message_names_the_contract_the_generator_implements(string source, string type, string contract)
    {
        var diagnostics = await RunAsync(source);

        diagnostics.Should().ContainSingle().Which.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            .Should().Contain($"adds to '{type}'").And.Contain($"List '{contract}' on its declaration");
    }

    /// <summary>
    /// Arithmetic over a type that is not a number is <c>VO0007</c>, and the generator then implements the plain
    /// contract, which is what the message names.
    /// </summary>
    [Fact]
    public async Task Arithmetic_over_a_type_that_is_not_a_number_names_the_plain_contract()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<RequestDelegateGeneratorAnalyzer>(
            """
            [ValueObject<string>(Arithmetic = true)]
            public readonly partial struct Name;
            """,
            WithRouting,
            Enabled);

        diagnostics.Should().ContainSingle().Which.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            .Should().Contain("List 'IValueObject<Name, string>'");
    }

    /// <summary>
    /// An <c>Arithmetic</c> argument the compiler refuses, which the IDE analyzes all the same, holds no value: the
    /// generator reads it as <see langword="false"/> and implements the plain contract, which is what the message names.
    /// </summary>
    [Fact]
    public async Task An_Arithmetic_argument_the_compiler_refuses_names_the_plain_contract()
    {
        const string source = """
            [ValueObject<int>(Arithmetic = 1)]
            public readonly partial struct Quantity;
            """;

        var run = GeneratorHarness.Run(source);
        run.CompilationDiagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("CS0029");
        run.SingleValueObject.Should().Contain("partial struct Quantity : global::AdCodicem.ValueObjects.IValueObject<");

        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<RequestDelegateGeneratorAnalyzer>(source, WithRouting, Enabled);

        diagnostics.Should().ContainSingle().Which.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            .Should().Contain("List 'IValueObject<Quantity, int>'");
    }

    /// <summary>
    /// Where the generator implements no contract, it writes no <c>IParsable&lt;T&gt;</c> the RDG could miss, and the
    /// type already carries an error of its own.
    /// </summary>
    [Theory]
    [InlineData("public readonly partial struct Plain;")]
    [InlineData("""
        [ValueObject<object>]
        public readonly partial struct Unsupported;
        """)]
    [InlineData("""
        [ValueObject<string>]
        public sealed partial class Written;
        """)]
    [InlineData("""
        [ValueObject<string>]
        public partial struct Mutable;
        """)]
    [InlineData("""
        [ValueObject<string>]
        public readonly partial record struct Recorded;
        """)]
    [InlineData("""
        [ValueObject<string>]
        public readonly ref partial struct OnTheStack;
        """)]
    [InlineData("""
        [ValueObject<string>]
        [EntityId("cus")]
        public readonly partial struct Both;
        """)]
    public async Task Nothing_is_reported_for_a_type_the_generator_implements_no_contract_on(string source)
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<RequestDelegateGeneratorAnalyzer>(source, WithRouting, Enabled);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A base the compiler refuses, a type parameter, brings nothing, and the value object is still reported.
    /// </summary>
    [Fact]
    public async Task A_base_that_is_no_interface_brings_nothing()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<RequestDelegateGeneratorAnalyzer>(
            """
            [ValueObject<string>]
            public readonly partial struct Code<TOwner> : TOwner;
            """,
            WithRouting,
            Enabled);

        diagnostics.Should().ContainSingle().Which.Id.Should().Be("VO0033");
    }

    /// <summary>
    /// The analyzer ships inside the package, but nothing stops a project from loading it without the contracts, with
    /// annotations of the same names declared in its own source: no contract can then be resolved for a value object or
    /// an entity identifier, so there is none the fix could list, and nothing is reported. The same source compiled
    /// against the contracts is reported.
    /// </summary>
    [Fact]
    public async Task Nothing_is_reported_where_the_contracts_are_not_referenced()
    {
        const string source = UninitializedValueObjectTests.AnnotationCopies + """

            namespace Shop
            {
                using AdCodicem.ValueObjects.Annotations;
                using AdCodicem.ValueObjects.Identifiers;

                [ValueObject<string>]
                public readonly partial struct Sku;

                [EntityId("cus")]
                public readonly partial struct CustomerId;
            }
            """;

        var without = await GeneratorHarness.RunAnalyzerAsync<RequestDelegateGeneratorAnalyzer>(
            source,
            GeneratorHarness.FrameworkReferences.Add(Routing),
            Enabled);
        var with = await GeneratorHarness.RunAnalyzerAsync<RequestDelegateGeneratorAnalyzer>(source, WithRouting, Enabled);

        without.Should().BeEmpty();
        with.Select(diagnostic => diagnostic.Id).Should().Equal("VO0033", "VO0033");
    }

    /// <summary>
    /// A referenced assembly holding internal copies of the contracts, and of <c>IParsable&lt;T&gt;</c>, under their full
    /// names hides nothing: the generated code binds the public ones, the only ones it can reach, and the RDG still
    /// misses them, so every value object is reported as it is without the copies.
    /// </summary>
    [Fact]
    public async Task Internal_copies_of_the_contracts_in_a_referenced_assembly_hide_no_value_object()
    {
        var copies = MetadataReference.CreateFromImage(GeneratorHarness.Emit(
            """
            namespace System
            {
                internal interface IParsable<TSelf>
                {
                }
            }

            namespace AdCodicem.ValueObjects
            {
                internal interface IValueObject<TSelf, TValue>
                {
                }

                internal interface INumericValueObject<TSelf, TValue>
                {
                }
            }

            namespace AdCodicem.ValueObjects.Identifiers
            {
                internal interface IEntityId<TSelf>
                {
                }
            }
            """,
            "Copies",
            GeneratorHarness.FrameworkReferences));

        var diagnostics = await RunAsync(
            """
            using AdCodicem.ValueObjects.Identifiers;

            [ValueObject<string>]
            public readonly partial struct Sku;

            [ValueObject<int>(Arithmetic = true)]
            public readonly partial struct Quantity;

            [EntityId("cus")]
            public readonly partial struct CustomerId;
            """,
            references: WithRouting.Add(copies));

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("VO0033", "VO0033", "VO0033");
    }

    /// <summary>
    /// A referenced assembly declaring the contract publicly, beside the real one, leaves the generated code ambiguous
    /// (CS0433): there is no one contract the fix could list, so nothing is reported, where the real one alone is.
    /// </summary>
    [Fact]
    public async Task Two_public_declarations_of_the_contract_leave_nothing_to_list()
    {
        var copy = MetadataReference.CreateFromImage(GeneratorHarness.Emit(
            """
            namespace AdCodicem.ValueObjects
            {
                public interface IValueObject<TSelf, TValue>
                {
                }
            }
            """,
            "PublicCopy",
            GeneratorHarness.FrameworkReferences));
        const string source = """
            [ValueObject<string>]
            public readonly partial struct Sku;
            """;

        var ambiguous = await RunAsync(source, references: WithRouting.Add(copy));
        var single = await RunAsync(source);

        ambiguous.Should().BeEmpty();
        single.Should().ContainSingle().Which.Id.Should().Be("VO0033");
    }

    /// <summary>
    /// A contract the project declares in its own source is the one the compiler binds the generated code to (CS0436),
    /// so it is the one the declaration has to list.
    /// </summary>
    [Fact]
    public async Task A_contract_declared_in_the_project_itself_is_the_one_to_list()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<RequestDelegateGeneratorAnalyzer>(
            """
            namespace AdCodicem.ValueObjects
            {
                public interface IValueObject<TSelf, TValue> : System.IParsable<TSelf>
                    where TSelf : IValueObject<TSelf, TValue>
                {
                }
            }

            namespace Shop
            {
                using AdCodicem.ValueObjects.Annotations;

                [ValueObject<string>]
                public readonly partial struct Listed : AdCodicem.ValueObjects.IValueObject<Listed, string>;

                [ValueObject<string>]
                public readonly partial struct Bare;
            }
            """,
            WithRouting,
            Enabled);

        diagnostics.Should().ContainSingle().Which.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Should().Contain("Bare");
    }

    /// <summary>
    /// A value object compiled into another assembly reaches the RDG as metadata, where its interfaces are.
    /// </summary>
    [Fact]
    public async Task A_value_object_of_a_referenced_assembly_is_not_reported()
    {
        var domain = MetadataReference.CreateFromImage(GeneratorHarness.Emit(
            """
            [ValueObject<string>]
            public readonly partial struct CountryCode;
            """,
            "Domain"));

        const string endpoints = """
            public static class Endpoints
            {
                public static string Describe(CountryCode code) => code.ToString();
            }
            """;

        // Emitting asserts that the snippet compiles, the value object resolved from the other assembly.
        GeneratorHarness.Emit(endpoints, "Api", WithRouting.Add(domain));

        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<RequestDelegateGeneratorAnalyzer>(
            endpoints,
            WithRouting.Add(domain),
            Enabled);

        diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("""
        [ValueObject<string>(MaxLength = 10)]
        public readonly partial struct Sku;
        """, "public readonly partial struct Sku : IValueObject<Sku, string>;")]
    [InlineData("""
        [ValueObject<string>]
        public readonly partial struct Sku : IValueObjectValidator<string>
        {
            public static ValidationResult ValidateValue(in string value) => ValidationResult.Success;
        }
        """, "public readonly partial struct Sku : IValueObjectValidator<string>, IValueObject<Sku, string>")]
    [InlineData("""
        [ValueObject<int>(Arithmetic = true)]
        public readonly partial struct Quantity;
        """, "public readonly partial struct Quantity : INumericValueObject<Quantity, int>;")]
    [InlineData("""
        [EntityId("cus")]
        public readonly partial struct CustomerId;
        """, "public readonly partial struct CustomerId : IEntityId<CustomerId>;")]
    [InlineData("""
        [ValueObject<string>]
        public readonly partial struct Code<TOwner>;
        """, "public readonly partial struct Code<TOwner> : IValueObject<Code<TOwner>, string>;")]
    [InlineData("""
        [ValueObject<string>]
        public readonly partial struct Sku;

        public readonly partial struct Sku
        {
            public int Length => Value.Length;
        }
        """, "public readonly partial struct Sku : IValueObject<Sku, string>;")]
    public async Task The_fix_lists_the_contract_on_the_annotated_declaration(string source, string fixedLine)
    {
        var fixedSource = await FixAsync(source);

        Lines(fixedSource).Should().Contain(fixedLine);
        await ShouldBeSilentAndCompileAsync(fixedSource);
    }

    /// <summary>
    /// A file that does not import <c>AdCodicem.ValueObjects</c>, only the annotations, gets the import with the
    /// interface rather than a qualified name.
    /// </summary>
    [Fact]
    public async Task The_fix_imports_the_namespace_of_the_contract_when_the_file_lacks_it()
    {
        var fixedSource = await FixAsync("""
            using AdCodicem.ValueObjects.Annotations;

            namespace Shop;

            [ValueObject<string>]
            public readonly partial struct Sku;
            """);

        var lines = Lines(fixedSource);
        lines.Should().Contain("using AdCodicem.ValueObjects;");
        lines.Should().Contain("public readonly partial struct Sku : IValueObject<Sku, string>;");
        await ShouldBeSilentAndCompileAsync(fixedSource);
    }

    [Fact]
    public async Task Fixing_all_in_a_document_lists_each_contract()
    {
        var fixedSource = await GeneratorHarness.FixAllAsync<RequestDelegateGeneratorAnalyzer, ListContractCodeFixProvider>(
            """
            [ValueObject<string>]
            public readonly partial struct Sku;

            [ValueObject<int>(Arithmetic = true)]
            public readonly partial struct Quantity;
            """,
            WithRouting,
            Enabled);

        var lines = Lines(fixedSource);
        lines.Should().Contain("public readonly partial struct Sku : IValueObject<Sku, string>;");
        lines.Should().Contain("public readonly partial struct Quantity : INumericValueObject<Quantity, int>;");
        await ShouldBeSilentAndCompileAsync(fixedSource);
    }

    [Fact]
    public void The_fix_is_offered_for_the_diagnostic_and_fixes_all_in_one_batch()
    {
        var fix = new ListContractCodeFixProvider();

        fix.FixableDiagnosticIds.Should().Equal("VO0033");
        fix.GetFixAllProvider().Should().BeSameAs(Microsoft.CodeAnalysis.CodeFixes.WellKnownFixAllProviders.BatchFixer);
    }

    /// <summary>
    /// The analyzer reports only on the name of a value object it computed a contract for, so a diagnostic anywhere
    /// else is only met in a direct call, where nothing is offered.
    /// </summary>
    [Theory]
    [InlineData("using AdCodicem.ValueObjects;")]
    [InlineData("Plain")]
    [InlineData("Sku")]
    public async Task Nothing_is_offered_where_no_value_object_takes_a_contract(string at)
    {
        const string source = """
            public readonly struct Plain;

            [ValueObject<object>]
            public readonly partial struct Sku;
            """;

        var titles = await GeneratorHarness.OfferedFixTitlesAsync(
            new ListContractCodeFixProvider(),
            source,
            RequestDelegateGeneratorAnalyzer.HiddenFromRequestDelegateGenerator,
            text => new TextSpan(text.IndexOf(at, StringComparison.Ordinal), at.Length));

        titles.Should().BeEmpty();
    }

    [Fact]
    public async Task The_fix_names_the_interface_it_lists()
    {
        var titles = await GeneratorHarness.OfferedFixTitlesAsync(
            new ListContractCodeFixProvider(),
            """
            [ValueObject<int>(Arithmetic = true)]
            public readonly partial struct Quantity;
            """,
            RequestDelegateGeneratorAnalyzer.HiddenFromRequestDelegateGenerator,
            text => new TextSpan(text.IndexOf("Quantity", StringComparison.Ordinal), "Quantity".Length));

        titles.Should().Equal("List INumericValueObject<Quantity, int> on the declaration");
    }

    /// <summary>
    /// The analyzer reads the property under the name the package's build props make visible: a mismatch would leave it
    /// silent everywhere, with nothing failing.
    /// </summary>
    [Fact]
    public void The_package_makes_the_property_it_reads_visible_to_the_compiler_in_every_project_that_gets_the_analyzer()
    {
        var props = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "AdCodicem.ValueObjects", "build", "AdCodicem.ValueObjects.props"));
        var visible = props.Descendants("CompilerVisibleProperty").Select(item => (string?)item.Attribute("Include"));

        visible.Should().ContainSingle().Which.Should().Be(EnabledKey["build_property.".Length..]);
        RequestDelegateGeneratorAnalyzer.EnabledKey.Should().Be(EnabledKey);

        var package = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "AdCodicem.ValueObjects", "AdCodicem.ValueObjects.csproj"));
        var packed = package.Descendants("None")
            .Single(item => (string?)item.Attribute("Include") == @"build\AdCodicem.ValueObjects.props");

        ((string?)packed.Attribute("PackagePath")).Should().Be(@"build\;buildTransitive\");
    }

    /// <summary>
    /// The help link lands on the section of the ASP.NET Core page that states the rule.
    /// </summary>
    [Fact]
    public void The_help_link_lands_on_the_section_that_states_the_rule()
    {
        var link = new Uri(RequestDelegateGeneratorAnalyzer.HiddenFromRequestDelegateGenerator.HelpLinkUri);

        link.GetLeftPart(UriPartial.Path)
            .Should().Be("https://adcodicem.github.io/AdCodicem.ValueObjects/docs/how-to/aspnet-core");

        var page = File.ReadAllLines(Path.Combine(RepositoryRoot(), "website", "docs", "how-to", "aspnet-core.md"));
        page.Should().Contain("slug: /how-to/aspnet-core");
        page.Select(Anchor).Should().Contain(link.Fragment.TrimStart('#'));
    }

    /// <summary>
    /// The compiler never hands an analyzer a null context, so the guard is only reached by a direct call, which
    /// it tolerates.
    /// </summary>
    [Fact]
    public void The_analyzer_initialized_without_a_context_does_nothing()
    {
        var analyzer = new RequestDelegateGeneratorAnalyzer();

        analyzer.Invoking(target => target.Initialize(null!)).Should().NotThrow();
    }

    private static AnalyzerConfiguration Property(string value)
        => new(global: new Dictionary<string, string> { [EnabledKey] = value });

    /// <summary>
    /// Runs the analyzer over a snippet that is otherwise valid C#, so that what it reports is never a side effect of
    /// code the compiler would refuse anyway.
    /// </summary>
    private static Task<ImmutableArray<Diagnostic>> RunAsync(
        string source,
        AnalyzerConfiguration? options = null,
        ImmutableArray<MetadataReference>? references = null)
    {
        GeneratorHarness.Run(source).CompilationDiagnostics.Should().BeEmpty();

        return GeneratorHarness.RunAnalyzerAsync<RequestDelegateGeneratorAnalyzer>(
            source,
            references ?? WithRouting,
            options ?? Enabled);
    }

    private static Task<string> FixAsync(string source)
        => GeneratorHarness.FixAsync<RequestDelegateGeneratorAnalyzer, ListContractCodeFixProvider>(source, WithRouting, Enabled);

    /// <summary>
    /// Asserts that fixed source compiles with the generator and that the analyzer has nothing left to say about it.
    /// </summary>
    private static async Task ShouldBeSilentAndCompileAsync(string fixedSource)
        => (await RunAsync(fixedSource)).Should().BeEmpty();

    private static IReadOnlyList<string> Lines(string source)
        => [.. source.Split('\n').Select(line => line.Trim())];

    private static (string Text, string Line) Located(Diagnostic diagnostic)
    {
        var text = diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken);
        var span = diagnostic.Location.SourceSpan;

        return (text.ToString(span), text.Lines.GetLineFromPosition(span.Start).ToString().Trim());
    }

    /// <summary>
    /// Gets the anchor Docusaurus gives a heading of any level: lower case, punctuation dropped, spaces as hyphens.
    /// Any other line gives none.
    /// </summary>
    private static string Anchor(string line)
    {
        if (!line.StartsWith('#'))
        {
            return string.Empty;
        }

        var heading = line.TrimStart('#').Trim().ToLowerInvariant();

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
