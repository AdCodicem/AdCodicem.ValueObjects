---
title: How the Library Is Tested
sidebar_label: How the library is tested
slug: /testing
description: The four test suites behind AdCodicem.ValueObjects — generated behaviour, the generator itself, real databases, minimal APIs bound by the Request Delegate Generator —, the applications CI publishes with native AOT, the packages run on the next .NET, and the two paths bugs hide in.
---

# How the library is tested

To test your own value objects, see [Test your value objects](./how-to/test-value-objects.md). This page is
about the library's own suites.

Four suites, each with a distinct job:

- **UnitTests** — behaviour of generated code, and of every integration package called directly. The sample
  value objects declare each of the 22 underlying types and each option and hook at least once, so that what the
  generator emits for every one of them runs rather than only compiles. Generated sources are emitted to disk
  during the build, so they can be read when diagnosing a failure instead of decompiled from memory. Value
  objects written by hand reach what the generator always replaces, such as the default members of the
  contracts, and four small fixture assemblies hold what the test assembly cannot: a generated value object whose
  module has not been used yet, annotated value objects in an assembly the generator does not run on,
  generated value objects in an assembly that does not reference the JSON package, and generated value objects of
  every underlying type in an assembly that opts into XML serialization.
- **GeneratorTests** — the generator itself: emission, every diagnostic, hook detection, the analyzers, and
  incremental caching. It drives Roslyn directly rather than through a testing harness that binds to an older
  xUnit, and it compiles snippets **without** implicit usings, which is what catches an unqualified name that
  slipped into emitted code. The incrementality tests assert on the Roslyn incremental step run reason — the
  only way to notice a caching regression, since losing incrementality breaks nothing visible while making
  every IDE keystroke re-run the pipeline.
- **IntegrationTests** — real PostgreSQL and SQL Server, asserting against `information_schema` that value
  objects reach the column types they claim, Dapper and EF Core round trips, plus the API surface end to end.
  These need a Docker daemon: Testcontainers starts both engines for the run.
