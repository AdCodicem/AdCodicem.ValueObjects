# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Twelve NuGet packages for single-value DDD value objects on .NET 10 and later. A `readonly partial struct` marked
`[ValueObject<T>]` gets its whole implementation from a Roslyn incremental generator, and crosses every boundary
as its underlying type: an IBAN is a JSON string, a `VARCHAR`, and a query-string parameter — never an object
wrapper. Consumers define their own value objects; this repository ships the frame.

Read `README.md` for the authoring surface and `benchmarks/README.md` for the measurements behind the design
decisions. The published documentation (`website/`) reorganises this same material into a narrative site — edit
the source of truth first (README, this file, the benchmark numbers), then the corresponding page under
`website/docs/`. The site is versioned: `website/docs/` is the preview and describes `main`, while
`website/versioned_docs/` holds what each stable line was released with (see Releases below). A change to
`website/docs/` therefore reaches the preview pages at the next `preview.yml` run, and the stable pages at the next
release, not before.

`skills/value-objects/` is the consumer-facing agent skill, distributed as a Claude Code plugin through
`.claude-plugin/`. It is prescriptive only — the authoring surface, the hooks, the wiring, the diagnostics — and
deliberately carries no rationale or benchmark numbers, so it stays a short file rather than a fourth copy of
the documentation. Anything that changes the surface a consumer writes (an option on `[ValueObject<T>]`, a hook
interface, a diagnostic, an extension method) must be reflected there too.

## Commands

