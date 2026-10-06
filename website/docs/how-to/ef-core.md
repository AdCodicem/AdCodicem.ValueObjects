---
title: Use with Entity Framework Core
sidebar_label: Entity Framework Core
slug: /how-to/ef-core
description: Map every value object of an assembly to its underlying column type in one call, size columns from declared rules, choose when reads are validated, never store a value a type rejects, compile the model, and work with JSON columns, raw SQL, other providers, bulk extensions, Always Encrypted and data masking.
---

# Use with Entity Framework Core

```bash
dotnet add package AdCodicem.ValueObjects.EntityFrameworkCore
```

## Map every value object at once

```csharp skip
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    => builder.ConfigureValueObjects(typeof(Iban).Assembly);
```

One call maps every value object declared in the assembly to its underlying column type, with a value converter
and a value comparer. It runs once, while the model is built; nothing happens per query or per row. Pass several
assemblies if the value objects live in more than one.

## Columns sized by the type

A value object declaring `MaxLength` also sizes its column: an `Iban` with `MaxLength = 34` becomes
`character varying(34)` on PostgreSQL and `nvarchar(34)` on SQL Server, instead of unbounded text. A `Guid` value
object lands in the provider's native GUID column. Change the rule on the type, and the next migration follows.

LINQ queries compare value objects as they would compare the underlying values:
`Where(a => a.Iban == iban)` becomes an ordinary `WHERE` on the column, with `iban` sent as a parameter.

That holds for a value captured in a variable. A value created inside the predicate is evaluated while the query is
translated and written into the SQL as a literal: `Where(a => a.Iban == Iban.Create("DE89370400440532013000"))`
gives `WHERE [a].[Iban] = N'DE89370400440532013000'`, and `Quantity.Create(3)` gives `= 3`. Each value then makes a
statement of its own in the plan cache, and on an Always Encrypted column the query fails, since a literal cannot be
encrypted. Hoist the value into a variable, or wrap it in `EF.Parameter(...)`:

```csharp skip
var iban = Iban.Create("DE89370400440532013000");
var accounts = await db.Accounts.Where(a => a.Iban == iban).ToListAsync();
```

A static property, `Currency.Eur`, is sent as a parameter already.

## Validation on read

Materializing a row does **not** validate the value again: it uses `CreateUnchecked`. The read path is the
hottest one in most applications, and it reads values this same application validated when it wrote them.

For a table another system also writes to, turn validation back on:

```csharp skip
builder.ConfigureValueObjects(strict: true, typeof(Iban).Assembly);
```

It then costs one normalization and validation per materialized value. A value the domain would refuse fails
the query with the value object's `ValueObjectException`; one it would only normalize comes back normalized.
On a key, that has a consequence: a row stored as `fr76 3000 …` is tracked under `FR763000…`, which is not the key
the table holds. An update or a delete of that row through the strict context matches no row, and `SaveChanges`
throws a `DbUpdateConcurrencyException`, as it does for a row another writer deleted. Normalize such keys in the
table before relying on strict reads to write them back.

`strict` is read once per context type, not per instance. Entity Framework Core builds the model the first time a
context type is used and caches it for every later instance, so a context whose constructor chooses `strict` from an
argument gets whichever model was built first, and the other value does nothing. Give strict reads a context type of
their own, which may derive from the other, or a custom `IModelCacheKeyFactory` that puts the choice in the key.

## Validation on write

An entity whose value object was never set holds an instance that never went through `Create`, and so the default
value: an empty `Iban`, a `Guid.Empty` customer identifier. Stored as it stands, it would come back from every later
read as an instance holding a value its own rules refuse, since the read trusts the column. The converters refuse it
instead:

- on a property of the value object's type, `SaveChanges` throws a `DbUpdateException` whose inner exception is the
  value object's `ValueObjectException`, naming the type and the rule, and nothing is written;
- on an optional property, `Iban?`, the column takes a `NULL`, and stores one.

