# tests/Fixtures — assemblies the test projects cannot hold

The unit test assembly cannot hold these, since the generator runs on it and its module initializer has run before any
test does. They are neither test projects nor packages. The unit suite references eight; `Fixtures.Functions` is the
integration suite's.

- `Untouched`: a generated value object in a module nothing has used yet. A test needing "registered by nothing but this"
  loads a fresh `AssemblyLoadContext` copy of it.
- `WithoutGenerator`: annotated hand-written value objects where no generator runs.
- `WithoutJson`: generated value objects in an assembly that does not reference `AdCodicem.ValueObjects.Json`, as a domain
  project serializing through an API's context does not.
- `XmlSerialization`: generated value objects of every underlying type in an assembly marked
  `[assembly: ValueObjectXmlSerialization]`, beside hand-written ones on `ValueObjectXml` whose schemas the generator never
  writes.
- `GraphQL`: a `[QueryType]` HotChocolate's source generator binds, so that the unit test assembly gets no generated module.
- `Blazor`: Razor forms, pages and value objects of their own, built by the Razor SDK while the suite stays on
  `Microsoft.NET.Sdk`. Its generated `Router` code warns under the trimming analyzer, which this fixture turns off.
- `Redaction`: `[LoggerMessage]` methods Microsoft.Gen.Logging writes, beside value objects classified as personal data,
  which the suite's own `[LoggerMessage]` generator would otherwise write. Its build holds its correct methods to `VO0045`
  and `VO0046` under `TreatWarningsAsErrors`, so a bump fixing one of the logging generator's defects fails here first,
  and says `VO0045` or `VO0046` can step back. Its classification is named `RedactionTaxonomy` and `ContactDataAttribute`,
  since the suite's global `Domain` using holds a `Taxonomy` and a `PersonalDataAttribute`.
- `Grains`: grains, grain interfaces, `[GenerateSerializer]` holders and value objects of its own, which Orleans' code
  generator (Microsoft.Orleans.Sdk) writes proxies, invokers and codecs for. It does not reference the Orleans package,
  as an application's grain interfaces do not. The unit suite gets Orleans' generator anyway through TestingHost, and
  the `ApplicationPart` list it writes there is how a test cluster finds these grains. A grain calling itself deadlocks
  (a grain is not reentrant): a relay goes to another key.
- `Functions`: the Azure Functions application the integration suite runs in the host's container. Keep
  `ConfigureFunctionsWorkerDefaults(...)` out of its `Program.cs`: the integration's analyzer
  `RegistrationExpectedInASPNetIntegration` (Http.AspNetCore.Analyzers 1.0.4) throws AD0001 on a file declaring `Main` that
  calls it, which fails the build under `TreatWarningsAsErrors`.
