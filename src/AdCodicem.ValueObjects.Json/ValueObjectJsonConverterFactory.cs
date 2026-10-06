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
/// by hand. For the first, it hands out the converter the generator registered with the value object's descriptor,
/// whatever the assembly declaring the value object references. For a construction of a generic value object, whose
/// registration registers its generic definition alone, the registry closes the generated converter over the
/// construction, by reflection, the first time it is asked for it.
/// </para>
/// <para>
/// A value object written by hand carries no converter, unless it was registered with one, and gets the general-purpose
/// <see cref="ValueObjectJsonConverter{TSelf, TValue}"/>, which the factory closes over it through the type arguments its
/// descriptor hands back (<see cref="ValueObjectDescriptor.Accept{TResult}(IValueObjectVisitor{TResult})"/>), at compile
/// time rather than by reflection. Under native AOT, register it,
/// <c>ValueObjectRegistry.Register&lt;Link, Uri&gt;(Link.Schema)</c>, and list it in the context: one nothing registered
/// is described by reflection, which only the JIT can do.
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
    private const string UnregisteredOnly =
        "ValueObjectRegistry.TryResolve reflects only for a value object nothing registered: one written by hand, or a "
        + "construction of a generic one. A generated value object registers itself, and its converter, statically. Under "
        + "trimming or native AOT, register the others through ValueObjectRegistry.Register: a construction with its "
        + "generated converter, a value object written by hand with none, over which the general-purpose converter is "
        + "then closed at compile time, through its descriptor.";

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
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = UnregisteredOnly)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = UnregisteredOnly)]
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

        // A generated value object whose module had not registered it yet: resolving it ran the registration.
        if (descriptor.JsonConverter is { } carried)
        {
            return carried;
        }

        // Closed over the type arguments the descriptor hands back, at compile time: native AOT has no code to close the
        // converter over a struct at run time.
        return descriptor.Accept(GeneralPurposeConverter.Instance);
    }

    /// <summary>
    /// Creates the general-purpose converter of the value object a descriptor stands for, closed over the type arguments
    /// it hands back.
    /// </summary>
    private sealed class GeneralPurposeConverter : IValueObjectVisitor<JsonConverter>
    {
        public static readonly GeneralPurposeConverter Instance = new();

        /// <summary>
        /// Creates the converter. It needs no filter: the underlying value goes through whatever contract the options
        /// hold for its type, and System.Text.Json itself refuses a type they hold none for.
        /// </summary>
        /// <typeparam name="TSelf">Value object type.</typeparam>
        /// <typeparam name="TValue">Underlying value type.</typeparam>
        /// <returns>The general-purpose converter of the value object.</returns>
        public JsonConverter Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => new ValueObjectJsonConverter<TSelf, TValue>();
    }
}
