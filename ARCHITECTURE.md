# Architecture

The "where do I start reading" map. It describes the current shape; the reasoning behind that shape lives in
[Design decisions](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/design-decisions) on the documentation
site, and the decisions that were costly to reverse are recorded in [`docs/adr/`](docs/adr/).

## What the repository ships

Thirty-four NuGet packages for single-value DDD value objects. A `readonly partial struct` marked `[ValueObject<T>]`
gets its whole implementation from a Roslyn incremental generator, and crosses every boundary as its underlying
type: an IBAN is a JSON string, a `VARCHAR`, and a query-string parameter — never an object wrapper. Consumers
define their own value objects; this repository ships the frame.

## Layout

```
src/          the thirty-four shipped packages
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

Alongside the generator, analyzers enforce what the generator cannot: `VO0010` makes `default(T)` a build error, and
`VO0011` catches a hook rule written without its interface.
`VO0033` reports a value object the Request Delegate Generator cannot see as parsable, and its code fix lives in
`AdCodicem.ValueObjects.CodeFixes`, an assembly of its own: a code fix needs the workspace layer, which the compiler
does not load with an analyzer. `VO0039` to `VO0042` check the DataAnnotations attributes of the contracts where they
sit: `[ParsesAs]` on a member that holds no text or naming no value object, `[NotDefault]` on a member that is no value
object, and, under `PublishAot`, which the package's props make visible to the analyzer, a `[ParsesAs(typeof(T))]`
naming a value object native AOT finds only once registered. `VO0043` and `VO0044` report a serializer's contract
attribute on a value object the generator writes, which makes that serializer describe it by the generator's members: it
loses the value, or reads it without the rules. Their code fix, beside `VO0033`'s, names protobuf-net's serializer or
removes the attribute.
`VO0045` and `VO0046` report a value object classified as personal data that a `[LoggerMessage]` method of
Microsoft.Gen.Logging, Microsoft.Extensions.Telemetry's logging generator, writes in clear, since that generator reads a
classification on a parameter or a property only, and a transitive expansion that drops a property's classification.
They read generated code, the logging generator's `[GeneratedCode]` being merged onto the author's method, and only a
method that generator implements. Their code fix, beside the others, copies the type's classification where the logging
generator reads it.
`VO0047` warns on `Unicode = false` where the rules the type declares let a character outside ASCII through, which SQL
Server replaces silently in a `varchar` column: no rule, a pattern not anchored at both ends or admitting such a
character, a known value spelled outside ASCII, a closed set compared as a culture compares. It reads the pattern
through the shared pattern reader, `src/Shared/ValueObjectPatternSyntax.cs`, which the generator links for it, and is an
analyzer rather than a diagnostic of the generator so that `#pragma` and a severity setting silence it, for a column of
names kept under Latin-1 on purpose.

## Two ways to reach a value object

This distinction is where bugs hide, so it is worth knowing before changing anything.

- **Typed path** — the static abstract members of `IValueObject<TSelf, TValue>` (`Create`, `TryCreate`,
  `TryParse`, `Normalize`, `Validate`). Domain code and every generic integration use this. The ASP.NET binder,
  the EF Core converter, the Dapper handler and the MongoDB serializer are all closed over the concrete types at
  startup, so per-request work is fully typed and allocates nothing extra.
- **Boxed path** — `ValueObjectDescriptor`, resolved from `ValueObjectRegistry`, for callers that only know a
  `Type` at run time: `MustParseAs(Type)`, DataAnnotations' `[ParsesAs(typeof(T))]`, which finds its descriptor as
  `MustParseAs` does, but the first time a value is validated rather than when the attribute is created, the OpenAPI
  transformer and the Swashbuckle filters, which describe a member carrying `[ParsesAs]` as the text of its value
  object, model-binder resolution. `descriptor.Accept` hands an `IValueObjectVisitor<TResult>` the type arguments back,
  so an integration closes its adapter at compile time rather than with `MakeGenericType`, which native AOT cannot run
  for a struct. The minimal API filter of
  `AdCodicem.ValueObjects.AspNetCore.Http` closes the check of each parameter it explains that way, the Azure Functions
  converter the conversion of each value object a parameter binds from the worker's text, and its middleware the check
  of each it explains, through `src/Shared/ValueObjectBindingText.cs`, which both Functions packages compile, the MongoDB
  provider the serializer the driver asks it for, over the serializer the driver holds for the underlying type, and the
  MessagePack resolver the formatter MessagePack asks it for, which writes through the options' formatter of the
  underlying type, protobuf-net's `AddValueObjects` the closed serializer type of each value object it registers, named
  with `typeof` over the visitor's type arguments, which writes through protobuf-net's own serializer of the underlying
  type, the Orleans codec selector the codec Orleans asks it for, which writes through Orleans' own codec of the
  underlying type, the HotChocolate type discovery handler the scalar of each value object a schema meets, the
  HotChocolate.Data type interceptor the operations of each value-object member a filter input meets, the NJsonSchema
  package the System.Text.Json converter through which it writes a value object's examples, bounds and known values as
  the Newtonsoft.Json settings of a schema write them, DataAnnotations' `[NotDefault]` its check of
  the default instance, closed once per type, and the Blazor input the two text adapters of the value object it binds,
  plain and nullable, which it finds as `[ParsesAs(typeof(T))]` does, through a visitor that is not generic, so that a
  native binary holds one closing per value object. The JSON Patch adapters of `TryApplyTo`, in
  `AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson`, tell a value object by `IsValueObject` and read its values through
  the Newtonsoft.Json converter and its dictionary keys through the descriptor's `TryParse`; the adapter of a dictionary
  is closed over its two types with `MakeGenericType`, as JSON Patch closes its own, since a visitor would name the value
  object and never the dictionary's other type, and MVC's JSON Patch runs in no native binary. The NJsonSchema package's reflection service and schema processor find a
  value object NJsonSchema meets through `TryResolve`, as the OpenAPI packages do. The
  test-data sampler of `AdCodicem.ValueObjects.Testing.Data` draws a value of a type known only by
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

