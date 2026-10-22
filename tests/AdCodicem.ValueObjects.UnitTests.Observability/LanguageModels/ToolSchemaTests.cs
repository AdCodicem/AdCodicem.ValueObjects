using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using AdCodicem.ValueObjects.AI;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using Microsoft.Extensions.AI;
using static AdCodicem.ValueObjects.UnitTests.LanguageModels.LanguageModelTools;

namespace AdCodicem.ValueObjects.UnitTests.LanguageModels;

/// <summary>
/// <c>WithValueObjects()</c>: the schema of a tool Microsoft.Extensions.AI builds describes each value object it takes
/// with the rules declared on it, as the JSON Schema core describes it for a language model.
/// </summary>
public sealed class ToolSchemaTests
{
    /// <summary>
    /// Every value object of the domain, over each of the underlying types, is described in a tool's schema alone,
    /// nullable and in a list, and an argument it refuses is answered with its rule.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public async Task Every_value_object_is_described_in_a_tool_schema_and_a_refused_argument_answered_with_its_rule(string type)
        => await Samples.All[type].AnswersARefusedToolArgumentWithItsRuleAsync();

    /// <summary>
    /// Without the package, Microsoft.Extensions.AI describes a value object as <c>true</c>, the schema that accepts
    /// anything, keeping only the description a <c>[Description]</c> gives the parameter.
    /// </summary>
    [Fact]
    public void Without_the_package_a_value_object_parameter_is_described_as_anything()
    {
        var schema = Parse(AIFunctionFactory.Create(PlaceOrder).JsonSchema);

        ShouldDescribe(schema["properties"]!["iban"]!, """{"description":"The account to debit."}""");
        schema["properties"]!["quantity"]!.GetValueKind().Should().Be(JsonValueKind.True);
        schema["properties"]!["country"]!.GetValueKind().Should().Be(JsonValueKind.True);
    }

    /// <summary>
    /// Each value object a tool takes, alone, nullable, generic, in a list, an array, a dictionary or an object, is
    /// described as the JSON Schema core describes it under the language-model profile.
    /// </summary>
    [Fact]
    public void A_value_object_parameter_is_described_as_the_JSON_Schema_core_describes_it_for_a_language_model()
    {
        var schema = Parse(AIFunctionFactory.Create(
            (Quantity quantity, Iban iban, CountryCode country, EffectiveDate effective, LedgerBalance balance, Amount amount,
                Reference<PurchaseOrder> reference, BirthDate? birth, List<Iban> accounts, Iban?[] optional,
                Dictionary<CountryCode, Quantity> perCountry, OrderLine line) => "described",
            new AIFunctionFactoryOptions { JsonSchemaCreateOptions = Rules }).JsonSchema)["properties"]!;

        foreach (var (name, type) in new (string, Type)[]
                 {
                     ("quantity", typeof(Quantity)), ("iban", typeof(Iban)), ("country", typeof(CountryCode)),
                     ("effective", typeof(EffectiveDate)), ("balance", typeof(LedgerBalance)), ("amount", typeof(Amount)),
                     ("reference", typeof(Reference<PurchaseOrder>)), ("birth", typeof(BirthDate?)),
                 })
        {
            ShouldEqual(schema[name]!, Core(type), name);
        }

        ShouldEqual(schema["accounts"]!["items"]!, Core(typeof(Iban)), "accounts");
        ShouldEqual(schema["optional"]!["items"]!, Core(typeof(Iban?)), "optional");
        ShouldEqual(schema["perCountry"]!["additionalProperties"]!, Core(typeof(Quantity)), "perCountry");
        ShouldEqual(schema["perCountry"]!["propertyNames"]!, Core(typeof(Dictionary<CountryCode, int>))["propertyNames"]!, "perCountry");
        ShouldEqual(schema["line"]!["properties"]!["account"]!, Core(typeof(Iban)), "line");
        ShouldEqual(schema["line"]!["properties"]!["quantity"]!, Core(typeof(Quantity)), "line");
    }

