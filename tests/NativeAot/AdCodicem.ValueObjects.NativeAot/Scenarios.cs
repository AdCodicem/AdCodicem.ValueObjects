using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// The scenarios that run in process: the registry and every value object it describes, the identifiers,
/// FluentValidation, and JSON through the source-generated context.
/// </summary>
internal static class Scenarios
{
    public static void Run(Report report)
    {
        // Minted first: the strings each value object over text is probed with include it.
        var minted = OrderId.New();

        Registry(report);
        foreach (var descriptor in ValueObjectRegistry.GetRegistered().OrderBy(d => Names.Of(d.ValueObjectType), StringComparer.Ordinal))
        {
            string[] extra = descriptor.ValueType == typeof(string) ? [minted.Value, Mangle(minted.Value), $"zzz{minted.Value[3..]}"] : [];
            descriptor.Accept(new ValueObjectProbe(descriptor, report, extra));
        }

        Identifiers(report, minted);
        Validation.Run(report);
        Records(report);
        Unregistered(report);
    }

    private static void Registry(Report report)
    {
        var registered = ValueObjectRegistry.GetRegistered().Select(d => Names.Of(d.ValueObjectType)).Order(StringComparer.Ordinal);
        report.Line("registry", $"{ValueObjectRegistry.GetRegistered().Count} value objects: {string.Join(", ", registered)}");
        var definitions = ValueObjectRegistry.GetRegisteredGenericDefinitions().Select(Names.Of).Order(StringComparer.Ordinal);
        report.Line("registry", $"generic definitions: {string.Join(", ", definitions)}");
        report.Line("registry", $"Quantity is a value object over {Names.Of(ValueObjectRegistry.GetUnderlyingType(typeof(Quantity))!)}: {ValueObjectRegistry.IsValueObject(typeof(Quantity))}");
        report.Line("registry", $"Order is a value object: {ValueObjectRegistry.IsValueObject(typeof(Order))}");
        report.Line("registry", $"TryGet(DocumentNumber<PurchaseOrder>): {ValueObjectRegistry.TryGet(typeof(DocumentNumber<PurchaseOrder>), out var number)}, schema max length {number?.Schema.MaxLength}");
    }

    private static void Identifiers(Report report, OrderId minted)
    {
        var next = OrderId.New();
        report.Line("identifiers", $"minted {minted}, then {next}: ordered {minted.CompareTo(next) < 0}");
        report.Line("identifiers", $"registered prefixes: {string.Join(", ", EntityIdRegistry.GetRegistered().Select(d => $"{d.Prefix} ({Names.Of(d.ValueObjectType)})").Order(StringComparer.Ordinal))}");
        report.Line("identifiers", $"TryGetByPrefix(\"ord\"): {EntityIdRegistry.TryGetByPrefix("ord", out var byPrefix)}, {(byPrefix is null ? "none" : Names.Of(byPrefix.ValueObjectType))}");

        foreach (var text in (string[])[minted.Value, Mangle(minted.Value), $"zzz{minted.Value[3..]}", "ord", ""])
        {
            if (AnyEntityId.TryParse(text, null, out var any, out var validation))
            {
                var converted = any.TryConvertTo<OrderId>(out var id) ? $"converts to {Names.Of(typeof(OrderId))} {id}" : "converts to no OrderId";
                report.Line($"AnyEntityId {Underlying.Quote(text)}", $"prefix {any.Prefix}, {Names.Of(any.ValueObjectType!)}, is OrderId {any.Is<OrderId>()}, {converted}, boxed {Underlying.Show(any.ToValueObject())}");
            }
            else
            {
                report.Line($"AnyEntityId {Underlying.Quote(text)}", $"refused {validation.ErrorCode}");
            }

            var json = JsonSerializer.Serialize(text, AppJsonContext.Default.String);
            try
            {
                report.Line($"AnyEntityId JSON {json}", $"read as {JsonSerializer.Deserialize(json, AppJsonContext.Default.AnyEntityId)}");
            }
            catch (JsonException exception)
            {
                report.Line($"AnyEntityId JSON {json}", $"threw {Report.Describe(exception)}");
            }
        }
    }

