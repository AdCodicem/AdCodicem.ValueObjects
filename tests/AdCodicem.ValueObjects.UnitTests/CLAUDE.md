# AdCodicem.ValueObjects.UnitTests

The behaviour of generated code, over the value objects of `Domain/`, and of every package called directly. The traps of
a package are in its own `src/<Package>/CLAUDE.md`; this file holds the suite's conventions.

## Layout

- `Domain/`: the suite's value objects. `Domain/UnderlyingTypes.cs` declares one per underlying type and per option or
  hook the rest leaves out; it is linked by the integration suite and `tests/NativeAot`, so a change there changes them.
  `Domain/HandWritten/`: value objects written by hand, the only way to reach the interface defaults and the registry's
  reflection fallback (`HandWrittenCode.cs`, `HandWrittenLink.cs` and `UnregisteredCode.cs` are linked by
  `tests/NativeAot` too, and `HandWrittenCode.cs` by the Functions and Wolverine.Http fixtures of `tests/Fixtures`).
- `GeneratedSurface/` runs every emitted member family on every sample, so nothing the generator emits only compiles, and
  holds every sample to each integration. To add an integration: declare an abstract check on `Sample`
  (`GeneratedSurface/Sample.cs`), implement it in `Sample<TSelf, TValue>` by calling an `…Parity` helper kept in the
  integration's own folder (`Forms/FormsParity.cs`, `BinarySerialization/ProtoParity.cs`, …), and add a theory over
  every sample.
- Folders, named apart from the packages and libraries they test (root `CLAUDE.md`): `Web/` (MVC, minimal APIs, OpenAPI,
  Swashbuckle, NSwag, JSON Patch, DataAnnotations through hosts), `Persistence/` (EF Core, Dapper, MongoDB, Marten as
  `Marten*`),
  `BinarySerialization/` (MessagePack; protobuf-net as `Proto*`; gRPC as `CodeFirstGrpc*` and `ContractFirstGrpc*`),
  `Grains/` (Orleans), `GraphQL/` (HotChocolate, HotChocolate.Data), `Forms/` (Blazor), `Cli/` (System.CommandLine),
  `Csv/` (CsvHelper), `Functions/` (Azure Functions), `WolverineHttp/` (Wolverine.Http, never `Wolverine/`),
  `LanguageModels/` (Microsoft.Extensions.AI, Model Context Protocol as `Mcp*`),
  `Logging/` (Serilog, Microsoft.Gen.Logging redaction), `SchemaGeneration/` (NJsonSchema, Confluent),
  `DataAnnotations/`, `TestData/` (the sampler and its three adapters), `XmlSerialization/`, `Identifiers/`.
- Each top-level folder belongs to the CI families `.github/ci-families.json` maps it to (`unit.folders`), which a pull
  request changing it runs, each family through `--filter-query` values on the namespaces of its folders (ADR 0013).
  A new folder goes there with a query in its family's `unit` list, `/*/(AdCodicem.ValueObjects.UnitTests.F)|(…F.*)`,
  or the plan fails. A file declares the namespace of its folder, the root namespace at the suite's root, which the
  plan checks. A `.cs` file at the root selects every family with a class there (`core`, `serialization`,
  `observability`, `testdata`); a helper several families' tests use goes into `unit.shared`, which selects every
  family, as any other file at the root and any `[assembly:]` or `[module:]` attribute or module initializer do. `Web/`
  and `Persistence/` hold two families each, told apart by class name (`NSwag*`, `Newtonsoft*`, `Swashbuckle*`;
  `Dapper*`, `*MongoDb*`, `Marten*`, `PcrePattern*`), the folder's first family taking the rest.
- `EmitCompilerGeneratedFiles` is on: generated sources land under `artifacts/obj/AdCodicem.ValueObjects.UnitTests/…/generated/`
  — read them when diagnosing emission.
- Fixtures the test assembly cannot hold, because the generator runs on it and its module initializer has run before any
  test, are in `tests/Fixtures/` (`tests/Fixtures/CLAUDE.md`).

## Which value object a test uses

- The registry is process-wide and `TryResolve` keeps what it resolves: never resolve a hand-written type other tests share
  (`HandWrittenCode`, `HandWrittenLink`, `UnregisteredCode`, …) through a boxed path (a `Type`-form attribute,
  `[NotDefault]`, the Functions converter, a HotChocolate scalar, an adapter), or it is registered for every later test.
