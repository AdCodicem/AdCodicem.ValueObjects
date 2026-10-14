using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using NSwag.CodeGeneration.CSharp;
using NSwagOpenApiDocument = NSwag.OpenApiDocument;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// The OpenAPI documents NSwag builds once <c>settings.SchemaSettings.AddValueObjects()</c> is called: one component per
/// value object, its underlying type carrying its rules, which parameters and members refer to, text held to a value
/// object's rules described in place, and Swagger 2.0 described in place throughout.
/// </summary>
/// <param name="fixture">The documents.</param>
public sealed partial class NSwagDocumentTests(NSwagDocument fixture) : IClassFixture<NSwagDocument>
{
    private const string IbanPattern = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$";

    private NSwagDocuments Document => fixture.SystemTextJson;

    [Fact]
    public void A_parameter_refers_to_the_component_of_its_value_object_and_says_null_beside_it()
    {
        Document.Settings.Should().Be<SystemTextJsonSchemaGeneratorSettings>();
        Document.Document["openapi"]!.ToString().Should().Be("3.0.0");

        Schema("/accounts/{iban}", "iban").Should().Be("""{"$ref":"#/components/schemas/Iban"}""");
        Schema("/accounts/{iban}", "quantity").Should().Be("""{"$ref":"#/components/schemas/Quantity"}""");
        Schema("/accounts/{iban}", "country").Should().Be("""{"nullable":true,"oneOf":[{"$ref":"#/components/schemas/CountryCode"}]}""");
        Schema("/accounts/{iban}", "X-Page").Should().Be("""{"$ref":"#/components/schemas/PageNumber"}""");
        Schema("/accounts/{iban}", "notice").Should().Be("""{"$ref":"#/components/schemas/NoticeChannelOfAccountFilter"}""");
        Schema("/quantities", "quantity").Should().Be("""{"type":"array","items":{"$ref":"#/components/schemas/Quantity"}}""");
        Schema("/ledgers/{account}", "Above").Should().Be("""{"nullable":true,"oneOf":[{"$ref":"#/components/schemas/Amount"}]}""");
        Schema("/ledgers/{account}", "X-Grade").Should().Be("""{"$ref":"#/components/schemas/Grade"}""");
        Schema("/mvc/accounts/{iban}", "iban").Should().Be("""{"$ref":"#/components/schemas/Iban"}""");
        Schema("/mvc/accounts/search", "Least").Should().Be("""{"$ref":"#/components/schemas/Quantity"}""");

        // A bare number is described as NSwag describes it, beside the value objects.
        Schema("/raw", "raw").Should().Be("""{"type":"integer","format":"int32"}""");
    }

    [Fact]
    public void Each_component_is_its_value_object_s_underlying_type_with_its_rules()
    {
        Document.Schema("Iban").Should().Be(
            $$$"""{"type":"string","description":"An International Bank Account Number, stored in its electronic form.","format":"iban","maxLength":34,"minLength":15,"pattern":"{{{IbanPattern}}}","example":"FR7630006000011234567890189"}""");
        Document.Schema("Quantity").Should().Be(
            """{"type":"integer","description":"A quantity of items, tested to cover the narrow integer promotion path.","format":"int32","maximum":1000.0,"minimum":0.0}""");
        Document.Schema("Amount").Should().Be(
            """{"type":"number","description":"A monetary amount in the ambient currency, never negative.","format":"decimal","minimum":0.0,"example":1250.0}""");
        Document.Schema("Consent").Should().Be("""{"type":"boolean","description":"Whether a customer agreed to be contacted.","example":true}""");
        Document.Schema("CountryCode").Should().Contain(
            ""","x-enumNames":["France","Belgium","Luxembourg"],"x-enum-descriptions":["France","Belgium","Luxembourg"],"enum":["FR","BE","LU"],"x-enum-varnames":["France","Belgium","Luxembourg"],"x-ms-enum":{"name":"CountryCode","modelAsString":false""");
        Document.Schema("VatRate").Should().Contain("\"type\":\"number\"").And.Contain("\"enum\":[20.0,5.5]");
        Document.Schema("NoticeChannelOfAccountFilter").Should().Contain("\"enum\":[\"email\",\"sms\"]").And.Contain("\"name\":\"NoticeChannelOfAccountFilter\"");
        Document.Schema("ServiceHour").Should().Be(
            """{"type":"string","description":"Between 08:00:00.0000000 and 18:00:00.0000000, inclusive.","pattern":"^(?:[01]\\d|2[0-3]):[0-5]\\d(?::[0-5]\\d(?:\\.\\d{1,7})?)?$","x-minimum":"08:00:00.0000000","x-maximum":"18:00:00.0000000"}""");
        Document.Schemas.Properties().Select(static schema => schema.Name).Should().NotContain(["Int128", "UInt128", "SByte"]);
    }