Over a value type, nothing tells the default from a constructed zero, so the converter validates it: a type that
accepts its zero, an `Amount` with a minimum of 0, stores it, and an identifier that must never be `Guid.Empty` says
so with a validator, as the [`CustomerId` of the tutorial](../tutorials/request-to-database.md#the-domain) does.
Any other instance went through `Create`, and is written without being validated again. The strict converters write
the same way.

`dotnet ef dbcontext optimize` converts the default of each property to write it into the compiled model. That writes
no column, and runs under `EF.IsDesignTime`, so the converters let it through.

## One property, differently

To depart from the convention for a single property:

```csharp skip
modelBuilder.Entity<BankAccount>()
    .Property(account => account.Iban)
    .HasValueObjectConversion<Iban, string>(strict: true);
```

Prefer the convention everywhere else. And do not write `HasConversion` by hand for a value object: you would
lose the generated comparer, and with it correct change tracking for a type whose comparison is not ordinal.

## Generic value objects

A generic value object, `Reference<TOwner>` or `Catalog<TItem>.Stock`, has no single type the convention could map
up front. `ConfigureValueObjects` maps its generic definition instead, and each property holding a construction,
`Reference<PurchaseOrder>` say, gets the converter, the comparer and the column length closed over that construction,
as the model meets it. `strict: true` applies to them as to any other value object, and a property mapped explicitly
with `HasValueObjectConversion<Reference<PurchaseOrder>, string>()` keeps what it was mapped with.

## 128-bit value objects

Entity Framework Core maps neither `Int128` nor `UInt128`, on any provider, so the convention leaves a value object
over either alone: the column it lands in is yours to choose, with a converter of your own. Give it the comparer the
convention would have given it, so that change tracking compares the way the value object does:

```csharp skip
builder.Properties<LedgerBalance>()
    .HaveConversion<LedgerBalanceToDecimal, ValueObjectComparer<LedgerBalance>>()
    .HavePrecision(38, 0);

internal sealed class LedgerBalanceToDecimal() : ValueConverter<LedgerBalance, decimal>(
    balance => (decimal)balance.Value,
    value => LedgerBalance.Create((Int128)value));
```

A numeric column sorts and compares as numbers do, but ADO.NET carries it through `System.Decimal`, which holds
about ±7.9 × 10²⁸: a value beyond that can be neither written nor read back. A text column holds the whole range,
and sorts and compares as text does, so `9` comes after `10`.

## Value objects as keys

A value object makes a perfectly good key, primary or foreign. For a string key declared with
`Comparison = StringComparison.OrdinalIgnoreCase`, set a case-insensitive collation on the column, or the
database and the application will disagree about which values are equal.

## Compiled models

`dotnet ef dbcontext optimize` turns the model into code that names each converter and comparer. Every value object
the conventions map compiles there, whatever its underlying type, generic or not, optional or not, identifiers
included. An optional property, `Quantity?`, is compared by `NullableValueObjectComparer<Quantity>`: given the comparer
of the value object, Entity Framework Core would wrap it in code that does not compile. A property you map with a
converter of your own, a [128-bit value object](#128-bit-value-objects) for one, needs the same for its optional form:

```csharp skip
builder.Properties<LedgerBalance?>()
    .HaveConversion<LedgerBalanceToDecimal, NullableValueObjectComparer<LedgerBalance>>();
```

Regenerate the compiled model when a rule that shapes a column changes, `MaxLength` say, as after any other change to
the model.

One difference remains, on a value object over `string` whose property has a database default, through
`HasDefaultValue` or `HasDefaultValueSql`. Left unset, the property holds `default(T)`, which a model built at run
time takes for unset, so the database default applies. A compiled model rebuilds that sentinel from the empty text,
which gives an instance unequal to `default(T)`, so it writes the property: the empty text, for a type that accepts
it, or a [refusal](#validation-on-write), for one that does not. An optional property, `Currency?`, is not affected,
and neither is a value object over a value type.

A [strict context](#validation-on-read) does not run on a compiled model yet when one of its value objects refuses the
default of its underlying type: `0` for a quantity of at least one, the empty text for a required string, an empty
`Guid`, any identifier. A compiled model rebuilds the sentinel of every property from that default, through the
property's converter, which in a strict context validates it, so the first entity the context tracks, read by a query
or passed to `Add`, fails with a `ValueObjectException`. A query with `AsNoTracking()` reads and validates as on a
model built at run time. Keep a strict context on the model built at run time, or read through it untracked.

CI compiles both models, for the JIT and for native AOT, of a context mapping every value object the conventions map,
required and optional, beside a strict one, and takes the first on a round trip through SQL Server.

### Native AOT

`dotnet ef dbcontext optimize --precompile-queries --nativeaot` writes a model whose code calls the conversions of
each converter itself, `ValueObjectConverter<OrderId, Guid>.ToProvider` and `FromProvider`. They are public for it,
and hidden from IntelliSense, since nothing else has a reason to call them. Such a model builds and publishes with
native AOT, and `SaveChanges` writes, but queries do not run yet. Two bugs of Entity Framework Core's precompiled
queries, which any converted type reproduces without this library, fail a query that materializes an entity whose key
has a converter, and a query that takes a parameter of a converted type. Until they are fixed, native AOT is out of
reach for an application reading value objects through Entity Framework Core, and the package does not claim to be
AOT-compatible.

The code it writes does not always compile either, with or without this library: Entity Framework Core 10 writes
precompiled code that does not compile for an untracked query and for a sealed entity type, and model code that
cannot tell a type of the model named as one of its own internal types, `Reference<T>` among them, from that type.

The conventions never run in the native binary. Entity Framework Core builds no model under native AOT, and refuses to
start without a compiled one, so `ConfigureValueObjects` and `ConfigureEntityIds` run only where a model is built:
under the JIT, and in `dotnet ef dbcontext optimize`, which writes the model the binary reads.

## Complex types and JSON columns

Complex types, `ToJson`, complex collections and owned types mapped to JSON work with the convention, and the rules
still shape the query: a value object declaring `MaxLength = 34` is read as
`JSON_VALUE([i].[Shipping], '$.Account' RETURNING nvarchar(34)) = @iban` on SQL Server. A collection of value objects
inside them fails, as a collection of value objects does anywhere in the model.

## Raw SQL

`FromSql` and `SqlQuery` know nothing of the convention. `FromSql($"… WHERE Iban = {iban}")` throws
`The current provider doesn't have a store type mapping for properties of type 'Iban'`, and so does
`SqlQuery<Iban>`. Pass `.Value` as the parameter, and create the value object from a scalar result:

```csharp skip
var orders = await db.Orders.FromSql($"SELECT * FROM Orders WHERE Iban = {iban.Value}").ToListAsync();
```

## Other providers

**Azure Cosmos DB**, through Microsoft.EntityFrameworkCore.Cosmos 10.0.12: the convention applies, and equality, range
and nullable queries translate. `ibans.Contains(x.Iban)` throws
`Couldn't find array type mapping when applying item/array mappings`, as it does for any value converter, one written
by hand included: write a chain of `||`, or query the underlying values. `MaxLength` has no effect there. This was
checked offline, on the text of the queries.

**MongoDB**, through MongoDB.EntityFrameworkCore 10.0.4: the convention is mandatory. Without it, the provider writes a
value object as `{}` and reads it back as a default instance, where a relational provider refuses to build the model.
With it, values are stored bare, queries translate, and strict reads validate. On a standalone `mongod`, which has no
transactions, `SaveChanges` needs `db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never`; a replica set
does not.

## Bulk extensions and linq2db

**EFCore.BulkExtensions 10.0.1** reads the model, so the convention is all it needs: `BulkInsert`, `BulkUpdate`,
`BulkRead` keyed on a value object, `BulkInsertOrUpdate` and `BulkDelete` honour the converter, the nullability and
the column sizes, and a strict context validates on `BulkRead`. It writes through the same converters, so a default its
type rejects is refused as [on write](#validation-on-write) (inferred, not run).

**Z.EntityFramework.Extensions 10.105.8.1**, a commercial package: `BulkInsert`, `BulkMerge` on a value-object key and
`UpdateFromQuery` work. `WhereBulkContains` over a list of value objects fails,
`'Iban' is not a member of type 'Probe.Domain.Iban'`: pass anonymous objects instead, `new { Iban = iban }`.

**linq2db.EntityFrameworkCore 10.6.0**: after `LinqToDBForEFTools.Initialize()`, `ToLinqToDB()` queries, `BulkCopy`,
`Merge` and `Update().Set(…)` use the converters and the column sizes of the model, `DECLARE @iban NVarChar(34)`.
Compare value objects there too, not `.Value`, which linq2db cannot translate.

## Always Encrypted

The converter runs before SqlClient encrypts, so deterministic equality, `Contains`, reads and `ExecuteUpdate` work on
an encrypted column. Three things trip it:

- A parameter wider than the column is refused. Declare a `MaxLength` no greater than the column's size: a value object
  without one is sent as `nvarchar(4000)`.
- A `varchar` column needs the value object mapped as non-Unicode:
  `configurationBuilder.Properties<Iban>().AreUnicode(false)`.
- A value object created inside a predicate, `Iban.Create("…")`, becomes a literal, which cannot be encrypted: hoist
  it into a variable, as [Columns sized by the type](#columns-sized-by-the-type) shows.

`BulkInsertOrUpdate` of EFCore.BulkExtensions does not work on randomized columns.

## Dynamic data masking

With EntityFrameworkCore.Extensions 10.2.0, a mask is one line per type, and covers its optional properties too:

```csharp skip
configurationBuilder.Properties<Iban>()
    .HaveAnnotation(AnnotationConstants.DynamicDataMasking, MaskingFunctions.Partial(2, "XXXXXXXX", 4));
```

A principal without `UNMASK` reads `DEXXXXXXXX3000`, which the default read, trusting the column, turns into an `Iban`
its rules refuse, and not a default one; a masked `int` reads as `0`, a default `Quantity`. Use
[strict reads](#validation-on-read) on a masked column.

## Public identifiers

`[EntityId]` identifiers have a convention of their own, from
`AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore`, which maps them to fixed-width, non-Unicode columns.
Call it in addition to `ConfigureValueObjects`; [Public identifiers](../tutorials/public-identifiers.md#store-it)
shows how. It sets the converter of the identifiers too, so a strict context passes `strict: true` to both calls.
