# tests/Fixtures — assemblies the test projects cannot hold

A unit test assembly cannot hold these, since the generator runs on it and its module initializer has run before any
test does. They are neither test projects nor packages. Each project of the unit suite references those its tests need,
named in each entry; the Hosts project builds `WolverineHttp` without referencing its assembly, and the integration
suite builds `Fixtures.Functions` the same way.

- `Untouched`, referenced by the core, Serialization, Observability and TestData projects: a generated value object in
  a module nothing has used yet. A test needing "registered by nothing but this" loads a fresh `AssemblyLoadContext`
  copy of it.
- `WithoutGenerator`, referenced by the core and AspNet projects: annotated hand-written value objects where no
  generator runs.
- `WithoutJson`, referenced by the core and Observability projects and by `Redaction`: generated value objects in an
  assembly that does not reference `AdCodicem.ValueObjects.Json`, as a domain project serializing through an API's
  context does not.
- `XmlSerialization`, referenced by the core and Data projects: generated value objects of every underlying type in an
  assembly marked `[assembly: ValueObjectXmlSerialization]`, beside hand-written ones on `ValueObjectXml` whose schemas
  the generator never writes.
- `GraphQL`, referenced by the GraphQL project: a `[QueryType]` HotChocolate's source generator binds, so that the
  project's own assembly gets no generated module.
- `Blazor`, referenced by the Text project: Razor forms, pages and value objects of their own, built by the Razor SDK
  while the Text project stays on `Microsoft.NET.Sdk`. Its generated `Router` code warns under the trimming analyzer,
  which this fixture turns off.
- `Redaction`, referenced by the Observability project: `[LoggerMessage]` methods Microsoft.Gen.Logging writes, beside
  value objects classified as personal data, which that project's own `[LoggerMessage]` generator would otherwise
  write. Its build holds its correct methods to `VO0045` and `VO0046` under `TreatWarningsAsErrors`, so a bump fixing
  one of the logging generator's defects fails here first, and says `VO0045` or `VO0046` can step back. Its
  classification is named `RedactionTaxonomy` and `ContactDataAttribute`, since the global `Domain` using of
  `tests/UnitTests.props` holds a `Taxonomy` and a `PersonalDataAttribute`.
- `Grains`, referenced by the Orleans project: grains, grain interfaces, `[GenerateSerializer]` holders and value
  objects of its own, which Orleans' code generator (Microsoft.Orleans.Sdk) writes proxies, invokers and codecs for. It
  does not reference the Orleans package, as an application's grain interfaces do not. The Orleans project gets
  Orleans' generator anyway through TestingHost, and the `ApplicationPart` list it writes there is how a test cluster
  finds these grains. A grain calling itself deadlocks (a grain is not reentrant): a relay goes to another key.
- `WolverineHttp`, built by the Hosts project through a `ReferenceOutputAssembly="false"` reference and started as a
  child process: a Wolverine.Http application in Wolverine's dynamic mode, an application, not a library. Its runtime
  compiler (WolverineFx.RuntimeCompilation) asks for `Microsoft.CodeAnalysis.CSharp` and its Workspaces at 5.0.0, which
  conditional `PackageVersion`s keyed on its project name give it alone (`Directory.Packages.props`); the Hosts project
  stays on 4.14.0, since a reference that does not reference the assembly brings none of its packages. It answers a
  fixed script of requests (`Program.cs`, `Script`) over TestHost, one JSON line each, the codes
  `CustomizeProblemDetails` reads and the run of a `Before`, `After` or `Finally` method noted beside the answer; a
  label of the script is a key of the Hosts project's `WolverineHttp/WolverineHttpEndToEndTests`. It links
  `Domain/HandWritten/HandWrittenCode.cs` of `tests/AdCodicem.ValueObjects.UnitTests`, in its own process. Its namespace
  hides the root `Wolverine` one: usings at the top of each file.
- `Functions`, built by the integration suite without referencing its assembly: the Azure Functions application it runs
  in the host's container. Keep `ConfigureFunctionsWorkerDefaults(...)` out of its `Program.cs`: the integration's
  analyzer `RegistrationExpectedInASPNetIntegration` (Http.AspNetCore.Analyzers 1.0.4) throws AD0001 on a file
  declaring `Main` that calls it, which fails the build under `TreatWarningsAsErrors`.
