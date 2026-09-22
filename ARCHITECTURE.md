# Architecture

The "where do I start reading" map. It describes the current shape; the reasoning behind that shape lives in
[Design decisions](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/design-decisions) on the documentation
site, and the decisions that were costly to reverse are recorded in [`docs/adr/`](docs/adr/).

## What the repository ships

Ten NuGet packages for single-value DDD value objects. A `readonly partial struct` marked `[ValueObject<T>]`
gets its whole implementation from a Roslyn incremental generator, and crosses every boundary as its underlying
type: an IBAN is a JSON string, a `VARCHAR`, and a query-string parameter — never an object wrapper. Consumers
define their own value objects; this repository ships the frame.

## Layout

```
src/          the ten shipped packages
tests/        three suites with distinct jobs (see below)
samples/      a showcase API exercising the whole chain end to end
benchmarks/   the measurements behind the design decisions
skills/       the consumer-facing agent skill, shipped as a Claude Code plugin via .claude-plugin/
website/      the documentation site (Docusaurus, versioned: docs/ is the preview, versioned_docs/ the releases)
docs/adr/     architecture decision records
```

## The generator is the centre

Everything else in `src/` is an integration hanging off what the generator emits.

```
ValueObjectGenerator          ForAttributeWithMetadataName over [ValueObject<T>]
        │
        ▼
ValueObjectModel              an equatable record, so an unrelated edit does not re-run the pipeline
        │
        ▼
ValueObjectEmitter ──┬─► JsonConverterEmitter
                     ├─► TypeConverterEmitter
                     └─► RegistrationEmitter ──► [ModuleInitializer] populating ValueObjectRegistry
```

`Model/UnderlyingType.cs` is the closed table of the 22 supported underlying types, and drives nearly every
per-type decision the emitters make. `RegistrationEmitter`'s module initializer is why descriptors are
available without a consumer registering anything.

Alongside the generator, two analyzers enforce what the generator cannot: `VO0010` makes `default(T)` a build
error, and `VO0011` catches a hook rule written without its interface.

## Two ways to reach a value object

This distinction is where bugs hide, so it is worth knowing before changing anything.

- **Typed path** — the static abstract members of `IValueObject<TSelf, TValue>` (`Create`, `TryCreate`,
  `TryParse`, `Normalize`, `Validate`). Domain code and every generic integration use this. The ASP.NET binder,
  the EF Core converter and the Dapper handler are all closed over the concrete types at startup, so
  per-request work is fully typed and allocates nothing extra.
- **Boxed path** — `ValueObjectDescriptor`, resolved from `ValueObjectRegistry`, for callers that only know a
  `Type` at run time: `MustParseAs(Type)`, the OpenAPI transformer, model-binder resolution.

The unit tests exercise the typed path, so a defect confined to the descriptor is invisible to them. That is
how the descriptor once flattened every rejection into a generic `not_parsable`, discarding the rule that
actually fired. `DescriptorTests.cs` covers that surface.

## Invariants

- **Normalize, then validate, then assign** — a non-default instance is by construction normalized and valid.
  The one exception is the EF Core read path, which uses `CreateUnchecked` because it reads values this same
  application already validated. `ConfigureValueObjects(strict: true)` turns validation back on.
- **Rejection is not an exception.** `ValidationResult` is a struct that allocates nothing on success, and
  every integration goes through `TryCreate`. Validation is fail-fast: the first violated rule wins.
- **Rules are declared once.** `MaxLength = 34` validates, sizes the EF column, and becomes the OpenAPI
  `maxLength`. Anything added to `[ValueObject<T>]` should feed all three.

## Testing

| Suite | Job |
|---|---|
| `UnitTests` | Behaviour of generated code, over value objects defined in `Domain/`. |
| `GeneratorTests` | The generator itself: emission, every diagnostic, hook detection, the analyzers, incremental caching, and every published documentation snippet. |
| `IntegrationTests` | Real PostgreSQL and SQL Server via Testcontainers, asserting against `information_schema`, plus the API surface end to end. |

`GeneratorTests` drives Roslyn directly through `Harness/GeneratorHarness.cs` rather than through
`Microsoft.CodeAnalysis.Testing`, which binds to xUnit v2. Its snippets compile **without** implicit usings,
which is what catches unqualified names in emitted code. Its incrementality tests assert on
`IncrementalStepRunReason` — the only way to notice a caching regression, which otherwise breaks nothing
visible while making every IDE keystroke re-run the pipeline.

## Releases

Merging to `main` publishes a preview package; a stable release is a manual action. See
[ADR-0003](docs/adr/0003-hybrid-release-manual-stable-continuous-preview.md) for why, and
[`docs/maintaining.md`](docs/maintaining.md) for the one-time settings the release and publish workflows
depend on. [ADR-0004](docs/adr/0004-pin-the-supply-chain-by-digest-not-nuget-lock-files.md) records how the
build's own dependencies are pinned, and why NuGet lock files are not part of it.
[ADR-0005](docs/adr/0005-version-the-documentation-site.md) records how the documentation site follows the same
two tracks: every preview redeploys the preview pages, and each stable release freezes its own.
