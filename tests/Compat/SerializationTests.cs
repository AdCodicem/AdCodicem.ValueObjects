using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.NewtonsoftJson;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>A payload carrying one value object of each shape.</summary>
/// <param name="Customer">A Guid value object.</param>
/// <param name="Email">A normalized string.</param>
/// <param name="Amount">A decimal.</param>
/// <param name="Country">A closed set.</param>
/// <param name="Payment">An entity identifier.</param>
/// <param name="Purchase">A construction of a generic value object.</param>
public sealed record OrderPlaced(
    CustomerId Customer,
    EmailAddress Email,
    Amount Amount,
    CountryCode Country,
    PaymentId Payment,
    Reference<PurchaseOrder> Purchase);

/// <summary>
/// A source-generated context. The [JsonConverter] the value object generator writes is invisible to the
/// System.Text.Json generator, so the factory is named here, as the README says.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, Converters = [typeof(ValueObjectJsonConverterFactory)])]
[JsonSerializable(typeof(OrderPlaced))]
public sealed partial class CompatJsonContext : JsonSerializerContext;

public sealed class SerializationTests
{
    private static readonly OrderPlaced Sample = new(
        CustomerId.Create(Guid.Parse("0193b1c0-0000-7000-8000-000000000001")),
        EmailAddress.Create("ada@example.com"),
        Amount.Create(12.5m),
        CountryCode.France,
        PaymentId.New(),
        Reference<PurchaseOrder>.Create("PO-7"));

    [Fact]
    public void A_source_generated_context_writes_bare_values_and_reads_them_back_normalized()
    {
        var json = JsonSerializer.Serialize(Sample, CompatJsonContext.Default.OrderPlaced);

        json.Should().Be(
            $$"""{"customer":"0193b1c0-0000-7000-8000-000000000001","email":"ada@example.com","amount":12.50,"country":"FR","payment":"{{Sample.Payment}}","purchase":"PO-7"}""");

        var back = JsonSerializer.Deserialize(json.Replace("ada@example.com", " ADA@example.com ", StringComparison.Ordinal), CompatJsonContext.Default.OrderPlaced);
        back.Should().Be(Sample);
    }

    [Fact]
    public void A_source_generated_context_refuses_a_value_the_domain_rejects()
    {
        var read = () => JsonSerializer.Deserialize(
            """{"customer":"0193b1c0-0000-7000-8000-000000000001","email":"not-an-email","amount":1,"country":"FR","payment":"pay_x","purchase":"PO-7"}""",
            CompatJsonContext.Default.OrderPlaced);

        read.Should().Throw<JsonException>();
    }

    /// <summary>
    /// The exporter of the next major still describes a value object as the schema that accepts anything, and the
    /// transform fills it in from the rules the type declares, for OpenAPI and for a language model.
    /// </summary>
    [Fact]
    public void The_exported_JSON_Schema_describes_each_value_object_as_its_underlying_value()
    {
        var openApi = JsonSchemaExporter.GetJsonSchemaAsNode(
            CompatJsonContext.Default.Options,
            typeof(OrderPlaced),
            new JsonSchemaExporterOptions { TransformSchemaNode = ValueObjectJsonSchema.TransformSchemaNode })["properties"]!;
        var languageModel = JsonSchemaExporter.GetJsonSchemaAsNode(
            CompatJsonContext.Default.Options,
            typeof(OrderPlaced),
            new JsonSchemaExporterOptions
            {
                TransformSchemaNode = ValueObjectJsonSchema.CreateTransform(ValueObjectJsonSchemaProfile.LanguageModel),
            })["properties"]!;

        openApi["customer"]!.ToJsonString().Should().Be("""{"description":"A Guid value object, with a rule the default value breaks.","type":"string","format":"uuid"}""");
        openApi["email"]!["maxLength"]!.GetValue<int>().Should().Be(254);
        openApi["email"]!["format"]!.GetValue<string>().Should().Be("email");
        openApi["amount"]!["type"]!.ToJsonString().Should().Be("""["string","number"]""");
        openApi["amount"]!["minimum"]!.ToJsonString().Should().Be("0");
        openApi["country"]!["enum"]!.ToJsonString().Should().Be("""["FR","BE","LU"]""");
        openApi["purchase"]!["maxLength"]!.GetValue<int>().Should().Be(12);
        languageModel["amount"]!["type"]!.ToJsonString().Should().Be("\"number\"");
        languageModel["country"]!["description"]!.GetValue<string>().Should().Be("A closed set of reference data.");
        languageModel["country"]!["enum"]!.ToJsonString().Should().Be("""["FR","BE","LU"]""");
    }

    [Fact]
    public void Reflection_based_options_need_the_extension_only()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web).AddValueObjects();

        JsonSerializer.Serialize(Reference<SalesInvoice>.Create("inv-9"), options).Should().Be("\"INV-9\"");
        JsonSerializer.Deserialize<Amount>("3.333", options).Value.Should().Be(3.33m);
    }

    [Fact]
    public void Newtonsoft_writes_and_reads_the_underlying_value()
    {
        var settings = new Newtonsoft.Json.JsonSerializerSettings { Converters = { new ValueObjectConverter() } };

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(Sample.Email, settings);

        json.Should().Be("\"ada@example.com\"");
        Newtonsoft.Json.JsonConvert.DeserializeObject<CountryCode>("\"be\"", settings).Should().Be(CountryCode.Belgium);
    }
}
