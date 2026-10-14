using NJsonSchema.Generation;

namespace AdCodicem.ValueObjects.NJsonSchema;

/// <summary>
/// Wires value object support into the schema settings of NJsonSchema, on which NSwag's OpenAPI documents and
/// Confluent's JSON serdes for the Schema Registry are built.
/// </summary>
public static class ValueObjectJsonSchemaGeneratorSettingsExtensions
{
    /// <summary>
    /// Describes every value object as its underlying type, with the rules declared on it, in the schemas these settings
    /// generate.
    /// </summary>
    /// <typeparam name="TSettings">The type of the settings: the System.Text.Json or the Newtonsoft.Json ones.</typeparam>
    /// <param name="settings">The settings, NSwag's <c>SchemaSettings</c> or the ones handed to Confluent's serdes.</param>
    /// <returns>The same settings, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// NJsonSchema describes a value object by its public properties under System.Text.Json settings, and as a bare
    /// <c>string</c>, through its type converter, under Newtonsoft.Json ones. Each value object gets one definition
    /// instead, a component in an OpenAPI document, describing it as its underlying type with the pattern, bounds,
    /// lengths, example and accepted values declared on it, and every use of it refers to that definition.
    /// </para>
    /// <para>
    /// The schema says what the settings' serializer writes, which the settings are never changed for: the
    /// <c>SerializerOptions</c> of System.Text.Json settings, as they stand when the schema is generated, a value object
    /// they do not convert as a value being left as NJsonSchema describes it; under Newtonsoft.Json settings, the value
    /// <c>ValueObjectConverter</c> writes when the type's or the settings' converter is it, and the text of its type
    /// converter when no converter claims it; a value object written otherwise is left as NJsonSchema describes it. In an
    /// OpenAPI or Swagger document a number is a number, whatever the options let be read from text.
    /// </para>
    /// <para>
    /// A Swagger 2.0 document, which refers to no schema from a parameter, describes every value object in place. Text a
    /// member or a parameter holds to a value object's rules with <c>[ParsesAs]</c> is described as that value object's
    /// text. A type mapper of the settings keeps the application's description of the forms NJsonSchema hands it: the
    /// value object and its nullable type for a mapper of the value object or of its generic definition, the nullable
    /// type alone for a mapper of that type, which only Newtonsoft.Json settings consult. A member NJsonSchema's
    /// <c>[JsonSchemaType]</c> or <c>[JsonSchema]</c> describes keeps the application's description too.
    /// </para>
    /// <para>
    /// Called twice, it changes nothing more.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static TSettings AddValueObjects<TSettings>(this TSettings settings)
        where TSettings : JsonSchemaGeneratorSettings
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.ReflectionService is not ValueObjectReflectionService)
        {
            settings.ReflectionService = new ValueObjectReflectionService(settings.ReflectionService);
        }

        if (!settings.SchemaProcessors.Any(static processor => processor is ValueObjectSchemaProcessor))
        {
            settings.SchemaProcessors.Add(ValueObjectSchemaProcessor.Instance);
        }

        return settings;
    }
}
