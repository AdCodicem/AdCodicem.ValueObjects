using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// Moves an <see cref="AnyEntityId"/> across a JSON boundary as the bare identifier text.
/// </summary>
/// <remarks>
/// Without this the serializer would reflect over the struct and write an object with a value, a prefix and a
/// type name — which is not what an identifier looks like anywhere else in this library, and is precisely the
/// wrong shape for the payloads this type exists to serve. Writing the text also makes the default OpenAPI
/// schema a plain string, which is the representation chosen over a <c>oneOf</c> across every registered
/// pattern: faithful, and unreadable past a handful of identifier types.
/// </remarks>
public sealed class AnyEntityIdJsonConverter : JsonConverter<AnyEntityId>
{
    /// <inheritdoc />
    public override AnyEntityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string holding an entity identifier, found {reader.TokenType}.");
        }

        var text = reader.GetString();

        if (!AnyEntityId.TryParse(text, null, out var result))
        {
            throw new JsonException($"'{text}' is not an identifier of any registered type.");
        }

        return result;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, AnyEntityId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value.Value);
    }

    /// <inheritdoc />
    public override AnyEntityId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => AnyEntityId.Parse(reader.GetString() ?? string.Empty, CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, AnyEntityId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WritePropertyName(value.Value);
    }
}

/// <summary>
/// Converts an <see cref="AnyEntityId"/> to and from text for the boundaries that go through
/// <see cref="TypeDescriptor"/>: MVC model binding, configuration binding, and the designers.
/// </summary>
/// <remarks>
/// Minimal APIs need none of this — <see cref="AnyEntityId"/> implements <see cref="IParsable{TSelf}"/>, which
/// is exactly what their parameter binding looks for.
/// </remarks>
public sealed class AnyEntityIdTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value switch
        {
            null => default(AnyEntityId),
            string text => AnyEntityId.Parse(text, CultureInfo.InvariantCulture),
            _ => base.ConvertFrom(context, culture, value),
        };

    /// <inheritdoc />
    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType)
        => destinationType == typeof(string) && value is AnyEntityId identifier
            ? identifier.Value
            : base.ConvertTo(context, culture, value, destinationType);
}