    [Fact]
    public void A_body_refers_to_its_value_objects_alone_in_a_collection_and_in_a_dictionary()
    {
        Member(nameof(EveryValueObject), "iban").Should().Be("""{"$ref":"#/components/schemas/Iban"}""");
        Member(nameof(EveryValueObject), "optional").Should().Be("""{"type":"array","items":{"nullable":true,"oneOf":[{"$ref":"#/components/schemas/Quantity"}]}}""");
        Member(nameof(EveryValueObject), "groups").Should().Be("""{"type":"array","items":{"type":"array","items":{"$ref":"#/components/schemas/Iban"}}}""");
        Member(nameof(EveryValueObject), "maybeCountryPerSite").Should().Be("""{"type":"object","additionalProperties":{"nullable":true,"oneOf":[{"$ref":"#/components/schemas/CountryCode"}]}}""");

        // A dictionary's key is not described: OpenAPI 3.0 has no propertyNames.
        Member(nameof(EveryValueObject), "stockPerCountry").Should().Be("""{"type":"object","additionalProperties":{"$ref":"#/components/schemas/Quantity"}}""");
    }

    [Fact]
    public void Text_a_parameter_holds_to_a_value_object_s_rules_is_described_as_its_text()
    {
        var iban = Parameter("/text/{iban}", "iban");
        iban["type"]!.ToString().Should().Be("string");
        iban["pattern"]!.ToString().Should().Be(IbanPattern);
        iban["minLength"]!.Value<int>().Should().Be(15, "NSwag reads no route constraint");

        var quantity = Parameter("/text/{iban}", "quantity");
        quantity["pattern"]!.ToString().Should().Be(@"^-?(?:0|[1-9]\d*)$");
        quantity["x-maximum"]!.ToString().Should().Be("1000");
        quantity["nullable"]!.Value<bool>().Should().BeTrue();

        var country = Parameter("/text/{iban}", "X-Country");
        country["type"]!.ToString(Formatting.None).Should().Be("\"string\"", "OpenAPI 3.0 says null beside the type");
        country["enum"]!.ToString(Formatting.None).Should().Be("""["FR","BE","LU",null]""");

        Parameter("/text/{iban}", "ibans")["items"]!["pattern"]!.ToString().Should().Be(IbanPattern);
        Parameter("/mvc/accounts/text-search", "Countries")["items"]!["enum"]!.ToString(Formatting.None).Should().Be("""["FR","BE","LU"]""", "an element is never null where the collection may be");
        Parameter("/text-criteria", "X-Quantity")["x-minimum"]!.ToString().Should().Be("0");
        Parameter("/mvc/accounts/text", "iban")["format"]!.ToString().Should().Be("iban");
        Parameter("/mvc/accounts/text-search", "Iban")["pattern"]!.ToString().Should().Be(IbanPattern);
        Parameter("/mvc/accounts/text-search-record", "Iban")["pattern"]!.ToString().Should().Be(IbanPattern, "the record's constructor parameter carries it");
        Parameter("/text/{iban}", "note").ToString(Formatting.None).Should().Be("""{"type":"string","nullable":true}""");
    }

