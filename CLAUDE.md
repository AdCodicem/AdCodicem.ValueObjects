# CLAUDE.md

Guidance for Claude Code (claude.ai/code) in this repository. This file holds what every change needs; the traps of
one package or one suite live in a `CLAUDE.md` of its own, next to the code (see [Where the rest lives](#where-the-rest-lives)).

## What this is

- Thirty-five NuGet packages for single-value DDD value objects on .NET 10 and later. A `readonly partial struct`
  marked `[ValueObject<T>]` gets its whole implementation from a Roslyn incremental generator, and crosses every
  boundary as its underlying type: an IBAN is a JSON string, a `VARCHAR`, a query-string parameter, never a wrapper
  object. Consumers write the value objects; this repository ships the frame.
- `README.md` is the authoring surface, `benchmarks/README.md` the measurements behind the design, `docs/adr/` the
  decisions, `docs/maintaining.md` the maintainer runbook (previews, releases, settings, Dependabot), `website/` the
  published documentation, which reorganises the same material. Edit the source of truth first (README, a `CLAUDE.md`,
  the benchmark numbers), then the matching page under `website/docs/` (`website/CLAUDE.md`).
- `skills/value-objects/` is the consumer agent skill, shipped as a Claude Code plugin through `.claude-plugin/`. It is
  prescriptive only, with no rationale and no numbers. A change to what a consumer writes (an option of
  `[ValueObject<T>]`, a hook interface, a diagnostic, an error code, an extension method) updates it in the same change;
  `SkillCoverageTests` catches a missing option, hook, diagnostic or error code, nothing else.
- `TreatWarningsAsErrors` is on repository-wide: any warning fails the build.

## Where the rest lives

Claude Code loads a nested `CLAUDE.md` when it reads a file under its folder.

- `src/<Package>/CLAUDE.md` for every package with traps of its own: Abstractions, Generators (generator, analyzers),
  AspNetCore, AspNetCore.Http, AspNetCore.NewtonsoftJson, NewtonsoftJson, EntityFrameworkCore, Dapper, MongoDB,
  Identifiers.MongoDB, MessagePack, ProtobufNet, Grpc, Orleans, Serilog, AI, ModelContextProtocol, AzureFunctions,
  AzureFunctions.AspNetCore, HotChocolate, HotChocolate.Data, Blazor, CommandLine, NJsonSchema, Swashbuckle,
  AutoFixture, Bogus, FsCheck; `src/Shared/CLAUDE.md` for the source files several packages link.
- `tests/<Suite>/CLAUDE.md` for each of the four suites, plus `tests/NativeAot/`, `tests/Compat/`, `tests/Fixtures/`.
- `.github/CLAUDE.md` for workflows, scripts, Dependabot and release mechanics; `website/CLAUDE.md` for the site.
- Record a new trap in the nearest of these files; at the root only when it crosses packages. One rule per bullet: the
  fact or the instruction, then why in one clause, with exact identifiers. Where a document already explains it, keep
  the one-line rule and point to the document.
- `src/**/CLAUDE.md` is excluded from `preview-gate.sh`'s package inputs, so editing one publishes no preview.

## Commands

```bash
dotnet build -c Release
dotnet test -c Release                                              # all four suites
# One suite. --project is required: on a bare directory dotnet test prints a hint and exits 0, which reads as a pass.
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests
dotnet test --project tests/AdCodicem.ValueObjects.GeneratorTests
dotnet test --project tests/AdCodicem.ValueObjects.IntegrationTests # needs Docker
dotnet test --project tests/AdCodicem.ValueObjects.RdgTests
# One test: xunit.v3 on Microsoft Testing Platform; wildcards, not substrings
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests --filter-method "*The_name_of_the_test*"

# Formatting, as CI runs it, before the build. Never plain `dotnet format`: its `analyzers` step runs without the
# generator's assembly and reports ASP0020 and compile errors for members the generator does emit.
dotnet format AdCodicem.ValueObjects.slnx whitespace --verify-no-changes
dotnet format AdCodicem.ValueObjects.slnx style --verify-no-changes

# Pack as CI does: through the solution filter, never the whole solution
dotnet pack src/AdCodicem.ValueObjects.Packages.slnf -c Release -o artifacts/packages

actionlint                                   # as lint.yml's `workflows` job: 1.7.12, shellcheck on PATH
node .github/scripts/ecmascript-patterns.mjs # Blazor's pattern table in Node's v-flag engine (`documentation site` job)

# Benchmarks: a quiet machine; absolute timings are not comparable across runs
cd benchmarks/AdCodicem.ValueObjects.Benchmarks && dotnet run -c Release -- --filter '*WrapperCost*'
```

Native AOT and the compiled model: `tests/NativeAot/CLAUDE.md`. The .NET 11 compatibility island:
`tests/Compat/CLAUDE.md`. The documentation site: `website/CLAUDE.md`.

## Releases

- Nothing you merge publishes. A preview comes from `preview.yml` (Mondays, or dispatched from `main`) when a package
  input changed; a stable release from a manual `release.yml` dispatch the `nuget-stable` reviewer approves.
  Mechanics: `.github/CLAUDE.md`; runbook: `docs/maintaining.md`; reasoning: ADR 0003 and 0009.
- The commit type sets the next version (Conventional Commits, `.releaserc.json`): `feat` a minor, `fix` and `perf` a
  patch; `chore`, `ci`, `test`, `docs`, `refactor` nothing, except `build(pack)` and `docs(readme)`, rated a patch
  because the package metadata and the README (the nuget.org page) ship inside every `.nupkg`. Scope such a commit
  that way, or it waits for the next `feat` or `fix`.
- While the major is 0, a breaking change (`!`, or a `BREAKING CHANGE:` footer) releases a minor: a minor may break,
  deprecate or remove public API, and a deprecation reads "any minor version may remove it before 1.0.0", never "the
  next major version". At 1.0, replace the first `releaseRules` entry with `{ "breaking": true, "release": "major" }`,
  never delete it, or `build(pack)!` and `docs(readme)!` fall back to a patch.
- The thirty-five packages share one version, never aligned with .NET's or EF Core's; a framework's next major is
  supported in the same packages (ADR 0010).

## Architecture

### The generator

- Pipeline: `ValueObjectGenerator` (`ForAttributeWithMetadataName`) → `ValueObjectModel` → `ValueObjectEmitter`, which
  delegates to `JsonConverterEmitter`, `TypeConverterEmitter`, `XmlSerializableEmitter` and `RegistrationEmitter`.
- `ValueObjectModel` is an equatable record of primitives and `EquatableArray`s: keep it so, or every unrelated edit
  re-runs the pipeline (GeneratorTests' incrementality tests catch it).
- `Model/UnderlyingType.cs` is the closed table of the 22 underlying types, which drives nearly every per-type decision
  of the emitters. A new underlying type starts there.
- `RegistrationEmitter` writes a `[ModuleInitializer]` that fills `ValueObjectRegistry`, so consumers register nothing.
  Each descriptor carries the generated JSON converter, which `ValueObjectJsonConverterFactory` hands to a
  source-generated serializer context.
- Shapes the module initializer cannot name directly: a `private` or `protected` value object registers through a route
  of nested static step classes (`ValueObjectModel.RegistrationRoute`); a generic value object, or one nested in a
  generic type, registers its definition (`RegisterGenericDefinition`), and `ValueObjectRegistry.TryResolve` describes
  each construction by reflection from its generated `Schema` and `ValueJsonConverter` the first time it is asked.
  Still refused (`VO0019`): `file`-local types, a private or protected type inside a generic one, a generic `[EntityId]`.
  Details: `src/AdCodicem.ValueObjects.Generators/CLAUDE.md`.

### Two ways to reach a value object

- **Typed path**: the static abstract members of `IValueObject<TSelf, TValue>` (`Create`, `TryCreate`, `TryParse`,
  `Normalize`, `Validate`, `Schema`). Domain code and the generic adapters (ASP.NET binder, EF converter, Dapper handler,
  MongoDB serializer) use it, closed over the concrete types at startup, allocation-free per call. A typed adapter reads
  `TSelf.Schema`, never the registry.
- **Boxed path**: `ValueObjectDescriptor`, from `ValueObjectRegistry`, for code that knows a `Type` only at run time
  (attribute lookups, schema generators, binder providers, test-data adapters). Get back to typed code through
  `descriptor.Accept(IValueObjectVisitor<TResult>)` and close the adapter in `Visit<TSelf, TValue>`; never
  `MakeGenericType`, `MakeGenericMethod` or `Activator.CreateInstance`, which native AOT cannot run for a struct. An
  entity identifier has its own `EntityIdDescriptor.Accept(IEntityIdVisitor<TResult>)`.
- `RuntimeClosingTests` reads every package's IL and fails on a runtime-closing call outside its allow-list (the
  registry's reflection fallback, EF Core's converter of a `TSelf?` property or element, the JSON Patch dictionary
  adapter). The test is the source of truth; a new entry needs a reason there.
- Code that only asks "is this a value object" calls `ValueObjectRegistry.IsValueObject` and resolves nothing (Serilog,
  AI, MCP, the EF Core `.Value` translator, JSON Patch, the System.CommandLine walk).
- The `native AOT` job runs the visitors of the JSON factory's general-purpose converter (for a hand-written value
  object), the minimal API filter, `[NotDefault]`, the `[ParsesAs(typeof(T))]` lookup, the Blazor input, the
  System.CommandLine symbols, and `EntityIdDescriptor.Accept`. Those of MVC, the EF Core conventions, Azure Functions,
  both HotChocolate packages, NJsonSchema, protobuf-net's `AddValueObjects` and Orleans' codec selector run in no native
  binary, so a defect there shows under the JIT only.
- A hand-written value object declares `Schema`: the registry describes it from that alone, and reads no annotation on
  it at run time.
- The unit tests exercise the typed path, so a defect confined to the descriptor is invisible to them (it once flattened
  every rejection into `not_parsable`). Extend `DescriptorTests.cs` whenever you touch the descriptor.

### Hooks are interfaces

- A value object declares a rule by implementing a hook of `src/AdCodicem.ValueObjects.Abstractions/ValueObjectHooks.cs`
  (`IValueObjectNormalizer<T>`, `IValueObjectSpanNormalizer`, `IValueObjectPatternValidator`, `IValueObjectValidator<T>`,
  `IValueObjectMinimum<T>`, `IValueObjectMaximum<T>`, `IValueObjectFormatter<T>`, `IValueObjectStringFormatter<T>`,
  `IValueObjectExample<TSelf>`), so the compiler checks the signature. Hook members are public because a static abstract
  member cannot be anything else. Callers use `Normalize`, which guards `null`, never `NormalizeValue`.
- `VO0011` reports a rule written without its interface: a public static `Regex Pattern`, unless the type implements any
  other hook, which may already run it; a public static `Minimum` or `Maximum` property of the underlying type, unless
  the type implements `IValueObjectValidator<T>` or `IValueObjectNormalizer<T>`, which may check or clamp to it, and
  never on a field, which could not implement the interface; a public static `Example` property of the type.
- `IValueObjectPatternValidator`: the consumer writes `[GeneratedRegex(...)] public static partial Regex Pattern { get; }`
  and the framework's regex generator supplies the body. Strings only (`VO0023`), never an `[EntityId]` (`VO0024`). The
  generator reads the pattern text off the attribute for the schema: `VO0025` warns on a `RegexOptions` the text cannot
  carry, `VO0026` on a missing `matchTimeoutMilliseconds` (ADR 0007).
- A bound is read through `ValueObjectBound` every time, never cached: a copy taken while the type initializes would keep
  the default for good (ADR 0008). The schema publishes it through `ValueObjectBound.Text`, which the schema transformer
  re-writes in the converter's form. `VO0030`: a bound hook over a type that takes no bound, or over another type.
- `IValueObjectExample<TSelf>` returns an instance, which the schema reads through `ValueObjectExample.Of<TSelf>()` and
  publishes as its underlying value (`ValueObjectSchema.Example` is `object?`). `VO0038`: an example hook over another
  type (ADR 0011).
- The `Pattern`, `Minimum`, `Maximum` and `Example` options are `[Obsolete(error: true)]` (`VO0021`, `VO0028`, `VO0035`)
  and read by nothing; any minor may remove them.

## Invariants

- **Normalize, then validate, then assign**: a non-default instance is normalized and valid by construction. Only three
  paths skip validation through `CreateUnchecked`: EF Core and Dapper reads (values this application wrote), and the
  temporary integer key `HasValueObjectKeyGeneration` hands EF Core (the database replaces it). Stores and peers another
  program may write validate on read unless the application opts into trust (MongoDB, MessagePack, protobuf-net,
  Orleans); a `null` and a value the underlying serializer cannot read are refused whatever the trust. The per-store
  detail is in each package's `CLAUDE.md` and in `README.md`.
- **Rejection is not an exception on a boundary.** `ValidationResult` is a struct that allocates nothing on success.
  Every integration goes through `TryCreate` or `TryParse` and reports a refusal in its ecosystem's own terms, carrying
  the rule's code (`ValueObjectErrors.TryGetCode` reads it). `ValueObjectException` is thrown by `Create`, `Parse` and an
  explicit conversion, for code that treats a rejected value as a bug; of the integrations, only the EF Core converter
  throws it to the caller (a refused write, a strict read); the Orleans package rebuilds on the caller one that a grain
  lets escape, without `AttemptedValue` (`src/AdCodicem.ValueObjects.Orleans/CLAUDE.md`). Misusing an integration (a
  member of the wrong type, an unregistered type under native AOT) throws `InvalidOperationException` or
  `NotSupportedException`, with no code. A change to what an integration throws updates
  `website/docs/reference/errors.md`, the reference for all of them.
- **Validation is fail-fast**: the first violated rule wins.
- **Messages stay English; an application localises them by code**, under `ValueObject.{code}` (`ValueObjectMessages`),
  keeping the English message for a key it lacks. `KeysFor` tries `….out_of_range.minimum` or `.maximum` first for a
  type declaring one bound. The arguments come from `TSelf.Schema` (`ArgumentsFor`: lengths, bounds in the current
  culture, known values joined by the culture's list separator); the refused value is never an argument; `{0}` is the
  member, the value object's type name in a JSON body, the scalar's name for an operand of a HotChocolate.Data filter,
  the option's alias as typed or the argument's name on a command line, the token's position after it, from zero, where
  the symbol received several (`--ibans[1]` for the second, as gRPC's `skus[1]`; the English message counts from one,
  "the 2nd value"). No translation ships. Each integration's entry point is listed in
  `website/docs/reference/errors.md`.
- **Rules are declared once, in `TSelf.Schema`.** Validation, the EF column, the CHECK constraints, the MongoDB
  `$jsonSchema`, every OpenAPI and JSON Schema writer, the HotChocolate scalars, the Blazor input attributes, the
  System.CommandLine descriptions and completions, the XSD facets, the test-data sampler and the message arguments all
  read it, so a new option of `[ValueObject<T>]` feeds every one of them. A writer whose rule a server enforces (MongoDB
  validator, CHECK constraints, Blazor attributes) writes it only where the target refuses no value the type accepts; a
  documentation schema keeps it strict. `Unicode = false` describes storage, not acceptance: it feeds the EF Core
  conventions and the Dapper handler alone (`Schema.IsUnicode`), and `VO0047` warns where the rules let a character
  outside ASCII through.
- **A pattern leaving .NET goes through `src/Shared/ValueObjectPatternSyntax.cs`**, which reads it into a tree and refuses
  what it cannot read with certainty; each dialect writes from the tree (`XsdPattern`, `SqlPattern`, `PcrePattern`,
  `EcmaScriptPattern`, `PatternSampler`). Never translate a pattern by rewriting its text.
- **`default(T)` is a build error** (`VO0010`, an analyzer): a test constructing one on purpose takes a targeted
  `#pragma warning disable VO0010` with a comment.
- **A known value is the author's member**: `[KnownValue]` on a `static readonly` field or get-only auto-property
  initialized through the generated private `Known(...)`, which applies every rule but membership. The author's part of
  a type initializes before the generated part, so a closed set's `FrozenSet` is built once the members exist; a static
  initializer reaching `Create` earlier makes `Validate` throw `InvalidOperationException`. `VO0036`: a member the
  generator cannot read; `VO0037`: `Known` called anywhere else; `VO0031`: a declared value its rules refuse, checked off
  constants only (ADR 0011).

## Cross-cutting traps

- **Generated code assumes nothing of the consumer's project.** Fully qualify every name, extension methods included
  (`global::System.MemoryExtensions.AsSpan(x)`, never `x.AsSpan()`): a consumer with `ImplicitUsings` off otherwise gets a
  compile error in code they cannot edit.
- **Source generators never see each other's output.** The System.Text.Json generator cannot see the `[JsonConverter]`
  this generator writes, hence `AdCodicem.ValueObjects.Json`'s hand-written `ValueObjectJsonConverterFactory`, named
  through `[JsonSourceGenerationOptions(Converters = ...)]`; emitted code cannot use `[GeneratedRegex]`, hence the
  pattern hook; the Request Delegate Generator binds a value object of its own project from the body unless the
  declaration lists its contract (`VO0033`).
- **Every diagnostic is tracked** in `src/AdCodicem.ValueObjects.Generators/AnalyzerReleases.Shipped.md` or
  `.Unshipped.md`, or RS2008 fails the build. `VO0021`, `VO0028`, `VO0034` and `VO0035` are the exceptions: the
  `DiagnosticId` of an `[Obsolete(error: true)]`, which the compiler reports, documented by hand. Never reuse a retired
  identifier (`website/docs/reference/diagnostics.md`).
- **`#pragma` silences an analyzer's diagnostic, never the generator's.** `VO0025`, `VO0026` and the generator's other
  warnings fail the build whatever surrounds the declaration: a test needing such a type writes the value object by hand.
  A diagnostic users must be able to silence is an analyzer (`VO0047`, `NonUnicodeTextAnalyzer`). No `#pragma` silences
  an `[Obsolete(error: true)]` either, and the compiler does not report one inside a member that is itself `[Obsolete]`:
  test it by compiling a snippet in GeneratorTests.
- **A package namespace hides the root namespace of the same name.** Inside any `AdCodicem.ValueObjects.*` namespace,
  tests and the compatibility island included, `Swashbuckle`, `Serilog`, `MongoDB`, `MessagePack`, `Grpc`, `Orleans`,
  `ModelContextProtocol`, `HotChocolate`, `NJsonSchema`, `AutoFixture`, `Bogus`, `FsCheck` and `CommandLine`
  (CommandLineParser's root namespace, never System.CommandLine's) resolve to the package, and
  `AdCodicem.ValueObjects.Metadata` hides `Grpc.Core.Metadata` (CS0118). Name third-party types through usings at the
  top of the file, or `global::X` in a body. For the same reason, never name a test folder, namespace or class after a
  library or a package (`Blazor`, `AI`, `HotChocolate`, `NJsonSchema`, `NSwag`, `Confluent`, `Azure`, `ProtoBuf`,
  `ProtobufNet`, `Grpc`, `Orleans`, `ModelContextProtocol`, `MessagePack`, `CommandLine`, …): the unit suite's folders
  are named apart (`tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`).
- **A file of `src/Shared/` linked into two assemblies the unit suite sees is two internal types**, both visible through
  `InternalsVisibleTo`, so a test naming one is CS0433. Change a shared description once, there; the table of files,
  linking projects and how tests reach each copy is in `src/Shared/CLAUDE.md`.
- **Some state is process-wide and cached for good**: `ValueObjectRegistry` (`TryResolve` keeps what it resolves),
  MongoDB.Driver's serializer registry and id generators, protobuf-net's `RuntimeTypeModel.Default`, FluentValidation's
  language manager. A suite registers such state once, strict, before any test; a test needing another configuration
  builds its own, and never resolves a hand-written type other tests share.
- **A test file linked into another project changes it too**: the unit suite's `Domain/UnderlyingTypes.cs` is linked by
  the integration suite and `tests/NativeAot`, and three files of `Domain/HandWritten/` (`HandWrittenCode.cs`,
  `HandWrittenLink.cs`, `UnregisteredCode.cs`) by `tests/NativeAot`. Grep `Link=` before editing one.
- **Native AOT compatibility is per package** (`IsAotCompatible`, true by default in `Directory.Build.props`).
  `tests/NativeAot` references every package that claims it, but HotChocolate (its framework warns on its own publish
  and starts natively only rooted), HotChocolate.Data (its filtering of a struct has no native code), ProtobufNet,
  published by an application of its own, and Orleans, which does not start natively.
- **`src/AdCodicem.ValueObjects.Packages.slnf` lists every project under `src/`.** Previews and releases pack through it,
  so a project left out never ships; `ci.yml` fails when a packable project is missing. It sits under `src/` because
  `dotnet format` and `dotnet sln` refuse two solution files in one folder.
- **Anything that changes a package's bytes or metadata is a `preview-gate.sh` input**, or it never reaches a preview;
  **a dependency added to a shipped package goes into `.github/shipped-dependencies`** in the same pull request, or CI
  fails. Both: `.github/CLAUDE.md`.
- **Two Roslyn versions, both in `Directory.Packages.props`.** `Microsoft.CodeAnalysis.CSharp` 4.14.0 is the API the
  generator is built against, the oldest compiler a consumer may use: never raise it to follow a dependency. The unit
  suite alone takes 5.0.0, through a second `PackageVersion` conditioned on `$(MSBuildProjectName)`, since Orleans
  brings `Microsoft.CodeAnalysis.Common` 5.0.0 there (NU1608 otherwise); `Microsoft.CodeAnalysis.Analyzers` sits at
  Orleans' floor, 5.9.0, and transitive pinning lists it in the Orleans package's nuspec
  (`.github/shipped-dependencies`).
- **Required checks are job names**: `build and test`, `native AOT` (`ci.yml`) and `workflows` (`lint.yml`). Renaming
  one leaves every pull request waiting for a check that never reports.
- **No NuGet lock files.** `Microsoft.SourceLink.GitHub` is referenced only under `GITHUB_ACTIONS == 'true'`, so the
  runner's package graph differs from a laptop's and `--locked-mode` fails with NU1004 (ADR 0004).
- **A pull request of more than 100 commits cannot be rebase-merged**, and `main` takes a linear history landed with
  *Rebase and merge*, so that semantic-release reads every commit. Past 100, GitHub answers `rebaseable: false` and the
  web UI blames conflicts that do not exist; squashing folds every `fix` and `feat` into one changelog line. Count with
  `git rev-list --count origin/main..HEAD` and stack pull requests past 100; CI runs only on a pull request targeting
  `main`.

## Testing

- **UnitTests**: the behaviour of generated code, over the value objects of `Domain/`, and of every package called
  directly.
- **GeneratorTests**: the generator, every diagnostic, the analyzers and code fixes, incremental caching, and every
  published C# snippet.
- **IntegrationTests**: PostgreSQL, SQL Server, MongoDB and the Azure Functions host through Testcontainers, and the
  sample API of `samples/` end to end. Needs Docker.
- **RdgTests**: minimal API endpoints whose binding the Request Delegate Generator writes.
- `tests/NativeAot` holds applications CI publishes natively and compares with the JIT (the `native AOT` check);
  `tests/Compat` installs the packed packages on the .NET 11 release candidate, outside the solution.
- Stack: xUnit v3 (`TestContext.Current.CancellationToken`), AwesomeAssertions, NSubstitute, Testcontainers. Versions
  live in `Directory.Packages.props`, never in a `.csproj`; `tests/Compat` has its own.
- `AdCodicem.ValueObjects.Testing` ships the contract kit (`ValueObjectContract`). Add a contract with each value object
  added to `Domain/`, overriding `DerivesRejectedValues` to return `true`. The exceptions are `Floor` and `Celsius`,
  whose formatted text does not parse back, and the private `Strongbox.Secret` and `Archive.Shelf`
  (`DeclarationContextTests` covers them).

### Coverage

The aim is 100 % of each pull request's patch, as Codecov counts it (ADR 0006 holds the reasons).

- `codecov.yml` is the floor, not the aim: 95 % of the changed lines, a partial line counting as missed, and a project
  drop of half a point at most. `tests/`, `samples/` and `benchmarks/` are ignored.
- Cover every member that is not private through a natural input (a declaration compiled through the generator, a call
  through the public API, a request, a database round trip), otherwise by a test calling it directly.
- Never reach a private member by reflection: its callers cover it, or it stays uncovered. Leave a branch the compiler
  adds and no input can take partial (the default arm of an exhaustive switch, a `?.` on a value never `null`).
- Remove code only when no input can reach it, never because no test does. A defensive branch stays. A public member
  nothing calls is a question for the maintainer.
- Blind spots: a package no test loads is missing from the report rather than at 0 %, which
  `.github/scripts/coverage-modules.sh` fails; the code the generator emits lives in the unit test assembly, which is not
  measured, and is audited by hand.