```bash
dotnet build -c Release
dotnet test -c Release                                    # all four suites
# One suite. --project is required: given a bare directory, dotnet test prints a hint and exits 0
# without running anything, which reads exactly like a pass.
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests        # behaviour of generated code
dotnet test --project tests/AdCodicem.ValueObjects.GeneratorTests   # the generator itself
dotnet test --project tests/AdCodicem.ValueObjects.IntegrationTests # needs Docker
dotnet test --project tests/AdCodicem.ValueObjects.RdgTests         # minimal APIs through the RDG

# Formatting, as CI checks it. Not plain `dotnet format`: its workspace runs the source generators
# it can load, but CI formats before it builds, and the generator is referenced as a project whose
# assembly does not exist yet. So `analyzers` reports ASP0020 against every minimal API endpoint
# binding a value object, and compile errors besides, for members the generator does emit. The
# Request Delegate Generator has that blind spot in every build, for a value object of its own
# project (VO0033).
dotnet format AdCodicem.ValueObjects.slnx whitespace --verify-no-changes
dotnet format AdCodicem.ValueObjects.slnx style --verify-no-changes
# The packable projects under src/, as CI packs them
dotnet pack src/AdCodicem.ValueObjects.Packages.slnf -c Release -o artifacts/packages

# .NET 11 compatibility island: its own global.json (SDK 11 RC), outside the solution, run from its folder.
# Pack at a fresh version: NuGet reuses whatever ~/.nuget/packages already holds at a version it has seen.
v=0.0.0-compat.$(date +%s)
MINVERVERSIONOVERRIDE=$v dotnet pack src/AdCodicem.ValueObjects.Packages.slnf -c Release -o artifacts/packages
cd tests/Compat && dotnet test --project AdCodicem.ValueObjects.CompatTests.csproj -p:AdCodicemVersion=$v
# Needs Docker for PostgreSQL and SQL Server; without it, add --filter-not-trait "Requires=Docker"

# Native AOT, as ci.yml's native AOT job runs it; needs clang and zlib. The application of tests/NativeAot under
# the JIT and as a native binary, compared; then the compiled model written for native AOT, published (Docker-free).
.github/scripts/native-aot.sh
.github/scripts/compiled-model.sh aot
# The compiled model as the build job checks it: dotnet ef dbcontext optimize, a build on it, a round trip (Docker)
.github/scripts/compiled-model.sh jit

# Workflows, as lint.yml checks them (actionlint 1.7.12, with shellcheck on PATH; reads .github/actionlint.yaml)
actionlint

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

Two tracks, and nothing you merge publishes anything by itself.

A **preview** is published by `preview.yml`, every Monday at 07:15 Paris time and whenever it is dispatched from
`main`, and only when a package input changed since the version nuget.org has from the nearest commit:
`.github/scripts/preview-gate.sh` decides `publish`, `repair` or `none`, and fails the run rather than guess when a
lookup fails. All twelve packages go out at one version, or none. That version is the one semantic-release would give
the next stable release, computed without a token by `.github/scripts/next-version.mjs`, which runs
semantic-release's own commit analyzer with `.releaserc.json`, suffixed `-preview.<commits since the last stable
tag>`: `0.3.0-preview.172` leads to `0.3.0`, and with no commit that releases anything the version is the next patch.
The four suites run on that exact commit first, while a job of its own packs it, `src/` only. Before logging in,
the publish job checks the set again (`verify-packages.sh`) and rechecks nuget.org (`preview-gate.sh --recheck`), so
re-running an old run cannot publish a stale version. `push-packages.sh` pushes dependencies first and only what
nuget.org lacks, in up to three passes, so a push that stops midway is completed at the same version by those
passes, by **Re-run failed jobs** or by the next run (`repair`); then it waits until nuget.org lists every package.
MinVer receives the version through `MINVERVERSIONOVERRIDE`. An empty override silently falls back to MinVer's own
version, which `verify-packages.sh` refuses. The reasoning, and what stays open, is in
`docs/adr/0009-publish-previews-weekly-when-a-package-input-changed.md`.

A **stable** release is a manual `workflow_dispatch` on `release.yml`. Its tests run in a job of their own, and a
**was it previewed** job (`preview-gate.sh --report`) warns, without ever blocking, when the release ships package
inputs no version on nuget.org carries: dispatch `preview.yml` first, and start the release once that run is green.
A **pack** job, which holds no credential, computes the version with `next-version.mjs`, packs `src/` through
`release-pack.sh` and freezes the documentation through `docs-snapshot.sh`. Once the `nuget-stable` reviewer
approves, the release job checks those files against the digests the pack job output, and semantic-release computes
the version from the Conventional Commits, refuses to go on unless it is the one packed, writes `CHANGELOG.md`,
commits it with the snapshot, tags, pushes through `push-packages.sh`, and the site is redeployed. No build code
runs beside the NuGet key, the App token or the OIDC token. The bridge is `MINVERVERSIONOVERRIDE` again: the pack
job hands MinVer the stable version and MinVer steps aside. Both read the same `v*` tags.

The GitHub Release links every package to its version on nuget.org. That list is never written down: the pack job
evaluates the packable projects under `src/` into `RELEASE_PACKAGE_IDS` with `.github/scripts/package-ids.sh`, and
semantic-release starts with it (the release body is rendered from that starting environment, so a prepare step
cannot feed it); `release-pack.sh` fails the pack job, before anything is pushed, if the packages it built differ
from that list.
The **attest provenance** job then signs those packages, and every assembly inside them, with a Sigstore SLSA
provenance attestation and attaches the bundle to the release. nuget.org re-signs each `.nupkg`, so the copy it
serves no longer has the attested digest, but it leaves the files inside as they were packed: an assembly restored
from nuget.org verifies (`SECURITY.md`). `preview.yml` attests the same subjects, before its push. Releases are
immutable on this repository — a published release takes no new asset — so semantic-release creates the GitHub
Release as a draft (`draftRelease`) and that job publishes it once the bundle is attached. The draft's URL dies when
it is published, which is why `.releaserc.json` overrides `successComment` to link to the tag instead.

A commit type that triggers no release (`chore`, `ci`, `test`) contributes nothing to the next version. The
reasoning for the stable track is in `docs/adr/0003-hybrid-release-manual-stable-continuous-preview.md`.
`build(pack)` and `docs(readme)` are the exceptions among the types that otherwise release nothing:
`.releaserc.json` rates them a patch, because the package metadata and the README ship inside every `.nupkg` — the
README is its nuget.org page — so a change to either reaches users only through a release. Scope the commit
accordingly, or the change waits for the next `feat` or `fix`. While the major is 0, a breaking change (`!`, or a
`BREAKING CHANGE:` footer) releases a minor: the first of the `releaseRules` is
`{ "breaking": true, "release": "minor" }`. At 1.0 that rule is replaced by `{ "breaking": true, "release": "major" }`,
not deleted, or `build(pack)!` and `docs(readme)!` would fall back to a patch. So before 1.0.0, a minor may break,
deprecate or remove public API, and the documentation says so (README's Versioning section): a deprecation reads
"any minor version may remove it before 1.0.0", never "removed in the next major version". Only from 1.0.0 on does
a breaking change wait for a major. The twelve packages share one version, never aligned with .NET's or EF Core's, and a
framework's next major is supported in the same packages:
`docs/adr/0010-version-every-package-in-lockstep-independently-of-dotnet.md`.

The documentation follows the same two tracks (`docs/adr/0005-version-the-documentation-site.md`, amended by
ADR-0009). Every `preview.yml` run that decides, whether it publishes or not, redeploys the site with
`website/docs/` as the preview under `/docs/preview/`, labelled with the version on nuget.org that describes the
commit it builds; a run in which a job failed deploys nothing. `deploy-docs.yml` is called only, never dispatched, so
the site is redeployed by hand by dispatching `preview.yml`. `ci.yml`'s **documentation site** job builds the site
on every pull request without deploying it, through `.github/actions/build-site`, the composite action
`deploy-docs.yml` builds with, so a broken link fails there. A stable release freezes `website/docs/` — generated API reference
included — into `website/versioned_docs/` through `.github/scripts/docs-snapshot.sh`, which `release.yml`'s pack
job runs and semantic-release commits with the changelog. There is one entry per line, `0.<minor>.x` before 1.0 and
`<major>.x` after, replaced wholesale when the line ships again. Never add or remove an entry by hand. Editing a
released page is allowed only to correct an error that misleads users of that release, and the same fix must land
in `website/docs/`, or the next snapshot of the line discards it.

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
available without the consumer registering anything. Each descriptor carries the generated JSON converter, which
`ValueObjectJsonConverterFactory` hands to a source-generated serializer context, whatever the declaring assembly
references.

Two shapes cannot be registered from that class by a closed type. A `private` or `protected` value object is
reachable only from the type declaring it, so the registration calls a step nested in the type declaring the
outermost private or protected type, each step calls the next, and the last, nested in the type declaring the most
deeply nested one, registers it (`ValueObjectModel.RegistrationRoute`). Each step is a static class of its own, named
after its position on the route: a member of the author's type would run its static constructor from the module
initializer, and one name for every position would be CS0108 where a type of the route derives from another.
`RegisterGenericDefinition` reads nothing off the definition but its kind, since native AOT keeps no interface list
for a generic type definition and it runs in the module initializer. A generic value object, or one nested in a generic type, has no
construction the registration knows: it registers the generic definition (`RegisterGenericDefinition`), and
`ValueObjectRegistry.TryResolve` describes each construction from its generated `Schema` and `ValueJsonConverter`,
by reflection, the first time it is asked for it. The EF Core convention maps the definition and closes the converter
per property; Dapper needs `AddValueObjectHandler<TSelf, TValue>()` per construction. Only `file`-local types, a
private or protected type inside a generic one, and a generic `[EntityId]` stay refused (`VO0019`).

### Two ways to reach a value object — and why bugs hide in one of them

- **Typed path.** The static abstract members of `IValueObject<TSelf, TValue>` (`Create`, `TryCreate`,
  `TryParse`, `Normalize`, `Validate`, and `Schema`, the rules as data). This is what domain code and the generic
  integrations use — the ASP.NET binder, the EF converter and the Dapper handler are all generic and closed over the
  concrete types at startup, so per-request work is fully typed and allocates nothing extra. A typed adapter reads
  `TSelf.Schema`, never the registry, which would describe a construction of a generic value object by reflection.
- **Boxed path.** `ValueObjectDescriptor`, resolved from `ValueObjectRegistry`, for callers that only know a
  `Type` at run time — `MustParseAs(Type)`, the OpenAPI transformer, model-binder resolution. `descriptor.Accept`
  hands an `IValueObjectVisitor<TResult>` the type arguments back, so an integration closes its adapter at compile
  time rather than with `MakeGenericType`, which native AOT cannot run for a struct. Dapper's `AddValueObjectHandlers`,
  the MVC binder provider, the JSON factory's general-purpose converter, for a value object written by hand, and EF
  Core's `ConfigureValueObjects` do. That convention still closes the converter of a `TSelf?` property with
  `MakeGenericType`, over the visitor's own type arguments: C# names it only under `TValue : struct` or
  `TSelf : IValueObject<TSelf, string>`, which `Visit` cannot prove, and it must stay the type a compiled model names.
  The identifiers' `ConfigureEntityIds` still calls `MakeGenericType` on a `Type`, and migrates last. Neither EF Core
  convention runs under native AOT, where EF Core reads the compiled model and builds none. A hand-written value object
  declares `Schema` too, and the registry describes it from that alone: an annotation on it is read by nothing at run
  time.

The unit tests exercise the typed path, so a defect confined to the descriptor is invisible to them. That is
exactly how the descriptor once flattened every rejection into a generic `not_parsable`, discarding the rule
that actually fired. `DescriptorTests.cs` exists to cover that surface; extend it when touching the descriptor.

### Invariants worth knowing before changing anything

- **Normalize, then validate, then assign**, so a non-default instance is by construction normalized and valid.
  The exceptions are the EF Core and Dapper read paths, which use `CreateUnchecked` because they read values this
  same application already validated. `ConfigureValueObjects(strict: true)` turns validation back on for EF Core;
  Dapper validates only a column the value object cannot have written: text read into a value object over another
  type, or a number or a `Guid` read into one over `string`.
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
  compile time, becomes the OpenAPI `pattern`. The OpenAPI transformer and the JSON Schema transform
  (`ValueObjectJsonSchema`, in the Json package) take what a rule becomes in a schema from
  `src/Shared/ValueObjectSchemaKeywords.cs`, an internal file each package links and compiles, not a project: a
  keyword changes there, for both.
- **`default(T)` is a build error** (`VO0010`). Tests that deliberately construct one need a targeted
  `#pragma warning disable VO0010` with a comment.
