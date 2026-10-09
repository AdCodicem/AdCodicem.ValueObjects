using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.AI;
using AdCodicem.ValueObjects.Json;
using Microsoft.Extensions.AI;

namespace AdCodicem.ValueObjects.UnitTests.LanguageModels;

/// <summary>
/// The methods the tests turn into tools, written as an application writes them, and what the tests build and call them
/// with.
/// </summary>
internal static class LanguageModelTools
{
    /// <summary>The arguments of <see cref="PlaceOrder"/> that every rule accepts.</summary>
    public const string ValidOrder =
        """{"iban":"FR7630006000011234567890189","quantity":3,"customer":"6f9619ff-8b86-d011-b42d-00c04fc964ff","country":"FR"}""";

    /// <summary>Gets the schema options of a tool whose value objects carry their rules.</summary>
    public static AIJsonSchemaCreateOptions Rules => new AIJsonSchemaCreateOptions().WithValueObjects();

    [Description("Places an order.")]
    public static string PlaceOrder(
        [Description("The account to debit.")] Iban iban,
        Quantity quantity,
        CustomerId customer,
        CountryCode country,
        EffectiveDate? effective = null)
        => $"placed {quantity} on {iban} for {customer} in {country}, from {effective?.ToString() ?? "today"}";

    /// <summary>Takes its arguments in the other order than <see cref="PlaceOrder"/>.</summary>
    public static string Swap(Quantity quantity, Iban iban) => $"{quantity} on {iban}";

    /// <summary>Tells the instance a value object was bound to, a default one included.</summary>
    public static string Count(Quantity quantity) => $"{quantity} default={((IValueObject<Quantity, short>)quantity).IsDefault}";

    public static string Settle(LedgerBalance balance) => $"settled {balance}";

    /// <summary>Three ways the binding knows a value for an absent argument, one per parameter, and one it does not.</summary>
    public static string Defaults(
        [DefaultValue(null)] Quantity? declared,
        [Optional] Quantity? omitted,
        Quantity? required,
        Quantity? optional = null)
        => $"{declared?.ToString() ?? "-"} {omitted?.ToString() ?? "-"} {required?.ToString() ?? "-"} {optional?.ToString() ?? "-"}";

#pragma warning disable MEAI001 // AIParameterNameAttribute is experimental: the package reads it by name, and this is its test.
    /// <summary>Renames its parameter in the schema, beside a description.</summary>
    public static string Ship([Description("How many to ship.")][AIParameterName("qty")] Quantity quantity) => $"shipped {quantity}";
#pragma warning restore MEAI001

    /// <summary>Takes, beside a value object, parameters that no argument binds.</summary>
    public static string Hosted(Quantity quantity, AIFunctionArguments arguments, IServiceProvider? services = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return $"{quantity} {arguments.Count} {services is null}";
    }

    /// <summary>Takes value objects in a collection, a dictionary and an object.</summary>
    public static string Book(List<Iban> accounts, Dictionary<CountryCode, Quantity> perCountry, OrderLine line)
        => $"{accounts.Count} {perCountry.Count} {line.Account}";

    /// <summary>Takes value objects as the values of dictionaries, keyed by a value object and by text.</summary>
    public static string Allot(Dictionary<Iban, Quantity> perAccount, Dictionary<string, Quantity> perCustomer)
        => $"{perAccount.Count} {perCustomer.Count}";

    /// <summary>Takes no parameter that may hold a value object.</summary>
    public static string Note(string text, int count, JsonElement raw) => $"{text} {count} {raw.ValueKind}";

    public static string Code(Domain.HandWritten.HandWrittenCode code) => code.Value;

    /// <summary>
    /// Builds a tool whose value objects carry their rules, wrapped.
    /// </summary>
    public static AIFunction Validated(Delegate method, JsonSerializerOptions? options = null)
        => AIFunctionFactory.Create(method, new AIFunctionFactoryOptions { JsonSchemaCreateOptions = Rules, SerializerOptions = options })
            .WithValueObjectValidation();

    /// <summary>
    /// Reads the arguments of a call as a model's arrive: each value a <see cref="JsonElement"/>, a JSON <c>null</c> a
    /// <see langword="null"/>, as the OpenAI adapter hands them over.
    /// </summary>
    public static AIFunctionArguments Arguments(string json)
        => new(JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!);

