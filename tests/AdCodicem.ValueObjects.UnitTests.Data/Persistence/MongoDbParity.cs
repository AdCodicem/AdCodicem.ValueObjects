using AdCodicem.ValueObjects.MongoDB;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// What a value object is stored as through MongoDB.Driver, beside what the bare value it carries is stored as, for the
/// samples of <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class MongoDbParity
{
    /// <summary>
    /// Checks that the serializer the driver resolves for a value object writes exactly the BSON the serializer of its
    /// underlying type writes for the value it carries, and reads that BSON back as the value the primitive reads, strict
    /// and trusted; and, for a value object over a 128-bit integer, which MongoDB.Bson has no serializer for, that it is
    /// refused both ways instead of being written as <c>{}</c>.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="value">An instance.</param>
    public static void Check<TSelf, TValue>(TSelf value)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        MongoDb.EnsureRegistered();
        var name = typeof(TSelf).Name;
        var serializer = BsonSerializer.LookupSerializer<TSelf>();

        if (typeof(TValue) == typeof(Int128) || typeof(TValue) == typeof(UInt128))
        {
            var message = $"{name} is a value object over {typeof(TValue).Name}, which MongoDB has no representation for.*";
            FluentActions.Invoking(() => MongoDb.Write(serializer, value))
                .Should().Throw<BsonSerializationException>().WithMessage(message);
            FluentActions.Invoking(() => MongoDb.Read(serializer, new BsonInt32(1)))
                .Should().Throw<BsonSerializationException>().WithMessage(message);
            return;
        }

        serializer.Should().BeOfType<ValueObjectBsonSerializer<TSelf, TValue>>();
        var primitive = BsonSerializer.LookupSerializer<TValue>();

        var written = MongoDb.Write(serializer, value);
        written.Should().Be(MongoDb.Write(primitive, value.Value), "{0} is stored as its {1} is", name, typeof(TValue).Name);

        var expected = MongoDb.Read(primitive, written);
        MongoDb.Read(serializer, written).Value.Should().Be(expected, "{0} reads back what its {1} reads", name, typeof(TValue).Name);
        MongoDb.Read(new ValueObjectBsonSerializer<TSelf, TValue>(primitive, trusted: true), written).Value
            .Should().Be(expected, "a trusted {0} reads the same", name);
    }
}