- **A known value is the author's member, created before the lookup it belongs to.** `[KnownValue]` marks a
  `static readonly` field or a get-only auto-property initialized through the generated, private `Known`, which
  applies every rule but membership. The author's part of a type initializes before the generated part (Roslyn orders
  the trees), so a closed set's `FrozenSet` is built from the members after they exist, and `Validate` throws
  `InvalidOperationException`, with the reason, when a static initializer reaches it before then through `Create`.
  `VO0036` reports a member the generator cannot read as a known value, `VO0037` a call to `Known` anywhere else.
  The rules a value is checked against at compile time (`VO0031`) are read off constants only: `Known("FR")`, an
  `Example` getter returning `Create(42)`, a bound returned as a constant. An attribute option written as text is no
  longer read anywhere (`docs/adr/0011-declare-known-values-and-examples-as-typed-members.md`).

### Hooks are interfaces

A value object declares a rule by implementing `IValueObjectNormalizer<T>`, `IValueObjectSpanNormalizer`,
`IValueObjectPatternValidator`, `IValueObjectValidator<T>`, `IValueObjectMinimum<T>`, `IValueObjectMaximum<T>`,
`IValueObjectFormatter<T>`, `IValueObjectStringFormatter<T>` or `IValueObjectExample<TSelf>`
(`src/AdCodicem.ValueObjects.Abstractions/ValueObjectHooks.cs`). The compiler
then checks the signature. The rules are public because a static abstract interface member cannot be anything
else; `Normalize` remains the member callers use, guarding null before deferring to `NormalizeValue`. `VO0011`
reports the one mistake left: a rule written without its interface. For a `static Regex Pattern` it reports a
public one only, and stays quiet on a type that implements another hook, which may already run it.

