# AdCodicem.ValueObjects.GeneratorTests

The generator, every diagnostic, the analyzers and code fixes, incremental caching, and every published C# snippet.

- Drive Roslyn through `Harness/GeneratorHarness.cs`, not `Microsoft.CodeAnalysis.Testing`, which binds to xUnit v2.
- Snippets compile **without** implicit usings: that is what catches an unqualified name in emitted code.
- Snippets compile with the Roslyn of the `Directory.Packages.props` pins (4.14) under `LanguageVersion.Preview`, which
  runs a syntax node action three times per node inside a C# 14 `extension` block, where the SDK's compilers run it
  once: a `VO0010` test puts no lambda and no local function in one.
- The framework's regex generator runs beside this one, so a snippet implementing `IValueObjectPatternValidator`
  compiles: the `CopyRegexGenerator` target copies it from the targeting pack the SDK resolved, so the SDK decides its
  version, on a laptop and in CI alike.
- Assert caching on `IncrementalStepRunReason`, the only way to notice a caching regression, which breaks nothing visible
  while making every IDE keystroke re-run the pipeline.
- An `[Obsolete(error: true)]` diagnostic (`VO0021`, `VO0028`, `VO0034`, `VO0035`) is tested by compiling a snippet and
  reading it among the compilation's diagnostics: no `#pragma` silences it, and the compiler reports it nowhere inside a
  member or type itself `[Obsolete]`.
- The harness ignores CS1701 and CS1702, which the SDK passes as `/nowarn`, for protobuf-net's assemblies built against
  .NET 8. The project references `AdCodicem.ValueObjects.ProtobufNet` for `SerializerContractAnalyzerTests` and
  `SerializerContractCodeFixTests` alone (`VO0043`'s fix names its serializer); they declare the other serializers'
  attributes under their real names in a stand-in assembly, and `AdCodicem.ValueObjects.Orleans`' registration, whose
  reach `VO0043`'s remedy reads, as a stand-in too: the real package brings Orleans' Roslyn 5 graph into a project
  compiled against the 4.14 pins.
- `MartenCompiledQueryAnalyzerTests` compiles against stand-ins of Marten's compiled-query interfaces, `[MartenIgnore]`
  and JasperFx's attributes under their real names (`MartenReferences`): the real Marten would bring JasperFx's source
  generators and Marten's graph into a project on the 4.14 pins. The unit suite checks the names against Marten itself.
- `withLoggingGenerator: true` runs Microsoft.Gen.Logging, which the `CopyLoggingGenerator` target copies from the
  pinned Microsoft.Extensions.Telemetry.Abstractions; `GeneratorRun.LoggingSource` holds what it wrote and
  `LoggingSnippets.LoggedTags` reads its tags. `DataClassificationAnalyzerTests` and `CopyClassificationCodeFixTests`
  assert that a warned tag lands in the generator's plain tag array and a fixed one in its classified array.
  `LoggingGeneratorAssumptionsTests` pins the logging generator's behaviour `VO0045` and `VO0046` rest on, its defects
  first, so a bump changing one fails there.

## Published snippets

- `DocumentationSnippetTests` runs the generator and six analyzers over every ```` ```csharp ```` block of
  `skills/value-objects/`, `README.md` and `website/docs/` (the homepage example included, through
  `website/docs/_homepage-example.md`; `website/versioned_docs/` is out of scope, describing older releases), compiled
  against Microsoft.Extensions.Logging.Abstractions and Microsoft.Extensions.Telemetry.Abstractions with
  Microsoft.Gen.Logging running.
- A skill snippet must compile outright; a README or site snippet is prose and may elide a body, so only the declaration
  the generator sees is held to account. `VO0042` is never checked there (it needs `PublishAot`).
- Tag a wiring or usage fragment ```` ```csharp skip ````, and any block showing a third-party serializer's attribute
  (Orleans, protobuf-net, MessagePack, MemoryPack, Newtonsoft.Json): the snippets reference none of them, while
  `[DataContract]`, `[XmlType]` and `[Serializable]` come with the framework.
- `SkillCoverageTests` reads the shipped assemblies and fails when an option, a hook, a diagnostic or an error code is
  missing from `skills/value-objects/`: add the entry it names.
- A test reading a file of the repository, through a `RepositoryRoot()` or `SkillDirectory()` of any type or a method
  looking for `AdCodicem.ValueObjects.slnx`, from its body, its data, its class's set-up or a fixture, carries
  `[Trait("Reads", "Documentation")]`, on itself or its class: a pull request changing only `README.md`,
  `website/docs/` or `skills/value-objects/` runs those tests alone (`.github/scripts/ci-plan.sh`), and
  `DocumentationTraitTests` fails on a reading test without the trait or a marked test that reads nothing.