- **Normalize, then validate, then assign** — a non-default instance is by construction normalized and valid. The
  exceptions are the EF Core and Dapper read paths, which use `CreateUnchecked` because they read values this same
  application already validated, and the temporary value of an integer key EF Core generates, which the database
  replaces and nothing writes; a GUID key it generates goes through `Create`. `ConfigureValueObjects(strict: true)`
  turns validation back on for EF Core; Dapper validates only a column the value object cannot have written: text read
  into a value object over another type, or a number read into one over `string`. MongoDB reads the other way round,
  strict unless the serializer is trusted (`ValueObjectBson.Register(trusted: true, …)`), since a collection has no
  schema and is often written by more than one program; so do MessagePack, strict unless trusted
  (`WithValueObjects(trusted: true)`), protobuf-net, strict unless trusted (`AddValueObjects(trusted: true, …)`, a
  trusted profile), and Orleans, strict unless trusted (`AddValueObjects(trusted: true)` on the serializer of a silo or
  a client), since the bytes may come from another service or a client.
- **Rejection is not an exception on a boundary.** `ValidationResult` is a struct that allocates nothing on success. The
  integrations go through `TryCreate` or `TryParse` and report a refusal in their own terms: a `JsonException` or
  `JsonSerializationException`, a model state error, a JSON Patch document's keyed by the path of its operation, the
  validation problem of a minimal API, a FluentValidation failure, a Dapper `DataException`, a MongoDB.Driver
  `FormatException` or `BsonSerializationException`, a MessagePack
  `MessagePackSerializationException`, a protobuf-net `ProtoException`, an Orleans `SerializerException`, carried back
  to the caller of a grain, the `INVALID_ARGUMENT` a gRPC service answers,
  through the protobuf-net package's code-first binder or the gRPC package's interceptor and collector, whose
  `google.rpc.ErrorInfo` or `google.rpc.BadRequest` carries the code, an `XmlException` from `XmlSerializer` or `DataContractSerializer` in an assembly
  marked `[assembly: ValueObjectXmlSerialization]`, the result of a Microsoft.Extensions.AI tool, which a model reads as
  it is, a Model Context Protocol tool execution error carrying the code, the `FormatException` that fails an Azure
  Functions parameter, whose text alone the worker keeps and which therefore ends with the code, the validation
  problem of an HTTP trigger of the Functions ASP.NET Core integration, a HotChocolate `LeafCoercionException`, whose
  GraphQL error carries the code as `extensions.code`, for an argument, a variable or a HotChocolate.Data filter
  operand, a DataAnnotations `ValueObjectValidationResult`, from `[ParsesAs]` and `[NotDefault]`, which `Validator`'s throwing methods wrap in a `ValidationException` that
  `ValueObjectErrors.TryGetCode` reads, and a validation message under a Blazor form's field, its code on the form's
  `EditContext`, which `TryGetValueObjectErrorCode` reads. Under `UseValueObjectCheckErrors()`, the
  database provider's error for a row a CHECK constraint from the rules refused carries the code too, read off the
  constraint's name, which ends with its rule. Of the integrations, the EF Core converter throws
  `ValueObjectException`, for a refused write and for a strict read, which goes through `Create` and fails the query,
  and the Orleans package rebuilds on the caller one that a grain lets escape, without `AttemptedValue`; `Create`,
  `Parse` and an explicit conversion throw it for code that treats a rejected value as a bug. The
  [error reference](website/docs/reference/errors.md) names
  what each integration throws. Validation is fail-fast: the first violated rule wins. The message of a rule stays
  English; its code keys the one an application shows in its users' language, `ValueObject.{code}`, which MVC, minimal
  APIs, the HTTP triggers of Azure Functions, a HotChocolate schema's error filter and the Blazor input read from the
  application's resource through `IStringLocalizer`, in one source file the five packages compile,
  `src/Shared/ValueObjectLocalizedMessages.cs`, the three ASP.NET Core ones with the part they share through
  `ValueObjectMessageOptions` beside it, FluentValidation from its `LanguageManager`, and the DataAnnotations attributes
  from the resource their `ErrorMessageResourceType` names alone, through a `ResourceManager` over it.