`IValueObjectPatternValidator` is the one hook whose member is half written by another generator: the consumer
declares `[GeneratedRegex(...)] public static partial Regex Pattern { get; }` and the framework's regex generator
supplies the body. It applies to string value objects only (`VO0023`), never to an `[EntityId]` (`VO0024`), and
replaces the `Pattern` option, now a compile error read by nothing (`VO0021`). The generator
reads the pattern text off the attribute for the schema, so `VO0025` warns on a `RegexOptions` that text cannot
carry, and `VO0026` on a missing `matchTimeoutMilliseconds`.

`IValueObjectMinimum<T>` and `IValueObjectMaximum<T>` replace the `Minimum` and `Maximum` options, which held the
bound as text and are now a compile error read by nothing (`VO0028`). The generated code reads a bound through `ValueObjectBound`, a bridge that reaches it however the
type implements it and that keeps nothing: a copy taken while the type initializes would keep the default for good.
The schema publishes it through `ValueObjectBound.Text`, which the schema transformer re-writes in the converter's
form. A hook over a type that takes no bound, or over another type than the underlying one, is `VO0030`. `VO0011`
reports a public static `Minimum` or `Maximum` property of the underlying type without its interface, and stays
quiet on a field, which could not implement it, on a type that implements `IValueObjectValidator<T>` or
`IValueObjectNormalizer<T>`, which may already check it or clamp to it.
`docs/adr/0008-deprecate-text-bounds-for-typed-bound-hooks.md` records why a bound is a constant that nothing caches.

