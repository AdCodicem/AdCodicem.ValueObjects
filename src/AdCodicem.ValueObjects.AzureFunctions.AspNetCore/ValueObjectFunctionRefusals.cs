using System.Buffers;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace AdCodicem.ValueObjects.AzureFunctions.AspNetCore;

/// <summary>
/// The value objects an invocation was refused for, each member with its messages and the code of its first refusal,
/// and the validation problem that reports them, in the shape the minimal API package writes.
/// </summary>
/// <param name="messages">The messages the application named, or <see langword="null"/> for the English ones.</param>
/// <param name="services">The request's services, which hold the localizer the messages are read through.</param>
internal sealed class ValueObjectFunctionRefusals(ValueObjectMessageOptions? messages, IServiceProvider services)
{
    /// <summary>What an absent value is reported with: the message of a <see langword="null"/> the generated code refuses.</summary>
    private const string RequiredMessage = "A value is required.";

    private readonly Dictionary<string, List<string>> _messages = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _codes = new(StringComparer.Ordinal);

    /// <summary>Gets the number of members refused.</summary>
    public int Count => _codes.Count;

    /// <summary>Gets the members refused, each with its code, in the order they were added.</summary>
    public IEnumerable<KeyValuePair<string, string>> Codes => _codes;

    /// <summary>Adds the refusal of a member; a member refused again keeps its first code and gains the message.</summary>
    /// <param name="member">The name the refusal is listed under.</param>
    /// <param name="message">The message.</param>
    /// <param name="code">The code.</param>
    public void Add(string member, string message, string code)
    {
        if (_messages.TryGetValue(member, out var existing))
        {
            existing.Add(message);
            return;
        }

        _messages[member] = [message];
        _codes[member] = code;
    }

    /// <summary>Adds the refusal of a value object parsed from text, with the message the resource holds for its code.</summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="member">The parameter's name, which the message quotes as <c>{0}</c>.</param>
    /// <param name="message">The English message of the rule.</param>
    /// <param name="code">The code of the rule.</param>
    public void Add<TSelf, TValue>(string member, string message, string code)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => Add(member, ValueObjectLocalizedMessages.Find<TSelf, TValue>(messages, services, code, member) ?? message, code);

    /// <summary>Adds a required value object that has no binding data.</summary>
    /// <param name="member">The parameter's name.</param>
    public void AddRequired(string member)
    {
        var message = messages?.ResourceType is { } resource
            ? ValueObjectLocalizedMessages.Find(resource, services, descriptor: null, ValueObjectErrorCodes.Required, member)
            : null;

        Add(member, message ?? RequiredMessage, ValueObjectErrorCodes.Required);
    }

    /// <summary>
    /// Creates the validation problem, with the codes under <see cref="ValueObjectProblemDetails.ExtensionName"/>, keyed as
    /// the serializer keys <c>errors</c>.
    /// </summary>
    /// <param name="httpContext">The request, whose services hold the JSON options the response is written with.</param>
    /// <returns>The validation problem.</returns>
    public ValidationProblem ToProblem(HttpContext httpContext)
    {
        var keyPolicy = httpContext.RequestServices.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions.DictionaryKeyPolicy;

        var errors = new Dictionary<string, string[]>(_messages.Count, StringComparer.Ordinal);
        foreach (var (member, list) in _messages)
        {
            errors[member] = [.. list];
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
