using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Shared;

/// <summary>
/// What the rules declared on a value object become in a JSON Schema: its JSON type, the pattern of its wire form, its
/// bounds, its example and its known values, each written as the type writes the value.
/// </summary>
/// <remarks>
/// <para>
/// This file is linked into every package that describes a value object as a schema, the OpenAPI schema transformer
/// and the System.Text.Json schema transform among them, so that they describe a value object alike from the same
/// <see cref="ValueObjectSchema"/>: each compiles its own internal copy, and none depends on another for it. What is
/// particular to a host, the shape of its schema object and the extensions it writes, stays with the host.
/// </para>
/// <para>
/// It reads no annotation and closes no generic type: everything comes from the descriptor, so it needs neither
/// reflection nor dynamic code.
/// </para>
/// </remarks>
internal static class ValueObjectSchemaKeywords
{
    /// <summary>
    /// The JSON type of a value written as a JSON string.
    /// </summary>
    internal const string String = "string";

    /// <summary>
    /// The JSON type of an integer written as a JSON number.
    /// </summary>
    internal const string Integer = "integer";

    /// <summary>
    /// The JSON type of a real written as a JSON number.
    /// </summary>
    internal const string Number = "number";

    /// <summary>
    /// The JSON type of a value written as <c>true</c> or <c>false</c>.
    /// </summary>
    internal const string Boolean = "boolean";

    /// <summary>
    /// The pattern System.Text.Json documents a <see cref="TimeSpan"/> with: its invariant constant form.
    /// </summary>
    internal const string DurationPattern = @"^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$";