    [Fact]
    public void A_parameter_s_own_validation_attribute_replaces_the_value_object_s_lengths()
    {
        // NSwag applies a parameter's validation attributes after the schema processors, and a parameter has no second
        // pass: [StringLength(20)] gives its 20, and its 0 in place of the value object's 15.
        var account = Parameter("/limited", "Account");

        account["maxLength"]!.Value<int>().Should().Be(20);
        account["minLength"]!.Value<int>().Should().Be(0);
        account["pattern"]!.ToString().Should().Be(IbanPattern);
    }

    [Fact]
    public void A_member_keeps_the_stricter_of_its_own_rules_and_the_value_object_s()
    {
        var bounded = JObject.Parse(Member(nameof(TextMembers), "bounded"));

        bounded["maxLength"]!.Value<int>().Should().Be(20);
        bounded["minLength"]!.Value<int>().Should().Be(15);
        Member(nameof(EveryValueObjectAsText), "quantity").Should().Contain("\"type\":\"string\"").And.Contain("\"x-minimum\":\"0\"");
    }

    [Fact]
    public void Under_Newtonsoft_Json_with_the_converter_numbers_and_booleans_are_described_as_the_converter_writes_them()
    {
        var document = fixture.Newtonsoft;

        document.Settings.Should().Be<NewtonsoftJsonSchemaGeneratorSettings>();
        document.Schema("Quantity").Should().Contain("\"type\":\"integer\"").And.Contain("\"maximum\":1000.0");
        document.Schema("Consent").Should().Contain("\"type\":\"boolean\"").And.Contain("\"example\":true");
        document.Schema("RecordedAt").Should().Contain("\"x-minimum\":\"2000-01-01T00:00:00\"");
        document.Schema("VatRate").Should().Contain("\"enum\":[20.0,5.5]");
    }

    [Fact]
    public void Under_Swagger_2_a_value_object_is_described_in_place_as_its_underlying_type()
    {
        var document = fixture.Swagger2;

        document.Document["swagger"]!.ToString().Should().Be("2.0");
        document.Schemas.Properties().Select(static schema => schema.Name).Should().BeEquivalentTo(nameof(EveryValueObject), nameof(EveryValueObjectAsText), nameof(TextMembers));
        document.Document.ToString().Should().NotContain("x-schema", "Swagger 2.0 refers to no schema from a parameter");

        var iban = document.Parameter("/accounts/{iban}", "iban");
        iban["type"]!.ToString().Should().Be("string");
        iban["format"]!.ToString().Should().Be("iban");
        iban["pattern"]!.ToString().Should().Be(IbanPattern);
        document.Parameter("/accounts/{iban}", "quantity")["maximum"]!.Value<decimal>().Should().Be(1000m);
        document.Parameter("/accounts/{iban}", "quantity")["type"]!.ToString(Formatting.None).Should().Be("\"integer\"", "a number is a number in a document");
        document.Parameter("/accounts/{iban}", "country")["enum"]!.ToString(Formatting.None).Should().Be("""["FR","BE","LU"]""", "Swagger 2.0 has no null");
        document.Parameter("/accounts/{iban}", "country")["x-nullable"]!.Value<bool>().Should().BeTrue("NSwag says null beside the type");
        document.Parameter("/accounts/{iban}", "iban")["x-nullable"]?.Value<bool>().Should().NotBe(true, "a route value is never null");
        document.Parameter("/text/{iban}", "X-Country")["enum"]!.ToString(Formatting.None).Should().Be("""["FR","BE","LU"]""", "nor for text");
        document.Property(nameof(EveryValueObject), "iban")["pattern"]!.ToString().Should().Be(IbanPattern);
    }

