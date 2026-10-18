using System.Globalization;
using System.IO;
using AdCodicem.ValueObjects.Generators.Analyzers;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// What <c>VO0039</c> to <c>VO0042</c> report: a <c>[ParsesAs]</c> or a <c>[NotDefault]</c> that would throw when a value is
/// validated, and, under <c>PublishAot</c>, a <c>[ParsesAs(typeof(T))]</c> a native binary finds only once the value object
/// is registered by hand.
/// </summary>
/// <remarks>
/// The analyzer runs over the compilation with the generator's output, as the compiler does, so that a value object of
/// the compilation itself, whose contract the generated partial implements, is recognized.
/// </remarks>
public sealed class ValidationAttributeAnalyzerTests
{
    private const string PublishAotKey = "build_property.PublishAot";

    private const string Domain = """
        [ValueObject<string>(MinLength = 15, MaxLength = 34)]
        public readonly partial struct Iban;

        [ValueObject<short>]
        public readonly partial struct Quantity;

        [ValueObject<string>(MaxLength = 12)]
        public readonly partial struct Reference<TOwner>;

        public static partial class Catalog<TItem>
        {
            [ValueObject<int>]
            public readonly partial struct Stock;
        }

        [EntityId("ord")]
        public readonly partial struct OrderId;

        public sealed class PurchaseOrder;

        """;

    /// <summary>A value object written by hand, which nothing generated registers.</summary>
    internal const string HandWritten = """
        public readonly struct Code : IValueObject<Code, string>
        {
            private readonly string? _value;
            private Code(string value) => _value = value;
            public static AdCodicem.ValueObjects.Metadata.ValueObjectSchema Schema => AdCodicem.ValueObjects.Metadata.ValueObjectSchema.Unconstrained;
            public string Value => _value ?? string.Empty;
            public bool IsDefault => _value is null;
            public static string Normalize(string value) => value;
            public static ValidationResult Validate(in string value) => ValidationResult.Success;
            public static Code Create(string value) => new(value);
            public static bool TryCreate(string value, out Code result) => TryCreate(value, out result, out _);
            public static bool TryCreate(string value, out Code result, out ValidationResult validation) { validation = default; result = new(value); return true; }
            public static Code CreateUnchecked(string value) => new(value);
            public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out Code result, out ValidationResult validation) => TryCreate(text.ToString(), out result, out validation);
            public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Code result) => TryParse(s, provider, out result, out _);
            public static bool TryParse(string? s, IFormatProvider? provider, out Code result) => TryParse(s.AsSpan(), provider, out result, out _);
            public static Code Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());
            public static Code Parse(string s, IFormatProvider? provider) => Create(s);
            public bool Equals(Code other) => Value == other.Value;
            public override bool Equals(object? obj) => obj is Code other && Equals(other);
            public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
            public int CompareTo(Code other) => string.CompareOrdinal(Value, other.Value);
            public override string ToString() => Value;
            public string ToString(string? format, IFormatProvider? formatProvider) => Value;
            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) { charsWritten = 0; return Value.TryCopyTo(destination) && (charsWritten = Value.Length) >= 0; }
            public static bool operator ==(Code left, Code right) => left.Equals(right);
            public static bool operator !=(Code left, Code right) => !left.Equals(right);
            public static bool operator <(Code left, Code right) => left.CompareTo(right) < 0;
            public static bool operator >(Code left, Code right) => left.CompareTo(right) > 0;
            public static bool operator <=(Code left, Code right) => left.CompareTo(right) <= 0;
            public static bool operator >=(Code left, Code right) => left.CompareTo(right) >= 0;
        }

        """;

    /// <summary>
    /// A struct implementing the contract over another value object, which every member delegates to: the registry
    /// describes a struct implementing it over itself alone.
    /// </summary>
    internal const string Imposter = """
        public readonly struct Imposter : IValueObject<Code, string>
        {
            public static AdCodicem.ValueObjects.Metadata.ValueObjectSchema Schema => Code.Schema;
            public string Value => string.Empty;
            public bool IsDefault => true;
            public static string Normalize(string value) => Code.Normalize(value);
            public static ValidationResult Validate(in string value) => Code.Validate(value);
            public static Code Create(string value) => Code.Create(value);
            public static bool TryCreate(string value, out Code result) => Code.TryCreate(value, out result);
            public static bool TryCreate(string value, out Code result, out ValidationResult validation) => Code.TryCreate(value, out result, out validation);
            public static Code CreateUnchecked(string value) => Code.CreateUnchecked(value);
            public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out Code result, out ValidationResult validation) => Code.TryParse(text, provider, out result, out validation);
            public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Code result) => Code.TryParse(s, provider, out result);
            public static bool TryParse(string? s, IFormatProvider? provider, out Code result) => Code.TryParse(s, provider, out result);
            public static Code Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Code.Parse(s, provider);
            public static Code Parse(string s, IFormatProvider? provider) => Code.Parse(s, provider);
            public bool Equals(Code other) => other.IsDefault;
            public int CompareTo(Code other) => other.IsDefault ? 0 : -1;
            public string ToString(string? format, IFormatProvider? formatProvider) => string.Empty;
            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) { charsWritten = 0; return true; }
        }

        """;

