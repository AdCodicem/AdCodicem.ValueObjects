# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Twenty-five NuGet packages for single-value DDD value objects on .NET 10 and later. A `readonly partial struct` marked
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
# The main project needs Docker for PostgreSQL, SQL Server and MongoDB; without it, add --filter-not-trait "Requires=Docker"
dotnet test --project Swashbuckle/AdCodicem.ValueObjects.CompatTests.Swashbuckle.csproj -p:AdCodicemVersion=$v   # no Docker

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

Integration tests start PostgreSQL, SQL Server and MongoDB through Testcontainers.

`TreatWarningsAsErrors` is on repository-wide, so a warning fails the build.

## Releases

Two tracks, and nothing you merge publishes anything by itself.

A **preview** is published by `preview.yml`, every Monday at 07:15 Paris time and whenever it is dispatched from
`main`, and only when a package input changed since the version nuget.org has from the nearest commit:
`.github/scripts/preview-gate.sh` decides `publish`, `repair` or `none`, and fails the run rather than guess when a
lookup fails. All twenty-five packages go out at one version, or none. That version is the one semantic-release would
give the next stable release, computed without a token by `.github/scripts/next-version.mjs`, which runs
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
a breaking change wait for a major. The twenty-five packages share one version, never aligned with .NET's or EF Core's,
and a framework's next major is supported in the same packages:
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
`TypeConverterEmitter`, `XmlSerializableEmitter` and `RegistrationEmitter`. `Model/UnderlyingType.cs` is the closed
table of the 22 supported underlying types and drives nearly every per-type decision the emitters make.

`XmlSerializableEmitter` writes only in an assembly marked `[assembly: ValueObjectXmlSerialization]`: an explicit
`IXmlSerializable` and a public static `GetXmlSchema` named by `[XmlSchemaProvider]`, each a one-line call to
`ValueObjectXml` in the contracts, which reads through `TSelf.TryCreate` and assigns through `Unsafe.AsRef(in this)`.
The generator reads the attribute off the compilation inside the transform, so the flag and its `Namespace` join the
model: the transform runs again on every compilation, and the model's equality keeps the emission cached. A type that
implements `IXmlSerializable` or carries `[XmlSchemaProvider]` itself is left alone.

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
  integrations use — the ASP.NET binder, the EF converter, the Dapper handler and the MongoDB serializer are all generic
  and closed over the concrete types at startup, so per-request work is fully typed and allocates nothing extra. A
  typed adapter reads `TSelf.Schema`, never the registry, which would describe a construction of a generic value object
  by reflection.
- **Boxed path.** `ValueObjectDescriptor`, resolved from `ValueObjectRegistry`, for callers that only know a `Type` at
  run time — `MustParseAs(Type)`, the OpenAPI transformer and the Swashbuckle filters, model-binder resolution.
  `descriptor.Accept` hands an `IValueObjectVisitor<TResult>` the type arguments back, so an integration closes its
  adapter at compile time rather than with `MakeGenericType`, which native AOT cannot run for a struct. Dapper's
  `AddValueObjectHandlers`, the MVC binder provider, the JSON factory's general-purpose converter, for a value object
  written by hand, the minimal API filter of `AspNetCore.Http`, which closes a text check per parameter, the MongoDB
  provider, which builds the serializer the driver asks it for over the driver's serializer of the underlying type, the
  MessagePack resolver, which builds, once per resolver, the formatter MessagePack asks it for, the test-data sampler's
  `Next(descriptor)`, the AutoFixture specimen builder, Bogus's `RuleForValueObjects` and `ValueObject<TSelf>`, FsCheck's
  `MergeValueObjects`, and EF Core's `ConfigureValueObjects` and `ConfigureEntityIds` do. An entity identifier's descriptor has a visitor of its
  own: `EntityIdDescriptor.Accept` hands an `IEntityIdVisitor<TResult>` the identifier type, under `IEntityId<TId>`,
  through which `EntityIdBson.Register` of `Identifiers.MongoDB` closes each id generator and reaches `TId.New()`.
  `ConfigureValueObjects` still closes the converter of a `TSelf?` property with `MakeGenericType`, over the visitor's
  own type arguments: C# names it only under `TValue : struct` or `TSelf : IValueObject<TSelf, string>`, which `Visit`
  cannot prove, and it must stay the type a compiled model names. It and the convention it adds for generic
  constructions are also the exception to a typed adapter reading `TSelf.Schema`: they size a column by the descriptor's
  `Schema`, which a registration made by hand may set apart from the type's, handed to their visitor in a field.
  `ConfigureEntityIds` needs none: an identifier is over `string`, so its visitor casts itself to an interface it
  implements over `string`, whose method takes that constraint. It visits the identifier's `ValueObjectDescriptor`, from
  `TryResolve`, for the column it sizes from the descriptor's `Schema`: an identifier registered with `EntityIdRegistry`
  alone, by hand, is described by reflection there, and stays in `ValueObjectRegistry` from then on. The Serilog
  integration closes nothing and visits no descriptor: its policy and its enricher read a value Serilog has already
  boxed through the `IValueObject` marker, and its option only reads the `ValueObjectType` of each descriptor the
  registry holds, which it hands Serilog as a scalar type. Neither does the Microsoft.Extensions.AI integration, which
  calls no `TryResolve` either: it tells a value-object parameter of a tool by `ValueObjectRegistry.IsValueObject`, and
  reads each argument through the contract the tool's serializer options hold for the parameter, the one the tool's own
  binding reads it through; nor the Model Context Protocol one, which reads them through the same checker,
  `src/Shared/ValueObjectArguments.cs`, linked into both packages. Neither EF Core convention runs under native AOT, where EF
  Core reads the compiled model and builds none, and the MVC binder provider runs in no native binary, MVC not being
  AOT-compatible, so the `native AOT` job guards the JSON factory's visitor and the minimal API filter's, and
  `EntityIdDescriptor.Accept`, which the application calls on every identifier registered. `RuntimeClosingTests` guards
  every package: it reads their IL and fails on a `MakeGenericType`, a `MakeGenericMethod` or an
  `Activator.CreateInstance` outside the list it holds, the registry's reflection fallback and the EF Core converter of
  a `TSelf?` property. A hand-written value object declares `Schema` too, and the registry describes it from that alone:
  an annotation on it is read by nothing at run time.