- On a boxed path, use `Declared<TDeclaration, TValue>` (`TestData/SamplerFixtures.cs`) over a declaration the test owns,
  or close a generic one (`Reference<TOwner>` in `Domain/DeclarationContexts.cs`, `HandWrittenTag<T>`,
  `HandWrittenId<TProfile>`) over a marker type private to the test, so the registration it causes is its own.
- `UnregisteredCode` stays resolved by nothing: it is the "nothing registered" case here and in native AOT.
- An assertion on "every registered type" (the Serilog option, the FsCheck merge) relies only on `Domain/` types or on a
  fresh `AssemblyLoadContext` copy of `Fixtures.Untouched`: other modules' initializers may not have run yet.
- A value object that trips a generator warning cannot be silenced with `#pragma`: write it by hand, as
  `Persistence/MongoDbShapedCode.cs`'s `ShapedCode<TShape>` does for a pattern under `IgnoreCase`, `Multiline` or
  `IgnorePatternWhitespace`.
- A test-owned value object declaring `Unicode = false` holds its text to ASCII (an anchored pattern of ASCII classes, a
  closed set of ASCII known values compared ordinally, or a normalizer or validator hook), or the suite fails on `VO0047`.
- Every value object of `Domain/` gets a `ValueObjectContract` overriding `DerivesRejectedValues` to return `true` (the
  exceptions are in the root `CLAUDE.md`).
- A test constructing `default(T)` on purpose takes a targeted `#pragma warning disable VO0010` with a comment; one
  misusing a DataAnnotations attribute on purpose, `VO0039` or `VO0040`; a Marten compiled query holding a single value
  object on purpose, `VO0048`, around the member alone. The package's build props are not imported here,
  so `VO0042` never fires.

## Process-wide state

- MongoDB: every test class goes through the one static, strict registration of `Persistence/MongoDb.cs`
  (`ValueObjectBson.Register`, then `EntityIdBson.Register`). Another trust or representation means a serializer built by
  hand, a class map of the test's own or a fresh `BsonSerializerRegistry`, which the package's internal checks take as a
  parameter; a test of `Register` refusing goes through `MongoDbStartUpTests`' fresh copies of the driver, loaded into an
  `AssemblyLoadContext` of their own. A test of an id generator registered or kept closes `HandWrittenId<TProfile>` over
  a profile of its own. A query's `ToString()` reports a refused translation instead of throwing it: a test of a refused
  query constant renders a `Builders` filter.
- protobuf-net: `RuntimeTypeModel.Default` is registered once, strict (`BinarySerialization/ProtoDefaultModel.cs`); every
  other test builds a model of its own (`ProtoWire.Model`). Never put `[ProtoContract]` on a `Domain/` type, since
  protobuf-net honours it on every model: the attribute-route value objects are the tests' own (`ProtoSku`,
  `ProtoTicket`, `ProtoBareCode`, the bare `[ProtoContract]` trap, under `#pragma warning disable VO0043`). Test contracts
  carry no `[ProtoMember]` initializer (`PBN0022`); the default-model test suppresses `PBN3010`; the one Google.Protobuf
  message of `CodeFirstGrpcContracts.cs` suppresses `PBN2008`.
- MessagePack: a `[MessagePackObject]` holding a value object of this assembly needs
  `[assembly: MessagePackAssumedFormattable(...)]` (`BinarySerialization/MessagePackAssumptions.cs`).
- FluentValidation's language manager is global: such tests join the `FluentValidationGlobalOptions` collection and restore
  what they set.
- EF Core builds a model once per context type: each model shape gets a context type of its own.
- Orleans: serializers are built per test configuration (`Grains/OrleansWire`), none process-wide; `OrleansParity`
  checks each sample through `AddValueObjects()` strict and `AddValueObject<TSelf, TValue>(trusted: true)`, a serializer
  per type built once. Grain calls go through `GrainCluster`, one in-process cluster per test class (about 1.5 s the
  first time, 0.3 s after); a test needing another configuration starts its own with `GrainCluster.StartAsync` and
  disposes of it. A generic value object or one written by hand that a boxed path resolves is closed over a marker or a
  declaration of the test's own (`OrleansCodecOwner`, `OrleansCodeDeclaration`, …).
- Marten: `Persistence/MartenHarness` builds stores over a port nothing listens on (`Unreachable`, `AutoCreate.None`)
  and reads Marten's SQL (`ToCommand`), compiled-query plans and DDL without a connection; PostgreSQL runs in the
  integration suite. Weasel's `PostgresqlProvider.Instance` mappings, which `UseValueObjects()` fills, are process-wide,
  so a store of a test without the package fails at execution rather than at parsing for any value object another test
  taught: assert what Marten refuses without the package on a value object no store of the suite is taught, and close a
  generic or hand-written one the boxed path resolves over a declaration of the test's own (`MartenCodeDeclaration`,
  `MartenStaticDeclaration`, …). An inline array in a `Contains` fails inside Marten (`MemoryExtensions.Contains`):
  hold the values in a static field.
