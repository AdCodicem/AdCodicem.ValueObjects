using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.ModelContextProtocol;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static AdCodicem.ValueObjects.UnitTests.LanguageModels.McpHarness;

namespace AdCodicem.ValueObjects.UnitTests.LanguageModels;

/// <summary>
/// What a client reads when a model sends a Model Context Protocol tool an argument a value object refuses: the SDK's
/// bare error without the package, and with it a tool execution error carrying the rule's code and message, never the
/// value sent.
/// </summary>
public sealed class McpArgumentValidationTests
{
    /// <summary>
    /// Without the package, the SDK answers a refused argument with an error that names the tool alone.
    /// </summary>
    [Fact]
    public async Task Without_the_package_a_refused_argument_gets_an_error_naming_the_tool_alone()
    {
        await using var harness = await StartAsync(static server => server.WithTools<OrderTools>());

        var result = await harness.CallAsync("place_order", Order("quantity", "1001"));

        result.IsError.Should().BeTrue();
        TextOf(result).Should().Be("An error occurred invoking 'place_order'.");
        result.StructuredContent.Should().BeNull();
    }

    /// <summary>
    /// Each rule a value object breaks is answered with its code and the converter's message, in a sentence and as
    /// structured content, neither holding the value sent, and the tool is never called.
    /// </summary>
    /// <param name="argument">The argument refused.</param>
    /// <param name="json">The JSON sent for it.</param>
    /// <param name="code">The code it is refused with.</param>
    [Theory]
    [InlineData("iban", "\"FR76\"", ValueObjectErrorCodes.TooShort)]
    [InlineData("iban", "\"FR7630006000011234567890189FR7630006000011234567890189\"", ValueObjectErrorCodes.TooLong)]
    [InlineData("iban", "\"1234567890123456\"", ValueObjectErrorCodes.InvalidFormat)]
    [InlineData("iban", "42", ValueObjectErrorCodes.NotParsable)]
    [InlineData("quantity", "1001", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("quantity", "\"1001\"", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("quantity", "true", ValueObjectErrorCodes.NotParsable)]
    [InlineData("quantity", "99999", ValueObjectErrorCodes.NotParsable)]
    [InlineData("quantity", "null", ValueObjectErrorCodes.Required)]
    [InlineData("customer", "\"not-a-guid\"", ValueObjectErrorCodes.NotParsable)]
    [InlineData("customer", "\"00000000-0000-0000-0000-000000000000\"", ValueObjectErrorCodes.Required)]
    [InlineData("country", "\"XX\"", ValueObjectErrorCodes.NotAKnownValue)]
    [InlineData("country", "\"FRA\"", ValueObjectErrorCodes.TooLong)]
    [InlineData("delivery", "\"1850-01-01\"", ValueObjectErrorCodes.OutOfRange)]
    public async Task A_refused_argument_is_answered_with_its_rule(string argument, string json, string code)
    {
        await using var harness = await StartAsync(static server => server.WithValueObjectTools<OrderTools>());

        var result = await harness.CallAsync("place_order", Order(argument, json));

        var refusal = argument switch
        {
            "iban" => LanguageModelTools.RefusalOf<Iban>(json, McpJsonUtilities.DefaultOptions),
            "quantity" => LanguageModelTools.RefusalOf<Quantity>(json, McpJsonUtilities.DefaultOptions),
            "customer" => LanguageModelTools.RefusalOf<CustomerId>(json, McpJsonUtilities.DefaultOptions),
            "country" => LanguageModelTools.RefusalOf<CountryCode>(json, McpJsonUtilities.DefaultOptions),
            _ => LanguageModelTools.RefusalOf<BirthDate?>(json, McpJsonUtilities.DefaultOptions),
        };
        refusal.ErrorCode.Should().Be(code);
        ShouldBeRefused(result, argument, code, LanguageModelTools.AnsweredMessage(refusal));
        if (json is not ("true" or "null"))
        {
            TextOf(result).Should().NotContain(json.Trim('"'));
            result.StructuredContent!.Value.GetRawText().Should().NotContain(json.Trim('"'));
        }

        harness.Log.Created.Should().Be(0);
    }

    /// <summary>
    /// An argument absent, for a parameter with no default, or a call that sends no arguments at all, is refused as
    /// required: the SDK would fail the call; a JSON <c>null</c> is refused by the converter, as required too.
    /// </summary>
    [Fact]
    public async Task An_absent_or_null_argument_is_required()
    {
        await using var harness = await StartAsync(static server => server.WithValueObjectTools<OrderTools>());

        ShouldBeRefused(await harness.CallAsync("place_order", Order("quantity", null)), "quantity", ValueObjectErrorCodes.Required, "A value is required.");
        ShouldBeRefused(await harness.CallAsync("place_order", null), "iban", ValueObjectErrorCodes.Required, "A value is required.");
        ShouldBeRefused(await harness.CallAsync("place_order", "{}"), "iban", ValueObjectErrorCodes.Required, "A value is required.");
        ShouldBeRefused(
            await harness.CallAsync("place_order", Order("country", "null")),
            "country",
            ValueObjectErrorCodes.Required,
            LanguageModelTools.RefusalOf<CountryCode>("null", McpJsonUtilities.DefaultOptions).Message);
    }

    /// <summary>
    /// A nullable value object given <c>null</c>, an absent argument with a default, and a number sent as text, which
    /// the SDK's options read, pass; the tool's result is the one it returns without the package.
    /// </summary>
    [Fact]
    public async Task Accepted_arguments_reach_the_tool_as_without_the_package()
    {
        await using var checkedServer = await StartAsync(static server => server.WithValueObjectTools<OrderTools>());
        await using var plain = await StartAsync(static server => server.WithTools<OrderTools>());

        foreach (var (tool, arguments) in new (string, string?)[]
                 {
                     ("place_order", OrderTools.ValidOrder),
                     ("place_order", Order("delivery", "null")),
                     ("place_order", Order("delivery", "\"1990-01-01\"")),
                     ("place_order", Order("quantity", "\"8\"")),
                     ("get_order", null),
                     ("get_iban", """{"quantity":3}"""),
                     ("settle", """{"balance":"12"}"""),
                     ("reference", """{"reference":"po-7"}"""),
                     ("book", """{"accounts":["FR7630006000011234567890189"],"line":{"account":"FR7630006000011234567890189","quantity":1,"count":2}}"""),
                 })
        {
            var expected = await plain.CallAsync(tool, arguments);
            var result = await checkedServer.CallAsync(tool, arguments);

            result.IsError.Should().NotBe(true, tool);
            TextOf(result).Should().Be(TextOf(expected));
            result.StructuredContent?.GetRawText().Should().Be(expected.StructuredContent?.GetRawText());
        }

        TextOf(await checkedServer.CallAsync("place_order", Order("quantity", "\"8\""))).Should().StartWith("placed 8 on FR76");
        (await checkedServer.CallAsync("get_order", null)).StructuredContent!.Value.GetProperty("iban").GetString().Should().Be("FR7630006000011234567890189");
    }

    /// <summary>
    /// Validation fails fast: of two refused arguments, the first in the order of the parameters is answered.
    /// </summary>
    [Fact]
    public async Task Of_two_refused_arguments_the_first_in_parameter_order_is_answered()
    {
        await using var harness = await StartAsync(static server => server.WithValueObjectTools<OrderTools>());
        var order = JsonNode.Parse(OrderTools.ValidOrder)!.AsObject();
        order["country"] = "XX";
        order["quantity"] = 1001;

        var result = await harness.CallAsync("place_order", order.ToJsonString());

        ShouldBeRefused(result, "quantity", ValueObjectErrorCodes.OutOfRange, "The value is not a valid Quantity: The value must be less than or equal to 1000.");
    }

    /// <summary>
    /// An argument is refused exactly when the SDK's binding fails, the form the value object travels in, a number read
    /// from text where the options allow it, decided by the tool's own serializer options, not by the underlying type.
    /// </summary>
    /// <param name="general">Whether the tools are registered with options under the general defaults, which read no
    /// number from text.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_argument_is_refused_exactly_when_the_SDK_binding_fails(bool general)
    {
        var options = general
            ? new JsonSerializerOptions(JsonSerializerDefaults.General) { TypeInfoResolver = new DefaultJsonTypeInfoResolver(), Converters = { new ValueObjectJsonConverterFactory() } }
            : null;
        await using var checkedServer = await StartAsync(server => server.WithValueObjectTools<OrderTools>(options));
        await using var plain = await StartAsync(server => server.WithTools<OrderTools>(options));

        foreach (var (tool, argument, json) in new (string, string, string)[]
                 {
                     ("get_iban", "quantity", "8"), ("get_iban", "quantity", "\"8\""), ("get_iban", "quantity", "1001"),
                     ("get_iban", "quantity", "\"1001\""), ("get_iban", "quantity", "\" 7\""), ("get_iban", "quantity", "3.5"),
                     ("settle", "balance", "\"12\""), ("settle", "balance", "12"), ("settle", "balance", "\"1000000000000000000001\""),
                     ("settle", "balance", "\"twelve\""),
                 })
        {
            var arguments = new JsonObject { [argument] = JsonNode.Parse(json) }.ToJsonString();
            var bound = (await plain.CallAsync(tool, arguments)).IsError != true;

            var result = await checkedServer.CallAsync(tool, arguments);

            (result.IsError != true).Should().Be(bound, "{0} {1} is {2} by the binding", tool, arguments, bound ? "taken" : "refused");
            if (!bound)
            {
                TextOf(result).Should().StartWith($"Argument '{argument}' rejected (value_object.");
            }
        }
    }

    /// <summary>
    /// The structured content carries the refusal only for a tool that declares no output schema, which it would have to
    /// conform to; the sentence always carries the code.
    /// </summary>
    [Fact]
    public async Task A_tool_that_declares_an_output_schema_gets_the_refusal_in_its_text_alone()
    {
        await using var harness = await StartAsync(static server => server.WithValueObjectTools<OrderTools>());
        const string Message = "The value is not a valid Quantity: The value must be less than or equal to 1000.";

        ShouldBeRefused(await harness.CallAsync("get_iban", """{"quantity":1001}"""), "quantity", ValueObjectErrorCodes.OutOfRange, Message, structured: false);
        ShouldBeRefused(await harness.CallAsync("describe_order", """{"quantity":1001}"""), "quantity", ValueObjectErrorCodes.OutOfRange, Message, structured: false);
        ShouldBeRefused(await harness.CallAsync("who", """{"quantity":1001}"""), "quantity", ValueObjectErrorCodes.OutOfRange, Message);
    }

    /// <summary>
    /// A value object held by a list or an object is answered under the parameter's name; any other error in an object is
    /// left to the SDK, which answers as it would without the package.
    /// </summary>
    [Fact]
    public async Task A_value_object_held_by_a_list_or_an_object_is_answered_under_the_parameter()
    {
        await using var harness = await StartAsync(static server => server.WithValueObjectTools<OrderTools>());
        var order = JsonSerializer.Serialize(OrderTools.Order, McpJsonUtilities.DefaultOptions).Replace("\"quantity\":3", "\"quantity\":1001", StringComparison.Ordinal);

        ShouldBeRefused(
            await harness.CallAsync("book", """{"accounts":["FR7630006000011234567890189","FR76"]}"""),
            "accounts",
            ValueObjectErrorCodes.TooShort,
            "The value is not a valid Iban: The value must be at least 15 characters long.");
        ShouldBeRefused(
            await harness.CallAsync("book", $$"""{"accounts":[],"order":{{order}}}"""),
            "order",
            ValueObjectErrorCodes.OutOfRange,
            "The value is not a valid Quantity: The value must be less than or equal to 1000.");

        var other = await harness.CallAsync("book", """{"accounts":[],"line":{"account":"FR7630006000011234567890189","quantity":1,"count":"two"}}""");
        other.IsError.Should().BeTrue();
        TextOf(other).Should().Be("An error occurred invoking 'book'.");
    }

    /// <summary>
    /// The parameters the SDK binds itself, the server, the request context, a progress reporter, a service and the
    /// cancellation token, are never read; the value object beside them is.
    /// </summary>
    [Fact]
    public async Task Parameters_the_SDK_binds_itself_are_never_read()
    {
        await using var harness = await StartAsync(static server => server.WithValueObjectTools<OrderTools>());

        var result = await harness.CallAsync("who", """{"quantity":5}""");

        result.IsError.Should().NotBe(true);
        TextOf(result).Should().Be("5 for who, 1 created, bound True");
    }

    /// <summary>
    /// An instance of an instance tool's type is created for each accepted call, and disposed after it, as the SDK
    /// creates one, even when the type is a service the server could resolve; a refused call creates none. The type is
    /// registered as a type argument or in a list of types.
    /// </summary>
    /// <param name="byType">Whether the type is registered in a list of types.</param>
    /// <param name="asService">Whether the tool type is also registered as a singleton among the server's services,
    /// which the SDK never takes the instance of a call from.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task An_instance_is_created_for_each_accepted_call_and_none_for_a_refused_one(bool byType, bool asService)
    {
        await using var harness = await StartAsync(
            server => _ = byType ? server.WithValueObjectTools([typeof(OrderTools)]) : server.WithValueObjectTools<OrderTools>(),
            services: asService ? static services => services.AddSingleton<OrderTools>() : null);

        foreach (var arguments in new[] { OrderTools.ValidOrder, Order("quantity", "1001"), OrderTools.ValidOrder, Order("iban", "42") })
        {
            _ = await harness.CallAsync("place_order", arguments);
        }

        harness.Log.Created.Should().Be(2);
        harness.Log.Disposed.Should().Be(2);
    }

    /// <summary>
    /// Only the tools created with the rules are checked: a tool without a value object, and a tool of the same type the
    /// SDK registered beside them, answer as without the package; a tool the server does not have is the SDK's protocol
    /// error.
    /// </summary>
    [Fact]
    public async Task Only_tools_created_with_the_rules_are_checked()
    {
        await using var harness = await StartAsync(static server => server
            .WithValueObjectTools([typeof(StaticTools), typeof(PlainTools)])
            .WithTools([McpServerTool.Create(typeof(OrderTools).GetMethod(nameof(OrderTools.Settle))!)]));

        TextOf(await harness.CallAsync("echo", """{"text":"hi","count":2}""")).Should().Be("hi 2");
        (await harness.CallAsync("echo", """{"text":"hi","count":"two"}""")).IsError.Should().BeTrue();
        TextOf(await harness.CallAsync("settle", """{"balance":"1000000000000000000001"}""")).Should().Be("An error occurred invoking 'settle'.");
        ShouldBeRefused(
            await harness.CallAsync("ship", """{"quantity":1001,"country":"FR"}"""),
            "quantity",
            ValueObjectErrorCodes.OutOfRange,
            "The value is not a valid Quantity: The value must be less than or equal to 1000.");

        await FluentActions.Awaiting(() => harness.CallAsync("unknown", "{}")).Should().ThrowAsync<McpProtocolException>();
    }

    /// <summary>
    /// A converter of the application's own, which refuses a value with a plain exception, is answered with
    /// <c>not_parsable</c> and a message naming the value object alone, as the AI package answers it: the arguments are
    /// read through the serializer options a registration is given, and through those a tool created by hand holds.
    /// </summary>
    [Fact]
    public async Task A_converter_of_the_application_s_own_is_answered_with_not_parsable()
    {
        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
        options.Converters.Insert(0, new RefusingQuantityConverter());
        var byHand = ValueObjectMcpServerTool.Create(
            typeof(StaticTools).GetMethod(nameof(StaticTools.Ship))!,
            options: new McpServerToolCreateOptions { Name = "ship_by_hand", SerializerOptions = options });
        await using var harness = await StartAsync(server => server.WithValueObjectTools([typeof(StaticTools)], options).WithValueObjectTools([byHand]));

        foreach (var tool in (string[])["ship", "ship_by_hand"])
        {
            ShouldBeRefused(
                await harness.CallAsync(tool, """{"quantity":3,"country":"FR"}"""),
                "quantity",
                ValueObjectErrorCodes.NotParsable,
                "The value is not a valid Quantity.");
        }
    }

    /// <summary>
    /// A tool whose structured content holds a default instance, which the generated writer refuses, gets the SDK's bare
    /// error: the package checks arguments, never results.
    /// </summary>
    [Fact]
    public async Task A_default_instance_in_a_result_gets_the_SDK_error()
    {
#pragma warning disable VO0010 // The default instance the generated writer refuses, built on purpose.
        var unset = default(Iban);
#pragma warning restore VO0010
        var tool = McpServerTool.Create(() => unset, new McpServerToolCreateOptions { Name = "unset", UseStructuredContent = true });
        await using var harness = await StartAsync(server => server.WithValueObjectTools([tool]));

        var result = await harness.CallAsync("unset", null);

        result.IsError.Should().BeTrue();
        TextOf(result).Should().Be("An error occurred invoking 'unset'.");
    }

    /// <summary>Replaces, or with <see langword="null"/> removes, one argument of <see cref="OrderTools.ValidOrder"/>.</summary>
    private static string Order(string name, string? json)
    {
        var order = JsonNode.Parse(OrderTools.ValidOrder)!.AsObject();
        if (json is null)
        {
            order.Remove(name);
        }
        else
        {
            order[name] = JsonNode.Parse(json);
        }

        return order.ToJsonString();
    }

    /// <summary>A converter of the application's own that refuses every value with a plain exception.</summary>
    private sealed class RefusingQuantityConverter : JsonConverter<Quantity>
    {
        public override Quantity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new JsonException("Refused by the application.");

        public override void Write(Utf8JsonWriter writer, Quantity value, JsonSerializerOptions options)
            => writer.WriteNumberValue(value.Value);
    }
}
