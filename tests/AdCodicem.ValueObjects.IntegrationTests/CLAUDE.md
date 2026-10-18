# AdCodicem.ValueObjects.IntegrationTests

Real servers through Testcontainers, and the sample API of `samples/` end to end. Needs Docker.

- Images: `postgres:17-alpine`, `mssql/server:2022-latest`, `mssql/server:2025-latest` (`SqlServer2025Fixture`, the only
  one that runs `REGEXP_LIKE`), `mongo:8.0`, Azurite, and `azure-functions/dotnet-isolated:4-dotnet-isolated10.0`, which
  exists for amd64 alone.
- Assert column types against `information_schema` (`format_type` for a PostgreSQL array, whose element length only it
  shows), never against what EF Core says it mapped.
- `Domain/UnderlyingTypes.cs` is linked from the unit suite: a value object added there is stored through a real MongoDB
  too.
- MongoDB.Driver's registry is process-wide: every MongoDB test class goes through `MongoDbFixture`'s one static, strict
  registration (`ValueObjectBson.Register`, then `EntityIdBson.Register`) before it touches the driver.
- Marten (`MartenTests.cs`): each test builds its store in a schema of its own on the class's PostgreSQL container, and
  calls Marten asynchronously, the only way Marten 9 supports. Marten takes no `Iban` or `[EntityId]` for an identity
  (another public property of an identity type): an identity over text is `Label`. A compiled query's identity replay
  is pinned, not fixed: when it starts failing, Marten binds identities, and the warning of the Marten guide goes. A
  query type holding a single value object on purpose takes a targeted `#pragma warning disable VO0048` around the
  member; the forms `VO0048` names (a `long` over a `uint`, a `DateTimeOffset` in UTC, which Npgsql refuses at another
  offset, a collection over a `char`, a collection of an identity) run here, where an offline preview cannot show
  PostgreSQL accepts them.
- Azure Functions: the worker's protocol with the host is internal, so no host runs in process. This project builds
  `tests/Fixtures/AdCodicem.ValueObjects.Fixtures.Functions` without referencing its assembly, finds it through an
  `AssemblyMetadata`, and copies it into the host's image beside Azurite. Three hosts share one `FunctionsHostFixture`
  (a collection), and all but one disable their queue triggers (`AzureWebJobs.<name>.Disabled`), so that each message is
  read once.
- Assert on the host's statuses, the bodies and the package's own log lines, never on the host's wording, which a new
  image may change.
- `SqlServerCheckConstraintScriptTests` holds the SQL Server checks that need no server (the text, the script, names kept
  apart per schema, each name read back to its rule).
