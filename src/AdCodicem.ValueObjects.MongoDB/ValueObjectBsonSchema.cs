using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace AdCodicem.ValueObjects.MongoDB;

/// <summary>
/// Builds a <c>$jsonSchema</c> collection validator from the rules of the value objects a document holds, so that the
/// server refuses a document another writer stores with a value the type rejects.
/// </summary>
/// <remarks>
/// <para>
/// A collection has no schema, and a strict read refuses, one document at a time, what another service, a script or
/// Compass stored. A validator built here makes the server refuse it on the write instead, with code 121, "Document
/// failed validation", naming the field and the rule:
/// </para>
/// <code>
/// await database.CreateCollectionAsync("orders", new CreateCollectionOptions&lt;BsonDocument&gt;
/// {
///     Validator = new BsonDocumentFilterDefinition&lt;BsonDocument&gt;(ValueObjectBsonSchema.For&lt;Order&gt;()),
/// });
/// </code>
/// <para>
/// Each rule is carried only where the server refuses no value the type accepts, since a validator stricter than the
/// type would refuse the application's own writes. <c>MaxLength</c> becomes <c>maxLength</c>; <c>MinLength</c> becomes
/// <c>minLength</c>, halved unless the pattern keeps every value in the Basic Multilingual Plane, since the server counts
/// a character outside it as one where .NET counts two; the pattern behind <see cref="IValueObjectPatternValidator"/>
/// becomes <c>pattern</c>, written in the PCRE2 dialect the server reads, a class escape listed as the characters .NET's
/// own Unicode tables give it, when it stays within what both engines read alike and sets no option such as
/// <c>IgnoreCase</c>; the bounds become <c>minimum</c> and <c>maximum</c>, written as the serializer writes a number; a
/// closed set becomes <c>enum</c>, unless the type stores another spelling of a known value, as a set looked up ignoring
/// case does; and the description becomes <c>description</c>. Rules only code checks, a checksum in
/// <see cref="IValueObjectValidator{TValue}"/>, stay on the read path.
/// </para>
/// <para>
/// The document is described as its class map writes it, element names and serialization options included: a value
/// object's <c>bsonType</c> is the BSON type the serializer of its underlying value writes, <c>binData</c> for a
/// <see cref="Guid"/> under the standard representation, <c>string</c> for one given
/// <c>[BsonRepresentation(BsonType.String)]</c>. A value object member is <c>required</c>, unless the class map leaves it
/// out when it is the default or <see langword="null"/>; a <c>TSelf?</c> is not, and adds <c>null</c> to its
/// <c>bsonType</c> and to its <c>enum</c>. A document nested in a member, and the elements of an array, are described
/// the same way, where they hold a value object. A dictionary, a member with a serializer of the application's own, and
/// whatever holds no value object, are left free.
/// </para>
/// <para>
/// The server checks an update as it checks an insert, so a validator also catches what no serializer sees, such as an
/// <c>Update.Inc(x =&gt; x.Quantity.Value, 1)</c> past the maximum. <c>validationLevel</c> and
/// <c>validationAction</c> are the application's to set, beside the validator, on <c>createCollection</c> or
/// <c>collMod</c>.
/// </para>
/// </remarks>
public static class ValueObjectBsonSchema
{
    /// <summary>
    /// Builds <c>{ "$jsonSchema": { … } }</c> for a document type, from its class map and the rules of each value object
    /// it holds.
    /// </summary>
    /// <typeparam name="TDocument">The type of the documents the collection holds.</typeparam>
    /// <returns>The validator, to pass to <c>createCollection</c> or <c>collMod</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// <see cref="ValueObjectBson"/> has not registered its serializers yet, or the driver serializes
    /// <typeparamref name="TDocument"/> through a serializer of its own rather than a class map.
    /// </exception>
    /// <remarks>
    /// It reads the serializer the driver gives each member, and builds the class maps of the document and of the
    /// classes nested in it, which the driver keeps for good. So call it at start-up, after
    /// <see cref="ValueObjectBson.Register(System.Reflection.Assembly[])"/>, without which a value object would be given
    /// a class map; after <c>EntityIdBson.Register</c>, whose id generators a class map takes when it is built; and after
    /// any <c>BsonClassMap.RegisterClassMap</c> of those types, which fails once one is built.
    /// </remarks>
    public static BsonDocument For<TDocument>()
    {
        if (!ValueObjectBson.IsRegistered)
        {
            throw new InvalidOperationException(
                "ValueObjectBsonSchema reads the serializer MongoDB.Driver gives each member, which a value object is "
                + "given a class map for, for the rest of the process, unless ValueObjectBson.Register ran first. Call "
                + "ValueObjectBson.Register at start-up, before building a validator.");
        }

        var serializer = BsonSerializer.LookupSerializer<TDocument>();
        if (!IsClassMapSerializer(serializer))
        {
            throw new InvalidOperationException(
                $"MongoDB.Driver serializes {typeof(TDocument).Name} through {serializer.GetType().Name}, not through a "
                + "class map, so the validator cannot tell what it writes.");
        }

        var schema = new BsonDocument("bsonType", "object");
        if (DescribeClass(BsonClassMap.LookupClassMap(typeof(TDocument)), []) is { } described)
        {
            schema.AddRange(described);
        }

        return new BsonDocument("$jsonSchema", schema);
    }

