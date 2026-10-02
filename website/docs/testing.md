---
title: How the Library Is Tested
sidebar_label: How the library is tested
slug: /testing
description: The three test suites behind AdCodicem.ValueObjects — generated behaviour, the generator itself, real databases — and the two paths bugs hide in.
---

# How the library is tested

To test your own value objects, see [Test your value objects](./how-to/test-value-objects.md). This page is
about the library's own suites.

Three suites, each with a distinct job:

- **UnitTests** — behaviour of generated code, and of every integration package called directly. The sample
  value objects declare each of the 22 underlying types and each option and hook at least once, so that what the
  generator emits for every one of them runs rather than only compiles. Generated sources are emitted to disk
  during the build, so they can be read when diagnosing a failure instead of decompiled from memory. Value
  objects written by hand reach what the generator always replaces, such as the default members of the
  contracts, and three small fixture assemblies hold what the test assembly cannot: a generated value object whose
  module has not been used yet, annotated value objects in an assembly the generator does not run on, and
  generated value objects in an assembly that does not reference the JSON package.
- **GeneratorTests** — the generator itself: emission, every diagnostic, hook detection, the analyzers, and
  incremental caching. It drives Roslyn directly rather than through a testing harness that binds to an older
  xUnit, and it compiles snippets **without** implicit usings, which is what catches an unqualified name that
  slipped into emitted code. The incrementality tests assert on the Roslyn incremental step run reason — the
  only way to notice a caching regression, since losing incrementality breaks nothing visible while making
  every IDE keystroke re-run the pipeline.
- **IntegrationTests** — real PostgreSQL and SQL Server, asserting against `information_schema` that value
  objects reach the column types they claim, Dapper and EF Core round trips, plus the API surface end to end.
  These need a Docker daemon: Testcontainers starts both engines for the run.

`AdCodicem.ValueObjects.Testing` ships the contract kit (`ValueObjectContract`) described in
[Test your value objects](./how-to/test-value-objects.md); the unit tests use it on every generated sample value
object but two, so the framework's own test suite is a live example of how a consumer would use it. `Floor` and
`Celsius` cannot satisfy it: their formatting hooks write text such as `floor 3` or `21 °C`, which does not parse
back, and the kit requires a text round trip.

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

## Coverage

Every pull request reports its coverage through Codecov: 95 % of the lines it changes, and the project's
coverage down by half a point at most. That is a floor; the aim is everything that can be covered, under a rule
that keeps the number honest — a defensive branch no test reaches stays, a private member is covered through its
callers or not at all, and code is removed only when no input can reach it. The rule, and the two things the
figure cannot show (a package no test loads, and the code the generator emits into the consumer's assembly), are
on the [Contributing](/contributing) page.

## Stack

xUnit v3, AwesomeAssertions, NSubstitute, FsCheck, Testcontainers.

## Commands

```bash
dotnet build -c Release
dotnet test -c Release                                    # all three suites
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests         # behaviour of generated code
dotnet test --project tests/AdCodicem.ValueObjects.GeneratorTests    # the generator itself
dotnet test --project tests/AdCodicem.ValueObjects.IntegrationTests  # needs Docker
dotnet pack -c Release -o artifacts/packages

# One test (Microsoft Testing Platform: a wildcard pattern, not a substring)
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests --filter-method "*The_name_of_the_test*"
```

`TreatWarningsAsErrors` is on repository-wide, so a warning fails the build before it reaches any of the three
suites.

Next: [Benchmarks](./benchmarks.md), for the numbers behind the design decisions.