    [Fact]
    public async Task NSwag_describes_minimal_APIs_alone_with_Newtonsoft_Json_s_settings_unless_handed_their_own()
    {
        // NSwag's choice, not the package's: with no System.Text.Json output formatter of MVC's, it describes the default
        // settings of Microsoft.AspNetCore.Mvc.NewtonsoftJson, which this suite can load, and so the type converter's text,
        // whatever the minimal APIs write. AddControllers(), or System.Text.Json settings of the application's own, keep
        // the document to the wire.
        var document = await NSwagDocuments.BuildAsync(NSwagMode.MinimalApisAlone);

        document.Settings.Should().Be<NewtonsoftJsonSchemaGeneratorSettings>();
        document.Schema("Quantity").Should().Contain("\"type\":\"string\"").And.Contain("\"pattern\":\"^-?(?:0|[1-9]\\\\d*)$\"");

        var handed = await NSwagDocuments.BuildAsync(NSwagMode.MinimalApisOwnSettings);
        handed.Settings.Should().Be<SystemTextJsonSchemaGeneratorSettings>();
        handed.Document["openapi"]!.ToString().Should().Be("3.0.0");
        handed.Schema("Quantity").Should().Be(fixture.SystemTextJson.Schema("Quantity"));
        handed.Schema("Consent").Should().Contain("\"type\":\"boolean\"");
    }

    [Fact]
    public async Task A_client_NSwag_generates_holds_the_underlying_types_and_one_enumeration_per_closed_set()
    {
        var document = await NSwagOpenApiDocument.FromJsonAsync(Document.Document.ToString(), TestContext.Current.CancellationToken);
        var code = new CSharpClientGenerator(document, new CSharpClientGeneratorSettings { ClassName = "{controller}Client" }).GenerateFile();

        code.Should().Contain("public string Iban { get; set; }")
            .And.Contain("public int Quantity { get; set; }")
            .And.Contain("public decimal Amount { get; set; }")
            .And.Contain("public bool Consent { get; set; }")
            .And.Contain("public CountryCode Country { get; set; }")
            .And.Contain("public enum CountryCode")
            .And.NotContain("public partial class Iban")
            .And.NotContain("public partial class Quantity");
        code.Split("public enum CountryCode").Should().HaveCount(2, "a closed set is one enumeration, wherever it is used");

        // NSwag's generator writes two converters on a member whose schema is a closed set of dates, a string enumeration
        // in the date format, which C# refuses (CS0579), whoever wrote the document: pinned, so that a fixed NSwag shows.
        var refused = Compile(code);
        var lines = code.Split('\n');
        refused.Should().HaveCount(2).And.OnlyContain(error => error.Id == "CS0579"
            && lines[error.Location.GetLineSpan().StartLinePosition.Line + 1].Contains("CutOff", StringComparison.Ordinal));

        // The rest of the client compiles once the second converter is left out.
        Compile(DuplicateDateConverter().Replace(code, "$1")).Should().BeEmpty();
    }

    private static List<Diagnostic> Compile(string code)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(JsonConvert).Assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Client",
            [CSharpSyntaxTree.ParseText(code, cancellationToken: TestContext.Current.CancellationToken)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var assembly = new MemoryStream();

        var emitted = compilation.Emit(assembly, cancellationToken: TestContext.Current.CancellationToken);

        return [.. emitted.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
    }

    [GeneratedRegex(@"(\[Newtonsoft\.Json\.JsonConverter\(typeof\(Newtonsoft\.Json\.Converters\.StringEnumConverter\)\)\]\s*)\[Newtonsoft\.Json\.JsonConverter\(typeof\(DateFormatConverter\)\)\]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DuplicateDateConverter();

    private string Schema(string path, string name) => Document.Parameter(path, name)["schema"]!.ToString(Formatting.None);

    private JObject Parameter(string path, string name) => (JObject)Document.Parameter(path, name)["schema"]!;

    private string Member(string component, string name) => Document.Property(component, name).ToString(Formatting.None);
}