    private static void Records(Report report)
    {
        var order = new Order(
            EmailAddress.Create(" Ada@Example.com"),
            Quantity.Create(3),
            Amount.Create(12.345m),
            VatRate.Reduced,
            DocumentStatus.Parse("Final", null),
            CustomerId.Parse("0f8fad5b-d9cb-469f-a165-70867728950e", null),
            DocumentNumber<PurchaseOrder>.Create("po-1042"),
            null,
            OpeningTime.Create(new TimeOnly(9, 30)));
        var json = JsonSerializer.Serialize(order, AppJsonContext.Default.Order);
        report.Line("record Order", $"written {json}");
        report.Line("record Order", $"read back equal: {JsonSerializer.Deserialize(json, AppJsonContext.Default.Order) == order}");

        foreach (var refused in (string[])[
            json.Replace("\"ada@example.com\"", "\"ada\"", StringComparison.Ordinal),
            json.Replace("\"Quantity\":3", "\"Quantity\":0", StringComparison.Ordinal),
            json.Replace("\"Rate\":5.5", "\"Rate\":7", StringComparison.Ordinal),
            json.Replace("\"Opens\":\"09:30:00.0000000\"", "\"Opens\":\"13:00:00\"", StringComparison.Ordinal),
            json.Replace("\"Quantity\":3", "\"Quantity\":null", StringComparison.Ordinal)])
        {
            try
            {
                report.Line("record Order refused", $"accepted {JsonSerializer.Deserialize(refused, AppJsonContext.Default.Order)}");
            }
            catch (JsonException exception)
            {
                report.Line("record Order refused", $"{exception.Path}: threw {Report.Describe(exception)}");
            }
        }

        report.Line("record Page", $"written {JsonSerializer.Serialize(new Page(null), AppJsonContext.Default.Page)} and {JsonSerializer.Serialize(new Page(PageNumber.First), AppJsonContext.Default.Page)}");

        var tally = new Tally(
            new() { [Quantity.Create(4)] = 1 },
            new() { [Consent.Create(true)] = 2 },
            RecordedAt.Create(new DateTime(2024, 1, 31, 8, 30, 0, DateTimeKind.Unspecified)));
        report.Line("record Tally", $"written {JsonSerializer.Serialize(tally, AppJsonContext.Default.Tally)}");

        // The mirror holds a value: System.Text.Json's nullable converter writes and reads a null itself, and hands any
        // other value to the converter the factory gives HandWrittenLink.
        var bookmark = new Bookmark(
            HandWrittenLink.Create(new Uri("https://example.com/a")),
            HandWrittenLink.Create(new Uri("https://example.com/m")),
            new() { [HandWrittenCode.Create("abc")] = 1 });
        var bookmarkJson = JsonSerializer.Serialize(bookmark, AppJsonContext.Default.Bookmark);
        var readBack = JsonSerializer.Deserialize(bookmarkJson, AppJsonContext.Default.Bookmark)!;
        report.Line("record Bookmark", $"written {bookmarkJson}");
        report.Line("record Bookmark", $"read back {readBack.Link}, mirror {readBack.Mirror?.ToString() ?? "null"}, {string.Join(", ", readBack.PerCode.Select(pair => $"{pair.Key}={pair.Value}"))}");
        foreach (var refused in (string[])[
            bookmarkJson.Replace("\"https://example.com/a\"", "\"/a\"", StringComparison.Ordinal),
            bookmarkJson.Replace("\"https://example.com/m\"", "\"/m\"", StringComparison.Ordinal),
            bookmarkJson.Replace("\"ABC\"", "\"ab1\"", StringComparison.Ordinal)])
        {
            try
            {
                report.Line("record Bookmark refused", $"accepted {JsonSerializer.Deserialize(refused, AppJsonContext.Default.Bookmark)}");
            }
            catch (JsonException exception)
            {
                report.Line("record Bookmark refused", $"{exception.Path}: threw {Report.Describe(exception)}: {exception.Message}");
            }
        }

        foreach (var profile in (ReadOnlySpan<ValueObjectJsonSchemaProfile>)[ValueObjectJsonSchemaProfile.OpenApi, ValueObjectJsonSchemaProfile.LanguageModel])
        {
            var options = new JsonSchemaExporterOptions { TransformSchemaNode = ValueObjectJsonSchema.CreateTransform(profile) };
            foreach (var typeInfo in (JsonTypeInfo[])[AppJsonContext.Default.Order, AppJsonContext.Default.Page, AppJsonContext.Default.Tally, AppJsonContext.Default.Bookmark])
            {
                report.Line($"record {Names.Of(typeInfo.Type)}", $"JSON Schema for {profile}: {JsonSchemaExporter.GetJsonSchemaAsNode(typeInfo, options).ToJsonString()}");
            }
        }
    }

    /// <summary>
    /// A value object written by hand that nothing registered: describing it by reflection takes dynamic code, which
    /// neither run has, so the JSON factory refuses it, naming the registration it needs, rather than fail inside the
    /// registry's reflection.
    /// </summary>
    private static void Unregistered(Report report)
    {
        try
        {
            report.Line("unregistered UnregisteredCode", $"written {JsonSerializer.Serialize(UnregisteredCode.Create("x"), AppJsonContext.Default.UnregisteredCode)}");
        }
        catch (NotSupportedException exception)
        {
            report.Line("unregistered UnregisteredCode", $"threw {Report.Describe(exception)}: {exception.Message}");
        }

        report.Line("unregistered UnregisteredCode", $"registered since: {ValueObjectRegistry.TryGet(typeof(UnregisteredCode), out _)}");
    }

    /// <summary>Changes the last character of an identifier, which its check character then refuses.</summary>
    private static string Mangle(string id) => string.Concat(id.AsSpan(0, id.Length - 1), id[^1] == '0' ? "1" : "0");
}
