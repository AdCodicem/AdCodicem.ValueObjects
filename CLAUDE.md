# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Ten NuGet packages for single-value DDD value objects on .NET 10. A `readonly partial struct` marked
`[ValueObject<T>]` gets its whole implementation from a Roslyn incremental generator, and crosses every boundary
as its underlying type: an IBAN is a JSON string, a `VARCHAR`, and a query-string parameter — never an object
wrapper. Consumers define their own value objects; this repository ships the frame.

Read `README.md` for the authoring surface and `benchmarks/README.md` for the measurements behind the design
decisions. The published documentation (`website/`) reorganises this same material into a narrative site — edit
the source of truth first (README, this file, the benchmark numbers), then the corresponding page under
`website/docs/`. The site is versioned: `website/docs/` is the preview and describes `main`, while
`website/versioned_docs/` holds what each stable line was released with (see Releases below). A change to
`website/docs/` therefore reaches the stable pages at the next release, not before.

`skills/value-objects/` is the consumer-facing agent skill, distributed as a Claude Code plugin through
`.claude-plugin/`. It is prescriptive only — the authoring surface, the hooks, the wiring, the diagnostics — and
deliberately carries no rationale or benchmark numbers, so it stays a short file rather than a fourth copy of
the documentation. Anything that changes the surface a consumer writes (an option on `[ValueObject<T>]`, a hook
interface, a diagnostic, an extension method) must be reflected there too.

## Commands

```bash
dotnet build -c Release
dotnet test -c Release                                    # all three suites
# One suite. --project is required: given a bare directory, dotnet test prints a hint and exits 0
# without running anything, which reads exactly like a pass.
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests        # behaviour of generated code
dotnet test --project tests/AdCodicem.ValueObjects.GeneratorTests   # the generator itself
dotnet test --project tests/AdCodicem.ValueObjects.IntegrationTests # needs Docker

# Formatting, as CI checks it. Not plain `dotnet format`: its workspace does not run source
# generators, so `analyzers` reports ASP0020 against the sample's minimal API endpoint for an
# IParsable<T> the generator does emit.
dotnet format AdCodicem.ValueObjects.slnx whitespace --verify-no-changes
dotnet format AdCodicem.ValueObjects.slnx style --verify-no-changes
dotnet pack -c Release -o artifacts/packages

# One test (xunit.v3 runs on Microsoft Testing Platform; wildcards, not substrings, so wrap the name in *)
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests --filter-method "*The_name_of_the_test*"

# Benchmarks; wants a quiet machine, and absolute timings are not comparable across runs
cd benchmarks/AdCodicem.ValueObjects.Benchmarks
dotnet run -c Release -- --filter '*WrapperCost*'

# Documentation site (Docusaurus, published to https://adcodicem.github.io/AdCodicem.ValueObjects/)
cd website
npm ci
npm run docs:api    # regenerate docs/api/ from the XML doc comments (DocFX; not committed)
npm start           # local dev server
npm run build       # production build; fails on a broken internal link
```

`npm run docs:api` has to run before `npm start` or `npm run build` on a fresh checkout: `sidebars.ts`
builds its API reference category from `website/docs/api/`, which is generated and gitignored.

Integration tests start PostgreSQL and SQL Server through Testcontainers.

`TreatWarningsAsErrors` is on repository-wide, so a warning fails the build.

## Releases

Merging to `main` publishes a **preview** to nuget.org, versioned by MinVer with no decision from anyone. A
**stable** release is a manual `workflow_dispatch` on `release.yml`, where semantic-release computes the
version from the Conventional Commits, writes `CHANGELOG.md`, packs, pushes, tags and redeploys the site. The
bridge between the two is `MINVERVERSIONOVERRIDE`: semantic-release hands MinVer the stable version and MinVer
steps aside. Both read the same `v*` tags.

The GitHub Release links every package to its version on nuget.org. That list is never written down:
`.github/scripts/package-ids.sh` evaluates the packable projects under `src/` into `RELEASE_PACKAGE_IDS` before
semantic-release starts (the release body is rendered from that starting environment, so a prepare step cannot
feed it), and `release-pack.sh` fails the run before the push if the packages it built differ from that list.
The **attest provenance** job then signs those packages with a Sigstore SLSA provenance attestation and attaches
the bundle to the release. It attests the release assets, not the nuget.org copies, which nuget.org re-signs
and whose digest therefore differs.

So nothing you merge publishes a stable package, and a commit type that triggers no release (`chore`, `ci`,
`test`) also contributes nothing to the next version. The reasoning, and what it costs, is in
`docs/adr/0003-hybrid-release-manual-stable-continuous-preview.md`.

