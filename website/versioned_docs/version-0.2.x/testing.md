---
title: Testing
sidebar_label: Testing
slug: /testing
---

# Testing

Three suites, each with a distinct job:

- **UnitTests** — behaviour of generated code, using sample value objects. Generated sources are emitted to
  disk during the build, so they can be read when diagnosing a failure instead of decompiled from memory.
- **GeneratorTests** — the generator itself: emission, every diagnostic, hook detection, the analyzers, and
  incremental caching. It drives Roslyn directly rather than through a testing harness that binds to an older
  xUnit, and it compiles snippets **without** implicit usings, which is what catches an unqualified name that
  slipped into emitted code. The incrementality tests assert on the Roslyn incremental step run reason — the
  only way to notice a caching regression, since losing incrementality breaks nothing visible while making
  every IDE keystroke re-run the pipeline.
- **IntegrationTests** — real PostgreSQL and SQL Server, asserting against `information_schema` that value
  objects reach the column types they claim, plus the API surface end to end. These need a Docker daemon:
  Testcontainers starts both engines for the run.

`AdCodicem.ValueObjects.Testing` ships the contract kit (`ValueObjectContract`) introduced in
[Getting Started](./getting-started.md#testing-your-own-value-objects); the unit tests use it on every sample
value object in the repository, so the framework's own test suite is a live example of how a consumer would use
it.

## Two ways to reach a value object — and why bugs hide in one of them

- **Typed path.** The static abstract members of `IValueObject<TSelf, TValue>` (`Create`, `TryCreate`,
  `TryParse`, `Normalize`, `Validate`). This is what domain code and the generic integrations use — the ASP.NET
  binder, the EF converter and the Dapper handler are all generic and closed over the concrete types at
  startup, so per-request work is fully typed and allocates nothing extra.
- **Boxed path.** A descriptor resolved from a runtime registry, for callers that only know a `Type` at run
  time — dynamic parsing, the OpenAPI transformer, model-binder resolution.

Unit tests naturally exercise the typed path, so a defect confined to the descriptor can be invisible to them
unless the suite deliberately covers that surface too. That asymmetry is worth keeping in mind when adding a
test for a new rule: prove it holds on both paths, not just the one that's convenient to call from a unit test.

## Stack

xUnit v3, AwesomeAssertions, NSubstitute, Testcontainers.

## Commands

```bash
dotnet build -c Release
dotnet test -c Release                                    # all three suites
dotnet test tests/AdCodicem.ValueObjects.UnitTests         # behaviour of generated code
dotnet test tests/AdCodicem.ValueObjects.GeneratorTests    # the generator itself
dotnet test tests/AdCodicem.ValueObjects.IntegrationTests  # needs Docker
dotnet pack -c Release -o artifacts/packages

# One test
dotnet test tests/AdCodicem.ValueObjects.UnitTests --filter "FullyQualifiedName~The_name_of_the_test"
```

`TreatWarningsAsErrors` is on repository-wide, so a warning fails the build before it reaches any of the three
suites.

Next: [Benchmarks](./benchmarks.md), for the numbers behind the design decisions.
