using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
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

        // Whatever the generator inferred about the wrapper type is wrong by construction: it is the underlying
        // value that goes on the wire.
        schema.Properties?.Clear();
        schema.Required?.Clear();
        schema.Type = MapType(descriptor.ValueType);
        schema.Format = declared.Format;
        schema.Pattern = declared.Pattern;

        if (declared.MinLength is { } minLength)
        {
            schema.MinLength = minLength;
        }

        if (declared.MaxLength is { } maxLength)
        {
            schema.MaxLength = maxLength;
        }

        var options = context.JsonTypeInfo.Options;
        string? bounds = null;
        if (schema.Type is JsonSchemaType.Integer or JsonSchemaType.Number)
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
        else if (declared.Minimum is not null || declared.Maximum is not null)
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

        return Task.CompletedTask;
    }

    /// <summary>
    /// Publishes the bounds of a value object written as a JSON string, which <c>minimum</c> and <c>maximum</c> cannot
    /// carry.
    /// </summary>
    /// <param name="schema">The schema, of type <c>string</c>.</param>
    /// <param name="declared">The declared rules.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="options">The options the document describes the wire with.</param>
    /// <returns>The sentence stating the bounds, for the description.</returns>
    /// <remarks>
    /// JSON Schema applies <c>minimum</c> and <c>maximum</c> to numbers only, so on a string, a 128-bit integer, a
    /// character or a date, they would be in the document, enforced by the type, and ignored by every client. The bounds
    /// go to <c>x-minimum</c> and <c>x-maximum</c> instead, in the form the type writes them, for tools that read
    /// extensions, and to a sentence in the description, for people.
    /// </remarks>
    private static string DescribeBounds(
        OpenApiSchema schema,
        ValueObjectSchema declared,
        ValueObjectDescriptor descriptor,
        JsonSerializerOptions options)
    {
        var minimum = declared.Minimum is { } low ? WriteText(low, descriptor, options) : null;
        var maximum = declared.Maximum is { } high ? WriteText(high, descriptor, options) : null;

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
    /// Writes text declared on the type, an example or a bound, as the type writes the value in JSON.
    /// </summary>
    /// <param name="text">The text, as declared.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="options">The options the document describes the wire with.</param>
    /// <returns>The value as JSON.</returns>
    /// <remarks>
    /// The text is parsed the way the type parses text, then written by the type's own converter, so that a client or
    /// a mock server checking an example against the schema finds a number where the schema says number. Text the
    /// type refuses, as an example nothing checks when the type compiles may be, is written as it was declared.
    /// </remarks>
    private static JsonNode WriteText(string text, ValueObjectDescriptor descriptor, JsonSerializerOptions options)
        => descriptor.TryParse(text, CultureInfo.InvariantCulture, out var parsed, out _)
            ? JsonSerializer.SerializeToNode(parsed, options.GetTypeInfo(descriptor.ValueObjectType))!
            : JsonValue.Create(text);

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
    /// value the type parses. One it cannot parse, and anything a hand-made schema holds, is written as its text.
    /// </remarks>
    private static JsonNode WriteKnownValue(object value, ValueObjectDescriptor descriptor, JsonTypeInfo typeInfo)
        => descriptor.ValueType.IsInstanceOfType(value)
            ? JsonSerializer.SerializeToNode(descriptor.CreateUnchecked(value), typeInfo)!
            : JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture))!;

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

    private static JsonSchemaType MapType(Type valueType) => Type.GetTypeCode(valueType) switch
    {
        TypeCode.Boolean => JsonSchemaType.Boolean,
        TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16
            or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => JsonSchemaType.Integer,
        TypeCode.Decimal or TypeCode.Double or TypeCode.Single => JsonSchemaType.Number,
        _ => JsonSchemaType.String,
    };
}
