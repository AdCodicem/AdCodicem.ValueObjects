using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.Metadata;
using Orleans.Serialization.Codecs;
using Orleans.Serialization.Serializers;

namespace AdCodicem.ValueObjects.Orleans;

/// <summary>
/// Answers Orleans with the codec of any value object, closed over it through the descriptor's visitor.
/// </summary>
/// <param name="trusted">Whether the codecs read without validating.</param>
/// <remarks>
/// Orleans asks a specializable codec only for a type no codec of its own serves, and keeps what it answers, so each value
/// object is resolved once per serializer. A <see cref="Nullable{T}"/> of a value object is left to Orleans, whose codec of
/// it asks for the value object's.
/// </remarks>
internal sealed class ValueObjectCodecSelector(bool trusted) : ISpecializableCodec
{
    private const string UnregisteredOnly =
        "ValueObjectRegistry.TryResolve reflects only for a value object nothing registered: one written by hand, or a "
        + "construction of a generic one. Without dynamic code, as under native AOT, it is not called, and "
        + "AddValueObject<TSelf, TValue>() registers such a type at compile time.";

    /// <summary>
    /// Gets whether the codecs read without validating.
    /// </summary>
    internal bool Trusted { get; } = trusted;

    /// <inheritdoc />
    public bool IsSupportedType(Type type)
        => Nullable.GetUnderlyingType(type) is null && ValueObjectRegistry.IsValueObject(type);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// The type is a value object nothing registered, and the runtime does not support the dynamic code describing it by
    /// reflection takes.
    /// </exception>
    public IFieldCodec GetSpecializedCodec(Type type) => Describe(type).Accept(new CodecFactory(Trusted));

    /// <summary>
    /// Finds the descriptor of a value object, registered, or described by reflection where the runtime supports dynamic
    /// code.
    /// </summary>
    /// <param name="type">A type <see cref="IsSupportedType"/> admitted as a value object.</param>
    /// <returns>Its descriptor.</returns>
    /// <exception cref="NotSupportedException">
    /// Nothing registered the value object, and the runtime does not support dynamic code.
    /// </exception>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = UnregisteredOnly)]
    private static ValueObjectDescriptor Describe(Type type)
    {
        if (ValueObjectRegistry.TryGet(type, out var descriptor))
        {
            return descriptor;
        }

        // Describing a value object nothing registered takes dynamic code, which native AOT has not: there, the reflection
        // would fail inside the registry, so the selector says what the type needs.
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            throw new NotSupportedException(
                $"'{type}' is a value object nothing registered, and describing it by reflection takes dynamic code, which "
                + "this runtime does not support. Register it at start-up, "
                + "ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema), or serialize it with "
                + "AddValueObject<TSelf, TValue>().");
        }

        // IsSupportedType admitted the type as a value object, which TryResolve describes exactly then.
        _ = ValueObjectRegistry.TryResolve(type, out descriptor);
        return descriptor!;
    }

    /// <summary>
    /// Builds the codec of a value object, closed over the type arguments the descriptor hands back.
    /// </summary>
    /// <param name="trusted">Whether the codec reads without validating.</param>
    private sealed class CodecFactory(bool trusted) : IValueObjectVisitor<IFieldCodec>
    {
        /// <inheritdoc />
        public IFieldCodec Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => new ValueObjectCodec<TSelf, TValue>(trusted);
    }
}
