---
title: Use with MongoDB.Driver
sidebar_label: MongoDB
slug: /how-to/mongodb
description: Register MongoDB.Driver serializers that store every value object as the bare value its underlying type writes, translate LINQ over .Value, read through the rules, refuse to write an uninitialized value object its type rejects, carry the rules to the server as a $jsonSchema validator, and mint entity identifiers on insert.
---

# Use with MongoDB.Driver

```bash
dotnet add package AdCodicem.ValueObjects.MongoDB
```

```csharp skip
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)); // first, for a value object over Guid
ValueObjectBson.Register(typeof(Iban).Assembly);                                     // once, at start-up
```

Without it, MongoDB.Driver maps a value object through a class map, as it maps any type it has no serializer for. The
generated struct has no member a class map can set, so every value object is written as an empty sub-document, `{}`,
and read back as a default instance; a filter over one serializes the constant as `{}` too, so
`Where(x => x.Iban == iban)` matches every document, a range matches none, and `x.Iban.Value` is refused. A document
holding the bare value, which is what a `string` property wrote before the value object replaced it, cannot be read at
all. Nothing throws on the write.

`Register` adds a serialization provider that hands the driver a `ValueObjectBsonSerializer<TSelf, TValue>` for every
value object it meets, whichever assembly declares it. The serializer writes the value through the serializer the
driver holds for the underlying type, so a value object stores exactly what the primitive it replaces stores: existing
documents, indexes and the application's conventions for `Guid`, `DateTime` or `decimal` keep working. Each serializer
is closed over its value object at compile time, through the type arguments its descriptor hands back
([Run-time lookup](runtime-lookup.md#back-to-the-typed-path)).

The driver keeps its serializers for the whole process and never forgets one, so the order matters: register any
serializer of the application's own first, the `GuidSerializer` before anything else, then call `Register` once,
before anything is serialized or any class map is built. A second call with the same trust registers the assemblies it
is given and repeats the checks below; one with the other trust throws. Pass the assemblies declaring value objects, so
that their registration runs and the checks below see them.

## What `Register` checks

Two mistakes are refused at start-up, with an `InvalidOperationException` saying what to do, rather than on the first
document:

- **A value object over `Guid`, with no `Guid` representation.** The driver's default `GuidSerializer` has none,
  `GuidRepresentation.Unspecified`, and throws on the first `Guid` it writes. Register
  `new GuidSerializer(GuidRepresentation.Standard)` before calling `Register`. A legacy representation, a
  representation as text, or a serializer of the application's own is left alone. `Register` checks the value objects
  the registry holds when it runs, those of the assemblies it is given included; the serializer repeats the check for
  a value object the driver meets later, when it writes one and when it reads a binary value, and a member given a
  representation of its own with `[BsonGuidRepresentation]` writes. One consequence: an application that keeps the
  global representation unspecified and sets one on every member with the attribute is refused at start-up as soon as
  a value object over `Guid` is registered.
- **A value object the driver has already mapped.** One serialized before `Register` keeps the class map it was given,
  which writes `{}`, for the rest of the process. The exception names the value objects concerned, a construction of a
  generic value object, `ShipmentTag<Shipment>`, and a value object written by hand included: move the registration
  before whatever serialized them.

## How each underlying type is stored

A value object stores what the driver's default serializer of its underlying type stores:

| Underlying type | BSON |
| --- | --- |
| `bool` | Boolean |
| `char` | Int32, its UTF-16 code |
| `sbyte`, `byte`, `short`, `ushort`, `int` | Int32 |
| `uint` | Int32: a value above 2³¹ − 1 throws an `OverflowException`, as the bare `uint` does |
| `long` | Int64 |
| `ulong` | Int64: a value above 2⁶³ − 1 throws an `OverflowException`, as the bare `ulong` does |
| `decimal` | Decimal128 |
| `double`, `float` | Double |
| `DateOnly` | Date, at midnight UTC |
| `TimeOnly` | Int64, its ticks |
| `DateTime` | Date, in UTC and to the millisecond |
| `DateTimeOffset` | A document holding `DateTime`, `Ticks` and `Offset` |
| `TimeSpan` | String |
| `Guid` | Binary, subtype 4 under `GuidRepresentation.Standard` |
| `string` | String |
| `Int128`, `UInt128` | Refused: see below |

A serialization option on a member reaches the serializer of the underlying value as it would on the primitive,
`[BsonRepresentation(BsonType.String)]` with `AllowOverflow` and `AllowTruncation`, `[BsonGuidRepresentation]` and
`[BsonDateTimeOptions]` included, on a `TSelf?` member and on the elements of a collection too:

```csharp skip
public sealed class Order
{
    [BsonRepresentation(BsonType.String)]
    public PageNumber Page { get; set; }                      // "7", and x.Page > p compares as text

    [BsonGuidRepresentation(GuidRepresentation.CSharpLegacy)]
    public CustomerId Customer { get; set; }                  // binary subtype 3, as a legacy Guid property
}
```

MongoDB.Bson has no serializer for `Int128` or `UInt128`: a bare one is written `{}` and read back as zero. A value
object over either refuses, with a `BsonSerializationException` both ways, rather than lose its value, unless the
application registered a serializer of its own for the underlying type, before `Register`, which it then goes
through. `ValueObjectBson.Register<TSelf, TValue>()` throws a `NotSupportedException` for such a value object instead.

## Reading

Reads are strict by default. A MongoDB collection has no schema, and is often written by other services, scripts or
Compass, so a value read goes through `TryCreate`, which normalizes it — a lowercase IBAN with spaces reads as
`FR7630006000011234567890189` — and refuses one the type rejects:

```text
FormatException: An error occurred while deserializing the Iban property of class Shop.Order: The value read is not a
valid Iban: The value must be at least 15 characters long.
```

When it reads a document, the driver wraps the serializer's `FormatException`, which carries the rule's code, in its
own, naming the member; `ValueObjectErrors.TryGetCode` finds the code through the wrapping
([the code in an exception](../reference/errors.md#the-code-in-an-exception)). No message quotes the value read.

A trusted serializer reads through `CreateUnchecked`, for a collection the application alone writes, where validating
each value again only costs time:

```csharp skip
ValueObjectBson.Register(trusted: true, typeof(Iban).Assembly);       // every value object

ValueObjectBson.Register<Iban, string>(trusted: true);                // one value object, before the driver asks for it

BsonClassMap.RegisterClassMap<Order>(map =>                           // one member
{
    map.AutoMap();
    map.MapMember(x => x.Iban).SetSerializer(
        new ValueObjectBsonSerializer<Iban, string>(BsonSerializer.LookupSerializer<string>(), trusted: true));
});
```

Whatever the trust, two values are refused, since no value object can hold them: a value the serializer of the
underlying type cannot read — a BSON value of another type, a number beyond its range, a double with a fraction for an
integer, a date or a time beyond the range of the .NET type, text that is no `Guid` — with `value_object.not_parsable`,
and a BSON `null` read into an `Iban` with `value_object.required`: declare a member that can hold nothing as an
`Iban?`, which the driver reads as `null` without asking the serializer.

A field missing from a document is not read at all: the class map leaves the member as it was, a default instance.
`[BsonRequired]` on the member makes the driver refuse such a document.

## Writing

A value object that never went through `Create`, an `Iban` property never set, holds the default value, which its type
may reject. The serializer refuses to write it, with a `BsonSerializationException` carrying the rule's code: from
`InsertOne`, `InsertMany`, `ReplaceOne`, an update, and a query constant alike, since the driver serializes them all
through the same serializer. When it writes a document, from `InsertOne`, `InsertMany` or `ReplaceOne`, the driver
wraps that exception in its own, naming the member; from an update or a query constant, the serializer's own reaches
the caller. `ValueObjectErrors.TryGetCode` finds the code either way. Nothing is written. An `Iban?` holding a default
`Iban` is refused too, and an `Iban?` holding nothing is written as `null`. A type that accepts its zero, an `Amount`
with a minimum of 0, writes it. Any other instance went through `Create`, or was read by a trusted serializer, and is
written as it is.

An update through `.Value` is not checked: `Update.Set(x => x.Page.Value, 0)`, `Inc`, `Mul`, `Min` and `Max` hand
their value to the serializer of the underlying type, as a filter over `.Value` does, so the server stores the raw
value, `0` for a `PageNumber` whose minimum is 1, or pushes the stored one past a bound. Set the value object instead,
`Update.Set(x => x.Page, page)`, which went through `Create`. A collection given the
[validator built from the rules](#a-collection-validator-from-the-rules) refuses such an update on the server.

## Querying

The serializer tells LINQ that `Value` is the field itself, so comparing value objects and comparing their underlying
values translate to the same filter, and a method of the underlying type translates as it would on the primitive:

| LINQ | Filter |
| --- | --- |
| `x.Iban == iban` | `{ "Iban" : "FR7630006000011234567890189" }` |
| `x.Iban.Value.StartsWith("FR")` | `{ "Iban" : { "$regularExpression" : { "pattern" : "^FR", "options" : "s" } } }` |
| `x.Quantity > quantity`, `x.Quantity.Value > 2` | `{ "Quantity" : { "$gt" : 2 } }` |
| `ibans.Contains(x.Iban)` | `{ "Iban" : { "$in" : ["FR7630006000011234567890189"] } }` |
| `x.Backup == null` | `{ "Backup" : null }` |
| `x.Totals[CountryCode.France] > amount` | `{ "Totals.FR" : { "$gt" : … } }` |
| `x.Lines.Any(l => l.Quantity == quantity)` | `{ "Lines" : { "$elemMatch" : { "Quantity" : 2 } } }` |

Projections (`Select(x => x.Iban.Value.ToLowerInvariant())`), sorts, groups (`Sum(x => x.Quantity.Value)`), and
`Builders` filters, updates (`Update.Inc(x => x.Quantity.Value, 1)`, [unchecked](#writing)) and sorts translate the same
way. A dictionary keyed
by a value object over text is stored as a document keyed by the text, and `ContainsKey` and the indexer translate.

Two things follow from the server comparing what it stores:

- **Equality is the server's.** A value object whose equality ignores case, a `DocumentStatus` declared with
  `Comparison = StringComparison.OrdinalIgnoreCase`, keeps the spelling it was created with: one stored `FINAL` does not
  match `DocumentStatus.Final`, `final`.
- **A constant the type rejects is refused.** `x.Page == default(PageNumber)` fails as a write would. To find the
  documents another writer stored with a value the type refuses, compare `.Value`, `x.Page.Value == 0`, which matches
  them without reading them; reading them still fails, strictly.

## A collection validator from the rules

A strict read refuses another writer's value one document at a time, when the application reads it.
`ValueObjectBsonSchema.For<TDocument>()` builds a `$jsonSchema` validator from the rules of the value objects a document
holds, which makes the server refuse such a document when it is written, by another service, a script or Compass as
much as by the application:

```csharp skip
await database.CreateCollectionAsync("orders", new CreateCollectionOptions<BsonDocument>
{
    Validator = new BsonDocumentFilterDefinition<BsonDocument>(ValueObjectBsonSchema.For<Order>()),
});

// A collection that exists already
await database.RunCommandAsync<BsonDocument>(new BsonDocument
{
    { "collMod", "orders" },
    { "validator", ValueObjectBsonSchema.For<Order>() },
});
```

It reads the serializer the driver gives each member, and builds the class maps of the document and of the classes
nested in it, which the driver keeps for good. So call it at start-up, after `ValueObjectBson.Register`, which it
refuses to run before, after `EntityIdBson.Register`, whose generators a class map takes when it is built, and after any
`BsonClassMap.RegisterClassMap` of those types, which fails once one is built. It walks the document's class map, so
the element names follow the application's conventions, camel case included, and a serialization option on a member is
described as it writes: an `Order` holding an `Iban`, a `Quantity` from 1 to 999, an `Iban?` and a list of
`CountryCode`, a closed set, is validated by

```json
{
  "$jsonSchema": {
    "bsonType": "object",
    "required": ["Iban", "Quantity"],
    "properties": {
      "Iban": {
        "bsonType": "string", "minLength": 15, "maxLength": 34, "pattern": "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$"
      },
      "Quantity": { "bsonType": "int", "minimum": 1, "maximum": 999 },
      "Backup": {
        "bsonType": ["string", "null"], "minLength": 15, "maxLength": 34, "pattern": "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$"
      },
      "Countries": { "items": { "bsonType": "string", "minLength": 1, "maxLength": 2, "enum": ["FR", "BE", "LU"] } }
    }
  }
}
```

The server applies it with its own engine, PCRE2 for a pattern, and counts a length in code points. A validator
stricter than the type would refuse the application's own writes, so each rule is carried only where the server refuses
no value the type accepts:

| Rule | Keyword | Carried |
| --- | --- | --- |
| The BSON type the serializer of the underlying value writes | `bsonType` | Always, where the serializer says: `binData` for a `Guid` under the standard representation, `string` for one given `[BsonRepresentation(BsonType.String)]`, `long` for a `TimeOnly`'s ticks. |
| `MaxLength` | `maxLength` | For text stored as text. The server counts an emoji as one character where .NET counts two, so it is never stricter. |
| `MinLength` | `minLength` | For text stored as text: as declared when the published pattern is anchored at both ends, which keeps every value in the Basic Multilingual Plane; otherwise halved, rounded up, the fewest code points a value of that length holds. |
| The `[GeneratedRegex]` of `IValueObjectPatternValidator` | `pattern` | When both engines read it alike (below), and the regex sets no option but `CultureInvariant`, `Compiled`, `ExplicitCapture` or `NonBacktracking`: the text alone cannot carry `IgnoreCase`, `Multiline` or `IgnorePatternWhitespace`. A value object written by hand without the hook has its `Schema.Pattern` published as declared, whose options nothing can read: that pattern has to be the one its validation runs, with no option. |
| `IValueObjectMinimum<T>`, `IValueObjectMaximum<T>` | `minimum`, `maximum` | Written as the serializer writes a number, where the server compares stored numbers in the type's order: not for a value stored as text or as a date, which they do not compare, nor under `AllowOverflow` or an Int32 that wraps a time or a duration around, nor through a serializer of the underlying type that is not MongoDB.Bson's. |
| A closed set | `enum` | Written as the serializer writes each value, when every value the type accepts is stored as one of them: not for a set looked up ignoring case or as a culture compares, which stores the spelling it was given, not for a real or a decimal stored as text, nor for a date, whose equality ignores its kind or offset. |
| The description | `description` | Always. |
| A value object member | `required` | Unless the class map leaves it out: `[BsonIgnoreIfDefault]`, `IgnoreIfNull`, a `ShouldSerialize` method. A `TSelf?` is not required and adds `null` to its `bsonType` and its `enum`. |

A document nested in a member becomes its `properties`, and the elements of an array its `items`, where they hold a
value object; a class that refers to itself is described down to where it does. A dictionary, a member with a serializer
of the application's own, a 128-bit value object, which nothing stores, and whatever holds no value object are left
free, and nothing forbids an element the class map does not know. Rules only code checks, a checksum in an
`IValueObjectValidator<T>`, stay on the read path, which is one more reason to keep reads strict.

A pattern is written in the PCRE2 dialect when it holds literals, positive classes, `\d`, `\w`, `\s` and the `\p{…}` of
a general category other than `C` and `Cs`, no surrogate, quantifiers up to 65535, groups, alternation and the anchors
`^`, `$`, `\A`, `\z` and `\Z`; a character outside printable ASCII is written `\x{…}`. Each class escape is written as
the list of the characters .NET matches it with, read from .NET's own Unicode tables: the server reads `\d`, `\w` and
`\s` as ASCII, and a category with the tables of its PCRE2, which lag .NET's (MongoDB 8.0 reads U+0ECE as no mark and
U+A7CB as no letter, which Unicode 15 and 16 added). Listed, a class means on the server what it means in .NET, whatever
Unicode version the server knows, at the cost of a longer pattern: `\w` lists some 500 ranges. A pattern holding `.`, a
negated class, `\D`, `\W`, `\S` or `\P{…}` is left out, its lengths kept: each matches half of an emoji in .NET and a
whole one on the server, so `^.{2}$` accepts one emoji in .NET and refuses it there. So is a word boundary, which
matches no character but which the engines place apart, since PCRE2 tells a word character by its ASCII `\w`; and a
class subtraction, a named block such as `\p{IsGreek}`, or a pattern the server could not compile, its repeated groups
copied and its classes listed, any of which would fail the whole validator. The e-mail pattern
`^[^@\s]+@[^@\s]+\.[^@\s]+$` is left out for its negated classes.

The validator is stricter than the reader in two ways. `bsonType` names the BSON type the serializer writes, so a
`Quantity` stored as an Int64 by another writer, `5L`, is refused by the server, where the serializer would read it. And
`required` refuses a document missing a value object member, which the reader reads, leaving the member as it was
([Reading](#reading)).

The server checks an update as it checks an insert, so it also refuses what no serializer sees, an
`Update.Inc(x => x.Quantity.Value, 1)` past the maximum. A refused document is reported as for any validator: a
`MongoWriteException`, or a `MongoBulkWriteException` from `InsertMany`, whose write error has code 121, "Document
failed validation", with details naming the field and the keyword
([errors](../reference/errors.md#a-value-refused-on-write)). `validationLevel` and `validationAction` are the
application's to set beside the validator.

## Entity identifiers as `_id`

```bash
dotnet add package AdCodicem.ValueObjects.Identifiers.MongoDB
```

```csharp skip
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
ValueObjectBson.Register(typeof(AccountId).Assembly);
EntityIdBson.Register(typeof(AccountId).Assembly);     // before any class map of a document holding one is built

public sealed class Account
{
    public AccountId Id { get; set; }                     // the _id, stored as acc_…
    public string Name { get; set; } = "";
}

await accounts.InsertOneAsync(new Account { Name = "Ada" }); // account.Id is now acc_2k7x9…
```

An `[EntityId]` left unassigned holds no valid value, and the serializer refuses to write it. `EntityIdBson.Register`
gives the driver an `EntityIdGenerator<TId>` for every entity identifier registered, those of the assemblies given
included, which mints one through `TId.New()`, under the clock and the entropy of
[`ValueObjectIds`](../entity-identifiers.md#the-ambient-provider), as the driver mints an `ObjectId`. An identifier
already assigned is kept. The driver gives a class map's id member the generator registered for its type when it builds
the class map, so register before any class map of a document holding one is built, which `ValueObjectBsonSchema.For`
does too; a class map built by hand sets it with `SetIdGenerator(new EntityIdGenerator<AccountId>())`. A generator of
the application's own, registered first, is kept; the checker the driver gives a type that has none, under
`BsonSerializer.UseZeroIdChecker` or `UseNullIdChecker`, mints nothing, and is replaced. `EntityIdBson.Register<TId>()`
registers one identifier. A value object over `Guid` used as `_id` gets no generator: an unassigned one is written as
`Guid.Empty` when the type accepts it.

## Generic value objects and value objects written by hand

A construction of a generic value object, `Reference<PurchaseOrder>`, a value object written by hand, and one in an
assembly whose registration has not run, are described by reflection the first time the driver asks for them, which
only the JIT can run. `ValueObjectBson.Register<Reference<PurchaseOrder>, string>()` registers one without reflection;
it goes through `BsonSerializer.TryRegisterSerializer`, so registering the serializer the provider would give changes
nothing, and a different one, of the other trust, throws.

## Native AOT

MongoDB.Driver maps documents through class maps it builds by reflection, and is not compatible with trimming or native
AOT, so neither package claims to be either. Neither adds reflection of its own for a value object or an identifier that
registered itself: the serializers and the id generators are closed through the descriptors' visitors. The validator
reads the options of a pattern's regex through the type's interface map.

## Azure Cosmos DB for MongoDB and Azure DocumentDB

Both are reached through the same driver, so the serializers and the id generators apply as they do against MongoDB.
This was not run against either service. A collection validator is the server's to apply: check that the service
supports `$jsonSchema` validation, and the keywords the validator uses, before relying on it.

## MongoDB.EntityFrameworkCore

The MongoDB provider for Entity Framework Core does not go through these serializers: it maps value objects through
`ConfigureValueObjects`, which it requires ([Other providers](ef-core.md#other-providers)).