- Wolverine.Http: `WolverineHttp/WolverineHttpHost` maps the endpoint types a test names, in `TypeLoadMode.Auto` and
  without Wolverine's runtime compiler, which this project does not reference: chains, ApiExplorer, documents and the
  code Wolverine writes are there, a request is not. Every test building a host joins the `WolverineHttpHosts`
  collection, run alone, since `GenerateCodeAsync` turns JasperFx's static `DynamicCodeBuilder.WithinCodegenCommand` on
  while it runs; such a host serves no request afterwards. Requests go through the fixture application
  (`WolverineHttpEndToEndTests`, below). Wolverine scans this assembly: name an endpoint type apart
  (`Wolverine…Endpoints`) and list it in `Map`, and keep endpoint classes public and static.
- `RuntimeFeature.IsDynamicCodeSupported` is read once per process: `WithoutDynamicCode` (at the suite's root) runs a test
  again in a child process whose runtime configuration turns dynamic code off. Such a test creates no NSubstitute proxy
  and no EF Core context, neither of which works there.

## Cultures and messages

- Never rely on the ambient culture: CI runs in the invariant culture, a developer's machine may not. A test depending on
  one sets `CurrentCulture` and `CurrentUICulture`, and gives them back.
- Localised messages are asserted in fr-FR (a decimal comma, a day-first date, `;` between known values) from
  `Web/ValidationMessages.fr.resx`, the French example of the errors reference word for word, and
  `Web/SparseMessages.fr.resx`, French alone, so that another culture keeps English.
- A resource type's namespace is the root namespace followed by its folder, or every key reads as missing, with no error.

## Harnesses and suite settings

- Blazor: `Forms/FormsHarness` adds the services before `SetRendererInfo`: bUnit throws `MissingRendererInfoException`
  when a component reads `RendererInfo` first, and refuses a service added once the renderer is asked for.
  `Forms/EcmaScriptPatterns.json` is held to .NET here and to Node by `.github/scripts/ecmascript-patterns.mjs`: change
  both together.
- System.CommandLine: `Cli/` parses through `Command.Parse` and invokes through `InvocationConfiguration` with writers
  of its own (`CliHarness.Invoke`), never the console; `CliHarness.Refusals` reads each parse error with its code and
  type. A `RootCommand` is named after the test host, so a test asserting an error that names the command parses a
  `Command("app")`. A command with subcommands and no action answers "Required command was not provided.".
  System.CommandLine converts a parent command's arguments, and its options neither `Recursive` nor with a default
  value, only at `GetValue`; the walk's validator reads them when a subcommand is invoked, so a test of
  System.CommandLine's own behaviour there configures its symbols with `AcceptValueObject` alone and walks no command.
- CsvHelper: `Csv/CsvHarness` reads and writes through `StringReader` and `StringWriter`, a context per read or write,
  so nothing is process-wide; its configuration ends lines with `\n`. A single-column row whose field is empty text is a
  blank line, which CsvHelper skips: put the value after a `Line` column (`CsvLine<T>`) or quote it. A refused text is
  written with `CsvHarness.Field`, so that CsvHelper quotes it. A `Csv/` file aliases `CsvHelper.MissingFieldException`,
  which meets `System.MissingFieldException`, and never imports `System.ComponentModel` beside
  `CsvHelper.Configuration.Attributes` (two `TypeConverterAttribute`s). A generated value object's `IsDefault` is an
  explicit implementation: read it through `CsvHarness.IsUninitialized()`.
- HotChocolate: `GraphQL/GraphQLHarness` builds executors in process. The project sets `HotChocolateImplicitUsings` to
  `disable`. `ScopedFilterQuery` has no projection (a `[UseProjection]` field beside a named convention fails the schema).
- `RegisteredAsciiDeclaration` is the one schema written by hand that a test resolves through the registry.
- Orleans: Microsoft.Orleans.TestingHost brings Orleans' code generator, its analyzers and global usings of `Orleans`,
  `Orleans.Hosting` and `Orleans.Runtime` into this project (through Microsoft.Orleans.Sdk): a type of the suite named
  like a public type of those namespaces is ambiguous (CS0104), so name it apart. Its generated `ApplicationPart` list
  is how a test cluster finds the fixture's grains. Orleans' `Deserialize<T>` returns `[MaybeNull]`: read a holder
  through `OrleansWire.Read<T>`. Bytes no serializer writes are crafted field by field through `OrleansWire.Craft`; a
  writer applies its own type filters, so bytes naming a type the reader must refuse go through `Craft(services, …)`
  over services allowing that type.
- Roslyn: this project takes `Microsoft.CodeAnalysis.CSharp` 5.0.0 (`Directory.Packages.props`), which NSwag's client
  and the code Wolverine writes compile with, since Orleans brings `Microsoft.CodeAnalysis.Common` 5.0.0; the generator
  and GeneratorTests stay on 4.14. `CSharp.Workspaces` stays on 4.14.0 here: the Wolverine.Http fixture takes 5.0.0
  through a pin of its own, and its reference (`ReferenceOutputAssembly="false"`) brings none of its packages here.
- Wolverine.Http end to end: `WolverineHttpEndToEndTests` starts
  `tests/Fixtures/AdCodicem.ValueObjects.Fixtures.WolverineHttp` with the `dotnet` host three times at once (lenient,
  strict, French), from the folder the `WolverineHttpApplication` assembly metadata names, and reads the JSON line each
  request is answered with, keyed by its label; a label the fixture's script changes must change here too. About 7 s,
  Roslyn compiling the endpoints in each child. The children run with `CORECLR_ENABLE_PROFILING=0`: the coverage
  collector follows a child through that profiler, and merging the fixture's report lost hits of the packages both
  processes load (`ValueObjectRefusals.Body.cs` read as missed).
- Marten's compiled-query diagnostic: `Persistence/MartenCompiledQueryDiagnosticTests` runs `VO0048`'s analyzer, loaded
  from the generator's assembly that the `CopyValueObjectAnalyzers` target copies to `analyzers/`, over a snippet
  compiled against the trusted platform assemblies; this project's Roslyn 5 runs the analyzer built against 4.14, as
  the SDK's compiler does.
- `NSwag.OpenApiDocument` meets the suite's `Web.OpenApiDocument`: alias it (`NSwagOpenApiDocument`, as
  `NSwagDocumentTests` does).
