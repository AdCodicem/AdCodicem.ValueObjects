using System.Globalization;
using System.Text.Json.Nodes;
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

        if (declared.Minimum is { } minimum && decimal.TryParse(minimum, CultureInfo.InvariantCulture, out var min))
        {
            schema.Minimum = min.ToString(CultureInfo.InvariantCulture);
        }

        if (declared.Maximum is { } maximum && decimal.TryParse(maximum, CultureInfo.InvariantCulture, out var max))
        {
            schema.Maximum = max.ToString(CultureInfo.InvariantCulture);
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
            schema.Enum = [.. declared.KnownValues.Select(ToJsonNode)];
        }

        return Task.CompletedTask;
    }

    private static JsonSchemaType MapType(Type valueType) => Type.GetTypeCode(valueType) switch
    {
        TypeCode.Boolean => JsonSchemaType.Boolean,
        TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16
            or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => JsonSchemaType.Integer,
        TypeCode.Decimal or TypeCode.Double or TypeCode.Single => JsonSchemaType.Number,
        _ => JsonSchemaType.String,
    };

    private static JsonNode ToJsonNode(object value) => value switch
    {
        string text => JsonValue.Create(text),
        bool boolean => JsonValue.Create(boolean),
        decimal number => JsonValue.Create(number),
        double number => JsonValue.Create(number),
        float number => JsonValue.Create(number),
        long number => JsonValue.Create(number),
        int number => JsonValue.Create(number),
        _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
    };
}
