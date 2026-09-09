using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdCodicem.ValueObjects.Json;

/// <summary>
/// Serializes a value object as its bare underlying value, delegating the underlying value to
/// System.Text.Json itself.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// This is the general-purpose converter, used for value objects written by hand. A generated value object
/// carries its own converter, which writes the value directly instead of going back through the serializer, and
/// is the one the factory hands out whenever it is available.
/// </remarks>
[RequiresUnreferencedCode("Delegating the underlying value to the serializer needs its metadata, which trimming may remove. Generated value objects carry their own converter and do not go through this one.")]
[RequiresDynamicCode("Delegating the underlying value to the serializer may need run-time code generation. Generated value objects carry their own converter and do not go through this one.")]
public sealed class ValueObjectJsonConverter<TSelf, TValue> : JsonConverter<TSelf>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <inheritdoc />
    public override TSelf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = JsonSerializer.Deserialize<TValue>(ref reader, options)!;

        if (!TSelf.TryCreate(value, out var result, out var validation))
        {
            throw new JsonException($"The value is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}");
        }

        return result;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value.Value, options);

    /// <inheritdoc />
    public override TSelf ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (!TSelf.TryParse(reader.GetString(), CultureInfo.InvariantCulture, out var result))
        {
            throw new JsonException($"The dictionary key is not a valid {typeof(TSelf).Name}.");
        }

        return result;
    }

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        Span<char> buffer = stackalloc char[64];
        if (value.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture))
        {
            writer.WritePropertyName(buffer[..written]);
            return;
        }

        writer.WritePropertyName(value.ToString(null, CultureInfo.InvariantCulture));
    }
}
