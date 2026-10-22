using System.Text.Json;
using AdCodicem.ValueObjects.AI;
using AdCodicem.ValueObjects.ModelContextProtocol;
using AdCodicem.ValueObjects.UnitTests.LanguageModels;
using AdCodicem.ValueObjects.UnitTests.Logging;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The observability family's part of Sample (GeneratedSurface/Sample.cs): the checks that hold every sample to Serilog,
// Microsoft.Extensions.AI and the Model Context Protocol.

public abstract partial class Sample
{
    /// <summary>
    /// Logs the value object through Serilog, with and without <c>@</c>, under the policy and under the option of
    /// <c>Destructure.ValueObjects()</c>, and checks that it is logged as the value it carries.
    /// </summary>
    public abstract void LogsAsItsUnderlyingValueThroughSerilog();

    /// <summary>
    /// Builds a tool taking the value object alone, nullable and in a list, with the schema options of
    /// <c>WithValueObjects()</c> and wrapped by <c>WithValueObjectValidation()</c>, and checks that each is described as the
    /// JSON Schema core describes it for a language model, that the refused text is answered with the code and the message
    /// the converter refuses it with, alone or in the list, and that accepted values reach the tool.
    /// </summary>
    /// <returns>The check.</returns>
    public abstract Task AnswersARefusedToolArgumentWithItsRuleAsync();

    /// <summary>
    /// Creates a Model Context Protocol tool taking the value object alone, nullable and in a list with
    /// <c>ValueObjectMcpServerTool.Create</c>, registers it with <c>WithValueObjectTools</c> on a server in process, and
    /// checks that the server lists each as the JSON Schema core describes it for a language model, that the refused text
    /// is answered with a tool execution error carrying the code and the message the converter refuses it with, alone,
    /// nullable or in the list, that a JSON null is required, and that accepted values reach the tool.
    /// </summary>
    /// <returns>The check.</returns>
    public abstract Task AnswersARefusedMcpToolArgumentWithItsRuleAsync();
}

public partial class Sample<TSelf, TValue>
{
    public override void LogsAsItsUnderlyingValueThroughSerilog()
    {
        SerilogParity.Check<TSelf, TValue>(Small);
        SerilogParity.Check<TSelf, TValue>(Large);
    }

    public override async Task AnswersARefusedToolArgumentWithItsRuleAsync()
    {
        var tool = AIFunctionFactory.Create(
                (TSelf value, TSelf? optional, List<TSelf> values) => $"{value}|{optional}|{values.Count}",
                new AIFunctionFactoryOptions { JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions().WithValueObjects() })
            .WithValueObjectValidation();

        var properties = ToolSchemaTests.Parse(tool.JsonSchema)["properties"]!;
        ToolSchemaTests.ShouldEqual(properties["value"]!, ToolSchemaTests.Core(typeof(TSelf)), "value");
        ToolSchemaTests.ShouldEqual(properties["optional"]!, ToolSchemaTests.Core(typeof(TSelf?)), "optional");
        ToolSchemaTests.ShouldEqual(properties["values"]!["items"]!, ToolSchemaTests.Core(typeof(TSelf)), "values");

        var small = JsonSerializer.Serialize(Small, AIJsonUtilities.DefaultOptions);
        var large = JsonSerializer.Serialize(Large, AIJsonUtilities.DefaultOptions);
        var isNumber = !small.StartsWith('"') && small is not ("true" or "false");
        var token = isNumber && IsJsonNumber(Refused) ? Refused : JsonSerializer.Serialize(Refused);
        var refusal = LanguageModelTools.RefusalOf<TSelf>(token);

        // A token the reader cannot read at all leaves the message to System.Text.Json, which writes the path of the value
        // after it: the answer then names the value object alone, nullable or in a list.
        var nullableRefusal = LanguageModelTools.RefusalOf<TSelf?>(token);
        var listRefusal = LanguageModelTools.RefusalOf<List<TSelf>>($"[{large},{token}]");

        (await LanguageModelTools.InvokeAsync(tool, LanguageModelTools.Arguments($$"""{"value":{{token}},"optional":null,"values":[]}""")))
            .Should().Be(LanguageModelTools.Rejection("value", refusal.ErrorCode, LanguageModelTools.AnsweredMessage(refusal)));
        (await LanguageModelTools.InvokeAsync(tool, LanguageModelTools.Arguments($$"""{"value":{{small}},"optional":{{token}},"values":[]}""")))
            .Should().Be(LanguageModelTools.Rejection("optional", nullableRefusal.ErrorCode, LanguageModelTools.AnsweredMessage(nullableRefusal)));
        (await LanguageModelTools.InvokeAsync(tool, LanguageModelTools.Arguments($$"""{"value":{{small}},"optional":null,"values":[{{large}},{{token}}]}""")))
            .Should().Be(LanguageModelTools.Rejection("values", listRefusal.ErrorCode, LanguageModelTools.AnsweredMessage(listRefusal)))
            .And.NotContain("Path:");
        (await LanguageModelTools.InvokeAsync(tool, LanguageModelTools.Arguments($$"""{"value":null,"optional":null,"values":[]}""")))
            .Should().Be(LanguageModelTools.Rejection("value", ValueObjectErrorCodes.Required, "A value is required."));

        var accepted = await tool.InvokeAsync(
            LanguageModelTools.Arguments($$"""{"value":{{small}},"optional":{{large}},"values":[{{small}},{{large}}]}"""),
            TestContext.Current.CancellationToken);
        ((JsonElement)accepted!).GetString().Should().Be($"{Small}|{Large}|2");
    }

