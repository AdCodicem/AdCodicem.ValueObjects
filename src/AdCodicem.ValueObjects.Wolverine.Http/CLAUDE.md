# AdCodicem.ValueObjects.Wolverine.Http

User-facing behaviour: `website/docs/how-to/wolverine.md`. Tests: the unit suite's `WolverineHttp/` (in process, without
Roslyn: the helpers over `DefaultHttpContext`, the strategy over real chains, the code Wolverine writes, compiled, and
the OpenAPI documents) and `WolverineHttpEndToEndTests`, which starts
`tests/Fixtures/AdCodicem.ValueObjects.Fixtures.WolverineHttp` as a child process; natively,
`tests/NativeAot/AdCodicem.ValueObjects.NativeAot` in static mode.

## Wolverine's code generation

- `AddParameterHandlingStrategy` inserts the strategy at index 0 of the graph's strategies
  (`HttpGraph.InsertParameterStrategy`), and fails anywhere but inside `MapWolverineEndpoints`'s callback, where the
  graph exists: `UseValueObjects()` can be called nowhere else, which is why its tests go through `WolverineHttpHost`.
- The strategy picks the source in the order of Wolverine's own strategies (`[FromForm]`; `[FromRoute(Name)]`, compared
  ignoring case, declined when the template lacks it so that Wolverine throws its own error; a route parameter of the
  parameter's exact name, before `[FromHeader]` and `[FromQuery]`; `[FromHeader]`; the query string), and declines what
  another strategy binds (`[AsParameters]`, `[FromBody]`, `[FromServices]`, `[FromKeyedServices]`, `[NotBody]`, any
  `WolverineParameterAttribute`): the package changes how a value is read, never where.
- Wolverine matches parameters in `HttpChain`'s constructor, the handler's then each middleware method's, before any
  `IHttpPolicy` runs: the policy finds the reads the strategy recorded (`ValueObjectRefusalPolicy.ReadsOf`, a
  `ConditionalWeakTable` keyed by chain) and inserts the refusal frame at `Middleware[0]`; its `FindVariables` returns
  every read, so JasperFx writes them all before it and a `Before`, `Load` or `Validate` method never runs for a refused
  request.
- A policy registered after `UseValueObjects()` may add a middleware method and match its parameters
  (`chain.ApplyParameterMatching`), which asks the strategy again: the refusal frame holds the policy's list of reads,
  never a copy, since JasperFx asks for its variables only when it writes the code, and the strategy calls
  `ValueObjectRefusalPolicy.Answer`, which inserts the frame for an endpoint whose first read comes after the policy
  ran. A later policy inserting its own frame at `Middleware[0]` still runs it before the answer.
- One read per source and key in a chain: Wolverine matches a compound handler's methods too, and a second read of `qty`
  would declare the local twice (CS0128). The same key bound to two value objects throws `InvalidOperationException` at
  `MapWolverineEndpoints`.
- Each read takes over the variable Wolverine's own factory creates for its source and name (`FindRouteVariable`,
  `TryFindOrCreateQuerystringValue`, `GetOrCreateHeaderVariable`, `TryFindOrCreateFormValue`), made the read frame's
  through `HttpElementVariable.ReassignCreator`, internal and called through `[UnsafeAccessor]`, as Wolverine's
  `AsParametersBindingFrame` re-homes what it absorbs (GH-3374). Wolverine describes a query, header or form parameter
  in ApiExplorer only from those variables (`HttpChain.CreateApiDescription`, GH-4314), and looks a value up again by
  source and name in the private lists they fill, for an `After` or `Finally` method
  (`tryApplyHttpBindingsFromSignature`), a middleware method's route value (`TryApplyRouteVariables`) and a parameter
  attribute's identity (`TryFindVariable`, as `[Entity]` calls it): a variable of the package's own there was a second
  local of the name (CS0128). Wolverine's frame is then written nowhere. A route variable keeps Wolverine's name, which
  `FindRouteVariable(ParameterInfo)` compares to a parameter's; a query, header or form one is renamed after the
  parameter, since Wolverine finds those by key and names some after it, unsanitized. A renamed `ReassignCreator` throws
  `MissingMethodException` when the chain is built; `WolverineHttpStrategyTests` and `WolverineHttpCodeTests` pin the
  lookups. The query factory logs Wolverine's lenient-binding warning under `RejectUnparseableQueryValues = false`, and
  `IsRequired` stays false; `WolverineHttpDocumentTests` pins the description.
