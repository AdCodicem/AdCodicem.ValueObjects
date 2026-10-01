using System.Collections.Frozen;
using System.Globalization;
using AdCodicem.ValueObjects.Metadata;
using Newtonsoft.Json;

namespace AdCodicem.ValueObjects.NewtonsoftJson;

/// <summary>
/// Reads and writes any value object as its bare underlying value.
/// </summary>
/// <remarks>
/// <para>
/// One converter covers every value object, because the work is delegated to the runtime descriptor rather than
/// to a type-specific implementation. Newtonsoft.Json resolves converters by reflection anyway, so there is
/// nothing to gain from a generic converter per type here.
/// </para>
/// <para>
/// It follows the System.Text.Json converter the generator emits: the same rules, and the same JSON.
/// </para>
/// </remarks>
public sealed class ValueObjectConverter : JsonConverter
{
    /// <summary>
    /// How the System.Text.Json converter the generator emits writes each of the 22 underlying types.
    /// </summary>
    private static readonly FrozenDictionary<Type, Wire> Wires = new Dictionary<Type, Wire>
    {
        [typeof(string)] = new(JsonToken.String),
        [typeof(char)] = new(JsonToken.String),
        [typeof(Guid)] = new(JsonToken.String, "D"),
        [typeof(bool)] = new(JsonToken.Boolean),
        [typeof(sbyte)] = new(JsonToken.Integer),
        [typeof(byte)] = new(JsonToken.Integer),
        [typeof(short)] = new(JsonToken.Integer),
        [typeof(ushort)] = new(JsonToken.Integer),
        [typeof(int)] = new(JsonToken.Integer),
        [typeof(uint)] = new(JsonToken.Integer),
        [typeof(long)] = new(JsonToken.Integer),
        [typeof(ulong)] = new(JsonToken.Integer),

        // No JSON consumer holds a 128-bit integer in a number without losing precision.
        [typeof(Int128)] = new(JsonToken.String, "D"),
        [typeof(UInt128)] = new(JsonToken.String, "D"),
        [typeof(decimal)] = new(JsonToken.Float),
        [typeof(double)] = new(JsonToken.Float),
        [typeof(float)] = new(JsonToken.Float),
        [typeof(DateOnly)] = new(JsonToken.String, "O"),
        [typeof(TimeOnly)] = new(JsonToken.String, "O"),
        [typeof(DateTime)] = new(JsonToken.String, "O"),
        [typeof(DateTimeOffset)] = new(JsonToken.String, "O"),
        [typeof(TimeSpan)] = new(JsonToken.String, "c"),
    }.ToFrozenDictionary();

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
    /// <remarks>
    /// The value is written as the System.Text.Json converter the generator emits writes it: a number or a boolean
    /// as such, and anything else as a string, <see cref="Int128"/> and <see cref="UInt128"/> included, in the same
    /// round-trip form. The serializer's date settings do not apply, so that both serializers write the same text.
    /// </remarks>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(serializer);

        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        var descriptor = Resolve(value.GetType());
        var raw = descriptor.GetValue(value);

        if (!Wires.TryGetValue(descriptor.ValueType, out var wire))
        {
            // A value object written by hand over a type the generator does not support: its value is written the
            // way Newtonsoft.Json writes that type, as the general-purpose System.Text.Json converter does.
            serializer.Serialize(writer, raw);
            return;
        }

        writer.WriteValue(wire.Format is null ? raw : Format(raw!, wire.Format));
    }

    /// <summary>
    /// Formats a value System.Text.Json writes as a string, in the form it writes it in.
    /// </summary>
    /// <param name="value">Underlying value.</param>
    /// <param name="format">Round-trip format of the underlying type.</param>
    /// <returns>The text System.Text.Json writes.</returns>
    private static string Format(object value, string format)
    {
        var text = ((IFormattable)value).ToString(format, CultureInfo.InvariantCulture);

        // System.Text.Json drops the trailing zeros of the seven-digit fraction the round-trip form of a DateTime
        // and of a DateTimeOffset carries after the seconds, and the point when nothing is left of it:
        // 2024-06-01T12:30:45.1230000Z is written 2024-06-01T12:30:45.123Z.
        if (value is not (DateTime or DateTimeOffset))
        {
            return text;
        }

        const int Point = 19;
        const int Digits = 7;

        var last = Point + Digits;
        while (last > Point && text[last] == '0')
        {
            last--;
        }

        return string.Concat(text.AsSpan(0, last == Point ? Point : last + 1), text.AsSpan(Point + Digits + 1));
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

    /// <summary>
    /// How the System.Text.Json converter the generator emits writes one underlying type.
    /// </summary>
    /// <param name="Token">The token it writes: a string, a boolean, an integer, or a number with a fraction.</param>
    /// <param name="Format">
    /// The round-trip format it writes a string in, or <see langword="null"/> for a value Newtonsoft.Json already
    /// writes the same way.
    /// </param>
    private sealed record Wire(JsonToken Token, string? Format = null);
}
