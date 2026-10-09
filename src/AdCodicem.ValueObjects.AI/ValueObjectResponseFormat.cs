using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace AdCodicem.ValueObjects.AI;

/// <summary>
/// Builds the JSON schema response format of a structured output whose value objects carry their rules.
/// </summary>
/// <remarks>
/// <para>
/// <c>GetResponseAsync&lt;T&gt;</c> and <see cref="ChatResponseFormat.ForJsonSchema{T}"/> build the schema with options
/// of their own, which no transform reaches: each value object of the answer is published as <c>true</c>, and a list of
/// them as <c>"items": {}</c>. This builds the same format with the rules of every value object it holds, and the
/// answer is read the way <c>GetResponseAsync&lt;T&gt;</c> reads it:
/// </para>
/// <code>
/// ChatResponse response = await chatClient.GetResponseAsync(
///     messages,
///     new ChatOptions { ResponseFormat = ValueObjectResponseFormat.ForJsonSchema&lt;Order&gt;(AppJsonContext.Default.Options) });
/// var order = new ChatResponse&lt;Order&gt;(response, AppJsonContext.Default.Options).Result;
/// </code>
/// <para>
/// The same rules validate the answer as it is read: a value the model wrote that a value object refuses throws a
/// <see cref="ValueObjectJsonException"/> from <c>Result</c>, whose code <see cref="ValueObjectErrors.TryGetCode"/>
/// reads, for the application to ask again.
/// </para>
/// </remarks>
public static partial class ValueObjectResponseFormat
{
    private static readonly AIJsonSchemaCreateOptions InferenceOptions
        = new AIJsonSchemaCreateOptions { IncludeSchemaKeyword = true }.WithValueObjects();

    /// <summary>
    /// Creates a JSON schema response format for <typeparamref name="T"/>, with the rules of every value object it holds.
    /// </summary>
    /// <typeparam name="T">The type of the structured output, which serializes as a JSON object.</typeparam>
    /// <param name="serializerOptions">
    /// The options the answer is read with, <see cref="AIJsonUtilities.DefaultOptions"/> by default.
    /// </param>
    /// <param name="schemaName">
    /// The name of the schema: by default the type's <see cref="DisplayNameAttribute"/>, or its name with every character
    /// outside <c>[0-9A-Za-z_]</c> replaced by <c>_</c>, as <see cref="ChatResponseFormat.ForJsonSchema{T}"/> names it.
    /// </param>
    /// <param name="schemaDescription">The description of the schema, the type's <see cref="DescriptionAttribute"/> by default.</param>
    /// <returns>The response format, its schema led by <c>$schema</c>, as Microsoft.Extensions.AI writes one.</returns>
    /// <exception cref="NotSupportedException">
    /// The resolver of <paramref name="serializerOptions"/> has no contract for <typeparamref name="T"/>.
    /// </exception>
    public static ChatResponseFormatJson ForJsonSchema<T>(
        JsonSerializerOptions? serializerOptions = null,
        string? schemaName = null,
        string? schemaDescription = null)
    {
        var type = typeof(T);
        var schema = AIJsonUtilities.CreateJsonSchema(
            type,
            serializerOptions: serializerOptions ?? AIJsonUtilities.DefaultOptions,
            inferenceOptions: InferenceOptions);

        return ChatResponseFormat.ForJsonSchema(
            schema,
            schemaName ?? type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? InvalidNameCharacters().Replace(type.Name, "_"),
            schemaDescription ?? type.GetCustomAttribute<DescriptionAttribute>()?.Description);
    }

    /// <summary>Matches a character a schema name cannot hold, as Microsoft.Extensions.AI tells one.</summary>
    [GeneratedRegex("[^0-9A-Za-z_]")]
    private static partial Regex InvalidNameCharacters();
}
