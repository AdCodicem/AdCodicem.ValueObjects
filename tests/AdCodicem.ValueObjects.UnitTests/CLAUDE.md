# AdCodicem.ValueObjects.UnitTests

The unit suite is ten projects, one per CI family of `.github/ci-families.json`, which names each in
`families.<f>.tests` (ADR 0013). This one is the core family's: the behaviour of generated code over the value objects
of `Domain/`, the JSON package, the identifiers, the test-data sampler, DataAnnotations and XML serialization. It is
also the home of the sources every project shares, linked from here. Each other project has a `CLAUDE.md` of its own
with its family's traps, and a package's traps are in its own `src/<Package>/CLAUDE.md`; this file holds the suite's
conventions.

## Layout

- The projects, each `tests/<name>/<name>.csproj`, and the folders each holds, named apart from the packages and
  libraries they test (root `CLAUDE.md`):
  - core, `AdCodicem.ValueObjects.UnitTests`: tests at its root (`DescriptorTests`, `RuntimeClosingTests`, …),
    `DataAnnotations/`, `Identifiers/`, `XmlSerialization/`, `TestData/` (the sampler's own tests, Testing.Data being a
    core project), `Domain/` and `GeneratedSurface/`; `Persistence/` and `Web/` hold only helpers, which others link.
  - aspnet, `.AspNet`: `Web/` (MVC, minimal APIs, OpenAPI, problem details, DataAnnotations through hosts).
  - efcore, `.EfCore`: `Persistence/` (Entity Framework Core and its identifiers, SQL patterns).
  - data, `.Data`: `Persistence/` (Dapper, MongoDB, PCRE patterns as `PcrePattern*`).
  - serialization, `.Serialization`: `BinarySerialization/` (MessagePack; protobuf-net as `Proto*`; gRPC as
    `CodeFirstGrpc*` and `ContractFirstGrpc*`), `SchemaGeneration/` (NJsonSchema, Confluent), `Web/` (Newtonsoft.Json
    formatters and JSON Patch, Swashbuckle, NSwag) and `NewtonsoftJsonTests.cs` at its root.
  - hosts, `.Hosts`: `Functions/` (Azure Functions).
  - graphql, `.GraphQL`: `GraphQL/` (HotChocolate).
  - text, `.Text`: `Forms/` (Blazor).
  - observability, `.Observability`: `LanguageModels/` (Microsoft.Extensions.AI, Agent Framework, Model Context
    Protocol as `Mcp*`), `Logging/` (Serilog, Microsoft.Gen.Logging's redaction), `FluentValidation*.cs` at its root.
  - testdata, `.TestData`: the contract kit's contracts and `PropertyTests.cs` at its root, `TestData/` (AutoFixture,
    Bogus, FsCheck).
- Every project's `RootNamespace` is `AdCodicem.ValueObjects.UnitTests`, and a file declares the namespace of its
  folder, the root namespace at a project's root: a test's name does not depend on the project holding it, and a
  folder in several projects (`Web/`, `Persistence/`) is one namespace.
- Every project imports `tests/UnitTests.props`: the root namespace, the generated sources on disk, references to
  Abstractions (alias `abstractions`), Identifiers, Json, Testing.Data (alias `testingdata`), the generator as an
  analyzer and Microsoft.Extensions.Compliance.Abstractions, global usings of `AdCodicem.ValueObjects`,
  `AdCodicem.ValueObjects.Annotations` and `AdCodicem.ValueObjects.UnitTests.Domain`, and `.github/ci-families.json`
  embedded as `ci-families.json`.
- Into every project but this one, `tests/UnitTests.props` links from this folder, at the same paths, `Domain/**`,
  `GeneratedSurface/Sample.cs`, `GeneratedSurface/Samples.cs`, `GeneratedSurface/NumericSample.cs`,
  `TestData/Declared.cs`, `RuntimeClosingTests.cs` and `DescriptorTests.Registered.cs`: every value object lives in the
  assembly whose tests use it, and its module initializer has run before any of them.
