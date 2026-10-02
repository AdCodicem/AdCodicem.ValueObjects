# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Twelve NuGet packages for single-value DDD value objects on .NET 10. A `readonly partial struct` marked
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
and whose digest therefore differs. Releases are immutable on this repository — a published release takes no new
asset — so semantic-release creates the GitHub Release as a draft (`draftRelease`) and that job publishes it once
the bundle is attached. The draft's URL dies when it is published, which is why `.releaserc.json` overrides
`successComment` to link to the tag instead.

So nothing you merge publishes a stable package, and a commit type that triggers no release (`chore`, `ci`,
`test`) also contributes nothing to the next version. The reasoning, and what it costs, is in
`docs/adr/0003-hybrid-release-manual-stable-continuous-preview.md`. `build(pack)` and `docs(readme)` are the
exceptions among the types that otherwise release nothing: `.releaserc.json` rates them a patch, because the
package metadata and the README ship inside every `.nupkg` — the README is its nuget.org page — so a change to
either reaches users only through a release. Scope the commit accordingly, or the change waits for the next
`feat` or `fix`.

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
  The exceptions are the EF Core and Dapper read paths, which use `CreateUnchecked` because they read values this
  same application already validated. `ConfigureValueObjects(strict: true)` turns validation back on for EF Core;
  Dapper validates only a column the value object cannot have written: text read into a value object over another
  type, or a number read into one over `string`.
- **Rejection is not an exception on a boundary.** `ValidationResult` is a struct that allocates nothing on
  success. The integrations go through `TryCreate` or `TryParse` and report a refusal in their own terms: a
  `JsonException` or `JsonSerializationException`, a model state error, a FluentValidation failure, a Dapper
  `DataException`. The one that throws `ValueObjectException` is a strict EF Core read, which goes through `Create`
  and fails the query; `Create`, `Parse` and an explicit conversion throw it for code that treats a rejected value
  as a bug. `website/docs/reference/errors.md` names what each integration throws. Validation is fail-fast: the
  first violated rule wins.
- **Rules are declared once.** `MaxLength = 34` validates, sizes the EF column and becomes the OpenAPI
  `maxLength`. Anything added to `[ValueObject<T>]` should feed all three. A hook can feed the schema too: the
  `[GeneratedRegex]` behind `IValueObjectPatternValidator` validates, and its text, read off the attribute at
  compile time, becomes the OpenAPI `pattern`.
- **`default(T)` is a build error** (`VO0010`). Tests that deliberately construct one need a targeted
  `#pragma warning disable VO0010` with a comment.

### Hooks are interfaces

A value object declares a rule by implementing `IValueObjectNormalizer<T>`, `IValueObjectSpanNormalizer`,
`IValueObjectPatternValidator`, `IValueObjectValidator<T>`, `IValueObjectFormatter<T>` or
`IValueObjectStringFormatter<T>` (`src/AdCodicem.ValueObjects.Abstractions/ValueObjectHooks.cs`). The compiler
then checks the signature. The rules are public because a static abstract interface member cannot be anything
else; `Normalize` remains the member callers use, guarding null before deferring to `NormalizeValue`. `VO0011`
reports the one mistake left: a rule written without its interface. For a `static Regex Pattern` it reports a
public one only, and stays quiet on a type that implements another hook, which may already run it.

`IValueObjectPatternValidator` is the one hook whose member is half written by another generator: the consumer
declares `[GeneratedRegex(...)] public static partial Regex Pattern { get; }` and the framework's regex generator
supplies the body. It applies to string value objects only (`VO0023`), never to an `[EntityId]` (`VO0024`), and
replaces the deprecated `Pattern` option (`VO0021`); declaring both is `VO0022`, and the hook wins. The generator
reads the pattern text off the attribute for the schema, so `VO0025` warns on a `RegexOptions` that text cannot
carry, and `VO0026` on a missing `matchTimeoutMilliseconds`.

## Constraints that will bite you

These are all load-bearing, and each cost real debugging time:

