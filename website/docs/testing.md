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
  contracts, and eight small fixture assemblies hold what the test assembly cannot: a generated value object whose
  module has not been used yet, annotated value objects in an assembly the generator does not run on,
  generated value objects in an assembly that does not reference the JSON package, generated value objects of
  every underlying type in an assembly that opts into XML serialization, generated value objects beside a query
  type HotChocolate's source generator binds, Blazor forms written in Razor markup, `[LoggerMessage]` methods
  Microsoft.Gen.Logging writes, beside value objects classified as personal data, and grains whose codecs and proxies
  Orleans' code generator writes; a ninth, a Wolverine.Http application, runs as a child process, Wolverine's dynamic
  mode compiling its endpoints' code with a Roslyn this suite does not take. What needs a server and no container runs
  here too, over TestHost: ASP.NET Core's
  pipelines, a SignalR hub talking MessagePack with the .NET client, and a protobuf-net.Grpc code-first service and a
  contract-first gRPC service with the .NET gRPC client; over localhost, an Orleans cluster in process, a silo and its
  client, and two silos; and
  over SQLite in memory: collections of value objects through Entity Framework Core, every sample value object among
  them stored and read back, queries over their elements translated, queries on `.Value` translated to the column,
  for every sample value object as the same query over its underlying type is, and the CHECK constraints written from
  the rules: each rule written or left out per provider, SQLite taking every value at the boundaries of the rules it
  writes and refusing another writer's row, the names, scripts and shapes of table the constraints come in, and the
  patterns, written in each dialect or failing the model on a construct or an option the database would read apart,
  the writer checked construct by construct against what .NET makes of the same pattern, and the code of the rule a
  refused row broke, on SQLite's error and on errors shaped as PostgreSQL's and SQL Server's, every name the convention
  writes read back to its rule, SQL Server's message in each language it is translated into, a name read past each
  separator a message may put around it, and a refusal reported while the results of a batch are read, which only a
  save interceptor sees.
  Property-based tests run the laws `IValueObject<TSelf, TValue>` states over generated input, the values each type
  accepts drawn by the [test-data sampler](./how-to/test-value-objects.md#generate-valid-values) from the rules it
  declares, through the arbitraries of the FsCheck package, and the test-data sampler itself draws a value of every
  sample value object. The AutoFixture, Bogus and FsCheck packages fill objects holding value objects in every shape a
  member takes. Microsoft.Extensions.AI builds a tool over every sample value object, alone, nullable and in a list,
  whose schema and whose answer to a refused argument are checked, and a scripted model stands for a real one: no test
  calls a model, and the request the OpenAI adapter sends in strict mode is captured before it leaves. A Model Context
  Protocol server lists a tool over every sample value object to the SDK's own client, over a pair of pipes in process,
  and answers each refused argument with a tool execution error carrying its rule. The Azure Functions converter is
  called as the isolated worker calls it, over every sample value object, and the problem details middleware runs over
  invocations written by the test, through the worker's own abstractions, answering each refusal as a minimal API
  answers it, in the request's language too. HotChocolate schemas built in process take every sample value object as a
  scalar of its own, from the edges its rules declare to the values they rule out, as literals and variables, and answer
  each refusal with its code, in the language of the execution and, over TestHost, of the request. HotChocolate.Data's
  filters over value objects run on SQLite and in memory, every sample value object's `eq`, `in` and `gt` (its `eq`
  alone over `bool`) answering as the same comparisons of its instances do, and, but for the 128-bit integers, as SQLite
  runs them on a column of the underlying type, with the text
  operations, the refusals quoted by the scalar's name and what the package leaves to the application. Blazor forms rendered through bUnit read every sample value object's text through its
  rules, alone and nullable, the text an input writes reading back to its instance and each value its rules rule out
  refused with its code, in each culture the renderer calls for and in French from a resource, and the text it writes
  reads back in every culture .NET lists, those whose calendar is not the Gregorian one included; the static and the
  prerendered pages of the Razor fixture are served over TestHost, the framework's own messages pinned, and the patterns
  an input writes for browsers are checked against .NET here and against Node in the build job.
  System.CommandLine commands parsed and invoked in process read every sample value object through an option, its
  nullable type, a list and an argument, the text of each instance and of each edge of its rules reading back to it and
  each value its rules rule out refused with its code on the parse error, every known value completed as text that
  reads back, with flags, cultures, a response file and messages in French from a resource. CsvHelper writes every
  sample value object as a column and reads it back, alone and nullable, in the invariant culture and in French, and
  the converter reads back the text it writes in every culture whose calendar is not the Gregorian one, each value its
  rules rule out refused with its code on CsvHelper's exception, and its default written only where its rules accept
  it, with class maps, defaults, the registration-order traps and CsvHelper's own behaviours pinned. Marten, over a
  store that never connects, translates a comparison of every sample value object, of its nullable type and a `Contains`
  over its values into the SQL and the parameters of the same query over its underlying type, and plans each compiled
  query shape, binding its collections and refusing what Marten would freeze; the Marten members and planning steps the
  package relies on, internal ones included, are pinned, so that a Marten release moving one fails here first.
  Wolverine.Http hosts built in process, without Wolverine's runtime compiler, show the endpoints Wolverine builds once
  the package claims their value objects: where each value is read from and under which name, what is declined, the
  refusal answered before any middleware method, the form marking antiforgery reads, the descriptions ApiExplorer and
  both OpenAPI documents give, and the code Wolverine writes for every family of value object, compiled; the helpers
  that code calls bind every sample value object as its `TryParse` reads it and answer its refusal as a minimal API
  does; and the fixture application answers a script of requests through the code Wolverine compiles, lenient, strict
  and in French. The
  DataAnnotations attributes hold the text of every sample value object to its rules, in both forms, and refuse each
  default instance, through `Validator`, options validated as a host starts and by the validator `[OptionsValidator]`
  writes, CommunityToolkit.Mvvm's `ObservableValidator`, MVC and a minimal API under `AddValidation()` over TestHost,
  with their messages in French from a resource, and both OpenAPI documents describe each value object's text as the
  other does. Newtonsoft.Json given `ValueObjectContractResolver` alone writes and reads every underlying type as it
  does with the converter in its settings, a generic construction, value objects written by hand and one carrying
  `[JsonObject]` included, keeps the converter a type names, leaves the contracts of the resolver
  it wraps as they were, and converts the values of a JSON Patch document applied outside MVC. JSON Patch documents
  applied through `TryApplyTo`, over TestHost and through its handler, read each value through its rules in every
  operation, on members, nullable members, lists, arrays and dictionaries keyed by value objects or holding them, every
  sample value object among them read as a body reads it, and report each refusal with its code under the path of its
  operation, beside what JSON Patch's own `ApplyTo` does. NJsonSchema's two
  generators describe every sample value object as its
  underlying type on each wire their settings describe, and what each serializer writes of it, from the edges its rules
  declare to values drawn from them, validates as Confluent's serializer validates a message; NSwag's documents over
  TestHost, on System.Text.Json, on Newtonsoft.Json and in Swagger 2.0, describe each value object and each text, the
  System.Text.Json one as the built-in stack's document does but for a bound no decimal holds, and a C# client
  generated from one compiles; Confluent's serdes, over a substitute registry client, register the rules and refuse a
  tampered message with its code. Keys typed as value objects are
  generated over a `Guid` and each integer type, a round trip through SQLite in memory included, a `Guid` one left unset
  is refused in every shape the convention concerns, and an integer key to generate is refused on Entity Framework
  Core's in-memory provider, which is not relational.
