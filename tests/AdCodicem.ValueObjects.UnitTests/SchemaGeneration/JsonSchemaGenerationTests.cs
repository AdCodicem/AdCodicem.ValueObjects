using System.Globalization;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.NewtonsoftJson;
using AdCodicem.ValueObjects.NJsonSchema;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.TestData;
using Namotion.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.Generation.TypeMappers;
using NJsonSchema.NewtonsoftJson.Generation;
using SystemTextJsonDefaults = System.Text.Json.JsonSerializerDefaults;
using SystemTextJsonOptions = System.Text.Json.JsonSerializerOptions;
using SystemTextJsonSerializer = System.Text.Json.JsonSerializer;

namespace AdCodicem.ValueObjects.UnitTests.SchemaGeneration;

/// <summary>
/// The schemas NJsonSchema's two generators write for value objects once <c>AddValueObjects()</c> is called on their
/// settings, as Confluent's serdes and NSwag hand them over: one definition per value object, its underlying value
/// carrying its rules, for the wire the settings' serializer writes.
/// </summary>
public sealed partial class JsonSchemaGenerationTests
{
    public static TheoryData<string> Every => GeneratedSurface.Samples.Names;

    [Theory]
    [MemberData(nameof(Every))]
    public void Every_value_object_is_its_underlying_type_on_every_wire_and_what_each_writes_validates(string type)
        => GeneratedSurface.Samples.All[type].DescribesItselfInEveryNJsonSchemaWire();

    [Fact]
    public void Under_Newtonsoft_Json_with_the_converter_each_value_object_is_one_definition_of_its_value()
    {
        var schema = SchemaJson.Generate<SchemaOrder>(SchemaJson.NewtonsoftWithConverter());

        SchemaJson.Property(schema, "Iban").Should().Be("""{"$ref":"#/definitions/Iban"}""");
        SchemaJson.Property(schema, "OptionalIban").Should().Be("""{"oneOf":[{"type":"null"},{"$ref":"#/definitions/Iban"}]}""");
        SchemaJson.Property(schema, "OptionalCountry").Should().Be("""{"oneOf":[{"type":"null"},{"$ref":"#/definitions/CountryCode"}]}""");
        SchemaJson.Property(schema, "Order").Should().Be("""{"$ref":"#/definitions/ReferenceOfPurchaseOrder"}""");
        SchemaJson.Property(schema, "OptionalOrder").Should().Be("""{"oneOf":[{"type":"null"},{"$ref":"#/definitions/ReferenceOfPurchaseOrder"}]}""");
        SchemaJson.Property(schema, "Ibans").Should().Be("""{"type":"array","items":{"$ref":"#/definitions/Iban"}}""");
        SchemaJson.Property(schema, "OptionalQuantities").Should().Be("""{"type":"array","items":{"oneOf":[{"type":"null"},{"$ref":"#/definitions/Quantity"}]}}""");
        SchemaJson.Property(schema, "AmountByCountry").Should().Be("""{"type":"object","additionalProperties":{"$ref":"#/definitions/Amount"}}""");
        SchemaJson.Property(schema, "QuantityByName").Should().Be("""{"type":"object","additionalProperties":{"$ref":"#/definitions/Quantity"}}""");

        SchemaJson.Definition(schema, "Iban").Should().Be(
            """{"type":"string","description":"An International Bank Account Number, stored in its electronic form.","format":"iban","maxLength":34,"minLength":15,"pattern":"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$","x-example":"FR7630006000011234567890189"}""");
        SchemaJson.Definition(schema, "Quantity").Should().Be(
            """{"type":"integer","description":"A quantity of items, tested to cover the narrow integer promotion path.","format":"int32","maximum":1000.0,"minimum":0.0}""");
        SchemaJson.Definition(schema, "Amount").Should().Be(
            """{"type":"number","description":"A monetary amount in the ambient currency, never negative.","format":"decimal","minimum":0.0,"x-example":1250.0}""");
        SchemaJson.Definition(schema, "Consent").Should().Be("""{"type":"boolean","description":"Whether a customer agreed to be contacted.","x-example":true}""");
        SchemaJson.Definition(schema, "Grade").Should().Be(
            """{"type":"string","description":"A school grade, from A to F.\n\nBetween A and F, inclusive.","maxLength":1,"minLength":1,"x-minimum":"A","x-maximum":"F"}""");
        SchemaJson.Definition(schema, "BirthDate").Should().Contain("\"format\":\"date\",\"x-minimum\":\"1900-01-01\",\"x-maximum\":\"2100-12-31\"");
        SchemaJson.Definition(schema, "LedgerBalance").Should().Contain("\"x-minimum\":\"-1000000000000000000000\",\"x-maximum\":\"1000000000000000000000\"");
        SchemaJson.Definition(schema, "Duration").Should().Contain("\"pattern\":\"^-?(\\\\d+\\\\.)?\\\\d{2}:\\\\d{2}:\\\\d{2}(\\\\.\\\\d{1,7})?$\",\"x-example\":\"01:30:00\"");
        // Written as ValueObjectConverter writes the value, its fraction left out when it is zero, as System.Text.Json does.
        SchemaJson.Definition(schema, "RecordedAt").Should().Contain("\"x-minimum\":\"2000-01-01T00:00:00\",\"x-maximum\":\"2099-12-31T00:00:00\"");
        SchemaJson.Definition(schema, "ReferenceOfPurchaseOrder").Should().StartWith("""{"type":"string","description":"A reference to a document""").And.EndWith(""","maxLength":12,"x-example":"PO-1042"}""");
        SchemaJson.Definition(schema, "CountryCode").Should().Be(
            """{"type":"string","description":"An ISO 3166-1 alpha-2 country code restricted to the countries the application serves.","maxLength":2,"minLength":2,"x-enumNames":["France","Belgium","Luxembourg"],"x-enum-descriptions":["France","Belgium","Luxembourg"],"enum":["FR","BE","LU"],"x-enum-varnames":["France","Belgium","Luxembourg"],"x-ms-enum":{"name":"CountryCode","modelAsString":false,"values":[{"value":"FR","name":"France","description":"France"},{"value":"BE","name":"Belgium","description":"Belgium"},{"value":"LU","name":"Luxembourg","description":"Luxembourg"}]}}""");
        SchemaJson.Definition(schema, "DocumentStatus").Should().Contain(
            ""","x-enumNames":["Draft","Final"],"x-enum-descriptions":[null,"Signed off, and no longer edited."],"enum":["draft","final"]""");

        // One definition per value object, holding none of the members it has in memory.
        ((JObject)schema["definitions"]!).Properties().Select(static definition => definition.Name).Should().BeEquivalentTo(
            "Iban", "Quantity", "Amount", "CustomerId", "CountryCode", "BirthDate", "Consent", "Grade", "ByteCount", "LedgerBalance",
            "Latitude", "OpeningTime", "RecordedAt", "OccurredAt", "Duration", "DocumentStatus", "ReferenceOfPurchaseOrder");
        schema.ToString().Should().NotContain("IsZero").And.NotContain("CountryCode\":{\"type\":\"string\"},\"Value\"");
    }

