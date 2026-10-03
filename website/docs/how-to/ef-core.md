---
title: Use with Entity Framework Core
sidebar_label: Entity Framework Core
slug: /how-to/ef-core
description: Map every value object of an assembly to its underlying column type in one call, size columns from declared rules, choose when reads are validated, and never store a value a type rejects.
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
`Where(a => a.Iban == iban)` becomes an ordinary `WHERE` on the column.

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

## Public identifiers

`[EntityId]` identifiers have a convention of their own, from
`AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore`, which maps them to fixed-width, non-Unicode columns.
Call it in addition to `ConfigureValueObjects`; [Public identifiers](../tutorials/public-identifiers.md#store-it)
shows how. It sets the converter of the identifiers too, so a strict context passes `strict: true` to both calls.
