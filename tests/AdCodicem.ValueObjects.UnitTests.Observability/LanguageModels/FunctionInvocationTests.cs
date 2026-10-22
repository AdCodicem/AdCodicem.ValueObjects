using System.Text.Json;
using AdCodicem.ValueObjects.AI;
using Microsoft.Extensions.AI;
using static AdCodicem.ValueObjects.UnitTests.LanguageModels.LanguageModelTools;

namespace AdCodicem.ValueObjects.UnitTests.LanguageModels;

/// <summary>
/// What a model reads through <c>FunctionInvokingChatClient</c> when it sends a tool an argument a value object refuses:
/// "Error: Function failed." alone, without the wrapper, and the rule with it.
/// </summary>
public sealed class FunctionInvocationTests
{
    private const string Refused = """{"iban":"FR7630006000011234567890189","quantity":1001,"customer":"6f9619ff-8b86-d011-b42d-00c04fc964ff","country":"FR"}""";

    /// <summary>
    /// <c>FunctionInvokingChatClient</c> hides the exception the binding throws behind a sentence that tells the model
    /// nothing, unless <c>IncludeDetailedErrors</c> exposes the message of every exception any tool throws.
    /// </summary>
    [Fact]
    public async Task Without_the_wrapper_the_model_reads_that_the_function_failed()
    {
        var model = await CallAsync(AIFunctionFactory.Create(PlaceOrder, new AIFunctionFactoryOptions { JsonSchemaCreateOptions = Rules }), Refused);

        model.Result.Should().Be("Error: Function failed.");
        model.Exception.Should().BeOfType<ValueObjectJsonException>();
    }

    /// <summary>
    /// With the wrapper, the model reads the rule its argument broke, as a result, and the function is never called.
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

    private static async Task<FunctionResultContent> CallAsync(AIFunction tool, string arguments)
    {
        var model = new ScriptedChatClient(tool.Name, arguments);
        using var client = new FunctionInvokingChatClient(model);

        await client.GetResponseAsync("Place the order.", new ChatOptions { Tools = [tool] }, TestContext.Current.CancellationToken);

        return model.Result!;
    }
}
