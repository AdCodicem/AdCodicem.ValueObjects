using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.NewtonsoftJson;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;

namespace AdCodicem.ValueObjects.NJsonSchema;

/// <summary>
/// How the serializer of a schema's settings writes a value object: as its value, or as the text of its type converter,
/// and the System.Text.Json options that write its examples, bounds and known values the same way.
/// </summary>
/// <param name="Descriptor">Descriptor of the value object.</param>
/// <param name="Options">The options the rules shared by every schema package write values with.</param>
/// <param name="AsText">Whether the value object travels as text, a number's or a boolean's included.</param>
/// <param name="AsKey">Whether its values are written as System.Text.Json writes the key of a dictionary, its text.</param>
internal sealed record ValueObjectWire(ValueObjectDescriptor Descriptor, JsonSerializerOptions Options, bool AsText, bool AsKey)
{
    /// <summary>
    /// Options holding System.Text.Json's reflection-based resolver alone, through which the converter the generator
    /// writes on every value object writes it.
    /// </summary>
    internal static readonly JsonSerializerOptions Unconfigured = new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    /// <summary>Copies of options in use, with a resolver, and numbers read and written as numbers only.</summary>
    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> NumbersAsNumbers = new();

    /// <summary>Copies of options in use, with a resolver.</summary>
    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> AsConfigured = new();

    /// <summary>Options writing a value object through Newtonsoft.Json settings, per settings.</summary>
    private static readonly ConditionalWeakTable<JsonSerializerSettings, JsonSerializerOptions> WrittenByNewtonsoft = new();

    /// <summary>
    /// Finds how the serializer of the settings writes a type, when it is a value object written as a value or as text.
    /// </summary>
    /// <param name="type">The type, possibly nullable.</param>
    /// <param name="settings">The schema's settings.</param>
    /// <param name="wire">How the value object is written.</param>
    /// <returns><see langword="false"/> for a type that is no value object, one the serializer writes otherwise, as an
    /// object with members, which NJsonSchema describes as it is, or the form of one a type mapper of the settings maps.</returns>
    /// <remarks>A value object is found through <see cref="ValueObjectRegistry.TryResolve"/> alone, as the OpenAPI packages
    /// find it: NJsonSchema walks types by reflection, so the package runs only where dynamic code does.</remarks>
    internal static bool TryGet(Type type, JsonSchemaGeneratorSettings settings, [NotNullWhen(true)] out ValueObjectWire? wire)
    {
        wire = null;
        if (!ValueObjectRegistry.TryResolve(type, out var descriptor) || Mapped(type, descriptor.ValueObjectType, settings))
        {
            return false;
        }

        switch (settings)
        {
            case SystemTextJsonSchemaGeneratorSettings systemTextJson:
                // An OpenAPI or a Swagger document describes a number as a number, as NSwag describes a bare one.
                var options = Copy(systemTextJson.SerializerOptions, numbersAsNumbers: settings.SchemaType != SchemaType.JsonSchema);
                if (ConvertedAsValue(options, descriptor.ValueObjectType))
                {
                    wire = new(descriptor, options, AsText: false, AsKey: false);
                }

                break;
            case NewtonsoftJsonSchemaGeneratorSettings newtonsoft:
                wire = Newtonsoft(descriptor, newtonsoft);
                break;
            default:
                wire = new(descriptor, Unconfigured, AsText: false, AsKey: false);
                break;
        }

        return wire is not null;
    }

    /// <summary>
    /// Tells whether NJsonSchema hands this form of the value object to a type mapper of the settings, which it consults
    /// before anything else: that form's description stays the application's, and every other form is described.
    /// </summary>
    /// <param name="type">The form NJsonSchema meets: the value object, or its nullable type.</param>
    /// <param name="valueObjectType">The value object.</param>
    /// <param name="settings">The schema's settings.</param>
    /// <remarks>
    /// NJsonSchema looks a mapper up by the type the reflection service describes, then by its generic definition. The
    /// value object is looked up as itself, and so is its nullable type, which this package answers unwrapped: a mapper of
    /// the value object maps both forms. A mapper of the nullable type is consulted only where the settings' own service
    /// keeps that type: Newtonsoft.Json's, which describes the nullable type of a type with a type converter as the string
    /// that converter writes. System.Text.Json's, like any other, unwraps it first, and never hands it to that mapper.
    /// </remarks>
    private static bool Mapped(Type type, Type valueObjectType, JsonSchemaGeneratorSettings settings)
        => type == valueObjectType
            ? MappedAsIs(valueObjectType, settings)
            : MappedAsIs(type, settings)
              && settings is NewtonsoftJsonSchemaGeneratorSettings newtonsoft
              && newtonsoft.ResolveContract(type) is JsonStringContract;