`IValueObjectExample<TSelf>` declares the OpenAPI example as an instance of the type, which the schema reads through
`ValueObjectExample.Of<TSelf>()`, a bridge that reaches an explicit implementation too, and publishes as its
underlying value: `ValueObjectSchema.Example` is an `object?`, written by `ValueObjectSchemaKeywords.WriteExample`.
It replaces the `Example` options of `[ValueObject<T>]` and `[EntityId]`, now a compile error (`VO0035`). A hook over
another type is `VO0038`, and a public static `Example` property of the type without the interface is `VO0011`.

## Constraints that will bite you

These are all load-bearing, and each cost real debugging time:

- **An attribute cannot name a type through a type parameter**, and neither System.Text.Json nor `TypeDescriptor`
  closes an open generic converter over the type it converts (STJ throws on `typeof(Code<>.ValueJsonConverter)`).
  A generic value object's `[JsonConverter]` and `[TypeConverter]` therefore name `GenericValueObjectJsonConverterFactory`
  and `GenericValueObjectTypeConverter`, in the contracts, which reach the construction through its descriptor: a
  construction registered by hand, as native AOT asks, needs no reflection. The generated code also names the types around a value object through their type parameters from
  inside it, which is why a nested type or type parameter of the same name in between is `VO0019`.
- **Source generators never observe each other's output.** The `[JsonConverter]` this generator writes is
  invisible to the System.Text.Json generator, which is the entire reason `AdCodicem.ValueObjects.Json` exists:
  a hand-written `ValueObjectJsonConverterFactory` the STJ generator *can* see, named via
  `[JsonSourceGenerationOptions(Converters = ...)]`. The same constraint rules out `[GeneratedRegex]` in emitted
  code, which is why the pattern is now a hook the consumer writes: `IValueObjectPatternValidator` takes a
  `[GeneratedRegex]` partial property the regex generator *can* see. The `Pattern` option it replaces compiled a
  `Regex` at run time with `RegexOptions.Compiled`, which native AOT interprets; it is now a compile error (`VO0021`)
  that any minor may remove. `docs/adr/0007-deprecate-pattern-for-a-source-generated-regex-hook.md` has the numbers.
  The Request Delegate Generator, on in every build under `PublishAot` or `PublishTrimmed`, is the case a consumer
  meets: it binds a value object of its own project from the request body unless the declaration lists its
  contract, which `VO0033` reports. That analyzer reads `EnableRequestDelegateGenerator` through the
  `CompilerVisibleProperty` the package's `build/AdCodicem.ValueObjects.props` adds, which a project here, referencing
  the generator by project, has to list itself.
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
  RS2008 fails the build. `VO0021`, `VO0028`, `VO0034` and `VO0035` are the exceptions: they are the `DiagnosticId`
  of an `[Obsolete(error: true)]`, on `Pattern`, on `Minimum` and `Maximum`, on the constructor of `[KnownValue]`
  that takes a name and a value (which `KnownValueMemberCodeFixProvider` rewrites) and on the `Example` options,
  which the compiler reports, so no descriptor declares them and they have to be documented by hand. No `#pragma`
  silences such an error, and the compiler does not report it inside a member or a type itself `[Obsolete]`: a test
  that exercises one compiles it in a generator test and reads it among the compilation's diagnostics. Retired
  identifiers (`VO0004`, `VO0006`, `VO0013`, `VO0014`, `VO0022`, `VO0029`) are never reused.
