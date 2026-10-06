using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AdCodicem.ValueObjects.Swashbuckle;

/// <summary>
/// Wires value object support into Swashbuckle's document generation.
/// </summary>
public static class ValueObjectSwaggerGenExtensions
{
    /// <summary>
    /// Documents value objects as their underlying types, with the rules declared on them.
    /// </summary>
    /// <param name="options">Swashbuckle's options, from <c>AddSwaggerGen</c>.</param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// Swashbuckle describes a type from its public properties, blind to the converter a value object travels through,
    /// so a value object would otherwise be documented as an object with a <c>value</c> property, the very wrapper the
    /// value object keeps off the wire. A schema filter describes it as its underlying type instead, with the pattern,
    /// bounds, lengths, example and accepted values declared on it, as AdCodicem.ValueObjects.OpenApi describes it on the
    /// built-in stack, and a parameter filter makes a minimal API's route, query or header parameter refer to its
    /// component, as an MVC action's already does. A value object written by hand, a construction of a generic value
    /// object, and the value object a <c>MapType</c> maps are described too.
    /// </para>
    /// <para>
    /// The examples and known values are written as the minimal API options of System.Text.Json write them. A number
    /// System.Text.Json writes as a JSON number is documented as Swashbuckle documents the underlying type in the same
    /// document, as a number, whatever those options let be read from text; a 128-bit integer, which travels as a JSON
    /// string, stays a string. A nullable member, element or dictionary value is described in place with <c>null</c>
    /// allowed, as Swashbuckle describes a nullable primitive, though with the rules of its type alone, as Swashbuckle
    /// describes the non-nullable one, and the enumeration of a closed set is named after its component, under the
    /// identifier <c>CustomSchemaIds</c> gives it when an application chose one.
    /// </para>
    /// <para>
    /// Call it after <c>IncludeXmlComments</c>: Swashbuckle runs its schema filters in the order they were added, and its
    /// XML comments filter would otherwise replace the description of a value object with the type's summary, losing the
    /// sentence that states the bounds of a value written as text. Called twice, it adds each filter once, and the
    /// options of the last call win.
    /// </para>
    /// </remarks>
    public static SwaggerGenOptions AddValueObjects(this SwaggerGenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Add(options, new ValueObjectSwaggerSettings(null));
    }

    /// <summary>
    /// Documents value objects as their underlying types, with the rules declared on them, writing their examples and
    /// known values with the given serializer options rather than with the minimal API ones.
    /// </summary>
    /// <param name="options">Swashbuckle's options, from <c>AddSwaggerGen</c>.</param>
    /// <param name="serializerOptions">The options the application writes value objects with, for an application whose
    /// MVC and minimal API options differ.</param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <remarks>
    /// A number System.Text.Json writes as a JSON number is still documented as a number, whatever
    /// <see cref="JsonSerializerOptions.NumberHandling"/> says, as Swashbuckle documents the underlying type. Everything
    /// else is as <see cref="AddValueObjects(SwaggerGenOptions)"/> says.
    /// </remarks>
    public static SwaggerGenOptions AddValueObjects(this SwaggerGenOptions options, JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serializerOptions);

        return Add(options, new ValueObjectSwaggerSettings(serializerOptions));
    }

    /// <summary>
    /// Adds the two filters, once each, the schema filter holding the settings of the last call.
    /// </summary>
    /// <param name="options">Swashbuckle's options.</param>
    /// <param name="settings">What the call asked for.</param>
    /// <returns>The same options.</returns>
    private static SwaggerGenOptions Add(SwaggerGenOptions options, ValueObjectSwaggerSettings settings)
    {
        if (options.SchemaFilterDescriptors.Find(static descriptor => descriptor.Type == typeof(ValueObjectSchemaFilter)) is { } added)
        {
            added.Arguments = [settings];
        }
        else
        {
            options.SchemaFilter<ValueObjectSchemaFilter>(settings);
        }

        if (!options.ParameterFilterDescriptors.Exists(static descriptor => descriptor.Type == typeof(ValueObjectParameterFilter)))
        {
            options.ParameterFilter<ValueObjectParameterFilter>();
        }

        return options;
    }
}
