using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.ModelContextProtocol;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static AdCodicem.ValueObjects.UnitTests.LanguageModels.ToolSchemaTests;

namespace AdCodicem.ValueObjects.UnitTests.LanguageModels;

/// <summary>
/// <c>WithValueObjectTools</c> and <c>ValueObjectMcpServerTool.Create</c>: the schemas a Model Context Protocol server
/// lists for a tool describe each value object it takes or returns with the rules declared on it, as the JSON Schema
/// core describes it for a language model.
/// </summary>
public sealed class McpToolSchemaTests
{
    /// <summary>The protocol version of the clients in use today, before the one that takes any output schema.</summary>
    internal const string LegacyProtocol = "2025-11-25";

    /// <summary>
    /// Every value object of the domain, over each of the underlying types, is described in a tool's input schema alone,
    /// nullable and in a list, and an argument it refuses is answered with its rule.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public async Task Every_value_object_is_described_in_an_MCP_tool_schema_and_a_refused_argument_answered_with_its_rule(string type)
        => await Samples.All[type].AnswersARefusedMcpToolArgumentWithItsRuleAsync();

    /// <summary>
    /// Without the package, the SDK describes a value-object parameter as <c>true</c>, keeping only the description a
    /// <c>[Description]</c> gives it, and a value object in structured content as <c>true</c> too, a list of them as a
    /// list of anything.
    /// </summary>
    [Fact]
    public async Task Without_the_package_a_value_object_is_described_as_anything()
    {
        await using var harness = await McpHarness.StartAsync(static server => server.WithTools<OrderTools>());
        var tools = await harness.ListAsync();

        var input = Parse(tools["place_order"].InputSchema)["properties"]!;
        ShouldEqual(input["iban"]!, JsonNode.Parse("""{"description":"The account to debit."}""")!, "iban");
        input["quantity"]!.GetValueKind().Should().Be(JsonValueKind.True);
        input["country"]!.GetValueKind().Should().Be(JsonValueKind.True);

        var output = Parse(tools["get_order"].OutputSchema!.Value)["properties"]!;
        output["iban"]!.GetValueKind().Should().Be(JsonValueKind.True);
        ShouldEqual(output["alternates"]!, JsonNode.Parse("""{"type":"array","items":{}}""")!, "alternates");
    }

    /// <summary>
    /// Each value-object parameter is described as the core describes it under the language-model profile, the
    /// description a <c>[Description]</c> gives it first, a nullable one with its default; only the parameters without a
    /// default are required.
    /// </summary>
    [Fact]
    public async Task A_value_object_parameter_is_described_as_the_JSON_Schema_core_describes_it_for_a_language_model()
    {
        await using var harness = await McpHarness.StartAsync(static server => server.WithValueObjectTools<OrderTools>());
        var tools = await harness.ListAsync();

        var schema = Parse(tools["place_order"].InputSchema);
        var properties = schema["properties"]!;
        ShouldEqual(properties["quantity"]!, McpCore(typeof(Quantity)), "quantity");
        ShouldEqual(properties["customer"]!, McpCore(typeof(CustomerId)), "customer");
        ShouldEqual(properties["country"]!, McpCore(typeof(CountryCode)), "country");

        var iban = McpCore(typeof(Iban)).AsObject();
        iban["description"] = $"The account to debit.\n\n{iban["description"]!.GetValue<string>()}";
        ShouldEqual(properties["iban"]!, iban, "iban");

        var delivery = McpCore(typeof(BirthDate?)).AsObject();
        delivery["default"] = null;
        ShouldEqual(properties["delivery"]!, delivery, "delivery");

        schema["required"]!.AsArray().Select(static name => name!.GetValue<string>()).Should().Equal("iban", "quantity", "customer", "country");

        ShouldEqual(Parse(tools["settle"].InputSchema)["properties"]!["balance"]!, McpCore(typeof(LedgerBalance)), "balance");
        ShouldEqual(Parse(tools["reference"].InputSchema)["properties"]!["reference"]!, McpCore(typeof(Reference<PurchaseOrder>)), "reference");
        var book = Parse(tools["book"].InputSchema)["properties"]!;
        ShouldEqual(book["accounts"]!["items"]!, McpCore(typeof(Iban)), "accounts");
        ShouldEqual(book["order"]!["properties"]!["quantity"]!, McpCore(typeof(Quantity)), "order");
    }

