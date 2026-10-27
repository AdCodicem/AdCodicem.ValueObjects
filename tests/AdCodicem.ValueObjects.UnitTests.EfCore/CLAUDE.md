# AdCodicem.ValueObjects.UnitTests.EfCore

The efcore family's unit tests (EntityFrameworkCore, Identifiers.EntityFrameworkCore), in `Persistence/`: the
conventions and converters, keys, CHECK constraints, primitive collections, `.Value` queries and SQL patterns. The
suite's conventions are in `tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`, each package's traps in its
`src/<Package>/CLAUDE.md` (none for Identifiers.EntityFrameworkCore).

- EF Core builds a model once per context type: each model shape gets a context type of its own.
- `EF.IsDesignTime` is global: a test setting it joins the `EntityFrameworkCoreDesignTime` collection, run alone, and
  gives it back.
- Three providers, no server: Npgsql's builds the model the conventions shape without opening a connection; SQLite in
  memory runs what only an engine shows (the JSON of a primitive collection, queries over its elements, change
  tracking, generated keys, `.Value` translated to the column, CHECK constraints accepting and refusing rows); the
  in-memory provider, which is not relational, generates no integer key. What a real server makes of them stays in the
  integration suite.
- `Persistence/EntityFrameworkCoreCheckErrorTests.cs` stands for the path only `SaveChangesFailed` sees by throwing
  SQLite's error from `DataReaderClosing`; the integration suite runs it for real.
- This project resolves Npgsql 10.0.3, through Npgsql.EntityFrameworkCore.PostgreSQL.
- It links the Data project's `Persistence/MongoDbShapedCode.cs` and core's `Persistence/NonUnicodeValueObjects.cs`
  (`RegisteredAsciiDeclaration`, which `EntityFrameworkCoreUnicodeTests` uses), at the same paths.
- The GraphQL project references the Entity Framework Core package too, so a change to that package runs the graphql
  family as well; a change to this project runs the compiled model (`.github/scripts/ci-plan.sh`).
