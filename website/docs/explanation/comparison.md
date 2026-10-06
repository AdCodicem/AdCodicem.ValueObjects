---
title: Compared With Other Libraries
sidebar_label: Compared with other libraries
slug: /explanation/comparison
description: How AdCodicem.ValueObjects compares with Vogen, StronglyTypedId and Thinktecture.Runtime.Extensions, including where each of them is the better choice.
---

# Compared with other libraries

AdCodicem.ValueObjects is not the first answer to primitive obsession in .NET. Three established libraries
generate value objects too, and each of them is the better choice for some projects. This page is meant to help
you tell which one yours is.

The comparison was made in September 2026 against **Vogen 8.0.7**, its latest stable release (9.0 was in beta:
9.0.0-beta.1 came out in July 2026), **StronglyTypedId 1.0.0-beta08** — the version most of its users install,
although nuget.org still lists 0.2.1 as the latest stable — and **Thinktecture.Runtime.Extensions 10.5.0**, from their
source, their documentation and, where the documentation was unclear, a compiled test project. Libraries move: if
something here is out of date, [open an issue](https://github.com/AdCodicem/AdCodicem.ValueObjects/issues) and it will
be corrected.

## At a glance

| | AdCodicem.ValueObjects | Vogen | StronglyTypedId | Thinktecture |
| --- | --- | --- | --- | --- |
| Consumer frameworks | .NET 10 and later | `netstandard2.0` and later | older frameworks too, including .NET Framework, with a .NET 7+ SDK | .NET 8 and later |
| Type kinds | `readonly struct` | class, struct, record | struct | class, struct |
| Underlying types | 22 built-in types | any type but a collection | `Guid`, `int`, `long`, `string`; others through templates | any type |
| Length, pattern and range declared once, on the type | yes: length on the attribute, range through typed bound hooks, a pattern through a source-generated regex hook | no | no | no |
| Validation and normalization hooks | interfaces, checked by the compiler | methods found by name | none | a partial method with `ref` parameters |
| Rejection carries a stable error code | yes | no, a message | — | no, a message (custom error types possible) |
| `default` and `new T()` rejected at build time | yes | yes | no | yes, for structs |
| String comparison | ordinal, or declared per type | ordinal, configurable | ordinal | case-insensitive by default, configurable |
| System.Text.Json | yes | yes | yes | yes |
| Source-generated `JsonSerializerContext` | declared on the context, at compile time | a factory passed in the options at run time | converters passed in the options | not documented |
| EF Core mapping | every value object of an assembly in one call | per property, or a marker class listing the types | per type | every value object in one call |
| Column size from the type's rules | yes | no | no | no, a max-length strategy can be configured |
| EF Core reads validate | on request (`strict: true`) | by default | — | no, reads use the constructor |
| ASP.NET Core model binding | yes | through the `TypeConverter` | through the `TypeConverter` | yes |
| Problem details carry the violated rule's code | MVC controllers and minimal APIs | no | no | no |
| OpenAPI | built-in stack and Swashbuckle 10, with lengths, pattern, bounds, `enum` and its names | type and format; Swashbuckle or built-in stack | none | Swashbuckle, type of the key |
| FluentValidation | yes | third-party package | no | no |
| Dapper | yes | yes | through a template | no |
| Other serializers and stores | Newtonsoft.Json; XML (`XmlSerializer`, `DataContractSerializer`, opted in per assembly, read with validation, rules in the XSD) | Newtonsoft.Json, LinqToDB, ServiceStack.Text, Orleans, MessagePack, BSON, XML (read without validation) | Newtonsoft.Json | Newtonsoft.Json, MessagePack |
| Structured logging | Serilog: `{@X}`, and `{X}` on request, as the underlying value | no | no | Serilog destructuring policy, `{@X}` only |
| Contract test kit for your own types | yes | no | no | no |
| Prefixed public identifiers (`acc_…`) | yes | no | no | no |
| Beyond single values | no | no | no | complex value objects, smart enums, discriminated unions |
| Licence | MIT | Apache-2.0 | MIT | BSD-3-Clause |

## Where this library differs

**A rule is declared once and reaches every boundary.** `MaxLength = 34` validates, sizes the EF Core column and
becomes the OpenAPI `maxLength`; the bound of `IValueObjectMinimum<T>`, and the `[GeneratedRegex]` behind
`IValueObjectPatternValidator`, do the same for the schema; known values become the `enum`, under the names a
generated client gives its members. In the other three, a length or a pattern is code inside
a validation method, so the column and the schema have to be told separately — which is exactly the drift that
primitive obsession produces.

**A rejection is data a client can act on.** Validation returns a `ValidationResult` struct holding a stable
code and a message, and allocates nothing when the value is valid. The code travels to the problem details of
MVC controllers, for a JSON body, read by System.Text.Json or Newtonsoft.Json, as for a query value, and of minimal
APIs, native AOT included, for a route, query or header value, and for a body where `ThrowOnBadRequest` is on, to
FluentValidation failures and to every exception an integration throws, so an API client branches on
`value_object.too_long` rather than on English.

**Rules are found by interface, not by name.** `IValueObjectValidator<T>` and `IValueObjectNormalizer<T>` let the
compiler check each signature, and `VO0011` reports the one mistake left: a rule written without its interface,
which would otherwise never run.

**Source-generated JSON is declared, not remembered.** Naming `ValueObjectJsonConverterFactory` in
`[JsonSourceGenerationOptions]` works at compile time, on the context itself. With Vogen and StronglyTypedId the
converters have to be added to the options at run time instead, and forgetting them can fail quietly: in a test
against Vogen 8.0.7, the value object was written as `{}` and read back uninitialized.

**The integrations go further than serialization.** Problem details, FluentValidation rules that defer to the type, an
OpenAPI schema carrying the constraints, a contract kit that tests your own types, and Stripe-style public identifiers
are part of the library rather than left to you. So is [structured logging](../how-to/logging.md#serilog): with
`AdCodicem.ValueObjects.Serilog`, Serilog logs a value object as the value it carries, with `@` and, on request, without
it, where Thinktecture's destructuring policy, `Destructure.UsingThinktectureRuntimeExtensions()`, covers `{@Value}`
alone and leaves `{Value}` to `ToString()`, a number then reaching the sink as a string.

## Where the others are stronger

**They run in more places.** This library requires .NET 10 or later. Vogen targets `netstandard2.0`, StronglyTypedId runs
on older frameworks including .NET Framework, and Thinktecture supports .NET 8. For an application that cannot
move to .NET 10, the choice is made.

**They accept more shapes.** Vogen wraps any type but a collection, as a class, a struct or a record;
Thinktecture accepts any key, generic ones included. This library is deliberately limited to 22 underlying types
and to `readonly struct`: [Design decisions](../design-decisions.md) explains why, but if you need a class or a
`Uri`, it is not for you.

**They cover more stores and serializers.** Vogen generates support for LinqToDB, ServiceStack.Text, Orleans,
MessagePack and MongoDB's BSON; Thinktecture for MessagePack. Both this library and Vogen implement XML serialization
on request, apart: Vogen's generated `ReadXml` assigns the value straight from the reader, with neither validation nor
normalization, and its option makes the struct's fields writable, where [this library's](../how-to/xml.md) reads
through the type's rules, keeps the fields `readonly` and publishes the rules as XSD facets.

**Thinktecture goes beyond single values.** Complex value objects with several members, smart enums and
discriminated unions are out of scope here.

**Vogen validates what it reads from the database by default.** This library skips validation on the EF Core
read path unless asked, a performance choice that assumes the application owns its tables. For a database
several systems write to, Vogen's default is the safer one; here, `strict: true` gives the same behaviour.

**They are proven.** Vogen and StronglyTypedId have millions of downloads and years of production use behind
them. This library was first published in September 2026 and is still at 0.x.

## Choosing

- **On .NET 10 or later, with an API and a database,** where a rule should be written once and reach the schema and the
  column: this library is built for that.
- **On an older framework, or with a store this library does not cover:** Vogen.
- **Only strongly-typed identifiers, and nothing to validate:** StronglyTypedId does that with the least
  ceremony.
- **Smart enums, discriminated unions or multi-member value objects** alongside single values: Thinktecture.

Moving from one of them? [Migrate from another library](../how-to/migrating.md) maps each one's surface onto
this one.
