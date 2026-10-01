---
title: Use with Entity Framework Core
sidebar_label: Entity Framework Core
slug: /how-to/ef-core
description: Map every value object of an assembly to its underlying column type in one call, size columns from declared rules, and choose when reads are validated.
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
the table holds, so an update or a delete of that row through the strict context finds nothing to change.
Normalize such keys in the table before relying on strict reads to write them back.

## One property, differently

To depart from the convention for a single property:

```csharp skip
modelBuilder.Entity<BankAccount>()
    .Property(account => account.Iban)
    .HasValueObjectConversion<Iban, string>(strict: true);
```

Prefer the convention everywhere else. And do not write `HasConversion` by hand for a value object: you would
lose the generated comparer, and with it correct change tracking for a type whose comparison is not ordinal.

## Value objects as keys

A value object makes a perfectly good key, primary or foreign. For a string key declared with
`Comparison = StringComparison.OrdinalIgnoreCase`, set a case-insensitive collation on the column, or the
database and the application will disagree about which values are equal.

## Public identifiers

`[EntityId]` identifiers have a convention of their own, from
`AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore`, which maps them to fixed-width, non-Unicode columns.
Call it in addition to `ConfigureValueObjects`; [Public identifiers](../tutorials/public-identifiers.md#store-it)
shows how.
