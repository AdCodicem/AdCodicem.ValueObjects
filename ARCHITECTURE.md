# Architecture

The "where do I start reading" map. It describes the current shape; the reasoning behind that shape lives in
[Design decisions](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/design-decisions) on the documentation
site, and the decisions that were costly to reverse are recorded in [`docs/adr/`](docs/adr/).

## What the repository ships

Twenty-five NuGet packages for single-value DDD value objects. A `readonly partial struct` marked `[ValueObject<T>]`
gets its whole implementation from a Roslyn incremental generator, and crosses every boundary as its underlying
type: an IBAN is a JSON string, a `VARCHAR`, and a query-string parameter — never an object wrapper. Consumers
define their own value objects; this repository ships the frame.

## Layout

```
src/          the twenty-five shipped packages
tests/        four suites with distinct jobs (see below)
  NativeAot/  applications CI publishes with native AOT and compiles an EF Core model for
  Compat/     the packed packages in .NET 11 applications, outside the solution
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
                     ├─► XmlSerializableEmitter (an assembly marked [assembly: ValueObjectXmlSerialization] only)
                     └─► RegistrationEmitter ──► [ModuleInitializer] populating ValueObjectRegistry
```

`Model/UnderlyingType.cs` is the closed table of the 22 supported underlying types, and drives nearly every
per-type decision the emitters make. `RegistrationEmitter`'s module initializer is why descriptors are
available without a consumer registering anything. `XmlSerializableEmitter` writes an explicit `IXmlSerializable` and
a schema provider whose members each call `ValueObjectXml`, in the contracts, which reads through the type's rules:
the flag is read off the compilation and joins the model.

Alongside the generator, two analyzers enforce what the generator cannot: `VO0010` makes `default(T)` a build
error, and `VO0011` catches a hook rule written without its interface.
`VO0033` reports a value object the Request Delegate Generator cannot see as parsable, and its code fix lives in
`AdCodicem.ValueObjects.CodeFixes`, an assembly of its own: a code fix needs the workspace layer, which the compiler
does not load with an analyzer.

## Two ways to reach a value object

This distinction is where bugs hide, so it is worth knowing before changing anything.

- **Typed path** — the static abstract members of `IValueObject<TSelf, TValue>` (`Create`, `TryCreate`,
  `TryParse`, `Normalize`, `Validate`). Domain code and every generic integration use this. The ASP.NET binder,
  the EF Core converter, the Dapper handler and the MongoDB serializer are all closed over the concrete types at
  startup, so per-request work is fully typed and allocates nothing extra.
- **Boxed path** — `ValueObjectDescriptor`, resolved from `ValueObjectRegistry`, for callers that only know a
  `Type` at run time: `MustParseAs(Type)`, the OpenAPI transformer and the Swashbuckle filters, model-binder
  resolution. `descriptor.Accept`
  hands an `IValueObjectVisitor<TResult>` the type arguments back, so an integration closes its adapter at compile
  time rather than with `MakeGenericType`, which native AOT cannot run for a struct. The minimal API filter of
  `AdCodicem.ValueObjects.AspNetCore.Http` closes the check of each parameter it explains that way, the MongoDB
  provider the serializer the driver asks it for, over the serializer the driver holds for the underlying type, and the
  MessagePack resolver the formatter MessagePack asks it for, which writes through the options' formatter of the
  underlying type. The test-data sampler of `AdCodicem.ValueObjects.Testing.Data` draws a value of a type known only by
  its descriptor the same way, through the typed draw that reads `TSelf.Schema`, and so do its adapters: the AutoFixture
  specimen builder, Bogus's `RuleForValueObjects` and FsCheck's `MergeValueObjects`, which merges one arbitrary per
  type. An
  entity identifier's descriptor has its own visitor, `IEntityIdVisitor<TResult>`, whose `Visit<TId>` reaches
  `TId.New()`: the MongoDB identifiers package closes its id generators through it. The
  Serilog integration closes nothing and visits no descriptor: it reads a value Serilog has already boxed through the
  `IValueObject` marker, and only reads the type of each descriptor the registry holds, which it hands Serilog as a
  scalar type. Neither does the Microsoft.Extensions.AI integration: it tells a value-object parameter of a tool by
  `ValueObjectRegistry.IsValueObject`, and reads each argument through the contract the tool's serializer options hold
  for the parameter, as the tool's own binding does; its argument check, `src/Shared/ValueObjectArguments.cs`, names no
  type of Microsoft.Extensions.AI, and the Model Context Protocol integration links it too. That one keeps the tools
  the SDK's own, which the SDK alone knows how to describe to an older client, and checks their arguments in a
  call-tool filter placed after every filter the server's options are configured with.

