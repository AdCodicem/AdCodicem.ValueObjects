using Orleans.Serialization.Codecs;
using Orleans.Serialization.Serializers;

namespace AdCodicem.ValueObjects.Orleans;

/// <summary>
/// Answers Orleans with the codec of one value object, registered by its type arguments.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <param name="trusted">Whether the codec reads without validating.</param>
/// <remarks>
/// A specializable codec rather than a field codec in Orleans' type manifest, which Orleans looks up by the generic
/// definition of a constructed type: a construction of a generic value object is found this way too.
/// </remarks>
internal sealed class ValueObjectCodecRegistration<TSelf, TValue>(bool trusted) : ISpecializableCodec
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    private readonly ValueObjectCodec<TSelf, TValue> _codec = new(trusted);

    /// <inheritdoc />
    public bool IsSupportedType(Type type) => type == typeof(TSelf);

    /// <inheritdoc />
    public IFieldCodec GetSpecializedCodec(Type type) => _codec;
}