- **Source generators never observe each other's output.** The `[JsonConverter]` this generator writes is
  invisible to the System.Text.Json generator, which is the entire reason `AdCodicem.ValueObjects.Json` exists:
  a hand-written `ValueObjectJsonConverterFactory` the STJ generator *can* see, named via
  `[JsonSourceGenerationOptions(Converters = ...)]`. The same constraint rules out `[GeneratedRegex]` in emitted
  code, which is why the pattern is now a hook the consumer writes: `IValueObjectPatternValidator` takes a
  `[GeneratedRegex]` partial property the regex generator *can* see. The `Pattern` option it replaces compiles a
  `Regex` at run time with `RegexOptions.Compiled`, which native AOT interprets; it is deprecated (`VO0021`) and
  goes at the next major. `docs/adr/0007-deprecate-pattern-for-a-source-generated-regex-hook.md` has the numbers.
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
  RS2008 fails the build. `VO0021` is the exception: it is the `DiagnosticId` of the `[Obsolete]` on `Pattern`,
  which the compiler reports, so no descriptor declares it and it has to be documented by hand. A test that
  exercises the deprecated option disables it on the spot, `#pragma warning disable VO0021` with a comment.
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
- **A pull request of more than 100 commits cannot be merged.** `main` takes a linear history and no merge
  commit, and a session's pull request lands with *Rebase and merge*, so that semantic-release reads each of its
  commits. GitHub rebases at most 100 commits
  ([its documented limit](https://docs.github.com/en/repositories/creating-and-managing-repositories/repository-limits#rebase-limits)).
  Past that, the API answers `rebaseable: false` beside `mergeable: true`, and the web UI blames conflicts that
  do not exist. *Squash and merge* still works, but it folds every `fix` and `feat` into one changelog line. Count
  with `git rev-list --count origin/main..HEAD` before opening a pull request, and split work past 100 commits into
  pull requests stacked on one another. CI runs only on pull requests that target `main`, so a stacked one gets
  its checks once the one beneath it has merged and it has been rebased onto `main`.
- **`website/package.json` carries `overrides`** for `qs`, `serialize-javascript` and `uuid`. All three are
  transitive under Docusaurus, which pins ranges too tight to pick up the patched versions on its own, so
  Dependabot alerts on them and `npm audit fix --force` "fixes" it by *downgrading* `@docusaurus/core` to
  3.5.2. Drop an override once Docusaurus widens the range that holds it back, not before, and re-run
  `npm audit` after touching them.

## Testing

Three suites, each with a distinct job:

- **UnitTests** — behaviour of generated code, using value objects defined in `Domain/`, and of every integration
  package called directly. `Domain/UnderlyingTypes.cs` declares one value object for each underlying type and each
  option or hook the rest of `Domain/` leaves out, and `GeneratedSurface/` runs every emitted member family on all
  of them, so nothing the generator emits only compiles. `EmitCompilerGeneratedFiles`
  is on, so generated sources land under `artifacts/obj/.../generated/` and can be read when diagnosing.
  `Domain/HandWritten/` holds value objects written by hand, the supported input that reaches what the generator
  always replaces: the interface defaults and the registry's reflection fallback. The test assembly cannot hold
  the rest, since the generator runs on it and its module initializer has run before any test does, so two
  fixture assemblies under `tests/Fixtures/` do: a generated value object in a module nothing has used yet, and
  annotated hand-written ones where no generator runs.
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
  is what catches unqualified names in emitted code. The harness also runs the framework's regex generator beside
  this one, so a snippet implementing `IValueObjectPatternValidator` compiles; the `CopyRegexGenerator` target in
  the test project copies it from the targeting pack the SDK resolved, so the SDK decides its version, on a laptop
  and in CI alike. The incrementality tests assert on
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
types; the unit tests use it on every generated value object of `Domain/` but two. `Floor` and `Celsius` cannot
satisfy it: their formatting hooks write text such as `floor 3` or `21 °C`, which does not parse back, and the kit
requires a text round trip. Add a contract with each value object added to `Domain/`.

**Coverage aims at 100 % of each pull request's patch, as Codecov counts it**
(`docs/adr/0006-coverage-is-a-signal-not-a-goal.md`). `codecov.yml` is the floor, not the aim: 95 % of the lines a
pull request changes, a partial line counting as missed, and the project's coverage dropping by half a point at
most, measuring `src/` only. Every member that is not private — public, internal, protected — is covered as far as
it can be: through a natural input where one reaches it (a declaration compiled through the generator, a call
through the public API, a request through ASP.NET Core, a database round trip), otherwise by a test that calls it
directly, with what no natural input can produce. A private member, or a member of a private nested type, which a
test could reach only by reflection, is not tested directly: its callers cover it, or it stays uncovered. A branch
the compiler adds that no input can take — the default arm of an exhaustive switch expression, a `?.` on a value
never null — stays partial rather than being rewritten. Code is removed for coverage only when no input can reach
it — conditions that contradict each other, a dead branch, a non-public member nothing calls —, never because no
test does. A defensive branch stays, covered or not. A public member nothing calls is a question for the
maintainer, not a removal.

Two blind spots, both deliberate. The collector instruments only the assemblies a test loads, so a package no test
loads is missing from the report rather than at 0 %: `.github/scripts/coverage-modules.sh` fails CI when a project
under `src/` is in no report. And the code the generator emits lives in the consumer's assembly — here the unit
test assembly, which is not measured — so Codecov sees the emitters, not what they produce; it is audited by hand
(see the ADR), not tracked.

Stack: xUnit v3 (`TestContext.Current.CancellationToken`), AwesomeAssertions, NSubstitute, Testcontainers.
Versions are centrally managed in `Directory.Packages.props`; versions live there, never in a `.csproj`.