- **Rules are declared once.** `MaxLength = 34` validates, sizes the EF column, and becomes the OpenAPI
  `maxLength`; opted in, `AddValueObjectCheckConstraints()` writes the minimum length, the bounds, the closed sets
  and, in a dialect the options name, the patterns as CHECK constraints of the EF Core columns, the maximum length
  staying the column size. Anything added to `[ValueObject<T>]` should feed all of them, the CHECK constraints wherever
  the database reads the rule as .NET does. A hook can feed the schema too: the `[GeneratedRegex]` behind
  `IValueObjectPatternValidator` validates, and its text, read off the attribute at compile time, becomes the OpenAPI
  `pattern`, the pattern of a HotChocolate scalar, and, on request, the `pattern` attribute of a Blazor input, written
  for the `v` flag a browser compiles it with, only for a pattern anchored at both ends that a browser reads as .NET
  does. It replaces the deprecated `Pattern` option, which builds its `Regex` at run time because one source
  generator cannot see another's output;
  [ADR-0007](docs/adr/0007-deprecate-pattern-for-a-source-generated-regex-hook.md) records why. The built-in stack's
  transformer and the Swashbuckle filters describe a value object through one source file both packages compile,
  `src/Shared/ValueObjectOpenApiSchema.cs`, so the two documents cannot drift apart: a value object, and the text a
  member or a parameter holds to its rules under `[ParsesAs]`, described as a dictionary key is, a stricter rule its
  declaration states kept. The NJsonSchema package describes a value object on NJsonSchema's object model the same
  way, through a host file of its own over the same keyword table (`ValueObjectJsonSchemaDescription`), for NSwag's
  components and the JSON Schema Confluent's serdes register with the Schema Registry. The XSD an assembly opted into XML
  serialization publishes reads the same schema, its pattern through `src/Shared/ValueObjectPatternSyntax.cs`, the
  reader of a .NET pattern each package writing one in another dialect links, which refuses what it cannot read with
  certainty. The MongoDB package links it too, and writes the pattern in PCRE2 for the `$jsonSchema` collection
  validator it builds from `TSelf.Schema`, where `MaxLength` becomes `maxLength` once more: each rule only where the
  server refuses no value the type accepts, the `enum` of a closed set of text, as the `IN` of a CHECK constraint, only
  where the type stores no other spelling of a known value, by the comparison the schema states, `Schema.Comparison`,
  which the generator writes on every value object over text: never under a culture's
  (`src/Shared/KnownValueSpellings.cs`, linked by both packages). The test-data sampler links the reader as well, and draws strings from the tree; it
  draws every test value from the same schema, lengths, pattern, bounds and known values, and keeps a candidate only once
  the type's own rules accept it, so a test never restates a rule the type declares. The JSON Schema core of the JSON
  package, `ValueObjectJsonSchema`, reads it once more for a language model, and the AI package carries what it writes
  to the schemas of Microsoft.Extensions.AI tools and structured output, the Model Context Protocol package to the input
  and output schemas of a server's tools. A localised message quotes the same rules: `ValueObjectMessages`, in the
  contracts, reads the length, the bounds and the known values of `TSelf.Schema` as the arguments of the message of a
  code, formatted for the culture, so a translation never states a rule a second time. `Unicode = false` is no rule but
  how the text is stored, `Schema.IsUnicode`: the EF Core conventions map it to non-Unicode text, `varchar` on SQL
  Server, and the Dapper handler declares its parameters so; nothing else stores text that way, and `VO0047` asks the
  rules to hold the text to ASCII.

## Testing

