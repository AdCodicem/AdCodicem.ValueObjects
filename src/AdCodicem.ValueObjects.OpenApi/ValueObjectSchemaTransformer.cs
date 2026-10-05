using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
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
/// values published in the document are literally the ones the type enforces — they cannot drift. What each rule
/// becomes is shared with <c>ValueObjectJsonSchema</c>, in AdCodicem.ValueObjects.Json, which describes a value object
/// alike in a JSON Schema System.Text.Json exports.
/// </para>
/// <para>
/// A value object written by hand is documented as well, from the schema it declares, through the descriptor the
/// registry builds by reflection when nothing registered the type, as the model binder and the FluentValidation rules
/// resolve it. It runs while the document is built, not per request.
/// </para>
/// <para>
/// A value object over a number honours <see cref="JsonSerializerOptions.NumberHandling"/> as the built-in converter of
/// its underlying type does, and is documented as System.Text.Json documents that type under the same options: as a
/// number or a string matching a numeric pattern when a number may be written or read as text, and, over a
/// <see cref="double"/> or a <see cref="float"/>, with the named literals its bounds let through.
/// </para>
/// <para>
/// A value object is described wherever it appears: as a route, query or header parameter, which ASP.NET Core hands
/// over as text, with the value object's schema in place; as the element of a collection or the value of a
/// dictionary, which System.Text.Json leaves out, with a reference to its component; and as the key of a dictionary,
/// in <c>propertyNames</c>, as the text the key is written in.
/// </para>
/// <para>
/// A closed value set lists its values in <c>enum</c>, and their names, the names of the known values, in the
/// <c>x-enum-varnames</c>, <c>x-enumNames</c> and <c>x-ms-enum</c> extensions, which client generators name the
/// members of their enumeration after; <c>x-ms-enum</c> carries the description of each value declared with one.
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

        var info = context.JsonTypeInfo;

        // A route, query or header parameter is bound from text, and reaches the transformer as a string: its
        // description still names the value object.
        if (info.Type == typeof(string) && ParameterValueObject(context.ParameterDescription) is { } parameter)
        {
            var declaredOnParameter = new OpenApiSchema
            {
                Minimum = schema.Minimum,
                Maximum = schema.Maximum,
                MinLength = schema.MinLength,
                MaxLength = schema.MaxLength,
                Pattern = schema.Pattern,
            };
            Describe(schema, parameter, info.Options);
            KeepParameterRules(schema, declaredOnParameter);
            return Task.CompletedTask;
        }

        if (info.Kind is JsonTypeInfoKind.Enumerable or JsonTypeInfoKind.Dictionary)
        {
            return DescribeContainerAsync(schema, context, cancellationToken);
        }

        // A value object written by hand that nothing registered binds and validates through a descriptor built by
        // reflection, and is documented through the same one.
        if (ValueObjectRegistry.TryResolve(info.Type, out var descriptor))
        {
            Describe(schema, descriptor, info.Options);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds the value object a parameter bound from text is, which a minimal API names as the parameter's type, and
    /// MVC as the type of its model, the parameter's type being the string it is read from.
    /// </summary>
    /// <param name="parameter">The parameter, if the schema describes one.</param>
    /// <returns>The descriptor of the value object, or <see langword="null"/> when the parameter is none.</returns>
    private static ValueObjectDescriptor? ParameterValueObject(ApiParameterDescription? parameter)
    {
        if (parameter?.Type is { } type && ValueObjectRegistry.TryResolve(type, out var descriptor))
        {
            return descriptor;
        }

        return parameter?.ModelMetadata?.ModelType is { } model && ValueObjectRegistry.TryResolve(model, out descriptor)
            ? descriptor
            : null;
    }

    /// <summary>
    /// Keeps a rule the parameter's declaration put on its schema, such as a route constraint, that is stricter than the
    /// value object's or that the value object leaves out: the request must satisfy both.
    /// </summary>
    /// <param name="schema">The parameter's schema, described as its value object.</param>
    /// <param name="declared">What the parameter's declaration had put on it before.</param>
    /// <remarks>
    /// A bound or a length the value object declares too is replaced by the stricter of the two. A pattern is kept only
    /// when the value object neither declares nor implies one, since a schema holds a single pattern.
    /// </remarks>
    private static void KeepParameterRules(OpenApiSchema schema, OpenApiSchema declared)
    {
        if (Stricter(declared.Minimum, schema.Minimum, sign: 1))
        {
            schema.Minimum = declared.Minimum;
        }

        if (Stricter(declared.Maximum, schema.Maximum, sign: -1))
        {
            schema.Maximum = declared.Maximum;
        }

        if (declared.MinLength is { } minLength && !(schema.MinLength >= minLength))
        {
            schema.MinLength = minLength;
        }

        if (declared.MaxLength is { } maxLength && !(schema.MaxLength <= maxLength))
        {
            schema.MaxLength = maxLength;
        }

        schema.Pattern ??= declared.Pattern;

        // Compared exactly where decimal holds both, as a double beyond its range; a bound that is no number, which no
        // route constraint writes, stays the value object's.
        static bool Stricter(string? parameter, string? valueObject, int sign)
        {
            if (parameter is null)
            {
                return false;
            }

            if (valueObject is null)
            {
                return true;
            }

            if (decimal.TryParse(parameter, NumberStyles.Float, CultureInfo.InvariantCulture, out var mine)
                && decimal.TryParse(valueObject, NumberStyles.Float, CultureInfo.InvariantCulture, out var its))
            {
                return decimal.Compare(mine, its) == sign;
            }

            return double.TryParse(parameter, NumberStyles.Float, CultureInfo.InvariantCulture, out var mineReal)
                   && double.TryParse(valueObject, NumberStyles.Float, CultureInfo.InvariantCulture, out var itsReal)
                   && mineReal.CompareTo(itsReal) == sign;
        }
    }

    /// <summary>
    /// Describes the elements of a collection, and the values and keys of a dictionary, that are value objects.
    /// </summary>
    /// <param name="schema">The schema of the collection or the dictionary.</param>
    /// <param name="context">The context of the transformation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <remarks>
    /// <para>
    /// System.Text.Json describes a type with a converter of its own as the schema <c>true</c>, and leaves
    /// <c>items</c> and <c>additionalProperties</c> out when that is the element's schema, so the element never
    /// reaches a transformer. Each is given the schema of its value object, which the document turns into a reference
    /// to the value object's component, as it does for a property. An element of a nullable value object is described
    /// in place instead, a value object or <c>null</c>, which a reference could not say.
    /// </para>
    /// <para>
    /// A key is written as text, so a dictionary keyed by a value object states the key's rules in
    /// <c>propertyNames</c>, a keyword of OpenAPI 3.1 that an OpenAPI 3.0 document carries as the
    /// <c>x-jsonschema-propertyNames</c> extension, as the key is written: a value object documented as a string as its
    /// component is, and one over a number or a boolean as the text its converter writes the key in, a string held to
    /// the pattern of that text, its bounds in <c>x-minimum</c>, <c>x-maximum</c> and a sentence.
    /// </para>
    /// </remarks>
    private static async Task DescribeContainerAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var info = context.JsonTypeInfo;
        var dictionary = info.Kind == JsonTypeInfoKind.Dictionary;
        var described = dictionary ? schema.AdditionalProperties : schema.Items;

        if (described is null && info.ElementType is { } elementType && ValueObjectRegistry.TryResolve(elementType, out var element))
        {
            var elementSchema = await DescribeElementAsync(elementType, element, context, cancellationToken).ConfigureAwait(false);
            if (dictionary)
            {
                schema.AdditionalProperties = elementSchema;
            }
            else
            {
                schema.Items = elementSchema;
            }
        }

        if (schema.PropertyNames is null
            && info.KeyType is { } keyType
            && ValueObjectRegistry.TryResolve(keyType, out var key))
        {
            var names = new OpenApiSchema();
            Describe(names, key, info.Options, asKey: true);
            schema.PropertyNames = names;
        }
    }

    /// <summary>
    /// Builds the schema of an element that is a value object.
    /// </summary>
    /// <param name="elementType">The element's type, the value object or a nullable one.</param>
    /// <param name="element">Descriptor of the value object.</param>
    /// <param name="context">The context of the transformation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The schema of the element.</returns>
    /// <remarks>
    /// Within a document, the schema comes from ASP.NET Core, which runs every schema transformer on it and gives it the
    /// identifier that makes it a reference to the value object's component. Outside one, as when the transformer is
    /// called directly, it is described in place.
    /// </remarks>
    private static async Task<IOpenApiSchema> DescribeElementAsync(
        Type elementType,
        ValueObjectDescriptor element,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var nullable = Nullable.GetUnderlyingType(elementType) is not null;
        if (!nullable && context.Document is not null)
        {
            return await context.GetOrCreateSchemaAsync(elementType, parameterDescription: null, cancellationToken).ConfigureAwait(false);
        }

        // A nullable element is the value object or null, which describing it keeps.
        var schema = new OpenApiSchema { Type = nullable ? JsonSchemaType.Null : null };
        Describe(schema, element, context.JsonTypeInfo.Options);
        return schema;
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
    /// Describes a value object as its underlying value, carrying the rules declared on it.
    /// </summary>
    /// <param name="schema">The schema to fill in.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="options">The options the document describes the wire with.</param>
    /// <param name="asKey">Whether the schema describes the key of a dictionary, which is written as text whatever the
    /// underlying type.</param>
    /// <remarks>
    /// Describing a schema a second time leaves it as the first time did: ASP.NET Core hands the element of a
    /// collection back to the transformers after this one gave it its schema. A schema that allows <c>null</c> keeps
    /// allowing it.
    /// </remarks>
    private static void Describe(OpenApiSchema schema, ValueObjectDescriptor descriptor, JsonSerializerOptions options, bool asKey = false)
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
            schema.Enum = [.. declared.KnownValues.Select(value => ValueObjectSchemaKeywords.WriteKnownValue(value, descriptor, options, asKey))];
            NameKnownValues(schema, declared, EnumName(schema, descriptor, options));
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
    /// Gets the name a client generator gives the enumeration of a closed set: the identifier of the value object's
    /// component, the one a reference to it names.
    /// </summary>
    /// <param name="schema">The schema of the value object.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="options">The options the document describes the wire with.</param>
    /// <returns>The name.</returns>
    /// <remarks>
    /// ASP.NET Core marks a schema it makes a component with its identifier, which an application may choose through
    /// <see cref="OpenApiOptions.CreateSchemaReferenceId"/>. A value object described in place, as a parameter or the key
    /// of a dictionary, carries none, and takes the identifier its component has by default: its type's name, or, for a
    /// construction of a generic value object, that name followed by <c>Of</c> and its type arguments,
    /// <c>ReferenceOfPurchaseOrder</c>.
    /// </remarks>
    private static string EnumName(OpenApiSchema schema, ValueObjectDescriptor descriptor, JsonSerializerOptions options)
    {
        if (schema.Metadata is { } metadata && metadata.TryGetValue(SchemaIdentifier, out var identifier) && identifier is string { Length: > 0 } component)
        {
            return component;
        }

        string? byDefault;
        try
        {
            byDefault = OpenApiOptions.CreateDefaultSchemaReferenceId(options.GetTypeInfo(descriptor.ValueObjectType));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or InvalidOperationException)
        {
            // The options hold no contract for the value object: the name is built from its type alone.
            byDefault = null;
        }

        return string.IsNullOrEmpty(byDefault) ? ComponentName(descriptor.ValueObjectType) : byDefault;
    }

    /// <summary>
    /// Builds the identifier ASP.NET Core gives a type's component by default from the type alone, which differs from it
    /// only where a type argument is a primitive, which ASP.NET Core names by its keyword.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>Its name, or, for a generic type, its name followed by <c>Of</c> and its type arguments.</returns>
    private static string ComponentName(Type type)
        => type.IsGenericType
            ? $"{type.Name.Split('`')[0]}Of{string.Join("And", type.GetGenericArguments().Select(ComponentName))}"
            : type.Name;

    /// <summary>
    /// The key under which ASP.NET Core keeps, in a schema's metadata, the identifier of the component it makes of it.
    /// </summary>
    private const string SchemaIdentifier = "x-schema-id";

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
