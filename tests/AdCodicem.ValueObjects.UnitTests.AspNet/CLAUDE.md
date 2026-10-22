# AdCodicem.ValueObjects.UnitTests.AspNet

The aspnet family's unit tests (AspNetCore.Http, AspNetCore, OpenApi), in `Web/`: MVC, minimal APIs, OpenAPI, problem
details, and DataAnnotations through hosts. The suite's conventions are in
`tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`, each package's traps in its `src/<Package>/CLAUDE.md` (none for
OpenApi).

- Requests go to an in-memory server (Microsoft.AspNetCore.TestHost), without a socket; the sample's end-to-end flows
  stay in the integration suite.
- This project adds `Microsoft.AspNetCore.OpenApi.Generated` to `InterceptorsNamespaces`: Microsoft.AspNetCore.OpenApi's
  generator reaches it through the OpenApi project and intercepts `AddOpenApi`, and the package's targets, which allow
  its interceptors, come only with a direct reference. The Serialization and Hosts projects set it too, for the same
  reason.
- Its helpers `Web/ProblemReading.cs` (`ProblemReading`, `ReadProblem`, the problem JSON contexts), linked by the
  Serialization and Hosts projects, and `Web/AnnotatedModels.cs`, `Web/OpenApiDocument.cs` and `Web/ProbeController.cs`,
  linked by the Serialization project, hold no test: a change to one runs those families too.
- From core's folder it links `DataAnnotations/Annotated.cs` and the French messages of `Web/` (`ValidationMessages`,
  `SparseMessages`, each with its `.fr.resx`), at the same paths.
- `Fixtures.WithoutGenerator` supplies hand-written value objects annotated where the generator does not run.
- A change to this project runs the RDG suite too (`.github/scripts/ci-plan.sh`).
