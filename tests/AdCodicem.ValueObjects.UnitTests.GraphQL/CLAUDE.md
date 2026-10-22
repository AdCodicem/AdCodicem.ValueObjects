# AdCodicem.ValueObjects.UnitTests.GraphQL

The graphql family's unit tests (HotChocolate, HotChocolate.Data), in `GraphQL/`: the scalars, errors, schema, the HTTP
endpoint, and HotChocolate.Data's filtering and sorting over Entity Framework Core on SQLite. The suite's conventions
are in `tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`, each package's traps in its `src/<Package>/CLAUDE.md`.

- `GraphQL/GraphQLHarness` builds executors in process; the HTTP endpoint is served over TestHost.
- This project sets `HotChocolateImplicitUsings` to `disable`: HotChocolate.AspNetCore's global usings would bring its
  `DefaultValueAttribute`, `Path` and `Tag` into every file, beside the suite's own names, so `GraphQL/` names
  HotChocolate's types through usings of its own.
- `ScopedFilterQuery` has no projection (a `[UseProjection]` field beside a named convention fails the schema).
- The `[QueryType]` HotChocolate's source generator binds is `Fixtures.GraphQL`'s, so that this assembly gets no
  generated module (`tests/Fixtures/CLAUDE.md`).
- It references the Entity Framework Core package, the model `GraphQL/` filters over, so a change to that package runs
  this family as well.
- It links core's `Web/ValidationMessages.*`, `Web/SparseMessages.*`, `TestData/UnboundedDecimal.cs` and
  `WithoutDynamicCode.cs` (`GraphQL/WithoutDynamicCodeGraphQLTests`), at the same paths.