    [Fact]
    public void Under_Newtonsoft_Json_without_the_converter_each_value_object_is_the_text_of_its_type_converter()
    {
        var schema = SchemaJson.Generate<SchemaOrder>(SchemaJson.NewtonsoftText());

        SchemaJson.Property(schema, "Quantity").Should().Be("""{"$ref":"#/definitions/Quantity"}""");
        SchemaJson.Definition(schema, "Quantity").Should().Be(
            """{"type":"string","description":"A quantity of items, tested to cover the narrow integer promotion path.\n\nBetween 0 and 1000, inclusive.","format":"int32","pattern":"^-?(?:0|[1-9]\\d*)$","x-minimum":"0","x-maximum":"1000"}""");
        SchemaJson.Definition(schema, "Amount").Should().Be(
            """{"type":"string","description":"A monetary amount in the ambient currency, never negative.\n\nAt least 0.","format":"decimal","pattern":"^-?(?:0|[1-9]\\d*)(?:\\.\\d+)?$","x-example":"1250.00","x-minimum":"0"}""");
        SchemaJson.Definition(schema, "Consent").Should().Be(
            """{"type":"string","description":"Whether a customer agreed to be contacted.","pattern":"^(?:[Tt]rue|[Ff]alse)$","x-example":"True"}""");
        SchemaJson.Definition(schema, "Iban").Should().Contain("\"pattern\":\"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$\"");
        SchemaJson.Definition(schema, "CountryCode").Should().Contain("\"enum\":[\"FR\",\"BE\",\"LU\"]");
    }

    [Fact]
    public void Each_wire_validates_its_own_payload_and_refuses_the_payload_of_the_other()
    {
        var order = SchemaOrder.Sample();
        var withConverter = SchemaJson.NewtonsoftWithConverter();
        var text = SchemaJson.NewtonsoftText();
        var valueSchema = JsonSchemaGenerator.FromType<SchemaOrder>(withConverter);
        var textSchema = JsonSchemaGenerator.FromType<SchemaOrder>(text);
        var valuePayload = JsonConvert.SerializeObject(order, withConverter.SerializerSettings);
        var textPayload = JsonConvert.SerializeObject(order, text.SerializerSettings);

        valuePayload.Should().Contain("\"Quantity\":7").And.Contain("\"Consent\":true");
        textPayload.Should().Contain("\"Quantity\":\"7\"").And.Contain("\"Consent\":\"True\"");
        SchemaJson.Errors(valueSchema, valuePayload).Should().BeEmpty();
        SchemaJson.Errors(textSchema, textPayload).Should().BeEmpty();
        SchemaJson.Errors(valueSchema, textPayload).Should().Contain(["IntegerExpected #/Quantity", "BooleanExpected #/Consent", "NumberExpected #/Amount"]);
        SchemaJson.Errors(textSchema, valuePayload).Should().Contain(["StringExpected #/Quantity", "StringExpected #/Consent"]);

        foreach (var options in (SystemTextJsonOptions[])[new(), new(SystemTextJsonDefaults.Web)])
        {
            var schema = JsonSchemaGenerator.FromType<SchemaOrder>(SchemaJson.SystemTextJson(options));
            SchemaJson.Errors(schema, SystemTextJsonSerializer.Serialize(order, options)).Should().BeEmpty();
        }
    }

