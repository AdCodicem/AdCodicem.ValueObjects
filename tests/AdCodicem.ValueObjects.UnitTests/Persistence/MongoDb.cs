using AdCodicem.ValueObjects.MongoDB;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// The registration every MongoDB test of the suite goes through, and the plumbing that writes a value through a
/// serializer and reads one back, with no server.
/// </summary>
/// <remarks>
/// MongoDB.Driver keeps its serializers, its serialization providers and its class maps for the whole process, and
/// never forgets one: a value object the driver serialized before <see cref="ValueObjectBson.Register(System.Reflection.Assembly[])"/>
/// would keep the class map it was given, and fail the registration. So every MongoDB test class calls
/// <see cref="EnsureRegistered"/> first, which registers the <see cref="Guid"/> representation and the provider once,
/// strict; a test that needs another trust or another representation builds its serializer by hand, or uses a registry
/// of its own, and leaves the driver's alone.
/// </remarks>
internal static class MongoDb
{
    private static readonly Lazy<bool> Registration = new(Register);

    /// <summary>
    /// Registers the driver's <see cref="Guid"/> representation, then the value objects of the suite, once.
    /// </summary>
    public static void EnsureRegistered() => _ = Registration.Value;

    /// <summary>
    /// Writes a value through a serializer, as the value of an element, and hands the BSON written back.
    /// </summary>
    /// <typeparam name="T">The type the serializer writes.</typeparam>
    /// <param name="serializer">The serializer.</param>
    /// <param name="value">The value.</param>
    /// <returns>The BSON value written.</returns>
    public static BsonValue Write<T>(IBsonSerializer<T> serializer, T value)
    {
        var document = new BsonDocument();
        using (var writer = new BsonDocumentWriter(document))
        {
            writer.WriteStartDocument();
            writer.WriteName("v");
            serializer.Serialize(BsonSerializationContext.CreateRoot(writer), value);
            writer.WriteEndDocument();
        }

        return document["v"];
    }

    /// <summary>
    /// Reads a BSON value through a serializer, as the value of an element.
    /// </summary>
    /// <typeparam name="T">The type the serializer reads.</typeparam>
    /// <param name="serializer">The serializer.</param>
    /// <param name="value">The BSON value.</param>
    /// <returns>What the serializer read.</returns>
    public static T Read<T>(IBsonSerializer<T> serializer, BsonValue value)
    {
        using var reader = new BsonDocumentReader(new BsonDocument("v", value));
        reader.ReadStartDocument();
        reader.ReadName("v");

        return serializer.Deserialize(BsonDeserializationContext.CreateRoot(reader));
    }

    private static bool Register()
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        ValueObjectBson.Register(typeof(Iban).Assembly);

        return true;
    }
}
