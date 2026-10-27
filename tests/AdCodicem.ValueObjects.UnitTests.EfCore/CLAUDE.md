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
- A test ordering decimals on SQLite runs in the invariant culture, set and given back around its body: EF Core 10's
  `EF_DECIMAL` collation parses the stored text in the current culture, and throws `FormatException` under fr-FR
  (dotnet/efcore#37432, fixed in EF Core 11), value object or not.
- `Persistence/EntityFrameworkCoreCheckErrorTests.cs` stands for the path only `SaveChangesFailed` sees by throwing
  SQLite's error from `DataReaderClosing`; the integration suite runs it for real.
- This project resolves Npgsql 10.0.3, through Npgsql.EntityFrameworkCore.PostgreSQL.
- It links the Data project's `Persistence/MongoDbShapedCode.cs` and core's `Persistence/NonUnicodeValueObjects.cs`
  (`RegisteredAsciiDeclaration`, which `EntityFrameworkCoreUnicodeTests` uses), at the same paths.
- The GraphQL project references the Entity Framework Core package too, so a change to that package runs the graphql
  family as well; a change to this project runs the compiled model (`.github/scripts/ci-plan.sh`).
