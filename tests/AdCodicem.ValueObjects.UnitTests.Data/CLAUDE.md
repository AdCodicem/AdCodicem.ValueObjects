# AdCodicem.ValueObjects.UnitTests.Data

The data family's unit tests (Dapper, MongoDB, Identifiers.MongoDB), offline, in `Persistence/` (`Dapper*`, `*MongoDb*`,
and `PcrePattern*` for the patterns a MongoDB `$jsonSchema` carries); the integration suite runs them against the real
engines. The suite's conventions are in `tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`, each package's traps in its
`src/<Package>/CLAUDE.md`.

- MongoDB: every test class goes through the one static, strict registration of `Persistence/MongoDb.cs`
  (`ValueObjectBson.Register`, then `EntityIdBson.Register`), since MongoDB.Driver keeps its serializers and class maps
  for the whole process. Another trust or representation means a serializer built by hand, a class map of the test's
  own or a fresh `BsonSerializerRegistry`, which the package's internal checks take as a parameter; a test of
  `Register` refusing goes through `MongoDbStartUpTests`' fresh copies of the driver and of `Fixtures.XmlSerialization`,
  loaded into an `AssemblyLoadContext` of their own. A test of an id generator registered or kept closes
  `HandWrittenId<TProfile>` over a profile of its own. A query's `ToString()` reports a refused translation instead of
  throwing it: a test of a refused query constant renders a `Builders` filter.
- Its `Persistence/MongoDbShapedCode.cs` (`ShapedCode<TShape>`, a value object written by hand for a pattern the
  generator warns on) is linked by the EfCore project and holds no test. From core's folder it links `CodeCarrying.cs`,
  `Identifiers/DeterministicEntropy.cs`, `Persistence/NonUnicodeValueObjects.cs` and `WithoutDynamicCode.cs`.