    /// <summary>
    /// Describes the members of a class map that hold a value object.
    /// </summary>
    /// <param name="classMap">The class map.</param>
    /// <param name="path">The classes being described, from the document down, which a class referring to itself stops at.</param>
    /// <returns><c>required</c> and <c>properties</c>, or <see langword="null"/> when no member holds a value object.</returns>
    private static BsonDocument? DescribeClass(BsonClassMap classMap, HashSet<Type> path)
    {
        if (!path.Add(classMap.ClassType))
        {
            return null;
        }

        var properties = new BsonDocument();
        var required = new BsonArray();
        foreach (var member in classMap.AllMemberMaps)
        {
            var (rules, valueObject) = DescribeMember(member.MemberType, member.GetSerializer(), path);
            if (rules is null)
            {
                continue;
            }

            properties[member.ElementName] = rules;
            if (valueObject && !member.IgnoreIfDefault && !member.IgnoreIfNull && member.ShouldSerializeMethod is null)
            {
                required.Add(member.ElementName);
            }
        }

        path.Remove(classMap.ClassType);
        if (properties.ElementCount == 0)
        {
            return null;
        }

        var described = new BsonDocument();
        if (required.Count > 0)
        {
            described["required"] = required;
        }

        described["properties"] = properties;
        return described;
    }

    /// <summary>
    /// Describes what a member's serializer writes, where it holds a value object.
    /// </summary>
    /// <param name="type">The type of the member.</param>
    /// <param name="serializer">The serializer the class map gives it.</param>
    /// <param name="path">The classes being described.</param>
    /// <returns>
    /// The keywords, or <see langword="null"/> when the member holds no value object, and whether it is a value object
    /// itself, which a document always holds.
    /// </returns>
    private static (BsonDocument? Rules, bool ValueObject) DescribeMember(Type type, IBsonSerializer serializer, HashSet<Type> path)
    {
        if (serializer is IValueObjectBsonSerializer valueObject)
        {
            return (valueObject.DescribeRules(), true);
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying && serializer is IChildSerializerConfigurable nullable)
        {
            var (rules, _) = DescribeMember(underlying, nullable.ChildSerializer, path);
            if (rules is not null)
            {
                if (rules.TryGetValue("bsonType", out var bsonType))
                {
                    rules["bsonType"] = new BsonArray { bsonType, "null" };
                }

                if (rules.TryGetValue("enum", out var values))
                {
                    values.AsBsonArray.Add(BsonNull.Value);
                }
            }

            return (rules, false);
        }

        // A dictionary is an array serializer too, of its key and value pairs, which a document representation does not
        // store as an array.
        if (serializer is not IBsonDictionarySerializer
            && serializer is IBsonArraySerializer array
            && array.TryGetItemSerializationInfo(out var item))
        {
            var (items, _) = DescribeMember(item.NominalType, item.Serializer, path);
            return (items is null ? null : new BsonDocument("items", items), false);
        }

        // A nested document: no bsonType, so that a member holding null, or another writer's array, is the server's to
        // accept, as properties checks a document only.
        return IsClassMapSerializer(serializer)
            ? (DescribeClass(BsonClassMap.LookupClassMap(serializer.ValueType), path), false)
            : (null, false);
    }

    /// <summary>
    /// Tells a serializer the driver builds over a class map.
    /// </summary>
    private static bool IsClassMapSerializer(IBsonSerializer serializer)
        => serializer.GetType() is { IsGenericType: true } type && type.GetGenericTypeDefinition() == typeof(BsonClassMapSerializer<>);
}
