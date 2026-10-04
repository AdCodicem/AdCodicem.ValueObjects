using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using BuildOptions = Json.Schema.BuildOptions;
using Dialect = Json.Schema.Dialect;
using EvaluationOptions = Json.Schema.EvaluationOptions;
using JsonSchema = Json.Schema.JsonSchema;
using OutputFormat = Json.Schema.OutputFormat;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The JSON Schema System.Text.Json exports for a type holding value objects, which describes each of them as
/// <c>true</c> on its own, completed by <see cref="ValueObjectJsonSchema"/> from the rules each one declares.
/// </summary>
public partial class JsonSchemaTests
{
    /// <summary>
    /// Every value object of <c>Domain/UnderlyingTypes.cs</c>, the others the rules below need, nullable ones, and
    /// collections and dictionaries of them, beside two members that are no value object.
    /// </summary>
    internal sealed record Catalog(
        Consent Consent,
        Grade Grade,
        Adjustment Adjustment,
        Score Score,
        Port Port,
        PageNumber Page,
        SequenceNumber Sequence,
        FileSize FileSize,
        ByteCount Bytes,
        LedgerBalance Balance,
        Fingerprint Fingerprint,
        Latitude Latitude,
        Ratio Ratio,
        OpeningTime Opening,
        RecordedAt Recorded,
        OccurredAt Occurred,
        Duration Duration,
        EffectiveDate Effective,
        Tolerance Tolerance,
        PhoneNumber Phone,
        Label Label,
        DocumentStatus Status,
        Iban Iban,
        Amount Amount,
        CustomerId Customer,
        CountryCode Country,
        Percentage Share,
        VatRate Vat,
        Reference<PurchaseOrder> Order,
        BirthDate? Birth,
        Quantity? Quantity,
        CountryCode? Destination,
        List<Iban> Alternates,
        Iban?[] Optional,
        Dictionary<string, Amount> Totals,
        Dictionary<CountryCode, int> PerCountry,
        Dictionary<Quantity, int> PerQuantity,
        List<List<Port>> Ports,
        string Note,
        int Count);

