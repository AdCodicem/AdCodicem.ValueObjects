using System.Globalization;
using AdCodicem.ValueObjects.Metadata;
using Newtonsoft.Json;

namespace AdCodicem.ValueObjects.NewtonsoftJson;

/// <summary>
/// Reads and writes any value object as its bare underlying value.
/// </summary>
/// <remarks>
/// One converter covers every value object, because the work is delegated to the runtime descriptor rather than
/// to a type-specific implementation. Newtonsoft.Json resolves converters by reflection anyway, so there is
/// nothing to gain from a generic converter per type here.
/// </remarks>
public sealed class ValueObjectConverter : JsonConverter
{
    /// <inheritdoc />
    public override bool CanConvert(Type objectType)
    {
        ArgumentNullException.ThrowIfNull(objectType);

        return ValueObjectRegistry.IsValueObject(objectType);
    }

    /// <inheritdoc />
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(objectType);

        var isNullable = Nullable.GetUnderlyingType(objectType) is not null;

        if (reader.TokenType == JsonToken.Null)
        {
            return isNullable
                ? null
                : throw new JsonSerializationException($"Cannot convert null to '{objectType.Name}'.");
        }

        var descriptor = Resolve(objectType);

        if (reader.Value is string text)
        {
            if (descriptor.TryParse(text, CultureInfo.InvariantCulture, out var parsed, out var textValidation))
            {
                return parsed;
            }

            throw new JsonSerializationException(
                $"The value is not a valid {descriptor.ValueObjectType.Name}: {textValidation.ErrorMessage}");
        }

        var raw = Convert.ChangeType(reader.Value, descriptor.ValueType, CultureInfo.InvariantCulture);
        if (descriptor.TryCreate(raw, out var created, out var validation))
        {
            return created;
        }

        throw new JsonSerializationException(
            $"The value is not a valid {descriptor.ValueObjectType.Name}: {validation.ErrorMessage}");
    }

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteValue(Resolve(value.GetType()).GetValue(value));
    }

    private static ValueObjectDescriptor Resolve(Type type)
    {
#pragma warning disable IL2026, IL3050 // Newtonsoft.Json is reflection-driven throughout.
        if (ValueObjectRegistry.TryResolve(type, out var descriptor))
        {
            return descriptor;
        }
#pragma warning restore IL2026, IL3050

        throw new JsonSerializationException($"'{type.Name}' is not a value object.");
    }
}