    public override async Task AnswersARefusedMcpToolArgumentWithItsRuleAsync()
    {
        var echo = (TSelf value, TSelf? optional, List<TSelf> values) => $"{value}|{optional}|{values.Count}";
        var tool = ValueObjectMcpServerTool.Create(echo.Method, echo.Target, new McpServerToolCreateOptions { Name = "echo" });
        await using var harness = await McpHarness.StartAsync(server => server.WithValueObjectTools([tool]));

        var properties = ToolSchemaTests.Parse((await harness.ListAsync())["echo"].InputSchema)["properties"]!;
        ToolSchemaTests.ShouldEqual(properties["value"]!, McpToolSchemaTests.McpCore(typeof(TSelf)), "value");
        ToolSchemaTests.ShouldEqual(properties["optional"]!, McpToolSchemaTests.McpCore(typeof(TSelf?)), "optional");
        ToolSchemaTests.ShouldEqual(properties["values"]!["items"]!, McpToolSchemaTests.McpCore(typeof(TSelf)), "values");

        var small = JsonSerializer.Serialize(Small, McpJsonUtilities.DefaultOptions);
        var large = JsonSerializer.Serialize(Large, McpJsonUtilities.DefaultOptions);
        var isNumber = !small.StartsWith('"') && small is not ("true" or "false");
        var token = isNumber && IsJsonNumber(Refused) ? Refused : JsonSerializer.Serialize(Refused);
        var refusal = LanguageModelTools.RefusalOf<TSelf>(token, McpJsonUtilities.DefaultOptions);
        var nullableRefusal = LanguageModelTools.RefusalOf<TSelf?>(token, McpJsonUtilities.DefaultOptions);
        var listRefusal = LanguageModelTools.RefusalOf<List<TSelf>>($"[{large},{token}]", McpJsonUtilities.DefaultOptions);
        var nullRefusal = LanguageModelTools.RefusalOf<TSelf>("null", McpJsonUtilities.DefaultOptions);

        McpHarness.ShouldBeRefused(
            await harness.CallAsync("echo", $$"""{"value":{{token}},"optional":null,"values":[]}"""),
            "value",
            refusal.ErrorCode,
            LanguageModelTools.AnsweredMessage(refusal));
        McpHarness.ShouldBeRefused(
            await harness.CallAsync("echo", $$"""{"value":{{small}},"optional":{{token}},"values":[]}"""),
            "optional",
            nullableRefusal.ErrorCode,
            LanguageModelTools.AnsweredMessage(nullableRefusal));
        McpHarness.ShouldBeRefused(
            await harness.CallAsync("echo", $$"""{"value":{{small}},"optional":null,"values":[{{large}},{{token}}]}"""),
            "values",
            listRefusal.ErrorCode,
            LanguageModelTools.AnsweredMessage(listRefusal));
        McpHarness.ShouldBeRefused(
            await harness.CallAsync("echo", """{"value":null,"optional":null,"values":[]}"""),
            "value",
            ValueObjectErrorCodes.Required,
            nullRefusal.Message);

        var accepted = await harness.CallAsync("echo", $$"""{"value":{{small}},"optional":{{large}},"values":[{{small}},{{large}}]}""");
        accepted.IsError.Should().NotBe(true);
        McpHarness.TextOf(accepted).Should().Be($"{Small}|{Large}|2");
    }
}