- MCP: `LanguageModels/McpHarness` runs a server and the SDK's client over a pair of pipes. Experimental-API diagnostics
  (`MEAI001`, `MCPEXP002`) are suppressed around the one statement needing them.
- Azure Functions: `Functions/FunctionsDoubles.cs` writes `FunctionContext`, `FunctionDefinition` and `BindingContext`
  (public abstract classes); the worker's internal `IFunctionBindingsFeature` is a substitute.
- Swashbuckle and NSwag documents are awaited with a timeout (`SwashbuckleDocuments.BuildAsync` in
  `Web/SwashbuckleDocument.cs`), so a schema-filter hang fails the test, not the job's time limit.
- `Persistence/EntityFrameworkCoreCheckErrorTests.cs` stands for the path only `SaveChangesFailed` sees by throwing
  SQLite's error from `DataReaderClosing`; the integration suite runs it for real.
- Confluent's serdes run over an NSubstitute `ISchemaRegistryClient`, no registry server; `librdkafka.redist` is referenced
  with `ExcludeAssets="native;build"` (about 125 MB less output: the serdes never load it).
- Logging: the suite references Microsoft.Extensions.Telemetry and the Redaction fixture, so its project sets
  `DisableMicrosoftExtensionsLoggingSourceGenerator` back to `false` and removes the package's analyzers after
  `ResolveReferences` (`KeepTheDefaultLoggingGenerator`); without both, Microsoft.Gen.Logging would pass `PageNumber` as
  text and `SerilogMicrosoftExtensionsLoggingTests` would fail.
- Shared-source copies: the contracts' `ValueObjectPatternSyntax` is named through `extern alias abstractions`,
  Testing.Data's types through `testingdata`, the CsvHelper package's `GregorianCultures` through `csvhelper`; every
  other copy through its package's API (`src/Shared/CLAUDE.md`).
- `DescriptorTests.cs` covers the boxed path the rest of the suite never reaches; `RuntimeClosingTests.cs` holds the
  allow-list of runtime-closing calls.
- Property tests (`PropertyTests.cs`) run the laws `IValueObject<TSelf, TValue>` states in prose. A property conditioned on
  "the value was accepted" passes vacuously unless the generator produces accepted values, so each counts how often it
  reached that branch and asserts on it; a range generator biases towards the sampler's `Boundaries` and
  `RejectedValues`, since a wide type never lands on its edge by chance. After changing a generator, re-run the mutations
  its commit message records before trusting the green.