- `Domain/`: the suite's value objects. `Domain/UnderlyingTypes.cs` declares one per underlying type and per option or
  hook the rest leaves out; the integration suite and `tests/NativeAot` link it too, so a change there changes them.
  `Domain/HandWritten/`: value objects written by hand, the only way to reach the interface defaults and the registry's
  reflection fallback (`HandWrittenCode.cs`, `HandWrittenLink.cs` and `UnregisteredCode.cs` are linked by
  `tests/NativeAot` too, and `HandWrittenCode.cs` by the Functions fixture of `tests/Fixtures`).
- A helper several projects use lives in one project's folder and each other project needing it links it in its
  csproj, at the same path, so that a resource keeps its name. It lives here when core's tests use it or no one family
  owns it (`CodeCarrying.cs`, `WithoutDynamicCode.cs`, `DataAnnotations/Annotated.cs`,
  `Identifiers/DeterministicEntropy.cs`, `Persistence/NonUnicodeValueObjects.cs`, `Web/ValidationMessages.*`,
  `Web/SparseMessages.*`, `TestData/SamplerFixtures.cs`, `TestData/UnboundedDecimal.cs`), otherwise in its family's
  (the AspNet project's `Web/ProblemReading.cs`, `Web/AnnotatedModels.cs`, `Web/OpenApiDocument.cs` and
  `Web/ProbeController.cs`; the Data project's `Persistence/MongoDbShapedCode.cs`). Grep `Link=` before editing one.
- A file of tests is never linked, or its tests would run in two projects, but `RuntimeClosingTests.cs` and
  `DescriptorTests.Registered.cs`, which each project runs over its own family (its packages' IL, its registry's value
  objects). The plan fails a `Compile` item (in `tests/UnitTests.props` or a project's csproj, whatever its `Link`)
  that matches no file, or that is taken from another project's folder and holds `[Fact]`, `[Theory]` or a
  `ValueObjectContract<…>` class.
- A change to a project's files runs its family; one to a file several projects link or import (`tests/UnitTests.props`,
  `Domain/`, a linked helper) runs each of their families, dotnet-affected following linked items and imports. Core's
  family also runs the generator suite in full, aspnet's the RDG suite, efcore's the compiled model
  (`.github/scripts/ci-plan.sh`'s header).
- `EmitCompilerGeneratedFiles` is on: generated sources land under `artifacts/obj/<project>/generated/` — read them when
  diagnosing emission.
- Fixtures a test assembly cannot hold, because the generator runs on it and its module initializer has run before any
  test, are in `tests/Fixtures/` (`tests/Fixtures/CLAUDE.md`); each project references those it needs, this one
  `Untouched`, `WithoutGenerator`, `WithoutJson` and `XmlSerialization`.

## The generated surface

- `GeneratedSurface/` runs every emitted member family on every sample, so nothing the generator emits only compiles,
  and holds every sample to each integration. `Sample.cs` is the base the other projects link, `Samples.cs` the samples,
  `Sample.Core.cs` core's checks (the emitted members, DataAnnotations, `[NotDefault]`, the sampler's
  `DrawsValuesItsRulesAccept`). Each other family but testdata declares its abstract checks and their overrides
  together in its own project's `GeneratedSurface/Sample.<Family>.cs`, which only its tests call.
- To add an integration, in its family's project: declare an abstract check on `Sample` and its override in
  `Sample<TSelf, TValue>`, in `GeneratedSurface/Sample.<Family>.cs`, calling an `…Parity` helper kept in the
  integration's own folder (the Text project's `Forms/FormsParity.cs`, the Serialization project's
  `BinarySerialization/ProtoParity.cs`, …), and add a theory over `Samples.Names`
  (`[MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]`) to a test class of that folder (as the Data
  project's `Persistence/MongoDbSerializerTests` does). Core's theories are in `GeneratedSurface/GeneratedSurfaceTests.cs`;
  the Text project's, in a `GeneratedSurfaceTests` part of its own (`GeneratedSurface/GeneratedSurfaceTests.Text.cs`).

## Adding a folder or a project

- A new folder goes into its family's project; nothing maps it, since a change to the project's files selects its
  family.
- A new family's project is `tests/<name>/<name>.csproj`, `<name>` being `AdCodicem.ValueObjects.UnitTests.<Name>`; it
  imports `..\UnitTests.props` and references its family's packages. Then, or the plan fails:
  - name it in the family's `tests` in `.github/ci-families.json`: every `tests/AdCodicem.ValueObjects.UnitTests*/`
    project must be one family's, and each family's `tests` such a project;
  - add it to `AdCodicem.ValueObjects.slnx`, where dotnet-affected looks for the projects a change marks;
  - replace `codecov.yml`'s flags between its `ci-families` markers with what
    `.github/scripts/ci-plan.sh --codecov-flags` prints.
- Give a new project an `InternalsVisibleTo` entry in `src/Directory.Build.props`, beside the ten: the plan does not
  check it, and it is a package input (`preview-gate.sh`), so the next preview publishes.
- A family's report must cover a line of each of its packages with code of its own but the generator and the code fix
  (`.github/scripts/coverage-modules.sh`): `RuntimeClosingTests` loads each, which reports it but covers nothing.

## Commands

```bash
# One family's project; core's is tests/AdCodicem.ValueObjects.UnitTests.
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests.Text
# Every family, as previews and releases run them: each one runs, and xargs fails when any failed.
jq -r '.families[].tests' .github/ci-families.json | xargs -I{} dotnet test --project tests/{} -c Release
# One test, on the project holding it: wildcards, not substrings.
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests.Data --filter-method "*The_name_of_the_test*"
```

`dotnet test -c Release` runs every test project of the solution, the integration suite included (Docker).

## Which value object a test uses

- The registry keeps what `TryResolve` resolves for the life of the process: never resolve a hand-written type other
  tests share (`HandWrittenCode`, `HandWrittenLink`, `UnregisteredCode`, …) through a boxed path (a `Type`-form
  attribute, `[NotDefault]`, the Functions converter, a HotChocolate scalar, an adapter), or it is registered for every
  later test of the project.
- On a boxed path, use `Declared<TDeclaration, TValue>` (`TestData/Declared.cs`, which every other project links) over a
  declaration the test owns, or close a generic one (`Reference<TOwner>` in `Domain/DeclarationContexts.cs`,
  `HandWrittenTag<T>`, `HandWrittenId<TProfile>`) over a marker type private to the test, so the registration it causes
  is its own.
- `UnregisteredCode` stays resolved by nothing: it is the "nothing registered" case here and in native AOT.
- An assertion on "every registered type" (the Serilog option, the FsCheck merge) relies only on `Domain/` types or on a
  fresh `AssemblyLoadContext` copy of `Fixtures.Untouched`: other modules' initializers may not have run yet.
- A value object that trips a generator warning cannot be silenced with `#pragma`: write it by hand, as the Data
  project's `Persistence/MongoDbShapedCode.cs` does with `ShapedCode<TShape>` for a pattern under `IgnoreCase`,
  `Multiline` or `IgnorePatternWhitespace`.
- A test-owned value object declaring `Unicode = false` holds its text to ASCII (an anchored pattern of ASCII classes, a
  closed set of ASCII known values compared ordinally, or a normalizer or validator hook), or the build fails on
  `VO0047`.
- Every value object of `Domain/` gets a `ValueObjectContract` overriding `DerivesRejectedValues` to return `true`, in
  the contract files at the root of `tests/AdCodicem.ValueObjects.UnitTests.TestData` (the exceptions are in the root
  `CLAUDE.md`).
- A test constructing `default(T)` on purpose takes a targeted `#pragma warning disable VO0010` with a comment; one
  misusing a DataAnnotations attribute on purpose, `VO0039` or `VO0040`. No project imports the `AdCodicem.ValueObjects`
  package's build props (the generator is referenced as an analyzer), so `VO0042` never fires.

## Process-wide state

- Each project runs in a test process of its own, so what is process-wide (the registry, MongoDB.Driver's serializers
  and id generators, protobuf-net's default model, FluentValidation's language manager, `EF.IsDesignTime`) is shared by one family's tests only. The registry's rules are under
  [Which value object a test uses](#which-value-object-a-test-uses) above; the rules for each of the others are in the
  `CLAUDE.md` of the project whose tests touch it.
- `RuntimeFeature.IsDynamicCodeSupported` is read once per process: `WithoutDynamicCode` (`WithoutDynamicCode.cs`,
  linked by the Data, Hosts, GraphQL and Text projects) runs a test again in a child process whose runtime
  configuration turns dynamic code off. Such a test creates no NSubstitute proxy and no EF Core context, neither of
  which works there.

## Cultures and messages

- Never rely on the ambient culture: CI runs in the invariant culture, a developer's machine may not. A test depending
  on one sets `CurrentCulture` and `CurrentUICulture`, and gives them back.
- Localised messages are asserted in fr-FR (a decimal comma, a day-first date, `;` between known values) from
  `Web/ValidationMessages.fr.resx`, the French example of the errors reference word for word, and
  `Web/SparseMessages.fr.resx`, French alone, so that another culture keeps English. Both live here with their types,
  and the AspNet, Hosts, GraphQL and Text projects link both, the Serialization project `ValidationMessages` alone, at
  the same paths.
- A resource type's namespace is the root namespace followed by its folder, or every key reads as missing, with no
  error: hence a linked resource keeps its path.

## Settings and core's own traps

- Roslyn: every project compiling C# at run time takes `Microsoft.CodeAnalysis.CSharp` 4.14.0, the API the generator is
  built against (`Directory.Packages.props`): the Serialization project, for NSwag's client.
- This project takes the ASP.NET Core shared framework (`BadHttpRequestException` for `ErrorCodeCarrierTests`, the
  generic host `DataAnnotationsHostTests` and `AnyEntityIdTests` bind options through), FsCheck for
  `ErrorCodeCarrierTests`' properties, LateApexEarlySpeed.Json.Schema, which checks written JSON against the exported
  schema, and CommunityToolkit.Mvvm, whose `ObservableValidator` runs `[ParsesAs]` and `[NotDefault]` in
  `DataAnnotations/`.
- Shared-source copies: the contracts' `ValueObjectPatternSyntax` is named through `extern alias abstractions`,
  Testing.Data's types through `testingdata` (both set by `tests/UnitTests.props`); every other copy through its
  package's API
  (`src/Shared/CLAUDE.md`).
- `DescriptorTests.cs` covers the boxed path the rest of the suite never reaches, and stays here but for its sweep of
  `ValueObjectRegistry.GetRegistered()`, `Every_rejection_carries_an_error_code`, in `DescriptorTests.Registered.cs`,
  which every project compiles: a project's registry holds only the value objects its own module initializer
  registers, so each project sweeps its own.
- `RuntimeClosingTests.cs`, compiled by every project, reads the IL of its own family's packages: its rows are the
  `projects` of the family whose `tests` is the assembly's name in the embedded table, but `NoRuntimeCode`
  (`AdCodicem.ValueObjects`, the generator, the code fix). `Allowed` is the allow-list (Abstractions,
  AspNetCore.NewtonsoftJson, EntityFrameworkCore), and a key naming no project of the table throws. A package added to
  a family is read with no row to write, so the family's project must reference it (`Assembly.Load`).
- `RegisteredAsciiDeclaration` (`Persistence/NonUnicodeValueObjects.cs`, linked by the EfCore and Data projects) is the
  one schema written by hand that a test resolves through the registry.
