using System.Text.Json;
using System.Text.Json.Nodes;
using AdCodicem.ValueObjects.AI;
using Microsoft.Extensions.AI;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// Microsoft.Extensions.AI tools and structured output with AdCodicem.ValueObjects.AI on the next major, its floor of
/// Microsoft.Extensions.AI.Abstractions resolved as an application without central package management resolves it: the
/// rules in the schemas, a refused tool argument answered with its rule, and a refused answer read with its code.
/// </summary>
public sealed class LanguageModelTests
{
    private const string Account = "FR7630006000011234567890189";

    private static readonly AIFunctionFactoryOptions Factory = new() { JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions().WithValueObjects() };

    /// <summary>
    /// A tool's schema describes each value object it takes with the rules declared on it.
    /// </summary>
    [Fact]
    public void A_tool_schema_carries_the_rules_of_each_value_object()
    {
        var tool = AIFunctionFactory.Create(
            (Iban iban, Quantity quantity, CountryCode country, BirthDate? birth) => $"{iban} {quantity} {country} {birth}",
            Factory);

        var properties = JsonNode.Parse(tool.JsonSchema.GetRawText())!["properties"]!;

        properties["iban"]!["type"]!.GetValue<string>().Should().Be("string");
        properties["iban"]!["minLength"]!.GetValue<int>().Should().Be(15);
        properties["iban"]!["maxLength"]!.GetValue<int>().Should().Be(34);
        properties["iban"]!["pattern"]!.GetValue<string>().Should().Be("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");
        properties["quantity"]!["type"]!.GetValue<string>().Should().Be("integer");
        properties["quantity"]!["minimum"]!.GetValue<int>().Should().Be(1);
        properties["quantity"]!["maximum"]!.GetValue<int>().Should().Be(100);
        properties["country"]!["enum"]!.AsArray().Select(static value => value!.GetValue<string>()).Should().Equal("FR", "BE", "LU");
        properties["birth"]!["format"]!.GetValue<string>().Should().Be("date");
        properties["birth"]!["type"]!.AsArray().Select(static value => value!.GetValue<string>()).Should().Equal("string", "null");
    }

    /// <summary>
    /// Through <c>FunctionInvokingChatClient</c>, a model that sends an argument a value object refuses reads the rule it
    /// broke, never the value, where it would read "Error: Function failed." without the wrapper.
    /// </summary>
    [Fact]
    public async Task A_refused_tool_argument_reaches_the_model_as_its_rule()
    {
        var tool = AIFunctionFactory.Create((Iban iban, Quantity quantity) => $"placed {quantity} on {iban}", Factory)
            .WithValueObjectValidation();

        var refused = await CallAsync(tool, $$"""{"iban":"{{Account}}","quantity":500}""");
        var unwrapped = await CallAsync(AIFunctionFactory.Create((Iban iban, Quantity quantity) => $"placed {quantity} on {iban}", Factory), """{"iban":"FR76","quantity":3}""");
        var accepted = await CallAsync(tool, $$"""{"iban":"{{Account}}","quantity":3}""");

        refused.Exception.Should().BeNull();
        ((JsonElement)refused.Result!).GetRawText().Should().Be(
            """{"error":"invalid_argument","argument":"quantity","code":"value_object.out_of_range","message":"The value is not a valid Quantity: The value must be less than or equal to 100."}""");
        unwrapped.Result.Should().Be("Error: Function failed.");
        ((JsonElement)accepted.Result!).GetString().Should().Be($"placed 3 on {Account}");
    }

    /// <summary>
    /// The response format of a structured output describes the value objects of the answer, a list's items included,
    /// and an answer a value object refuses throws from <c>Result</c> with the code of the rule.
    /// </summary>
    [Fact]
    public void A_structured_output_carries_the_rules_and_a_refused_answer_its_code()
    {
        var format = ValueObjectResponseFormat.ForJsonSchema<Transfer>();
        var properties = JsonNode.Parse(format.Schema!.Value.GetRawText())!["properties"]!;

        format.SchemaName.Should().Be(nameof(Transfer));
        properties["account"]!["minLength"]!.GetValue<int>().Should().Be(15);
        properties["alternates"]!["items"]!["maxLength"]!.GetValue<int>().Should().Be(34);
        properties["quantity"]!["maximum"]!.GetValue<int>().Should().Be(100);

        var answer = new ChatResponse(new ChatMessage(ChatRole.Assistant, $$"""{"account":"{{Account}}","quantity":3,"alternates":["XX"]}"""));
        var refusal = FluentActions.Invoking(() => new ChatResponse<Transfer>(answer, AIJsonUtilities.DefaultOptions).Result)
            .Should().Throw<ValueObjectJsonException>().Which;
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.TooShort);

        var accepted = new ChatResponse(new ChatMessage(ChatRole.Assistant, $$"""{"account":"{{Account}}","quantity":3,"alternates":[]}"""));
        new ChatResponse<Transfer>(accepted, AIJsonUtilities.DefaultOptions).Result.Quantity.Should().Be(Quantity.Create(3));
    }

    private static async Task<FunctionResultContent> CallAsync(AIFunction tool, string arguments)
    {
        var model = new ScriptedModel(tool.Name, arguments);
        using var client = new FunctionInvokingChatClient(model);
        await client.GetResponseAsync("Place the order.", new ChatOptions { Tools = [tool] }, TestContext.Current.CancellationToken);

        return model.Result!;
    }

    /// <summary>An answer a model is asked for.</summary>
    public sealed record Transfer(Iban Account, Quantity Quantity, List<Iban> Alternates);

    /// <summary>A model that calls one tool, then keeps the result it reads.</summary>
    private sealed class ScriptedModel(string tool, string arguments) : IChatClient
    {
        public FunctionResultContent? Result { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (messages.SelectMany(static message => message.Contents).OfType<FunctionResultContent>().LastOrDefault() is { } result)
            {
                Result = result;
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
            }

            var call = new FunctionCallContent("call-1", tool, JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
