# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Ten NuGet packages for single-value DDD value objects on .NET 10. A `readonly partial struct` marked
`[ValueObject<T>]` gets its whole implementation from a Roslyn incremental generator, and crosses every boundary
as its underlying type: an IBAN is a JSON string, a `VARCHAR`, and a query-string parameter — never an object
wrapper. Consumers define their own value objects; this repository ships the frame.

Read `README.md` for the authoring surface and `benchmarks/README.md` for the measurements behind the design
decisions. The published documentation (`website/`, deployed from `main`) reorganises this same material into a
narrative site — edit the source of truth first (README, this file, the benchmark numbers), then the
corresponding page under `website/docs/`.

## Commands

```bash
dotnet build -c Release
dotnet test -c Release                                    # all three suites
dotnet test tests/AdCodicem.ValueObjects.UnitTests        # behaviour of generated code
dotnet test tests/AdCodicem.ValueObjects.GeneratorTests   # the generator itself
dotnet test tests/AdCodicem.ValueObjects.IntegrationTests # needs Docker
dotnet pack -c Release -o artifacts/packages

# One test
dotnet test tests/AdCodicem.ValueObjects.UnitTests --filter "FullyQualifiedName~The_name_of_the_test"

# Benchmarks; wants a quiet machine, and absolute timings are not comparable across runs
cd benchmarks/AdCodicem.ValueObjects.Benchmarks
dotnet run -c Release -- --filter '*WrapperCost*'

# Documentation site (Docusaurus, published to https://adcodicem.github.io/AdCodicem.ValueObjects/)
cd website
npm ci
npm start          # local dev server
npm run build       # production build; fails on a broken internal link
```

Integration tests start PostgreSQL and SQL Server through Testcontainers.

`TreatWarningsAsErrors` is on repository-wide, so a warning fails the build.

## Architecture

### The generator is the centre

`ValueObjectGenerator` (`ForAttributeWithMetadataName`) → `ValueObjectModel` (an equatable record, so an
unrelated edit does not re-run the pipeline) → `ValueObjectEmitter`, which delegates to `JsonConverterEmitter`,
`TypeConverterEmitter` and `RegistrationEmitter`. `Model/UnderlyingType.cs` is the closed table of the 22
supported underlying types and drives nearly every per-type decision the emitters make.

`RegistrationEmitter` produces a `[ModuleInitializer]` that populates `ValueObjectRegistry`, so descriptors are
available without the consumer registering anything.

### Two ways to reach a value object — and why bugs hide in one of them

- **Typed path.** The static abstract members of `IValueObject<TSelf, TValue>` (`Create`, `TryCreate`,
  `TryParse`, `Normalize`, `Validate`). This is what domain code and the generic integrations use — the ASP.NET
  binder, the EF converter and the Dapper handler are all generic and closed over the concrete types at startup,
  so per-request work is fully typed and allocates nothing extra.
- **Boxed path.** `ValueObjectDescriptor`, resolved from `ValueObjectRegistry`, for callers that only know a
  `Type` at run time — `MustParseAs(Type)`, the OpenAPI transformer, model-binder resolution.

The unit tests exercise the typed path, so a defect confined to the descriptor is invisible to them. That is
exactly how the descriptor once flattened every rejection into a generic `not_parsable`, discarding the rule
that actually fired. `DescriptorTests.cs` exists to cover that surface; extend it when touching the descriptor.

### Invariants worth knowing before changing anything

- **Normalize, then validate, then assign**, so a non-default instance is by construction normalized and valid.
  The one exception is the EF Core read path, which uses `CreateUnchecked` because it reads values this same
  application already validated. `ConfigureValueObjects(strict: true)` turns validation back on.
- **Rejection is not an exception.** `ValidationResult` is a struct that allocates nothing on success; every
  integration goes through `TryCreate`. Validation is fail-fast: the first violated rule wins.
- **Rules are declared once.** `MaxLength = 34` validates, sizes the EF column and becomes the OpenAPI
  `maxLength`. Anything added to `[ValueObject<T>]` should feed all three.