The unit tests exercise the typed path, so a defect confined to the descriptor is invisible to them. That is
how the descriptor once flattened every rejection into a generic `not_parsable`, discarding the rule that
actually fired. `DescriptorTests.cs` covers that surface.

## Invariants

- **Normalize, then validate, then assign** — a non-default instance is by construction normalized and valid.
  The exceptions are the EF Core and Dapper read paths, which use `CreateUnchecked` because they read values this
  same application already validated. `ConfigureValueObjects(strict: true)` turns validation back on for EF Core;
  Dapper validates only a column the value object cannot have written: text read into a value object over another
  type, or a number read into one over `string`. MongoDB reads the other way round, strict unless the serializer is
  trusted (`ValueObjectBson.Register(trusted: true, …)`), since a collection has no schema and is often written by
  more than one program; so does MessagePack, strict unless trusted (`WithValueObjects(trusted: true)`), since the
  bytes may come from another service or a client.
- **Rejection is not an exception on a boundary.** `ValidationResult` is a struct that allocates nothing on success. The
  integrations go through `TryCreate` or `TryParse` and report a refusal in their own terms: a `JsonException` or
  `JsonSerializationException`, a model state error, the validation problem of a minimal API, a FluentValidation
  failure, a Dapper `DataException`, a MongoDB.Driver `FormatException` or `BsonSerializationException`, a MessagePack
  `MessagePackSerializationException`, an `XmlException` from `XmlSerializer` or `DataContractSerializer` in an assembly
  marked `[assembly: ValueObjectXmlSerialization]`, the result of a Microsoft.Extensions.AI tool, which a model reads as
  it is, a Model Context Protocol tool execution error carrying the code. The one that throws `ValueObjectException` is
  a strict EF Core read, which goes through `Create` and fails the query; `Create`, `Parse` and an explicit conversion
  throw it for code that treats a rejected value as a bug. The [error reference](website/docs/reference/errors.md) names
  what each integration throws. Validation is fail-fast: the first violated rule wins.
- **Rules are declared once.** `MaxLength = 34` validates, sizes the EF column, and becomes the OpenAPI
  `maxLength`. Anything added to `[ValueObject<T>]` should feed all three. A hook can feed the schema too: the
  `[GeneratedRegex]` behind `IValueObjectPatternValidator` validates, and its text, read off the attribute at
  compile time, becomes the OpenAPI `pattern`. It replaces the deprecated `Pattern` option, which builds its
  `Regex` at run time because one source generator cannot see another's output;
  [ADR-0007](docs/adr/0007-deprecate-pattern-for-a-source-generated-regex-hook.md) records why. The built-in stack's
  transformer and the Swashbuckle filters describe a value object through one source file both packages compile,
  `src/Shared/ValueObjectOpenApiSchema.cs`, so the two documents cannot drift apart. The XSD an assembly opted into XML
  serialization publishes reads the same schema, its pattern through `src/Shared/ValueObjectPatternSyntax.cs`, the
  reader of a .NET pattern each package writing one in another dialect links, which refuses what it cannot read with
  certainty. The MongoDB package links it too, and writes the pattern in PCRE2 for the `$jsonSchema` collection
  validator it builds from `TSelf.Schema`, where `MaxLength` becomes `maxLength` once more: each rule only where the
  server refuses no value the type accepts. The test-data sampler links it as well, and draws strings from the tree; it
  draws every test value from the same schema, lengths, pattern, bounds and known values, and keeps a candidate only once
  the type's own rules accept it, so a test never restates a rule the type declares. The JSON Schema core of the JSON
  package, `ValueObjectJsonSchema`, reads it once more for a language model, and the AI package carries what it writes
  to the schemas of Microsoft.Extensions.AI tools and structured output, the Model Context Protocol package to the input
  and output schemas of a server's tools.

