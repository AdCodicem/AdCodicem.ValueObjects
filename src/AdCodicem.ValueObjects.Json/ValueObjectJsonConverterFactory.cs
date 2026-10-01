using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        return ValueObjectJsonRegistry.TryGet(typeToConvert, out _) || TryGetValueType(typeToConvert, out _);
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

        if (!TryGetValueType(typeToConvert, out var valueType))
        {
            return null;
        }

        var converterType = typeof(ValueObjectJsonConverter<,>).MakeGenericType(typeToConvert, valueType);

        return (JsonConverter?)Activator.CreateInstance(converterType);
    }

    /// <summary>
    /// Finds the underlying type of a struct implementing <see cref="IValueObject{TSelf, TValue}"/> over itself, the
    /// only shape <see cref="ValueObjectJsonConverter{TSelf, TValue}"/> can be built for.
    /// </summary>
    /// <param name="type">Candidate type.</param>
    /// <param name="valueType">The underlying type when the candidate qualifies.</param>
    /// <returns><see langword="true"/> when a converter can be built for <paramref name="type"/>.</returns>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:UnrecognizedReflectionPattern",
        Justification = "The interface list of a value object is preserved: the type is referenced by the caller and its IValueObject implementation is part of its public contract.")]
    private static bool TryGetValueType(Type type, [NotNullWhen(true)] out Type? valueType)
    {
        // The marker is a cheap filter for the many structs a serializer meets that are not value objects at all.
        if (type.IsValueType && typeof(IValueObject).IsAssignableFrom(type))
        {
            foreach (var candidate in type.GetInterfaces())
            {
                if (candidate.IsGenericType
                    && candidate.GetGenericTypeDefinition() == typeof(IValueObject<,>)
                    && candidate.GetGenericArguments()[0] == type)
                {
                    valueType = candidate.GetGenericArguments()[1];
                    return true;
                }
            }
        }

        valueType = null;
        return false;
    }
}
