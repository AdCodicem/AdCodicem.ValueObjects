# tests/Fixtures — assemblies the test projects cannot hold

A unit test assembly cannot hold these, since the generator runs on it and its module initializer has run before any
test does. They are neither test projects nor packages. Each project of the unit suite references those its tests need,
named in each entry; the integration suite builds `Fixtures.Functions` without referencing its assembly.

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
- `Functions`, built by the integration suite without referencing its assembly: the Azure Functions application it runs
  in the host's container. Keep `ConfigureFunctionsWorkerDefaults(...)` out of its `Program.cs`: the integration's
  analyzer `RegistrationExpectedInASPNetIntegration` (Http.AspNetCore.Analyzers 1.0.4) throws AD0001 on a file
  declaring `Main` that calls it, which fails the build under `TreatWarningsAsErrors`.
