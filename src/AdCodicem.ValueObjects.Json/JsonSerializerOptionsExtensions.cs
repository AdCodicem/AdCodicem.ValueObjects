using System.Text.Json;

namespace AdCodicem.ValueObjects.Json;

/// <summary>
/// Wires value object support into <see cref="JsonSerializerOptions"/>.
/// </summary>
public static class JsonSerializerOptionsExtensions
{
    /// <summary>
    /// Adds the value object converter factory to the options.
    /// </summary>
    /// <param name="options">Options to configure.</param>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <remarks>
    /// Reflection-based serialization already honours the converter each generated value object carries, so
    /// this is only needed for value objects written by hand, or to make the behaviour explicit at the
    /// composition root. Adding it twice is harmless but wasteful, so an existing registration is left alone.
    /// </remarks>
    public static JsonSerializerOptions AddValueObjects(this JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (var converter in options.Converters)
        {
            if (converter is ValueObjectJsonConverterFactory)
            {
                return options;
            }
        }

        options.Converters.Add(new ValueObjectJsonConverterFactory());

        return options;
    }
}
