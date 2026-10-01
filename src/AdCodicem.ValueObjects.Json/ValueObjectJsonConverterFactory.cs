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
/// by hand. For the first, the assembly that declares the value objects must reference this package too: that
/// reference is what makes the generator register the converter of each one, which the factory then hands out.
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
        "Only reached for a value object that registered no converter, which never happens for a generated one "
        + "declared in an assembly referencing this package, as the documentation requires: those are served from "
        + "the registry, statically. A hand-written value object combined with trimming or native AOT has to supply "
        + "its own converter.";

    /// <inheritdoc />
    /// <remarks>
    /// Claims a value object only, never <see cref="Nullable{T}"/> over one: a converter in the options outranks the
    /// serializer's own handling of nullable types, which wraps the converter of the value object itself once this
    /// one declines.
    /// </remarks>
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        return ValueObjectJsonRegistry.TryGet(typeToConvert, out _)
               || (Nullable.GetUnderlyingType(typeToConvert) is null && ValueObjectRegistry.IsValueObject(typeToConvert));
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

        // The registry describes a value object whatever wraps it, and the factory claims none wrapped in Nullable<T>.
        if (Nullable.GetUnderlyingType(typeToConvert) is not null
            || !ValueObjectRegistry.TryResolve(typeToConvert, out var descriptor))
        {
            return null;
        }

        var converterType = typeof(ValueObjectJsonConverter<,>).MakeGenericType(typeToConvert, descriptor.ValueType);

        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}