    /// <summary>
    /// How each underlying type is read from the text of a bound: a <see cref="DateTime"/> keeping the kind its text
    /// names, as the type's own parser reads it.
    /// </summary>
    private static readonly Dictionary<Type, Func<string, object?>> UnderlyingParsers = new()
    {
        [typeof(bool)] = Parse<bool>,
        [typeof(char)] = Parse<char>,
        [typeof(sbyte)] = Parse<sbyte>,
        [typeof(byte)] = Parse<byte>,
        [typeof(short)] = Parse<short>,
        [typeof(ushort)] = Parse<ushort>,
        [typeof(int)] = Parse<int>,
        [typeof(uint)] = Parse<uint>,
        [typeof(long)] = Parse<long>,
        [typeof(ulong)] = Parse<ulong>,
        [typeof(Int128)] = Parse<Int128>,
        [typeof(UInt128)] = Parse<UInt128>,
        [typeof(decimal)] = Parse<decimal>,
        [typeof(double)] = Parse<double>,
        [typeof(float)] = Parse<float>,
        [typeof(DateOnly)] = Parse<DateOnly>,
        [typeof(TimeOnly)] = Parse<TimeOnly>,
        [typeof(DateTimeOffset)] = Parse<DateTimeOffset>,
        [typeof(TimeSpan)] = Parse<TimeSpan>,
        [typeof(DateTime)] = static text
            => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant) ? instant : null,
    };

    /// <summary>
    /// Gets the JSON type a value object travels as: the one its underlying type is written as by the converter the
    /// generator emits.
    /// </summary>
    /// <param name="valueType">Underlying type of the value object.</param>
    /// <returns>
    /// <see cref="Boolean"/>, <see cref="Integer"/> for an integer of 64 bits at most, <see cref="Number"/> for a real,
    /// and <see cref="String"/> for everything else: text, a character, a <see cref="Guid"/>, a date or a time, and a
    /// 128-bit integer, which no JSON number reader holds without losing precision.
    /// </returns>
    internal static string TypeOf(Type valueType) => Type.GetTypeCode(valueType) switch
    {
        TypeCode.Boolean => Boolean,
        TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16
            or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => Integer,
        TypeCode.Decimal or TypeCode.Double or TypeCode.Single => Number,
        _ => String,
    };

    /// <summary>
    /// Gets the pattern a value object that declares none is held to on the wire, as System.Text.Json documents its
    /// underlying type: a number written as text, and a duration, which no <c>format</c> describes.
    /// </summary>
    /// <param name="valueType">Underlying type of the value object.</param>
    /// <param name="asText">Whether a number may be written or read as text.</param>
    /// <returns>The pattern, or <see langword="null"/> for a value the type alone describes.</returns>
    /// <remarks>
    /// A duration is written in the invariant constant form <c>[-][d.]hh:mm:ss[.fffffff]</c>. The <c>duration</c>
    /// format of JSON Schema is ISO 8601, <c>PT1H30M</c>, which the type does not read: a client taking the schema at
    /// its word would send a value the type refuses.
    /// </remarks>
    internal static string? WirePattern(Type valueType, bool asText)
    {
        if (asText)
        {
            return NumberPattern(valueType);
        }

        return valueType == typeof(TimeSpan) ? DurationPattern : null;
    }

    /// <summary>
    /// Reads a declared bound as the number the type enforces, in the form a schema writes it.
    /// </summary>
    /// <param name="bound">The bound, as declared.</param>
    /// <param name="valueType">Underlying type of the value object.</param>
    /// <returns>The number, or <see langword="null"/> when the bound is not one, as a date's is not.</returns>
    /// <remarks>
    /// A double or a float bound may be written with an exponent, and may lie beyond the range of decimal or below
    /// its precision, so it is read as a double; every other numeric type has bounds decimal carries exactly.
    /// </remarks>
    internal static string? FormatBound(string bound, Type valueType)
    {
        if (valueType == typeof(double) || valueType == typeof(float))
        {
            // A schema is JSON, which has no number for an infinity.
            return double.TryParse(bound, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && double.IsFinite(real)
                ? real.ToString("R", CultureInfo.InvariantCulture)
                : null;
        }

        return decimal.TryParse(bound, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact)
            ? exact.ToString(CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>
    /// Writes the bounds declared on the type as the type writes the value in JSON.
    /// </summary>
    /// <param name="declared">The declared rules.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="options">The options the schema describes the wire with.</param>
    /// <returns>Each bound as JSON, or <see langword="null"/> for one the type does not declare.</returns>
    internal static (JsonNode? Minimum, JsonNode? Maximum) WriteBounds(
        ValueObjectSchema declared,
        ValueObjectDescriptor descriptor,
        JsonSerializerOptions options)
        => (declared.Minimum is { } low ? WriteBound(low, descriptor, options) : null,
            declared.Maximum is { } high ? WriteBound(high, descriptor, options) : null);

    /// <summary>
    /// States bounds in a sentence, for a value <c>minimum</c> and <c>maximum</c> cannot bound: a string, a 128-bit
    /// integer, a character, a date, or a number written as text.
    /// </summary>
    /// <param name="minimum">The lower bound as the type writes it, if any.</param>
    /// <param name="maximum">The upper bound as the type writes it, required when there is no lower bound.</param>
    /// <returns>The sentence.</returns>
    internal static string BoundsSentence(JsonNode? minimum, JsonNode? maximum)
        => (minimum, maximum) switch
        {
            ({ } from, { } to) => $"Between {Quote(from)} and {Quote(to)}, inclusive.",
            ({ } from, null) => $"At least {Quote(from)}.",
            _ => $"At most {Quote(maximum!)}.",
        };

    /// <summary>
    /// Writes the example declared on the type as the type writes the value in JSON.
    /// </summary>
    /// <param name="text">The example, as declared.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="options">The options the schema describes the wire with.</param>
    /// <returns>The value as JSON.</returns>
    /// <remarks>
    /// The example is an input, parsed the way the type parses text, then written by the type's own converter, so that
    /// a client or a mock server checking it against the schema finds a number where the schema says number. An example
    /// the type refuses, which nothing checks when the type compiles, or one its converter cannot write under the
    /// options, as a real cannot write <c>NaN</c> without the named literals, is written as it was declared.
    /// </remarks>
    internal static JsonNode WriteText(string text, ValueObjectDescriptor descriptor, JsonSerializerOptions options)
        => descriptor.TryParse(text, CultureInfo.InvariantCulture, out var parsed, out _)
            ? Write(parsed!, descriptor, options) ?? JsonValue.Create(text)
            : JsonValue.Create(text);

    /// <summary>
    /// Writes one known value of a closed set as the type writes it in JSON.
    /// </summary>
    /// <param name="value">The known value, as the schema holds it.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="options">The options the schema describes the wire with.</param>
    /// <returns>The value as JSON.</returns>
    /// <remarks>
    /// A value of the underlying type is written by the type's own converter, so that a client checks a payload
    /// against exactly what the type writes: a number of any width as a number, a date in its round-trip form. A
    /// generated registration guarantees that type. A value of any other type, which a schema written by hand may hold,
    /// and one the converter cannot write under the options, is written as its text.
    /// </remarks>
    internal static JsonNode WriteKnownValue(object value, ValueObjectDescriptor descriptor, JsonSerializerOptions options)
        => (descriptor.ValueType.IsInstanceOfType(value) ? Write(descriptor.CreateUnchecked(value), descriptor, options) : null)
           ?? JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture))!;

    /// <summary>
    /// Tells whether the details of a schema's known values list its values one for one, the only case in which a name
    /// can be published beside each value.
    /// </summary>
    /// <param name="declared">The declared rules.</param>
    /// <returns>
    /// <see langword="true"/> when <see cref="ValueObjectSchema.KnownValueDetails"/> holds as many entries as
    /// <see cref="ValueObjectSchema.KnownValues"/>, each of the same value, in the same order; a schema built by hand may
    /// leave them empty or out of step, and a name beside the wrong value would mislead every reader.
    /// </returns>
    internal static bool DetailsListTheKnownValues(ValueObjectSchema declared)
    {
        var details = declared.KnownValueDetails;

        return !details.IsDefaultOrEmpty
               && details.Length == declared.KnownValues.Length
               && !details.Where((detail, index) => !Equals(detail.Value, declared.KnownValues[index])).Any();
    }

    /// <summary>
    /// Lists the named literals of a real a value object's bounds let through: <c>NaN</c>, which a bound compares
    /// false with and so refuses, only when it has none, and each infinity when no bound stands on its side.
    /// </summary>
    /// <param name="declared">The declared rules.</param>
    /// <returns>The literals, as System.Text.Json writes them.</returns>
    internal static List<JsonNode> NamedLiterals(ValueObjectSchema declared)
    {
        var literals = new List<JsonNode>(3);
        if (declared.Minimum is null && declared.Maximum is null)
        {
            literals.Add(JsonValue.Create("NaN"));
        }

        if (declared.Maximum is null)
        {
            literals.Add(JsonValue.Create("Infinity"));
        }

        if (declared.Minimum is null)
        {
            literals.Add(JsonValue.Create("-Infinity"));
        }

        return literals;
    }

    /// <summary>
    /// Gets the pattern System.Text.Json holds a number written as text to.
    /// </summary>
    /// <param name="valueType">Underlying type of the value object, a number.</param>
    /// <returns>The pattern.</returns>
    private static string NumberPattern(Type valueType) => Type.GetTypeCode(valueType) switch
    {
        TypeCode.Double or TypeCode.Single => @"^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$",
        TypeCode.Decimal => @"^-?(?:0|[1-9]\d*)(?:\.\d+)?$",
        _ => @"^-?(?:0|[1-9]\d*)$",
    };

    /// <summary>
    /// Quotes a value written in JSON in a sentence: a string as its text, anything else as its JSON.
    /// </summary>
    /// <param name="value">The value, as the type writes it.</param>
    /// <returns>Its text.</returns>
    internal static string Quote(JsonNode value)
        => value is JsonValue text && text.TryGetValue<string>(out var written) ? written : value.ToJsonString();

    /// <summary>
    /// Writes a bound declared on the type as the type writes the value in JSON.
    /// </summary>
    /// <param name="text">The bound, as the schema holds it.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="options">The options the schema describes the wire with.</param>
    /// <returns>The value as JSON.</returns>
    /// <remarks>
    /// The check compares the normalized value with the bound as declared, so the bound is read as the underlying value
    /// and written as it is, never normalized or validated: a normalizer converting a date to UTC would otherwise publish
    /// a bound that moves with the time zone of the server. A bound the underlying type cannot read, as a schema made by
    /// hand may hold, is written as it was declared.
    /// </remarks>
    private static JsonNode WriteBound(string text, ValueObjectDescriptor descriptor, JsonSerializerOptions options)
        => ParseUnderlying(text, descriptor.ValueType) is { } value
            ? Write(descriptor.CreateUnchecked(value), descriptor, options) ?? JsonValue.Create(text)
            : JsonValue.Create(text);

    /// <summary>
    /// Writes a value object through its converter, or answers <see langword="null"/> when the converter cannot write
    /// it under the options, rather than failing the whole schema.
    /// </summary>
    /// <param name="valueObject">The value object.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="options">The options the schema describes the wire with.</param>
    /// <returns>The value as JSON, or <see langword="null"/>.</returns>
    /// <remarks>
    /// The options may hold no contract for the value object: a resolver generated for the types an application
    /// serializes knows nothing of one that only ever is a route or query parameter.
    /// </remarks>
    private static JsonNode? Write(object valueObject, ValueObjectDescriptor descriptor, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.SerializeToNode(valueObject, options.GetTypeInfo(descriptor.ValueObjectType));
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a bound as the underlying value, in the invariant form the schema holds it in.
    /// </summary>
    /// <param name="text">The bound.</param>
    /// <param name="valueType">The underlying type.</param>
    /// <returns>
    /// The value, or <see langword="null"/> when the type cannot read the text, or takes no bound, as text, written
    /// as it is read, does not.
    /// </returns>
    private static object? ParseUnderlying(string text, Type valueType)
        => UnderlyingParsers.TryGetValue(valueType, out var parse) ? parse(text) : null;

    /// <summary>
    /// Reads text as a value of a type that parses itself, in the invariant culture.
    /// </summary>
    private static object? Parse<TValue>(string text)
        where TValue : IParsable<TValue>
        => TValue.TryParse(text, CultureInfo.InvariantCulture, out var value) ? value : null;
}