    /// <summary>
    /// The language-model profile writes the JSON type alone and moves a format JSON Schema does not define into the
    /// description, which follows the one the parameter's <c>[Description]</c> gives.
    /// </summary>
    [Fact]
    public void A_parameter_description_comes_first_and_the_value_object_follows_it()
    {
        var properties = Parse(Validated(PlaceOrder).JsonSchema)["properties"]!;

        ShouldDescribe(
            properties["iban"]!,
            """{"description":"The account to debit.\n\nAn International Bank Account Number, stored in its electronic form.\n\nFormat: iban.","type":"string","pattern":"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$","minLength":15,"maxLength":34,"examples":["FR7630006000011234567890189"]}""");
        ShouldDescribe(
            properties["quantity"]!,
            """{"description":"A quantity of items, tested to cover the narrow integer promotion path.\n\nFormat: int32.","type":"integer","minimum":0,"maximum":1000}""");
        ShouldDescribe(
            properties["country"]!,
            """{"description":"An ISO 3166-1 alpha-2 country code restricted to the countries the application serves.","type":"string","minLength":2,"maxLength":2,"enum":["FR","BE","LU"]}""");
        ShouldDescribe(
            properties["effective"]!,
            """{"default":null,"description":"The day a contract takes effect, never before the first day the ledger covers.\n\nAt least 2000-01-01.","type":["string","null"],"format":"date"}""");
    }

    /// <summary>
    /// A transform the options already carry runs first, and the value object's description completes what it wrote.
    /// </summary>
    [Fact]
    public void A_transform_the_options_carry_runs_first_and_is_completed()
    {
        var options = new AIJsonSchemaCreateOptions
        {
            TransformSchemaNode = static (context, schema) =>
            {
                if (context.TypeInfo.Type == typeof(Quantity))
                {
                    return new JsonObject { ["x-host"] = schema.GetValueKind() == JsonValueKind.True, ["description"] = "From the host." };
                }

                return schema;
            },
        }.WithValueObjects();

        var quantity = Parse(AIFunctionFactory.Create(Count, new AIFunctionFactoryOptions { JsonSchemaCreateOptions = options }).JsonSchema)["properties"]!["quantity"]!;

        ShouldDescribe(
            quantity,
            """{"x-host":true,"description":"From the host.\n\nA quantity of items, tested to cover the narrow integer promotion path.\n\nFormat: int32.","type":"integer","minimum":0,"maximum":1000}""");
    }

    /// <summary>
    /// <see langword="null"/> starts from <see cref="AIJsonSchemaCreateOptions.Default"/>, which stays as it was: a
    /// record, copied.
    /// </summary>
    [Fact]
    public void Null_options_start_from_the_default_options_which_stay_untouched()
    {
        var options = ((AIJsonSchemaCreateOptions?)null).WithValueObjects();

        options.Should().NotBeSameAs(AIJsonSchemaCreateOptions.Default);
        options.TransformSchemaNode.Should().NotBeNull();
        AIJsonSchemaCreateOptions.Default.TransformSchemaNode.Should().BeNull();
        (options with { TransformSchemaNode = null }).Should().Be(AIJsonSchemaCreateOptions.Default);
    }

