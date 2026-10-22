using AdCodicem.ValueObjects.NJsonSchema;
using Namotion.Reflection;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;

namespace AdCodicem.ValueObjects.UnitTests.SchemaGeneration;

/// <summary>
/// Text a member holds to a value object's rules under <c>[ParsesAs]</c>, described in place as that value object's
/// text in both of NJsonSchema's generators, as the OpenAPI packages describe it.
/// </summary>
public sealed class ParsesAsSchemaTests
{
    private const string IbanText =
        "\"description\":\"An International Bank Account Number, stored in its electronic form.\",\"format\":\"iban\",\"maxLength\":34,\"minLength\":15,\"pattern\":\"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$\"";

    public static TheoryData<string> JsonSchemaWires => ["newtonsoft", "system-text-json"];

    public static TheoryData<string> Wires => ["newtonsoft", "system-text-json", "openapi3"];

    [Theory]
    [MemberData(nameof(JsonSchemaWires))]
    public void Text_is_described_as_its_value_object_s_text_in_a_JSON_Schema(string wire)
    {
        var schema = SchemaJson.Generate<SchemaTextForm>(Settings(wire));

        SchemaJson.Property(schema, "Account").Should().Be($$$"""{"type":["null","string"],{{{IbanText}}},"x-example":"FR7630006000011234567890189"}""");
        SchemaJson.Property(schema, "Quantity").Should().Be(
            """{"type":["null","string"],"description":"A quantity of items, tested to cover the narrow integer promotion path.\n\nBetween 0 and 1000, inclusive.","format":"int32","pattern":"^-?(?:0|[1-9]\\d*)$","x-minimum":"0","x-maximum":"1000"}""");
        SchemaJson.Property(schema, "Consent").Should().Be(
            """{"type":["null","string"],"description":"Whether a customer agreed to be contacted.","pattern":"^(?:[Tt]rue|[Ff]alse)$","x-example":"True"}""");
        SchemaJson.Property(schema, "Born").Should().Contain("\"format\":\"date\",\"x-minimum\":\"1900-01-01\",\"x-maximum\":\"2100-12-31\"");
        SchemaJson.Property(schema, "Accounts").Should().Be($$$"""{"type":"array","items":{"type":"string",{{{IbanText}}},"x-example":"FR7630006000011234567890189"}}""");

        // null joins the values of a closed set its member may leave out, and only then.
        SchemaJson.Property(schema, "Country").Should().Contain("\"type\":[\"null\",\"string\"]").And.Contain("\"enum\":[\"FR\",\"BE\",\"LU\",null]");
        SchemaJson.Property(schema, "RequiredCountry").Should().StartWith("{\"type\":\"string\"").And.Contain("\"enum\":[\"FR\",\"BE\",\"LU\"]");

        // A value object without a description states its bounds once, though its member is described twice.
        SchemaJson.Property(schema, "Count").Should().Be(
            """{"type":["null","string"],"description":"Between 1 and 5, inclusive.","pattern":"^-?(?:0|[1-9]\\d*)$","x-minimum":"1","x-maximum":"5"}""");
    }

    [Theory]
    [MemberData(nameof(JsonSchemaWires))]
    public void Under_Swagger_2_a_nullable_text_says_null_beside_its_type_and_never_among_its_values(string wire)
    {
        var settings = Settings(wire);
        settings.SchemaType = SchemaType.Swagger2;
        settings.GenerateCustomNullableProperties = true;

        var schema = SchemaJson.Generate<SchemaTextForm>(settings);

        SchemaJson.Property(schema, "Country").Should().Contain("\"type\":\"string\"").And.Contain("\"x-nullable\":true").And.Contain("\"enum\":[\"FR\",\"BE\",\"LU\"]");
        SchemaJson.Property(schema, "RequiredCountry").Should().NotContain("x-nullable").And.Contain("\"enum\":[\"FR\",\"BE\",\"LU\"]");
    }

    [Fact]
    public void In_a_document_a_nullable_text_keeps_one_type_and_says_null_beside_it()
    {
        var schema = SchemaJson.Generate<SchemaTextForm>(Settings("openapi3"));

        SchemaJson.Property(schema, "account").Should().Be($$$"""{"type":"string",{{{IbanText}}},"x-nullable":true,"x-example":"FR7630006000011234567890189"}""");
        SchemaJson.Property(schema, "country").Should().Contain("\"type\":\"string\"").And.Contain("\"x-nullable\":true").And.Contain("\"enum\":[\"FR\",\"BE\",\"LU\",null]");
        SchemaJson.Property(schema, "requiredCountry").Should().NotContain("null");
    }

