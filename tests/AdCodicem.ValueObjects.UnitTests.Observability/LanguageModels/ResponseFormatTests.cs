using System.Text.Json;
using System.Text.Json.Nodes;
using AdCodicem.ValueObjects.AI;
using Microsoft.Extensions.AI;
using static AdCodicem.ValueObjects.UnitTests.LanguageModels.ToolSchemaTests;

namespace AdCodicem.ValueObjects.UnitTests.LanguageModels;

/// <summary>
/// <c>ValueObjectResponseFormat.ForJsonSchema&lt;T&gt;()</c>: the response format of a structured output whose value
/// objects carry their rules, named and described as Microsoft.Extensions.AI names and describes one, and an answer read
/// through the same rules.
/// </summary>
public sealed class ResponseFormatTests
{
    private const string Answer =
        """{"account":"FR7630006000011234567890189","quantity":3,"country":"FR","effective":null,"alternates":["DE89370400440532013000"]}""";

    /// <summary>
    /// The schema is the one Microsoft.Extensions.AI writes for the type, led by <c>$schema</c>, with every value object
    /// described, a list's items included, where <c>ChatResponseFormat.ForJsonSchema&lt;T&gt;</c> leaves them open.
    /// </summary>
    [Fact]
    public void The_schema_describes_every_value_object_of_the_answer()
    {
        var format = ValueObjectResponseFormat.ForJsonSchema<Shipment>();
        var schema = Parse(format.Schema!.Value);

        schema.AsObject().First().Key.Should().Be("$schema");
        JsonNode.DeepEquals(
                schema,
                Parse(AIJsonUtilities.CreateJsonSchema(
                    typeof(Shipment),
                    serializerOptions: AIJsonUtilities.DefaultOptions,
                    inferenceOptions: new AIJsonSchemaCreateOptions { IncludeSchemaKeyword = true }.WithValueObjects())))
            .Should().BeTrue();
        ShouldEqual(schema["properties"]!["account"]!, Core(typeof(Iban)), "account");
        ShouldEqual(schema["properties"]!["effective"]!, Core(typeof(EffectiveDate?)), "effective");
        ShouldEqual(schema["properties"]!["alternates"]!["items"]!, Core(typeof(Iban)), "alternates");

        var upstream = Parse(ChatResponseFormat.ForJsonSchema<Shipment>().Schema!.Value);
        upstream["properties"]!["account"]!.GetValueKind().Should().Be(JsonValueKind.True);
        upstream["properties"]!["alternates"]!["items"]!.ToJsonString().Should().Be("{}");
    }

    /// <summary>
    /// The name and the description default as Microsoft.Extensions.AI defaults them: the type's display name or its name,
    /// every character a schema name cannot hold replaced, and its description; explicit ones win.
    /// </summary>
    [Fact]
    public void The_name_and_the_description_default_as_Microsoft_Extensions_AI_defaults_them()
    {
        foreach (var (ours, theirs) in new (ChatResponseFormatJson, ChatResponseFormatJson)[]
                 {
                     (ValueObjectResponseFormat.ForJsonSchema<Shipment>(), ChatResponseFormat.ForJsonSchema<Shipment>()),
                     (ValueObjectResponseFormat.ForJsonSchema<NamedShipment>(), ChatResponseFormat.ForJsonSchema<NamedShipment>()),
                     (ValueObjectResponseFormat.ForJsonSchema<Envelope<Iban>>(), ChatResponseFormat.ForJsonSchema<Envelope<Iban>>()),
                 })
        {
            ours.SchemaName.Should().Be(theirs.SchemaName);
            ours.SchemaDescription.Should().Be(theirs.SchemaDescription);
        }

        ValueObjectResponseFormat.ForJsonSchema<Shipment>().SchemaName.Should().Be("Shipment");
        ValueObjectResponseFormat.ForJsonSchema<Shipment>().SchemaDescription.Should().Be("A shipment to make.");
        ValueObjectResponseFormat.ForJsonSchema<NamedShipment>().SchemaName.Should().Be("shipment_v2");
        ValueObjectResponseFormat.ForJsonSchema<NamedShipment>().SchemaDescription.Should().BeNull();
        ValueObjectResponseFormat.ForJsonSchema<Envelope<Iban>>().SchemaName.Should().Be("Envelope_1");

        var named = ValueObjectResponseFormat.ForJsonSchema<Shipment>(schemaName: "plan", schemaDescription: "A plan.");
        named.SchemaName.Should().Be("plan");
        named.SchemaDescription.Should().Be("A plan.");
    }

    /// <summary>
    /// The options the answer is read with describe it: a source-generated context gives the schema reflection gives.
    /// </summary>
    [Fact]
    public void The_options_the_answer_is_read_with_describe_it()
    {
        var context = ValueObjectResponseFormat.ForJsonSchema<Shipment>(LanguageModelContext.Default.Options);

        context.Schema!.Value.GetRawText().Should().Be(
            ValueObjectResponseFormat.ForJsonSchema<Shipment>(Reflection).Schema!.Value.GetRawText());
        FluentActions.Invoking(() => ValueObjectResponseFormat.ForJsonSchema<OrderLine>(LanguageModelContext.Default.Options))
            .Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// The answer is read through the same rules: a value a value object refuses, or a <c>null</c> for one that cannot be
    /// <see langword="null"/>, throws a <see cref="ValueObjectJsonException"/> from <c>Result</c>, whose code the application
    /// can ask again with.
    /// </summary>
    [Theory]
    [InlineData("account", "\"FR76\"", ValueObjectErrorCodes.TooShort)]
    [InlineData("quantity", "null", ValueObjectErrorCodes.Required)]
    [InlineData("alternates", "[\"XX\"]", ValueObjectErrorCodes.TooShort)]
    public void An_answer_a_value_object_refuses_throws_with_the_code_of_the_rule(string member, string json, string code)
    {
        var answer = JsonNode.Parse(Answer)!.AsObject();
        answer[member] = JsonNode.Parse(json);
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, answer.ToJsonString()));

        var refusal = FluentActions.Invoking(() => new ChatResponse<Shipment>(response, AIJsonUtilities.DefaultOptions).Result)
            .Should().Throw<JsonException>().Which;

        ValueObjectErrors.TryGetCode(refusal, out var found).Should().BeTrue();
        found.Should().Be(code);
        refusal.Message.Should().NotContain(json.Trim('"', '[', ']'));
    }

    /// <summary>
    /// An answer every rule accepts is read into the value objects it describes.
    /// </summary>
    [Fact]
    public void An_answer_every_rule_accepts_is_read()
    {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, Answer));

        var shipment = new ChatResponse<Shipment>(response, AIJsonUtilities.DefaultOptions).Result;

        shipment.Account.Should().Be(Iban.Example);
        shipment.Country.Should().Be(CountryCode.France);
        shipment.Alternates.Should().Equal(Iban.Create("DE89370400440532013000"));
    }
}
