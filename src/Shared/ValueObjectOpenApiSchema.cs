using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Metadata;
using Microsoft.OpenApi;

namespace AdCodicem.ValueObjects.Shared;

/// <summary>
/// Describes a value object on the object model of Microsoft.OpenApi 2: as its underlying value, carrying the rules
/// declared on it.
/// </summary>
/// <remarks>
/// <para>
/// This file is linked into every package that describes a value object in an OpenAPI document built on that object
/// model, the schema transformer of AdCodicem.ValueObjects.OpenApi and the Swashbuckle filters of
/// AdCodicem.ValueObjects.Swashbuckle, so that both documents describe a value object alike: each compiles its own
/// internal copy, and none depends on another for it. What is particular to a host stays with it: how it finds the
/// value object, the options it describes the wire with, the name it gives the enumeration of a closed set, and its
/// parameters, collections and dictionaries.
/// </para>
/// <para>
/// What each rule becomes is read from <see cref="ValueObjectSchemaKeywords"/>, which the JSON Schema of
/// AdCodicem.ValueObjects.Json shares. Like it, this file reads no annotation and closes no generic type.
/// </para>
/// </remarks>
internal static class ValueObjectOpenApiSchema
{
    /// <summary>
    /// Describes a value object as its underlying value, carrying the rules declared on it.
    /// </summary>
    /// <param name="schema">The schema to fill in.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="options">The options the document describes the wire with.</param>
    /// <param name="enumName">Gets the name of the enumeration of a closed set, for <c>x-ms-enum</c>; asked only for
    /// one.</param>
    /// <param name="asKey">Whether the schema describes the key of a dictionary, which is written as text whatever the
    /// underlying type.</param>
    /// <remarks>
    /// Describing a schema a second time leaves it as the first time did: ASP.NET Core hands the element of a
    /// collection back to the transformers after this one gave it its schema. A schema that allows <c>null</c> keeps
    /// allowing it, and the <c>enum</c> of a closed set then lists <c>null</c> after the known values, or it would
    /// refuse the <c>null</c> its type allows.
    /// </remarks>
    internal static void Describe(
        OpenApiSchema schema,
        ValueObjectDescriptor descriptor,
        JsonSerializerOptions options,
        Func<string> enumName,
        bool asKey = false)
    {
        var declared = descriptor.Schema;

        // A key is the text the converter writes it in, a number's included, which minimum and maximum cannot bound.
        var jsonType = asKey ? JsonSchemaType.String : MapType(descriptor.ValueType);
        var numeric = jsonType is JsonSchemaType.Integer or JsonSchemaType.Number;

        // A number the options let be written or read as text is a number or a string, as System.Text.Json documents
        // the bare number under the same options, the string held to the form a number is written in.
        var asText = numeric
                     && (options.NumberHandling & (JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)) != 0;

        // Whatever the generator inferred about the wrapper type is wrong by construction: it is the underlying
        // value that goes on the wire.
        schema.Properties?.Clear();
        schema.Required?.Clear();
        var nullable = AllowsNull(schema);
        schema.Type = (asText ? jsonType | JsonSchemaType.String : jsonType) | (nullable ? JsonSchemaType.Null : default);
        schema.Format = declared.Format;
        schema.Pattern = declared.Pattern ?? (asKey
            ? ValueObjectSchemaKeywords.KeyPattern(descriptor.ValueType, declared)
            : ValueObjectSchemaKeywords.WirePattern(descriptor.ValueType, asText));

        var (minLength, maxLength) = ValueObjectSchemaKeywords.Lengths(declared, descriptor.ValueType);
        if (minLength is not null)
        {
            schema.MinLength = minLength;
        }

        if (maxLength is not null)
        {
            schema.MaxLength = maxLength;
        }

        string? bounds = null;
        if (numeric)
        {
            if (declared.Minimum is { } minimum && ValueObjectSchemaKeywords.FormatBound(minimum, descriptor.ValueType) is { } min)
            {
                schema.Minimum = min;
            }

            if (declared.Maximum is { } maximum && ValueObjectSchemaKeywords.FormatBound(maximum, descriptor.ValueType) is { } max)
            {
                schema.Maximum = max;
            }
        }

        // minimum and maximum hold a number only: a value written as a string carries its bounds otherwise too. One
        // that may only be read from a string is still written as a number, which they describe.
        var writtenAsText = !numeric || (options.NumberHandling & JsonNumberHandling.WriteAsString) != 0;
        if (writtenAsText && (declared.Minimum is not null || declared.Maximum is not null))
        {
            bounds = DescribeBounds(schema, declared, descriptor, options, asKey);
        }

        if (!string.IsNullOrEmpty(declared.Description))
        {
            schema.Description = declared.Description;
        }

        // Stated once, though the schema be described again.
        if (bounds is not null && schema.Description?.EndsWith(bounds, StringComparison.Ordinal) != true)
        {
            schema.Description = string.IsNullOrEmpty(schema.Description) ? bounds : $"{schema.Description}\n\n{bounds}";
        }

        if (declared.Example is { } example && example is not "")
        {
            schema.Examples = [ValueObjectSchemaKeywords.WriteExample(example, descriptor, options, asKey)];
        }

        if (declared.IsClosedValueSet && !declared.KnownValues.IsDefaultOrEmpty)
        {
            List<JsonNode> values = [.. declared.KnownValues.Select(value => ValueObjectSchemaKeywords.WriteKnownValue(value, descriptor, options, asKey))];
            schema.Enum = values;
            NameKnownValues(schema, declared, enumName());

            // null joins the values of a nullable set, or the enum would refuse the null its type allows: OpenAPI 3.0
            // asks it beside nullable, and JSON Schema checks enum beside type. The names stay those of the known
            // values alone; Microsoft.OpenApi writes its sentinel as null.
            if (nullable)
            {
                values.Add(JsonNullSentinel.JsonNull.DeepClone());
            }
        }

        if (!asKey
            && (options.NumberHandling & JsonNumberHandling.AllowNamedFloatingPointLiterals) != 0
            && (descriptor.ValueType == typeof(double) || descriptor.ValueType == typeof(float))
            && ValueObjectSchemaKeywords.NamedLiterals(declared) is { Count: > 0 } literals)
        {
            AllowNamedLiterals(schema, literals);
        }
    }