    [Theory]
    [MemberData(nameof(Wires))]
    public void A_member_keeps_the_stricter_of_its_own_rules_and_the_value_object_s(string wire)
    {
        var schema = SchemaJson.Generate<SchemaTextForm>(Settings(wire));
        var name = wire == "openapi3" ? "shortAccount" : "ShortAccount";
        var looser = wire == "openapi3" ? "looseAccount" : "LooseAccount";

        var shorter = schema["properties"]![name]!;
        shorter["maxLength"]!.Value<int>().Should().Be(20, "[StringLength(20)] is stricter than 34");
        shorter["minLength"]!.Value<int>().Should().Be(15, "the attribute's 0 would loosen the value object's 15");
        shorter["pattern"]!.ToString().Should().Be("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");

        var loose = schema["properties"]![looser]!;
        loose["maxLength"]!.Value<int>().Should().Be(34, "[StringLength(40)] is looser than 34");
        loose["minLength"]!.Value<int>().Should().Be(15);

        var narrow = schema["properties"]![wire == "openapi3" ? "narrowAccount" : "NarrowAccount"]!;
        narrow["minLength"]!.Value<int>().Should().Be(20, "a minimum of 20 is stricter than 15");
        narrow["maxLength"]!.Value<int>().Should().Be(30);

        // A pattern of the member's own is kept where the value object declares none.
        var order = schema["properties"]![wire == "openapi3" ? "order" : "Order"]!;
        order["pattern"]!.ToString().Should().Be("^PO-[0-9]+$");
        order["maxLength"]!.Value<int>().Should().Be(12);
    }

    [Theory]
    [MemberData(nameof(Wires))]
    public void A_derived_form_s_own_members_keep_the_stricter_rules_beside_its_base(string wire)
    {
        // NJsonSchema's inheritance, which NSwag keeps, puts the derived form's own members in a part of allOf beside a
        // reference to its base: no processor is handed that part, which the form's processing describes again.
        var schema = JsonSchemaGenerator.FromType<SchemaDerivedTextForm>(Settings(wire));
        var json = SchemaJson.Parse(schema.ToJson());
        string Named(string name) => wire == "openapi3" ? char.ToLowerInvariant(name[0]) + name[1..] : name;

        json["allOf"]![0]!["$ref"].Should().NotBeNull();
        var own = json["allOf"]![1]!["properties"]!;
        own[Named("Account")]!["maxLength"]!.Value<int>().Should().Be(20, "[StringLength(20)] is stricter than 34");
        own[Named("Account")]!["minLength"]!.Value<int>().Should().Be(15, "the attribute's 0 would loosen the value object's 15");
        own[Named("Order")]!["maxLength"]!.Value<int>().Should().Be(12, "[MaxLength(40)] is looser than 12");

        // NJsonSchema closes every object, so each part of allOf refuses the members of the other: the derived form's
        // part is validated alone.
        var part = schema.AllOf.Last();
        SchemaJson.Errors(part, new JObject { [Named("Account")] = "NO9386011117947", [Named("Order")] = "PO-1042" }.ToString()).Should().BeEmpty();
        SchemaJson.Errors(part, new JObject { [Named("Order")] = "PO-1042-1042-1042" }.ToString()).Should().ContainSingle().Which.Should().EndWith($" #/{Named("Order")}");
    }

    [Theory]
    [MemberData(nameof(Wires))]
    public void A_record_s_constructor_parameter_holds_its_property_to_the_rules(string wire)
    {
        var schema = SchemaJson.Generate<SchemaTextRecord>(Settings(wire));
        var account = wire == "openapi3" ? "account" : "Account";
        var country = wire == "openapi3" ? "country" : "Country";

        SchemaJson.Property(schema, account).Should().Be($$$"""{"type":"string",{{{IbanText}}},"x-example":"FR7630006000011234567890189"}""");
        SchemaJson.Property(schema, country).Should().Contain("\"enum\":[\"FR\",\"BE\",\"LU\",null]");

        // Described again once the record is complete, a property's own attribute applied: the stricter rules kept.
        var shortAccount = schema["properties"]![wire == "openapi3" ? "shortAccount" : "ShortAccount"]!;
        shortAccount["maxLength"]!.Value<int>().Should().Be(20);
        shortAccount["minLength"]!.Value<int>().Should().Be(15);
    }