    /// <summary>
    /// Every other setting of the options is kept, and the options given are left as they are.
    /// </summary>
    [Fact]
    public void Every_other_setting_is_kept_and_the_options_given_are_left_untouched()
    {
        Func<ParameterInfo, bool> include = static parameter => parameter.Name != "customer";
        Func<ParameterInfo, string?> describe = static parameter => parameter.Name == "quantity" ? "How many." : null;
        var given = new AIJsonSchemaCreateOptions
        {
            IncludeSchemaKeyword = true,
            IncludeParameter = include,
            ParameterDescriptionProvider = describe,
            TransformOptions = new AIJsonSchemaTransformOptions { DisallowAdditionalProperties = true },
        };

        var options = given.WithValueObjects();

        options.IncludeSchemaKeyword.Should().BeTrue();
        options.IncludeParameter.Should().BeSameAs(include);
        options.ParameterDescriptionProvider.Should().BeSameAs(describe);
        options.TransformOptions.Should().BeSameAs(given.TransformOptions);
        given.TransformSchemaNode.Should().BeNull();

        var schema = Parse(AIFunctionFactory.Create(PlaceOrder, new AIFunctionFactoryOptions { JsonSchemaCreateOptions = options }).JsonSchema);
        schema["$schema"]!.GetValue<string>().Should().Be("https://json-schema.org/draft/2020-12/schema");
        schema["additionalProperties"]!.GetValue<bool>().Should().BeFalse();
        schema["properties"]!.AsObject().Select(static member => member.Key).Should().NotContain("customer");
        schema["properties"]!["quantity"]!["description"]!.GetValue<string>().Should().StartWith("How many.\n\nA quantity of items");
    }

    /// <summary>
    /// The options can be given the transform twice, and describe each value object once.
    /// </summary>
    [Fact]
    public void Options_given_the_transform_twice_describe_a_value_object_once()
    {
        var once = AIFunctionFactory.Create(PlaceOrder, new AIFunctionFactoryOptions { JsonSchemaCreateOptions = Rules }).JsonSchema;
        var twice = AIFunctionFactory.Create(PlaceOrder, new AIFunctionFactoryOptions { JsonSchemaCreateOptions = Rules.WithValueObjects() }).JsonSchema;

        twice.GetRawText().Should().Be(once.GetRawText());
    }

    /// <summary>
    /// With reflection out of the way, a source-generated context naming the converter factory and the value objects, not
    /// their underlying types, gives the schema reflection gives.
    /// </summary>
    [Fact]
    public void A_source_generated_context_naming_the_value_objects_gives_the_schema_reflection_gives()
    {
        var reflection = AIFunctionFactory.Create(
            PlaceOrder,
            new AIFunctionFactoryOptions { JsonSchemaCreateOptions = Rules, SerializerOptions = Reflection });
        var context = AIFunctionFactory.Create(
            PlaceOrder,
            new AIFunctionFactoryOptions { JsonSchemaCreateOptions = Rules, SerializerOptions = LanguageModelContext.Default.Options });

        context.JsonSchema.GetRawText().Should().Be(reflection.JsonSchema.GetRawText());
        Parse(context.JsonSchema)["properties"]!["quantity"]!["maximum"]!.GetValue<int>().Should().Be(1000);
    }

    /// <summary>Gets options that read every type by reflection, under the defaults of the source-generated context.</summary>
    internal static JsonSerializerOptions Reflection { get; } =
        new(JsonSerializerDefaults.Web) { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };

    /// <summary>Describes a type as the JSON Schema core does for a language model, alone.</summary>
    internal static JsonNode Core(Type type)
        => JsonSchemaExporter.GetJsonSchemaAsNode(
            AIJsonUtilities.DefaultOptions,
            type,
            new JsonSchemaExporterOptions
            {
                TransformSchemaNode = ValueObjectJsonSchema.CreateTransform(ValueObjectJsonSchemaProfile.LanguageModel),
                TreatNullObliviousAsNonNullable = true,
            });

    internal static JsonNode Parse(JsonElement schema) => JsonNode.Parse(schema.GetRawText())!;

    internal static void ShouldEqual(JsonNode actual, JsonNode expected, string because)
        => JsonNode.DeepEquals(actual, expected).Should().BeTrue("{0} is {1}, where the core writes {2}", because, actual.ToJsonString(), expected.ToJsonString());

    private static void ShouldDescribe(JsonNode schema, string expected)
        => JsonNode.DeepEquals(schema, JsonNode.Parse(expected)).Should().BeTrue($"the schema is {schema.ToJsonString()}");
}
