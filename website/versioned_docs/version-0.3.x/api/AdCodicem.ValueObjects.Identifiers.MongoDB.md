# Namespace AdCodicem.ValueObjects.Identifiers.MongoDB {#AdCodicem_ValueObjects_Identifiers_MongoDB}

### Classes

 [EntityIdBson](AdCodicem.ValueObjects.Identifiers.MongoDB.EntityIdBson.md)

Registers the id generators of entity identifiers with MongoDB.Driver, so that a document inserted without its
identifier is given a new one.

 [EntityIdGenerator<TId\>](AdCodicem.ValueObjects.Identifiers.MongoDB.EntityIdGenerator\-1.md)

Mints an entity identifier for a document inserted without one, as MongoDB.Driver mints an <code>ObjectId</code>.