    /// <summary>
    /// The output schema of a tool returning structured content describes each value object of its result as the core
    /// describes it, a list's items included, whether the type is the method's or the one its attribute declares.
    /// </summary>
    [Fact]
    public async Task A_value_object_in_structured_content_is_described_as_the_JSON_Schema_core_describes_it()
    {
        await using var harness = await McpHarness.StartAsync(static server => server.WithValueObjectTools<OrderTools>());
        var tools = await harness.ListAsync();

        var order = McpCore(typeof(PlacedOrder));
        foreach (var name in (string[])["get_order", "describe_order"])
        {
            var properties = Parse(tools[name].OutputSchema!.Value)["properties"]!;
            foreach (var (member, _) in order["properties"]!.AsObject())
            {
                ShouldEqual(properties[member]!, order["properties"]![member]!, $"{name}.{member}");
            }

            ShouldEqual(properties["alternates"]!["items"]!, McpCore(typeof(Iban)), name);
        }

        ShouldEqual(Parse(tools["get_iban"].OutputSchema!.Value), McpCore(typeof(Iban)), "get_iban");
        tools["place_order"].OutputSchema.Should().BeNull();
    }

    /// <summary>
    /// The tools stay the SDK's own, which a client on a protocol version before <c>2026-07-28</c> gets a value object's
    /// output schema from wrapped in an object, as the specification asks, its rules kept: the structured content is
    /// wrapped likewise. A tool wrapped in another would publish the bare schema beside wrapped content.
    /// </summary>
    [Fact]
    public async Task A_client_on_an_older_protocol_gets_the_output_schema_the_SDK_wraps_for_it()
    {
        await using var legacy = await McpHarness.StartAsync(static server => server.WithValueObjectTools<OrderTools>(), LegacyProtocol);
        await using var latest = await McpHarness.StartAsync(static server => server.WithValueObjectTools<OrderTools>());

        legacy.Client.NegotiatedProtocolVersion.Should().Be(LegacyProtocol);
        ShouldEqual(
            Parse((await legacy.ListAsync())["get_iban"].OutputSchema!.Value),
            new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["result"] = McpCore(typeof(Iban)) }, ["required"] = new JsonArray("result") },
            "get_iban");
        (await legacy.CallAsync("get_iban", """{"quantity":3}""")).StructuredContent!.Value.GetRawText()
            .Should().Be("""{"result":"FR7630006000011234567890189"}""");

        ShouldEqual(Parse((await latest.ListAsync())["get_iban"].OutputSchema!.Value), McpCore(typeof(Iban)), "get_iban");
        (await latest.CallAsync("get_iban", """{"quantity":3}""")).StructuredContent!.Value.GetRawText()
            .Should().Be("\"FR7630006000011234567890189\"");
    }

    /// <summary>
    /// Each registration describes the same tools the same way, the methods the SDK's own registration finds, one
    /// that is not public included: by type argument, by type, from an assembly given or the calling one, which finds
    /// its one tool type, and tools created one by one.
    /// </summary>
    [Fact]
    public async Task Every_registration_gives_the_same_schemas()
    {
        var expected = await InputSchemasAsync(static server => server.WithValueObjectTools<OrderTools>());
        (await InputSchemasAsync(static server => server.WithTools<OrderTools>())).Keys.Should().Equal(expected.Keys).And.Contain("count", "a method that is not public is a tool too");

        (await InputSchemasAsync(static server => server.WithValueObjectTools([typeof(OrderTools), null!]))).Should().Equal(expected);
        // Created by hand with no services, a tool lists a service its method takes as an argument, as the SDK's does.
        (await InputSchemasAsync(static server => server.WithValueObjectTools(
                typeof(OrderTools).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Where(static method => method.GetCustomAttribute<McpServerToolAttribute>() is not null && method.Name != nameof(OrderTools.Who))
                    .Select(static method => ValueObjectMcpServerTool.Create(method, method.IsStatic ? null : new OrderTools(new InvocationLog()))))))
            .Should().Equal(expected.Where(static tool => tool.Key != "who"));

        var catalog = await InputSchemasAsync(static server => server.WithValueObjectTools([typeof(CatalogTools)]));
        catalog.Keys.Should().Equal("catalog_country");
        (await InputSchemasAsync(static server => server.WithValueObjectToolsFromAssembly(typeof(CatalogTools).Assembly))).Should().Equal(catalog);
        (await InputSchemasAsync(static server => server.WithValueObjectToolsFromAssembly())).Should().Equal(catalog);
        Parse(JsonDocument.Parse(catalog["catalog_country"]).RootElement)["properties"]!["country"]!["enum"]!.ToJsonString().Should().Be("""["FR","BE","LU"]""");

        var statics = await InputSchemasAsync(static server => server.WithValueObjectTools([typeof(StaticTools)]));
        statics.Keys.Should().Equal("ship");
        ShouldEqual(Parse(JsonDocument.Parse(statics["ship"]).RootElement)["properties"]!["quantity"]!, McpCore(typeof(Quantity)), "ship");
    }

    /// <summary>
    /// The options a tool is created with reach the tool, every one of them: a transform its schema options carry runs
    /// first and is completed, and neither the options nor their schema options are changed.
    /// </summary>
    [Fact]
    public void A_tool_created_with_options_keeps_them_and_runs_their_transform_first()
    {
        Func<AIJsonSchemaCreateContext, JsonNode, JsonNode> transform = static (context, schema) =>
            context.TypeInfo.Type == typeof(Quantity) ? new JsonObject { ["x-host"] = schema.GetValueKind() == JsonValueKind.True } : schema;
        var schemaOptions = new AIJsonSchemaCreateOptions { TransformSchemaNode = transform };
        var options = new McpServerToolCreateOptions
        {
            Name = "ship_it",
            Title = "Ship it",
            Description = "Ships an order.",
            Destructive = true,
            Idempotent = false,
            OpenWorld = true,
            ReadOnly = false,
            Icons = [new Icon { Source = "https://example.com/ship.png" }],
            SchemaCreateOptions = schemaOptions,
        };

        var tool = ValueObjectMcpServerTool.Create(typeof(StaticTools).GetMethod(nameof(StaticTools.Ship))!, options: options).ProtocolTool;

        tool.Name.Should().Be("ship_it");
        tool.Title.Should().Be("Ship it");
        tool.Description.Should().Be("Ships an order.");
        tool.Annotations!.DestructiveHint.Should().BeTrue();
        tool.Annotations.IdempotentHint.Should().BeFalse();
        tool.Annotations.OpenWorldHint.Should().BeTrue();
        tool.Annotations.ReadOnlyHint.Should().BeFalse();
        tool.Icons.Should().BeSameAs(options.Icons);
        var quantity = Parse(tool.InputSchema)["properties"]!["quantity"]!;
        quantity["x-host"]!.GetValue<bool>().Should().BeTrue();
        quantity["maximum"]!.GetValue<int>().Should().Be(1000);

        options.SchemaCreateOptions.Should().BeSameAs(schemaOptions);
        schemaOptions.TransformSchemaNode.Should().BeSameAs(transform);
    }

    /// <summary>
    /// A tool created without options is described with the rules too, under the SDK's own serializer options.
    /// </summary>
    [Fact]
    public void A_tool_created_without_options_is_described_with_the_rules()
    {
        var tool = ValueObjectMcpServerTool.Create(typeof(StaticTools).GetMethod(nameof(StaticTools.Ship))!).ProtocolTool;

        tool.Name.Should().Be("ship");
        ShouldEqual(Parse(tool.InputSchema)["properties"]!["country"]!, McpCore(typeof(CountryCode)), "country");
    }

    /// <summary>
    /// The copy of the options the tool is created with holds every public property the SDK's options have, each as it
    /// was given but the schema options: a property the SDK adds and the copy drops fails here.
    /// </summary>
    [Fact]
    public void The_options_are_copied_property_by_property()
    {
        var given = new McpServerToolCreateOptions();
        var properties = typeof(McpServerToolCreateOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            property.SetValue(given, Distinct(property.PropertyType));
        }

        var copy = ValueObjectMcpServerTool.Describe(given);

        foreach (var property in properties.Where(static property => property.Name != nameof(McpServerToolCreateOptions.SchemaCreateOptions)))
        {
            property.GetValue(copy).Should().Be(property.GetValue(given), property.Name);
        }

        copy.SchemaCreateOptions.Should().NotBeSameAs(given.SchemaCreateOptions);
        copy.SchemaCreateOptions!.TransformSchemaNode.Should().NotBeNull();
        (copy.SchemaCreateOptions with { TransformSchemaNode = given.SchemaCreateOptions!.TransformSchemaNode }).Should().Be(given.SchemaCreateOptions);
        ValueObjectMcpServerTool.Describe(null).SchemaCreateOptions!.TransformSchemaNode.Should().NotBeNull();
    }

    /// <summary>
    /// Serializer options built for a server without reflection, a copy of the SDK's whose resolver chain starts with a
    /// source-generated context listing the value objects, and whose converters hold the converter factory, give the
    /// schemas reflection gives. A context resolving for options other than its own applies the converters of those
    /// options alone, not the ones its attribute names.
    /// </summary>
    [Fact]
    public async Task Serializer_options_from_a_source_generated_context_give_the_schemas_reflection_gives()
    {
        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
        options.TypeInfoResolverChain.Insert(0, McpToolContext.Default);
        options.Converters.Add(new ValueObjectJsonConverterFactory());

        var expected = await InputSchemasAsync(static server => server.WithValueObjectTools<OrderTools>());

        (await InputSchemasAsync(server => server.WithValueObjectTools<OrderTools>(options))).Should().Equal(expected);
    }

    /// <summary>Describes a type as the JSON Schema core does for a language model, under the SDK's serializer options.</summary>
    internal static JsonNode McpCore(Type type)
        => JsonSchemaExporter.GetJsonSchemaAsNode(
            McpJsonUtilities.DefaultOptions,
            type,
            new JsonSchemaExporterOptions
            {
                TransformSchemaNode = ValueObjectJsonSchema.CreateTransform(ValueObjectJsonSchemaProfile.LanguageModel),
                TreatNullObliviousAsNonNullable = true,
            });

    private static async Task<IReadOnlyDictionary<string, string>> InputSchemasAsync(Action<IMcpServerBuilder> configure)
    {
        await using var harness = await McpHarness.StartAsync(configure);
        return (await harness.ListAsync()).OrderBy(static tool => tool.Key, StringComparer.Ordinal)
            .ToDictionary(static tool => tool.Key, static tool => tool.Value.InputSchema.GetRawText());
    }

    /// <summary>Makes a value of a property type no other property shares, and that no default equals.</summary>
    private static object Distinct(Type type)
        => type switch
        {
            _ when type == typeof(IServiceProvider) => new ServiceCollection().BuildServiceProvider(),
            _ when type == typeof(string) => Guid.NewGuid().ToString(),
            _ when type == typeof(bool?) => true,
            _ when type == typeof(bool) => true,
            _ when type == typeof(JsonElement?) => JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone(),
            _ when type == typeof(JsonSerializerOptions) => new JsonSerializerOptions(),
            _ when type == typeof(AIJsonSchemaCreateOptions) => new AIJsonSchemaCreateOptions { IncludeSchemaKeyword = true },
            _ when type == typeof(IReadOnlyList<object>) => new object[] { new object() },
            _ when type == typeof(IList<Icon>) => new List<Icon> { new() { Source = "https://example.com/icon.png" } },
            _ when type == typeof(JsonObject) => new JsonObject { ["x"] = 1 },
            _ => throw new NotSupportedException($"No distinct value for {type}: the SDK's options gained a property the copy may drop."),
        };
}