The documentation follows the same two tracks (`docs/adr/0005-version-the-documentation-site.md`). Every
preview redeploys the site, with `website/docs/` as the preview under `/docs/preview/`. A stable release
freezes `website/docs/` — generated API reference included — into `website/versioned_docs/` through
`.github/scripts/docs-snapshot.sh`, which semantic-release runs in its prepare step and commits with the
changelog. There is one entry per line, `0.<minor>.x` before 1.0 and `<major>.x` after, replaced wholesale when
the line ships again. Never add or remove an entry by hand. Editing a released page is allowed only to correct
an error that misleads users of that release, and the same fix must land in `website/docs/`, or the next
snapshot of the line discards it.

`v0.1.0` is a baseline tag placed by hand, not a release: no `0.1.0` package exists. semantic-release ignores
prerelease tags on a stable branch, so without it the first stable release would have been `1.0.0`.
`docs/maintaining.md` has the one-time settings the workflows depend on.

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
- **Every action in `.github/workflows` is pinned to a commit SHA**, with the release as a same-line comment
  (`uses: actions/checkout@3d3c42e... # v7.0.1`). Dependabot reads that comment to derive the semver bump, so a
  pin without one falls out of the `actions` group and may auto-merge as a non-major. Three of the eighteen
  actions publish *annotated* tags — `codecov/codecov-action`, `ossf/scorecard-action`, `github/codeql-action` —
  so re-pinning by hand needs `git ls-remote <repo> 'refs/tags/vX.Y.Z^{}'`: without the `^{}` you get the tag
  object's SHA, which GitHub refuses to resolve. The calls from `ci.yml` and `release.yml` to
  `./.github/workflows/deploy-docs.yml` target a local reusable workflow and must stay unpinned; GitHub rejects
  `@ref` on one.
- **NuGet lock files are deliberately absent**, and adding them breaks CI on the first run:
  `src/Directory.Build.props` references `Microsoft.SourceLink.GitHub` under
  `Condition="'$(GITHUB_ACTIONS)' == 'true'"`, so the package graph on a laptop is not the graph on the runner
  and `--locked-mode` fails `NU1004`. `docs/adr/0004-pin-the-supply-chain-by-digest-not-nuget-lock-files.md`
  has the full reasoning.
- **`website/package.json` carries `overrides`** for `qs`, `serialize-javascript` and `uuid`. All three are
  transitive under Docusaurus, which pins ranges too tight to pick up the patched versions on its own, so
  Dependabot alerts on them and `npm audit fix --force` "fixes" it by *downgrading* `@docusaurus/core` to
  3.5.2. Drop an override once Docusaurus widens the range that holds it back, not before, and re-run
  `npm audit` after touching them.

## Testing

Three suites, each with a distinct job:

- **UnitTests** — behaviour of generated code, using value objects defined in `Domain/`. `EmitCompilerGeneratedFiles`
  is on, so generated sources land under `artifacts/obj/.../generated/` and can be read when diagnosing.
  `PropertyTests.cs` runs the laws `IValueObject<TSelf, TValue>` states in prose — normalization is
  idempotent, an accepted value is a normalization fixed point, rejection never throws — over FsCheck-generated
  input. Two things keep such a suite honest and both are easy to lose: a property conditioned on "the value was
  accepted" passes vacuously unless the generator produces values the type accepts, so each one counts how often
  it reached the accepting branch and asserts on it; and a property over a wide type never lands on the boundary
  by chance, so the range generator biases towards the edges. Change a generator and re-run the mutations in the
  commit message before trusting the green.
- **GeneratorTests** — the generator itself: emission, every diagnostic, hook detection, the analyzers, and
  incremental caching. It drives Roslyn directly through `Harness/GeneratorHarness.cs` rather than through
  `Microsoft.CodeAnalysis.Testing`, which binds to xUnit v2. Snippets compile **without** implicit usings, which
  is what catches unqualified names in emitted code. The incrementality tests assert on
  `IncrementalStepRunReason`, the only way to notice caching regressions — losing them breaks nothing visible
  while making every IDE keystroke re-run the pipeline. `DocumentationSnippetTests` also runs the generator and
  both analyzers over every ```` ```csharp ```` block the repository publishes — `skills/value-objects/`,
  `README.md` and `website/docs/` — so a snippet that stops generating, or that trips `VO0011`, fails the build.
  A skill snippet must compile outright; a README or site snippet is prose and may elide a body, so only the
  declaration the generator sees is held to account. Tag a block ```` ```csharp skip ```` when it is a wiring or
  usage fragment rather than a declaration. The homepage example is covered too, because it lives in the partial
  `website/docs/_homepage-example.md` rather than in the TSX. `website/versioned_docs/` is deliberately out of
  scope: those snapshots describe older releases, not the current generator.
- **IntegrationTests** — real PostgreSQL and SQL Server, asserting against `information_schema` that value
  objects reach the column types they claim, plus the API surface end to end.

`AdCodicem.ValueObjects.Testing` ships a contract kit (`ValueObjectContract`) that consumers point at their own
types; the unit tests use it on every sample value object.

Stack: xUnit v3 (`TestContext.Current.CancellationToken`), AwesomeAssertions, NSubstitute, Testcontainers.
Versions are centrally managed in `Directory.Packages.props`; versions live there, never in a `.csproj`.
