---
title: Use with MongoDB.Driver
sidebar_label: MongoDB
slug: /how-to/mongodb
description: Register MongoDB.Driver serializers that store every value object as the bare value its underlying type writes, translate LINQ over .Value, read through the rules, and refuse to write an uninitialized value object its type rejects.
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
`Update.Set(x => x.Page, page)`, which went through `Create`.

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

## Generic value objects and value objects written by hand

A construction of a generic value object, `Reference<PurchaseOrder>`, a value object written by hand, and one in an
assembly whose registration has not run, are described by reflection the first time the driver asks for them, which
only the JIT can run. `ValueObjectBson.Register<Reference<PurchaseOrder>, string>()` registers one without reflection;
it goes through `BsonSerializer.TryRegisterSerializer`, so registering the serializer the provider would give changes
nothing, and a different one, of the other trust, throws.

## Native AOT

MongoDB.Driver maps documents through class maps it builds by reflection, and is not compatible with trimming or native
AOT, so this package does not claim to be either. It adds no reflection of its own for a value object that registered
itself.

## Azure Cosmos DB for MongoDB and Azure DocumentDB

Both are reached through the same driver, so the serializers apply as they do against MongoDB. This was not run against
either service.

## MongoDB.EntityFrameworkCore

The MongoDB provider for Entity Framework Core does not go through these serializers: it maps value objects through
`ConfigureValueObjects`, which it requires ([Other providers](ef-core.md#other-providers)).
