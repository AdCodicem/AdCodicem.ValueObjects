using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace AdCodicem.ValueObjects.MongoDB;

/// <summary>
/// Refuses to write or read a value object over a type MongoDB.Bson has no representation for, which it would otherwise
/// write as <c>{}</c> and read back as the default.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <param name="valueType">Its underlying type.</param>
internal sealed class UnrepresentableValueObjectSerializer<TSelf>(Type valueType) : SerializerBase<TSelf>
    where TSelf : struct
{
    /// <inheritdoc />
    /// <exception cref="BsonSerializationException">Always.</exception>
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TSelf value)
        => throw Unrepresentable();

    /// <inheritdoc />
    /// <exception cref="BsonSerializationException">Always.</exception>
    public override TSelf Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        => throw Unrepresentable();

    /// <summary>
    /// Reports the value object and the remedy.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    private BsonSerializationException Unrepresentable()
        => new($"{typeof(TSelf).Name} is a value object over {valueType.Name}, which MongoDB has no representation for. "
               + $"Register a serializer of the application's own for {valueType.Name}, with "
               + "BsonSerializer.RegisterSerializer, before anything is serialized: the value object then writes what "
               + "that serializer writes.");
}
