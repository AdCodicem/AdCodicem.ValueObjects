using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Json;

/// <summary>
/// Supplies the converter of any value object, so that a value object always serializes as its bare underlying
/// value.
/// </summary>
/// <remarks>
/// <para>
/// A generated value object already carries its own <c>[JsonConverter]</c> and needs nothing from this factory
/// when the serializer resolves types by reflection. The factory exists for the two cases the attribute cannot
/// reach: a <c>JsonSerializerContext</c>, whose generator never sees the attribute, and value objects written
/// by hand.
/// </para>
/// <para>
/// Register it on the context so the System.Text.Json generator picks it up:
/// <code>
/// [JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
/// [JsonSerializable(typeof(Payment))]
/// internal sealed partial class PaymentContext : JsonSerializerContext;
/// </code>
/// </para>
/// </remarks>
public sealed class ValueObjectJsonConverterFactory : JsonConverterFactory
{
    private const string FallbackOnly =
        "Only reached for a value object that registered no converter, which never happens for a generated one: "
        + "those are served from the registry, statically. A hand-written value object combined with trimming or "
        + "native AOT has to supply its own converter.";

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        return ValueObjectRegistry.IsValueObject(typeToConvert);
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = FallbackOnly)]
    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = FallbackOnly)]
    [UnconditionalSuppressMessage("Trimming", "IL2071", Justification = FallbackOnly)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = FallbackOnly)]
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        // Fast path: the generator registered the converter it emitted for this type.
        if (ValueObjectJsonRegistry.TryGet(typeToConvert, out var registered))
        {
            return registered;
        }

        var valueType = ValueObjectRegistry.GetUnderlyingType(typeToConvert);
        if (valueType is null)
        {
            return null;
        }

        var converterType = typeof(ValueObjectJsonConverter<,>).MakeGenericType(typeToConvert, valueType);

        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}