    [Theory]
    [InlineData("Iban", "\"XX\"", "PatternMismatch #/Iban", "StringTooShort #/Iban")]
    [InlineData("Quantity", "5000", "NumberTooBig #/Quantity")]
    [InlineData("Country", "\"ZZ\"", "NotInEnumeration #/Country")]
    [InlineData("Took", "\"PT1H\"", "PatternMismatch #/Took")]
    [InlineData("Consent", "\"True\"", "BooleanExpected #/Consent")]
    [InlineData("Order", "\"PURCHASE-ORDER-1042\"", "StringTooLong #/Order")]
    public void A_member_the_value_object_refuses_fails_the_schema_with_the_rule_it_breaks(string member, string json, params string[] errors)
    {
        var settings = SchemaJson.NewtonsoftWithConverter();
        var payload = JObject.Parse(JsonConvert.SerializeObject(SchemaOrder.Sample(), settings.SerializerSettings));
        payload[member] = JToken.Parse(json);

        SchemaJson.Errors(JsonSchemaGenerator.FromType<SchemaOrder>(settings), payload.ToString()).Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void A_null_where_a_value_object_is_optional_is_valid_and_where_it_is_required_is_not()
    {
        var settings = SchemaJson.NewtonsoftWithConverter();
        var schema = JsonSchemaGenerator.FromType<SchemaOrder>(settings);
        var payload = JObject.Parse(JsonConvert.SerializeObject(SchemaOrder.Sample(), settings.SerializerSettings));
        payload["OptionalCountry"] = JValue.CreateNull();
        payload["OptionalQuantities"] = new JArray(JValue.CreateNull());

        SchemaJson.Errors(schema, payload.ToString()).Should().BeEmpty();

        payload["Country"] = JValue.CreateNull();
        SchemaJson.Errors(schema, payload.ToString()).Should().Contain("NotInEnumeration #/Country");
    }

    [Fact]
    public void Under_System_Text_Json_numbers_are_numbers_and_the_options_are_left_as_they_were()
    {
        var settings = SchemaJson.SystemTextJson();
        var schema = SchemaJson.Generate<SchemaOrder>(settings);

        SchemaJson.Definition(schema, "Quantity").Should().Be(
            """{"type":"integer","description":"A quantity of items, tested to cover the narrow integer promotion path.","format":"int32","maximum":1000.0,"minimum":0.0}""");
        SchemaJson.Definition(schema, "Amount").Should().Contain("\"type\":\"number\"").And.Contain("\"x-example\":1250.0");
        SchemaJson.Definition(schema, "Consent").Should().Contain("\"type\":\"boolean\"").And.Contain("\"x-example\":true");
        SchemaJson.Property(schema, "OptionalIban").Should().Be("""{"oneOf":[{"type":"null"},{"$ref":"#/definitions/Iban"}]}""");
        ((JObject)schema["definitions"]!).ContainsKey("Int128").Should().BeFalse("a 128-bit integer travels as text");

        settings.SerializerOptions.TypeInfoResolver.Should().BeNull("the schema reads a copy of the options");
        settings.SerializerOptions.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public void Under_the_web_defaults_a_JSON_Schema_takes_a_number_as_text_too_and_a_document_does_not()
    {
        var web = new SystemTextJsonOptions(SystemTextJsonDefaults.Web);

        var jsonSchema = SchemaJson.Generate<SchemaOrder>(SchemaJson.SystemTextJson(web));
        SchemaJson.Definition(jsonSchema, "Quantity").Should().Be(
            """{"type":["integer","string"],"description":"A quantity of items, tested to cover the narrow integer promotion path.","format":"int32","maximum":1000.0,"minimum":0.0,"pattern":"^-?(?:0|[1-9]\\d*)$"}""");

        var document = SchemaJson.Generate<SchemaOrder>(SchemaJson.SystemTextJson(web, SchemaType.OpenApi3));
        SchemaJson.Definition(document, "Quantity").Should().Be(
            """{"type":"integer","description":"A quantity of items, tested to cover the narrow integer promotion path.","format":"int32","maximum":1000.0,"minimum":0.0}""");
        SchemaJson.Property(document, "optionalIban").Should().Be("""{"x-nullable":true,"oneOf":[{"$ref":"#/definitions/Iban"}]}""");
        SchemaJson.Definition(document, "CountryCode").Should().NotContain("null", "a definition is never nullable: a reference says so");

        // Nor does a Swagger 2.0 document, whose type is a single one.
        var swagger = SchemaJson.Generate<SchemaOrder>(SchemaJson.SystemTextJson(web, SchemaType.Swagger2));
        SchemaJson.Property(swagger, "quantity").Should().StartWith("""{"type":"integer",""").And.NotContain("pattern");
        web.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public void Options_no_serializer_used_yet_are_described_as_they_stand_when_each_schema_is_generated()
    {
        var options = new SystemTextJsonOptions();
        SchemaJson.Definition(SchemaJson.Generate<SchemaHolder<Quantity>>(SchemaJson.SystemTextJson(options)), "Quantity").Should().Be(
            """{"type":"integer","description":"A quantity of items, tested to cover the narrow integer promotion path.","format":"int32","maximum":1000.0,"minimum":0.0}""");

        options.NumberHandling = JsonNumberHandling.WriteAsString;
        var asText = JsonSchemaGenerator.FromType<SchemaHolder<Quantity>>(SchemaJson.SystemTextJson(options));

        // A number written as text keeps its bounds for text too, in the extensions and the description.
        SchemaJson.Definition(SchemaJson.Parse(asText.ToJson()), "Quantity").Should().Be(
            """{"type":["integer","string"],"description":"A quantity of items, tested to cover the narrow integer promotion path.\n\nBetween 0 and 1000, inclusive.","format":"int32","maximum":1000.0,"minimum":0.0,"pattern":"^-?(?:0|[1-9]\\d*)$","x-minimum":"0","x-maximum":"1000"}""");
        var holder = new SchemaHolder<Quantity> { Value = Quantity.Create(7), Values = [Quantity.Create(8)] };
        var payload = SystemTextJsonSerializer.Serialize(holder, options);
        payload.Should().Contain("\"Value\":\"7\"");
        SchemaJson.Errors(asText, payload).Should().BeEmpty();

        // Options in use change no more: each kind of schema keeps its copy of them.
        options.MakeReadOnly(populateMissingResolver: true);
        foreach (var schemaType in (SchemaType[])[SchemaType.JsonSchema, SchemaType.OpenApi3])
        {
            var first = JsonSchemaGenerator.FromType<SchemaHolder<Quantity>>(SchemaJson.SystemTextJson(options, schemaType)).ToJson();
            JsonSchemaGenerator.FromType<SchemaHolder<Quantity>>(SchemaJson.SystemTextJson(options, schemaType)).ToJson().Should().Be(first);
        }

        var document = SchemaJson.Generate<SchemaHolder<Quantity>>(SchemaJson.SystemTextJson(options, SchemaType.OpenApi3));
        SchemaJson.Definition(document, "Quantity").Should().StartWith("""{"type":"integer",""").And.NotContain("x-minimum");
        SchemaJson.Errors(JsonSchemaGenerator.FromType<SchemaHolder<Quantity>>(SchemaJson.SystemTextJson(options)), payload).Should().BeEmpty();
    }

    [Fact]
    public void Under_named_literals_a_real_of_a_JSON_Schema_is_a_number_or_one_of_them()
    {
        var options = new SystemTextJsonOptions { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
        var settings = SchemaJson.SystemTextJson(options);
        var schema = JsonSchemaGenerator.FromType<SchemaHolder<Declared<Reading, double>>>(settings);

        SchemaJson.DefinitionOf(SchemaJson.Parse(schema.ToJson()), "Value").Should().Be(
            """{"anyOf":[{"type":"number"},{"enum":["NaN","Infinity","-Infinity"]}]}""");

        var holder = new SchemaHolder<Declared<Reading, double>> { Value = Declared<Reading, double>.Create(double.NaN), Values = [Declared<Reading, double>.Create(1.5)] };
        SchemaJson.Errors(schema, SystemTextJsonSerializer.Serialize(holder, options)).Should().BeEmpty();
    }

    [Fact]
    public void Settings_of_another_serializer_describe_a_value_object_as_its_generated_converter_writes_it()
    {
        var settings = new OtherSettings().AddValueObjects();

        SchemaJson.Compact(SchemaJson.Generate<Quantity>(settings)).Should().Be(
            """{"$schema":"http://json-schema.org/draft-04/schema#","title":"Quantity","type":"integer","description":"A quantity of items, tested to cover the narrow integer promotion path.","format":"int32","maximum":1000.0,"minimum":0.0}""");
        SchemaJson.Generate<Consent>(settings)["type"]!.ToString().Should().Be("boolean");
    }

    [Fact]
    public void A_value_object_as_the_root_is_its_own_schema()
    {
        var settings = SchemaJson.NewtonsoftWithConverter();

        SchemaJson.Compact(SchemaJson.Generate<Iban>(settings)).Should().Be(
            """{"$schema":"http://json-schema.org/draft-04/schema#","title":"Iban","type":"string","description":"An International Bank Account Number, stored in its electronic form.","format":"iban","maxLength":34,"minLength":15,"pattern":"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$","x-example":"FR7630006000011234567890189"}""");
        SchemaJson.Generate<Iban?>(settings)["pattern"]!.ToString().Should().Be("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");
    }

    /// <summary>
    /// A schema of another type than <see cref="JsonSchema"/>, which NJsonSchema makes refer to the definition and hands
    /// the processors with the value object's type, stays a reference: the definition holds the rules.
    /// </summary>
    [Fact]
    public void A_schema_referring_to_a_value_object_s_definition_is_left_a_reference()
    {
        var settings = SchemaJson.NewtonsoftWithConverter();
        var root = new JsonSchema();
        var property = new JsonSchemaProperty();

        new JsonSchemaGenerator(settings).Generate(property, typeof(Iban).ToContextualType(), new JsonSchemaResolver(root, settings));

        property.HasReference.Should().BeTrue();
        property.Type.Should().Be(JsonObjectType.None);
        property.Pattern.Should().BeNull();
        property.Reference!.Pattern.Should().Be("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");
    }

    [Fact]
    public void A_value_object_nothing_registered_yet_is_described_once_met()
    {
        ValueObjectRegistry.TryGet(typeof(Reference<UnheardOf>), out _).Should().BeFalse("nothing in the process met it before");

        var schema = SchemaJson.Generate<SchemaHolder<Reference<UnheardOf>>>(SchemaJson.NewtonsoftWithConverter());

        SchemaJson.Property(schema, "Optional").Should().Be("""{"oneOf":[{"type":"null"},{"$ref":"#/definitions/ReferenceOfUnheardOf"}]}""");
        SchemaJson.Definition(schema, "ReferenceOfUnheardOf").Should().EndWith(""","maxLength":12,"x-example":"PO-1042"}""");
    }

    [Fact]
    public void A_value_object_written_by_hand_is_described_where_its_serializer_writes_a_value()
    {
        var holder = new SchemaHolder<Declared<NoRule, int>> { Value = Declared<NoRule, int>.Create(3), Values = [Declared<NoRule, int>.Create(4)] };

        // Its System.Text.Json converter, which its type names, writes the value.
        var systemTextJson = JsonSchemaGenerator.FromType<SchemaHolder<Declared<NoRule, int>>>(SchemaJson.SystemTextJson());
        SchemaJson.DefinitionOf(SchemaJson.Parse(systemTextJson.ToJson()), "Value").Should().Be("""{"type":"integer"}""");
        SchemaJson.Errors(systemTextJson, SystemTextJsonSerializer.Serialize(holder)).Should().BeEmpty();

        // Newtonsoft.Json, with no type converter to read, writes its public properties: left as NJsonSchema describes it.
        var text = SchemaJson.NewtonsoftText();
        var properties = JsonSchemaGenerator.FromType<SchemaHolder<Declared<NoRule, int>>>(text);
        SchemaJson.DefinitionOf(SchemaJson.Parse(properties.ToJson()), "Value").Should().Contain("\"properties\":{\"Value\"");
        SchemaJson.Errors(properties, JsonConvert.SerializeObject(holder, text.SerializerSettings)).Should().BeEmpty();

        // ValueObjectConverter writes the value.
        var withConverter = SchemaJson.NewtonsoftWithConverter();
        var value = JsonSchemaGenerator.FromType<SchemaHolder<Declared<NoRule, int>>>(withConverter);
        SchemaJson.DefinitionOf(SchemaJson.Parse(value.ToJson()), "Value").Should().Be("""{"type":"integer"}""");
        SchemaJson.Errors(value, JsonConvert.SerializeObject(holder, withConverter.SerializerSettings)).Should().BeEmpty();
    }

    [Fact]
    public void A_value_object_written_by_hand_that_System_Text_Json_writes_as_an_object_is_left_alone_until_its_options_convert_it()
    {
        var objects = SchemaJson.Generate<SchemaHolder<HandWrittenTag<JsonSchemaGenerationTests>>>(SchemaJson.SystemTextJson());
        SchemaJson.DefinitionOf(objects, "Value").Should().Contain("\"properties\":{\"Value\"");

        var options = new SystemTextJsonOptions();
        options.AddValueObjects();
        var values = SchemaJson.Generate<SchemaHolder<HandWrittenTag<JsonSchemaGenerationTests>>>(SchemaJson.SystemTextJson(options));
        SchemaJson.DefinitionOf(values, "Value").Should().Be("""{"type":"string"}""");
    }

    [Fact]
    public void A_bound_no_decimal_holds_is_published_as_an_extension_and_in_the_description()
    {
        var schema = SchemaJson.Generate<SchemaHolder<Declared<FarBounds, double>>>(SchemaJson.NewtonsoftWithConverter());

        SchemaJson.DefinitionOf(schema, "Value").Should().Be(
            """{"type":"number","description":"A distance beyond what a decimal holds.\n\nBetween -1E+300 and 1E+300, inclusive.","x-example":1E+300,"x-minimum":-1E+300,"x-maximum":1E+300}""");
    }

    [Fact]
    public void A_bound_a_decimal_holds_is_a_bound_however_it_is_written()
    {
        // A double formats 0.00001 with an exponent, which a decimal reads too.
        var schema = SchemaJson.Generate<SchemaHolder<Declared<SmallBound, double>>>(SchemaJson.NewtonsoftWithConverter());

        SchemaJson.DefinitionOf(schema, "Value").Should().Be("""{"type":"number","maximum":1.0,"minimum":1E-05}""");
    }

    [Fact]
    public void A_closed_set_of_decimals_is_listed_as_declared_in_a_document_and_as_doubles_in_a_JSON_Schema()
    {
        var holder = new SchemaHolder<Declared<PreciseRates, decimal>> { Value = Declared<PreciseRates, decimal>.Create(0.3333333333333333333333333333m) };

        // A document, which no validator of NJsonSchema's reads, lists each value as the type writes it.
        foreach (var settings in (JsonSchemaGeneratorSettings[])[SchemaJson.SystemTextJson(schemaType: SchemaType.OpenApi3), Document(SchemaJson.NewtonsoftWithConverter())])
        {
            Listed(JsonSchemaGenerator.FromType<SchemaHolder<Declared<PreciseRates, decimal>>>(settings)).Select(static value => value.Value<decimal>())
                .Should().Equal(1m, 1.00000000000000001m, 0.3333333333333333333333333333m);
        }

        // NJsonSchema's validator, which Confluent's serializer runs, reads a payload's real as a double and compares it
        // with each value by its text: a JSON Schema lists the doubles, rounded where no double holds the decimal.
        var jsonSchema = JsonSchemaGenerator.FromType<SchemaHolder<Declared<PreciseRates, decimal>>>(SchemaJson.SystemTextJson());
        Listed(jsonSchema).Select(static value => value.Value<decimal>()).Should().Equal(1m, 1m, 0.3333333333333333m);
        SchemaJson.Errors(jsonSchema, SystemTextJsonSerializer.Serialize(holder)).Should().BeEmpty();

        static NewtonsoftJsonSchemaGeneratorSettings Document(NewtonsoftJsonSchemaGeneratorSettings settings)
        {
            settings.SchemaType = SchemaType.OpenApi3;
            return settings;
        }

        // Read with every digit a decimal holds, more than a double does.
        static JToken Listed(JsonSchema schema)
        {
            using var reader = new JsonTextReader(new StringReader(schema.ToJson())) { FloatParseHandling = FloatParseHandling.Decimal };

            return JObject.Load(reader)["definitions"]!["DeclaredOfPreciseRatesAndDecimal"]!["enum"]!;
        }
    }

    [Fact]
    public void A_value_object_naming_the_converter_on_its_type_is_described_as_it_writes_it_whatever_the_settings_hold()
    {
        var settings = SchemaJson.NewtonsoftText();
        var schema = JsonSchemaGenerator.FromType<SchemaHolder<ConvertedCount>>(settings);
        var payload = JsonConvert.SerializeObject(new SchemaHolder<ConvertedCount> { Value = ConvertedCount.Create(3), Values = [ConvertedCount.Create(4)] }, settings.SerializerSettings);

        SchemaJson.DefinitionOf(SchemaJson.Parse(schema.ToJson()), "Value").Should().StartWith("""{"type":"integer",""").And.EndWith(""","format":"int32"}""");
        payload.Should().Contain("\"Value\":3");
        SchemaJson.Errors(schema, payload).Should().BeEmpty();
    }

    [Fact]
    public void A_value_Newtonsoft_Json_refuses_to_write_is_published_as_declared()
    {
        // ValueObjectConverter refuses to write a value its type refuses, as the example 0 is: the example is then its
        // text, and a bound that is no number is left out of the number's.
        var schema = SchemaJson.Generate<SchemaHolder<Declared<RefusedMinimum, int>>>(SchemaJson.NewtonsoftWithConverter());

        SchemaJson.DefinitionOf(schema, "Value").Should().Be("""{"type":"integer","minimum":0.0,"x-example":"0"}""");
    }

    [Fact]
    public void A_value_object_another_converter_of_the_settings_writes_is_left_as_NJsonSchema_describes_it()
    {
        var settings = new NewtonsoftJsonSchemaGeneratorSettings();
        settings.SerializerSettings.Converters.Add(new DeclaredTextConverter());
        settings.SerializerSettings.AddValueObjects();

        var schema = SchemaJson.Generate<SchemaHolder<Declared<NoRule, int>>>(settings.AddValueObjects());

        SchemaJson.DefinitionOf(schema, "Value").Should().Contain("\"properties\":{\"Value\"");
    }

    [Fact]
    public void Options_whose_resolver_cannot_answer_take_a_value_object_at_its_word()
    {
        var options = new SystemTextJsonOptions { TypeInfoResolver = new RefusingResolver() };
        var schema = SchemaJson.Generate<Iban>(SchemaJson.SystemTextJson(options));

        schema["pattern"]!.ToString().Should().Be("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");
        schema["x-example"]!.ToString().Should().Be("FR7630006000011234567890189", "the example is written as declared");
    }

    [Fact]
    public void Under_Swagger_2_a_value_object_written_as_text_is_described_in_place_as_its_text()
    {
        var settings = SchemaJson.NewtonsoftText();
        settings.SchemaType = SchemaType.Swagger2;

        var schema = SchemaJson.Generate<SchemaOrder>(settings);

        SchemaJson.Property(schema, "Quantity").Should().Contain("\"type\":\"string\"").And.Contain("\"pattern\":\"^-?(?:0|[1-9]\\\\d*)$\"");
        SchemaJson.Property(schema, "Consent").Should().Contain("\"type\":\"string\"").And.Contain("\"x-example\":\"True\"");
    }

    [Fact]
    public void A_closed_set_whose_details_do_not_list_its_values_names_none()
    {
        var schema = SchemaJson.Generate<SchemaHolder<Declared<UnnamedSet, int>>>(SchemaJson.SystemTextJson());

        SchemaJson.DefinitionOf(schema, "Value").Should().Be("""{"type":"integer","enum":[1,2]}""");
    }

    /// <summary>As NJsonSchema describes an enumeration: a set none of whose values is described has no descriptions.</summary>
    [Fact]
    public void A_closed_set_whose_values_have_names_and_no_description_lists_no_description()
    {
        var schema = SchemaJson.Generate<SchemaHolder<Declared<NamedSet, int>>>(SchemaJson.SystemTextJson());
        var definition = SchemaJson.DefinitionOf(schema, "Value");

        definition.Should().StartWith("""{"type":"integer","x-enumNames":["One","Two"],"enum":[1,2],"x-enum-varnames":["One","Two"],"x-ms-enum":""");
        definition.Should().EndWith("""
            "modelAsString":false,"values":[{"value":1,"name":"One"},{"value":2,"name":"Two"}]}}
            """);
        definition.Should().NotContain("x-enum-descriptions");
    }

    [Fact]
    public void Options_whose_resolver_knows_nothing_of_a_value_object_take_it_at_its_word()
    {
        var options = new SystemTextJsonOptions { TypeInfoResolver = new IgnorantResolver() };
        var schema = SchemaJson.Generate<Quantity>(SchemaJson.SystemTextJson(options));

        schema["type"]!.ToString().Should().Be("integer");
        schema["maximum"]!.Value<decimal>().Should().Be(1000m);

        // In a document too, which reads a copy of the options holding their resolver, though numbers be read strictly
        // there: a value object written by hand, which the default resolver writes as an object, is taken at its word.
        var document = SchemaJson.Generate<SchemaHolder<HandWrittenTag<JsonSchemaGenerationTests>>>(SchemaJson.SystemTextJson(options, SchemaType.OpenApi3));
        SchemaJson.DefinitionOf(document, "Value").Should().Be("""{"type":"string"}""");
    }

    [Fact]
    public void The_writer_of_Newtonsoft_Json_values_closes_a_converter_for_a_value_object_alone()
    {
        var factory = new NewtonsoftJsonWrittenConverterFactory(new JsonSerializerSettings());

        factory.CanConvert(typeof(string)).Should().BeFalse();
        factory.CreateConverter(typeof(string), new SystemTextJsonOptions()).Should().BeNull();
        factory.CanConvert(typeof(Iban?)).Should().BeTrue();
        factory.CreateConverter(typeof(Iban), new SystemTextJsonOptions()).Should().BeOfType<NewtonsoftJsonWrittenConverter<Iban>>();
    }

    [Fact]
    public void Under_Swagger_2_every_value_object_is_described_in_place()
    {
        var schema = SchemaJson.Generate<SchemaOrder>(SchemaJson.SystemTextJson(schemaType: SchemaType.Swagger2));

        schema["definitions"].Should().BeNull("no value object has a definition of its own");
        SchemaJson.Property(schema, "Iban").Should().Contain("\"type\":\"string\"").And.Contain("\"pattern\":\"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$\"");
        SchemaJson.Property(schema, "Quantity").Should().Contain("\"type\":\"integer\"").And.Contain("\"maximum\":1000.0");
        SchemaJson.Property(schema, "OptionalCountry").Should().Contain("\"enum\":[\"FR\",\"BE\",\"LU\"]").And.NotContain("null,");

        // Settings saying null with x-nullable say it beside a nullable value object's type, never among its values.
        var nullable = SchemaJson.SystemTextJson(schemaType: SchemaType.Swagger2);
        nullable.GenerateCustomNullableProperties = true;
        var custom = SchemaJson.Generate<SchemaOrder>(nullable);
        SchemaJson.Property(custom, "OptionalCountry").Should().Contain("\"x-nullable\":true").And.Contain("\"enum\":[\"FR\",\"BE\",\"LU\"]");
        SchemaJson.Property(custom, "Country").Should().NotContain("x-nullable");
    }

    [Fact]
    public void A_member_annotated_for_validation_keeps_the_reference_and_NJsonSchema_s_own_attribute_wins()
    {
        var newtonsoft = SchemaJson.Generate<SchemaAnnotated>(SchemaJson.NewtonsoftWithConverter());
        var systemTextJson = SchemaJson.Generate<SchemaAnnotated>(SchemaJson.SystemTextJson());

        // NJsonSchema applies [Required] as it would to any string contract, beside the reference, under Newtonsoft.Json.
        SchemaJson.Property(newtonsoft, "Iban").Should().Be("""{"minLength":1,"oneOf":[{"$ref":"#/definitions/Iban"}]}""");
        SchemaJson.Property(systemTextJson, "Iban").Should().Be("""{"$ref":"#/definitions/Iban"}""");

        // [Range] beside a reference is applied by neither.
        SchemaJson.Property(newtonsoft, "Ranged").Should().Be("""{"$ref":"#/definitions/Quantity"}""");
        SchemaJson.Property(systemTextJson, "Ranged").Should().Be("""{"$ref":"#/definitions/Quantity"}""");

        // [JsonSchemaType] and [JsonSchema] are the application's own description.
        SchemaJson.Property(newtonsoft, "Forced").Should().Be("""{"type":"string"}""");
        SchemaJson.Property(systemTextJson, "Forced").Should().Be("""{"type":"string"}""");
        SchemaJson.Property(newtonsoft, "Stated").Should().Be("""{"type":"integer"}""");
        SchemaJson.Property(systemTextJson, "Stated").Should().Be("""{"type":"integer"}""");
    }

    [Fact]
    public void A_type_mapper_of_the_application_keeps_its_description_of_each_form_NJsonSchema_hands_it()
    {
        var settings = new NewtonsoftJsonSchemaGeneratorSettings();
        settings.SerializerSettings.AddValueObjects();
        settings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(CountryCode), MappedByTheApplication));
        settings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(Reference<>), static schema => schema.Type = JsonObjectType.String));
        settings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(Quantity?), static schema => schema.Type = JsonObjectType.Integer));
        settings.AddValueObjects();

        var generated = JsonSchemaGenerator.FromType<SchemaOrder>(settings);
        var schema = SchemaJson.Parse(generated.ToJson());

        // A mapper of the value object, or of its generic definition, maps its nullable form too, which NJsonSchema meets
        // as the value object.
        SchemaJson.Property(schema, "Country").Should().Be("""{"type":"string","description":"Mapped by the application."}""");
        SchemaJson.Property(schema, "OptionalCountry").Should().Be("""{"type":["null","string"],"description":"Mapped by the application."}""");
        SchemaJson.Property(schema, "Order").Should().Be("""{"type":"string"}""");
        SchemaJson.Property(schema, "OptionalOrder").Should().Be("""{"type":["null","string"]}""");

        // A mapper of the nullable type maps that form alone, which Newtonsoft.Json's service keeps as such: the value
        // object itself stays described, as the converter writes it.
        SchemaJson.Property(schema, "OptionalQuantity").Should().Be("""{"type":["integer","null"]}""");
        SchemaJson.Property(schema, "Quantity").Should().Be("""{"$ref":"#/definitions/Quantity"}""");
        SchemaJson.Definition(schema, "Quantity").Should().StartWith("""{"type":"integer",""");
        ((JObject)schema["definitions"]!).Properties().Select(static definition => definition.Name).Should()
            .Contain(["Iban", "Quantity"]).And.NotContain(["CountryCode", "ReferenceOfPurchaseOrder"]);
        SchemaJson.Errors(generated, JsonConvert.SerializeObject(SchemaOrder.Sample(), settings.SerializerSettings)).Should().BeEmpty();

        // System.Text.Json's service unwraps a nullable type before NJsonSchema looks a mapper up: a mapper of Quantity?
        // maps nothing there, and both forms are described.
        var systemTextJson = new SystemTextJsonSchemaGeneratorSettings { SerializerOptions = new SystemTextJsonOptions() };
        systemTextJson.TypeMappers.Add(new PrimitiveTypeMapper(typeof(Quantity?), static schema => schema.Type = JsonObjectType.Integer));
        var unwrapped = JsonSchemaGenerator.FromType<SchemaOrder>(systemTextJson.AddValueObjects());
        var described = SchemaJson.Parse(unwrapped.ToJson());
        SchemaJson.Property(described, "Quantity").Should().Be("""{"$ref":"#/definitions/Quantity"}""");
        SchemaJson.Property(described, "OptionalQuantity").Should().Be("""{"oneOf":[{"type":"null"},{"$ref":"#/definitions/Quantity"}]}""");
        SchemaJson.Errors(unwrapped, SystemTextJsonSerializer.Serialize(SchemaOrder.Sample())).Should().BeEmpty();

        // In place, under Swagger 2.0, a mapper of the value object maps its nullable form too.
        var swagger = new NewtonsoftJsonSchemaGeneratorSettings { SchemaType = SchemaType.Swagger2 };
        swagger.TypeMappers.Add(new PrimitiveTypeMapper(typeof(CountryCode), MappedByTheApplication));
        var inPlace = SchemaJson.Generate<SchemaOrder>(swagger.AddValueObjects());
        SchemaJson.Property(inPlace, "OptionalCountry").Should().Be("""{"type":"string","description":"Mapped by the application."}""");
        SchemaJson.Property(inPlace, "Quantity").Should().StartWith("""{"type":"string",""");

        // Newtonsoft.Json's service unwraps the nullable type of a value object it writes with no type converter: a mapper
        // of that type maps nothing, and the value object stays described in place.
        var written = SchemaJson.NewtonsoftWithConverter();
        written.SchemaType = SchemaType.Swagger2;
        written.TypeMappers.Add(new PrimitiveTypeMapper(typeof(Declared<NoRule, int>?), static schema => schema.Type = JsonObjectType.String));
        var holder = SchemaJson.Generate<SchemaHolder<Declared<NoRule, int>>>(written);
        holder["definitions"].Should().BeNull();
        SchemaJson.Property(holder, "Optional").Should().Be("""{"type":"integer"}""");

        static void MappedByTheApplication(JsonSchema schema)
        {
            schema.Type = JsonObjectType.String;
            schema.Description = "Mapped by the application.";
        }
    }

    [Fact]
    public void Flattened_hierarchies_and_abstract_properties_add_no_member_to_a_value_object()
    {
        var settings = new NewtonsoftJsonSchemaGeneratorSettings { FlattenInheritanceHierarchy = true, GenerateAbstractProperties = true };
        settings.SerializerSettings.AddValueObjects();
        var schema = SchemaJson.Generate<SchemaOrder>(settings.AddValueObjects());

        SchemaJson.Definition(schema, "Quantity").Should().Be(
            """{"type":"integer","description":"A quantity of items, tested to cover the narrow integer promotion path.","format":"int32","maximum":1000.0,"minimum":0.0}""");
        SchemaJson.Definition(schema, "Iban").Should().NotContain("properties");
    }

    [Fact]
    public void Called_twice_it_adds_one_reflection_service_and_one_processor_and_describes_the_same()
    {
        var once = SchemaJson.NewtonsoftText();
        var twice = SchemaJson.NewtonsoftText().AddValueObjects();

        twice.SchemaProcessors.Should().ContainSingle();
        var service = twice.ReflectionService.Should().BeOfType<ValueObjectReflectionService>().Subject;
        service.Inner.Should().BeOfType<NewtonsoftJsonReflectionService>();
        JsonSchemaGenerator.FromType<SchemaOrder>(twice).ToJson().Should().Be(JsonSchemaGenerator.FromType<SchemaOrder>(once).ToJson());

        var act = static () => ((NewtonsoftJsonSchemaGeneratorSettings)null!).AddValueObjects();
        act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }

    [Fact]
    public void The_reflection_service_hands_every_other_question_to_the_inner_one()
    {
        var inner = new SystemTextJsonReflectionService();
        var service = new ValueObjectReflectionService(inner);
        var settings = new SystemTextJsonSchemaGeneratorSettings();
        var text = typeof(SchemaTextRecord).ToContextualType();

        service.IsNullable(typeof(string).ToContextualType(), ReferenceTypeNullHandling.Null).Should().BeTrue();
        service.IsStringEnum(typeof(DayOfWeek).ToContextualType(), settings).Should().Be(inner.IsStringEnum(typeof(DayOfWeek).ToContextualType(), settings));
        service.GetEnumValueConverter(settings)(DayOfWeek.Monday).Should().Be(inner.GetEnumValueConverter(settings)(DayOfWeek.Monday));
        service.GetPropertyName(text.Properties.Single(static property => property.Name == "Account"), settings).Should().Be("Account");
        service.GetDescription(typeof(int).ToContextualType(), settings).Type.Should().Be(JsonObjectType.Integer);
        service.GetDescription(typeof(Iban).ToContextualType(), settings).Type.Should().Be(JsonObjectType.Object);
    }

    [Fact]
    public void NJsonSchema_s_validator_throws_on_an_integer_beyond_a_long_whatever_the_schema_says()
    {
        // Pinned: the validator reads a JSON integer through decimal, and casts the BigInteger Newtonsoft.Json holds one
        // beyond Int64 in, catching an overflow only. Confluent's serializer runs it, with or without this package.
        var settings = SchemaJson.NewtonsoftWithConverter();
        var schema = JsonSchemaGenerator.FromType<SchemaHolder<ByteCount>>(settings);
        var payload = JsonConvert.SerializeObject(new SchemaHolder<ByteCount> { Value = ByteCount.Create(ulong.MaxValue) }, settings.SerializerSettings);

        payload.Should().Contain(ulong.MaxValue.ToString(CultureInfo.InvariantCulture));
        var act = () => SchemaJson.Errors(schema, payload);
        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void The_writer_of_Newtonsoft_Json_values_writes_and_never_reads()
    {
        var converter = new NewtonsoftJsonWrittenConverter<Quantity>(new JsonSerializerSettings());

        var act = () =>
        {
            var reader = new System.Text.Json.Utf8JsonReader("7"u8);
            converter.Read(ref reader, typeof(Quantity), new SystemTextJsonOptions());
        };

        act.Should().Throw<NotSupportedException>();
    }

    /// <summary>A count naming Newtonsoft.Json's converter on its type, which settings without it then apply.</summary>
    [ValueObject<int>]
    [Newtonsoft.Json.JsonConverter(typeof(ValueObjectConverter))]
    public readonly partial struct ConvertedCount;

    /// <summary>The owner of a construction nothing in the process meets but one test.</summary>
    private sealed class UnheardOf;

    /// <summary>A real with no bound, which may be one of the named literals.</summary>
    private sealed class Reading : IDeclaration<double>
    {
        public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;
    }

    /// <summary>An integer with no rule, written by hand: a System.Text.Json converter, and no type converter.</summary>
    private sealed class NoRule : IDeclaration<int>
    {
        public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;
    }

    /// <summary>A real bounded far beyond what a decimal holds, with an example as far.</summary>
    private sealed class FarBounds : IDeclaration<double>
    {
        public static ValueObjectSchema Schema { get; } = new()
        {
            Minimum = "-1E+300",
            Maximum = "1E+300",
            Example = 1e300,
            Description = "A distance beyond what a decimal holds.",
        };
    }

    /// <summary>A real bounded below by a decimal a double formats with an exponent.</summary>
    private sealed class SmallBound : IDeclaration<double>
    {
        public static ValueObjectSchema Schema { get; } = new() { Minimum = "0.00001", Maximum = "1" };
    }

    /// <summary>A closed set of decimals no double holds, two of which one double merges.</summary>
    private sealed class PreciseRates : IDeclaration<decimal>
    {
        public static ValueObjectSchema Schema { get; } = new()
        {
            IsClosedValueSet = true,
            KnownValues = [1m, 1.00000000000000001m, 0.3333333333333333333333333333m],
        };
    }

    /// <summary>An integer whose schema declares an example its rule refuses, and a maximum that is no number.</summary>
    private sealed class RefusedMinimum : IDeclaration<int>
    {
        public static ValueObjectSchema Schema { get; } = new() { Minimum = "0", Maximum = "low", Example = 0 };

        public static ValidationResult Validate(int value) => value > 0 ? ValidationResult.Success : ValidationResult.OutOfRange("Positive only.");
    }

    /// <summary>A closed set of integers whose schema, written by hand, names none of its values.</summary>
    private sealed class NamedSet : IDeclaration<int>
    {
        public static ValueObjectSchema Schema { get; } = new() { IsClosedValueSet = true, KnownValues = [1, 2], KnownValueDetails = [new(1, "One"), new(2, "Two")] };

        public static ValidationResult Validate(int value) => value is 1 or 2 ? ValidationResult.Success : ValidationResult.Failure(ValueObjectErrorCodes.NotAKnownValue, "One or two.");
    }

    private sealed class UnnamedSet : IDeclaration<int>
    {
        public static ValueObjectSchema Schema { get; } = new() { IsClosedValueSet = true, KnownValues = [1, 2] };

        public static ValidationResult Validate(int value) => value is 1 or 2 ? ValidationResult.Success : ValidationResult.Failure(ValueObjectErrorCodes.NotAKnownValue, "One or two.");
    }

    /// <summary>A resolver that knows no type, as a context generated for other types.</summary>
    private sealed class IgnorantResolver : System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver
    {
        public System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(Type type, SystemTextJsonOptions options) => null;
    }

    /// <summary>A resolver that answers for no type, as one generated for other types may throw.</summary>
    private sealed class RefusingResolver : System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver
    {
        public System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(Type type, SystemTextJsonOptions options)
            => throw new NotSupportedException($"No contract for {type.Name}.");
    }

    /// <summary>Writes a value object written by hand as the text of its value, a converter of the application's own.</summary>
    private sealed class DeclaredTextConverter : Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(Declared<NoRule, int>);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, Newtonsoft.Json.JsonSerializer serializer)
            => throw new NotSupportedException();

        public override void WriteJson(JsonWriter writer, object? value, Newtonsoft.Json.JsonSerializer serializer)
            => writer.WriteValue(((Declared<NoRule, int>)value!).Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Settings of a serializer neither System.Text.Json nor Newtonsoft.Json.</summary>
    private sealed class OtherSettings() : JsonSchemaGeneratorSettings(new OtherReflectionService());

    /// <summary>The reflection service of <see cref="OtherSettings"/>, which describes no member.</summary>
    private sealed class OtherReflectionService : ReflectionServiceBase<OtherSettings>
    {
        public override Func<object, string?> GetEnumValueConverter(OtherSettings settings) => static value => value.ToString();

        public override void GenerateProperties(JsonSchema schema, ContextualType contextualType, OtherSettings settings, JsonSchemaGenerator schemaGenerator, JsonSchemaResolver schemaResolver)
        {
        }

        public override string GetPropertyName(ContextualAccessorInfo accessorInfo, JsonSchemaGeneratorSettings settings) => accessorInfo.Name;
    }
}
