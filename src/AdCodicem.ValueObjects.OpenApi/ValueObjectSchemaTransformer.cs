using System.Globalization;
using System.Text.Json;
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
/// alike in a JSON Schema System.Text.Json exports, and the whole description of a value object with the Swashbuckle
/// filters of AdCodicem.ValueObjects.Swashbuckle, which build on the same object model.
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
            ValueObjectOpenApiSchema.Describe(schema, parameter, info.Options, () => EnumName(schema, parameter, info.Options));
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
            ValueObjectOpenApiSchema.Describe(schema, descriptor, info.Options, () => EnumName(schema, descriptor, info.Options));
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
            ValueObjectOpenApiSchema.Describe(names, key, info.Options, () => EnumName(names, key, info.Options), asKey: true);
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
        ValueObjectOpenApiSchema.Describe(schema, element, context.JsonTypeInfo.Options, () => EnumName(schema, element, context.JsonTypeInfo.Options));
        return schema;
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
}