    /// <summary>
    /// Tells whether a schema allows <c>null</c>, whether it states its type itself or, a real under named literals
    /// described once already, in its first alternative.
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <returns><see langword="true"/> when the schema allows <c>null</c>.</returns>
    private static bool AllowsNull(OpenApiSchema schema)
        => (schema.Type ?? (schema.AnyOf is [OpenApiSchema number, ..] ? number.Type : null)) is { } type
           && (type & JsonSchemaType.Null) != 0;

    /// <summary>
    /// Publishes the name of each value of a closed set, and its description where one was declared, beside the
    /// <c>enum</c>, so that a client generated from the document names the members of its enumeration as the server code
    /// names its known values.
    /// </summary>
    /// <param name="schema">The schema, whose <c>enum</c> lists the values as the type writes them.</param>
    /// <param name="declared">The declared rules.</param>
    /// <param name="enumName">The name of the enumeration, for <c>x-ms-enum</c>.</param>
    /// <remarks>
    /// <para>
    /// Each extension is read by other tools: <c>x-enum-varnames</c> by openapi-generator and Scalar,
    /// <c>x-enumNames</c> by NSwag, and <c>x-ms-enum</c> by Kiota and AutoRest, which take each value's description
    /// from it too. A value in <c>x-ms-enum</c> is written as the <c>enum</c> writes it, and has a description only
    /// where one was declared. The object form of <c>x-enum-descriptions</c>, keyed by value, is never written: NSwag
    /// refuses the whole document over it.
    /// </para>
    /// <para>
    /// Nothing is written when the details do not list the values of the <c>enum</c> one for one, as a schema built by
    /// hand may not: a name beside the wrong value would mislead every client.
    /// </para>
    /// </remarks>
    private static void NameKnownValues(OpenApiSchema schema, ValueObjectSchema declared, string enumName)
    {
        if (!ValueObjectSchemaKeywords.DetailsListTheKnownValues(declared))
        {
            return;
        }

        var details = declared.KnownValueDetails;

        var varNames = new JsonArray();
        var enumNames = new JsonArray();
        var values = new JsonArray();
        for (var index = 0; index < details.Length; index++)
        {
            var detail = details[index];
            varNames.Add(JsonValue.Create(detail.Name));
            enumNames.Add(JsonValue.Create(detail.Name));

            var value = new JsonObject
            {
                ["value"] = schema.Enum![index].DeepClone(),
                ["name"] = JsonValue.Create(detail.Name),
            };
            if (!string.IsNullOrWhiteSpace(detail.Description))
            {
                value["description"] = JsonValue.Create(detail.Description);
            }

            values.Add(value);
        }

        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        schema.Extensions["x-enum-varnames"] = new JsonNodeExtension(varNames);
        schema.Extensions["x-enumNames"] = new JsonNodeExtension(enumNames);
        schema.Extensions["x-ms-enum"] = new JsonNodeExtension(new JsonObject
        {
            ["name"] = JsonValue.Create(enumName),
            ["modelAsString"] = JsonValue.Create(false),
            ["values"] = values,
        });
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
    /// Publishes the bounds of a value object that may be written as a JSON string, which <c>minimum</c> and
    /// <c>maximum</c> cannot carry.
    /// </summary>
    /// <param name="schema">The schema, whose type includes <c>string</c>.</param>
    /// <param name="declared">The declared rules.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="options">The options the document describes the wire with.</param>
    /// <param name="asKey">Whether the schema describes the key of a dictionary, whose bounds are written as the key.</param>
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
        JsonSerializerOptions options,
        bool asKey)
    {
        var (minimum, maximum) = ValueObjectSchemaKeywords.WriteBounds(declared, descriptor, options, asKey);

        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        if (minimum is not null)
        {
            schema.Extensions["x-minimum"] = new JsonNodeExtension(minimum);
        }

        if (maximum is not null)
        {
            schema.Extensions["x-maximum"] = new JsonNodeExtension(maximum);
        }

        return ValueObjectSchemaKeywords.BoundsSentence(minimum, maximum);
    }

    /// <summary>
    /// Gets the JSON type a value object is documented as, the one its underlying type travels as.
    /// </summary>
    /// <param name="valueType">Underlying type of the value object.</param>
    /// <returns>The type.</returns>
    private static JsonSchemaType MapType(Type valueType) => ValueObjectSchemaKeywords.TypeOf(valueType) switch
    {
        ValueObjectSchemaKeywords.Boolean => JsonSchemaType.Boolean,
        ValueObjectSchemaKeywords.Integer => JsonSchemaType.Integer,
        ValueObjectSchemaKeywords.Number => JsonSchemaType.Number,
        _ => JsonSchemaType.String,
    };
}
