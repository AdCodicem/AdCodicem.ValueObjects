using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdCodicem.ValueObjects.Metadata;

/// <summary>
/// Supplies the System.Text.Json converter of each construction of a generic value object.
/// </summary>
/// <remarks>
/// <para>
/// The generator puts this factory in the <c>[JsonConverter]</c> attribute of a generic value object, where a value
/// object that is not generic names its own converter. An attribute cannot name a type through a type parameter, and
/// System.Text.Json does not close an open generic converter over the type it converts, so the attribute names this
/// factory, which hands out the converter the generator wrote, closed over the construction it is asked for.
/// </para>
/// <para>
/// It is reached through the attribute, which only the serializer resolving types by reflection reads. A
/// source-generated serializer context reaches the value object through <c>ValueObjectJsonConverterFactory</c>
/// instead, which hands out the same converter.
/// </para>
/// </remarks>
public sealed class GenericValueObjectJsonConverterFactory : JsonConverterFactory
{
    private const string ReflectionOnly =
        "Reached through the [JsonConverter] attribute of a generic value object, which only the serializer resolving "
        + "types by reflection reads, and which the source generator never sees, since another generator writes it.";

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        return typeToConvert.IsConstructedGenericType
               && Nullable.GetUnderlyingType(typeToConvert) is null
               && ValueObjectRegistry.IsValueObject(typeToConvert);
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">The construction carries no generated converter.</exception>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = ReflectionOnly)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = ReflectionOnly)]
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        return ValueObjectRegistry.TryResolve(typeToConvert, out var descriptor) && descriptor.JsonConverter is { } converter
            ? converter
            : throw new NotSupportedException(
                $"'{typeToConvert}' carries no generated converter. Register its generic definition with "
                + "ValueObjectRegistry.RegisterGenericDefinition, or the construction with ValueObjectRegistry.Register.");
    }
}
