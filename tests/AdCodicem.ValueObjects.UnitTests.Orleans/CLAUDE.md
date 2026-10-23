# AdCodicem.ValueObjects.UnitTests.Orleans

The orleans family's unit tests (`AdCodicem.ValueObjects.Orleans`), in `Grains/`, through serializers built per test
and an in-process cluster. The suite's conventions are in `tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`, the
package's traps in `src/AdCodicem.ValueObjects.Orleans/CLAUDE.md`.

- Serializers are built per test configuration (`Grains/OrleansWire`), none process-wide; `OrleansParity` checks each
  sample through `AddValueObjects()` strict and `AddValueObject<TSelf, TValue>(trusted: true)`, a serializer per type
  built once.
- Grain calls go through `GrainCluster`, one in-process cluster per test class (about 1.5 s the first time, 0.3 s
  after); a test needing another configuration starts its own with `GrainCluster.StartAsync` and disposes of it. The
  grains are `Fixtures.Grains`' (`tests/Fixtures/CLAUDE.md`).
- A generic value object or one written by hand that a boxed path resolves is closed over a marker or a declaration of
  the test's own (`OrleansCodecOwner`, `OrleansCodeDeclaration`, …).
- Microsoft.Orleans.TestingHost brings Orleans' code generator, its analyzers and global usings of `Orleans`,
  `Orleans.Hosting` and `Orleans.Runtime` into this project (through Microsoft.Orleans.Sdk): a type of this project
  named like a public type of those namespaces is ambiguous (CS0104), so name it apart. Its generated `ApplicationPart`
  list is how a test cluster finds the fixture's grains.
- Orleans' `Deserialize<T>` returns `[MaybeNull]`: read a holder through `OrleansWire.Read<T>`. Bytes no serializer
  writes are crafted field by field through `OrleansWire.Craft`; a writer applies its own type filters, so bytes naming
  a type the reader must refuse go through `Craft(services, …)` over services allowing that type.
- This project references no `Microsoft.CodeAnalysis.CSharp`: Microsoft.Orleans.Serialization brings
  `Microsoft.CodeAnalysis.Common` 5.0.0 here, beside which the 4.14.0 pin fails the restore (NU1608).
- It links core's `WithoutDynamicCode.cs` (`Grains/OrleansWithoutDynamicCodeTests`), and embeds the source of
  `SerializerContractAnalyzer.cs`, which `Grains/OrleansRemedyNameTests` reads for the names `VO0043` writes of the
  package: with no `Microsoft.CodeAnalysis.CSharp` here, it cannot load the analyzer.
