using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;

namespace AdCodicem.ValueObjects.NJsonSchema;

/// <summary>
/// Answers NJsonSchema for a value object, and hands every other type to the reflection service of the settings.
/// </summary>
/// <param name="inner">The reflection service the settings had, which describes every other type.</param>
/// <remarks>
/// <para>
/// NJsonSchema asks its reflection service what a type is before it describes it, and looks a type mapper up by the
/// exact type or its generic definition only: a mapper per value object, built from the registry, misses a value object
/// the registry did not hold yet, and a nullable one under the Newtonsoft.Json generator, which meets a
/// <see cref="Nullable{T}"/> as the string its type converter writes. This service answers for any of them, a nullable
/// one unwrapped: an object, so that NJsonSchema files one definition per value object and refers to it wherever it is
/// used, as it does for an enumeration, and with no member, which <see cref="ValueObjectSchemaProcessor"/> fills in.
/// </para>
/// <para>
/// A value object the settings' serializer does not write as a value, as <see cref="ValueObjectWire"/> decides, is
/// handed to the inner service, which describes it as that serializer writes it.
/// </para>
/// </remarks>
internal sealed class ValueObjectReflectionService(IReflectionService inner) : IReflectionService
{
    /// <summary>Gets the reflection service this one hands every other type to.</summary>
    internal IReflectionService Inner => inner;

    /// <inheritdoc />
    public JsonTypeDescription GetDescription(
        ContextualType contextualType,
        ReferenceTypeNullHandling defaultReferenceTypeNullHandling,
        JsonSchemaGeneratorSettings settings)
        => Describe(contextualType, settings) ?? inner.GetDescription(contextualType, defaultReferenceTypeNullHandling, settings);

    /// <inheritdoc />
    public JsonTypeDescription GetDescription(ContextualType contextualType, JsonSchemaGeneratorSettings settings)
        => Describe(contextualType, settings) ?? inner.GetDescription(contextualType, settings);

    /// <inheritdoc />
    public bool IsNullable(ContextualType contextualType, ReferenceTypeNullHandling defaultReferenceTypeNullHandling)
        => inner.IsNullable(contextualType, defaultReferenceTypeNullHandling);

    /// <inheritdoc />
    public bool IsStringEnum(ContextualType contextualType, JsonSchemaGeneratorSettings settings)
        => inner.IsStringEnum(contextualType, settings);

    /// <inheritdoc />
    public Func<object, string?> GetEnumValueConverter(JsonSchemaGeneratorSettings settings) => inner.GetEnumValueConverter(settings);

    /// <inheritdoc />
    public string GetPropertyName(ContextualAccessorInfo accessorInfo, JsonSchemaGeneratorSettings settings)
        => inner.GetPropertyName(accessorInfo, settings);

    /// <inheritdoc />
    /// <remarks>A value object has no member on the wire: the schema processor describes its definition.</remarks>
    public void GenerateProperties(
        JsonSchema schema,
        ContextualType contextualType,
        JsonSchemaGeneratorSettings settings,
        JsonSchemaGenerator schemaGenerator,
        JsonSchemaResolver schemaResolver)
    {
        ArgumentNullException.ThrowIfNull(contextualType);

        if (!ValueObjectWire.TryGet(contextualType.OriginalType, settings, out _))
        {
            inner.GenerateProperties(schema, contextualType, settings, schemaGenerator, schemaResolver);
        }
    }

    /// <summary>
    /// Describes a value object, or a nullable one, as an object NJsonSchema refers to, or in place under Swagger 2.0.
    /// </summary>
    /// <param name="contextualType">The type.</param>
    /// <param name="settings">The settings, whose serializer decides whether the value object is written as a value.</param>
    /// <returns>The description, or <see langword="null"/> for a type the inner service describes.</returns>
    private static JsonTypeDescription? Describe(ContextualType contextualType, JsonSchemaGeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(contextualType);

        if (ValueObjectSchemaProcessor.Annotated(contextualType) || !ValueObjectWire.TryGet(contextualType.OriginalType, settings, out var wire))
        {
            return null;
        }

        // A nullable value object is the value object, which NJsonSchema then describes, or hands to the application's
        // type mapper of it, and says null beside.
        var nullable = Nullable.GetUnderlyingType(contextualType.OriginalType) is not null;
        var described = nullable ? contextualType.OriginalGenericArguments[0] : contextualType;

        // Swagger 2.0 refers to no schema from a route, query or header parameter: there, the value object is described
        // in place, as the underlying type its parameter is.
        return settings.SchemaType == SchemaType.Swagger2
            ? JsonTypeDescription.Create(described, ValueObjectJsonSchemaDescription.TypeOf(wire), nullable, wire.Descriptor.Schema.Format)
            : JsonTypeDescription.Create(described, JsonObjectType.Object, nullable, format: null);
    }
}
