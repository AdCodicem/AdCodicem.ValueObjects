using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Json;

/// <summary>
/// Directory of the converters generated for the value objects of the loaded assemblies.
/// </summary>
/// <remarks>
/// <para>
/// A source generator only ever sees the original compilation, never the output of another generator. The
/// <c>[JsonConverter]</c> attribute placed on a generated value object is therefore invisible to the
/// System.Text.Json generator, and a <c>JsonSerializerContext</c> would otherwise fall back to serializing the
/// value object as an object with properties.
/// </para>
/// <para>
/// This registry closes that gap. The value object generator registers the converter of every value object with
/// its descriptor in <see cref="ValueObjectRegistry"/>, whatever the declaring assembly references, and
/// <see cref="ValueObjectJsonConverterFactory"/> — a hand-written type the System.Text.Json generator can see —
/// hands the right converter back at run time. A converter registered here, for a value object written by hand,
/// comes first. The lookup is a dictionary hit, so nothing here needs reflection or dynamic code.
/// </para>
/// </remarks>
public static class ValueObjectJsonRegistry
{
    private static readonly ConcurrentDictionary<Type, JsonConverter> Converters = new();

    /// <summary>
    /// Registers the converter of a value object.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <param name="converter">Converter to register.</param>
    public static void Register<TSelf>(JsonConverter<TSelf> converter)
        where TSelf : struct
    {
        ArgumentNullException.ThrowIfNull(converter);

        Converters[typeof(TSelf)] = converter;
    }

    /// <summary>
    /// Looks up the converter registered for a value object type.
    /// </summary>
    /// <param name="type">Value object type.</param>
    /// <param name="converter">The registered converter.</param>
    /// <returns>
    /// <see langword="true"/> when a converter is registered here, or with the descriptor of the type, as the
    /// generator registers it.
    /// </returns>
    /// <remarks>
    /// <see cref="Nullable{T}"/> over a value object has no converter of its own: the serializer wraps the converter
    /// of the value object itself.
    /// </remarks>
    public static bool TryGet(Type type, [NotNullWhen(true)] out JsonConverter? converter)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (Converters.TryGetValue(type, out converter))
        {
            return true;
        }

        if (Nullable.GetUnderlyingType(type) is null
            && ValueObjectRegistry.TryGet(type, out var descriptor)
            && descriptor.JsonConverter is { } carried)
        {
            converter = carried;
            return true;
        }

        converter = null;
        return false;
    }

    /// <summary>
    /// Gets the number of converters registered here, those registered with a descriptor aside.
    /// </summary>
    public static int Count => Converters.Count;
}
