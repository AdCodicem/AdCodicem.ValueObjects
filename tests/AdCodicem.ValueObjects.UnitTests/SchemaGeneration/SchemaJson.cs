using AdCodicem.ValueObjects.NewtonsoftJson;
using AdCodicem.ValueObjects.NJsonSchema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using NJsonSchema.Validation;
using SystemTextJsonOptions = System.Text.Json.JsonSerializerOptions;

namespace AdCodicem.ValueObjects.UnitTests.SchemaGeneration;

/// <summary>
/// The settings a schema is generated with on each wire, and the reading of what NJsonSchema writes.
/// </summary>
internal static class SchemaJson
{
    /// <summary>Gets Newtonsoft.Json settings holding <c>ValueObjectConverter</c>, as the Confluent guide sets them.</summary>
    /// <returns>The settings, value objects added.</returns>
    public static NewtonsoftJsonSchemaGeneratorSettings NewtonsoftWithConverter()
    {
        var settings = new NewtonsoftJsonSchemaGeneratorSettings();
        settings.SerializerSettings.AddValueObjects();

        return settings.AddValueObjects();
    }

    /// <summary>Gets Newtonsoft.Json's default settings, which write a value object through its type converter.</summary>
    /// <returns>The settings, value objects added.</returns>
    public static NewtonsoftJsonSchemaGeneratorSettings NewtonsoftText() => new NewtonsoftJsonSchemaGeneratorSettings().AddValueObjects();

    /// <summary>Gets System.Text.Json settings.</summary>
    /// <param name="options">The options, NJsonSchema's own default when none.</param>
    /// <param name="schemaType">The kind of schema.</param>
    /// <returns>The settings, value objects added.</returns>
    public static SystemTextJsonSchemaGeneratorSettings SystemTextJson(SystemTextJsonOptions? options = null, SchemaType schemaType = SchemaType.JsonSchema)
        => new SystemTextJsonSchemaGeneratorSettings { SerializerOptions = options ?? new SystemTextJsonOptions(), SchemaType = schemaType }.AddValueObjects();

    /// <summary>Generates the schema of a type.</summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="settings">The settings.</param>
    /// <returns>The schema, as JSON, read with its strings kept as they are written.</returns>
    public static JObject Generate<T>(JsonSchemaGeneratorSettings settings) => Parse(JsonSchemaGenerator.FromType<T>(settings).ToJson());

    /// <summary>Reads JSON, keeping a string that looks like a date a string.</summary>
    /// <param name="json">The JSON.</param>
    /// <returns>The object.</returns>
    public static JObject Parse(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double };

        return JObject.Load(reader);
    }

    /// <summary>Gets the schema of a property, compact.</summary>
    /// <param name="schema">The schema of the object.</param>
    /// <param name="name">The property's name.</param>
    /// <returns>Its JSON.</returns>
    public static string Property(JObject schema, string name) => Compact(schema["properties"]![name]!);

    /// <summary>Gets a definition, compact.</summary>
    /// <param name="schema">The root schema.</param>
    /// <param name="name">The definition's name.</param>
    /// <returns>Its JSON.</returns>
    public static string Definition(JObject schema, string name) => Compact(schema["definitions"]![name]!);

    /// <summary>Gets the definition a member of the root object refers to, compact.</summary>
    /// <param name="schema">The root schema.</param>
    /// <param name="member">The member's name.</param>
    /// <returns>The JSON of the definition.</returns>
    public static string DefinitionOf(JObject schema, string member)
        => Compact(schema.SelectToken(schema["properties"]![member]!["$ref"]!.ToString().TrimStart('#', '/').Replace('/', '.'))!);

    /// <summary>Writes a token without indentation.</summary>
    /// <param name="token">The token.</param>
    /// <returns>Its JSON.</returns>
    public static string Compact(JToken token) => token.ToString(Formatting.None);

    /// <summary>
    /// Validates JSON against a schema as Confluent's serializer and deserializer do: with no format validator.
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <param name="json">The JSON.</param>
    /// <returns>Each error, its kind and its path.</returns>
    public static List<string> Errors(JsonSchema schema, string json)
        => [.. schema.Validate(json, new JsonSchemaValidatorSettings { FormatValidators = [] }).Select(static error => $"{error.Kind} {error.Path}")];
}
