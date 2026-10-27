# AdCodicem.ValueObjects.UnitTests.GraphQL

The graphql family's unit tests (HotChocolate), in `GraphQL/`: the scalars, errors, schema and the HTTP endpoint. The
suite's conventions are in `tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`, the package's traps in
`src/AdCodicem.ValueObjects.HotChocolate/CLAUDE.md`.

- `GraphQL/GraphQLHarness` builds executors in process; the HTTP endpoint is served over TestHost.
- This project sets `HotChocolateImplicitUsings` to `disable`: HotChocolate.AspNetCore's global usings would bring its
  `DefaultValueAttribute`, `Path` and `Tag` into every file, beside the suite's own names, so `GraphQL/` names
  HotChocolate's types through usings of its own.
- The `[QueryType]` HotChocolate's source generator binds is `Fixtures.GraphQL`'s, so that this assembly gets no
  generated module (`tests/Fixtures/CLAUDE.md`).
- It links core's `Web/ValidationMessages.*`, `Web/SparseMessages.*`, `TestData/UnboundedDecimal.cs` and
  `WithoutDynamicCode.cs` (`GraphQL/WithoutDynamicCodeGraphQLTests`), at the same paths.
