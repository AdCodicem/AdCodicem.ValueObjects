using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AdCodicem.ValueObjects.UnitTests.LanguageModels;

/// <summary>
/// Model Context Protocol tools on instance methods, written as an application writes them: an instance is created for
/// each call, its constructor's parameter resolved from the server's services, and disposed after it.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "The tools are instance methods on purpose: an instance is created for each call.")]
internal sealed class OrderTools : IDisposable
{
    /// <summary>The arguments of <c>place_order</c> that every rule accepts.</summary>
    public const string ValidOrder =
        """{"iban":"FR7630006000011234567890189","quantity":3,"customer":"6f9619ff-8b86-d011-b42d-00c04fc964ff","country":"FR"}""";

    private readonly InvocationLog _log;

    public OrderTools(InvocationLog log)
    {
        _log = log;
        log.OnCreated();
    }

    /// <summary>Gets the order the structured tools return.</summary>
    public static PlacedOrder Order { get; } = new(
        CustomerId.Create(Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff")),
        Iban.Create("FR7630006000011234567890189"),
        Quantity.Create(3),
        CountryCode.France,
        null,
        Amount.Create(12.5m),
        [Iban.Create("DE89370400440532013000")]);

    [McpServerTool(Name = "place_order")]
    [Description("Places an order.")]
    public string PlaceOrder(
        [Description("The account to debit.")] Iban iban,
        Quantity quantity,
        CustomerId customer,
        CountryCode country,
        BirthDate? delivery = null)
        => $"placed {quantity} on {iban} for {customer} in {country}, delivery {delivery?.ToString() ?? "none"}";

    [McpServerTool(Name = "get_order", UseStructuredContent = true)]
    public PlacedOrder GetOrder() => Order;

    /// <summary>Returns a value object alone, which the SDK wraps for a client on an older protocol version.</summary>
    [McpServerTool(Name = "get_iban", UseStructuredContent = true)]
    public Iban GetIban(Quantity quantity) => quantity.Value > 0 ? Order.Iban : Order.Alternates[0];

    /// <summary>Takes value objects in a list and in objects, one holding a member that is none.</summary>
    [McpServerTool(Name = "book")]
    public string Book(List<Iban> accounts, PlacedOrder? order = null, OrderLine? line = null)
        => $"booked {accounts.Count}, {order?.Quantity.ToString() ?? "no order"}, {line?.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "no line"}";

    /// <summary>Takes, beside a value object, parameters the SDK binds itself and never lists in the schema.</summary>
    [McpServerTool(Name = "who")]
    public string Who(
        McpServer server,
        RequestContext<CallToolRequestParams> context,
        IProgress<ProgressNotificationValue> progress,
        InvocationLog log,
        Quantity quantity,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return $"{quantity} for {context.Params.Name}, {log.Created} created, bound {server is not null && progress is not null}";
    }

    /// <summary>A static tool beside the instance ones, created with no instance.</summary>
    [McpServerTool(Name = "settle")]
    public static string Settle(LedgerBalance balance) => $"settled {balance}";

    [McpServerTool(Name = "reference")]
    public string Reference(Reference<PurchaseOrder> reference) => $"referenced {reference}";

    /// <summary>Declares the type of its structured content, and returns the result it builds itself.</summary>
    [McpServerTool(Name = "describe_order", UseStructuredContent = true, OutputSchemaType = typeof(PlacedOrder))]
    public CallToolResult DescribeOrder(Quantity quantity)
        => new() { Content = [new TextContentBlock { Text = $"described {quantity}" }] };

    /// <summary>A tool on a method that is not public, which the SDK's registrations find as well.</summary>
    [McpServerTool(Name = "count")]
    internal string Count(Quantity quantity) => $"counted {quantity}";

    /// <summary>A public method without the attribute, which is no tool.</summary>
    public string NotATool(Quantity quantity) => $"not a tool {quantity}";

    public void Dispose() => _log.OnDisposed();
}

/// <summary>
/// Tools on the static methods of a static class, which only the registrations taking a <see cref="Type"/> accept.
/// </summary>
internal static class StaticTools
{
    [McpServerTool(Name = "ship")]
    public static string Ship(Quantity quantity, CountryCode country) => $"shipped {quantity} to {country}";

    /// <summary>A public method without the attribute, which is no tool.</summary>
    public static string NotATool(Quantity quantity) => $"not a tool {quantity}";
}

/// <summary>A tool that takes no value object, which the registrations leave to the SDK as it is.</summary>
internal sealed class PlainTools
{
    [McpServerTool(Name = "echo")]
    public static string Echo(string text, int count) => $"{text} {count}";
}

/// <summary>The one tool type of the test assembly, which a registration from the assembly finds.</summary>
[McpServerToolType]
internal static class CatalogTools
{
    [McpServerTool(Name = "catalog_country")]
    public static string Country(CountryCode country) => $"serving {country}";
}

/// <summary>An order as a tool returns it: value objects alone, nullable and in a list, beside each other.</summary>
[Description("An order placed.")]
internal sealed record PlacedOrder(
    CustomerId Customer,
    Iban Iban,
    Quantity Quantity,
    CountryCode Country,
    BirthDate? Delivery,
    Amount Amount,
    List<Iban> Alternates);

/// <summary>
/// The contracts of the order tools, without reflection: the value objects and the results, beside the converter
/// factory, never their underlying types.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, Converters = [typeof(ValueObjectJsonConverterFactory)])]
[JsonSerializable(typeof(Iban))]
[JsonSerializable(typeof(Quantity))]
[JsonSerializable(typeof(CustomerId))]
[JsonSerializable(typeof(CountryCode))]
[JsonSerializable(typeof(BirthDate?))]
[JsonSerializable(typeof(LedgerBalance))]
[JsonSerializable(typeof(Reference<PurchaseOrder>))]
[JsonSerializable(typeof(List<Iban>))]
[JsonSerializable(typeof(PlacedOrder))]
[JsonSerializable(typeof(OrderLine))]
internal sealed partial class McpToolContext : JsonSerializerContext;
