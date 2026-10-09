---
title: Frequently Asked Questions
sidebar_label: FAQ
slug: /faq
description: Short answers to the questions people ask about AdCodicem.ValueObjects, with links to the pages that explain them.
---

# Frequently asked questions

## About the library

### What problem does it solve?

Primitive obsession: domain concepts carried as bare `string`, `int` and `Guid`, with their rules restated — and
drifting apart — in every layer. [Primitive obsession](./explanation/primitive-obsession.md) describes the
problem and how the library answers it.

### Which versions of .NET are supported?

.NET 10 and later: every package targets `net10.0`, so a project on .NET 10 or a later version can use them.
[Supported frameworks](./packages.md#supported-frameworks) lists what each integration is built and tested against,
and what CI checks on the next .NET before it ships. If your application cannot move to .NET 10,
[Compared with other libraries](./explanation/comparison.md#where-the-others-are-stronger) names alternatives
that run on older frameworks.

### Will there be a package per EF Core or .NET version?

No. The twenty-five packages share one version, driven by their own API and not by the framework's, and a framework's
next major is supported by the same packages: their dependencies are minimums with no upper bound, and a CI job runs
them on the next .NET before it ships. If a new major ever breaks what a package calls, the package moves to that
major in a release that says so, and an application on the older one keeps the version before.
[ADR-0010](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/docs/adr/0010-version-every-package-in-lockstep-independently-of-dotnet.md) has the reasoning.

### Is it ready for production?

It is still at 0.x. Semantic versioning holds from 1.0.0 on, where only a major version breaks the public API;
until then, a minor version may break, deprecate or remove part of it, so read the changelog before taking a new
minor ([Versioning](./packages.md#versioning)). Every change is recorded in the
[changelog](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/CHANGELOG.md). Stable releases are
cut by hand; in between, a preview is published in any week in which something that ships has changed
([Trying a preview](./packages.md#trying-a-preview)).

### How does it compare with Vogen, StronglyTypedId or Thinktecture?

[Compared with other libraries](./explanation/comparison.md) has a feature table and says where each of them is
the better choice. [Migrate from another library](./how-to/migrating.md) maps their surface onto this one.

## Declaring value objects

### Why a `readonly struct`, and not a class or a `record struct`?

A struct wrapping a `string` costs exactly what the `string` costs, to the byte; a class adds 24 bytes per
instance. A `record struct` is refused because its `with` expression and field-wise equality would bypass
validation and the declared comparison. [Design decisions](./design-decisions.md) has the reasoning and
[Benchmarks](./benchmarks.md) the numbers.

### Which underlying types can I use?

`string`, `Guid`, `bool`, `char`, every built-in integer including `Int128` and `UInt128`, `decimal`, `double`,
`float`, `DateOnly`, `TimeOnly`, `DateTime`, `DateTimeOffset` and `TimeSpan`. Anything else is `VO0003`.

### Can a value object hold several values?

No. A concept made of several values — an amount and its currency, a postal address — belongs in an ordinary type
whose members are value objects.

### My normalization or validation rule is never called. Why?

The interface is missing. A hook is found through `IValueObjectNormalizer<T>`, `IValueObjectValidator<T>` or a
formatter interface, not through its name, and `VO0011` warns about a method that looks like a rule but is not
declared as one.

### How do I see the code the generator writes?

Set `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` in the project and build: the files land
under `obj/…/generated/AdCodicem.ValueObjects.Generators/`. [Generated members](./reference/generated-members.md)
lists what to expect.

## Using value objects

### How do I represent a missing value?

With `T?`. `default(T)` and `new T()` are build errors (`VO0010`), entity identifiers included, because an
uninitialized struct would skip every rule. A type whose zero value is genuinely meaningful can opt out with
`AllowDefault = true`, on `[ValueObject<T>]` as on `[EntityId]`.

### Can I throw my own exception type?

`Create` always throws `ValueObjectException`, which carries the error code, the type and, unless the type is
[classified as personal data](./reference/errors.md#personal-data-in-an-exception), the attempted value. To throw
something else, call `TryCreate` and throw your own exception from the `ValidationResult` it returns.

### Can a value object be a dictionary key, or an EF Core key?

Yes to both. The generated equality and hashing make dictionary lookups allocation-free, and a value object
serializes as a JSON property name. In EF Core it can be a primary or a foreign key; for a string key compared
without case, give the column a matching collation.

### Why are values read from the database not validated?

Because the EF Core and Dapper read paths are the hottest in most applications, and they read values the same
application validated when writing them. `ConfigureValueObjects(strict: true, …)` validates EF Core reads for tables
other systems also write to; Dapper validates only a column the value object cannot have written, text read into a
value object over a number, say. MongoDB reads are the other way round: validated, since a collection has no schema and
is often written by more than one program, unless the serializer is registered with
`ValueObjectBson.Register(trusted: true, …)`. So are MessagePack reads, since the bytes may come from another service,
unless the resolver is trusted, `WithValueObjects(trusted: true)` or `UseValueObjects(trusted: true)`.

### Does it work with minimal APIs?

Yes, with no package to install: a value object implements `IParsable<T>`, which is what minimal API parameter
binding looks for. A value it rejects is answered with a bare 400, though, naming neither the parameter nor the rule,
as [Minimal APIs](./how-to/aspnet-core.md#minimal-apis) explains, unless `AdCodicem.ValueObjects.AspNetCore.Http`
covers the endpoints: they then answer with the problem details MVC writes, carrying the rule's code, for a route,
query or header value, and for a JSON body where `ThrowOnBadRequest` is on, as
[Problem details for minimal APIs](./how-to/aspnet-core.md#problem-details-for-minimal-apis) shows. Under native AOT,
the Request Delegate Generator writes that binding and does not see what this generator adds, so a value object
declared in the project that maps the endpoints lists its contract on its declaration, as
[the Request Delegate Generator](./how-to/aspnet-core.md#the-request-delegate-generator) shows.

### Does it work with Swashbuckle?

Yes, Swashbuckle 10 and later, through `AdCodicem.ValueObjects.Swashbuckle`: `AddSwaggerGen(o => o.AddValueObjects())`
documents a value object as its underlying type, with the rules declared on it, as `AdCodicem.ValueObjects.OpenApi`
does on the built-in stack, a number as Swashbuckle documents a number, and a route, query or header parameter as a
reference to its component. [Swashbuckle](./how-to/openapi.md#swashbuckle) says what differs from the built-in stack.
Swashbuckle 10 needs `Microsoft.OpenApi` 2, so it fails beside `Microsoft.AspNetCore.OpenApi` 11, which brings 3.

### Is a value object logged as a number?

With Serilog and `AdCodicem.ValueObjects.Serilog`, yes: `Destructure.ValueObjects()` logs a value object with `@` as
the value it carries, and its `CaptureAsUnderlyingValue` option does the same without `@`, so a filter or a query
compares a numeric one as a number. [Serilog](./how-to/logging.md#serilog) says what else it changes. Other loggers
write a value object as its text, through `ToString()`: [Logging](./how-to/logging.md) says, for each, how to keep a
number a number.

### Does it work with MongoDB?

Through MongoDB.Driver, with `AdCodicem.ValueObjects.MongoDB`: `ValueObjectBson.Register(assembly)`, once at start-up,
stores a value object as the bare value the driver writes for its underlying type, translates a LINQ query over
`.Value` on the field itself, reads through the rules unless asked to trust the collection, and refuses to write an
uninitialized value object its type rejects. Without it, the driver writes `{}` and reads back a default instance, silently.
`ValueObjectBsonSchema.For<Order>()` carries the rules to the server too, as a `$jsonSchema` collection validator, and
`AdCodicem.ValueObjects.Identifiers.MongoDB` mints an entity identifier for a document inserted without one.
[MongoDB](./how-to/mongodb.md) has the wiring and what each type is stored as. Azure Cosmos DB for MongoDB and Azure
DocumentDB go through the same driver (not run). The MongoDB provider for Entity Framework Core goes through
`ConfigureValueObjects` instead: [Other providers](./how-to/ef-core.md#other-providers).

### Does it work with MessagePack and SignalR?

Through MessagePack 3, with `AdCodicem.ValueObjects.MessagePack`: `options.WithValueObjects()` puts a resolver in front
of the options' own, which writes a value object as the bare value its underlying type writes under the same options,
byte for byte, and reads it back through its rules unless asked to trust the bytes, a refusal carrying its code. Without
it, MessagePack's standard resolver throws on the first value object, and a contractless resolver writes `{}` and reads
back a default instance, silently. SignalR's MessagePack hub protocol uses such a resolver: `UseValueObjects()` in
`AddMessagePackProtocol`, on the server and on the .NET client, makes a hub method receive the value sent, or refuse
the invocation. MassTransit.MessagePack is not covered, and the package is not AOT-compatible:
[MessagePack and SignalR](./how-to/messagepack.md).

### Does it work with `XmlSerializer`, `DataContractSerializer` or WCF?

Once the assembly asks for it: `[assembly: ValueObjectXmlSerialization]` makes the generator implement
`IXmlSerializable` on every value object of the assembly, which `XmlSerializer` and `DataContractSerializer` honour, and
through them the MVC XML formatters (probed), CoreWCF and Dapr actor remoting (expected, not run). A value object is
then written as its underlying value, read back through its rules, and described in the exported schema with its rules
as facets. Without it, both serializers write an empty element and read back a default instance. [XML](./how-to/xml.md)
has the forms written, the errors and the limits: `DataContractSerializer` needs dynamic code to read one.

### Does it work with AutoFixture, Bogus or FsCheck?

Yes, through a package each. Left to themselves, AutoFixture feeds `Create` strings and numbers the rules refuse,
Bogus leaves a value object without a rule at its default, and FsCheck refuses the type.
`AdCodicem.ValueObjects.AutoFixture` adds a customization, `AdCodicem.ValueObjects.Bogus` a `RuleForValueObjects()`
that `StrictMode(true)` accepts, and `AdCodicem.ValueObjects.FsCheck` merges an arbitrary per value object into an
`ArbMap`, biased towards the type's edges and shrinking to values it accepts. All three draw from
`AdCodicem.ValueObjects.Testing.Data`, which any other library can call: a one-line generator does for
[CsCheck](./how-to/test-value-objects.md#cscheck). [AutoFixture, Bogus and
FsCheck](./how-to/test-value-objects.md#autofixture-bogus-and-fscheck) shows the wiring of each.

### Does it work with Microsoft.Extensions.AI or Agent Framework?

Through Microsoft.Extensions.AI, with `AdCodicem.ValueObjects.AI`, and so through what is built on it. Left to itself,
`AIFunctionFactory` describes a value object a tool takes as `true`, the schema that accepts anything, and
`FunctionInvokingChatClient` answers a value its converter refuses with "Error: Function failed.".
`new AIJsonSchemaCreateOptions().WithValueObjects()` puts the rules in the tool's schema, and
`.WithValueObjectValidation()` on the function answers a refused argument with its rule and its code, as a result the
model reads and can correct, never with the value it sent; `ValueObjectResponseFormat.ForJsonSchema<T>()` carries the
rules to a structured answer. Agent Framework's `ChatClientAgent` calls its tools through `FunctionInvokingChatClient`,
so the same calls apply; its `RunAsync<T>` drops the rules of a structured answer, which a plain `RunAsync` takes
through its run options.
[Language models](./how-to/language-models.md) has the wiring, the result, and the limits.

### Does it work with a Model Context Protocol server?

With the C# SDK, through `AdCodicem.ValueObjects.ModelContextProtocol`. Left to itself, the SDK lists a value object a
tool takes as `true` in its `inputSchema`, and one it returns as structured content as `true` in its `outputSchema`,
and answers a value its converter refuses with `An error occurred invoking 'place_order'.`; its `WithTools` methods take
no schema options. `WithValueObjectTools<OrderTools>()` registers the same tools as `WithTools<OrderTools>()`, with the
rules in both schemas, and answers a refused argument with a tool execution error carrying the argument, the code and
the message, never the value sent. The tools stay the SDK's own, so a client on an older protocol version still gets
the output schema the SDK writes for it. [Model Context Protocol
servers](./how-to/language-models.md#model-context-protocol-servers) has the wiring, the result, and the limits.

## Performance

### Does a value object cost more than the primitive it wraps?

Holding one costs nothing more: a struct wrapper has the size of the value it wraps. Validation costs what the
rules cost, on the way in only. The struct gives something back when it is boxed — passed as `object` or through
a non-generic interface — which is why the generated equality, hashing and comparison keep the hot paths
generic. [Benchmarks](./benchmarks.md) measures each case.

### Does it use reflection?

Not in the generated code, and not to find value objects: a generated module initializer registers each type at
start-up. The exceptions are a generic value object, whose registration knows none of the constructions an application
uses, and a value object written by hand, which nothing registers: the registry describes each one by reflection, the
first time it is asked for it, and under native AOT each is registered by hand instead, as [Where a value object can be
declared](./authoring-guide.md#where-a-value-object-can-be-declared) and [Run-time
lookup](./how-to/runtime-lookup.md#value-objects-written-by-hand) show. Code that finds a value object by its `Type` and
closes a generic adapter over it does so through the descriptor's visitor rather than `MakeGenericType`, which native
AOT cannot run for a struct, as [Run-time lookup](./how-to/runtime-lookup.md#back-to-the-typed-path) shows; the Dapper
integration registers its handlers that way, the MongoDB provider builds its serializers, the MessagePack resolver the
formatter MessagePack asks it for, the MongoDB identifiers package its id generators, through the visitor of an entity
identifier's descriptor, the MVC model binder provider creates its binders, the minimal API filter closes the check of
each parameter it explains, the JSON converter factory closes the general-purpose converter it gives a value object
written by hand, the test-data sampler, the AutoFixture customization, the Bogus extensions and FsCheck's
`MergeValueObjects` draw each value object they meet by its `Type`, and the Entity Framework Core conventions map each
value object and each entity identifier, all but the converter of an optional value object, which C# cannot name there
and which they close with `MakeGenericType`; they do so only while a model is built, never under native AOT. The
Microsoft.Extensions.AI integration closes nothing: it reads a tool's parameters once, when it wraps the function, and
each argument through the function's own contracts. Nor does the Model Context Protocol one, which finds the tool methods
of a type as the SDK does, the generic registration through a type parameter carrying the SDK's trimming annotation. The
contracts, the generated code, the JSON package, the minimal API problem details, FluentValidation, identifiers and the
Serilog, Microsoft.Extensions.AI and Model Context Protocol integrations are marked AOT-compatible and built with the
trimming and AOT analyzers on, and CI publishes an
application using them all with native AOT on every pull request, which merges only once that passes: it fails on any
trimming or AOT warning, and unless the native binary does exactly what the application does under the JIT. The EF Core,
ASP.NET Core MVC, OpenAPI, Swashbuckle, Dapper, MongoDB, MongoDB identifiers, MessagePack and Newtonsoft.Json
integrations are not AOT-compatible, because the frameworks they plug into are not. Dapper, for one, files each handler
in a cache it closes over the type at run time, so under native AOT a handler this package built without dynamic code
still fails inside Dapper: native AOT goes through [Dapper.AOT](./how-to/dapper.md#dapperaot).

### Does a pattern run compiled under native AOT?

Through `IValueObjectPatternValidator`, yes. Its `Pattern` is a `[GeneratedRegex]` property you write, which the
regex source generator turns into code at build time, so it runs the same under native AOT as under the JIT and
costs nothing until it first runs. The `Pattern` option of `[ValueObject<T>]` did not: it built its `Regex` at
start-up with `RegexOptions.Compiled`, which native AOT cannot honour, so there the expression was interpreted. The
option no longer compiles (`VO0021`), and any minor version may remove it before 1.0.0;
[Diagnostics](./reference/diagnostics.md#moving-off-pattern) shows the change. [Benchmarks](./benchmarks.md)
has the measurements.
