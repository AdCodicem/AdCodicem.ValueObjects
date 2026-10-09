using System.Text.Json.Nodes;
using AdCodicem.ValueObjects.AI;
using Microsoft.Extensions.AI;
using static AdCodicem.ValueObjects.UnitTests.LanguageModels.LanguageModelTools;

namespace AdCodicem.ValueObjects.UnitTests.LanguageModels;

/// <summary>
/// What Microsoft.Extensions.AI.OpenAI sends OpenAI in strict mode, captured before it leaves: the lengths, the pattern,
/// the bounds and the format move into the description, so the rules still reach the model, and the result of a refused
/// argument goes back as the tool's message. These pin the adapter's behaviour, so that a change upstream shows here.
/// </summary>
public sealed class OpenAIStrictModeTests
{
    /// <summary>
    /// The adapter starts each line it adds to a description as the platform does, <c>\r\n</c> on Windows, where the
    /// value object's own description holds <c>\n</c> alone; written here as JSON writes it.
    /// </summary>
    private static readonly string Line = Environment.NewLine
        .Replace("\r", @"\r", StringComparison.Ordinal)
        .Replace("\n", @"\n", StringComparison.Ordinal);

    /// <summary>
    /// In strict mode, the adapter keeps <c>type</c>, <c>enum</c> and <c>examples</c>, and writes the lengths, the
    /// pattern, the bounds and the format into the description, after the value object's own.
    /// </summary>
    [Fact]
    public async Task Strict_mode_keeps_the_rules_in_the_description()
    {
        var handler = CapturingHandler.CallingTool("PlaceOrder", ValidOrder);
        using var client = new FunctionInvokingChatClient(handler.CreateClient());

        await client.GetResponseAsync(
            "Place the order.",
            new ChatOptions { Tools = [Validated(PlaceOrder)], AdditionalProperties = new() { ["strict"] = true } },
            TestContext.Current.CancellationToken);

        var function = handler.Requests[0]["tools"]![0]!["function"]!;
        function["strict"]!.GetValue<bool>().Should().BeTrue();
        var properties = function["parameters"]!["properties"]!;
        ShouldDescribe(
            properties["iban"]!,
            $$"""{"description":"The account to debit.\n\nAn International Bank Account Number, stored in its electronic form.\n\nFormat: iban.{{Line}}minLength: 15{{Line}}maxLength: 34{{Line}}pattern: ^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$","type":"string","examples":["FR7630006000011234567890189"]}""");
        ShouldDescribe(
            properties["quantity"]!,
            $$"""{"description":"A quantity of items, tested to cover the narrow integer promotion path.\n\nFormat: int32.{{Line}}minimum: 0{{Line}}maximum: 1000","type":"integer"}""");
        ShouldDescribe(
            properties["country"]!,
            $$"""{"description":"An ISO 3166-1 alpha-2 country code restricted to the countries the application serves.{{Line}}minLength: 2{{Line}}maxLength: 2","type":"string","enum":["FR","BE","LU"]}""");
        ShouldDescribe(
            properties["customer"]!,
            $$"""{"description":"The identifier of a customer.{{Line}}format: uuid","type":"string"}""");
    }

    /// <summary>
    /// The adapter hands a JSON <c>null</c> to the function as <see langword="null"/>, which the wrapper answers as
    /// required; the result goes back to OpenAI as the tool's message, as does the rule of a refused value.
    /// </summary>
    [Theory]
    [InlineData("null", ValueObjectErrorCodes.Required, "A value is required.")]
    [InlineData("1001", ValueObjectErrorCodes.OutOfRange, "The value is not a valid Quantity: The value must be less than or equal to 1000.")]
    public async Task A_refused_argument_goes_back_to_OpenAI_as_the_tool_s_message(string quantity, string code, string message)
    {
        var arguments = JsonNode.Parse(ValidOrder)!.AsObject();
        arguments["quantity"] = JsonNode.Parse(quantity);
        var handler = CapturingHandler.CallingTool("PlaceOrder", arguments.ToJsonString());
        using var client = new FunctionInvokingChatClient(handler.CreateClient());

        await client.GetResponseAsync("Place the order.", new ChatOptions { Tools = [Validated(PlaceOrder)] }, TestContext.Current.CancellationToken);

        var tool = handler.Requests[1]["messages"]!.AsArray()[^1]!;
        tool["role"]!.GetValue<string>().Should().Be("tool");
        tool["tool_call_id"]!.GetValue<string>().Should().Be("call-1");
        JsonNode.DeepEquals(JsonNode.Parse(tool["content"]!.GetValue<string>()), JsonNode.Parse(Rejection("quantity", code, message)))
            .Should().BeTrue("the tool's message is {0}", tool["content"]);
    }

    /// <summary>
    /// The response format of a structured output goes out strict, with the rules of its value objects in the
    /// description, where the format of <c>ChatResponseFormat.ForJsonSchema&lt;T&gt;</c> carries none.
    /// </summary>
    [Fact]
    public async Task A_structured_output_goes_out_strict_with_the_rules_in_the_description()
    {
        var handler = CapturingHandler.Answering("{}");
        var client = handler.CreateClient();

        await client.GetResponseAsync(
            "Plan a shipment.",
            new ChatOptions { ResponseFormat = ValueObjectResponseFormat.ForJsonSchema<Shipment>(), AdditionalProperties = new() { ["strict"] = true } },
            TestContext.Current.CancellationToken);
        await client.GetResponseAsync(
            "Plan a shipment.",
            new ChatOptions { ResponseFormat = ChatResponseFormat.ForJsonSchema<Shipment>(), AdditionalProperties = new() { ["strict"] = true } },
            TestContext.Current.CancellationToken);

        var format = handler.Requests[0]["response_format"]!;
        format["type"]!.GetValue<string>().Should().Be("json_schema");
        format["json_schema"]!["name"]!.GetValue<string>().Should().Be("Shipment");
        format["json_schema"]!["strict"]!.GetValue<bool>().Should().BeTrue();
        var properties = format["json_schema"]!["schema"]!["properties"]!;
        properties["quantity"]!["description"]!.GetValue<string>().Should().EndWith($"Format: int32.{Environment.NewLine}minimum: 0{Environment.NewLine}maximum: 1000");
        properties["alternates"]!["items"]!["description"]!.GetValue<string>().Should().EndWith("pattern: ^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");

        var upstream = handler.Requests[1]["response_format"]!["json_schema"]!["schema"]!["properties"]!;
        ShouldDescribe(upstream["quantity"]!, "{}");
        ShouldDescribe(upstream["alternates"]!, """{"type":"array","items":{}}""");
    }

    private static void ShouldDescribe(JsonNode schema, string expected)
        => JsonNode.DeepEquals(schema, JsonNode.Parse(expected)).Should().BeTrue($"the schema is {schema.ToJsonString()}");
}
