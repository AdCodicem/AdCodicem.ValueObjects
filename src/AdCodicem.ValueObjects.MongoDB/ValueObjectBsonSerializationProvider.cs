using AdCodicem.ValueObjects.Metadata;
using MongoDB.Bson.Serialization;

namespace AdCodicem.ValueObjects.MongoDB;

/// <summary>
/// Hands the driver a serializer for each value object it meets, closed over the value object at compile time through
/// the descriptor's visitor.
/// </summary>
/// <param name="trusted">Whether the serializers it builds read without validating.</param>
/// <remarks>
/// <para>
/// It answers nothing for a <see cref="Nullable{T}"/>: the driver wraps the serializer of the value object in its own
/// <see cref="global::MongoDB.Bson.Serialization.Serializers.NullableSerializer{T}"/> for a <c>TSelf?</c>, which writes
/// and reads a BSON <c>null</c> itself, whereas the registry, asked for <c>TSelf?</c>, would answer with the descriptor of
/// <c>TSelf</c>.
/// </para>
/// <para>
/// A value object the registry holds is closed through <see cref="ValueObjectDescriptor.Accept{TResult}"/>. One it does
/// not hold yet, a construction of a generic value object nothing registered, a value object written by hand, a value
/// object of a module whose registration has not run, is described by <see cref="ValueObjectRegistry.TryResolve"/>, by
/// reflection, which only the JIT can run.
/// </para>
/// </remarks>
internal sealed class ValueObjectBsonSerializationProvider(bool trusted) : IRegistryAwareBsonSerializationProvider
{
    /// <summary>
    /// Gets whether the serializers it builds read without validating.
    /// </summary>
    public bool Trusted { get; } = trusted;

    /// <inheritdoc />
    public IBsonSerializer? GetSerializer(Type type) => GetSerializer(type, BsonSerializer.SerializerRegistry);

    /// <inheritdoc />
    public IBsonSerializer? GetSerializer(Type type, IBsonSerializerRegistry serializerRegistry)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(serializerRegistry);

        if (!type.IsValueType
            || Nullable.GetUnderlyingType(type) is not null
            || !ValueObjectRegistry.TryResolve(type, out var descriptor))
        {
            return null;
        }

        return descriptor.Accept(new SerializerFactory(serializerRegistry, Trusted));
    }

    /// <summary>
    /// Builds the serializer of a value object over the serializer a registry holds for its underlying type.
    /// </summary>
    /// <param name="serializerRegistry">The registry the serializer of the underlying type is taken from.</param>
    /// <param name="trusted">Whether the serializer reads without validating.</param>
    internal sealed class SerializerFactory(IBsonSerializerRegistry serializerRegistry, bool trusted)
        : IValueObjectVisitor<IBsonSerializer>
    {
        /// <summary>
        /// Builds the serializer of the value object, or one that refuses it when it is over a 128-bit integer
        /// MongoDB.Bson has no representation for.
        /// </summary>
        /// <typeparam name="TSelf">Value object type.</typeparam>
        /// <typeparam name="TValue">Underlying value type.</typeparam>
        /// <returns>The serializer.</returns>
        /// <remarks>
        /// MongoDB.Bson has no serializer for <see cref="Int128"/> or <see cref="System.UInt128"/>, and maps either as a class,
        /// through a class map that writes no member: a bare one becomes <c>{}</c> and reads back as zero. A value object
        /// over one refuses rather than lose its value, unless the application registered a serializer of its own for the
        /// underlying type, which it then goes through as over any other type.
        /// </remarks>
        public IBsonSerializer Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
        {
            var valueSerializer = serializerRegistry.GetSerializer<TValue>();

            return ValueObjectBson.Is128Bit(typeof(TValue)) && valueSerializer is BsonClassMapSerializer<TValue>
                ? new UnrepresentableValueObjectSerializer<TSelf>(typeof(TValue))
                : new ValueObjectBsonSerializer<TSelf, TValue>(valueSerializer, trusted);
        }
    }
}
