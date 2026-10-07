using MongoDB.Bson.Serialization;

namespace AdCodicem.ValueObjects.Identifiers.MongoDB;

/// <summary>
/// Mints an entity identifier for a document inserted without one, as MongoDB.Driver mints an <c>ObjectId</c>.
/// </summary>
/// <typeparam name="TId">The identifier type.</typeparam>
/// <remarks>
/// <para>
/// The driver asks the generator of a document's id member, before it serializes the document, whether the id is still
/// unassigned, and has it mint one if so. An unassigned identifier is the default instance, which holds no valid value:
/// without a generator, the serializer of <c>AdCodicem.ValueObjects.MongoDB</c> would refuse to write it, with
/// <c>value_object.required</c>.
/// </para>
/// <para>
/// <see cref="EntityIdBson"/> registers one per identifier type, which the driver's class maps take up when they are
/// built; a class map built by hand sets it with <see cref="BsonMemberMap.SetIdGenerator(IIdGenerator)"/>.
/// </para>
/// </remarks>
public sealed class EntityIdGenerator<TId> : IIdGenerator
    where TId : struct, IEntityId<TId>
{
    /// <summary>
    /// Mints a new identifier, through <see cref="IEntityId{TSelf}.New()"/>, under the clock and the entropy
    /// <see cref="ValueObjectIds"/> holds for the current flow.
    /// </summary>
    /// <param name="container">The collection the document is inserted into.</param>
    /// <param name="document">The document.</param>
    /// <returns>The new identifier, boxed.</returns>
    public object GenerateId(object container, object document) => TId.New();

    /// <summary>
    /// Tells an unassigned identifier: the default instance, or anything that is not an identifier of the type.
    /// </summary>
    /// <param name="id">The value of the document's id member.</param>
    /// <returns><see langword="true"/> when the document needs a new identifier.</returns>
    public bool IsEmpty(object? id) => id is not TId identifier || IsDefault(identifier);

    /// <summary>
    /// Reads <c>IsDefault</c>, which a value object implements explicitly, through the interface.
    /// </summary>
    private static bool IsDefault<T>(in T value)
        where T : struct, IValueObject<T, string>
        => value.IsDefault;
}
