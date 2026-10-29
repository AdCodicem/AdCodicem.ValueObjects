# Namespace AdCodicem.ValueObjects.MongoDB {#AdCodicem_ValueObjects_MongoDB}

### Classes

 [ValueObjectBson](AdCodicem.ValueObjects.MongoDB.ValueObjectBson.md)

Registers the MongoDB.Driver serializers of value objects.

 [ValueObjectBsonSchema](AdCodicem.ValueObjects.MongoDB.ValueObjectBsonSchema.md)

Builds a <code>$jsonSchema</code> collection validator from the rules of the value objects a document holds, so that the
server refuses a document another writer stores with a value the type rejects.

 [ValueObjectBsonSerializer<TSelf, TValue\>](AdCodicem.ValueObjects.MongoDB.ValueObjectBsonSerializer\-2.md)

Stores a value object as the bare value the serializer of its underlying type writes, and reads it back through the
value object's rules.

