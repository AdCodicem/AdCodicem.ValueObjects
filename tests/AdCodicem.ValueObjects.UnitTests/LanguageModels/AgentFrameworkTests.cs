using System.Text.Json;
using System.Text.Json.Nodes;
using AdCodicem.ValueObjects.AI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using static AdCodicem.ValueObjects.UnitTests.LanguageModels.LanguageModelTools;

namespace AdCodicem.ValueObjects.UnitTests.LanguageModels;

/// <summary>
/// Agent Framework, run as an application runs it: a <c>ChatClientAgent</c> calls its tools through
/// <c>FunctionInvokingChatClient</c>, so a tool built with <c>AIFunctionFactory</c> takes the same two calls, and its
/// <c>RunAsync&lt;T&gt;</c> builds a response format no schema option reaches, which the run options replace.
/// </summary>
public sealed class AgentFrameworkTests
{
    private const string Refused = """{"iban":"FR7630006000011234567890189","quantity":1001,"customer":"6f9619ff-8b86-d011-b42d-00c04fc964ff","country":"FR"}""";

    private const string Answer =
        """{"account":"FR7630006000011234567890189","quantity":3,"country":"FR","effective":null,"alternates":["DE89370400440532013000"]}""";

    /// <summary>
    /// The agent hands the model the tool as it was built: its schema carries the rules.
    /// </summary>
    [Fact]
    public async Task The_model_reads_the_rules_in_the_schema_of_an_agent_tool()
    {
        var tool = Validated(PlaceOrder);
        var model = new AnsweringChatClient("done");

        await new ChatClientAgent(model, tools: [tool]).RunAsync("Place the order.", cancellationToken: TestContext.Current.CancellationToken);

        var sent = model.Options!.Tools.Should().ContainSingle().Which.Should().BeAssignableTo<AIFunction>().Which;
        JsonNode.DeepEquals(JsonNode.Parse(sent.JsonSchema.GetRawText()), JsonNode.Parse(tool.JsonSchema.GetRawText())).Should().BeTrue();
        sent.JsonSchema.GetProperty("properties").GetProperty("quantity").GetProperty("maximum").GetInt32().Should().Be(1000);
        sent.JsonSchema.GetProperty("properties").GetProperty("iban").ValueKind.Should().Be(JsonValueKind.Object);
    }

    /// <summary>
    /// Without the wrapper, the agent's function invocation answers a refused argument as Microsoft.Extensions.AI's does.
    /// </summary>
    [Fact]
    public async Task Without_the_wrapper_the_model_reads_that_the_function_failed()
    {
        var model = await CallAsync(AIFunctionFactory.Create(PlaceOrder, new AIFunctionFactoryOptions { JsonSchemaCreateOptions = Rules }), Refused);

        model.Result.Should().Be("Error: Function failed.");
        model.Exception.Should().BeOfType<ValueObjectJsonException>();
    }

    /// <summary>
    /// With the wrapper, the model reads the rule its argument broke, with its code, and the function is never called.
    /// </summary>
    [Fact]
    public async Task With_the_wrapper_the_model_reads_the_rule_its_argument_broke()
    {
        var model = await CallAsync(Validated(PlaceOrder), Refused);

        model.Exception.Should().BeNull();
        model.Result.Should().BeOfType<JsonElement>().Which.GetRawText().Should().Be(
            Rejection("quantity", ValueObjectErrorCodes.OutOfRange, "The value is not a valid Quantity: The value must be less than or equal to 1000."));
    }

    /// <summary>
    /// An argument every rule accepts reaches the function, whose result the model reads.
    /// </summary>
    [Fact]
    public async Task With_the_wrapper_accepted_arguments_reach_the_function()
    {
        var model = await CallAsync(Validated(PlaceOrder), ValidOrder);

        model.Exception.Should().BeNull();
        model.Result.Should().BeOfType<JsonElement>().Which.GetString().Should().StartWith("placed 3 on FR76");
    }

