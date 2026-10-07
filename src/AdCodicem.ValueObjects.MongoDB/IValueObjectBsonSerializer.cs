using MongoDB.Bson;

namespace AdCodicem.ValueObjects.MongoDB;

/// <summary>
/// A serializer of this package, which knows the value object it stores, and so the rules a validator can carry to the
/// server.
/// </summary>
internal interface IValueObjectBsonSerializer
{
    /// <summary>
    /// Writes the rules of the value object as the keywords of a <c>$jsonSchema</c>, applied to what the serializer
    /// stores.
    /// </summary>
    /// <returns>The keywords.</returns>
    BsonDocument DescribeRules();
}
