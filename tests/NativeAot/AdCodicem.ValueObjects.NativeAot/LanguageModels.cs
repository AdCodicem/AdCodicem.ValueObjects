using System.ComponentModel;
using System.Text.Json;
using AdCodicem.ValueObjects.AI;
using AdCodicem.ValueObjects.Metadata;
using Microsoft.Extensions.AI;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// Microsoft.Extensions.AI tools and structured output: every value object the registry holds described in a tool's
/// schema with its rules, tools whose refused arguments are answered with their rule, and the response format of a
/// structured output, all over the source-generated context, since reflection-based serialization is off.
/// </summary>
/// <remarks>
/// <c>AIFunctionFactory</c> reads the method of a tool at run time, through the delegate it is handed; a method group, not
/// a lambda closed over a type parameter, so that nothing the native binary lacks is asked for.
/// </remarks>
internal static class LanguageModels
{
    private static readonly AIFunctionFactoryOptions Factory = new()
    {
        SerializerOptions = AppJsonContext.Default.Options,
        JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions().WithValueObjects(),
    };

    private const string ValidOrder =
        """{"email":"ada@example.com","quantity":3,"customer":"6f9619ff-8b86-d011-b42d-00c04fc964ff","rate":5.5}""";

    /// <param name="report">Where each scenario is written.</param>
    /// <returns>The scenarios, run.</returns>
    public static async Task RunAsync(Report report)
    {
        foreach (var descriptor in ValueObjectRegistry.GetRegistered().OrderBy(d => Names.Of(d.ValueObjectType), StringComparer.Ordinal))
        {
            var schema = AIJsonUtilities.CreateJsonSchema(
                descriptor.ValueObjectType,
                serializerOptions: AppJsonContext.Default.Options,
                inferenceOptions: Factory.JsonSchemaCreateOptions);
            report.Line($"ai schema {Names.Of(descriptor.ValueObjectType)}", schema.GetRawText());
        }

        // Microsoft.Extensions.AI's own options know no value object, and reflection cannot make up for it.
        try
        {
            _ = AIFunctionFactory.Create(Tools.PlaceOrder);
            report.Line("ai default options", "created");
        }
        catch (NotSupportedException exception)
        {
            report.Line("ai default options", Report.Describe(exception));
        }

        var placeOrder = AIFunctionFactory.Create(Tools.PlaceOrder, Factory).WithValueObjectValidation();
        report.Line("ai tool PlaceOrder", placeOrder.JsonSchema.GetRawText());
        foreach (var (name, json) in new (string, string?)[]
                 {
                     ("valid", ValidOrder),
                     ("quantity", "0"), ("quantity", "1001"), ("quantity", "\"7\""), ("quantity", "true"), ("quantity", "null"), ("quantity", null),
                     ("email", "\"ada\""), ("email", "42"), ("customer", "\"bad\""), ("customer", "\"00000000-0000-0000-0000-000000000000\""),
                     ("rate", "7"), ("opens", "\"08:30:00\""), ("opens", "null"),
                 })
        {
            report.Line($"ai call PlaceOrder {name} {json ?? "absent"}", await InvokeAsync(placeOrder, name == "valid" ? ValidOrder : With(name, json)));
        }

        // A host may hand an argument over as text or as a number, which the binding converts rather than refuses.
        foreach (var (name, value) in new (string, object)[]
                 {
                     ("quantity", "7"), ("quantity", "1001"), ("quantity", "abc"), ("quantity", 1001),
                     ("email", "ada"), ("customer", "bad"), ("rate", "7"),
                 })
        {
            report.Line($"ai call PlaceOrder {name} as {value.GetType().Name} {value}", await InvokeAsync(placeOrder, ValidOrder, (name, value)));
        }

        var book = AIFunctionFactory.Create(Tools.Book, Factory).WithValueObjectValidation();
        report.Line("ai tool Book", book.JsonSchema.GetRawText());
        foreach (var arguments in (string[])
                 [
                     """{"addresses":["ada@example.com"],"number":"po-7","balance":"12"}""",
                     """{"addresses":["ada@example.com","ada"],"number":"po-7","balance":"12"}""",
                     """{"addresses":[],"number":"PO-1234567890123","balance":"12"}""",
                     """{"addresses":[],"number":"po-7","balance":12}""",
                 ])
        {
            report.Line($"ai call Book {arguments}", await InvokeAsync(book, arguments));
        }

        // A value object nothing registered, which the converter factory refuses without dynamic code.
        try
        {
            _ = AIFunctionFactory.Create(Tools.Unregistered, Factory);
            report.Line("ai tool Unregistered", "created");
        }
        catch (NotSupportedException exception)
        {
            report.Line("ai tool Unregistered", Report.Describe(exception));
        }

        // The answer an application asks a model for, read back through the same context: Scenarios' records read it.
        var format = ValueObjectResponseFormat.ForJsonSchema<Order>(AppJsonContext.Default.Options);
        report.Line("ai response format", $"{format.SchemaName}: {format.Schema!.Value.GetRawText()}");
    }

    private static string With(string name, string? json)
    {
        var order = System.Text.Json.Nodes.JsonNode.Parse(ValidOrder)!.AsObject();
        if (json is null)
        {
            order.Remove(name);
        }
        else
        {
            order[name] = System.Text.Json.Nodes.JsonNode.Parse(json);
        }

        return order.ToJsonString();
    }

    /// <summary>
    /// Calls a tool as a model's call reaches it: each argument a <see cref="JsonElement"/>, a JSON null a null, but the one
    /// handed over as it is.
    /// </summary>
    private static async Task<string> InvokeAsync(AIFunction tool, string json, (string Name, object Value)? replaced = null)
    {
        using var document = JsonDocument.Parse(json);
        var arguments = new AIFunctionArguments();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            arguments[property.Name] = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.Clone();
        }

        if (replaced is { } argument)
        {
            arguments[argument.Name] = argument.Value;
        }

        try
        {
            return await tool.InvokeAsync(arguments) is JsonElement result ? result.GetRawText() : "no result";
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return $"threw {Report.Describe(exception)}";
        }
    }

    /// <summary>The methods turned into tools.</summary>
    private static class Tools
    {
        [Description("Places an order.")]
        public static string PlaceOrder(
            [Description("The address to confirm the order to.")] EmailAddress email,
            Quantity quantity,
            CustomerId customer,
            VatRate rate,
            OpeningTime? opens = null)
            => $"placed {quantity} for {email} ({customer}) at {rate} %, opens {opens?.ToString() ?? "now"}";

        public static string Book(List<EmailAddress> addresses, DocumentNumber<PurchaseOrder> number, LedgerBalance balance)
            => $"booked {number} for {addresses.Count} at {balance}";

        public static string Unregistered(UnregisteredCode code) => code.Value;
    }
}