- **GeneratorTests** — the generator itself: emission, every diagnostic, hook detection, the analyzers, and
  incremental caching, the analyzer of the DataAnnotations attributes and its `PublishAot` gate included, the analyzer
  of serializers' contract attributes and its code fix, over protobuf-net itself and the other serializers' attributes
  declared under their real names, and the analyzer of data classifications held to the tags Microsoft.Gen.Logging, run
  beside the generator, writes, the analyzer of non-Unicode text held to what .NET's regular expressions match under
  each option, and the analyzer of Marten's compiled queries, over Marten's interfaces declared under their real names,
  which the unit suite runs over the real Marten and the identity query it pins. It drives
  Roslyn directly rather than through a testing harness that binds to an older
  xUnit, and it compiles snippets **without** implicit usings, which is what catches an unqualified name that
  slipped into emitted code. The incrementality tests assert on the Roslyn incremental step run reason — the
  only way to notice a caching regression, since losing incrementality breaks nothing visible while making
  every IDE keystroke re-run the pipeline.
- **IntegrationTests** — real PostgreSQL and SQL Server, asserting against `information_schema` that value
  objects reach the column types they claim, a collection of them the PostgreSQL array of its element's type, Dapper and
  EF Core round trips, a query over the elements of a collection included, `Guid` value-object keys generated as each
  provider generates a `Guid`, in the order the engine sorts them, and integer ones by the engine's identity column,
  a prefix search on `.Value` reading the column itself, with a parameter declared as the column, `.Value` of a
  value object over a type the provider converts, and the CHECK constraints written from the rules, on PostgreSQL and
  SQL Server 2022, taking every value each type accepts and refusing another writer's row, and a row the context
  writes with the code of the rule it broke, from `SaveChanges`, a refusal in a later statement of a batch included,
  `ExecuteUpdate`, raw SQL and a migration, in every language SQL Server writes its messages in, and with none on SQL
  Server for a raw query reading back the rows it writes, their patterns matched value by value as .NET
  matches them on PostgreSQL and on SQL Server 2025, a value object declaring `Unicode = false` in `varchar` and
  compared through `varchar` parameters from EF Core and Dapper, an Always Encrypted column included, beside the audit
  of the rows a migration to it converts and the migration of a key, HotChocolate.Data's filters over value objects
  translated to the column, a list's element and the text operations under the column's collation included, plus the
  API surface end to end; and a
  real MongoDB server, which stores a value object of every underlying type MongoDB.Bson can represent as the document
  its primitive writes, refuses one over `Int128` or `UInt128`, and answers each LINQ and `Builders` shape over them,
  `.Value` included; which, given the `$jsonSchema` validator built from the rules, refuses another writer's document
  and stores every value each type accepts, reads each pattern the validator publishes as .NET does, and mints an
  entity identifier for a document inserted without one. On PostgreSQL too, Marten: every underlying type queried as
  the primitive it replaces, each LINQ shape answering what the documents answer in memory, duplicated fields as columns
  of the underlying type, a computed index over a date serving the query, events, identities, compiled queries binding
  each call's collection and the forms `VO0048` names, a batch included, the identity a compiled query replays pinned,
  patches, and a stored value the rules refuse refused with its code however it is read. And the Azure Functions host,
  in Microsoft's `dotnet-isolated` image, running an application of the isolated worker with Azurite for its queues:
  route values and query values bound through the rules, each refusal answered with the minimal API's validation problem
  and its code, a `[FromBody]` one included, in the request's language, a failure no value object explains keeping the
  host's 500, the middleware registered before the integration keeping it too, with its warning, and a queue message's
  binding data bound beside the message its binding reads. These need a Docker daemon: Testcontainers starts the engines
  and the host for the run.