## Testing

| Suite | Job |
|---|---|
| `UnitTests` | Behaviour of generated code, over value objects defined in `Domain/` — one per underlying type and per option or hook — and of every integration package called directly, a SignalR hub over TestHost and collections of value objects on SQLite in memory included; four fixture assemblies hold what the test assembly cannot, value objects of an assembly that opts into XML serialization among them. Its property-based tests draw the values each type accepts with the test-data sampler, through the FsCheck package's arbitraries, the AutoFixture, Bogus and FsCheck packages fill objects holding value objects, every contract of `Domain/` checks the values its schema rules out, and Microsoft.Extensions.AI tools are called by a scripted model, the OpenAI adapter's strict-mode request captured with no network. |
| `GeneratorTests` | The generator itself: emission, every diagnostic, hook detection, the analyzers, incremental caching, and every published documentation snippet. |
| `IntegrationTests` | Real PostgreSQL and SQL Server via Testcontainers, asserting against `information_schema`, a collection of value objects included, plus the API surface end to end; and a real MongoDB server, storing every underlying type MongoDB.Bson can represent as its primitive does, refusing the two 128-bit integers, answering each LINQ and `Builders` shape over value objects, applying the `$jsonSchema` validator built from the rules, and minting entity identifiers on insert. |
| `RdgTests` | Minimal API endpoints whose binding the Request Delegate Generator writes, over value objects declared in the endpoints' own project, which list their contract (`VO0033`), and the problem details their refusals are answered with. |
| `tests/NativeAot` | Not a suite: an application referencing every AOT-compatible package, run under the JIT and as a native AOT binary by the `native AOT` job of `ci.yml`, a required check, which fails on a trimming or AOT warning or on any difference between the two outputs; and an EF Core model compiled with `dotnet ef dbcontext optimize`, taken on a round trip through SQL Server and published for native AOT. |
| `tests/Compat` | Not a suite of the solution: the packages exactly as packed, installed into `net11.0` applications on the .NET 11 release candidate, the Swashbuckle package into one of its own, with its own `global.json` and package versions. Run by the `compat (.NET 11)` job of `ci.yml`, informational until .NET 11 ships. |

`GeneratorTests` drives Roslyn directly through `Harness/GeneratorHarness.cs` rather than through
`Microsoft.CodeAnalysis.Testing`, which binds to xUnit v2. Its snippets compile **without** implicit usings,
which is what catches unqualified names in emitted code. The harness also runs the framework's regex generator,
which the `CopyRegexGenerator` target copies from the targeting pack the SDK resolved, so a snippet implementing
`IValueObjectPatternValidator` compiles. Its incrementality tests assert on
`IncrementalStepRunReason` — the only way to notice a caching regression, which otherwise breaks nothing
visible while making every IDE keystroke re-run the pipeline.

[ADR-0006](docs/adr/0006-coverage-is-a-signal-not-a-goal.md) records what coverage a pull request owes, what stays
uncovered and why, and the two things Codecov's figure cannot show: a package no test loads, and the code the
generator emits.

## Releases

A merge publishes nothing by itself. `preview.yml` publishes a preview of every package each week, and on
dispatch, when a package input changed since the version nuget.org has; a stable release is a manual action. See
[ADR-0003](docs/adr/0003-hybrid-release-manual-stable-continuous-preview.md) for the stable track,
[ADR-0009](docs/adr/0009-publish-previews-weekly-when-a-package-input-changed.md) for the previews, and
[`docs/maintaining.md`](docs/maintaining.md) for the one-time settings the release and publish workflows
depend on. [ADR-0004](docs/adr/0004-pin-the-supply-chain-by-digest-not-nuget-lock-files.md) records how the
build's own dependencies are pinned, and why NuGet lock files are not part of it.
[ADR-0005](docs/adr/0005-version-the-documentation-site.md) records how the documentation site follows the same
two tracks: every `preview.yml` run redeploys the preview pages, and each stable release freezes its own.
[ADR-0010](docs/adr/0010-version-every-package-in-lockstep-independently-of-dotnet.md) records the versioning
policy: one version for the twenty-five packages, never aligned with .NET, and a framework's next major supported in
the same packages.