- **Every action in `.github/workflows` and `.github/actions` is pinned to a commit SHA**, with the release as a
  same-line comment (`uses: actions/checkout@3d3c42e... # v7.0.1`). Dependabot reads that comment to derive the
  semver bump, so a pin without one falls out of the `actions` group and may auto-merge as a non-major. Its
  `github-actions` entry lists `/.github/actions/*` beside `/`, since it does not look into a composite action's
  folder on its own. Three of the eighteen actions publish *annotated* tags — `codecov/codecov-action`,
  `ossf/scorecard-action`, `github/codeql-action` — so re-pinning by hand needs
  `git ls-remote <repo> 'refs/tags/vX.Y.Z^{}'`: without the `^{}` you get the tag object's SHA, which GitHub
  refuses to resolve. The calls from `preview.yml` and `release.yml` to
  `./.github/workflows/deploy-docs.yml` target a local reusable workflow, and a `./.github/actions/` path a local
  action: both must stay unpinned, since GitHub rejects `@ref` on either.
- **actionlint is a downloaded binary, not an action.** `lint.yml`'s `workflows` job fetches the release named by
  `ACTIONLINT_VERSION` and checks it against `ACTIONLINT_SHA256` before running it, so Dependabot never updates it:
  bump both together, taking the digest from the release's checksums file. `.github/actionlint.yaml` silences the
  one key actionlint does not know yet, `queue: max` in `deploy-docs.yml` and `preview.yml`.
- **Trusted Publishing is keyed on a workflow file and an environment.** The nuget.org policies name `preview.yml`
  with the `nuget` environment and `release.yml` with `nuget-stable`. Renaming either file, or moving its publish job
  to another environment, breaks the OIDC exchange: change the policy on nuget.org first.
- **Required checks are job names.** The `Default` ruleset on `main` requires `build and test` and `native AOT`
  (`ci.yml`), and `workflows` (`lint.yml`). Renaming any of them leaves every pull request waiting for a check that
  never reports. `compat (.NET 11)` becomes required when .NET 11 ships, not before.
- **`src/AdCodicem.ValueObjects.Packages.slnf` must list every project under `src/`.** A solution filter cannot
  glob, and previews and releases pack through it: a project left out is a package that never ships. `ci.yml` packs
  through it and fails when a packable project is missing from the result. It sits under `src/`, not beside the
  `.slnx`: `dotnet format` and `dotnet sln` refuse to pick between two solution files in one folder.
- **`preview-gate.sh` holds the list of package inputs.** It covers `src/`, the solution filter included and the
  analyzer release-tracking files left out, `README.md`, `icon.png`, the root `Directory.Build.*` files,
  `global.json`, `nuget.config`, `.gitattributes` and `release-pack.sh`. It also covers `Directory.Packages.props`:
  any change outside its `<PackageVersion>` elements, and the `<PackageVersion>` of any package in the restore graph
  of the projects under `src/`, build-time ones included (MinVer, SourceLink, PolySharp, Roslyn and its analyzers).
  That is a wider set than `.github/shipped-dependencies`, so a `chore(deps)` bump of MinVer still publishes a
  preview. Anything else that changes the bytes or the metadata of a package has to be added to its `inputs`, or a
  change to it never reaches a preview.