- `HttpChain.IsFormData` and `RequestType` have internal setters, which `applyMetadata()` reads for
  `AutoAntiforgeryOnFormEndpoints()`: for a form value the strategy sets them through `[UnsafeAccessor]`, as Wolverine's
  own `[FromForm]` strategy does, or antiforgery would silently stop guarding the endpoint, and
  `JsonBodyParameterStrategy` (`RequestType == null`) would claim a class parameter after it as the body, a form request
  then answered 415. A renamed setter throws `MissingMethodException` when the chain is built;
  `WolverineHttpStrategyTests` pins the marking, the metadata and the class parameter.
- `[AsParameters]` members, a complex `[FromQuery]`/`[FromForm]` type's members, an `After`/`Finally` parameter reading
  a value no value object of the endpoint reads, and the JSON body are bound by Wolverine without asking a strategy
  (`AsParametersBindingFrame`, `tryApplyHttpBindingsFromSignature`, `HttpHandler.ReadJsonAsync`): out of the package's
  reach, documented, open points upstream. A complex type's property bound to the very key a value object reads stays
  default, since `QueryStringBindingFrame` and `FormBindingFrame` assign a property only from a variable an
  `IReadHttpFrame` creates (Wolverine alone overflows the stack on that endpoint, and on an `[AsParameters]` property
  sharing a route value).

## The generated code

- The code calls `ValueObjectHttpBinding` by name, and code written by `codegen write` is compiled into the application:
  a change to a signature there breaks that code until it is written again, so it changes only with a release note
  saying so. Static mode never notices stale code: code written with the package keeps running after `UseValueObjects()`
  is removed, and code written before it was installed keeps Wolverine's lenient binding.
- `httpContext` is the generated handler's parameter, which Wolverine's own frames write too. Keys go through
  `ValueObjectReadFrame.Literal`, so a name holding a quote stays one literal; types through JasperFx's
  `FullNameInCode()`, as Wolverine's frames write them; the helper as `global::…`, whatever the application's usings.
- A refusal is recorded on `HttpContext.Items` (`ValueObjectRefusals`, `src/Shared/ValueObjectRefusals.cs`) and its code
  through `ValueObjectProblemDetails.RecordErrorCode`; the messages come from the policy in Wolverine's options, read
  from the request's services, so a request outside Wolverine keeps the English ones.
- The registry is never asked: the generated code names the value object, so a generic construction and one written by
  hand bind without registration, natively too. The one reflection call is the interface lookup at startup
  (`ValueObjectHttpParameter.TryGetValueType`, IL2070 suppressed with the registry's justification).
- F# endpoints are not supported: the frames write C# alone, and JasperFx's base `GenerateFSharpCode` throws
  `NotSupportedException` naming the frame.

## Builds and tests

- WolverineFx lists `JasperFx.SourceGenerator` with `include="All"`, which reaches this project despite
  WolverineFx.Http's `exclude="Build,Analyzers"`: `LeaveJasperFxGeneratorsOut` removes it after `ResolveReferences`, or
  an empty `JasperFx.Generated.DiscoveredEventModels` lands in the assembly.
- Dynamic mode needs WolverineFx.RuntimeCompilation, whose JasperFx.RuntimeCompiler 5.0.0 asks for Roslyn 5.0.0 and its
  Workspaces: the fixture application alone references it, through conditional `PackageVersion`s keyed on its project
  name. The unit suite runs Wolverine in `TypeLoadMode.Auto` without it (chains, ApiExplorer, documents and
  `DynamicCodeBuilder.GenerateAllCode()` work; a request would fail), and every test building a host joins the
  `WolverineHttpHosts` collection, run alone: `GenerateAllCode()` turns JasperFx's static
  `DynamicCodeBuilder.WithinCodegenCommand` on while it runs.
- `GenerateAllCode()` writes the endpoint registry twice (two collections hold it): a test compiling the code compiles
  each file once, as `codegen write`'s second copy overwrites the first on disk.
- The namespace hides the root `Wolverine` namespace (root `CLAUDE.md`): name Wolverine's types through usings at the
  top of a file.
- The `WolverineFx.Http` pin is the package's floor, and the weekly `dotnet` Dependabot group moves it, each pull request
  a `fix(deps)` patch: the maintainer's choice (2026-10-18), the repository's rule for a shipped dependency, over ignoring
  WolverineFx's minors and moving the floor by hand. Its majors are ignored: Wolverine 7 is a floor to decide, since the
  package reaches three of its internals (`docs/maintaining.md`).
