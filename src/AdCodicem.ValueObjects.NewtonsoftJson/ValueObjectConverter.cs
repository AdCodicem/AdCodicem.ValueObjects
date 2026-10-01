using System.Collections.Frozen;
using System.Globalization;
using System.Numerics;
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
        [typeof(sbyte)] = Wire.Integer<sbyte>(),
        [typeof(byte)] = Wire.Integer<byte>(),
        [typeof(short)] = Wire.Integer<short>(),
        [typeof(ushort)] = Wire.Integer<ushort>(),
        [typeof(int)] = Wire.Integer<int>(),
        [typeof(uint)] = Wire.Integer<uint>(),
        [typeof(long)] = Wire.Integer<long>(),
        [typeof(ulong)] = Wire.Integer<ulong>(),

        // No JSON consumer holds a 128-bit integer in a number without losing precision.
        [typeof(Int128)] = new(JsonToken.String, "D"),
        [typeof(UInt128)] = new(JsonToken.String, "D"),
        [typeof(decimal)] = Wire.Real<decimal>(),
        [typeof(double)] = Wire.Real<double>(),
        [typeof(float)] = Wire.Real<float>(),
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

        if (Wires.TryGetValue(descriptor.ValueType, out var wire) && wire.Token != JsonToken.String)
        {
            var value = wire.Token == JsonToken.Boolean
                ? ReadBoolean(reader, descriptor)
                : ReadNumber(reader, descriptor, wire);

            return Create(descriptor, value);
        }

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

    /// <summary>
    /// Reads a boolean from a boolean token only, as System.Text.Json does.
    /// </summary>
    /// <param name="reader">Reader positioned on the token.</param>
    /// <param name="descriptor">Value object being read.</param>
    /// <returns>The underlying value.</returns>
    private static object ReadBoolean(JsonReader reader, ValueObjectDescriptor descriptor)
        => reader.TokenType == JsonToken.Boolean ? reader.Value! : throw Expected(descriptor, "boolean", reader);

    /// <summary>
    /// Reads a number as System.Text.Json reads it: from a number token only, with no fraction for an integral type,
    /// and within the range of the type.
    /// </summary>
    /// <param name="reader">Reader positioned on the token.</param>
    /// <param name="descriptor">Value object being read.</param>
    /// <param name="wire">How the underlying type travels.</param>
    /// <returns>The underlying value.</returns>
    private static object ReadNumber(JsonReader reader, ValueObjectDescriptor descriptor, Wire wire)
    {
        if (reader.TokenType is not (JsonToken.Integer or JsonToken.Float))
        {
            throw Expected(descriptor, "number", reader);
        }

        // Newtonsoft.Json has already turned the token into a long, a ulong or a BigInteger, or, under the
        // serializer's FloatParseHandling, into a double or a decimal. The invariant text of each is the token's own
        // digits, but for a double, whose shortest round-trip text is the closest to them it left: a decimal is never
        // read through a double's fifteen digits.
        var text = Convert.ToString(reader.Value, CultureInfo.InvariantCulture)!;

        return (reader.TokenType == JsonToken.Integer || wire.Token == JsonToken.Float) && wire.Parse!(text) is { } number
            ? number
            : throw new JsonSerializationException($"The value could not be read as {descriptor.ValueObjectType.Name}.");
    }

    /// <summary>
    /// Builds the value object from its underlying value, through its rules.
    /// </summary>
    /// <param name="descriptor">Value object being read.</param>
    /// <param name="raw">Underlying value.</param>
    /// <returns>The value object.</returns>
    private static object Create(ValueObjectDescriptor descriptor, object raw)
        => descriptor.TryCreate(raw, out var created, out var validation)
            ? created!
            : throw new JsonSerializationException(
                $"The value is not a valid {descriptor.ValueObjectType.Name}: {validation.ErrorMessage}");

    private static JsonSerializationException Expected(ValueObjectDescriptor descriptor, string token, JsonReader reader)
        => new($"Expected a JSON {token} for {descriptor.ValueObjectType.Name} but found {reader.TokenType}.");

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
    /// <param name="Parse">
    /// For a number, parses the invariant text of the token, giving <see langword="null"/> for what the type cannot
    /// hold: out of its range, or not finite.
    /// </param>
    private sealed record Wire(JsonToken Token, string? Format = null, Func<string, object?>? Parse = null)
    {
        public static Wire Integer<T>()
            where T : struct, INumberBase<T>
            => new(JsonToken.Integer, Parse: static text => Number<T>(text, NumberStyles.AllowLeadingSign));

        public static Wire Real<T>()
            where T : struct, INumberBase<T>
            => new(JsonToken.Float, Parse: static text => Number<T>(text, NumberStyles.Float));

        private static object? Number<T>(string text, NumberStyles styles)
            where T : struct, INumberBase<T>
            => T.TryParse(text, styles, CultureInfo.InvariantCulture, out var value) && T.IsFinite(value) ? value : null;
    }
}