    [Theory]
    [MemberData(nameof(JsonSchemaWires))]
    public void A_member_a_derived_form_hides_is_read_as_the_derived_form_declares_it(string wire)
    {
        var flattened = Settings(wire);
        flattened.FlattenInheritanceHierarchy = true;
        var flat = SchemaJson.Generate<SchemaHidingText>(flattened);

        SchemaJson.Property(flat, "Page").Should().Be("""{"type":["array","null"],"items":{"type":"string"}}""");
        SchemaJson.Property(flat, "Country").Should().Contain("\"enum\":[\"FR\",\"BE\",\"LU\",null]");

        var layered = SchemaJson.Generate<SchemaHidingText>(Settings(wire));
        SchemaJson.Compact(layered["allOf"]![1]!["properties"]!["Page"]!).Should().Be("""{"type":["array","null"],"items":{"type":"string"}}""");
        SchemaJson.Compact(layered["definitions"]!["SchemaTextBase"]!["properties"]!["Page"]!).Should().Contain("\"x-maximum\":\"1000\"");
    }

    [Theory]
    [MemberData(nameof(Wires))]
    public void A_misuse_is_described_as_the_member_it_is(string wire)
    {
        var schema = SchemaJson.Generate<SchemaTextForm>(Settings(wire));
        var number = wire == "openapi3" ? "number" : "Number";
        var noValueObject = wire == "openapi3" ? "noValueObject" : "NoValueObject";

        SchemaJson.Property(schema, number).Should().Be("""{"type":"integer","format":"int32"}""");
        SchemaJson.Property(schema, noValueObject).Should().NotContain("pattern").And.NotContain("format");
    }

    [Fact]
    public void A_field_Newtonsoft_Json_writes_is_held_to_the_rules_too()
    {
        var schema = SchemaJson.Generate<SchemaTextForm>(Settings("newtonsoft"));

        SchemaJson.Property(schema, "Written").Should().Contain("\"pattern\":\"^-?(?:0|[1-9]\\\\d*)$\"");

        // Described again once the form is complete, its own attribute applied: the stricter rules kept.
        var account = schema["properties"]!["WrittenAccount"]!;
        account["maxLength"]!.Value<int>().Should().Be(20);
        account["minLength"]!.Value<int>().Should().Be(15);
    }

    [Theory]
    [MemberData(nameof(Wires))]
    public void A_form_the_rules_accept_validates_and_one_they_refuse_does_not(string wire)
    {
        var settings = Settings(wire);
        var schema = JsonSchemaGenerator.FromType<SchemaTextForm>(settings);
        string Named(string name) => wire == "openapi3" ? char.ToLowerInvariant(name[0]) + name[1..] : name;

        var good = new JObject
        {
            [Named("Account")] = "DE89370400440532013000",
            [Named("Quantity")] = "7",
            [Named("Country")] = "FR",
            [Named("RequiredCountry")] = "BE",
            [Named("Accounts")] = new JArray("DE89370400440532013000"),
            [Named("Born")] = "1990-05-17",
            [Named("Consent")] = "True",
        };
        SchemaJson.Errors(schema, good.ToString()).Should().BeEmpty();

        var bad = (JObject)good.DeepClone();
        bad[Named("Quantity")] = "seven";
        bad[Named("Country")] = "ZZ";
        bad[Named("Accounts")] = new JArray("XX");
        SchemaJson.Errors(schema, bad.ToString()).Should().Contain($"NotInEnumeration #/{Named("Country")}").And.Contain($"ArrayItemNotValid #/{Named("Accounts")}[0]");
    }

    [Fact]
    public void An_array_of_no_known_item_and_a_member_that_is_no_text_are_left_alone()
    {
        var settings = new SystemTextJsonSchemaGeneratorSettings().AddValueObjects();
        var holder = new JsonSchema();
        var array = new JsonSchema { Type = JsonObjectType.Array };
        holder.Properties["Accounts"] = new JsonSchemaProperty { Type = JsonObjectType.Array };
        holder.Properties["Number"] = new JsonSchemaProperty { Type = JsonObjectType.Integer };
        var form = typeof(SchemaTextForm).ToContextualType();

        ValueObjectSchemaProcessor.Instance.Process(new SchemaProcessorContext(form, holder, new JsonSchemaResolver(holder, settings), new JsonSchemaGenerator(settings), settings));
        var accounts = form.Properties.Single(static property => property.Name == nameof(SchemaTextForm.Accounts)).AccessorType;
        ValueObjectSchemaProcessor.Instance.Process(new SchemaProcessorContext(accounts, array, new JsonSchemaResolver(array, settings), new JsonSchemaGenerator(settings), settings));

        holder.Properties["Accounts"].Item.Should().BeNull();
        holder.Properties["Number"].Pattern.Should().BeNull();
        array.Item.Should().BeNull();
    }

    private static JsonSchemaGeneratorSettings Settings(string wire) => wire switch
    {
        "newtonsoft" => SchemaJson.NewtonsoftText(),
        "system-text-json" => SchemaJson.SystemTextJson(),
        _ => SchemaJson.SystemTextJson(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web), SchemaType.OpenApi3),
    };
}