| Suite | Job |
|---|---|
| `UnitTests` | Behaviour of generated code, over value objects defined in `Domain/` — one per underlying type and per option or hook — and of every integration package called directly, a SignalR hub, a protobuf-net.Grpc code-first service and a contract-first gRPC service over TestHost, Orleans grain calls through an in-process cluster over localhost, and collections of value objects, keys generated or refused when a `Guid` one is left unset, queries on `.Value` and CHECK constraints on SQLite in memory, the code of the rule a refused row broke among them, included; eight fixture assemblies hold what the test assembly cannot, value objects of an assembly that opts into XML serialization, value objects beside a query type HotChocolate's source generator binds, Blazor forms written in Razor markup, `[LoggerMessage]` methods Microsoft.Gen.Logging writes, beside value objects classified as personal data, and grains Orleans' code generator writes codecs and proxies for, among them. Its property-based tests draw the values each type accepts with the test-data sampler, through the FsCheck package's arbitraries, the AutoFixture, Bogus and FsCheck packages fill objects holding value objects, every contract of `Domain/` checks the values its schema rules out, Microsoft.Extensions.AI tools are called by a scripted model, the OpenAI adapter's strict-mode request captured with no network, the Azure Functions converter and problem details middleware run over the worker's own abstractions, HotChocolate schemas built in process and over TestHost take every value object of `Domain/` as a scalar of its own, and HotChocolate.Data filters every one through its scalar on SQLite and in memory, the DataAnnotations attributes hold every value object's text to its rules through `Validator`, options validation, `ObservableValidator`, MVC and `AddValidation()`, NJsonSchema's generators, NSwag's documents over TestHost and Confluent's serdes over a substitute registry client describe every value object as its underlying type, what each serializer writes of it validating against the schema, and Blazor forms, through bUnit and, statically rendered, over TestHost, read every value object's text through its rules. |
| `GeneratorTests` | The generator itself: emission, every diagnostic, hook detection, the analyzers, incremental caching, and every published documentation snippet. |
| `IntegrationTests` | Real PostgreSQL and SQL Server (2022, and 2025 for `REGEXP_LIKE`) via Testcontainers, asserting against `information_schema`, a collection of value objects included, a prefix search on `.Value` reading the column itself, keys typed as value objects generated as each provider generates a key of their underlying type, and the CHECK constraints written from the rules accepting every value each type accepts and refusing another writer's row, and a row the context writes with the code of its rule, in every language SQL Server writes its messages in, patterns included, a value object declaring `Unicode = false` stored in `varchar` and compared through `varchar` parameters from EF Core and Dapper, an Always Encrypted `varchar` column included, the audit of the rows a migration to it converts and the migration of a key, HotChocolate.Data's filters over value objects translated to their columns, plus the API surface end to end; and a real MongoDB server, storing every underlying type MongoDB.Bson can represent as its primitive does, refusing the two 128-bit integers, answering each LINQ and `Builders` shape over value objects, applying the `$jsonSchema` validator built from the rules, and minting entity identifiers on insert; and the Azure Functions host in Microsoft's `dotnet-isolated` image, with Azurite, binding route values, query values and queue binding data through the rules and answering a refused HTTP value with its code. |
| `RdgTests` | Minimal API endpoints whose binding the Request Delegate Generator writes, over value objects declared in the endpoints' own project, which list their contract (`VO0033`), the problem details their refusals are answered with, and the DataAnnotations attributes validated by `AddValidation()` over that binding. |
| `tests/NativeAot` | Not a suite: an application referencing every AOT-compatible package but the two HotChocolate ones, the protobuf-net one and the Orleans one, which Orleans' own failure to start natively keeps out, a Blazor form of every registered value object rendered through `HtmlRenderer` among its scenarios, run under the JIT and as a native AOT binary by the `native AOT` job of `ci.yml`, a required check, which fails on a trimming or AOT warning but the two Microsoft.AspNetCore.Components reports for itself, or on any difference between the two outputs; protobuf-net's compile-time model, in an application of its own run the same way, which fails on any warning but the ones its generated code is known to raise; and an EF Core model compiled with `dotnet ef dbcontext optimize`, taken on a round trip through SQL Server and published for native AOT. |
| `tests/Compat` | Not a suite of the solution: the packages exactly as packed, installed into `net11.0` applications on the .NET 11 release candidate, the Swashbuckle package into one of its own, with its own `global.json` and package versions. Run by the `compat (.NET 11)` job of `ci.yml`, informational until .NET 11 ships. |

`GeneratorTests` drives Roslyn directly through `Harness/GeneratorHarness.cs` rather than through
`Microsoft.CodeAnalysis.Testing`, which binds to xUnit v2. Its snippets compile **without** implicit usings,
which is what catches unqualified names in emitted code. The harness also runs the framework's regex generator,
which the `CopyRegexGenerator` target copies from the targeting pack the SDK resolved, so a snippet implementing
`IValueObjectPatternValidator` compiles, and on request Microsoft.Gen.Logging, which the `CopyLoggingGenerator` target
copies from the Microsoft.Extensions.Telemetry.Abstractions package `Directory.Packages.props` pins, so a test of
`VO0045` and `VO0046` reads the tags the logging generator writes. Its incrementality tests assert on
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
policy: one version for the thirty-four packages, never aligned with .NET, and a framework's next major supported in
the same packages.