    /// <summary>Replaces, or with <see langword="null"/> removes, one argument of <see cref="ValidOrder"/>.</summary>
    public static AIFunctionArguments Order(string name, string? json)
    {
        var order = JsonNode.Parse(ValidOrder)!.AsObject();
        if (json is null)
        {
            order.Remove(name);
        }
        else
        {
            order[name] = JsonNode.Parse(json);
        }

        return Arguments(order.ToJsonString());
    }

    /// <summary>
    /// Hands the arguments of a call over as text, a string's content or the JSON of anything else, which the binding
    /// converts through a JSON round trip of its own.
    /// </summary>
    public static AIFunctionArguments AsText(AIFunctionArguments arguments)
        => new(arguments.ToDictionary(static argument => argument.Key, static argument => (object?)argument.Value?.ToString()));

    /// <summary>Calls a function and writes its result as the JSON a model reads.</summary>
    public static async Task<string> InvokeAsync(AIFunction function, AIFunctionArguments arguments)
        => ((JsonElement)(await function.InvokeAsync(arguments, TestContext.Current.CancellationToken))!).GetRawText();

    /// <summary>Writes the result the wrapper answers a refused argument with.</summary>
    public static string Rejection(string argument, string code, string message)
        => new JsonObject { ["error"] = "invalid_argument", ["argument"] = argument, ["code"] = code, ["message"] = message }.ToJsonString();

    /// <summary>
    /// Writes the message the wrapper answers a refusal with: the converter's, or, where the converter left the message to
    /// System.Text.Json, which writes the path of the value after it, one naming the value object alone.
    /// </summary>
    public static string AnsweredMessage(ValueObjectJsonException refusal)
        => refusal.Message.Contains(" | LineNumber: ", StringComparison.Ordinal)
            ? $"The value is not a valid {refusal.ValueObjectType.Name}."
            : refusal.Message;

    /// <summary>Reads JSON as a value of a type, and returns the refusal of the value object's converter.</summary>
    public static ValueObjectJsonException RefusalOf<T>(string json, JsonSerializerOptions? options = null)
        => FluentActions.Invoking(() => JsonSerializer.Deserialize<T>(json, options ?? AIJsonUtilities.DefaultOptions))
            .Should().Throw<ValueObjectJsonException>().Which;
}

/// <summary>A line of an order, an object holding value objects beside members that are none.</summary>
internal sealed record OrderLine(Iban Account, Quantity Quantity, int Count);

/// <summary>The answer a model is asked for, its value objects alone, nullable and in a list.</summary>
[Description("A shipment to make.")]
internal sealed record Shipment(Iban Account, Quantity Quantity, CountryCode Country, EffectiveDate? Effective, List<Iban> Alternates);

/// <summary>An answer named in the schema by a <see cref="DisplayNameAttribute"/>.</summary>
[DisplayName("shipment_v2")]
internal sealed record NamedShipment(Iban Account);

/// <summary>A generic answer, whose name holds a character a schema name cannot.</summary>
internal sealed record Envelope<T>(T Content);

/// <summary>
/// The contracts of the tools' parameters, without reflection: the value objects and the result, beside the converter
/// factory, never their underlying types.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, Converters = [typeof(ValueObjectJsonConverterFactory)])]
[JsonSerializable(typeof(Iban))]
[JsonSerializable(typeof(Quantity))]
[JsonSerializable(typeof(CustomerId))]
[JsonSerializable(typeof(CountryCode))]
[JsonSerializable(typeof(EffectiveDate?))]
[JsonSerializable(typeof(Shipment))]
[JsonSerializable(typeof(string))]
internal sealed partial class LanguageModelContext : JsonSerializerContext;

/// <summary>A context that knows a value object, and not the text or the number an argument may be handed over as.</summary>
[JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
[JsonSerializable(typeof(Quantity))]
internal sealed partial class QuantityOnlyContext : JsonSerializerContext;

/// <summary>
/// A function written by hand, which no method may back, with a schema of its own.
/// </summary>
internal sealed class HandWrittenFunction(System.Reflection.MethodInfo? method, string schema) : AIFunction
{
    public override string Name => "hand_written";

    /// <summary>Gets the options the function binds its arguments with.</summary>
    public JsonSerializerOptions Options { get; init; } = AIJsonUtilities.DefaultOptions;

    public override JsonSerializerOptions JsonSerializerOptions => Options;

    public override System.Reflection.MethodInfo? UnderlyingMethod => method;

    public override JsonElement JsonSchema { get; } = JsonDocument.Parse(schema).RootElement.Clone();

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        => new(JsonSerializer.SerializeToElement("called"));
}
