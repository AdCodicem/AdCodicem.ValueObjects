using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using RequiredAttribute = System.ComponentModel.DataAnnotations.RequiredAttribute;

namespace AdCodicem.ValueObjects.Swashbuckle;

/// <summary>
/// Describes a value object as its underlying type wherever Swashbuckle describes it, and a nullable one in place where
/// Swashbuckle would refer to its component.
/// </summary>
/// <param name="httpJson">The minimal API options, which the examples and known values are written with unless the
/// application named others.</param>
/// <param name="generator">Swashbuckle's schema options, whose identifiers name the enumeration of a closed set.</param>
/// <param name="contracts">The data contracts Swashbuckle builds its schemas from.</param>
/// <param name="settings">What the application asked for.</param>
/// <remarks>
/// <para>
/// Swashbuckle hands a schema filter every schema it generates: the component of a type it refers to, and every schema it
/// writes in place, a parameter's, a member's, an element's or a dictionary value's. A value object's component, or a
/// value object written in place, as a <c>MapType</c> writes it, is described through the description shared with the
/// built-in stack's transformer. The <c>allOf</c> Swashbuckle wraps a reference in under
/// <c>UseAllOfToExtendReferenceSchemas</c>, handed over under the value object's type, is left as it is there: it refers
/// to the component, which is described.
/// </para>
/// <para>
/// Swashbuckle refers to the component of a nullable value object as it does for a non-nullable one, as it does for a
/// nullable enumeration, so the nullability is lost, though it writes a nullable primitive in place with <c>null</c>
/// allowed. The schema of the type holding it, a collection, a dictionary or an object, is handed to the filter after
/// its members: there, a reference to a nullable value object is replaced with the value object described in place,
/// <c>null</c> allowed, as Swashbuckle writes a nullable primitive. A property Swashbuckle makes non-nullable, one
/// marked <see cref="RequiredAttribute"/>, keeps its reference. Under <c>UseAllOfToExtendReferenceSchemas</c>,
/// Swashbuckle wraps the reference of a nullable property in an <c>allOf</c> whose type is <c>null</c> alone, which no
/// value satisfies in OpenAPI 3.1: the value object is described in place inside the wrapper instead, <c>null</c>
/// allowed, and the wrapper keeps what Swashbuckle put beside the reference. A dictionary keyed by a value object states
/// the key's rules in <c>propertyNames</c>, as the key is written.
/// </para>
/// </remarks>
internal sealed class ValueObjectSchemaFilter(
    IOptions<HttpJsonOptions> httpJson,
    IOptions<SchemaGeneratorOptions> generator,
    ISerializerDataContractResolver contracts,
    ValueObjectSwaggerSettings settings) : ISchemaFilter
{
    private JsonSerializerOptions? _options;

    /// <summary>
    /// Gets the options the value objects are described with: the application's, with numbers read and written as
    /// numbers only.
    /// </summary>
    /// <remarks>
    /// Swashbuckle documents a number as a number whatever the options let be read from text, and so does the filter:
    /// the examples, known values and bounds it writes stay numbers beside a <c>type</c> that says number. A copy of
    /// options that hold no resolver gets System.Text.Json's reflection-based one, the one a serializer call would use,
    /// without which no example could be written as the type writes it.
    /// </remarks>
    internal JsonSerializerOptions Options => _options ??= Strict(settings.Options ?? httpJson.Value.SerializerOptions);

    /// <inheritdoc />
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema target)
        {
            return;
        }

        if (ValueObjectRegistry.TryResolve(context.Type, out var descriptor))
        {
            if (target.AllOf is not { Count: > 0 })
            {
                // Swashbuckle describes the value object as an object that allows no other property.
                target.AdditionalPropertiesAllowed = true;
                ValueObjectOpenApiSchema.Describe(target, descriptor, Options, () => EnumName(context.SchemaRepository, descriptor.ValueObjectType));
            }

            return;
        }

        var contract = contracts.GetDataContractForType(context.Type);
        switch (contract.DataType)
        {
            case DataType.Array when NullableValueObject(target.Items, contract.ArrayItemType) is { } element:
                target.Items = InPlace(element, context.SchemaRepository);
                break;
            case DataType.Dictionary:
                DescribeDictionary(target, contract, context);
                break;
            case DataType.Object when target.Properties is { } properties:
                DescribeProperties(properties, contract, context.SchemaRepository);
                break;
        }
    }

    /// <summary>
    /// Describes the nullable value objects a dictionary holds, and the value object that keys it.
    /// </summary>
    /// <param name="schema">The dictionary's schema.</param>
    /// <param name="contract">Its data contract.</param>
    /// <param name="context">The context of the filter.</param>
    /// <remarks>
    /// <para>
    /// Swashbuckle describes the values of a dictionary in <c>additionalProperties</c>, or, for a dictionary keyed by an
    /// enumeration, once for each of its keys, in <c>properties</c>: a nullable value object is described in place in
    /// either.
    /// </para>
    /// <para>
    /// A key is written as text, so a dictionary keyed by a value object states the key's rules in <c>propertyNames</c>,
    /// a keyword of OpenAPI 3.1 that an OpenAPI 3.0 document carries as the <c>x-jsonschema-propertyNames</c> extension,
    /// as the key is written.
    /// </para>
    /// </remarks>
    private void DescribeDictionary(OpenApiSchema schema, DataContract contract, SchemaFilterContext context)
    {
        if (NullableValueObject(schema.AdditionalProperties, contract.DictionaryValueType) is { } value)
        {
            schema.AdditionalProperties = InPlace(value, context.SchemaRepository);
        }

        if (schema.Properties is { } entries)
        {
            foreach (var name in entries.Keys.ToList())
            {
                if (NullableValueObject(entries[name], contract.DictionaryValueType) is { } entry)
                {
                    entries[name] = InPlace(entry, context.SchemaRepository);
                }
            }
        }

        if (schema.PropertyNames is null && KeyOf(context.Type) is { } keyType && ValueObjectRegistry.TryResolve(keyType, out var key))
        {
            var names = new OpenApiSchema();
            ValueObjectOpenApiSchema.Describe(names, key, Options, () => EnumName(context.SchemaRepository, key.ValueObjectType), asKey: true);
            schema.PropertyNames = names;
        }
    }

    /// <summary>
    /// Describes in place each nullable value object a type's properties refer to, as Swashbuckle describes a nullable
    /// primitive property.
    /// </summary>
    /// <param name="properties">The schemas of the properties, keyed by name.</param>
    /// <param name="contract">The type's data contract.</param>
    /// <param name="repository">The components of the document.</param>
    /// <remarks>
    /// <para>
    /// What Swashbuckle put on the reference stays with the property: its description, the summary of the member or the
    /// component's own, whether it is deprecated, and its default value. Whether it is read-only or write-only is the
    /// member's, as for a primitive. The member's validation attributes are not applied: Swashbuckle applies none beside
    /// a reference, so that a nullable value object is documented as the non-nullable one is, <c>null</c> allowed, with
    /// the rules of its type.
    /// </para>
    /// <para>
    /// Under <c>UseAllOfToExtendReferenceSchemas</c>, Swashbuckle wraps the reference in an <c>allOf</c> it marks
    /// <c>null</c>, beside which it writes what it reads off the member, its validation attributes included. The wrapper
    /// is kept, as for a non-nullable value object, and its reference is replaced with the value object described in
    /// place, which allows <c>null</c> in its stead.
    /// </para>
    /// </remarks>
    private void DescribeProperties(IDictionary<string, IOpenApiSchema> properties, DataContract contract, SchemaRepository repository)
    {
        foreach (var property in contract.ObjectProperties)
        {
            if (!properties.TryGetValue(property.Name, out var current) || !IsNullable(property))
            {
                continue;
            }

            if (NullableValueObject(current, property.MemberType) is { } member)
            {
                var schema = InPlace(member, repository);
                if (!string.IsNullOrEmpty(current.Description))
                {
                    schema.Description = current.Description;
                }

                schema.Deprecated = current.Deprecated;
                schema.Default = current.Default;
                schema.ReadOnly = property.IsReadOnly;
                schema.WriteOnly = property.IsWriteOnly;
                properties[property.Name] = schema;
            }
            else if (current is OpenApiSchema { Type: JsonSchemaType.Null, AllOf: [var reference] } wrapper
                     && NullableValueObject(reference, property.MemberType) is { } wrapped)
            {
                wrapper.Type = null;
                wrapper.AllOf[0] = InPlace(wrapped, repository);
            }
        }
    }

    /// <summary>
    /// Tells whether Swashbuckle makes a property nullable: when its type is, and nothing marks it required.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns><see langword="true"/> when the property may be <c>null</c>.</returns>
    private static bool IsNullable(DataProperty property)
        => property.IsNullable
           && property.MemberInfo?.GetInlineAndMetadataAttributes().OfType<RequiredAttribute>().Any() != true;

    /// <summary>
    /// Finds the value object a schema refers to for a type that makes it nullable.
    /// </summary>
    /// <param name="schema">The schema of the element, value or property.</param>
    /// <param name="type">Its type.</param>
    /// <returns>The descriptor of the value object, or <see langword="null"/> when the schema is no reference to a
    /// nullable value object.</returns>
    private static ValueObjectDescriptor? NullableValueObject(IOpenApiSchema? schema, Type? type)
        => schema is OpenApiSchemaReference
           && type is not null
           && Nullable.GetUnderlyingType(type) is { } underlying
           && ValueObjectRegistry.TryResolve(underlying, out var descriptor)
            ? descriptor
            : null;

    /// <summary>
    /// Describes a nullable value object in place: the value object, or <c>null</c>.
    /// </summary>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="repository">The components of the document.</param>
    /// <returns>The schema.</returns>
    private OpenApiSchema InPlace(ValueObjectDescriptor descriptor, SchemaRepository repository)
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Null };
        ValueObjectOpenApiSchema.Describe(schema, descriptor, Options, () => EnumName(repository, descriptor.ValueObjectType));
        return schema;
    }

    /// <summary>
    /// Gets the name a client generator gives the enumeration of a closed set: the identifier of the value object's
    /// component, the one a reference to it names.
    /// </summary>
    /// <param name="repository">The components of the document.</param>
    /// <param name="type">The value object.</param>
    /// <returns>The identifier of its component, or, before it has one, the identifier Swashbuckle will give it.</returns>
    /// <remarks>
    /// The repository builds the reference from the identifier it reserved for the type, which the reference's
    /// constructor refuses empty: it always has one.
    /// </remarks>
    private string EnumName(SchemaRepository repository, Type type)
        => repository.TryLookupByType(type, out var reference)
            ? reference.Reference.Id!
            : generator.Value.SchemaIdSelector(type);

    /// <summary>
    /// Gets the type of the keys of a dictionary, as Swashbuckle finds it.
    /// </summary>
    /// <param name="dictionary">The dictionary's type.</param>
    /// <returns>The type of its keys, or <see langword="null"/> for a dictionary that is not generic.</returns>
    private static Type? KeyOf(Type dictionary)
        => dictionary.IsConstructedFrom(typeof(IDictionary<,>), out var constructed)
           || dictionary.IsConstructedFrom(typeof(IReadOnlyDictionary<,>), out constructed)
            ? constructed.GenericTypeArguments[0]
            : null;

    /// <summary>
    /// Copies options so that they read and write numbers as numbers only, with a resolver.
    /// </summary>
    /// <param name="options">The application's options.</param>
    /// <returns>The options themselves when they already do, otherwise a copy.</returns>
    private static JsonSerializerOptions Strict(JsonSerializerOptions options)
        => options.NumberHandling == JsonNumberHandling.Strict && options.TypeInfoResolver is not null
            ? options
            : new JsonSerializerOptions(options)
            {
                NumberHandling = JsonNumberHandling.Strict,
                TypeInfoResolver = options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver(),
            };
}
