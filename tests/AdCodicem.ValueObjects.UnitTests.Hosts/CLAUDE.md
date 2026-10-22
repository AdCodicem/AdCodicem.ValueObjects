# AdCodicem.ValueObjects.UnitTests.Hosts

The hosts family's unit tests (AzureFunctions, AzureFunctions.AspNetCore, Wolverine.Http), in `Functions/` (the Azure
Functions worker and its ASP.NET Core integration) and `WolverineHttp/` (Wolverine.Http, never `Wolverine/`). The
suite's conventions are in `tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`, each package's traps in its
`src/<Package>/CLAUDE.md`.

- Azure Functions: `Functions/FunctionsDoubles.cs` writes `FunctionContext`, `FunctionDefinition` and `BindingContext`
  (public abstract classes); the worker's internal `IFunctionBindingsFeature` is a substitute. The Functions host runs
  in the integration suite.
- Wolverine.Http: `WolverineHttp/WolverineHttpHost` maps the endpoint types a test names, in `TypeLoadMode.Auto` and
  without Wolverine's runtime compiler, which this project does not reference: chains, ApiExplorer, documents and the
  code Wolverine writes are there, a request is not. Every test building a host joins the `WolverineHttpHosts`
  collection, run alone, since `GenerateCodeAsync` turns JasperFx's static `DynamicCodeBuilder.WithinCodegenCommand` on
  while it runs; such a host serves no request afterwards.
- Wolverine scans this assembly: name an endpoint type apart (`Wolverine…Endpoints`) and list it in `Map`, and keep
  endpoint classes public and static.
- `WolverineHttp/WolverineHttpCodeTests` compiles the code Wolverine writes with `Microsoft.CodeAnalysis.CSharp`
  4.14.0, the suite's Roslyn.
- Requests go through the fixture application: `WolverineHttpEndToEndTests` starts
  `tests/Fixtures/AdCodicem.ValueObjects.Fixtures.WolverineHttp` with the `dotnet` host three times at once (lenient,
  strict, French), from the folder the `WolverineHttpApplication` assembly metadata names, and reads the JSON line each
  request is answered with, keyed by its label; a label the fixture's script changes must change here too. About 7 s,
  Roslyn compiling the endpoints in each child. The children run with `CORECLR_ENABLE_PROFILING=0`: the coverage
  collector follows a child through that profiler, and merging the fixture's report lost hits of the packages both
  processes load (`ValueObjectRefusals.Body.cs` read as missed).
- The fixture is built with this project and never loaded by it (`ReferenceOutputAssembly="false"`): it takes Roslyn
  5.0.0 and its Workspaces through pins of its own (`Directory.Packages.props`), none of which reaches this project, and
  a change to it runs this family.
- `WolverineHttp/WolverineHttpDocumentTests` reads its documents with a timeout (`DocumentTimeout`), through the OpenApi
  and Swashbuckle packages, which this project references too: a change to either runs this family as well.
- This project adds `Microsoft.AspNetCore.OpenApi.Generated` to `InterceptorsNamespaces`, as the AspNet project does
  and for the same reason.
- It links the AspNet project's `Web/ProblemReading.cs`, and core's `CodeCarrying.cs`, `Web/ValidationMessages.*`,
  `Web/SparseMessages.*` and `WithoutDynamicCode.cs` (whose `Host()` names the `dotnet` host the fixture's children
  start with), at the same paths.