    /// <summary>Tells whether a type mapper of the settings maps a type, by itself or by its generic definition.</summary>
    private static bool MappedAsIs(Type type, JsonSchemaGeneratorSettings settings)
    {
        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : null;

        return settings.TypeMappers.Any(mapper => mapper.MappedType == type || (definition is not null && mapper.MappedType == definition));
    }

    /// <summary>
    /// Finds how Newtonsoft.Json writes a value object under the settings: through <see cref="ValueObjectConverter"/>, as
    /// its value, or through its type converter, as text, when no converter of the settings or of the type claims it.
    /// </summary>
    private static ValueObjectWire? Newtonsoft(ValueObjectDescriptor descriptor, NewtonsoftJsonSchemaGeneratorSettings settings)
    {
        var serializerSettings = settings.SerializerSettings;

        // A closed type always has a full name, which the settings cache its contract under.
        var contract = settings.ResolveContract(descriptor.ValueObjectType)!;

        // Newtonsoft.Json takes the converter a type's attribute names before those of the settings, in their order.
        var converter = contract.Converter
                        ?? serializerSettings.Converters.FirstOrDefault(candidate => candidate.CanConvert(descriptor.ValueObjectType));

        return converter switch
        {
            ValueObjectConverter => new(descriptor, WrittenBy(serializerSettings), AsText: false, AsKey: false),
            null when contract is JsonStringContract => new(descriptor, WrittenBy(serializerSettings), AsText: true, AsKey: false),
            _ => null,
        };
    }

    /// <summary>
    /// Gets options writing a value object as Newtonsoft.Json writes it under the settings.
    /// </summary>
    private static JsonSerializerOptions WrittenBy(JsonSerializerSettings settings)
        => WrittenByNewtonsoft.GetValue(settings, static key => new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            Converters = { new NewtonsoftJsonWrittenConverterFactory(key) },
        });

    /// <summary>
    /// Copies an application's options, never changed for a schema, with a resolver, without which no example could be
    /// written as the type writes it, and, for a document, numbers read and written as numbers only.
    /// </summary>
    /// <remarks>Options no serializer has used yet may still change, and are copied afresh each time: a copy kept would
    /// describe them as they stood the first time. Options in use change no more, and their copy is kept.</remarks>
    private static JsonSerializerOptions Copy(JsonSerializerOptions options, bool numbersAsNumbers)
        => !options.IsReadOnly ? CopyOf(options, numbersAsNumbers)
            : numbersAsNumbers ? NumbersAsNumbers.GetValue(options, static source => CopyOf(source, numbersAsNumbers: true))
            : AsConfigured.GetValue(options, static source => CopyOf(source, numbersAsNumbers: false));

    /// <summary>Copies options, with a resolver, and, when asked, numbers read and written as numbers only.</summary>
    private static JsonSerializerOptions CopyOf(JsonSerializerOptions source, bool numbersAsNumbers)
    {
        var copy = new JsonSerializerOptions(source) { TypeInfoResolver = source.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver() };
        if (numbersAsNumbers)
        {
            copy.NumberHandling = JsonNumberHandling.Strict;
        }

        return copy;
    }

    /// <summary>
    /// Tells whether the options write a value object as a value: always for a generated one, whose converter its type
    /// names; for one written by hand, when they hold a converter for it, as <c>ValueObjectJsonConverterFactory</c>.
    /// </summary>
    /// <remarks>A resolver that knows nothing of the type, as a context generated for other types, cannot say: the type
    /// is taken at its word.</remarks>
    private static bool ConvertedAsValue(JsonSerializerOptions options, Type type)
    {
        try
        {
            return !options.TryGetTypeInfo(type, out var contract) || contract.Kind == JsonTypeInfoKind.None;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return true;
        }
    }
}
