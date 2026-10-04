using System.Buffers.Text;
using System.Collections.Frozen;
using System.Globalization;
using System.Numerics;
using System.Text;
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
/// It follows the System.Text.Json converter the generator emits: the same rules, and the same values on the wire.
/// The text is the same too, but for a whole <see cref="decimal"/>, <see cref="double"/> or <see cref="float"/>,
/// which Newtonsoft.Json always writes with a fraction: <c>1250.0</c> where System.Text.Json writes <c>1250</c>.
/// Each serializer reads the other's text as the same value.
/// </para>
/// <para>
/// <see cref="ValueObjectJsonSerializerSettingsExtensions.AddValueObjects(JsonSerializerSettings, bool)"/> adds it to
/// the serializer settings with the two settings it reads value objects best under.
/// </para>
/// <para>
/// A value it refuses is a <see cref="JsonSerializationException"/>, the exception Newtonsoft.Json's own converters
/// throw, carrying the code of the rule in <see cref="Exception.Data"/> under
/// <see cref="ValueObjectErrors.ErrorCodeKey"/>, where <see cref="ValueObjectErrors.TryGetCode"/> reads it: the rule's
/// own code, <see cref="ValueObjectErrorCodes.NotParsable"/> for a token that is not of the underlying type at all, and
/// <see cref="ValueObjectErrorCodes.Required"/> for a <c>null</c> read into a value object that cannot be
/// <see langword="null"/>. A value object written by hand over a type the generator does not support has its value
/// read by Newtonsoft.Json, whose own exception carries no code.
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
        [typeof(sbyte)] = Wire.Integer<sbyte>(Utf8Parser.TryParse),
        [typeof(byte)] = Wire.Integer<byte>(Utf8Parser.TryParse),
        [typeof(short)] = Wire.Integer<short>(Utf8Parser.TryParse),
        [typeof(ushort)] = Wire.Integer<ushort>(Utf8Parser.TryParse),
        [typeof(int)] = Wire.Integer<int>(Utf8Parser.TryParse),
        [typeof(uint)] = Wire.Integer<uint>(Utf8Parser.TryParse),
        [typeof(long)] = Wire.Integer<long>(Utf8Parser.TryParse),
        [typeof(ulong)] = Wire.Integer<ulong>(Utf8Parser.TryParse),

        // No JSON consumer holds a 128-bit integer in a number without losing precision.
        [typeof(Int128)] = new(JsonToken.String, "D"),
        [typeof(UInt128)] = new(JsonToken.String, "D"),
        [typeof(decimal)] = Wire.Real<decimal>(Utf8Parser.TryParse),
        [typeof(double)] = Wire.Real<double>(Utf8Parser.TryParse),
        [typeof(float)] = Wire.Real<float>(Utf8Parser.TryParse),
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
    /// <remarks>
    /// <para>
    /// The value is read as the System.Text.Json converter the generator emits reads it: from the kind of token it
    /// writes, and through the value object's rules, so a rejection says which rule refused it.
    /// </para>
    /// <para>
    /// A value written as a number is read from a string as well, as System.Text.Json reads one under
    /// <c>JsonNumberHandling.AllowReadingFromString</c>: the whole text, with no white space, no group separator and no
    /// culture. That is how Newtonsoft.Json writes a numeric value object without this converter, through its type
    /// converter, so data stored that way still reads once the converter is added.
    /// </para>
    /// <para>
    /// Newtonsoft.Json reads the token under the serializer's settings before the converter sees it. Under its
    /// default <see cref="DateParseHandling"/>, a string that looks like a date becomes a <see cref="DateTime"/>,
    /// converted to local time when the text carries an offset; where that date no longer says what the text said,
    /// the value is refused rather than rebuilt into something else. A <see cref="DateTimeOffset"/> value object
    /// therefore refuses any text with an explicit offset, its own output included, since it is written with its
    /// offset, <c>+00:00</c> for UTC. <see cref="DateParseHandling.None"/> keeps the text, and reads it back.
    /// </para>
    /// <para>
    /// Under <see cref="FloatParseHandling.Decimal"/>, every number with a fraction or an exponent becomes a
    /// <see cref="decimal"/>, which keeps every digit of a <see cref="decimal"/> value object. A
    /// <see cref="double"/> or <see cref="float"/> value object beyond the range of a <see cref="decimal"/> then makes
    /// the reader throw, and one smaller than its 28 decimal places loses the digits past them, all of them below
    /// about 10^-28, where it reads as zero.
    /// </para>
    /// </remarks>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(objectType);
        ArgumentNullException.ThrowIfNull(serializer);

        if (reader.TokenType == JsonToken.Null)
        {
            return Nullable.GetUnderlyingType(objectType) is not null
                ? null
                : throw Refusal($"Cannot convert null to '{objectType.Name}'.", ValueObjectErrorCodes.Required);
        }

        var descriptor = Resolve(objectType);

        if (!Wires.TryGetValue(descriptor.ValueType, out var wire))
        {
            // A value object written by hand over a type the generator does not support: its value is read the way
            // Newtonsoft.Json reads that type, as the general-purpose System.Text.Json converter does.
            return Create(descriptor, serializer.Deserialize(reader, descriptor.ValueType));
        }

        return wire.Token switch
        {
            JsonToken.String => ReadText(reader, descriptor),
            JsonToken.Boolean => Create(descriptor, ReadBoolean(reader, descriptor)),
            _ => Create(descriptor, ReadNumber(reader, descriptor, wire)),
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// The value is written as the System.Text.Json converter the generator emits writes it: a number or a boolean
    /// as such, and anything else as a string, <see cref="Int128"/> and <see cref="UInt128"/> included, in the same
    /// round-trip form. The serializer's date settings do not apply, so that both serializers write the same text.
    /// A whole <see cref="decimal"/>, <see cref="double"/> or <see cref="float"/> is the one difference:
    /// Newtonsoft.Json writes it with a fraction, as the same value. An instance equal to the default whose value the
    /// value object rejects is refused with a <see cref="JsonSerializationException"/> naming the rule, as the
    /// System.Text.Json converter refuses it, rather than written for a reader to refuse.
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
        if (descriptor.ValidateWrite(value) is { IsValid: false } refusal)
        {
            throw Refusal(
                $"The value to write is not a valid {descriptor.ValueObjectType.Name}: {refusal.ErrorMessage}",
                refusal.ErrorCode);
        }

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
    /// Reads a value System.Text.Json writes as a string, from the text of a string token.
    /// </summary>
    /// <param name="reader">Reader positioned on the token.</param>
    /// <param name="descriptor">Value object being read.</param>
    /// <returns>The value object.</returns>
    private static object ReadText(JsonReader reader, ValueObjectDescriptor descriptor)
    {
        var type = descriptor.ValueType;

        switch (reader.Value)
        {
            // A DateTime is read as System.Text.Json reads one: UTC stays UTC, an offset is converted to local time,
            // and no zone leaves the kind unspecified. Text that is not a date falls to the value object's parser,
            // which says so.
            case string text when type == typeof(DateTime)
                && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime):
                return Create(descriptor, dateTime);

            case string text:
                return descriptor.TryParse(text, CultureInfo.InvariantCulture, out var parsed, out var validation)
                    ? parsed!
                    : throw Invalid(descriptor, validation);

            // Newtonsoft.Json read the string as a date. The same kind of date is the same value; a DateTime in UTC,
            // or of no zone, gives the DateTimeOffset System.Text.Json reads from the same text. A DateTime in local
            // time is an offset Newtonsoft.Json converted away, and any other date stands for a text that is lost.
            case DateTime date when type == typeof(DateTime):
                return Create(descriptor, date);

            case DateTimeOffset instant when type == typeof(DateTimeOffset):
                return Create(descriptor, instant);

            case DateTime { Kind: not DateTimeKind.Local } date when type == typeof(DateTimeOffset):
                return Create(descriptor, new DateTimeOffset(date));

            case DateTime or DateTimeOffset:
                throw Refusal(
                    "Newtonsoft.Json read the string as a date before the converter saw it, and "
                    + $"{descriptor.ValueObjectType.Name} cannot be read back from that date. "
                    + "Set DateParseHandling to None in the serializer settings.",
                    ValueObjectErrorCodes.NotParsable);

            default:
                throw Expected(descriptor, "string", reader);
        }
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
    /// Reads a number as System.Text.Json reads it: from a number token, or from a string as it reads one under
    /// <c>JsonNumberHandling.AllowReadingFromString</c>, with no fraction for an integral type, and within the range
    /// of the type.
    /// </summary>
    /// <param name="reader">Reader positioned on the token.</param>
    /// <param name="descriptor">Value object being read.</param>
    /// <param name="wire">How the underlying type travels.</param>
    /// <returns>The underlying value.</returns>
    private static object ReadNumber(JsonReader reader, ValueObjectDescriptor descriptor, Wire wire)
    {
        var number = reader.TokenType switch
        {
            JsonToken.Integer => wire.Parse!(TokenText(reader)),

            // A fraction or an exponent is no integer.
            JsonToken.Float => wire.Token == JsonToken.Float ? wire.Parse!(TokenText(reader)) : null,

            // A number written as text, which Newtonsoft.Json without this converter writes for every value object.
            JsonToken.String => wire.ParseQuoted!((string)reader.Value!),
            _ => throw Expected(descriptor, "number", reader),
        };

        return number ?? throw Refusal(
            $"The value could not be read as {descriptor.ValueObjectType.Name}.",
            ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// Gives the invariant text of a number token.
    /// </summary>
    /// <remarks>
    /// Newtonsoft.Json has already turned the token into a long, a ulong or a BigInteger, or, under the serializer's
    /// FloatParseHandling, into a double or a decimal. The invariant text of each is the token's own digits, but for a
    /// double, whose shortest round-trip text is the closest to them it left: a decimal is never read through a
    /// double's fifteen digits.
    /// </remarks>
    /// <param name="reader">Reader positioned on the token.</param>
    /// <returns>The text.</returns>
    private static string TokenText(JsonReader reader) => Convert.ToString(reader.Value, CultureInfo.InvariantCulture)!;

    /// <summary>
    /// Builds the value object from its underlying value, through its rules.
    /// </summary>
    /// <param name="descriptor">Value object being read.</param>
    /// <param name="raw">Underlying value.</param>
    /// <returns>The value object.</returns>
    private static object Create(ValueObjectDescriptor descriptor, object? raw)
        => descriptor.TryCreate(raw, out var created, out var validation) ? created! : throw Invalid(descriptor, validation);

    private static JsonSerializationException Invalid(ValueObjectDescriptor descriptor, ValidationResult validation)
        => Refusal(
            $"The value is not a valid {descriptor.ValueObjectType.Name}: {validation.ErrorMessage}",
            validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable);

    private static JsonSerializationException Expected(ValueObjectDescriptor descriptor, string token, JsonReader reader)
        => Refusal(
            $"Expected a JSON {token} for {descriptor.ValueObjectType.Name} but found {reader.TokenType}.",
            ValueObjectErrorCodes.NotParsable);

    /// <summary>
    /// Builds the exception refusing a value, carrying the code of the rule where
    /// <see cref="ValueObjectErrors.TryGetCode"/> reads it.
    /// </summary>
    /// <param name="message">The message, naming the value object and the rule, never the value.</param>
    /// <param name="code">The code of the rule.</param>
    /// <returns>The exception to throw.</returns>
    private static JsonSerializationException Refusal(string message, string code)
    {
        var exception = new JsonSerializationException(message);
        exception.Data[ValueObjectErrors.ErrorCodeKey] = code;

        return exception;
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
    /// <param name="ParseQuoted">
    /// For a number, parses the text of a string as System.Text.Json parses a number written as text, giving
    /// <see langword="null"/> for text that is not such a number or that the type cannot hold.
    /// </param>
    private sealed record Wire(
        JsonToken Token,
        string? Format = null,
        Func<string, object?>? Parse = null,
        Func<string, object?>? ParseQuoted = null)
    {
        public static Wire Integer<T>(Utf8Parse<T> parse)
            where T : struct, INumberBase<T>
            => new(
                JsonToken.Integer,
                Parse: static text => Number<T>(text, NumberStyles.AllowLeadingSign),
                ParseQuoted: text => Quoted(text, parse));

        public static Wire Real<T>(Utf8Parse<T> parse)
            where T : struct, INumberBase<T>
            => new(
                JsonToken.Float,
                Parse: static text => Number<T>(text, NumberStyles.Float),
                ParseQuoted: text => Quoted(text, parse));

        private static object? Number<T>(string text, NumberStyles styles)
            where T : struct, INumberBase<T>
            => T.TryParse(text, styles, CultureInfo.InvariantCulture, out var value) && T.IsFinite(value) ? value : null;

        /// <summary>
        /// Parses text as System.Text.Json parses a number written as a string: its UTF-8 bytes, through
        /// <see cref="Utf8Parser"/>, which has to consume them whole. A real the parser reads as an infinity, from
        /// digits beyond its range, is refused as System.Text.Json refuses it, and so are <c>NaN</c> and the
        /// infinities spelled out, which the converter reads from no token either.
        /// </summary>
        private static object? Quoted<T>(string text, Utf8Parse<T> parse)
            where T : struct, INumberBase<T>
        {
            var bytes = Encoding.UTF8.GetBytes(text);

            return parse(bytes, out var value, out var consumed, default) && consumed == bytes.Length && T.IsFinite(value)
                ? value
                : null;
        }
    }

    /// <summary>
    /// The shape of the <see cref="Utf8Parser"/> overload of one number type.
    /// </summary>
    private delegate bool Utf8Parse<T>(ReadOnlySpan<byte> source, out T value, out int bytesConsumed, char standardFormat);
}