The unit tests exercise the typed path, so a defect confined to the descriptor is invisible to them. That is
exactly how the descriptor once flattened every rejection into a generic `not_parsable`, discarding the rule
that actually fired. `DescriptorTests.cs` exists to cover that surface; extend it when touching the descriptor.

### Invariants worth knowing before changing anything

- **Normalize, then validate, then assign**, so a non-default instance is by construction normalized and valid.
  The exceptions are the EF Core and Dapper read paths, which use `CreateUnchecked` because they read values this
  same application already validated. `ConfigureValueObjects(strict: true)` turns validation back on for EF Core;
  Dapper validates only a column the value object cannot have written: text read into a value object over another
  type, or a number or a `Guid` read into one over `string`. MongoDB reads the other way round: strict, through
  `TryCreate`, unless the serializer is trusted (`ValueObjectBson.Register(trusted: true, …)`), since a collection has
  no schema and is often written by more than one program; so does MessagePack, unless the resolver is trusted
  (`WithValueObjects(trusted: true)`, `UseValueObjects(trusted: true)`), since the bytes may come from another service.
  A `nil`, a BSON `null`, and a value the underlying type's own formatter or serializer cannot read are refused whatever
  the trust.
- **Rejection is not an exception on a boundary.** `ValidationResult` is a struct that allocates nothing on success. The
  integrations go through `TryCreate` or `TryParse` and report a refusal in their own terms: a `JsonException` or
  `JsonSerializationException`, a model state error, the validation problem of a minimal API, a FluentValidation
  failure, a Dapper `DataException`, a MongoDB.Driver `FormatException` on read or `BsonSerializationException` on
  write, a MessagePack `MessagePackSerializationException`, an `XmlException` from `XmlSerializer` or
  `DataContractSerializer` in an assembly marked `[assembly: ValueObjectXmlSerialization]`, carrying the code in its
  `Data`; the AI package's wrapper throws nothing, and returns the result of a Microsoft.Extensions.AI tool,
  `{"error":"invalid_argument","argument","code","message"}`, which `FunctionInvokingChatClient` hands the model as it
  is, and the Model Context Protocol package answers a tool execution error (`isError: true`) whose text carries the
  code, and whose structured content is that object when the tool declares no output schema. The one that throws
  `ValueObjectException` is a strict EF Core read, which goes through `Create` and fails the query; `Create`, `Parse`
  and an explicit conversion throw it for code that treats a rejected value as a bug. `website/docs/reference/errors.md`
  names what each integration throws. Validation is fail-fast: the first violated rule wins.