    [JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
    [JsonSerializable(typeof(Catalog))]
    internal sealed partial class CatalogContext : JsonSerializerContext;

    /// <summary>
    /// The two ways the options describe a type: by reflection, and through a source-generated context, which reaches
    /// every value object through the converter factory.
    /// </summary>
    public static TheoryData<string> Resolvers => ["reflection", "context"];

    /// <summary>
    /// Each row: a member of <see cref="Catalog"/>, the profile, and its schema, the same under either resolver.
    /// </summary>
    public static TheoryData<string, ValueObjectJsonSchemaProfile, string> Rules => new()
    {
        { "Consent", OpenApi, """{"description":"Whether a customer agreed to be contacted.","type":"boolean","examples":[true]}""" },
        { "Grade", OpenApi, """{"description":"A school grade, from A to F.\n\nBetween A and F, inclusive.","type":"string"}""" },
        { "Adjustment", OpenApi, """{"description":"A thermostat adjustment, in degrees.","type":"integer","format":"int32","minimum":-10,"maximum":10}""" },
        { "Adjustment", LanguageModel, """{"description":"A thermostat adjustment, in degrees.\n\nFormat: int32.","type":"integer","minimum":-10,"maximum":10}""" },
        { "Score", OpenApi, """{"description":"A score out of a hundred.","type":"integer","format":"int32","maximum":100}""" },
        { "Port", OpenApi, """{"description":"A TCP port.","type":"integer","format":"int32","minimum":1,"examples":[8080]}""" },
        { "Page", OpenApi, """{"description":"A page number, counted from one, with the first page named in an open value set.","type":"integer","format":"int32","minimum":1}""" },
        { "Sequence", OpenApi, """{"description":"A sequence number, which starts at zero.","type":"integer","format":"int64"}""" },
        { "FileSize", OpenApi, """{"description":"The size of a file, in bytes.","type":"integer","format":"int64","minimum":0}""" },
        { "Bytes", OpenApi, """{"description":"A count of bytes transferred.","type":"integer","format":"int64"}""" },
        { "Balance", OpenApi, """{"description":"A balance in the smallest unit of a currency, wider than 64 bits.\n\nBetween -1000000000000000000000 and 1000000000000000000000, inclusive.","type":"string"}""" },
        { "Fingerprint", OpenApi, """{"description":"The fingerprint of a document's content.","type":"string"}""" },
        { "Latitude", OpenApi, """{"description":"A latitude, in degrees.","type":"number","format":"double","minimum":-90,"maximum":90}""" },
        { "Latitude", LanguageModel, """{"description":"A latitude, in degrees.\n\nFormat: double.","type":"number","minimum":-90,"maximum":90}""" },
        { "Ratio", OpenApi, """{"description":"A ratio between zero and one.","type":"number","format":"float","minimum":0,"maximum":1}""" },
        { "Opening", OpenApi, """{"description":"The time a shop opens.\n\nBetween 06:00:00.0000000 and 12:00:00.0000000, inclusive.","type":"string","format":"time"}""" },
        { "Recorded", OpenApi, """{"description":"When a record was written.\n\nBetween 2000-01-01T00:00:00 and 2099-12-31T00:00:00, inclusive.","type":"string","format":"date-time"}""" },
        { "Recorded", LanguageModel, """{"description":"When a record was written.\n\nBetween 2000-01-01T00:00:00 and 2099-12-31T00:00:00, inclusive.","type":"string","format":"date-time"}""" },
        { "Occurred", OpenApi, """{"description":"When an event occurred, with the offset it occurred at.\n\nAt least 2000-01-01T00:00:00+00:00.","type":"string","format":"date-time"}""" },
        { "Duration", OpenApi, """{"description":"How long a task took.\n\nBetween 00:00:00 and 1.00:00:00, inclusive.","type":"string","pattern":"^-?(\\d+\\.)?\\d{2}:\\d{2}:\\d{2}(\\.\\d{1,7})?$","examples":["01:30:00"]}""" },
        { "Duration", LanguageModel, """{"description":"How long a task took.\n\nBetween 00:00:00 and 1.00:00:00, inclusive.","type":"string","pattern":"^-?(\\d+\\.)?\\d{2}:\\d{2}:\\d{2}(\\.\\d{1,7})?$","examples":["01:30:00"]}""" },
        { "Effective", OpenApi, """{"description":"The day a contract takes effect, never before the first day the ledger covers.\n\nAt least 2000-01-01.","type":"string","format":"date"}""" },
        { "Tolerance", OpenApi, """{"description":"A measuring tolerance, as a fraction no larger than one.","type":"number","format":"double","maximum":1}""" },
        { "Phone", OpenApi, """{"description":"An international phone number, printed in groups by its own formatter.","type":"string","pattern":"^\\+[0-9]{6,15}$"}""" },
        { "Label", OpenApi, """{"description":"A free-text label, which may be empty, and whose wide formats outgrow the emitted stack buffer.","type":"string","maxLength":200}""" },
        { "Status", OpenApi, """{"description":"The status of a document, from a closed set whose spelling does not matter.","type":"string","enum":["draft","final"]}""" },
        { "Status", LanguageModel, """{"description":"The status of a document, from a closed set whose spelling does not matter.\n\ndraft: Draft\nfinal: Final","type":"string","enum":["draft","final"]}""" },
        { "Iban", OpenApi, """{"description":"An International Bank Account Number, stored in its electronic form.","type":"string","format":"iban","pattern":"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$","minLength":15,"maxLength":34,"examples":["FR7630006000011234567890189"]}""" },
        { "Iban", LanguageModel, """{"description":"An International Bank Account Number, stored in its electronic form.\n\nFormat: iban.","type":"string","pattern":"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$","minLength":15,"maxLength":34,"examples":["FR7630006000011234567890189"]}""" },
        { "Amount", OpenApi, """{"description":"A monetary amount in the ambient currency, never negative.","type":"number","format":"decimal","minimum":0,"examples":[1250.00]}""" },
        { "Customer", OpenApi, """{"description":"The identifier of a customer.","type":"string","format":"uuid"}""" },
        { "Customer", LanguageModel, """{"description":"The identifier of a customer.","type":"string","format":"uuid"}""" },
        { "Country", OpenApi, """{"description":"An ISO 3166-1 alpha-2 country code restricted to the countries the application serves.","type":"string","minLength":2,"maxLength":2,"enum":["FR","BE","LU"]}""" },
        { "Country", LanguageModel, """{"description":"An ISO 3166-1 alpha-2 country code restricted to the countries the application serves.\n\nFR: France\nBE: Belgium\nLU: Luxembourg","type":"string","minLength":2,"maxLength":2,"enum":["FR","BE","LU"]}""" },
        { "Share", OpenApi, """{"description":"A share of a whole, between 0 and 100.","type":"number","format":"percentage","minimum":0,"maximum":100}""" },
        { "Share", LanguageModel, """{"description":"A share of a whole, between 0 and 100.\n\nFormat: percentage.","type":"number","minimum":0,"maximum":100}""" },
        { "Vat", LanguageModel, """{"description":"A value-added tax rate, in percent.\n\nFormat: decimal.\n\n20.0: Standard\n5.5: Reduced — Food, books and medicine.","type":"number","enum":[20.0,5.5]}""" },
        { "Order", OpenApi, """{"description":"A reference to a document, whose owner is part of its type: a purchase order's and a sales invoice's are not interchangeable.","type":"string","maxLength":12,"examples":["PO-1042"]}""" },
        { "Birth", OpenApi, """{"description":"A date of birth, which must be in the past and within a plausible human lifespan.\n\nBetween 1900-01-01 and 2100-12-31, inclusive.","type":["string","null"],"format":"date"}""" },
        { "Quantity", OpenApi, """{"description":"A quantity of items, tested to cover the narrow integer promotion path.","type":["integer","null"],"format":"int32","minimum":0,"maximum":1000}""" },
        { "Destination", OpenApi, """{"description":"An ISO 3166-1 alpha-2 country code restricted to the countries the application serves.","type":["string","null"],"minLength":2,"maxLength":2,"enum":["FR","BE","LU",null]}""" },
        { "Alternates", OpenApi, """{"type":"array","items":{"description":"An International Bank Account Number, stored in its electronic form.","type":"string","format":"iban","pattern":"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$","minLength":15,"maxLength":34,"examples":["FR7630006000011234567890189"]}}""" },
        { "Optional", OpenApi, """{"type":"array","items":{"description":"An International Bank Account Number, stored in its electronic form.","type":["string","null"],"format":"iban","pattern":"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$","minLength":15,"maxLength":34,"examples":["FR7630006000011234567890189"]}}""" },
        { "Totals", OpenApi, """{"type":"object","additionalProperties":{"description":"A monetary amount in the ambient currency, never negative.","type":"number","format":"decimal","minimum":0,"examples":[1250.00]}}""" },
        { "PerCountry", LanguageModel, """{"type":"object","additionalProperties":{"type":"integer"},"propertyNames":{"description":"An ISO 3166-1 alpha-2 country code restricted to the countries the application serves.\n\nFR: France\nBE: Belgium\nLU: Luxembourg","type":"string","minLength":2,"maxLength":2,"enum":["FR","BE","LU"]}}""" },
        { "PerQuantity", OpenApi, """{"type":"object","additionalProperties":{"type":"integer"}}""" },
        { "Ports", OpenApi, """{"type":"array","items":{"type":["array","null"],"items":{"description":"A TCP port.","type":"integer","format":"int32","minimum":1,"examples":[8080]}}}""" },
        { "Note", OpenApi, """{"type":"string"}""" },
        { "Count", LanguageModel, """{"type":"integer"}""" },
    };

    private const ValueObjectJsonSchemaProfile OpenApi = ValueObjectJsonSchemaProfile.OpenApi;

    private const ValueObjectJsonSchemaProfile LanguageModel = ValueObjectJsonSchemaProfile.LanguageModel;

    /// <summary>
    /// Without the transform, every value object is <c>true</c>, and a collection of them has no <c>items</c>: the
    /// exporter has nothing to read on a type its converter serializes.
    /// </summary>
    [Fact]
    public void The_exporter_alone_describes_a_value_object_as_the_schema_that_accepts_anything()
    {
        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(CatalogContext.Default.Options, typeof(Catalog));

        Member(schema, "Iban").GetValueKind().Should().Be(JsonValueKind.True);
        Member(schema, "Birth").GetValueKind().Should().Be(JsonValueKind.True);
        Member(schema, "Alternates").ToJsonString().Should().Be("""{"type":"array"}""");
    }

    /// <summary>
    /// Every member of the catalog gets a type, under either resolver and either profile: none is left as
    /// <c>true</c>, and the two resolvers describe it alike.
    /// </summary>
    [Theory]
    [InlineData(ValueObjectJsonSchemaProfile.OpenApi)]
    [InlineData(ValueObjectJsonSchemaProfile.LanguageModel)]
    public void Every_value_object_is_described_as_its_underlying_value_under_either_resolver(ValueObjectJsonSchemaProfile profile)
    {
        var byReflection = Export(OptionsFor("reflection"), typeof(Catalog), profile);
        var byContext = Export(OptionsFor("context"), typeof(Catalog), profile);

        foreach (var (name, member) in byContext["properties"]!.AsObject())
        {
            member.Should().BeOfType<JsonObject>($"'{name}' is described").Which.ContainsKey("type").Should().BeTrue(name);
        }

        JsonNode.DeepEquals(byReflection, byContext).Should().BeTrue($"{byReflection.ToJsonString()}\n{byContext.ToJsonString()}");
    }

    /// <summary>
    /// The keyword each declared rule becomes, for each underlying type, nullable, as the element of a collection and as
    /// the value or the key of a dictionary, with a format JSON Schema does not define moved into the description for a
    /// language model, and a closed set naming its values there.
    /// </summary>
    /// <param name="member">The member of the catalog.</param>
    /// <param name="profile">The profile.</param>
    /// <param name="expected">The schema expected for it.</param>
    [Theory]
    [MemberData(nameof(Rules))]
    public void The_schema_of_a_value_object_states_the_rules_it_declares(
        string member,
        ValueObjectJsonSchemaProfile profile,
        string expected)
    {
        foreach (var resolver in Resolvers)
        {
            var schema = Member(Export(OptionsFor(resolver), typeof(Catalog), profile), member);

            JsonNode.DeepEquals(schema, JsonNode.Parse(expected)).Should().BeTrue($"{resolver} wrote {schema.ToJsonString()}");
        }
    }

    /// <summary>
    /// A value object at the root of the schema is described too, a nullable one with <c>null</c> in its type.
    /// </summary>
    [Fact]
    public void A_nullable_value_object_at_the_root_allows_null()
    {
        var schema = Export(OptionsFor("reflection"), typeof(BirthDate?), OpenApi);

        schema["type"]!.ToJsonString().Should().Be("""["string","null"]""");
        schema["format"]!.GetValue<string>().Should().Be("date");
    }

    /// <summary>
    /// A number the options let be read from text is a number or a string held to the form a number is written in, as
    /// System.Text.Json documents the number alone, for OpenAPI; a language model is asked for the number alone.
    /// </summary>
    [Fact]
    public void A_number_the_options_let_be_read_as_text_may_be_a_string_under_the_OpenAPI_profile_alone()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

        var openApi = Export(options, typeof(Catalog), OpenApi);
        var languageModel = Export(options, typeof(Catalog), LanguageModel);

        ShouldDescribe(Member(openApi, "quantity"),
            """{"description":"A quantity of items, tested to cover the narrow integer promotion path.","type":["string","integer","null"],"format":"int32","pattern":"^-?(?:0|[1-9]\\d*)$","minimum":0,"maximum":1000}""");
        Member(openApi, "amount")["pattern"]!.GetValue<string>().Should().Be(@"^-?(?:0|[1-9]\d*)(?:\.\d+)?$");
        Member(openApi, "latitude")["pattern"]!.GetValue<string>().Should().Be(@"^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$");
        ShouldDescribe(Member(languageModel, "quantity"),
            """{"description":"A quantity of items, tested to cover the narrow integer promotion path.\n\nFormat: int32.","type":["integer","null"],"minimum":0,"maximum":1000}""");
    }