    /// <summary>
    /// <c>RunAsync&lt;T&gt;</c> replaces the response format, the one its run options carry included, with
    /// <c>ChatResponseFormat.ForJsonSchema&lt;T&gt;</c>, built with private schema options: the model reads every value object
    /// as the schema that accepts anything. This pins Agent Framework's behaviour, which the next test works around.
    /// </summary>
    [Fact]
    public async Task RunAsync_of_T_sends_a_response_format_without_the_rules()
    {
        var model = new AnsweringChatClient(Answer);
        var agent = new ChatClientAgent(model);

        await agent.RunAsync<Shipment>(
            "Plan the shipment.",
            serializerOptions: AIJsonUtilities.DefaultOptions,
            options: new AgentRunOptions { ResponseFormat = ValueObjectResponseFormat.ForJsonSchema<Shipment>() },
            cancellationToken: TestContext.Current.CancellationToken);

        var sent = JsonNode.Parse(model.Options!.ResponseFormat.Should().BeOfType<ChatResponseFormatJson>().Which.Schema!.Value.GetRawText())!;
        sent["properties"]!["account"]!.GetValueKind().Should().Be(JsonValueKind.True);
        sent["properties"]!["alternates"]!["items"]!.ToJsonString().Should().Be("{}");
    }

    /// <summary>
    /// The run options of a plain <c>RunAsync</c> carry the response format with the rules to the model, and
    /// <c>AgentResponse&lt;T&gt;</c> reads the answer through the same options.
    /// </summary>
    [Fact]
    public async Task The_run_options_carry_the_rules_and_the_answer_is_read_through_them()
    {
        var format = ValueObjectResponseFormat.ForJsonSchema<Shipment>(AIJsonUtilities.DefaultOptions);
        var model = new AnsweringChatClient(Answer);
        var agent = new ChatClientAgent(model);

        var response = await agent.RunAsync(
            "Plan the shipment.",
            options: new AgentRunOptions { ResponseFormat = format },
            cancellationToken: TestContext.Current.CancellationToken);

        model.Options!.ResponseFormat.Should().BeSameAs(format);
        var shipment = new AgentResponse<Shipment>(response, AIJsonUtilities.DefaultOptions).Result;
        shipment.Account.Should().Be(Iban.Example);
        shipment.Alternates.Should().Equal(Iban.Create("DE89370400440532013000"));
    }

    /// <summary>
    /// An answer a value object refuses throws a <see cref="ValueObjectJsonException"/> from <c>Result</c>, whose code the
    /// application can ask again with.
    /// </summary>
    [Fact]
    public async Task An_answer_a_value_object_refuses_throws_with_the_code_of_the_rule()
    {
        var answer = JsonNode.Parse(Answer)!.AsObject();
        answer["account"] = "FR76";
        var agent = new ChatClientAgent(new AnsweringChatClient(answer.ToJsonString()));

        var response = await agent.RunAsync(
            "Plan the shipment.",
            options: new AgentRunOptions { ResponseFormat = ValueObjectResponseFormat.ForJsonSchema<Shipment>(AIJsonUtilities.DefaultOptions) },
            cancellationToken: TestContext.Current.CancellationToken);

        var refusal = FluentActions.Invoking(() => new AgentResponse<Shipment>(response, AIJsonUtilities.DefaultOptions).Result)
            .Should().Throw<JsonException>().Which;
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.TooShort);
    }

    private static async Task<FunctionResultContent> CallAsync(AIFunction tool, string arguments)
    {
        var model = new ScriptedChatClient(tool.Name, arguments);

        await new ChatClientAgent(model, tools: [tool]).RunAsync("Place the order.", cancellationToken: TestContext.Current.CancellationToken);

        return model.Result!;
    }

    /// <summary>A model that answers the same text to every request, and keeps the options of the last one.</summary>
    private sealed class AnsweringChatClient(string answer) : IChatClient
    {
        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Options = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
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