- **RdgTests** — minimal API endpoints whose binding the Request Delegate Generator writes, as it does in every
  build under `PublishAot` or `PublishTrimmed`, over value objects declared in the endpoints' own project: the case
  [the Request Delegate Generator](./how-to/aspnet-core.md#the-request-delegate-generator) cannot see the
  generator's output for. A route, query or header value binds, a refused one answers 400, an absent optional one
  binds `null`, and a test fails if the RDG did not write the binding, so that the suite cannot silently become one
  more test of the reflection-based binding. The same refusals are answered with the minimal API problem details, in
  each setting of `ThrowOnBadRequest`, where the RDG refuses no empty query text and takes an empty header for an
  absent one.

`AdCodicem.ValueObjects.Testing` ships the contract kit (`ValueObjectContract`) described in
[Test your value objects](./how-to/test-value-objects.md); the unit tests use it on every generated sample value
object but two, so the framework's own test suite is a live example of how a consumer would use it. `Floor` and
`Celsius` cannot satisfy it: their formatting hooks write text such as `floor 3` or `21 °C`, which does not parse
back, and the kit requires a text round trip.

## Native AOT and compiled models

Two applications, built as an application builds them, are published rather than tested.

The first references every package that claims to be AOT-compatible: the contracts, the generated code, the JSON package
with a source-generated context and the JSON Schema it exports, the minimal API problem details, the identifiers,
FluentValidation and the Serilog integration. Its value objects cover each of the 22 underlying types, a pattern hook, a
closed set, an identifier, a generic value object registered by hand, and two value objects written by hand, registered
without a converter, which the JSON factory serves its general-purpose one, beside a third that nothing registers, which
it refuses. It runs a fixed script over every one of them — the typed path, the descriptor, JSON, FluentValidation,
Serilog, each value object logged checked against the bare value it carries, and requests over Kestrel to minimal API
endpoints the Request Delegate Generator binds, refused values answered with problem details in each setting of
`ThrowOnBadRequest`, each answer checked against the status and the codes the script expects — once under the JIT and
once as a native AOT binary. CI's `native AOT` job fails on any trimming or AOT warning in the publish, and on any
difference between the two outputs, and a pull request merges only once it passes, so the claim of AOT compatibility is
run, not only analysed.

The second holds an Entity Framework Core context mapping every value object the conventions map, required and
optional, a generic one and an identifier as the key, beside a strict context. `dotnet ef dbcontext optimize`
writes its compiled model on every pull request, which the project then builds on and takes on a round trip through
SQL Server; the native AOT job also writes the model for native AOT, with its queries precompiled, and publishes it.
[Compiled models](./how-to/ef-core.md#compiled-models) says what holds there.

## The compatibility island

The four suites build and test the source. One more project tests the packages: `tests/Compat`, outside the
solution, installs the sixteen packages exactly as they were packed — from the folder the build packs into, at that
one version, never from nuget.org — into `net11.0` applications on the .NET 11 release candidate. It has its own
SDK, its own package versions and no transitive pinning, so the dependency floors of the packages meet the next
major as they would in an application. It runs the generator in that SDK's compiler, Entity Framework Core 11 on
SQLite, SQL Server and PostgreSQL, System.Text.Json source generation, ASP.NET Core model binding on System.Text.Json
and on Newtonsoft.Json, and a minimal API with its problem details,
`Microsoft.AspNetCore.OpenApi` 11 over `Microsoft.OpenApi` 3, Dapper, FluentValidation, Newtonsoft.Json, Serilog,
`XmlSerializer` and `DataContractSerializer` over a domain that opts into XML serialization, and the contract kit. A second project installs the Swashbuckle package, Swashbuckle 10 failing on the `Microsoft.OpenApi` 3
the first one's `Microsoft.AspNetCore.OpenApi` 11 brings: there, Swashbuckle documents a minimal API over
`Microsoft.OpenApi` 2, as in an application that takes Swashbuckle alone.

CI's `compat (.NET 11)` job runs it on every pull request, against the packages that commit packed. It informs and
blocks nothing until .NET 11 ships, then becomes a required check. The "On the next .NET" column of
[Supported frameworks](./packages.md#supported-frameworks) states what it covers.

## Two ways to reach a value object — and why bugs hide in one of them

- **Typed path.** The static abstract members of `IValueObject<TSelf, TValue>` (`Create`, `TryCreate`,
  `TryParse`, `Normalize`, `Validate`). This is what domain code and the generic integrations use — the ASP.NET
  binder, the EF converter and the Dapper handler are all generic and closed over the concrete types at
  startup, so per-request work is fully typed and allocates nothing extra.
- **Boxed path.** A descriptor resolved from a runtime registry, for callers that only know a `Type` at run
  time — dynamic parsing, the OpenAPI transformer and the Swashbuckle filters, model-binder resolution.

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
dotnet test -c Release                                    # all four suites
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests         # behaviour of generated code
dotnet test --project tests/AdCodicem.ValueObjects.GeneratorTests    # the generator itself
dotnet test --project tests/AdCodicem.ValueObjects.IntegrationTests  # needs Docker
dotnet test --project tests/AdCodicem.ValueObjects.RdgTests          # minimal APIs through the RDG
dotnet pack src/AdCodicem.ValueObjects.Packages.slnf -c Release -o artifacts/packages   # the packable projects only

# The compatibility island, from its own folder, with the .NET 11 SDK its global.json names. Pack at a fresh
# version: NuGet reuses whatever its global packages folder already holds at a version it has seen.
v=0.0.0-compat.$(date +%s)
MINVERVERSIONOVERRIDE=$v dotnet pack src/AdCodicem.ValueObjects.Packages.slnf -c Release -o artifacts/packages
cd tests/Compat && dotnet test --project AdCodicem.ValueObjects.CompatTests.csproj -p:AdCodicemVersion=$v
# the main project needs Docker for PostgreSQL and SQL Server; without it, add --filter-not-trait "Requires=Docker"
dotnet test --project Swashbuckle/AdCodicem.ValueObjects.CompatTests.Swashbuckle.csproj -p:AdCodicemVersion=$v   # no Docker

# Native AOT (needs clang and zlib), and the compiled model, for the JIT (a round trip, needs Docker) and native AOT
.github/scripts/native-aot.sh
.github/scripts/compiled-model.sh jit
.github/scripts/compiled-model.sh aot

# One test (Microsoft Testing Platform: a wildcard pattern, not a substring)
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests --filter-method "*The_name_of_the_test*"
```

`TreatWarningsAsErrors` is on repository-wide, so a warning fails the build before it reaches any of the four
suites. The island sets it too.

Next: [Benchmarks](./benchmarks.md), for the numbers behind the design decisions.
