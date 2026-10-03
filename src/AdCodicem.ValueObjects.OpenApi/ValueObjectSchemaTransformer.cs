using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AdCodicem.ValueObjects.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AdCodicem.ValueObjects.OpenApi;

/// <summary>
/// Documents a value object as its underlying type rather than as an object with a <c>Value</c> property.
/// </summary>
/// <remarks>
/// <para>
/// Schema generation is driven by <c>JsonTypeInfo</c>, and a type carrying a custom converter is opaque to it,
/// so a value object would otherwise appear as an empty schema. This transformer fills it in from the
/// <see cref="ValueObjectSchema"/> the generator captured, which means the pattern, bounds, length and accepted
/// values published in the document are literally the ones the type enforces — they cannot drift.
/// </para>
/// <para>
/// A value object written by hand is documented as well, from its annotation, through the descriptor the registry
/// builds by reflection when nothing registered the type, as the model binder and the FluentValidation rules resolve
/// it. It runs while the document is built, not per request.
/// </para>
/// <para>
/// A value object over a number honours <see cref="JsonSerializerOptions.NumberHandling"/> as the built-in converter of
/// its underlying type does, and is documented as System.Text.Json documents that type under the same options: as a
/// number or a string matching a numeric pattern when a number may be written or read as text, and, over a
/// <see cref="double"/> or a <see cref="float"/>, with the named literals its bounds let through.
/// </para>
/// </remarks>
public sealed class ValueObjectSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        // A value object written by hand that nothing registered binds and validates through a descriptor built by
        // reflection, and is documented through the same one.
        var type = context.JsonTypeInfo.Type;
        if (!ValueObjectRegistry.TryResolve(type, out var descriptor))
        {
            return Task.CompletedTask;
        }

        var declared = descriptor.Schema;
        var options = context.JsonTypeInfo.Options;
        var jsonType = MapType(descriptor.ValueType);
        var numeric = jsonType is JsonSchemaType.Integer or JsonSchemaType.Number;

        // A number the options let be written or read as text is a number or a string, as System.Text.Json documents
        // the bare number under the same options, the string held to the form a number is written in.
        var asText = numeric
                     && (options.NumberHandling & (JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)) != 0;

        // Whatever the generator inferred about the wrapper type is wrong by construction: it is the underlying
        // value that goes on the wire.
        schema.Properties?.Clear();
        schema.Required?.Clear();
        schema.Type = asText ? jsonType | JsonSchemaType.String : jsonType;
        schema.Format = declared.Format;
        schema.Pattern = declared.Pattern ?? WirePattern(descriptor.ValueType, asText);

        if (declared.MinLength is { } minLength)
        {
            schema.MinLength = minLength;
        }

        if (declared.MaxLength is { } maxLength)
        {
            schema.MaxLength = maxLength;
        }

        string? bounds = null;
        if (numeric)
        {
            if (declared.Minimum is { } minimum && FormatBound(minimum, descriptor.ValueType) is { } min)
            {
                schema.Minimum = min;
            }

            if (declared.Maximum is { } maximum && FormatBound(maximum, descriptor.ValueType) is { } max)
            {
                schema.Maximum = max;
            }
        }

        // minimum and maximum hold a number only: a value written as a string carries its bounds otherwise too. One
        // that may only be read from a string is still written as a number, which they describe.
        var writtenAsText = !numeric || (options.NumberHandling & JsonNumberHandling.WriteAsString) != 0;
        if (writtenAsText && (declared.Minimum is not null || declared.Maximum is not null))
        {
            bounds = DescribeBounds(schema, declared, descriptor, options);
        }

        if (!string.IsNullOrEmpty(declared.Description))
        {
            schema.Description = declared.Description;
        }

        if (bounds is not null)
        {
            schema.Description = string.IsNullOrEmpty(schema.Description) ? bounds : $"{schema.Description}\n\n{bounds}";
        }

        if (!string.IsNullOrEmpty(declared.Example))
        {
            schema.Examples = [WriteText(declared.Example, descriptor, options)];
        }

        if (declared.IsClosedValueSet && !declared.KnownValues.IsEmpty)
        {
            var typeInfo = context.JsonTypeInfo.Options.GetTypeInfo(descriptor.ValueObjectType);
            schema.Enum = [.. declared.KnownValues.Select(value => WriteKnownValue(value, descriptor, typeInfo))];
        }

        if ((options.NumberHandling & JsonNumberHandling.AllowNamedFloatingPointLiterals) != 0
            && (descriptor.ValueType == typeof(double) || descriptor.ValueType == typeof(float))
            && NamedLiterals(declared) is { Count: > 0 } literals)
        {
            AllowNamedLiterals(schema, literals);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Lists the named literals of a real a value object's bounds let through: <c>NaN</c>, which a bound compares
    /// false with and so refuses, only when it has none, and each infinity when no bound stands on its side.
    /// </summary>
    /// <param name="declared">The declared rules.</param>
    /// <returns>The literals, as System.Text.Json writes them.</returns>
    private static List<JsonNode> NamedLiterals(ValueObjectSchema declared)
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
    /// Documents a real as System.Text.Json documents one under <c>AllowNamedFloatingPointLiterals</c>: the number, or
    /// one of the named literals.
    /// </summary>
    /// <param name="schema">The schema, describing the number.</param>
    /// <param name="literals">The named literals the value object lets through.</param>
    /// <remarks>
    /// What describes the number moves to the first alternative; what describes the value object as a whole, its
    /// description, its example and its known values, stays where it is.
    /// </remarks>
    private static void AllowNamedLiterals(OpenApiSchema schema, List<JsonNode> literals)
    {
        var number = new OpenApiSchema
        {
            Type = schema.Type,
            Format = schema.Format,
            Pattern = schema.Pattern,
            Minimum = schema.Minimum,
            Maximum = schema.Maximum,
        };

        schema.Type = null;
        schema.Format = null;
        schema.Pattern = null;
        schema.Minimum = null;
        schema.Maximum = null;
        schema.AnyOf = [number, new OpenApiSchema { Enum = literals }];
    }

    /// <summary>
    /// Gets the pattern a value object that declares none is held to on the wire, as System.Text.Json documents its
    /// underlying type: a number written as text, and a duration, which no <c>format</c> describes.
    /// </summary>
    /// <param name="valueType">Underlying type of the value object.</param>
    /// <param name="asText">Whether a number may be written or read as text.</param>
    /// <returns>The pattern, or <see langword="null"/> for a value the type alone describes.</returns>
    /// <remarks>
    /// A duration is written in the invariant constant form <c>[-][d.]hh:mm:ss[.fffffff]</c>. The <c>duration</c>
    /// format of JSON Schema is ISO 8601, <c>PT1H30M</c>, which the type does not read: a client taking the document
    /// at its word would send a value the server refuses.
    /// </remarks>
    private static string? WirePattern(Type valueType, bool asText)
    {
        if (asText)
        {
            return NumberPattern(valueType);
        }

        return valueType == typeof(TimeSpan) ? DurationPattern : null;
    }

    /// <summary>
    /// The pattern System.Text.Json documents a <see cref="TimeSpan"/> with: its invariant constant form.
    /// </summary>
    private const string DurationPattern = @"^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$";

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
    /// Publishes the bounds of a value object that may be written as a JSON string, which <c>minimum</c> and
    /// <c>maximum</c> cannot carry.
    /// </summary>
    /// <param name="schema">The schema, whose type includes <c>string</c>.</param>
    /// <param name="declared">The declared rules.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="options">The options the document describes the wire with.</param>
    /// <returns>The sentence stating the bounds, for the description.</returns>
    /// <remarks>
    /// JSON Schema applies <c>minimum</c> and <c>maximum</c> to numbers only, so on a string, a 128-bit integer, a
    /// character, a date, or a number written as text, they would be in the document, enforced by the type, and ignored
    /// by every client. The bounds go to <c>x-minimum</c> and <c>x-maximum</c> instead, in the form the type writes them,
    /// for tools that read extensions, and to a sentence in the description, for people. A bound is published as it is
    /// declared and enforced: read as the underlying value, then written, never normalized or validated as an input
    /// would be.
    /// </remarks>
    private static string DescribeBounds(
        OpenApiSchema schema,
        ValueObjectSchema declared,
        ValueObjectDescriptor descriptor,
        JsonSerializerOptions options)
    {
        var minimum = declared.Minimum is { } low ? WriteBound(low, descriptor, options) : null;
        var maximum = declared.Maximum is { } high ? WriteBound(high, descriptor, options) : null;

        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        if (minimum is not null)
        {
            schema.Extensions["x-minimum"] = new JsonNodeExtension(minimum);
        }

        if (maximum is not null)
        {
            schema.Extensions["x-maximum"] = new JsonNodeExtension(maximum);
        }

        return (minimum, maximum) switch
        {
            ({ } from, { } to) => $"Between {Quote(from)} and {Quote(to)}, inclusive.",
            ({ } from, null) => $"At least {Quote(from)}.",
            _ => $"At most {Quote(maximum!)}.",
        };

        static string Quote(JsonNode value)
            => value is JsonValue text && text.TryGetValue<string>(out var written) ? written : value.ToJsonString();
    }

    /// <summary>
    /// Writes the example declared on the type as the type writes the value in JSON.
    /// </summary>
    /// <param name="text">The example, as declared.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="options">The options the document describes the wire with.</param>
    /// <returns>The value as JSON.</returns>
    /// <remarks>
    /// The example is an input, parsed the way the type parses text, then written by the type's own converter, so that
    /// a client or a mock server checking it against the schema finds a number where the schema says number. An example
    /// the type refuses, which nothing checks when the type compiles, or one its converter cannot write under the
    /// options, as a real cannot write <c>NaN</c> without the named literals, is written as it was declared.
    /// </remarks>
    private static JsonNode WriteText(string text, ValueObjectDescriptor descriptor, JsonSerializerOptions options)
        => descriptor.TryParse(text, CultureInfo.InvariantCulture, out var parsed, out _)
            ? Write(parsed!, options.GetTypeInfo(descriptor.ValueObjectType)) ?? JsonValue.Create(text)
            : JsonValue.Create(text);

    /// <summary>
    /// Writes a bound declared on the type as the type writes the value in JSON.
    /// </summary>
    /// <param name="text">The bound, as the schema holds it.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="options">The options the document describes the wire with.</param>
    /// <returns>The value as JSON.</returns>
    /// <remarks>
    /// The check compares the normalized value with the bound as declared, so the bound is read as the underlying value
    /// and written as it is, never normalized or validated: a normalizer converting a date to UTC would otherwise publish
    /// a bound that moves with the time zone of the server. A bound the underlying type cannot read, as a schema made by
    /// hand may hold, is written as it was declared.
    /// </remarks>
    private static JsonNode WriteBound(string text, ValueObjectDescriptor descriptor, JsonSerializerOptions options)
        => ParseUnderlying(text, descriptor.ValueType) is { } value
            ? Write(descriptor.CreateUnchecked(value), options.GetTypeInfo(descriptor.ValueObjectType)) ?? JsonValue.Create(text)
            : JsonValue.Create(text);

    /// <summary>
    /// Writes a value object through its converter, or answers <see langword="null"/> when the converter cannot write
    /// it under the options, rather than failing the whole document.
    /// </summary>
    /// <param name="valueObject">The value object.</param>
    /// <param name="typeInfo">Its contract, under the options the document describes the wire with.</param>
    /// <returns>The value as JSON, or <see langword="null"/>.</returns>
    private static JsonNode? Write(object valueObject, JsonTypeInfo typeInfo)
    {
        try
        {
            return JsonSerializer.SerializeToNode(valueObject, typeInfo);
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

    /// <summary>
    /// Writes one known value of a closed set as the type writes it in JSON.
    /// </summary>
    /// <param name="value">The known value, as the schema holds it.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="typeInfo">The value object's contract, under the options the document describes the wire with.</param>
    /// <returns>The value as JSON.</returns>
    /// <remarks>
    /// A value of the underlying type is written by the type's own converter, so that a client checks a payload
    /// against exactly what the type writes: a number of any width as a number, a date in its round-trip form. A
    /// generated registration guarantees that type, and a schema read from an annotation holds it for every known
    /// value the type parses. A value of any other type, from an annotation the type cannot parse or from a schema made
    /// by hand, and one the converter cannot write under the options, is written as its text.
    /// </remarks>
    private static JsonNode WriteKnownValue(object value, ValueObjectDescriptor descriptor, JsonTypeInfo typeInfo)
        => (descriptor.ValueType.IsInstanceOfType(value) ? Write(descriptor.CreateUnchecked(value), typeInfo) : null)
           ?? JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture))!;

    /// <summary>
    /// Reads a declared bound as the number the type enforces, in the form the document writes it.
    /// </summary>
    /// <param name="bound">The bound, as declared.</param>
    /// <param name="valueType">Underlying type of the value object.</param>
    /// <returns>The number, or <see langword="null"/> when the bound is not one, as a date's is not.</returns>
    /// <remarks>
    /// A double or a float bound may be written with an exponent, and may lie beyond the range of decimal or below
    /// its precision, so it is read as a double; every other numeric type has bounds decimal carries exactly.
    /// </remarks>
    private static string? FormatBound(string bound, Type valueType)
    {
        if (valueType == typeof(double) || valueType == typeof(float))
        {
            // The document is JSON, which has no number for an infinity.
            return double.TryParse(bound, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && double.IsFinite(real)
                ? real.ToString("R", CultureInfo.InvariantCulture)
                : null;
        }

        return decimal.TryParse(bound, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact)
            ? exact.ToString(CultureInfo.InvariantCulture)
            : null;
    }

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

    private static JsonSchemaType MapType(Type valueType) => Type.GetTypeCode(valueType) switch
    {
        TypeCode.Boolean => JsonSchemaType.Boolean,
        TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16
            or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => JsonSchemaType.Integer,
        TypeCode.Decimal or TypeCode.Double or TypeCode.Single => JsonSchemaType.Number,
        _ => JsonSchemaType.String,
    };
}
