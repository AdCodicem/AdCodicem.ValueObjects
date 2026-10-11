using System.Buffers;
using System.Text.Json;
using AdCodicem.ValueObjects.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace AdCodicem.ValueObjects.AspNetCore.Http.Binding;

/// <summary>
/// The value objects a request was refused for, each member with the messages of the rules it broke and the code of the
/// first, and the validation problem that reports them.
/// </summary>
/// <param name="messages">
/// The messages the endpoint's convention configured, or <see langword="null"/> for the English ones of the rules.
/// </param>
/// <param name="services">The request's services, which hold the localizer the messages are read through.</param>
/// <remarks>
/// Every refusal is listed through this class, so that each gets the message the application's resource holds for its
/// code, when the endpoint names one, and keeps the English message of the rule otherwise.
/// </remarks>
internal sealed class ValueObjectRefusals(ValueObjectMessageOptions? messages, IServiceProvider services)
{
    /// <summary>What an absent value is reported with: the message of a <see langword="null"/> the generated code refuses.</summary>
    private const string RequiredMessage = "A value is required.";

    private readonly Dictionary<string, List<string>> _messages = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _codes = new(StringComparer.Ordinal);

    /// <summary>Gets the number of members refused.</summary>
    public int Count => _codes.Count;

    /// <summary>Adds the refusal of a member; a member refused again keeps its first code and gains the message.</summary>
    /// <param name="member">The name the refusal is listed under.</param>
    /// <param name="message">The message of the rule.</param>
    /// <param name="code">The code of the rule.</param>
    public void Add(string member, string message, string code)
    {
        if (_messages.TryGetValue(member, out var messages))
        {
            messages.Add(message);
            return;
        }

        _messages[member] = [message];
        _codes[member] = code;
    }

    /// <summary>
    /// Adds the refusal of a value object parsed from text, with the message the resource holds for its code, if any.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="member">The name it binds from, which the refusal is listed under and the message quotes as <c>{0}</c>.</param>
    /// <param name="message">The English message of the rule.</param>
    /// <param name="code">The code of the rule.</param>
    public void Add<TSelf, TValue>(string member, string message, string code)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => Add(member, ValueObjectLocalizedMessages.Find<TSelf, TValue>(messages, services, code, member) ?? message, code);

    /// <summary>Adds a required value object that is absent.</summary>
    /// <param name="member">The name it binds from.</param>
    public void AddRequired(string member)
    {
        var message = messages?.ResourceType is { } resource
            ? ValueObjectLocalizedMessages.Find(resource, services, descriptor: null, ValueObjectErrorCodes.Required, member)
            : null;

        Add(member, message ?? RequiredMessage, ValueObjectErrorCodes.Required);
    }

    /// <summary>
    /// Adds the value object a request body refused, under its JSON path, with the message the resource holds for its
    /// code, if any, or the serializer's.
    /// </summary>
    /// <param name="json">The exception the serializer threw.</param>
    /// <param name="code">The code it carries.</param>
    /// <remarks>
    /// The message quotes the value object by the name of its type as <c>{0}</c>, never by the JSON path, which holds the
    /// key a dictionary keyed by a value object refused, the refused value itself. A type the registry cannot describe,
    /// such as <c>AnyEntityId</c>, has the key of the code alone and no argument. An exception that names no value object
    /// keeps its message.
    /// </remarks>
    public void AddBody(JsonException json, string code)
    {
        string? message = null;
        if (messages?.ResourceType is { } resource && json is ValueObjectJsonException refusal)
        {
            // Described only when a resource is named, so that an endpoint keeping the English messages asks the registry
            // nothing more than it did.
            message = ValueObjectLocalizedMessages.Find(
                resource,
                services,
                ValueObjectBindingFilter.TryDescribe(refusal.ValueObjectType, out var descriptor) ? descriptor : null,
                code,
                ValueObjectLocalizedMessages.NameOf(refusal.ValueObjectType));
        }

        Add(json.Path ?? string.Empty, message ?? json.Message, code);
    }

    /// <summary>
    /// Creates the validation problem MVC writes for a refused value, with the codes under
    /// <see cref="ValueObjectProblemDetails.ExtensionName"/>.
    /// </summary>
    /// <param name="httpContext">The request, whose services hold the JSON options the response is written with.</param>
    /// <returns>The validation problem.</returns>
    /// <remarks>
    /// The codes are a <see cref="JsonElement"/>, which the serializer context the framework writes problem details
    /// with knows, so that a native binary writes them without a context of the application's naming a dictionary. Their
    /// keys go through the dictionary key policy the serializer applies to the keys of <c>errors</c>, so that both are
    /// keyed alike, as MVC keys them.
    /// </remarks>
    public ValidationProblem ToProblem(HttpContext httpContext)
    {
        var keyPolicy = httpContext.RequestServices.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions.DictionaryKeyPolicy;

        var errors = new Dictionary<string, string[]>(_messages.Count, StringComparer.Ordinal);
        foreach (var (member, messages) in _messages)
        {
            errors[member] = [.. messages];
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (member, code) in _codes)
            {
                writer.WriteString(keyPolicy is null ? member : keyPolicy.ConvertName(member), code);
            }

            writer.WriteEndObject();
        }

        using var codes = JsonDocument.Parse(buffer.WrittenMemory);

        return TypedResults.ValidationProblem(
            errors,
            extensions: [new KeyValuePair<string, object?>(ValueObjectProblemDetails.ExtensionName, codes.RootElement.Clone())]);
    }
}
