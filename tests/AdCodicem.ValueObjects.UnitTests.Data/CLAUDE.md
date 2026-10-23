# AdCodicem.ValueObjects.UnitTests.Data

The data family's unit tests (Dapper, MongoDB, Identifiers.MongoDB, Marten), offline, in `Persistence/` (`Dapper*`,
`*MongoDb*`, `Marten*`, and `PcrePattern*` for the patterns a MongoDB `$jsonSchema` carries); the integration suite runs
them against the real engines. The suite's conventions are in `tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`, each
package's traps in its `src/<Package>/CLAUDE.md`.

- MongoDB: every test class goes through the one static, strict registration of `Persistence/MongoDb.cs`
  (`ValueObjectBson.Register`, then `EntityIdBson.Register`), since MongoDB.Driver keeps its serializers and class maps
  for the whole process. Another trust or representation means a serializer built by hand, a class map of the test's
  own or a fresh `BsonSerializerRegistry`, which the package's internal checks take as a parameter; a test of
  `Register` refusing goes through `MongoDbStartUpTests`' fresh copies of the driver and of `Fixtures.XmlSerialization`,
  loaded into an `AssemblyLoadContext` of their own. A test of an id generator registered or kept closes
  `HandWrittenId<TProfile>` over a profile of its own. A query's `ToString()` reports a refused translation instead of
  throwing it: a test of a refused query constant renders a `Builders` filter.
- Marten: `Persistence/MartenHarness` builds stores over a port nothing listens on (`Unreachable`, `AutoCreate.None`)
  and reads Marten's SQL (`ToCommand`), compiled-query plans and DDL without a connection; PostgreSQL runs in the
  integration suite. Weasel's `PostgresqlProvider.Instance` mappings, which `UseValueObjects()` fills, are process-wide,
  so a store of a test without the package fails at execution rather than at parsing for any value object another test
  taught: assert what Marten refuses without the package on a value object no store of this project is taught, and
  close a generic or hand-written one the boxed path resolves over a declaration of the test's own
  (`MartenCodeDeclaration`, `MartenStaticDeclaration`, …). An inline array in a `Contains` fails inside Marten
  (`MemoryExtensions.Contains`): hold the values in a static field.
- A Marten compiled query holding a single value object on purpose takes a targeted `#pragma warning disable VO0048`
  around the member alone.
- `Persistence/MartenCompiledQueryDiagnosticTests` runs `VO0048`'s analyzer, loaded from the generator's assembly that
  this project's `CopyValueObjectAnalyzers` target copies to `analyzers/`, over a snippet compiled against the trusted
  platform assemblies, on `Microsoft.CodeAnalysis.CSharp` 4.14.0, the Roslyn the analyzer is built against. Its expected
  message names the package and `UseValueObjects()` through `typeof` and `nameof`, so a rename in the satellites' train
  fails it.
- This project resolves Npgsql 9.0.4, the version Marten asks for; the EfCore project resolves 10.0.3.
- Its `Persistence/MongoDbShapedCode.cs` (`ShapedCode<TShape>`, a value object written by hand for a pattern the
  generator warns on) is linked by the EfCore project and holds no test. From core's folder it links `CodeCarrying.cs`,
  `Identifiers/DeterministicEntropy.cs`, `Persistence/NonUnicodeValueObjects.cs` and `WithoutDynamicCode.cs`
  (`MartenWithoutDynamicCodeTests`).