- **Rules are declared once.** `MaxLength = 34` validates, sizes the EF column and becomes the OpenAPI `maxLength`, and
  the `maxLength` of the MongoDB `$jsonSchema` validator (`ValueObjectBsonSchema`, which writes each rule of
  `TSelf.Schema` only where the server refuses no value the type accepts, the pattern in PCRE2 through `PcrePattern`
  over the shared reader below, each class escape listed from .NET's own Unicode tables). Anything added to
  `[ValueObject<T>]` should feed all of them, the validator wherever the server reads the rule as .NET does. A hook can
  feed the schema too: the `[GeneratedRegex]` behind `IValueObjectPatternValidator` validates, and its text, read off
  the attribute at compile time, becomes the OpenAPI `pattern`. The OpenAPI transformer, the Swashbuckle filters and the
  JSON Schema transform (`ValueObjectJsonSchema`, in the Json package) take what a rule becomes in a schema from
  `src/Shared/ValueObjectSchemaKeywords.cs`, an internal file each package links and compiles, not a project: a keyword
  changes there, for all three. The JSON Schema transform's language-model profile reaches the tools and the
  structured output of Microsoft.Extensions.AI through the AI package's `WithValueObjects()` and
  `ValueObjectResponseFormat.ForJsonSchema<T>()`, and the input and output schemas of a Model Context Protocol tool
  through `WithValueObjectTools<T>()`. That profile describes what a model should send, and, as the OpenAPI one, keeps
  two rules the MongoDB writer loosens where the server would refuse a value the type accepts: the `enum` of a closed
  set looked up ignoring case, and a `minLength` .NET counts in UTF-16 code units. A tool's `outputSchema` can therefore
  refuse a result the server writes, which the guide states as a limit. The OpenAPI transformer and the Swashbuckle
  filters, both on the object model of `Microsoft.OpenApi` 2, share the whole description of a value object the same
  way, through `src/Shared/ValueObjectOpenApiSchema.cs`; what is particular to each host stays with it: how it finds
  the value object, the options, the name of a closed set's enumeration, parameters and containers. The XSD facets the
  schema provider of an assembly opted into XML serialization publishes (`ValueObjectXml.ProvideSchema`) read the same
  `TSelf.Schema`. A pattern leaving .NET goes through `src/Shared/ValueObjectPatternSyntax.cs`, the reader every package
  that writes a .NET pattern in another dialect, or draws values from one, links: it reads a pattern into a tree,
  refusing what it cannot read with certainty, and each dialect writes what it reads alike from the tree, `XsdPattern` in
  the contracts for XSD; the test-data sampler (`PatternSampler` in `AdCodicem.ValueObjects.Testing.Data`) draws strings
  from the same tree, and leaves a pattern outside its subset to rejection sampling, over the tree of the pattern read
  without its lookarounds and with its categories reduced to printable ASCII. The rules a test draws values from
  are the same `TSelf.Schema`: lengths, pattern, bounds, known values, and the example as a fallback.
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
- **A minimal API convention runs before the binder describes the parameters.** `WithValueObjectProblemDetails`
  reads them in its filter factory, from the `IParameterBindingMetadata` both binders add to the endpoint's metadata:
  the parameters it checks, and the names they bind under, come from there, because `MethodInfo.GetParameters()`
  misses the members `[AsParameters]` expands and the names those members bind under. `GetParameters()` serves only to
  add no filter factory to a handler that has no value object, no array of them and no `[AsParameters]` parameter. The
  filter mirrors the binder's empty-text rule, which differs: the reflection-based binding parses empty text; the RDG
  refuses no empty query text, binding `null` to a nullable value object and the default instance, unchecked, to one
  that cannot be `null`, and takes an empty header for an absent one. It tells the RDG's endpoints by the
  `GeneratedCodeAttribute` the RDG adds to their metadata, which an RdgTests test pins, so that an SDK dropping it fails
  there first. The convention fails the build of an endpoint whose HTTP JSON options cannot serialize the problem
  details, as without reflection and without `AddProblemDetails()`, rather than let each refusal end in a 500.
  `ValueObjectProblemDetails` lives in `AdCodicem.ValueObjects.AspNetCore.Http`, under the namespace
  `AdCodicem.ValueObjects.AspNetCore`, and the MVC package forwards it (`TypeForwards.cs`), so that code compiled
  against an earlier MVC package still finds it: keep both the namespace and the forward.
- **MVC's Newtonsoft.Json input formatter keeps its settings protected and changes its exception policy for a
  subclass.** `AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson` derives from `NewtonsoftJsonInputFormatter`, which
  answers `InputFormatterExceptionPolicy.AllExceptions` for any type but its own, under which MVC turns any exception a
  read throws into a 400: the subclass overrides `ExceptionPolicy` back to `MalformedInputExceptions`. The setup tells
  the framework's formatter by its settings, a protected property a derived class reads on its own instances alone
  (CS1540), so it reads them through `[UnsafeAccessor]`. The subclass adds its `Error` handler to the pooled serializer
  for the time of each read, in `CreateJsonSerializer(context)`, and removes it in `ReleaseJsonSerializer`, or the
  handler of one request would record the codes of the next. The key it records a code under is the framework's model
  state key, rebuilt by `KeyOf` from the framework's error handler: a change to that handler upstream has to be carried
  there, and the parity theory of `NewtonsoftJsonInputFormatterTests` and the compatibility island are where it shows.
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
- **Swashbuckle 10 is built on `Microsoft.OpenApi` 2, and fails on 3.** Its document generator throws
  `MissingMethodException` once `Microsoft.OpenApi` 3 is resolved, which `Microsoft.AspNetCore.OpenApi` 11 forces: the
  island's main project references that package, so `AdCodicem.ValueObjects.Swashbuckle` is installed by a project of
  its own, `tests/Compat/Swashbuckle`, which the main one excludes from its sources (`DefaultItemExcludes`), and the
  `compat (.NET 11)` job runs both. Its floor on `Microsoft.OpenApi` is the transitive pin of `Directory.Packages.props`,
  2.12.2: the 2.7.5 Swashbuckle asks for drops the example of an OpenAPI 3.0 schema. Pin no version of
  `Swashbuckle.AspNetCore.Swagger`, which the tests take through `Swashbuckle.AspNetCore.SwaggerGen`: the pin would make
  it a dependency of the package, to list in `.github/shipped-dependencies`. The package's namespace,
  `AdCodicem.ValueObjects.Swashbuckle`, hides the root `Swashbuckle` namespace from code in any `AdCodicem.ValueObjects.*`
  namespace, tests included: name Swashbuckle's types through usings at the top of the file, never through a
  qualified `Swashbuckle.…` in a body. `AdCodicem.ValueObjects.Serilog` hides the root `Serilog` namespace the same
  way, the island's `AdCodicem.ValueObjects.CompatTests` included, and a file importing `Serilog` beside
  `Microsoft.Extensions.Logging` aliases one `ILogger` (CS0104).
- **MongoDB.Driver's serializer registry is process-wide, and caches for good.** A value object the driver serializes
  before `ValueObjectBson.Register` keeps the class map it was given, which writes `{}`, for the rest of the process,
  and `Register` then fails, naming it (it reads `BsonClassMap.GetRegisteredClassMaps()`, which caches nothing, and asks
  `ValueObjectRegistry.IsValueObject` of each type, so a generic construction counts); a serializer of the application's
  own, the `GuidSerializer(GuidRepresentation.Standard)` above all, is registered before it. So every MongoDB test class
  of a suite goes through one static registration first (the unit suite's `Persistence/MongoDb.cs`, the integration
  suite's `MongoDbFixture`, the island's), strict, and a test needing another trust or representation builds its
  serializer by hand, sets one on a class map of its own, or uses a fresh `BsonSerializerRegistry`, which the internal
  checks take as a parameter; a test of `Register` refusing goes through `MongoDbStartUpTests`' fresh copies of the
  driver, loaded into an `AssemblyLoadContext` of their own. The id generators of
  `AdCodicem.ValueObjects.Identifiers.MongoDB` are process-wide too, and a class map takes its id member's generator
  when it is built, never again: the same static registrations call `EntityIdBson.Register` right after
  `ValueObjectBson`, and a test of an id generator registered or kept closes `HandWrittenId<TProfile>` over a profile of
  its own. The package's namespace, `AdCodicem.ValueObjects.MongoDB`, hides the root `MongoDB` namespace in every
  `AdCodicem.ValueObjects.*` namespace, as Swashbuckle's and Serilog's do: name the driver's types through usings at the
  top of the file. MongoDB.Bson has an internal `UInt128` of its own, which a `cref` to `UInt128` in a file importing
  `MongoDB.Bson` resolves to (CS0419): write `System.UInt128` there. A query's `ToString()` reports a translation the
  serializer refused instead of throwing it; a test of a refused query constant renders a `Builders` filter.
- **MessagePack's analyzer comes with the MessagePack package, and neither MessagePack nor its SignalR protocol is
  AOT-compatible.** MessagePack's nuspec takes `MessagePackAnalyzer` with `include="All"`, so its analyzer and source
  generator reach every project referencing `AdCodicem.ValueObjects.MessagePack`, by package or by project, the unit
  suite and the island included: `MsgPack003` fails the build of a `[MessagePackObject]` type holding a value object of
  the same assembly, so `BinarySerialization/MessagePackAssumptions.cs` and the island's `MessagePackTests.cs` assume
  each such value object formattable (`[assembly: MessagePackAssumedFormattable(typeof(Iban))]`), as the guide tells an
  application to, and `MsgPack010` refuses a private formatter. MessagePack 3.1 has no trimming annotation (any native
  publish warns `IL2104` and `IL3053` for the whole assembly), builds the formatters of a `Nullable<T>`, a collection or
  a dictionary by reflection, which fails natively for any struct, and SignalR's MessagePack hub protocol needs dynamic
  code: the package claims no AOT compatibility and stays out of `tests/NativeAot`. Under
  `MessagePackSecurity.UntrustedData`, which SignalR sets, MessagePack refuses a dictionary keyed by a value object and
  a set of them (`TypeAccessException`); the package adds no comparer, a value object's equality possibly differing from
  its value's, and a test pins the refusal and the guide's workaround. The package's namespace,
  `AdCodicem.ValueObjects.MessagePack`, hides the root `MessagePack` namespace in every `AdCodicem.ValueObjects.*`
  namespace, as Swashbuckle's, Serilog's and MongoDB's do: name MessagePack's types through usings at the top of the
  file, and name no test folder or namespace `MessagePack` (the unit suite's is `BinarySerialization/`).
- **Serilog destructures no object under trimming.** Its `buildTransitive/Serilog.targets` sets
  `Serilog.Capturing.IsStructureValueSupported` to false under `PublishTrimmed`, which `PublishAot` implies, so a native
  binary logs an object with `@` as its `ToString()`, and a bare `Int128` as its text. The native AOT application sets
  the same switch for its JIT run, conditioned on `PublishTrimmed` not being on, or every `{@object}` line would
  differ. Serilog.Expressions warns under native AOT (IL2104, IL3053), and `native-aot.sh` fails on any warning: it
  stays in the unit suite, out of `tests/NativeAot`. Under the JIT, the option of `Destructure.ValueObjects` reads the
  registry when it runs, before most module initializers have: the unit tests assert only on types registered for
  certain, the domain's, or absent for certain, a fresh `AssemblyLoadContext` copy of the untouched fixture or a
  construction over a marker type private to the test, and never `Register` a shared hand-written type.
- **Microsoft.Extensions.AI binds an argument without asking the value object.** `AIFunctionFactory` hands a C# `null`,
  which is what the OpenAI adapter makes of a JSON `null`, to a struct parameter as its uninitialized instance, without
  calling the converter, fails an absent argument with an `ArgumentException`, and takes an object of another type
  through a JSON round trip of its own, text that may be JSON read as JSON first, swallowing what it throws: the AI
  package's wrapper answers the first two as `value_object.required`, and reads the third through the same round trip,
  so that its verdict on text or a number a host hands over is the binding's. System.Text.Json writes the message of
  a refusal the generated converter leaves without one, a number the underlying type cannot hold, with the path of the
  value, which holds a dictionary's keys: the wrapper tells it by the path information at its end and answers
  "The value is not a valid X." instead. It reads the name of a parameter through `AIParameterNameAttribute`, which is
  `[Experimental("MEAI001")]`: `src/Shared/ValueObjectArguments.cs` reads it by name, so that a renaming or a removal
  upstream leaves the check working, and mirrors `AIJsonUtilities.TryGetEffectiveDefaultValue` for the parameters the
  binding fills in. Agent Framework's `RunAsync<T>` replaces the response format its run options carry with
  `ChatResponseFormat.ForJsonSchema<T>`, which no schema option reaches: the guide hands the package's format to a plain
  `RunAsync` and reads the answer with `AgentResponse<T>`, and `AgentFrameworkTests` pins both.
- **The Model Context Protocol SDK rewrites the output schema of its own tools only.** For a client on a protocol version
  before `2026-07-28`, which is every client in use today, `McpServerImpl` wraps a non-object `outputSchema` in
  `{"type":"object","properties":{"result":…}}` only when the registered tool is its internal `AIFunctionMcpServerTool`,
  while the tool still wraps its structured content: a tool wrapping another would publish a schema its content does not
  match. So the package registers the SDK's own tools, built with `McpServerTool.Create` and schema options from
  `WithValueObjects()`, keeps the parameters to check of each in a `ConditionalWeakTable` keyed by the tool, and checks
  them in a call-tool filter that an `IPostConfigureOptions<McpServerOptions>` places, once per service collection
  (`TryAddEnumerable`), after every filter `Configure` added: the last of the list is the innermost. Options built or
  changed after that, by hand for a server created without dependency injection or in ModelContextProtocol.AspNetCore's
  `ConfigureSessionOptions`, take it through the public `AddValueObjectValidation()`, which moves it last, once. The SDK
  builds the pipeline when it creates a server, and refuses call-tool filters beside an explicit
  `CallToolWithAlternateHandler` (`MCPEXP002`) then: at start over stdio, for each session over HTTP, for each request
  when stateless. The SDK's client asks for `2026-07-28`, which hides the rewrite: a test of the older shape pins
  `McpClientOptions.ProtocolVersion`. The SDK's own `McpServerToolCreateOptions.Clone()` is internal, so
  `ValueObjectMcpServerTool.Describe` copies its properties one by one, and a test fails when the SDK adds one the copy
  drops. Under reflection-free serialization the server's options are a copy of `McpJsonUtilities.DefaultOptions` with
  the application's context first in the resolver chain and `ValueObjectJsonConverterFactory` in `Converters`: a
  context resolving for options other than its own does not apply the converters its attribute names. The package's
  namespace, `AdCodicem.ValueObjects.ModelContextProtocol`, hides the root `ModelContextProtocol` namespace in every
  `AdCodicem.ValueObjects.*` namespace, as Swashbuckle's, Serilog's, MongoDB's and MessagePack's do: name the SDK's types
  through usings at the top of the file, and name no test folder, class or namespace `ModelContextProtocol`.
- **`XmlSerializer` reads a value object's schema before it serializes anything.** It calls the provider when the
  serializer is built, insists on finding it public and static, and compiles its `xs:simpleType`, refusing the whole
  type, and every serializer over a type holding it, for one facet System.Xml cannot read. System.Xml holds an
  `xs:integer` in a `decimal`, so `ValueObjectXml` leaves out a bound beyond one, checks every bound and enumeration
  value with `XmlSchemaDatatype.ParseValue`, and drops the rules of a type that still does not compile. It also hands
  an XSD pattern to .NET's engine as `^(…)$`, so a bare `$` in one is an anchor there: `XsdPattern` writes it `[$]`, and
  `XsdPatternTests` compares every translation with .NET on the same values, through System.Xml's own validation.
  `DataContractSerializer` cannot read an `IXmlSerializable` struct without dynamic code; the native AOT domain opts in
  as a guard that the emission adds no warning, and calls neither serializer.
- **A file of `src/Shared/` linked into two assemblies the unit suite sees is two internal types.** Both are visible
  through `InternalsVisibleTo`, so a test naming one is CS0433. The contracts and the MongoDB package both link
  `ValueObjectPatternSyntax.cs`: the unit suite references the contracts under `Aliases="global,abstractions"`, and
  `ValueObjectPatternSyntaxTests` names the contracts' copy through `extern alias abstractions`; the MongoDB copy is
  tested through `PcrePattern`, and the copy of `AdCodicem.ValueObjects.Testing.Data` through `PatternSampler`, whose
  tests name no node type. A further package linking it is tested the same way. `TypeNames.cs`, which writes a type as C#
  names it in a message, is linked by `Testing.Data` and by its AutoFixture, Bogus and FsCheck adapters: the unit suite
  references `Testing.Data` under `Aliases="global,testingdata"`, and `SamplerTests` names its copy through the alias.
  `ValueObjectArguments.cs` and `ValueObjectArgumentRejection.cs`, the check of a tool's arguments and the result it
  answers a refusal with, name no type of Microsoft.Extensions.AI, and are linked by the AI and the Model Context
  Protocol packages, each overload of `FirstRejection` called by one of them: no test names them, `LanguageModels/`
  reaching them through `WithValueObjectValidation()` and `WithValueObjectTools` alone, so that neither copy needs an
  alias, and coverage counts a line of the file covered by either copy.
- **The test-data adapters each meet their library's own rules.** AutoFixture stays at 4.18.1 while 5.0 is a release
  candidate: a stable package depending on a prerelease is NU5104, which fails the pack; Dependabot proposes 5.0 as
  `fix(deps)!` once it ships. Bogus keeps the last rule of a member, so `RuleForValueObjects` replaces a rule written
  before it, and a test pins both orders. FsCheck's `MergeValueObjects` merges what the assembly's generated registration
  lists, never the constructions of a generic value object nor a hand-written type nothing registered, which
  `MergeValueObject<TSelf, TValue>` merges one by one: scanning the assembly through `TryResolve` would register every
  hand-written type for the process. FsCheck stops a run at 5,000 shrinks, draws `bool` and `Guid` with no shrinker,
  handles neither `DateOnly` nor `TimeOnly`, and derives the 128-bit integers by reflection, drawing 0, so a value object
  over one of them shrinks to its declared values alone. Its other shrinks reach `ValueObjectSampler.Shrink` only when
  the normalizer leaves them unchanged: its character shrinker proposes `a`, `b` and `c` for any capital, so an
  upper-casing normalizer would shrink `B` and `C` to each other until that cap, and a test counts the shrinks. Its
  `DateTime` shrinker makes a UTC value, as the sampler draws one, of no kind and nothing else, which the arbitrary works
  around by shrinking the value of no kind and giving the kind back. FsCheck writes `Shrunk:` in a failure only after one
  shrink at least: a test reading the counterexample falls back on `Original:`. The namespaces `AdCodicem.ValueObjects.AutoFixture`, `.Bogus` and `.FsCheck` hide the root `AutoFixture`,
  `Bogus` and `FsCheck` namespaces in every `AdCodicem.ValueObjects.*` namespace, as Swashbuckle's, Serilog's, MongoDB's
  and MessagePack's do: name their types through usings at the top of the file, and name no test folder or namespace after
  one of them (the unit suite's is `TestData/`).
- **`#pragma` does not silence a diagnostic the generator reports.** `VO0025`, `VO0026` and the generator's other
  warnings fail the build under `TreatWarningsAsErrors` whatever surrounds the declaration: a test needing a type that
  trips one writes the value object by hand, as `Persistence/MongoDbShapedCode.cs` does for a pattern matched under
  `IgnoreCase`, `Multiline` or `IgnorePatternWhitespace`.
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
  the rest, since the generator runs on it and its module initializer has run before any test does, so four
  fixture assemblies under `tests/Fixtures/` do: a generated value object in a module nothing has used yet,
  annotated hand-written ones where no generator runs, generated ones in an assembly that does not reference
  `AdCodicem.ValueObjects.Json`, as a domain project serializing through an API's context does not, and generated ones
  of every underlying type in an assembly marked `[assembly: ValueObjectXmlSerialization]`, beside value objects
  written by hand on `ValueObjectXml` whose schemas the generator never writes (`XmlSerialization/` tests them).
  `BinarySerialization/` drives the MessagePack package directly and through a SignalR hub and the .NET client over
  TestHost; a test of the reflection fallback there closes `Reference<TOwner>` over the test class, never
  `UnregisteredCode`, which nothing may resolve. `LanguageModels/` (not `AI/`, whose namespace would hide
  `AdCodicem.ValueObjects.AI` inside the suite's) builds Microsoft.Extensions.AI tools over `Domain/`, and over every
  sample through `GeneratedSurface/`: a scripted `IChatClient` stands for the model behind `FunctionInvokingChatClient`,
  and the request the OpenAI adapter sends in strict mode is captured by an `HttpMessageHandler` behind
  `HttpClientPipelineTransport`, with no network; a test that names `AIParameterNameAttribute`, experimental
  (`MEAI001`), silences it around that one use. `AgentFrameworkTests` runs the same tools and the response format
  through Agent Framework's `ChatClientAgent`, over a scripted model. The folder's `Mcp*` files run a Model Context
  Protocol server in process, over a pair of pipes, against the SDK's own client (`McpHarness`), the tools of
  `McpTools.cs` and one over every sample; `McpRegistrationTests.cs` silences `MCPEXP002` around the one statement
  setting `CallToolWithAlternateHandler`.
  `PropertyTests.cs` runs the laws `IValueObject<TSelf, TValue>` states in prose — normalization is
  idempotent, an accepted value is a normalization fixed point, rejection never throws — over FsCheck-generated
  input, the values each type accepts drawn by the FsCheck package's `ValueObjectArbitrary`, over `ValueObjectSampler`,
  from a seed FsCheck draws, the IBAN's from the
  MOD-97 generator registered with `Use<Iban, string>`, and the deliberately invalid input written by hand. Two things
  keep such a suite honest and both are easy to lose: a property conditioned on "the value was
  accepted" passes vacuously unless the generator produces values the type accepts, so each one counts how often
  it reached the accepting branch and asserts on it; and a property over a wide type never lands on the boundary
  by chance, so the range generator biases towards the edges, the `Boundaries` and `RejectedValues` the sampler
  derives from the declared range. Change a generator and re-run the mutations in the commit message before trusting
  the green. `TestData/` tests the sampler on value objects of its own beside `Domain/`, and on schemas written by hand
  through `Declared<TDeclaration, TValue>`, a value object written by hand over any underlying type: the sampler's typed
  path never asks the registry, and a test of its boxed path resolves only generated value objects, never a shared one
  written by hand, which `TryResolve` would keep registered for the process. `GeneratedSurface/` draws every value object
  of `Domain/` through it too. `AutoFixtureTests`, `BogusTests` and `FsCheckTests` drive the three adapters over the
  objects of `TestDataOrders.cs`, which hold value objects in every shape a member takes; the adapters resolve through
  the registry, so the only hand-written value objects they meet are `Declared<,>` constructions over declarations
  those tests own (`Tally`, `WatchedTally`), and `FsCheckTests` merges a fresh `AssemblyLoadContext` copy of the
  untouched fixture, whose registration only the merge can have run.
- **GeneratorTests** — the generator itself: emission, every diagnostic, hook detection, the analyzers, and
  incremental caching. It drives Roslyn directly through `Harness/GeneratorHarness.cs` rather than through
  `Microsoft.CodeAnalysis.Testing`, which binds to xUnit v2. Snippets compile **without** implicit usings, which
  is what catches unqualified names in emitted code. They compile with the Roslyn `Directory.Packages.props` pins,
  4.14, under `LanguageVersion.Preview`, which runs a syntax node action three times per node inside a C# 14
  extension block, where the compilers of SDK 10 and 11 run it once: a VO0010 test puts no lambda and no local
  function in one. The harness also runs the framework's regex generator beside
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
  objects reach the column types they claim, plus the API surface end to end; and a real MongoDB 8 server, which stores
  a value object of every underlying type MongoDB.Bson can represent, linked from the unit suite's
  `Domain/UnderlyingTypes.cs`, as the document its primitive writes, refuses one over `Int128` or `UInt128`, and
  answers each LINQ and `Builders` shape over value objects with the documents the same query over the primitives
  would; under the `$jsonSchema` validator built from the rules, it refuses another writer's document, stores every
  value each type accepts, reads each pattern the validator publishes as .NET does (`MongoDbValidatorTests`, which pins
  the PCRE2 facts the writer works around), and mints an entity identifier for a document inserted without one.
- **RdgTests** — minimal API endpoints whose binding the Request Delegate Generator writes, over value objects
  declared in the endpoints' own project, which list their contract (VO0033). The project imports the package's
  `build/AdCodicem.ValueObjects.props`, so VO0033 fails its build for a value object that drops its contract, and a
  test reads the RDG's output under the generated files, so that a change in the SDK's defaults cannot turn it into a
  test of the reflection-based binding. The RDG writes one interceptor for two handlers whose parameters share types
  and names, dropping the attribute of the second, a `[FromHeader]` included: name such parameters apart. Its problem
  details endpoints (`ProblemEndpoints.cs`) cover what only the RDG does: it refuses no empty query text and takes an
  empty header for an absent one, and the filter tells its endpoints by the `GeneratedCodeAttribute` it adds to their
  metadata, which a test pins.

Beside them, in the solution but no suite, `tests/NativeAot` holds applications built as consumers build them, which
CI publishes rather than tests. None imports `tests/Directory.Build.props`, so the trimming and AOT analyzers stay
on where they apply.

- `AdCodicem.ValueObjects.NativeAot` is a minimal API referencing every package that claims to be AOT-compatible, with
  its value objects in `AdCodicem.ValueObjects.NativeAot.Domain`, which links the unit suite's
  `Domain/UnderlyingTypes.cs`. The application links two value objects written by hand from `Domain/HandWritten/`, one
  over `Uri`, and registers them without a converter, so the JSON factory's general-purpose converter runs natively,
  and links `UnregisteredCode`, registered by nothing, which the factory refuses with a `NotSupportedException`. Two
  more applications answer refusals with the minimal API problem details (`Problems.cs`), with `ThrowOnBadRequest` off
  and on, each answer checked against the status and the codes the script expects, since both runs explaining nothing
  would agree; a third, without `AddProblemDetails()`, must fail to build its endpoints. `Logging.cs` logs every
  registered value object through Serilog with and without `@`, under the policy and the option, and fails the run when
  one is not the scalar of its underlying type, written as the bare value is. `LanguageModels.cs` describes every
  registered value object in a Microsoft.Extensions.AI tool's schema, builds tools from method groups over
  `AppJsonContext`, whose arguments are refused and accepted, and the response format of a structured output; a tool's
  parameter and result types go into `AppJsonContext`, never their underlying types. `Mcp.cs` runs a Model Context
  Protocol server in process over a pair of pipes, registered with `WithValueObjectTools<T>()` over a copy of
  `McpJsonUtilities.DefaultOptions` whose resolver chain starts with `AppJsonContext`, lists its tools on the latest and
  on an older protocol version, and calls them with refused and accepted arguments; the SDK's default options, which
  know no value object, fail its start. The domain opts into XML serialization,
  which nothing calls: a guard that the emission adds no trimming or AOT warning and changes no output.
  `ci.yml`'s `native AOT` job (`.github/scripts/native-aot.sh`), a required check, runs its fixed script once under the
  JIT and once as the native binary, and fails on a trimming or AOT warning or on any difference between the two
  outputs. The JIT run turns on the RDG and turns off reflection-based serialization and dynamic code, as `PublishAot`
  does, so that the outputs differ only where native AOT changes something: a branch guarded by
  `RuntimeFeature.IsDynamicCodeSupported` takes the same side in both runs, while a `MakeGenericType` left unguarded
  still runs under the JIT and fails the native binary. A value object added to `UnderlyingTypes.cs`, or registered by
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

Beside them, outside the solution, `tests/Compat` is the **compatibility island**: the twenty-five packages exactly as
packed, installed from `artifacts/packages` at the one version just built into `net11.0` applications on the .NET 11
release candidate. Its main project runs the generator in that SDK's compiler, Entity Framework Core 11 on SQLite, SQL
Server and PostgreSQL 17, System.Text.Json source generation, ASP.NET Core model binding, on System.Text.Json and on
Newtonsoft.Json through `Microsoft.AspNetCore.Mvc.NewtonsoftJson` 11, minimal API problem details,
`Microsoft.AspNetCore.OpenApi` 11 over `Microsoft.OpenApi` 3, Dapper, MongoDB.Driver on MongoDB 8 with a collection
validator and entity identifiers minted on insert, MessagePack, with SignalR's MessagePack hub protocol 11 between a hub
and the .NET client over TestHost and MessagePack's analyzer in that SDK's compiler, FluentValidation, Newtonsoft.Json,
Serilog, Microsoft.Extensions.AI tools and structured output, through `FunctionInvokingChatClient` and `ChatResponse<T>`
of Microsoft.Extensions.AI 10, Model Context Protocol tools on ModelContextProtocol 2.2, a server in process and the SDK's
own client over a pair of pipes, `XmlSerializer` and
`DataContractSerializer` over its domain, which opts into XML serialization, the contract kit and the test-data sampler,
through AutoFixture, Bogus and FsCheck too, with no transitive
pinning, so the dependency floors of the packages meet the next
major as an application's would. `AdCodicem.ValueObjects.Swashbuckle` is installed by a second project, `tests/Compat/Swashbuckle`,
which the main one excludes from its sources: Swashbuckle 10 over `Microsoft.OpenApi` 2, on which it is built, since it
fails on the `Microsoft.OpenApi` 3 the main project's `Microsoft.AspNetCore.OpenApi` 11 brings. `ci.yml`'s
`compat (.NET 11)` job runs both against the packages its build job packed; it is not a required check until .NET 11 ships, and is
measured by no coverage. Without Docker its 21 container tests fail rather than skip, so leave them out explicitly
(Commands above). Tools that walk the repository rather than the solution do see it: CodeQL downloads its SDK, and
GitHub's automatic dependency submission restores it with SDK 10 from the root, which is why its project files leave
themselves empty on an SDK that cannot target `net11.0`. The suites above are still the four; the island checks the
packages, not the code.

`AdCodicem.ValueObjects.Testing` ships a contract kit (`ValueObjectContract`) that consumers point at their own
types; the unit tests use it on every generated value object of `Domain/` but four. `Floor` and `Celsius` cannot
satisfy it: their formatting hooks write text such as `floor 3` or `21 °C`, which does not parse back, and the kit
requires a text round trip. `Strongbox.Secret` and `Archive.Shelf` are private, and no public contract class can
name them; `DeclarationContextTests` covers them instead, with the protected `StrongboxId`. Every unit contract
overrides `DerivesRejectedValues` to return `true`, so the kit also checks the values its schema rules out, which
`AdCodicem.ValueObjects.Testing.Data` derives; a type whose normalizer clamps into its rules would fail it. Add a
contract with each value object added to `Domain/`, opted in the same way.

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