- **RdgTests** — minimal API endpoints whose binding the Request Delegate Generator writes, as it does in every
  build under `PublishAot` or `PublishTrimmed`, over value objects declared in the endpoints' own project: the case
  [the Request Delegate Generator](./how-to/aspnet-core.md#the-request-delegate-generator) cannot see the
  generator's output for. A route, query or header value binds, a refused one answers 400, an absent optional one
  binds `null`, and a test fails if the RDG did not write the binding, so that the suite cannot silently become one
  more test of the reflection-based binding. The same refusals are answered with the minimal API problem details, in
  each setting of `ThrowOnBadRequest`, where the RDG refuses no empty query text and takes an empty header for an
  absent one, and the DataAnnotations attributes are validated by `AddValidation()` over the binding the RDG writes,
  each refusal answered as the reflection-based binding answers it.

`AdCodicem.ValueObjects.Testing` ships the contract kit (`ValueObjectContract`) described in
[Test your value objects](./how-to/test-value-objects.md); the unit tests use it on every generated sample value
object but two, so the framework's own test suite is a live example of how a consumer would use it. `Floor` and
`Celsius` cannot satisfy it: their formatting hooks write text such as `floor 3` or `21 °C`, which does not parse
back, and the kit requires a text round trip. Every one of those contracts also asks the kit to derive the values its
schema rules out (`DerivesRejectedValues`), and checks that the type refuses each of them.

## Native AOT and compiled models

Four applications, built as an application builds them, are published rather than tested.

The first references every package that claims to be AOT-compatible: the contracts, the generated code, the JSON package
with a source-generated context and the JSON Schema it exports, the minimal API problem details, the identifiers,
FluentValidation, the Serilog integration, the Microsoft.Extensions.AI one, the Model Context Protocol one, the gRPC
one, the Blazor one, the System.CommandLine one and the Wolverine.Http one, all but
the two HotChocolate ones, whose framework warns on its own publish and starts natively only rooted, and whose
HotChocolate.Data filtering of a struct has no native code ([GraphQL](./how-to/graphql.md#native-aot)), and whose
refusal of a value object nothing registered the unit suite runs instead under the JIT, in a child process whose
runtime supports no dynamic code, the protobuf-net one, which the third publishes, the Marten one, which the fourth
publishes, the Orleans one, since Orleans does not start natively, and the CsvHelper one, whose CsvHelper record API has
no native code, whose refusals of a value object nothing registered the unit suite runs under the JIT too. Its
value
objects cover each of the 22 underlying types, a pattern hook, a
closed set, an identifier, a generic value object registered by hand, and two value objects written by hand, registered
without a converter, which the JSON factory serves its general-purpose one, beside a third that nothing registers, which
it refuses. It runs a fixed script over every one of them — the typed path, the descriptor, JSON, FluentValidation,
Serilog, each value object logged checked against the bare value it carries, the schema of every one in a tool, tools
built over the context answering refused arguments with their rule, a Model Context Protocol server in process listing
its tools on two protocol versions and answering refused arguments, and requests over Kestrel to minimal API
endpoints the Request Delegate Generator binds, refused values answered with problem details in each setting of
`ThrowOnBadRequest`, in English and with the messages of a resource read through the localizer, each answer checked
against the status, the codes and, translated, the messages the script expects, and the DataAnnotations attributes, both
forms and `[NotDefault]`, through `Validator`, the validator `[OptionsValidator]` writes and a minimal API under
`AddValidation()`, each refusal checked against its code, a Blazor form of every one of them through
`HtmlRenderer`, each text of its underlying type typed into its input, alone and nullable, and a System.CommandLine
command holding an option of every one of them, each text of its underlying type given to it alone and in an array, and
its nullable option given nothing and empty text, and Wolverine.Http endpoints in Wolverine's static mode, over the code
`codegen write` writes for them before both runs, each refused route, query, header or form value answered with its code
and no handler or `Before` method run for it — once under the JIT and
once as a native AOT binary. CI's `native AOT` job fails on any trimming or AOT warning in the publish, but the two
Microsoft.AspNetCore.Components reports for itself once a component renders, which the project lists in its
`aot-warnings.allowed`, and on any difference between the two outputs. It runs after every merge, on a pull request
labelled `ci:heavy`, and before every preview and stable release, which publish nothing unless it passes, so the claim
of AOT compatibility is run, not only analysed.

The second holds an Entity Framework Core context mapping every value object the conventions map, required and
optional, a generic one, an identifier as the key and collections of them, `Guid` and `int` keys it generates and a
`Guid` key it refuses to add unset, beside a strict context, both filtering on `.Value` and adding the CHECK
constraints from the rules. `dotnet ef dbcontext optimize` writes its compiled model on every pull request reaching it, which
the project then builds on; with the heavy jobs, the integration job takes it on a round trip through SQL Server, whose
tables `EnsureCreated` creates with the constraints, a row SQL writes breaking a rule refused with the rule's code, and
the native AOT job writes the model for native AOT, with its queries precompiled, and publishes it.
[Compiled models](./how-to/ef-core.md#compiled-models) says what holds there.

The third publishes protobuf-net's compile-time model over the value objects of the first one's domain whose underlying
type protobuf-net writes, twenty of the twenty-three, each naming its serializer on a partial declaration, and two more
over a `Guid` and a `decimal` written as text:
a message of all of them written as the same message over the primitives and read back, each value its rules refuse
refused with its code, a trusted profile reading one anyway, a field left out read as the default instance, and a
default refused on write, once under the JIT and once as a native binary. The model protobuf-net generates warns at
each call into a serializer, its annotations reaching the converters every value object nests: the job lets through
the warnings the project lists, `aot-warnings.allowed`, and fails on any other, this library's included
([protobuf-net](./how-to/protobuf-net.md#native-aot)).

The fourth runs a Marten document store against the PostgreSQL service container of `native AOT (Marten)`, a job of
its own beside `native AOT`, since the native AOT compiler alone takes about 20 minutes over Marten: documents
holding value objects of every family Marten maps, a nullable one, a construction of a generic value object and one
written by hand, both registered by hand, stored and read back as the bare values, each LINQ shape checked against what
the documents answer in memory, an identity, compiled queries binding a collection and refusing a single value object,
a value object nothing registered refused, and a stored value the rules refuse refused with its code, once under the
JIT and once as a native binary. Marten, JasperFx, Weasel and FastExpressionCompiler warn for themselves: the job lets
through the warnings the project lists, and fails on any other, this library's included
([Marten](./how-to/marten.md#native-aot)).

## The compatibility island

The four suites build and test the source. One more project tests the packages: `tests/Compat`, outside the solution,
installs the thirty-eight packages exactly as they were packed — from the folder the build packs into, at that one
version, never from nuget.org — into `net11.0` applications on the .NET 11 release candidate. It has its own SDK, its own package
versions and no transitive pinning, so the dependency floors of the packages meet the next major as they would in an
application. It runs the generator in that SDK's compiler, Entity Framework Core 11 on SQLite, SQL Server and
PostgreSQL, collections of value objects, queries on `.Value`, CHECK constraints from the rules on SQLite and a pattern
constraint on PostgreSQL included, a refused row carrying the code of its rule, System.Text.Json source generation, ASP.NET Core model binding on System.Text.Json and on Newtonsoft.Json,
a JSON Patch document through `TryApplyTo`, and a minimal API with its problem details, their messages and FluentValidation's in the language of the request, the
DataAnnotations attributes through `Validator`, options validation, the validator `[OptionsValidator]` writes, MVC 11 and
the minimal API validation of Microsoft.Extensions.Validation 11, its own message keys and its handling of an
attribute's exception included,
`Microsoft.AspNetCore.OpenApi` 11 over `Microsoft.OpenApi` 3, Dapper,
MongoDB.Driver on MongoDB 7.0 with a collection validator and entity identifiers minted on insert, Marten on PostgreSQL
17, its LINQ, a duplicated field and a compiled query's collection, MessagePack and SignalR's MessagePack hub protocol
11 over TestHost, protobuf-net on a runtime model and on its compile-time model and a protobuf-net.Grpc code-first
service over TestHost, a contract-first gRPC service answering through the gRPC package, Orleans' serializer,
FluentValidation, Newtonsoft.Json through its settings and its contract resolver, Serilog, Microsoft.Extensions.AI
tools and structured output, Model Context Protocol tools over the SDK's own client, the Azure Functions converter and
the problem details middleware beside ASP.NET Core 11, HotChocolate 16 scalars in a schema built in process, their
messages in the language of the execution, HotChocolate.Data filters over Entity Framework Core 11 on SQLite, a Blazor input rendered by `HtmlRenderer` on the Components of .NET 11, its
messages from a resource, System.CommandLine 3.0's release candidate over the package built against 2.0, CsvHelper 33.1
reading and writing value objects as columns, Wolverine.Http 6.51 endpoints in Wolverine's dynamic mode over TestHost,
an NSwag 14.7
document and Confluent's JSON serdes 2.16 over NJsonSchema,
`XmlSerializer` and
`DataContractSerializer` over a domain that opts into XML serialization, the contract kit, and the test-data sampler
drawing each value object of that domain from the schema the generator of that SDK wrote, through AutoFixture, Bogus and
FsCheck too. On Entity Framework Core 11, keys typed as value objects are generated over a `Guid` and an `int`, and a
`Guid` one left unset is refused. A second project installs
the Swashbuckle package, Swashbuckle 10 failing on the `Microsoft.OpenApi` 3 the first one's
`Microsoft.AspNetCore.OpenApi` 11 brings: there, Swashbuckle documents a minimal API over `Microsoft.OpenApi` 2, as in
an application that takes Swashbuckle alone.

CI's `compat (.NET 11)` job runs it with the heavy jobs: after every merge and on a pull request labelled `ci:heavy`,
against the packages that commit packs, and before every preview and stable release, against the very packages they
publish. It gates every stable release, previews once .NET 11 ships, and never a pull request
([ADR-0013](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/docs/adr/0013-run-a-targeted-pull-request-ci-and-the-heavy-jobs-before-a-release.md)).
The "On the next .NET" column of [Supported frameworks](./packages.md#supported-frameworks) states what it covers.

## Two ways to reach a value object — and why bugs hide in one of them

- **Typed path.** The static abstract members of `IValueObject<TSelf, TValue>` (`Create`, `TryCreate`,
  `TryParse`, `Normalize`, `Validate`). This is what domain code and the generic integrations use — the ASP.NET
  binder, the EF converter, the Dapper handler and the MongoDB serializer are all generic and closed over the
  concrete types at startup, so per-request work is fully typed and allocates nothing extra.
- **Boxed path.** A descriptor resolved from a runtime registry, for callers that only know a `Type` at run
  time — dynamic parsing, the OpenAPI transformer and the Swashbuckle filters, model-binder resolution.

Unit tests naturally exercise the typed path, so a defect confined to the descriptor can be invisible to them
unless the suite deliberately covers that surface too. That asymmetry is worth keeping in mind when adding a
test for a new rule: prove it holds on both paths, not just the one that's convenient to call from a unit test.

## Coverage

A pull request that runs tests reports their coverage through Codecov: 95 % of the lines it changes, and the
project's coverage down by half a point at most. It runs the unit tests of the package families it changes, and
uploads their coverage under each family's flag; the families it does not run keep their coverage from the commit
before. The integration suite reports with the heavy jobs, after the merge or on a pull request labelled `ci:heavy`.
That is a floor; the aim is everything that can be covered, under a rule that keeps the number honest — a defensive
branch no test reaches stays, a private member is covered through its callers or not at all, and code is removed
only when no input can reach it. The rule, and the two things the figure cannot show (a package no test loads, and
the code the generator emits into the consumer's assembly), are on the [Contributing](/contributing) page.

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
# the main project needs Docker for PostgreSQL, SQL Server and MongoDB; without it, add --filter-not-trait "Requires=Docker"
dotnet test --project Swashbuckle/AdCodicem.ValueObjects.CompatTests.Swashbuckle.csproj -p:AdCodicemVersion=$v   # no Docker

# Native AOT (needs clang and zlib), and the compiled model: compiled only (no database), for the JIT (a round trip,
# needs Docker) and native AOT
.github/scripts/native-aot.sh
.github/scripts/native-aot.sh artifacts/native-aot-protobuf tests/NativeAot/AdCodicem.ValueObjects.NativeAot.ProtobufNet
.github/scripts/compiled-model.sh compile
.github/scripts/compiled-model.sh jit
.github/scripts/compiled-model.sh aot

# One test (Microsoft Testing Platform: a wildcard pattern, not a substring)
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests --filter-method "*The_name_of_the_test*"
```

`TreatWarningsAsErrors` is on repository-wide, so a warning fails the build before it reaches any of the four
suites. The island sets it too.

Next: [Benchmarks](./benchmarks.md), for the numbers behind the design decisions.
