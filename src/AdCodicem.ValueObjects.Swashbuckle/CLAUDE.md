# AdCodicem.ValueObjects.Swashbuckle

The description it shares with the OpenAPI transformer is `src/Shared/ValueObjectOpenApiSchema.cs`
(`src/Shared/CLAUDE.md`).

- Swashbuckle 10 is built on `Microsoft.OpenApi` 2 and throws `MissingMethodException` once `Microsoft.OpenApi` 3 is
  resolved, which `Microsoft.AspNetCore.OpenApi` 11 forces: the island installs this package from a project of its own,
  `tests/Compat/Swashbuckle`. Moving to Swashbuckle's next major is a decision (ADR 0012), not a bump.
- The floor on `Microsoft.OpenApi` is the transitive pin of `Directory.Packages.props`, 2.12.2: the 2.7.5 Swashbuckle asks
  for drops the example of an OpenAPI 3.0 schema.
- Pin no version of `Swashbuckle.AspNetCore.Swagger`, which the tests take through `Swashbuckle.AspNetCore.SwaggerGen`:
  the pin would make it a dependency of the package, to list in `.github/shipped-dependencies`.
- Swashbuckle creates its schema filters with `ActivatorUtilities` while it configures `SchemaGeneratorOptions`. A filter
  whose constructor takes `SchemaGeneratorOptions`, `ISchemaGenerator` or, from 10.3.0, `ISerializerDataContractResolver`
  has the options configured again from inside their own configuration, and the document request hangs, with no error,
  once the container's `StackGuard` moves the recursion to a thread waiting on the first one's lock
  (domaindrivendev/Swashbuckle.AspNetCore#4207).
  `IOptions<SchemaGeneratorOptions>` alone is safe, its value read lazily. `ValueObjectSchemaFilter` takes the container
  and resolves both as it describes a schema: keep it so. Tests await every document with a timeout.