- **`.github/shipped-dependencies` must match the packed nuspecs.** `dependabot-auto-merge.yml` retitles a Dependabot
  pull request `fix(deps):`, or `fix(deps)!:` for a major, when it updates a dependency the list names, and a step of
  `ci.yml` (`shipped-dependencies-check.sh`) fails when a nuspec names a dependency the list lacks, or a line matches
  nothing. A dependency added to a shipped package goes into the list in the same pull request. Dependabot pull
  requests are squash-merged, so that the title becomes the commit semantic-release reads; a rebase merge keeps
  Dependabot's `chore(deps)` commit and releases nothing. One case needs a hand: a Dependabot security update that
  pins a transitive dependency the list does not name yet. It opens as `chore(deps)` with auto-merge queued, and
  `build and test` fails on the list; the workflow reads the list from the base branch and ignores your events, so it
  cannot retitle it. Retitle the pull request `fix(deps): …` (`fix(deps)!: …` across a major) first, then push the
  list line to its branch; the auto-merge still queued squashes it under your title. Never merge the line in a pull
  request of its own: nothing on `main` ships that ID yet, so the check fails there too.
- **The compatibility island is invisible to the root tooling.** `tests/Compat` has its own `global.json`, naming
  the .NET 11 release candidate's SDK, which CI installs exactly and a later 11.0 SDK may run locally
  (`rollForward: latestFeature`), and its own `Directory.Build.props`, `Directory.Packages.props` and
  `nuget.config`. It is in no solution, so the root build, `dotnet format`, coverage and Dependabot never see it, and
  it must be run from its folder: from the root, the root `global.json` selects SDK 10 and the build stops. Its SDK
  and its .NET 11 packages are bumped by hand, Npgsql's provider in the same change as EF Core, which it pins exactly.
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

Four suites, each with a distinct job:

- **UnitTests** — behaviour of generated code, using value objects defined in `Domain/`, and of every integration
  package called directly. `Domain/UnderlyingTypes.cs` declares one value object for each underlying type and each
  option or hook the rest of `Domain/` leaves out, and `GeneratedSurface/` runs every emitted member family on all
  of them, so nothing the generator emits only compiles. `EmitCompilerGeneratedFiles`
  is on, so generated sources land under `artifacts/obj/.../generated/` and can be read when diagnosing.
  `Domain/HandWritten/` holds value objects written by hand, the supported input that reaches what the generator
  always replaces: the interface defaults and the registry's reflection fallback. The test assembly cannot hold
  the rest, since the generator runs on it and its module initializer has run before any test does, so three
  fixture assemblies under `tests/Fixtures/` do: a generated value object in a module nothing has used yet,
  annotated hand-written ones where no generator runs, and generated ones in an assembly that does not reference
  `AdCodicem.ValueObjects.Json`, as a domain project serializing through an API's context does not.
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
- **RdgTests** — minimal API endpoints whose binding the Request Delegate Generator writes, over value objects
  declared in the endpoints' own project, which list their contract (VO0033). The project imports the package's
  `build/AdCodicem.ValueObjects.props`, so VO0033 fails its build for a value object that drops its contract, and a
  test reads the RDG's output under the generated files, so that a change in the SDK's defaults cannot turn it into a
  test of the reflection-based binding. The RDG writes one interceptor for two handlers whose parameters share types
  and names, dropping the attribute of the second, a `[FromHeader]` included: name such parameters apart.

Beside them, in the solution but no suite, `tests/NativeAot` holds applications built as consumers build them, which
CI publishes rather than tests. None imports `tests/Directory.Build.props`, so the trimming and AOT analyzers stay
on where they apply.

- `AdCodicem.ValueObjects.NativeAot` is a minimal API referencing every package that claims to be AOT-compatible, with
  its value objects in `AdCodicem.ValueObjects.NativeAot.Domain`, which links the unit suite's
  `Domain/UnderlyingTypes.cs`. The application links two value objects written by hand from `Domain/HandWritten/`, one
  over `Uri`, and registers them without a converter, so the JSON factory's general-purpose converter runs natively.
  `ci.yml`'s `native AOT` job (`.github/scripts/native-aot.sh`), a required check, runs its fixed script once under the
  JIT and once as the native binary, and fails on a trimming or AOT warning or on any difference between the two
  outputs. The JIT run turns on the RDG and turns off reflection-based serialization, as `PublishAot` does, so that the
  outputs differ only where native AOT changes something. A value object added to `UnderlyingTypes.cs`, or registered by
  the application, goes into its `AppJsonContext` too, with its underlying type, or the script reports it missing; an
  underlying type no other value object has, as `Uri`, also needs its texts in `Underlying.cs`, or the script probes it
  with none. `PublishAot` is set by the project when `NativeAot` is true, never as `-p:PublishAot`, which would reach
  the `netstandard2.0` generator. A `#pragma` silences only the analyzers that run with the compiler: the AOT compiler
  reads the compiled code, so a warning the library suppresses takes `[UnconditionalSuppressMessage]` and a guard, as
  `MustParseAs` found out.
