# AdCodicem.ValueObjects.UnitTests.Hosts

The hosts family's unit tests (AzureFunctions, AzureFunctions.AspNetCore), in `Functions/` (the Azure Functions worker
and its ASP.NET Core integration). The suite's conventions are in `tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`,
each package's traps in its `src/<Package>/CLAUDE.md`.

- Azure Functions: `Functions/FunctionsDoubles.cs` writes `FunctionContext`, `FunctionDefinition` and `BindingContext`
  (public abstract classes); the worker's internal `IFunctionBindingsFeature` is a substitute. The Functions host runs
  in the integration suite.
- It links the AspNet project's `Web/ProblemReading.cs`, and core's `CodeCarrying.cs`, `Web/ValidationMessages.*`,
  `Web/SparseMessages.*` and `WithoutDynamicCode.cs`, at the same paths.