    /// <summary>
    /// Text, a collection of text, and a type the compiler leaves open are silent, wherever the attribute sits: a property,
    /// a field, a record's or a method's parameter, a lambda's or a local function's.
    /// </summary>
    [Fact]
    public async Task Text_and_collections_of_text_are_silent_wherever_the_attribute_sits()
    {
        var diagnostics = await RunAsync(Domain + """
            public sealed record Transfer([ParsesAs(typeof(Iban))] string? Iban, [property: ParsesAs<Quantity, short>] string Quantity);

            public sealed class Form<T>
            {
                [ParsesAs(typeof(Iban))] public string? Account { get; set; }
                [ParsesAs(typeof(Iban))] public string? Field;
                [ParsesAs(typeof(Iban))] public System.Collections.Generic.List<string?>? Accounts { get; set; }
                [ParsesAs(typeof(Iban))] public string[]? Array { get; set; }
                [ParsesAs(typeof(Iban))] public System.Collections.Immutable.ImmutableArray<string> Immutable { get; set; }
                [ParsesAs(typeof(Iban))] public System.Collections.Generic.IReadOnlyList<string>? ReadOnly { get; set; }
                [ParsesAs(typeof(Iban))] public T? Open { get; set; }
                [ParsesAs(typeof(Iban?))] public string? Nullable { get; set; }
                [ParsesAs(typeof(Reference<PurchaseOrder>))] public string? Constructed { get; set; }
                [ParsesAs(typeof(Catalog<int>.Stock))] public string? Nested { get; set; }
                [ParsesAs(typeof(OrderId))] public string? Order { get; set; }

                public void Handle([ParsesAs<Iban, string>] string? iban)
                {
                    Func<string?, bool> lambda = ([ParsesAs(typeof(Iban))] string? text) => text is null;
                    bool Local([ParsesAs(typeof(Iban))] string? text) => text is null;
                    _ = lambda(iban) && Local(iban);
                }
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A member of any other type would throw when it is validated: a number, a value object, a collection of something
    /// else, a dictionary, and <see cref="object"/>, which a JSON body fills with a <c>JsonElement</c>, never text. The
    /// message suggests the value object named, a nullable one as it is named.
    /// </summary>
    [Fact]
    public async Task A_member_that_holds_no_text_is_reported_where_the_attribute_sits()
    {
        var diagnostics = await RunAsync(Domain + """
            public sealed record Transfer([ParsesAs(typeof(Iban))] int Count);

            public sealed class Form
            {
                [ParsesAs(typeof(Iban))] public object? Raw { get; set; }
                [ParsesAs<Iban, string>] public Iban Typed { get; set; }
                [ParsesAs(typeof(Iban))] public System.Collections.Generic.List<int>? Numbers { get; set; }
                [ParsesAs(typeof(Iban))] public System.Collections.Generic.Dictionary<string, string>? Pairs { get; set; }
                [ParsesAs(typeof(Iban))] public long Field;
                [ParsesAs(typeof(Iban?))] public int Optional { get; set; }

                public void Handle([ParsesAs(typeof(Iban))] int page)
                {
                    Func<int, bool> lambda = ([ParsesAs(typeof(Quantity))] int number) => number > 0;
                    _ = lambda(page);
                }
            }
            """);

        diagnostics.Select(static diagnostic => (diagnostic.Id, diagnostic.Severity, diagnostic.GetMessage(CultureInfo.InvariantCulture))).Should().Equal(
            ("VO0039", DiagnosticSeverity.Error, "'Count' is of type 'int', which is neither text nor a collection of text: [ParsesAs] would throw when it is validated. Declare it string or a collection of string, or declare it 'Iban' and drop the attribute."),
            ("VO0039", DiagnosticSeverity.Error, "'Raw' is of type 'object?', which is neither text nor a collection of text: [ParsesAs] would throw when it is validated. Declare it string or a collection of string, or declare it 'Iban' and drop the attribute."),
            ("VO0039", DiagnosticSeverity.Error, "'Typed' is of type 'Iban', which is neither text nor a collection of text: [ParsesAs] would throw when it is validated. Declare it string or a collection of string, or declare it 'Iban' and drop the attribute."),
            ("VO0039", DiagnosticSeverity.Error, "'Numbers' is of type 'List<int>?', which is neither text nor a collection of text: [ParsesAs] would throw when it is validated. Declare it string or a collection of string, or declare it 'Iban' and drop the attribute."),
            ("VO0039", DiagnosticSeverity.Error, "'Pairs' is of type 'Dictionary<string, string>?', which is neither text nor a collection of text: [ParsesAs] would throw when it is validated. Declare it string or a collection of string, or declare it 'Iban' and drop the attribute."),
            ("VO0039", DiagnosticSeverity.Error, "'Field' is of type 'long', which is neither text nor a collection of text: [ParsesAs] would throw when it is validated. Declare it string or a collection of string, or declare it 'Iban' and drop the attribute."),
            ("VO0039", DiagnosticSeverity.Error, "'Optional' is of type 'int', which is neither text nor a collection of text: [ParsesAs] would throw when it is validated. Declare it string or a collection of string, or declare it 'Iban?' and drop the attribute."),
            ("VO0039", DiagnosticSeverity.Error, "'page' is of type 'int', which is neither text nor a collection of text: [ParsesAs] would throw when it is validated. Declare it string or a collection of string, or declare it 'Iban' and drop the attribute."),
            ("VO0039", DiagnosticSeverity.Error, "'number' is of type 'int', which is neither text nor a collection of text: [ParsesAs] would throw when it is validated. Declare it string or a collection of string, or declare it 'Quantity' and drop the attribute."));
    }

    /// <summary>
    /// <c>typeof</c> naming a type that is no value object would throw when a value is validated: text, a class, a generic
    /// definition, of which no instance exists, a struct implementing the contract over another value object, which the
    /// registry describes no more than the class, and <see langword="null"/>; a misuse of both kinds reports both, and
    /// <c>VO0039</c> then suggests no type to declare the member as, the one named being no value object.
    /// </summary>
    [Fact]
    public async Task A_type_that_is_no_value_object_is_reported()
    {
        var diagnostics = await RunAsync(Domain + HandWritten + Imposter + """
            public sealed class Form
            {
                [ParsesAs(typeof(string))] public string? Text { get; set; }
                [ParsesAs(typeof(Form))] public string? Class { get; set; }
                [ParsesAs(typeof(Reference<>))] public string? Definition { get; set; }
                [ParsesAs(typeof(Imposter))] public string? Stranger { get; set; }
                [ParsesAs(null!)] public string? Nothing { get; set; }
                [ParsesAs(typeof(System.Guid))] public int Both { get; set; }
                [ParsesAs(typeof(Imposter))] public int Pretender { get; set; }
                [ParsesAs(null!)] public int Neither { get; set; }
            }
            """);

        diagnostics.Select(static diagnostic => (diagnostic.Id, diagnostic.GetMessage(CultureInfo.InvariantCulture))).Should().Equal(
            ("VO0040", "[ParsesAs] names 'string', which is no value object: it would throw when a value is validated. Name a struct implementing IValueObject<TSelf, TValue> over itself, constructed when it is generic."),
            ("VO0040", "[ParsesAs] names 'Form', which is no value object: it would throw when a value is validated. Name a struct implementing IValueObject<TSelf, TValue> over itself, constructed when it is generic."),
            ("VO0040", "[ParsesAs] names 'Reference<>', which is no value object: it would throw when a value is validated. Name a struct implementing IValueObject<TSelf, TValue> over itself, constructed when it is generic."),
            ("VO0040", "[ParsesAs] names 'Imposter', which is no value object: it would throw when a value is validated. Name a struct implementing IValueObject<TSelf, TValue> over itself, constructed when it is generic."),
            ("VO0040", "[ParsesAs] names null, which is no value object: it would throw when a value is validated. Name a struct implementing IValueObject<TSelf, TValue> over itself, constructed when it is generic."),
            ("VO0039", "'Both' is of type 'int', which is neither text nor a collection of text: [ParsesAs] would throw when it is validated. Declare it string or a collection of string, or declare it as the value object and drop the attribute."),
            ("VO0040", "[ParsesAs] names 'Guid', which is no value object: it would throw when a value is validated. Name a struct implementing IValueObject<TSelf, TValue> over itself, constructed when it is generic."),
            ("VO0039", "'Pretender' is of type 'int', which is neither text nor a collection of text: [ParsesAs] would throw when it is validated. Declare it string or a collection of string, or declare it as the value object and drop the attribute."),
            ("VO0040", "[ParsesAs] names 'Imposter', which is no value object: it would throw when a value is validated. Name a struct implementing IValueObject<TSelf, TValue> over itself, constructed when it is generic."),
            ("VO0039", "'Neither' is of type 'int', which is neither text nor a collection of text: [ParsesAs] would throw when it is validated. Declare it string or a collection of string, or declare it as the value object and drop the attribute."),
            ("VO0040", "[ParsesAs] names null, which is no value object: it would throw when a value is validated. Name a struct implementing IValueObject<TSelf, TValue> over itself, constructed when it is generic."));
        diagnostics.Should().AllSatisfy(static diagnostic => diagnostic.Severity.Should().Be(DiagnosticSeverity.Error));
    }

    /// <summary>
    /// <c>[NotDefault]</c> checks a value object or its nullable type; on anything else it would throw, a struct
    /// implementing the contract over another value object included, and a collection of value objects, whose elements it
    /// does not check, and a type the compiler leaves open is silent.
    /// </summary>
    [Fact]
    public async Task NotDefault_on_a_member_that_is_no_value_object_is_reported()
    {
        var diagnostics = await RunAsync(Domain + HandWritten + Imposter + """
            public sealed class Command<T>
            {
                [NotDefault] public Iban Account { get; set; }
                [NotDefault] public Iban? Fallback { get; set; }
                [NotDefault] public Reference<PurchaseOrder> Order { get; set; }
                [NotDefault] public Code Code { get; set; }
                [NotDefault] public Imposter Stranger { get; set; }
                [NotDefault] public T? Open { get; set; }
                [NotDefault] public int Count { get; set; }
                [NotDefault] public string? Text { get; set; }
                [NotDefault] public IValueObject? Marker { get; set; }
                [NotDefault] public System.Collections.Generic.List<Iban>? Accounts { get; set; }
                [NotDefault] public long Field;

                public void Handle([NotDefault] Quantity quantity, [NotDefault] Guid id) { }
            }
            """);

        diagnostics.Select(static diagnostic => (diagnostic.Id, diagnostic.GetMessage(CultureInfo.InvariantCulture))).Should().Equal(
            ("VO0041", "'Stranger' is of type 'Imposter', which is no value object: [NotDefault] would throw when it is validated. Declare it as a value object or its nullable type, or drop the attribute."),
            ("VO0041", "'Count' is of type 'int', which is no value object: [NotDefault] would throw when it is validated. Declare it as a value object or its nullable type, or drop the attribute."),
            ("VO0041", "'Text' is of type 'string?', which is no value object: [NotDefault] would throw when it is validated. Declare it as a value object or its nullable type, or drop the attribute."),
            ("VO0041", "'Marker' is of type 'IValueObject?', which is no value object: [NotDefault] would throw when it is validated. Declare it as a value object or its nullable type, or drop the attribute."),
            ("VO0041", "'Accounts' is of type 'List<Iban>?', which is no value object: [NotDefault] would throw when it is validated. Declare it as a value object or its nullable type, or drop the attribute."),
            ("VO0041", "'Field' is of type 'long', which is no value object: [NotDefault] would throw when it is validated. Declare it as a value object or its nullable type, or drop the attribute."),
            ("VO0041", "'id' is of type 'Guid', which is no value object: [NotDefault] would throw when it is validated. Declare it as a value object or its nullable type, or drop the attribute."));
    }

    /// <summary>
    /// Under <c>PublishAot</c>, the <see cref="Type"/> form finds in the registry alone what nothing generated registers: a
    /// construction of a generic value object, one nested in a generic type, and a value object written by hand. A
    /// generated value object and an entity identifier are registered by their assembly.
    /// </summary>
    [Theory]
    [InlineData("true")]
    [InlineData(" True ")]
    public async Task Under_PublishAot_a_value_object_nothing_generated_registers_is_reported(string publishAot)
    {
        var diagnostics = await RunAsync(
            Domain + HandWritten + """
                public sealed class Form
                {
                    [ParsesAs(typeof(Reference<PurchaseOrder>))] public string? Order { get; set; }
                    [ParsesAs(typeof(Catalog<int>.Stock))] public string? Stock { get; set; }
                    [ParsesAs(typeof(Code))] public string? Code { get; set; }
                    [ParsesAs(typeof(Iban))] public string? Iban { get; set; }
                    [ParsesAs(typeof(OrderId))] public string? OrderId { get; set; }
                    [ParsesAs<Reference<PurchaseOrder>, string>] public string? Typed { get; set; }
                }
                """,
            PublishAot(publishAot));

        diagnostics.Select(static diagnostic => (diagnostic.Id, diagnostic.Severity, diagnostic.GetMessage(CultureInfo.InvariantCulture))).Should().Equal(
            ("VO0042", DiagnosticSeverity.Warning, "Under native AOT, [ParsesAs(typeof(Reference<PurchaseOrder>))] finds Reference<PurchaseOrder> only once the application registers it, since a generic value object registers its definition, not its constructions. Write [ParsesAs<Reference<PurchaseOrder>, string>], which needs no registry."),
            ("VO0042", DiagnosticSeverity.Warning, "Under native AOT, [ParsesAs(typeof(Catalog<int>.Stock))] finds Catalog<int>.Stock only once the application registers it, since its assembly registers the definition of the generic 'Catalog<TItem>' it is nested in, not its constructions. Write [ParsesAs<Catalog<int>.Stock, int>], which needs no registry."),
            ("VO0042", DiagnosticSeverity.Warning, "Under native AOT, [ParsesAs(typeof(Code))] finds Code only once the application registers it, since it is written by hand, and nothing generated registers it. Write [ParsesAs<Code, string>], which needs no registry."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task Without_PublishAot_a_value_object_nothing_generated_registers_is_silent(string? publishAot)
    {
        var diagnostics = await RunAsync(
            Domain + HandWritten + """
                public sealed class Form
                {
                    [ParsesAs(typeof(Reference<PurchaseOrder>))] public string? Order { get; set; }
                    [ParsesAs(typeof(Code))] public string? Code { get; set; }
                }
                """,
            publishAot is null ? null : PublishAot(publishAot));

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A value object of a referenced assembly is judged as one of the compilation: generated, it is registered by its
    /// assembly; written by hand, it is found only once registered.
    /// </summary>
    [Fact]
    public async Task A_value_object_of_a_referenced_assembly_is_judged_from_its_metadata()
    {
        var domain = MetadataReference.CreateFromImage(GeneratorHarness.Emit(
            """
            namespace Shop.Domain;

            [AdCodicem.ValueObjects.Annotations.ValueObject<string>(MaxLength = 10)]
            public readonly partial struct Sku;
            """,
            "Shop.Domain"));
        var handWritten = MetadataReference.CreateFromImage(GeneratorHarness.Emit(HandWritten, "Shop.Codes"));

        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<ValidationAttributeAnalyzer>(
            """
            public sealed class Form
            {
                [ParsesAs(typeof(Shop.Domain.Sku))] public string? Sku { get; set; }
                [ParsesAs(typeof(global::Test.Code))] public string? Code { get; set; }
                [NotDefault] public Shop.Domain.Sku Typed { get; set; }
            }
            """,
            GeneratorHarness.LibraryReferences.Add(domain).Add(handWritten),
            PublishAot("true"));

        diagnostics.Should().ContainSingle().Which.GetMessage(CultureInfo.InvariantCulture)
            .Should().StartWith("Under native AOT, [ParsesAs(typeof(Code))] finds Code only once the application registers it, since it is written by hand");
    }

    /// <summary>Both forms on one member are two attributes, and each is judged on its own.</summary>
    [Fact]
    public async Task Both_forms_on_one_member_are_each_reported()
    {
        var diagnostics = await RunAsync(Domain + """
            public sealed class Form
            {
                [ParsesAs(typeof(Iban)), ParsesAs<Quantity, short>] public int Count { get; set; }
            }
            """);

        diagnostics.Select(static diagnostic => diagnostic.Id).Should().Equal("VO0039", "VO0039");
    }

    /// <summary>
    /// The analysis needs the attributes and the contract: a compilation of the framework alone, one declaring an
    /// attribute of the same name, an attribute the compiler cannot bind and one on a type, which the attribute cannot
    /// target, are silent.
    /// </summary>
    [Fact]
    public async Task What_names_no_attribute_of_the_package_is_silent()
    {
        var framework = await GeneratorHarness.RunAnalyzerAsync<ValidationAttributeAnalyzer>(
            """
            namespace AdCodicem.ValueObjects.Annotations
            {
                public sealed class ParsesAsAttribute(System.Type type) : System.Attribute
                {
                    public System.Type Type { get; } = type;
                }
            }

            namespace Test
            {
                public sealed class Form
                {
                    [AdCodicem.ValueObjects.Annotations.ParsesAs(typeof(int))] public int Count { get; set; }
                }
            }
            """,
            GeneratorHarness.FrameworkReferences);
        var bare = await GeneratorHarness.RunAnalyzerAsync<ValidationAttributeAnalyzer>(
            "public sealed class Form { [System.Obsolete] public int Count { get; set; } }",
            GeneratorHarness.FrameworkReferences);
        var notDefaultAlone = await GeneratorHarness.RunAnalyzerAsync<ValidationAttributeAnalyzer>(
            """
            namespace AdCodicem.ValueObjects.Annotations
            {
                public sealed class NotDefaultAttribute : System.Attribute;
            }

            namespace Test
            {
                public sealed class Form
                {
                    [AdCodicem.ValueObjects.Annotations.NotDefault] public int Count { get; set; }
                }
            }
            """,
            GeneratorHarness.FrameworkReferences);
        var unbound = await GeneratorHarness.RunAnalyzerAsync<ValidationAttributeAnalyzer>(Domain + """
            [ParsesAs(typeof(Iban))]
            public sealed class Form
            {
                [Unknown] public int Count { get; set; }
            }
            """);

        framework.Should().BeEmpty();
        bare.Should().BeEmpty();
        notDefaultAlone.Should().BeEmpty();
        unbound.Should().BeEmpty();
    }

    /// <summary>
    /// The analyzer reads <c>PublishAot</c> under the name the package's build props make visible, beside the property
    /// <c>VO0033</c> reads: a mismatch would leave it silent everywhere, with nothing failing.
    /// </summary>
    [Fact]
    public void The_package_makes_PublishAot_visible_to_the_compiler()
    {
        var props = System.Xml.Linq.XDocument.Load(Path.Combine(RepositoryRoot(), "src", "AdCodicem.ValueObjects", "build", "AdCodicem.ValueObjects.props"));
        var visible = props.Descendants("CompilerVisibleProperty").Select(static item => (string?)item.Attribute("Include"));

        visible.Should().Contain(PublishAotKey["build_property.".Length..]);
        ValidationAttributeAnalyzer.PublishAotKey.Should().Be(PublishAotKey);
    }

    /// <summary>Each help link lands on the guide that states the rules.</summary>
    [Fact]
    public void The_help_links_land_on_the_DataAnnotations_guide()
    {
        var analyzer = new ValidationAttributeAnalyzer();

        analyzer.SupportedDiagnostics.Select(static descriptor => descriptor.Id).Should().Equal("VO0039", "VO0040", "VO0041", "VO0042");
        analyzer.SupportedDiagnostics.Should().AllSatisfy(static descriptor => descriptor.HelpLinkUri
            .Should().Be("https://adcodicem.github.io/AdCodicem.ValueObjects/docs/how-to/data-annotations"));
        File.ReadAllLines(Path.Combine(RepositoryRoot(), "website", "docs", "how-to", "data-annotations.md"))
            .Should().Contain("slug: /how-to/data-annotations");
    }

    /// <summary>The compiler never hands an analyzer a null context, so the guard is only reached by a direct call.</summary>
    [Fact]
    public void The_analyzer_initialized_without_a_context_does_nothing()
        => new ValidationAttributeAnalyzer().Invoking(static analyzer => analyzer.Initialize(null!)).Should().NotThrow();

    private static AnalyzerConfiguration PublishAot(string value)
        => new(global: new Dictionary<string, string> { [PublishAotKey] = value });

    /// <summary>
    /// Runs the analyzer over a snippet that is otherwise valid C#, so that what it reports is never a side effect of
    /// code the compiler would refuse anyway, and lists what it reports in the order of the source, which an analyzer
    /// running concurrently does not keep.
    /// </summary>
    private static async Task<IReadOnlyList<Diagnostic>> RunAsync(string source, AnalyzerConfiguration? options = null)
    {
        GeneratorHarness.Run(source).CompilationDiagnostics.Should().BeEmpty();

        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<ValidationAttributeAnalyzer>(source, options: options);

        return [.. diagnostics.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start).ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)];
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AdCodicem.ValueObjects.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The repository root was not found.");
    }
}
