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

No. The thirty-eight packages are released in two trains, each with one version driven by its own API and not by a
framework's: the core, `AdCodicem.ValueObjects` and the nine packages released with it, and the satellites, every
other package. [Supported frameworks](./packages.md#supported-frameworks) gives the train of each. Reference every
package of a train at that train's version; the two trains carry the same version until they first part, and from
then on they may differ. A framework's next major is supported by the same packages: their dependencies on the
frameworks are minimums with no upper bound, and a CI job runs them on the next .NET before it ships. If a new major
ever breaks what a package calls, the package moves to that major in a release of its train that says so, and an
application on the older one keeps the version before.
[ADR-0012](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md) has the reasoning.

### Is it ready for production?

Both trains are still at 0.x. Each follows semantic versioning from its own 1.0.0 on, where only a major version
breaks the public API; until then, a minor version of the train may break, deprecate or remove part of it, so read
the changelog before taking a new minor ([Versioning](./packages.md#versioning)). The core reaches 1.0 first; the
satellites stay 0.x and reach 1.0 together, later. Every change is recorded in the changelog of the package's train:
[CHANGELOG.md](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/CHANGELOG.md) for the core,
[CHANGELOG.satellites.md](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/CHANGELOG.satellites.md) for
the satellites (CHANGELOG.md up to their first release of their own). Stable releases are cut by hand; in between, a
preview is published in any week in which something that ships has changed
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

### Does `[Required]` catch a value object that was never created?

No: a value object is a struct, never `null`, so DataAnnotations' `[Required]` passes on one left at its default, in
`Validator`, MVC, `AddValidation()`, options validation and `ObservableValidator` alike. `[NotDefault]`, in the
contracts, refuses it with `value_object.required` and `[Required]`'s message, and so refuses a valid zero over a value
type, `OrderId.Create(Guid.Empty)`, as FluentValidation's `NotDefault` does
([DataAnnotations](./how-to/data-annotations.md#an-uninitialized-value-object)).

### How do I hold the raw text of a DTO or an options class to a value object's rules?

With `[ParsesAs(typeof(Iban))]`, or `[ParsesAs<Iban, string>]`, on the `string` member: it runs the type's own parsing,
normalization and rules included, wherever DataAnnotations runs, reports the rule's message, and carries its code on the
`ValueObjectValidationResult` it refuses with. The member keeps the text; the application creates the value object where
it reads it. An options class the configuration binding generator binds keeps the text that way, and `ValidateOnStart`
then checks it ([DataAnnotations](./how-to/data-annotations.md)). FluentValidation's `MustParseAs` does the same in a
validator.

### Can I throw my own exception type?

`Create` always throws `ValueObjectException`, which carries the error code, the type and, unless the type is
[classified as personal data](./reference/errors.md#personal-data-in-an-exception), the attempted value. To throw
something else, call `TryCreate` and throw your own exception from the `ValidationResult` it returns.

### Can the messages be in another language?

Yes, by the code of the rule. The messages of the rules stay English, for the developers who read exceptions and logs,
and an application translates them under the key `ValueObject.` followed by the code, with arguments the declared rules
supply: the length, the bounds, the known values, formatted in the request's culture. MVC and minimal APIs read the
translations from a resource the application names, `AddValueObjects(messages => messages.UseResource<T>())` and
`WithValueObjectProblemDetails(messages => messages.UseResource<T>())`, so do the HTTP triggers of Azure Functions,
`UseValueObjectProblemDetails(messages => messages.UseResource<T>())`, Wolverine.Http endpoints,
`UseValueObjects(messages => messages.UseResource<T>())`, a HotChocolate schema,
`AddValueObjects(options => options.UseResource<T>())`, the inputs of a Blazor form,
`AddValueObjectInputs(options => options.UseResource<T>())`, and the options and arguments of a System.CommandLine
command, `AcceptValueObjects(options => options.UseResource<T>())`, FluentValidation reads them from its
`LanguageManager`, and
DataAnnotations' `[ParsesAs]` and `[NotDefault]` from the resource their `ErrorMessageResourceType` names alone.
CsvHelper shows no message to a person: an import reads the code off the exception and looks the message up itself
([CsvHelper](./how-to/files-and-command-line.md#what-a-refused-field-looks-like)). No translation ships with the
packages. The
[errors reference](./reference/errors.md#a-message-in-the-applications-language) lists the keys and the arguments, with a
French example, and the guides for [ASP.NET Core](./how-to/aspnet-core.md#messages-in-the-applications-language),
[Azure Functions](./how-to/azure.md#messages-in-the-requests-language),
[Wolverine](./how-to/wolverine.md#messages-in-the-requests-language),
[GraphQL](./how-to/graphql.md#messages-in-the-applications-language),
[Blazor](./how-to/blazor.md#messages-in-the-applications-language),
[System.CommandLine](./how-to/files-and-command-line.md#messages-in-the-applications-language),
[FluentValidation](./how-to/fluentvalidation.md#messages-in-another-language)
and [DataAnnotations](./how-to/data-annotations.md#messages-in-another-language) the wiring.

### Can a value object be a dictionary key, or an EF Core key?

Yes to both. The generated equality and hashing make dictionary lookups allocation-free, and a value object
serializes as a JSON property name. In EF Core it can be a primary or a foreign key; for a string key compared
without case, give the column a matching collation. `HasValueObjectKeyGeneration()` generates a `Guid` key with GUIDs
the database keeps in order, and makes a `short`, `int` or `long` key an identity column; under `ConfigureValueObjects`,
a `Guid` key nothing generates refuses an entity added without it
([Value objects as keys](./how-to/ef-core.md#value-objects-as-keys)).

### Can an entity hold a list of value objects?

Yes. `ConfigureValueObjects` maps a `List<Iban>`, a `Quantity[]` or an `IReadOnlyList<Iban?>` as a primitive collection:
an array of `character varying(34)` on PostgreSQL, a JSON array elsewhere, each element converted, compared and sized as
its value object, and queried with `Contains` or `Any`.
[Collections of value objects](./how-to/ef-core.md#collections-of-value-objects) says which properties it takes, and
what it writes for an element its type rejects.

### Can an EF Core query filter on `.Value`?

Yes, once the context's options call `UseValueObjectQueries()`: `x.Iban.Value.StartsWith("FR")` then reads the column
itself, which its index serves, and a parameter compared with it is declared as the column is, `nvarchar(34)`. Without
it, Entity Framework Core refuses `.Value` as untranslatable; a comparison between value objects translates either way.
[The value in a query](./how-to/ef-core.md#the-value-in-a-query) says what it covers.

### Can a value object's column be `varchar` on SQL Server?

Yes: declare its text non-Unicode once, `[ValueObject<string>(MaxLength = 34, Unicode = false)]`, and
`ConfigureValueObjects` maps it to `varchar(34)`, with `varchar` parameters from Entity Framework Core and Dapper alike,
which keeps the index seek of a SQL collation and suits an Always Encrypted `varchar` column. SQL Server writes a
character its code page lacks as a look-alike or `?`, silently, so the option is for ASCII text, which `VO0047` asks the
rules to hold. PostgreSQL and SQLite store the text as before. [Non-Unicode text](./how-to/ef-core.md#non-unicode-text)
covers existing columns and keys.

### Why are values read from the database not validated?

Because the EF Core and Dapper read paths are the hottest in most applications, and they read values the same
application validated when writing them. `ConfigureValueObjects(strict: true, …)` validates EF Core reads for tables
other systems also write to, and
[`AddValueObjectCheckConstraints()`](./how-to/ef-core.md#check-constraints-from-the-rules) makes such a table refuse a
row breaking a rule when another writer stores it, and, under
[`UseValueObjectCheckErrors()`](./how-to/ef-core.md#when-a-constraint-fires), a write of the application's own that a
constraint refuses carries the rule's code; Dapper validates only a column the value object cannot have written, text read into a
value object over a number, say. MongoDB reads are the other way round: validated, since a collection has no schema and
is often written by more than one program, unless the serializer is registered with
`ValueObjectBson.Register(trusted: true, …)`. So are MessagePack reads, since the bytes may come from another service,
unless the resolver is trusted, `WithValueObjects(trusted: true)` or `UseValueObjects(trusted: true)`, protobuf-net
reads, unless the serializers are registered with `AddValueObjects(trusted: true, …)` or name a trusted profile, and
Orleans reads, unless every silo and client registers `AddValueObjects(trusted: true)`. Marten reads are validated
always: a document goes through the generated JSON converter, and a value a query projects through the package.

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

Yes, Swashbuckle 10.3 and later, through `AdCodicem.ValueObjects.Swashbuckle`: `AddSwaggerGen(o => o.AddValueObjects())`
documents a value object as its underlying type, with the rules declared on it, as `AdCodicem.ValueObjects.OpenApi`
does on the built-in stack, a number as Swashbuckle documents a number, and a route, query or header parameter as a
reference to its component. [Swashbuckle](./how-to/openapi.md#swashbuckle) says what differs from the built-in stack.
Swashbuckle 10 needs `Microsoft.OpenApi` 2, so it fails beside `Microsoft.AspNetCore.OpenApi` 11, which brings 3.

### Does it work with NSwag?

Yes, NSwag 14.7 and later, through `AdCodicem.ValueObjects.NJsonSchema`:
`AddOpenApiDocument(s => s.SchemaSettings.AddValueObjects())` describes each value object as one component of its
underlying type, with every rule the built-in stack's document carries, and a parameter, a member or an element as a
reference to it, `nullable: true` beside the reference for a nullable one. A C# client NSwag generates from it holds
the underlying types and one enumeration per closed set. [NSwag](./how-to/openapi.md#nswag) says what differs from the
built-in stack: OpenAPI 3.0, Swagger 2.0 in place, and the settings NSwag chooses for minimal APIs alone.

### Is a value object logged as a number?

With Serilog and `AdCodicem.ValueObjects.Serilog`, yes: `Destructure.ValueObjects()` logs a value object with `@` as
the value it carries, and its `CaptureAsUnderlyingValue` option does the same without `@`, so a filter or a query
compares a numeric one as a number. [Serilog](./how-to/logging.md#serilog) says what else it changes. Other loggers
write a value object as its text, through `ToString()`: [Logging](./how-to/logging.md) says, for each, how to keep a
number a number.

### Is a value object classified as personal data redacted in the logs?

By Microsoft.Extensions.Telemetry's logging generator, once the parameter or the property logging it carries the
classification too: the generator reads none on the type of a value. `VO0045` warns where a `[LoggerMessage]` method
would write one in clear, and its code fix copies the type's classification. `LogInformation` and Serilog's own API log
it in clear. [Data classification](./how-to/logging.md#data-classification) has the details.

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

### Does it work with Marten?

Marten stores a value object as its bare value without help, through the generated JSON converter, and refuses on read
a stored value the rules refuse, but it cannot query one: a comparison throws, `.Value` reads a path no document has and
answers no rows, and a duplicated field fails at the first write. Through Marten 9.50, with
`AdCodicem.ValueObjects.Marten`, `AddMarten(…).UseValueObjects()` teaches the store every value object as Marten's own
value types are taught: LINQ, `Contains`, `IsOneOf`, duplicated fields, computed indexes and identities see the
underlying value, a compiled query binds a collection of value objects on every call, Marten refusing a single one,
which `VO0048` reports when the project builds, and binding its `.Value` compared with a member of a type it binds, and
a value a query projects is read through the rules, its refusal carrying the code.
[Marten](./how-to/marten.md) has the wiring, the SQL each shape translates to, and what stays Marten's.

### Does it work with MessagePack and SignalR?

Through MessagePack 3, with `AdCodicem.ValueObjects.MessagePack`: `options.WithValueObjects()` puts a resolver in front
of the options' own, which writes a value object as the bare value its underlying type writes under the same options,
byte for byte, and reads it back through its rules unless asked to trust the bytes, a refusal carrying its code. Without
it, MessagePack's standard resolver throws on the first value object, and a contractless resolver writes `{}` and reads
back a default instance, silently. SignalR's MessagePack hub protocol uses such a resolver: `UseValueObjects()` in
`AddMessagePackProtocol`, on the server and on the .NET client, makes a hub method receive the value sent, or refuse
the invocation. MassTransit.MessagePack is not covered, and the package is not AOT-compatible:
[MessagePack and SignalR](./how-to/messagepack.md).

### Does it work with protobuf-net and gRPC?

Through protobuf-net 3.4, with `AdCodicem.ValueObjects.ProtobufNet`: `RuntimeTypeModel.Default.AddValueObjects(…)`
gives every value object a serializer that writes it as protobuf-net writes its underlying type, at the model's
compatibility level (but a valid zero is written where the primitive's is left out, and a member's `DataFormat` never
reaches a value object), reads it back through its rules unless asked to trust the bytes, a refusal carrying its code,
and names it in the `.proto` export as that type's scalar. Without it, the first message holding a value object throws,
and a bare `[ProtoContract]` on the value object, which would write an empty message and lose the value, fails the build
(`VO0043`), its code fix naming the serializer. A protobuf-net.Grpc
code-first service whose configuration is `ValueObjectBinderConfiguration.ForServer` answers a refused request with
`INVALID_ARGUMENT` and a `google.rpc.ErrorInfo` carrying the code, where Grpc.AspNetCore would answer `UNKNOWN`. A field
a proto3 sender leaves out is never read and leaves a default instance: declare it `TSelf?`. Under native AOT, the
serializers run in protobuf-net's compile-time model, named on the value object with `[ProtoContract(Serializer = …)]`:
[protobuf-net and code-first gRPC](./how-to/protobuf-net.md). A contract-first service, whose messages Grpc.Tools
generates, maps its fields to value objects in its handlers: `AdCodicem.ValueObjects.Grpc` answers the refusal of a
`Create` there with `INVALID_ARGUMENT` and the same `ErrorInfo`, through an interceptor, and every refused field of a
request at once in a `google.rpc.BadRequest`, through `ValueObjectBadRequest`: [gRPC services](./how-to/grpc.md).

### Does it work with Orleans?

Through Orleans 10.4, with `AdCodicem.ValueObjects.Orleans`: `AddValueObjects()` on the serializer of every silo and
client writes each value object in a grain call as Orleans writes its underlying value, byte for byte, a text in full
even where Orleans would refer back to the same string, with its type where a member typed `object` holds it, and reads
it back through its rules unless asked to trust the cluster. A refused
request, a refused reply and a `ValueObjectException` a grain lets escape reach the caller with the rule's code.
Without it, every grain call holding a value object fails with `CodecNotFoundException`, and `[GenerateSerializer]` on
the value object, which would lose the value, fails the build (`VO0043`). A call within one silo copies its arguments,
so a default instance arrives there; Orleans does not run natively yet: [Orleans](./how-to/orleans.md).

### Does it work with Kafka and the Schema Registry?

Yes, with Confluent's JSON serdes: `AddValueObjects()` from `AdCodicem.ValueObjects.NJsonSchema` on the
`NewtonsoftJsonSchemaGeneratorSettings` handed to `JsonSerializer<T>` and `JsonDeserializer<T>`, after
`ValueObjectConverter`, registers each value object as its underlying type with its rules, numbers as numbers, so the
producer's validation passes, and a message whose value its type refuses is refused with the code of the rule. A subject
already holding the schema written without it refuses the new one under `BACKWARD`:
[Kafka with Confluent's Schema Registry](./how-to/messaging.md#kafka-with-confluents-schema-registry) says how to move
it.

### Does it work with GraphQL?

Through HotChocolate 16, with `AdCodicem.ValueObjects.HotChocolate`: `AddValueObjects()` on the schema's builder gives
every value object a scalar of its own, `scalar Iban`, named and described by its type and serialized as its underlying
value, a Boolean, an Int, a Float or a String, which reads an argument, an input field or a variable through its rules
and answers a refused value with the rule's code under `extensions.code`. Without it, HotChocolate infers an input
object type from a value object it meets as an argument and the schema does not start. HotChocolate.Data filters every
value object through its scalar with `AdCodicem.ValueObjects.HotChocolate.Data`'s `AddValueObjectFiltering()`: an
operand normalized, refused with its rule code and translated by Entity Framework Core, with no binding per type, and
`contains`, `startsWith` and `endsWith` over the text on request
([Filtering](./how-to/graphql.md#filtering-and-sorting-with-hotchocolatedata)). The native binary of a HotChocolate
application needs HotChocolate.Types and the application's assembly rooted, and HotChocolate.Data's filtering does not
run natively: [GraphQL](./how-to/graphql.md).

### Does it work with Blazor?

Through `AdCodicem.ValueObjects.Blazor`, whose `InputValueObject` binds a value object, or its nullable type, in an
`EditForm`: it reads the text a person types through the rules, binds the normalized instance, and answers refused text
with the rule's message under the field and its code on the form's `EditContext`, which
`TryGetValueObjectErrorCode` reads. Without it, `@bind` drops refused text without a message and binds empty text as the
default instance, and none of the built-in inputs takes a value object. A statically rendered form is mapped by the
framework first, which answers a refused value with its own message, quoting the text, and refuses an empty optional
value object: [Blazor forms](./how-to/blazor.md).

### Does it work with System.CommandLine?

Through `AdCodicem.ValueObjects.CommandLine`, whose `AcceptValueObjects()` reads every option and argument of a value
object, its nullable type or a collection of it, in a command and its subcommands, through the rules, in the invariant
culture unless told otherwise, a parent command's while a subcommand is validated. A refused value is a parse error
naming the option and giving the rule's message, which an invocation writes to standard error before the help,
returning 1 without running the action, and `TryGetValueObjectErrorCode` reads its code.
Without it, System.CommandLine 2.0, which reads neither `TypeDescriptor` nor `IParsable<T>`, parses a valid value with
no error and then throws from `GetValue`, and gives the flag syntax to `bool` alone. Spectre.Console.Cli reads the
`TypeConverter`, and needs no package:
[Files and command line](./how-to/files-and-command-line.md#systemcommandline).

### Does it work with CsvHelper?

Through `AdCodicem.ValueObjects.CsvHelper`, whose `AddValueObjects()` on a context's converter cache maps each value
object as one column: written as its own text, which its parser reads back, and read through its rules, in the
member's culture. A refused field is CsvHelper's own `TypeConverterException`, naming the type and the rule, never the
field, the rule's code in its `Data`, which an import reads in `ReadingExceptionOccurred` to report each refused row and
go on; an empty field read into a value object that cannot be `null` is `value_object.required`, and an uninitialized
instance its rules refuse is not written. Register before the first read, write or class map of the context. Without
it, CsvHelper 33.1 writes a `Value` column per value object and reads default instances, with nothing thrown:
[Files and command line](./how-to/files-and-command-line.md#csvhelper).

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

### Does it work with Azure Functions?

With the isolated worker, through `AdCodicem.ValueObjects.AzureFunctions`. Left to itself, the worker reads no
`[TypeConverter]`: the text of a route value, a query value or other binding data falls through to its JSON conversion,
which refuses a valid `Guid`, text or a date, and fails the invocation with a 500. `UseValueObjects()` binds them
through the value object's rules, and a refusal fails the parameter with the code at the end of the text the worker
keeps. On the ASP.NET Core integration, `AdCodicem.ValueObjects.AzureFunctions.AspNetCore`'s
`UseValueObjectProblemDetails()`, called after `ConfigureFunctionsWebApplication()`, answers an HTTP trigger's refused
value objects with the validation problem a minimal API writes, and their codes. A trigger's payload stays its
binding's, read through the generated JSON converter. The in-process model is not covered, nor native AOT, which the
worker does not support. [Azure Functions](./how-to/azure.md#azure-functions-isolated-worker) has the wiring, the
answers, and the limits.

### Does it work with Wolverine?

Messaging needs nothing: Wolverine serializes with System.Text.Json, which writes a value object as its underlying value
and reads it back through its rules, a refused message moving to the error queue with the converter's exception and its
code. Wolverine.Http binds a route, query, header or form value through `TryParse`, but in its own terms: a refused
route value is a 404, a refused query value reaches the handler as the default instance under its default
`RejectUnparseableQueryValues = false`, and so do an empty or absent one in either mode and a refused, empty or absent
header or form value, and `[FromQuery]` on a value object stops the application at startup.
`AdCodicem.ValueObjects.Wolverine.Http`'s `UseValueObjects()`, inside `MapWolverineEndpoints`, binds each through its
rules and answers every refused or missing one of a request at once with the validation problem a minimal API writes,
and their codes, before any middleware method runs. `[AsParameters]` members and the JSON body stay Wolverine's. It runs
in Wolverine's static mode under native AOT, over the code `codegen write` writes. [Wolverine](./how-to/wolverine.md)
has the wiring, the answers, and the limits.

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
formatter MessagePack asks it for, protobuf-net's `AddValueObjects` names the closed serializer type of each value object, the Orleans package the codec Orleans asks it for, the MongoDB identifiers package its id generators, through the visitor of an entity
identifier's descriptor, the MVC model binder provider creates its binders, the minimal API filter closes the check of
each parameter it explains, the Azure Functions converter the conversion of each value object a parameter binds and its
middleware the check of each it explains, the HotChocolate type discovery handler the scalar of each value object a schema
meets, the HotChocolate.Data type interceptor the operations of each value-object member a filter input meets, the NJsonSchema package the converter it writes a value object's examples through under Newtonsoft.Json
settings, the Blazor input the text adapters of the value object it binds, the System.CommandLine symbols the parser of
the value object their option or argument holds, the CsvHelper factory the converter of each value object a context
meets, the Marten package the member of each value object a query meets, the JSON converter factory closes the
general-purpose converter it gives a value object
written by hand, the test-data sampler, the AutoFixture customization, the Bogus extensions and FsCheck's
`MergeValueObjects` draw each value object they meet by its `Type`, and the Entity Framework Core conventions map each
value object, each element of a collection of them, and each entity identifier, all but the converter of an optional
value object, which C# cannot name there and which they close with `MakeGenericType`; they do so only while a model is
built, never under native AOT. `TryApplyTo`'s JSON Patch adapter of a dictionary keyed by value objects, or holding
them, is closed over the dictionary's two types with `MakeGenericType` as well, as JSON Patch closes its own over the
same types: MVC's JSON Patch runs in no native binary. DataAnnotations' `[NotDefault]` closes its check of the default instance per type through
the visitor too, and `[ParsesAs(typeof(T))]` finds the descriptor as the registry does, its generic form reading the
type's static members. The Microsoft.Extensions.AI integration closes nothing: it reads a tool's parameters
once, when it wraps the function, and each argument through the function's own contracts. Nor does the Model Context Protocol one, which finds the tool methods
of a type as the SDK does, the generic registration through a type parameter carrying the SDK's trimming annotation. The
contracts, the generated code, the JSON package, the minimal API problem details, FluentValidation, identifiers and the
Serilog, Microsoft.Extensions.AI and Model Context Protocol integrations are marked AOT-compatible and built with the
trimming and AOT analyzers on, and CI publishes an
application using them all with native AOT after every merge and before every preview and stable release, which
publish nothing unless it passes: it fails on any trimming or AOT warning, and unless the native binary does exactly
what the application does under the JIT. The
two HotChocolate packages are marked AOT-compatible too, and their own code publishes with no warning, but CI publishes
no HotChocolate application: HotChocolate's own publish warns, its binary starts only with HotChocolate.Types and the
application's assembly rooted, and HotChocolate.Data's filtering of a struct has no native code
([GraphQL](./how-to/graphql.md#native-aot)). The gRPC package is
AOT-compatible, and CI's application serves a contract-first service with it. The Blazor package is AOT-compatible,
and CI's application renders a form of every registered value object through `HtmlRenderer`; Microsoft.AspNetCore.Components
warns for itself there ([Blazor](./how-to/blazor.md#native-aot-and-trimming)). The System.CommandLine package is
AOT-compatible, and CI's application parses a command of every registered value object with it
([System.CommandLine](./how-to/files-and-command-line.md#generic-and-hand-written-value-objects-and-native-aot)). The
protobuf-net package is marked
AOT-compatible: its serializers run in protobuf-net's compile-time model, which CI publishes in an application of its
own, whose generated code protobuf-net's annotations make warn, while its registration and its code-first binder serve
protobuf-net's runtime model, which needs the JIT ([protobuf-net](./how-to/protobuf-net.md#native-aot)). The Orleans
package is marked AOT-compatible, and its own code publishes with no warning, but Orleans 10.4 does not start in a
native binary, so CI publishes no Orleans application ([Orleans](./how-to/orleans.md#native-aot)). The CsvHelper
package is marked AOT-compatible, and its own code publishes with no warning, but CsvHelper's record API has no native
code, its auto-mapping and class maps failing in a native binary, so CI publishes no CsvHelper application; its
`GetField<T>` and `WriteField<T>` run natively
([CsvHelper](./how-to/files-and-command-line.md#generic-and-hand-written-value-objects-in-a-file-and-native-aot)). The
Marten package is AOT-compatible, and CI publishes a Marten application natively and runs it against PostgreSQL;
Marten's own publish warns for Marten, JasperFx, Weasel and FastExpressionCompiler, never for this library
([Marten](./how-to/marten.md#native-aot)). The Wolverine.Http package is AOT-compatible and closes nothing at run time:
the code Wolverine writes for an endpoint names each value object, and CI's application serves Wolverine.Http endpoints
natively in Wolverine's static mode, over the code `codegen write` writes first
([Wolverine](./how-to/wolverine.md#native-aot)). The EF Core, ASP.NET Core MVC, OpenAPI, Swashbuckle, NJsonSchema,
Dapper, MongoDB, MongoDB identifiers, MessagePack, Azure Functions and Newtonsoft.Json integrations are not
AOT-compatible, because the frameworks they plug into are not. Dapper, for one, files each handler
in a cache it closes over the type at run time, so under native AOT a handler this package built without dynamic code
still fails inside Dapper: native AOT goes through [Dapper.AOT](./how-to/dapper.md#dapperaot).

### Does a pattern run compiled under native AOT?

Through `IValueObjectPatternValidator`, yes. Its `Pattern` is a `[GeneratedRegex]` property you write, which the
regex source generator turns into code at build time, so it runs the same under native AOT as under the JIT and
costs nothing until it first runs. The `Pattern` option of `[ValueObject<T>]` did not: it built its `Regex` at
start-up with `RegexOptions.Compiled`, which native AOT cannot honour, so there the expression was interpreted. The
option no longer compiles (`VO0021`), and any minor version of the core may remove it before the core's 1.0.0;
[Diagnostics](./reference/diagnostics.md#moving-off-pattern) shows the change. [Benchmarks](./benchmarks.md)
has the measurements.
