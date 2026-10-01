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
/// It runs while the document is built, not per request.
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

        var type = context.JsonTypeInfo.Type;
        if (!ValueObjectRegistry.TryGet(type, out var descriptor))
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

        if (declared.Minimum is { } minimum && FormatBound(minimum, descriptor.ValueType) is { } min)
        {
            schema.Minimum = min;
        }

        if (declared.Maximum is { } maximum && FormatBound(maximum, descriptor.ValueType) is { } max)
        {
            schema.Maximum = max;
        }

        if (!string.IsNullOrEmpty(declared.Description))
        {
            schema.Description = declared.Description;
        }

        if (!string.IsNullOrEmpty(declared.Example))
        {
            schema.Examples = [JsonValue.Create(declared.Example)];
        }

        if (declared.IsClosedValueSet && !declared.KnownValues.IsEmpty)
        {
            var typeInfo = context.JsonTypeInfo.Options.GetTypeInfo(descriptor.ValueObjectType);
            schema.Enum = [.. declared.KnownValues.Select(value => WriteKnownValue(value, descriptor, typeInfo))];
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes one known value of a closed set as the type writes it in JSON.
    /// </summary>
    /// <param name="value">The known value, as the schema holds it.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="typeInfo">The value object's contract, under the options the document describes the wire with.</param>
    /// <returns>The value as JSON.</returns>
    /// <remarks>
    /// A value of the underlying type is written by the type's own converter, so that a client checks a payload
    /// against exactly what the type writes: a number of any width as a number, a date in its round-trip form. Only a
    /// generated registration guarantees that type. A schema read from an annotation by reflection holds what the
    /// attribute was given, such as a decimal written as text, and a hand-made one holds anything: such a value is
    /// written as its text.
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
    /// The attribute reads a decimal, double or float bound as a floating-point literal, so an exponent is part of
    /// the syntax. A double or a float bound may lie beyond the range of decimal or below its precision, so it is
    /// read as a double; every other numeric type has bounds decimal carries exactly.
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