- `AdCodicem.ValueObjects.CompiledModel` holds a context mapping every value object the EF Core convention maps,
  required and optional, a generic one and an `[EntityId]` key, and a strict context beside it.
  `.github/scripts/compiled-model.sh` writes their model with `dotnet ef dbcontext optimize` under
  `artifacts/compiled-model/`, never committed, and builds on it with `CompiledModel=jit` or `aot`. The build job
  takes the `jit` one on a round trip through SQL Server; the native AOT job publishes the `aot` one, written with its
  queries precompiled, never runs it (two bugs of EF Core stop its queries), and fails on a warning about this
  library's code only, EF Core and its dependencies warning on their own. The native binary holds the converters and
  comparers the model names, never the conventions: EF Core builds no model under native AOT, so the AOT compiler drops
  them, and only `optimize`, under the JIT, runs them, which is why a change to a convention shows in the model it
  writes, never in a warning of the publish. `dotnet-ef` is pinned in
  `.config/dotnet-tools.json` to the version of `Microsoft.EntityFrameworkCore.Design`, EF Core's own: Dependabot's
  `dotnet` group bumps them together, and holds back the tool's next major as it does EF Core's. The project
  turns transitive pinning off: Design depends on a later Roslyn than the one `Directory.Packages.props` pins for the
  generator. EF Core 10 constrains what its query precompilation accepts, and Program.cs is written around it: each
  query on a context held in a local and over locals (a context passed as a parameter is a "dynamic" query, a method
  parameter inside the query throws), tracked (it writes an untracked query that does not compile), on an entity type
  that is not sealed, in a model with no type named as one of EF Core's internal ones, `Reference<T>` among them
  (CS0104). A strict context cannot track on a compiled model, so the `jit` round trip reads untracked
  (`UNTRACKED_READS`); the EF Core guide says why.

Beside them, outside the solution, `tests/Compat` is the **compatibility island**: the twelve packages exactly as
packed, installed from `artifacts/packages` at the one version just built into a `net11.0` application on the .NET 11
release candidate. It runs the generator in that SDK's compiler, Entity Framework Core 11 on SQLite, SQL Server and
PostgreSQL 17, System.Text.Json source generation, ASP.NET Core model binding, `Microsoft.AspNetCore.OpenApi` 11 over
`Microsoft.OpenApi` 3, Dapper, FluentValidation, Newtonsoft.Json and the contract kit, with no transitive pinning, so
the dependency floors of the packages meet the next major as an application's would. `ci.yml`'s `compat (.NET 11)`
job runs it against the packages its build job packed; it is not a required check until .NET 11 ships, and is
measured by no coverage. Without Docker its 18 container tests fail rather than skip, so leave them out explicitly
(Commands above). Tools that walk the repository rather than the solution do see it: CodeQL downloads its SDK, and
GitHub's automatic dependency submission restores it with SDK 10 from the root, which is why its project file leaves
itself empty on an SDK that cannot target `net11.0`. The suites above are still the four; the island checks the
packages, not the code.

`AdCodicem.ValueObjects.Testing` ships a contract kit (`ValueObjectContract`) that consumers point at their own
types; the unit tests use it on every generated value object of `Domain/` but four. `Floor` and `Celsius` cannot
satisfy it: their formatting hooks write text such as `floor 3` or `21 °C`, which does not parse back, and the kit
requires a text round trip. `Strongbox.Secret` and `Archive.Shelf` are private, and no public contract class can
name them; `DeclarationContextTests` covers them instead, with the protected `StrongboxId`. Add a contract with each
value object added to `Domain/`.

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
`tests/Compat`, outside the solution, is the one exception: it has a `Directory.Packages.props` of its own.
