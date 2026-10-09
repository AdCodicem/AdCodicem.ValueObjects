using System.Buffers;
using System.Text.Json;

namespace AdCodicem.ValueObjects.Shared;

/// <summary>
/// An argument a model sent that a value object refused: the name it was sent under, the code of the rule, and the
/// message of the refusal, never the value sent.
/// </summary>
/// <param name="Argument">The name the model sent the argument under.</param>
/// <param name="Code">The code of the rule.</param>
/// <param name="Message">The message of the refusal, which names the value object and the rule, never the value.</param>
/// <remarks>
/// It names no type of Microsoft.Extensions.AI, so that each package that hands a model the result of a tool, the AI
/// package and any other host of tools, writes the same object.
/// </remarks>
internal sealed record ValueObjectArgumentRejection(string Argument, string Code, string Message)
{
    /// <summary>
    /// The value of <c>error</c>, which tells a refusal from a result of the tool that happens to be an object.
    /// </summary>
    public const string Error = "invalid_argument";

    /// <summary>
    /// Writes the refusal as the JSON object a model reads, without a serializer, so that no context has to know it.
    /// </summary>
    /// <returns><c>{"error":"invalid_argument","argument":…,"code":…,"message":…}</c>, in that order.</returns>
    public JsonElement ToJsonElement()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("error"u8, Error);
            writer.WriteString("argument"u8, Argument);
            writer.WriteString("code"u8, Code);
            writer.WriteString("message"u8, Message);
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
