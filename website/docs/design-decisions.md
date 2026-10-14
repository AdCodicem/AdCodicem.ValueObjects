---
title: Design Decisions
sidebar_label: Design decisions
slug: /design-decisions
description: Why a value object is a readonly partial struct, why default is a build error, why rejection is not an exception, and why rules are declared once.
---

# Design decisions worth knowing

**A `readonly partial struct`, not a `record struct`.** A record's `with` expression and field-wise equality
would both bypass validation and the configured comparison. The generator owns equality, ordering and hashing
so that `Comparison = StringComparison.OrdinalIgnoreCase` actually means something.

**A struct, even when the underlying type is a `string`.** Holding 100 000 struct wrappers allocates exactly
what holding 100 000 bare strings allocates, to the byte; the class equivalent costs four times the memory and
2.3x the time, because a reference type adds 24 bytes of header, method table pointer and field per instance.
The struct gives that back only when it crosses a non-generic boundary and boxes, so the generated equality,
hashing and comparison exist to keep the hot paths generic — dictionary lookups and sorts on value objects
allocate nothing. See [Benchmarks](./benchmarks.md) for the numbers and for where the struct loses.

**`default(Iban)` is a build error.** A struct can always be brought into existence uninitialized, and that is the one
hole a struct value object cannot close by itself. The `VO0010` analyzer closes it at compile time, which is what makes
the struct representation — zero allocation, no null — safe to choose. Opt out per type with `AllowDefault = true`.
Another source generator cannot see the generated members, and may write `new Iban()` in its own output: `VO0032`
reports it in the code of Riok.Mapperly and of the configuration binding generator, and of any generator a
`.globalconfig` adds ([the fix](./reference/diagnostics.md#a-value-object-another-generator-creates)). What reaches a
boundary the analyzer cannot see — an entity property never set, a default array element — is not written as it stands:
the JSON converters, the Dapper handler, the MongoDB serializers, the MessagePack formatters, the protobuf-net
serializers, the HotChocolate scalars, the XML serialization of an assembly marked `[assembly: ValueObjectXmlSerialization]` and the EF Core converters refuse an uninitialized instance
whose value its type rejects, and an optional EF Core column stores a `NULL` instead, as does an element of a
`List<Iban?>` ([what each one throws](./reference/errors.md#a-value-refused-on-write)). Under
`ConfigureValueObjects`, an EF Core primary key of one property over a `Guid` value object, which nothing generates,
refuses an entity added without it, whatever the type's rules
([an entity added without its key](./reference/errors.md#an-entity-added-without-its-key)).

**Rejection is not an exception.** `Validate` returns a `readonly struct` that allocates nothing when the value is
valid. The integrations that take outside input go through `TryCreate` or `TryParse` and report a refusal in their own
terms: a JSON exception, a model state error, a FluentValidation failure, a Dapper `DataException`, a MongoDB.Driver
`FormatException`, a MessagePack `MessagePackSerializationException`, a protobuf-net `ProtoException`, the
`INVALID_ARGUMENT` status of a gRPC service, code-first or contract-first, an `XmlException` from `XmlSerializer` or
`DataContractSerializer` in an assembly marked `[assembly: ValueObjectXmlSerialization]`, a tool result a language model
reads, a Model Context Protocol tool execution error, the text of the Azure Functions worker's binding failure, which
ends with the code, a validation problem for one of its HTTP triggers, a GraphQL error whose `extensions.code` is the
rule's, and the `ValueObjectValidationResult` DataAnnotations' `[ParsesAs]` and `[NotDefault]` refuse with
([what each one throws](./reference/errors.md)).
Each carries the code of the rule, which `ValueObjectErrors.TryGetCode` reads from any of those exceptions ([the code in
an exception](./reference/errors.md#the-code-in-an-exception)). Under `UseValueObjectCheckErrors()`, the database
provider's error for a row a CHECK constraint from the rules refused carries the code too, read off the constraint's
name. The message is English; the code keys the one an
application shows in its users' language, which the MVC, minimal API, Azure Functions, HotChocolate, FluentValidation
and DataAnnotations integrations look up with the
arguments the declared rules supply ([a message in the application's
language](./reference/errors.md#a-message-in-the-applications-language)). `Create` throws `ValueObjectException`, and is
for the call sites that want it; a strict EF Core read goes through it, and fails the query. Validation is fail-fast: the
first violated rule wins.

**Normalize, then validate, then assign.** So a non-default instance is by construction both normalized and
valid. It happens on construction, on parsing, on deserialization and on model binding — but *not* when
materializing a row through EF Core or Dapper, which is the hottest path in most applications and reads values this
same application wrote, nor for the temporary value EF Core holds for an integer key it generates, which the database
replaces before anything is written. `ConfigureValueObjects(strict: true)` turns that back on for a table another system
also writes to. MongoDB, MessagePack and protobuf-net reads validate, since a collection or a message may come from
another program, unless the application trusts them: `ValueObjectBson.Register(trusted: true, …)`,
`WithValueObjects(trusted: true)`, `AddValueObjects(trusted: true, …)`.

**Rules are declared once.** `MaxLength = 34` validates the value, sizes the EF Core column, each element of a
collection of IBANs included, and becomes the `maxLength` keyword of the OpenAPI schema, NSwag's included, of the JSON Schema a
[Kafka topic registers](./how-to/messaging.md#kafka-with-confluents-schema-registry), and of the [MongoDB collection
validator](./how-to/mongodb.md#a-collection-validator-from-the-rules). A declared pattern becomes the pattern of the
[GraphQL scalar](./how-to/graphql.md#what-the-schema-publishes) too. The members marked `[KnownValue]` become a frozen
membership lookup and the `enum` keyword of the schema, whose values generated clients name after them. On request,
`AddValueObjectCheckConstraints()` writes the minimum length, the bounds and the closed sets as [CHECK
constraints](./how-to/ef-core.md#check-constraints-from-the-rules) of the EF Core columns, wherever the database reads
them as .NET does, and, once the options name the dialect of SQL Server 2025 or PostgreSQL, the patterns, refusing to
build the model on one the database would read apart from .NET unless `ExcludePattern<TSelf>()` leaves it to the
application. Each constraint is named after its rule, so that a row it refuses comes back with the rule's code under
`UseValueObjectCheckErrors()` ([when a constraint fires](./how-to/ef-core.md#when-a-constraint-fires)). A DTO or an
options class that keeps the text holds it to the same rules with [`[ParsesAs]`](./how-to/data-annotations.md), which
the OpenAPI packages, NSwag's included, describe as the value object's text, rather than restating them in
`[StringLength]`.

## Constraints that shape the generated code

A few constraints of the .NET compiler and source generator model are the reason some of the generated code
looks the way it does — worth knowing if a compile error in generated code is confusing:

- **Source generators never observe each other's output.** The `[JsonConverter]` this generator writes is
  invisible to the System.Text.Json generator, which is the entire reason `AdCodicem.ValueObjects.Json` exists:
  a hand-written converter factory the STJ generator *can* see. The same constraint is why a pattern is a hook:
  the regex generator only sees code a person wrote, so this generator cannot write a `[GeneratedRegex]` itself.
  The consumer writes it instead, as the `Pattern` property of `IValueObjectPatternValidator`, and the generator
  reads its text off the attribute for the schema. The `Pattern` option it replaces had to build its `Regex` at
  run time, which native AOT interprets, and no longer compiles.
- **Generated code cannot rely on the consumer's usings.** Every type and extension method is fully qualified
  in emitted code. A consumer with `ImplicitUsings` disabled would otherwise get a compile error in code they
  cannot edit.
- **`static virtual` and `static abstract` interface members are reachable only through a type parameter.**
  That's a C# rule, not a choice this library made — it's why the generator emits concrete members for each
  value object rather than relying on default interface implementations.

Next: [Entity Identifiers](./entity-identifiers.md), which build on the same generator for a different shape of
value.
