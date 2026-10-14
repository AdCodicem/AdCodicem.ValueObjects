using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Metadata;
using JsonSerializerSettings = Newtonsoft.Json.JsonSerializerSettings;

namespace AdCodicem.ValueObjects.NJsonSchema;

/// <summary>
/// Writes a value object for the rules shared by every schema package as Newtonsoft.Json writes it under the settings of a
/// schema: the value <c>ValueObjectConverter</c> writes, or the text of the type converter.
/// </summary>
/// <param name="settings">The settings the schema describes the wire of.</param>
/// <remarks>
/// The example, the bounds and the known values of a schema are written by the serializer the payload is written by, so
/// that a validator, Confluent's among them, finds in the schema what it finds in the payload.
/// </remarks>
internal sealed class NewtonsoftJsonWrittenConverterFactory(JsonSerializerSettings settings) : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => ValueObjectRegistry.IsValueObject(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => ValueObjectRegistry.TryResolve(typeToConvert, out var descriptor) ? descriptor.Accept(new Closer(settings)) : null;

    /// <summary>Closes the converter over the value object, its type arguments handed back by its descriptor.</summary>
    private sealed class Closer(JsonSerializerSettings settings) : IValueObjectVisitor<JsonConverter>
    {
        /// <inheritdoc />
        public JsonConverter Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => new NewtonsoftJsonWrittenConverter<TSelf>(settings);
    }
}

/// <summary>
/// Writes one value object as Newtonsoft.Json writes it under the settings of a schema.
/// </summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <param name="settings">The settings the schema describes the wire of.</param>
/// <remarks>It writes only: a schema reads nothing.</remarks>
internal sealed class NewtonsoftJsonWrittenConverter<TSelf>(JsonSerializerSettings settings) : JsonConverter<TSelf>
    where TSelf : struct
{
    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: a schema writes the values it describes, and reads none.</exception>
    public override TSelf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("A schema writes the values it describes, and reads none.");

    /// <inheritdoc />
    /// <remarks>A value Newtonsoft.Json refuses to write is reported as System.Text.Json reports one, so that the schema
    /// falls back to the value as declared rather than failing.</remarks>
    public override void Write(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        string written;
        try
        {
            written = Newtonsoft.Json.JsonConvert.SerializeObject(value, typeof(TSelf), settings);
        }
        catch (Newtonsoft.Json.JsonException exception)
        {
            throw new JsonException(exception.Message, exception);
        }

        writer.WriteRawValue(written);
    }
}