- **`default(T)` is a build error** (`VO0010`). Tests that deliberately construct one need a targeted
  `#pragma warning disable VO0010` with a comment.

### Hooks are interfaces

A value object declares a rule by implementing `IValueObjectNormalizer<T>`, `IValueObjectSpanNormalizer`,
`IValueObjectValidator<T>`, `IValueObjectFormatter<T>` or `IValueObjectStringFormatter<T>`
(`src/AdCodicem.ValueObjects.Abstractions/ValueObjectHooks.cs`). The compiler then checks the signature. The
rules are public because a static abstract interface member cannot be anything else; `Normalize` remains the
member callers use, guarding null before deferring to `NormalizeValue`. `VO0011` reports the one mistake left:
a rule written without its interface.

## Constraints that will bite you

These are all load-bearing, and each cost real debugging time:

- **Source generators never observe each other's output.** The `[JsonConverter]` this generator writes is
  invisible to the System.Text.Json generator, which is the entire reason `AdCodicem.ValueObjects.Json` exists:
  a hand-written `ValueObjectJsonConverterFactory` the STJ generator *can* see, named via
  `[JsonSourceGenerationOptions(Converters = ...)]`. The same constraint rules out `[GeneratedRegex]` in emitted
  code, which is why `Pattern` compiles a `Regex` with `RegexOptions.Compiled`.
- **`static virtual` and `static abstract` interface members are reachable only through a type parameter**
  (CS8926, CS0103 for explicit implementations). Default implementations on `INumericValueObject` are therefore
  unusable directly; the generator emits concrete members, and `UnderlyingValue` holds constrained generic
  bridges (which the JIT devirtualizes) for things like `Guid.TryFormat`, whose 4-argument overload is an
  explicit interface implementation.
- **Generated code cannot rely on the consumer's usings.** Fully qualify everything, extension methods
  included — `global::System.MemoryExtensions.AsSpan(x)`, never `x.AsSpan()`. A consumer with
  `ImplicitUsings` disabled otherwise gets a compile error in code they cannot edit.
- **`SymbolDisplayFormat.FullyQualifiedFormat` implies `UseSpecialTypes`**, so it yields `string` rather than
  `global::System.String` and every primitive fails metadata resolution. Use the `QualifiedFormat` constant in
  `ValueObjectGenerator`.
- **The syntax predicate admits any `TypeDeclarationSyntax`**, not just structs, so a value object written as a
  class or a record struct reaches `VO0002` instead of silently generating nothing.
- **Analyzer release tracking** (`AnalyzerReleases.Shipped.md` / `.Unshipped.md`) must list every diagnostic, or
  RS2008 fails the build.

## Testing

Three suites, each with a distinct job:

- **UnitTests** — behaviour of generated code, using value objects defined in `Domain/`. `EmitCompilerGeneratedFiles`
  is on, so generated sources land under `artifacts/obj/.../generated/` and can be read when diagnosing.
- **GeneratorTests** — the generator itself: emission, every diagnostic, hook detection, the analyzers, and
  incremental caching. It drives Roslyn directly through `Harness/GeneratorHarness.cs` rather than through
  `Microsoft.CodeAnalysis.Testing`, which binds to xUnit v2. Snippets compile **without** implicit usings, which
  is what catches unqualified names in emitted code. The incrementality tests assert on
  `IncrementalStepRunReason`, the only way to notice caching regressions — losing them breaks nothing visible
  while making every IDE keystroke re-run the pipeline.
- **IntegrationTests** — real PostgreSQL and SQL Server, asserting against `information_schema` that value
  objects reach the column types they claim, plus the API surface end to end.

`AdCodicem.ValueObjects.Testing` ships a contract kit (`ValueObjectContract`) that consumers point at their own
types; the unit tests use it on every sample value object.

Stack: xUnit v3 (`TestContext.Current.CancellationToken`), AwesomeAssertions, NSubstitute, Testcontainers.
Versions are centrally managed in `Directory.Packages.props`; versions live there, never in a `.csproj`.