    /// <summary>
    /// A number the options write as text keeps <c>minimum</c> and <c>maximum</c> for the number it may be, and states
    /// its bounds in the description for the string it is written as. A language model is still asked for a number, and
    /// shown its example and its known values as numbers.
    /// </summary>
    [Fact]
    public void A_number_written_as_text_states_its_bounds_in_the_description_and_is_shown_as_a_number_to_a_language_model()
    {
        var options = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.WriteAsString,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };

        var openApi = Export(options, typeof(Catalog), OpenApi);
        var languageModel = Export(options, typeof(Catalog), LanguageModel);

        ShouldDescribe(Member(openApi, "Port"),
            """{"description":"A TCP port.\n\nAt least 1.","type":["string","integer"],"format":"int32","pattern":"^-?(?:0|[1-9]\\d*)$","minimum":1,"examples":["8080"]}""");
        ShouldDescribe(Member(languageModel, "Port"),
            """{"description":"A TCP port.\n\nFormat: int32.","type":"integer","minimum":1,"examples":[8080]}""");
        Member(languageModel, "Vat")["enum"]!.ToJsonString().Should().Be("[20.0,5.5]");
    }

    /// <summary>
    /// A real under <see cref="JsonNumberHandling.AllowNamedFloatingPointLiterals"/> may be one of the named literals its
    /// bounds let through, as System.Text.Json documents a bare real; one bounded on both sides lets none through. A
    /// language model is asked for the number alone.
    /// </summary>
    [Fact]
    public void A_real_under_named_literals_may_be_those_its_bounds_let_through_for_OpenAPI()
    {
        var options = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };

        var openApi = Export(options, typeof(Gauges), OpenApi);
        var languageModel = Export(options, typeof(Gauges), LanguageModel);

        ShouldDescribe(Member(openApi, "Tolerance"),
            """{"description":"A measuring tolerance, as a fraction no larger than one.","anyOf":[{"type":"number","format":"double","maximum":1},{"enum":["-Infinity"]}]}""");
        ShouldDescribe(Member(openApi, "Reading"),
            """{"description":"A reading nothing bounds, which an instrument may fail to take.","anyOf":[{"type":["number","null"],"format":"double"},{"enum":["NaN","Infinity","-Infinity"]}],"examples":["NaN"]}""");
        ShouldDescribe(Member(openApi, "Latitude"),
            """{"description":"A latitude, in degrees.","type":"number","format":"double","minimum":-90,"maximum":90}""");
        ShouldDescribe(Member(languageModel, "Tolerance"),
            """{"description":"A measuring tolerance, as a fraction no larger than one.\n\nFormat: double.","type":"number","maximum":1}""");
    }

    /// <summary>
    /// An example the options write as text that is no number, a named literal, stays the text it is for a language
    /// model: no JSON number writes it.
    /// </summary>
    [Fact]
    public void A_named_literal_written_as_text_stays_text_for_a_language_model()
    {
        var options = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };

        var schema = Export(options, typeof(Gauges), LanguageModel);

        Member(schema, "Reading")["examples"]!.ToJsonString().Should().Be("""["NaN"]""");
    }

    /// <summary>
    /// A host's schema is completed rather than replaced: its description comes first and the value object's follows,
    /// once, and a keyword the value object does not declare stays, while what describes the wrapper as an object goes.
    /// </summary>
    [Fact]
    public void A_description_and_a_keyword_the_host_wrote_stay()
    {
        var typeInfo = OptionsFor("reflection").GetTypeInfo(typeof(Iban));
        var host = new JsonObject
        {
            ["description"] = "The account to debit.",
            ["default"] = "FR7630006000011234567890189",
            ["properties"] = new JsonObject { ["Value"] = true },
            ["required"] = new JsonArray("Value"),
            ["format"] = "byte",
        };

        var schema = ValueObjectJsonSchema.Apply(typeInfo, host);
        var again = ValueObjectJsonSchema.Apply(typeInfo, schema);

        schema.Should().BeSameAs(host);
        again.Should().BeSameAs(host);
        host["description"]!.GetValue<string>().Should().Be("The account to debit.\n\nAn International Bank Account Number, stored in its electronic form.");
        host["default"]!.GetValue<string>().Should().Be("FR7630006000011234567890189");
        host.ContainsKey("properties").Should().BeFalse();
        host.ContainsKey("required").Should().BeFalse();
        host["format"]!.GetValue<string>().Should().Be("iban");
    }

    /// <summary>
    /// A description equal to the value object's is not repeated, and a format the host wrote goes when the value object
    /// declares none.
    /// </summary>
    [Fact]
    public void A_description_the_host_wrote_as_the_value_object_s_is_not_repeated()
    {
        var typeInfo = OptionsFor("reflection").GetTypeInfo(typeof(Label));
        var host = new JsonObject
        {
            ["description"] = "A free-text label, which may be empty, and whose wide formats outgrow the emitted stack buffer.",
            ["format"] = "byte",
        };

        ValueObjectJsonSchema.Apply(typeInfo, host);

        host["description"]!.GetValue<string>().Should().Be(
            "A free-text label, which may be empty, and whose wide formats outgrow the emitted stack buffer.");
        host.ContainsKey("format").Should().BeFalse();
    }

    /// <summary>
    /// A host that already allows <c>null</c> keeps allowing it, whether it wrote its type as an array or alone, and a
    /// description that is no text is replaced.
    /// </summary>
    /// <param name="type">The type the host wrote.</param>
    /// <param name="expected">The type of the schema completed.</param>
    [Theory]
    [InlineData("""["string","null"]""", """["string","null"]""")]
    [InlineData("\"null\"", """["string","null"]""")]
    [InlineData("\"object\"", "\"string\"")]
    [InlineData("[1]", "\"string\"")]
    public void A_null_the_host_allows_stays_allowed(string type, string expected)
    {
        var typeInfo = OptionsFor("reflection").GetTypeInfo(typeof(Fingerprint));
        var host = new JsonObject { ["type"] = JsonNode.Parse(type), ["description"] = 42 };

        ValueObjectJsonSchema.Apply(typeInfo, host);

        host["type"]!.ToJsonString().Should().Be(expected);
        host["description"]!.GetValue<string>().Should().Be("The fingerprint of a document's content.");
    }

    /// <summary>
    /// Nothing that is not a value object, or a container of them, is touched, nor is the schema <c>false</c>, which
    /// accepts nothing, whether it stands for a value object or for its element.
    /// </summary>
    [Fact]
    public void A_schema_that_does_not_describe_a_value_object_is_left_as_it_is()
    {
        var options = OptionsFor("reflection");
        var text = JsonNode.Parse("""{"type":"string"}""")!;
        var record = JsonNode.Parse("""{"type":"object"}""")!;
        var numbers = JsonNode.Parse("""{"type":"array"}""")!;
        var nothing = JsonValue.Create(false);
        var noElement = JsonNode.Parse("""{"type":"array","items":false}""")!;

        ValueObjectJsonSchema.Apply(options.GetTypeInfo(typeof(string)), text).ToJsonString().Should().Be("""{"type":"string"}""");
        ValueObjectJsonSchema.Apply(options.GetTypeInfo(typeof(Catalog)), record).Should().BeSameAs(record);
        ValueObjectJsonSchema.Apply(options.GetTypeInfo(typeof(List<int>)), numbers).ToJsonString().Should().Be("""{"type":"array"}""");
        ValueObjectJsonSchema.Apply(options.GetTypeInfo(typeof(Iban)), nothing).Should().BeSameAs(nothing);
        ValueObjectJsonSchema.Apply(options.GetTypeInfo(typeof(List<Iban>)), nothing).Should().BeSameAs(nothing);
        ValueObjectJsonSchema.Apply(options.GetTypeInfo(typeof(List<Iban>)), noElement).ToJsonString().Should().Be("""{"type":"array","items":false}""");
    }

    /// <summary>
    /// What a host wrote for an element or a key, which the exporter never hands the transform, is completed as the
    /// schema of a value object is: the <c>{}</c> Microsoft.Extensions.AI writes for the element of a collection, the
    /// schema <c>true</c>, or a description of its own.
    /// </summary>
    [Fact]
    public void What_a_host_wrote_for_an_element_or_a_key_is_completed()
    {
        var options = OptionsFor("reflection");
        var empty = JsonNode.Parse("""{"type":"array","items":{}}""")!.AsObject();
        var anything = JsonNode.Parse("""{"type":"array","items":true}""")!.AsObject();
        var dictionary = JsonNode.Parse("""{"type":"object","additionalProperties":{"description":"The rate."},"propertyNames":{"description":"The country."}}""")!.AsObject();
        var element = empty["items"];

        ValueObjectJsonSchema.Apply(options.GetTypeInfo(typeof(List<Score>)), empty);
        ValueObjectJsonSchema.Apply(options.GetTypeInfo(typeof(List<Score>)), anything);
        ValueObjectJsonSchema.Apply(options.GetTypeInfo(typeof(Dictionary<CountryCode, Score>)), dictionary);

        empty["items"].Should().BeSameAs(element);
        ShouldDescribe(empty["items"]!, """{"description":"A score out of a hundred.","type":"integer","format":"int32","maximum":100}""");
        ShouldDescribe(anything["items"]!, """{"description":"A score out of a hundred.","type":"integer","format":"int32","maximum":100}""");
        dictionary["additionalProperties"]!["description"]!.GetValue<string>().Should().Be("The rate.\n\nA score out of a hundred.");
        dictionary["propertyNames"]!["enum"]!.ToJsonString().Should().Be("""["FR","BE","LU"]""");
        dictionary["propertyNames"]!["description"]!.GetValue<string>().Should().StartWith("The country.\n\n");
    }

    /// <summary>
    /// A value object written by hand without a converter is written by System.Text.Json as an object with properties,
    /// and so is a nullable one: the schema the exporter built for it is right, and is left as it is.
    /// </summary>
    [Fact]
    public void A_value_object_the_serializer_writes_as_an_object_is_left_as_it_is()
    {
        var options = OptionsFor("reflection");
        var exported = JsonSchemaExporter.GetJsonSchemaAsNode(options, typeof(HandWrittenCode?));

        var schema = Export(options, typeof(HandWrittenCode?), OpenApi);

        JsonNode.DeepEquals(schema, exported).Should().BeTrue(schema.ToJsonString());
        schema["properties"].Should().NotBeNull();
    }

    /// <summary>
    /// A value object nothing registered, here a construction of a generic one whose converter the options name
    /// themselves, is described by reflection, as the registry describes it everywhere else.
    /// </summary>
    [Fact]
    public void A_value_object_nothing_registered_is_described_by_reflection()
    {
#pragma warning disable IL2026, IL3050 // The general-purpose converter is what a value object nothing registered is serialized with.
        var options = new JsonSerializerOptions
        {
            Converters = { new ValueObjectJsonConverter<Reference<Journal>, string>() },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
#pragma warning restore IL2026, IL3050
        ValueObjectRegistry.TryGet(typeof(Reference<Journal>), out _).Should().BeFalse("nothing resolved it yet");

        var schema = ValueObjectJsonSchema.Apply(options.GetTypeInfo(typeof(Reference<Journal>)), JsonValue.Create(true));

        schema["maxLength"]!.GetValue<int>().Should().Be(12);
        schema["examples"]!.ToJsonString().Should().Be("""["PO-1042"]""");
    }

    /// <summary>
    /// A closed set whose schema, built by hand, does not detail its values one for one names none of them: a name
    /// beside the wrong value would mislead the model.
    /// </summary>
    [Fact]
    public void A_closed_set_whose_details_do_not_list_its_values_names_none()
    {
        ValueObjectRegistry.EnsureAssemblyRegistered(typeof(Shade).Assembly);
        ValueObjectRegistry.Register(ValueObjectDescriptor.For<Shade, string>(new ValueObjectSchema
        {
            IsClosedValueSet = true,
            KnownValues = ["light", "dark"],
            KnownValueDetails = [new KnownValueInfo("light", "Light")],
        }));

        var schema = Export(OptionsFor("reflection"), typeof(Shade), LanguageModel);

        ShouldDescribe(schema, """{"type":"string","enum":["light","dark"]}""");
    }

    /// <summary>
    /// A bound a schema built by hand holds, which the generator would have refused, is published as the type can: one
    /// the underlying type cannot read, or of a type that takes no bound, in a sentence as it was declared; a number that
    /// is no number not at all; and one the converter refuses to write, a zero the type rejects at its default, as its
    /// text.
    /// </summary>
    [Fact]
    public void A_bound_a_schema_built_by_hand_holds_is_published_as_the_type_can()
    {
        ValueObjectRegistry.EnsureAssemblyRegistered(typeof(Stamp).Assembly);
        ValueObjectRegistry.Register(ValueObjectDescriptor.For<Stamp, DateTime>(new ValueObjectSchema { Minimum = "soon" }));
        ValueObjectRegistry.Register(ValueObjectDescriptor.For<Fee, decimal>(new ValueObjectSchema { Minimum = "low" }));
        ValueObjectRegistry.Register(ValueObjectDescriptor.For<Tag, string>(new ValueObjectSchema { Minimum = "a", Maximum = "z" }));
        ValueObjectRegistry.Register(ValueObjectDescriptor.For<Odd, int>(new ValueObjectSchema { Minimum = "0", Maximum = "9" }));
        var options = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.WriteAsString,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };

        ShouldDescribe(Export(options, typeof(Stamp), OpenApi), """{"description":"At least soon.","type":"string"}""");
        ShouldDescribe(Export(options, typeof(Fee), LanguageModel), """{"type":"number"}""");
        ShouldDescribe(Export(options, typeof(Tag), OpenApi), """{"description":"Between a and z, inclusive.","type":"string"}""");
        ShouldDescribe(
            Export(options, typeof(Odd), OpenApi),
            """{"description":"Between 0 and 9, inclusive.","type":["string","integer"],"pattern":"^-?(?:0|[1-9]\\d*)$","minimum":0,"maximum":9}""");
    }

    /// <summary>
    /// The transform of a profile is one instance, and the default one is the OpenAPI profile's; a profile no member of
    /// the enumeration names is refused, and so are missing arguments.
    /// </summary>
    [Fact]
    public void The_transforms_and_their_arguments()
    {
        var typeInfo = OptionsFor("reflection").GetTypeInfo(typeof(Port));
        var undefined = (ValueObjectJsonSchemaProfile)42;

        ValueObjectJsonSchema.CreateTransform(OpenApi).Should().BeSameAs(ValueObjectJsonSchema.CreateTransform(OpenApi));
        ValueObjectJsonSchema.CreateTransform(LanguageModel).Should().NotBeSameAs(ValueObjectJsonSchema.CreateTransform(OpenApi));
        JsonNode.DeepEquals(
            ValueObjectJsonSchema.Apply(typeInfo, JsonValue.Create(true)),
            ValueObjectJsonSchema.Apply(typeInfo, JsonValue.Create(true), OpenApi)).Should().BeTrue();

        FluentActions.Invoking(() => ValueObjectJsonSchema.CreateTransform(undefined))
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("profile");
        FluentActions.Invoking(() => ValueObjectJsonSchema.Apply(typeInfo, JsonValue.Create(true), undefined))
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("profile");
        FluentActions.Invoking(() => ValueObjectJsonSchema.Apply(null!, JsonValue.Create(true)))
            .Should().Throw<ArgumentNullException>().WithParameterName("typeInfo");
        FluentActions.Invoking(() => ValueObjectJsonSchema.Apply(typeInfo, null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("schema");
        FluentActions.Invoking(() => ValueObjectJsonSchema.TransformSchemaNode(default, JsonValue.Create(true)))
            .Should().Throw<ArgumentNullException>().WithParameterName("typeInfo");
    }

    /// <summary>
    /// What the serializer writes is valid against the schema exported for it, under either resolver, and under the
    /// options that widen a number for OpenAPI; and a value a rule refuses is invalid against it, so the schema is not
    /// one that accepts anything.
    /// </summary>
    /// <param name="resolver">How the options describe the type.</param>
    [Theory]
    [MemberData(nameof(Resolvers))]
    public void What_the_serializer_writes_is_valid_against_the_schema(string resolver)
    {
        var options = OptionsFor(resolver);
        var written = JsonSerializer.SerializeToElement(Sample, options.GetTypeInfo(typeof(Catalog)));

        foreach (var profile in (ValueObjectJsonSchemaProfile[])[OpenApi, LanguageModel])
        {
            var schema = Validator(Export(options, typeof(Catalog), profile));

            Evaluate(schema, written).Should().BeEmpty(profile.ToString());
            Evaluate(schema, With(written, "Quantity", 5000)).Should().ContainSingle(profile.ToString()).Which.Should().Contain("Quantity");
            Evaluate(schema, With(written, "Country", "DE")).Should().ContainSingle(profile.ToString()).Which.Should().Contain("Country");
            Evaluate(schema, With(written, "Iban", "FR76")).Should().ContainSingle(profile.ToString()).Which.Should().Contain("Iban");
        }
    }

    /// <summary>
    /// Under the options that let a number be written as text and a real be a named literal, what the serializer writes
    /// is valid against the OpenAPI profile, which describes exactly that wire.
    /// </summary>
    [Fact]
    public void What_the_serializer_writes_under_widened_numbers_is_valid_against_the_OpenAPI_profile()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        var catalog = JsonSerializer.SerializeToElement(Sample, options);
        var gauges = JsonSerializer.SerializeToElement(new Gauges(Tolerance.Create(double.NegativeInfinity), Reading.Create(double.NaN), Latitude.Create(45)), options);

        Evaluate(Validator(Export(options, typeof(Catalog), OpenApi)), catalog).Should().BeEmpty();
        Evaluate(Validator(Export(options, typeof(Gauges), OpenApi)), gauges).Should().BeEmpty();
    }

    /// <summary>
    /// Readings of instruments, over doubles bounded on one side, on none, and on both.
    /// </summary>
    internal sealed record Gauges(Tolerance Tolerance, Reading? Reading, Latitude Latitude);

    /// <summary>
    /// The owner of a reference no other test resolves, so that its construction reaches the schema unregistered.
    /// </summary>
    internal sealed class Journal;

    /// <summary>A reading nothing bounds, which an instrument may fail to take.</summary>
    [ValueObject<double>(Example = "NaN")]
    public readonly partial struct Reading;

    /// <summary>A shade no other test uses, whose registration one test replaces.</summary>
    [ValueObject<string>]
    public readonly partial struct Shade;

    /// <summary>A time stamp no other test uses, whose registration one test replaces.</summary>
    [ValueObject<DateTime>]
    public readonly partial struct Stamp;

    /// <summary>A fee no other test uses, whose registration one test replaces.</summary>
    [ValueObject<decimal>]
    public readonly partial struct Fee;

    /// <summary>A tag no other test uses, whose registration one test replaces.</summary>
    [ValueObject<string>]
    public readonly partial struct Tag;

    /// <summary>An odd number, so that zero, its default, is refused, whose registration one test replaces.</summary>
    [ValueObject<int>]
    public readonly partial struct Odd : IValueObjectValidator<int>
    {
        public static ValidationResult ValidateValue(in int value)
            => value % 2 != 0 ? ValidationResult.Success : ValidationResult.InvalidFormat("An odd number is required.");
    }

    private static Catalog Sample => new(
        Consent.Create(true),
        Grade.Create('B'),
        Adjustment.Create(-3),
        Score.Create(90),
        Port.Create(8080),
        PageNumber.First,
        SequenceNumber.Create(0),
        FileSize.Create(1024),
        ByteCount.Create(ulong.MaxValue),
        LedgerBalance.Create(Int128.Parse("-999999999999999999999", System.Globalization.CultureInfo.InvariantCulture)),
        Fingerprint.Create(UInt128.MaxValue),
        Latitude.Create(48.85),
        Ratio.Create(0.5f),
        OpeningTime.Create(new TimeOnly(8, 30)),
        RecordedAt.Create(new DateTime(2024, 1, 31, 8, 30, 0, DateTimeKind.Utc)),
        OccurredAt.Create(new DateTimeOffset(2024, 1, 31, 8, 30, 0, TimeSpan.FromHours(1))),
        Duration.Create(TimeSpan.FromMinutes(90)),
        EffectiveDate.Create(new DateOnly(2024, 1, 31)),
        Tolerance.Create(0.25),
        PhoneNumber.Create("+33123456789"),
        Label.Create(string.Empty),
        DocumentStatus.Final,
        Iban.Create("FR7630006000011234567890189"),
        Amount.Create(1250m),
        CustomerId.Create(Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff")),
        CountryCode.France,
        Percentage.Create(12.5m),
        VatRate.Reduced,
        Reference<PurchaseOrder>.Create("PO-1042"),
        null,
        Domain.Quantity.Create(3),
        CountryCode.Belgium,
        [Iban.Create("DE89370400440532013000")],
        [null, Iban.Create("DE89370400440532013000")],
        new() { ["net"] = Amount.Create(10m) },
        new() { [CountryCode.Luxembourg] = 2 },
        new() { [Domain.Quantity.Create(4)] = 1 },
        [[Port.Create(443)]],
        "Shelf B",
        7);

    private static JsonSerializerOptions OptionsFor(string resolver) => resolver == "context"
        ? CatalogContext.Default.Options
        : new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    private static JsonNode Export(JsonSerializerOptions options, Type type, ValueObjectJsonSchemaProfile profile)
        => JsonSchemaExporter.GetJsonSchemaAsNode(
            options,
            type,
            new JsonSchemaExporterOptions { TransformSchemaNode = ValueObjectJsonSchema.CreateTransform(profile) });

    private static JsonNode Member(JsonNode schema, string name) => schema["properties"]![name]!;

    /// <summary>
    /// Asserts that a schema holds exactly the keywords expected, in whatever order.
    /// </summary>
    private static void ShouldDescribe(JsonNode schema, string expected)
        => JsonNode.DeepEquals(schema, JsonNode.Parse(expected)).Should().BeTrue($"the schema is {schema.ToJsonString()}");

    /// <summary>
    /// Reads an exported schema as the draft System.Text.Json writes, 2020-12, in which <c>format</c> annotates rather
    /// than asserts.
    /// </summary>
    private static JsonSchema Validator(JsonNode schema)
        => JsonSchema.FromText(schema.ToJsonString(), new BuildOptions { Dialect = Dialect.Draft202012 });

    /// <summary>
    /// Evaluates a payload against a schema, answering where each failing keyword stands, or nothing for a valid one.
    /// </summary>
    private static List<string> Evaluate(JsonSchema schema, JsonElement payload)
    {
        var results = schema.Evaluate(payload, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (results.IsValid)
        {
            // The alternatives of an anyOf the payload did not take report errors of their own.
            return [];
        }

        return [.. results.Details!
            .Where(detail => detail.Errors is { Count: > 0 })
            .Select(detail => $"{detail.InstanceLocation}: {string.Join(", ", detail.Errors!.Keys)}")];
    }

    /// <summary>
    /// Replaces one member of a payload.
    /// </summary>
    private static JsonElement With(JsonElement payload, string member, JsonNode value)
    {
        var node = JsonNode.Parse(payload.GetRawText())!;
        node[member] = value;

        return JsonSerializer.SerializeToElement(node);
    }
}
