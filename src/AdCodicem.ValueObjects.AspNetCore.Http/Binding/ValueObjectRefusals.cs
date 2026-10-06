using System.Buffers;
using System.Text.Json;
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
internal sealed class ValueObjectRefusals
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

    /// <summary>Adds a required value object that is absent.</summary>
    /// <param name="member">The name it binds from.</param>
    public void AddRequired(string member) => Add(member, RequiredMessage, ValueObjectErrorCodes.Required);

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
