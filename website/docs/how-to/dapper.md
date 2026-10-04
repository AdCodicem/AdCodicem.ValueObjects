---
title: Use with Dapper
sidebar_label: Dapper
slug: /how-to/dapper
description: Register Dapper type handlers for every value object of an assembly, so raw SQL reads and writes the underlying value, and declare them for Dapper.AOT.
---

# Use with Dapper

```bash
dotnet add package AdCodicem.ValueObjects.Dapper
```

```csharp skip
ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);   // once, at start-up
```

That registers a type handler for every value object of the assembly. From then on a value object can be a query
parameter and a column of a result, with no projection:

```csharp skip
var account = await connection.QuerySingleOrDefaultAsync<BankAccount>(
    "SELECT iban AS Iban, balance AS Balance FROM accounts WHERE iban = @iban",
    new { iban });
```

Dapper keeps its handlers in a process-wide table, so the call belongs at start-up rather than per connection.
Pass several assemblies if the value objects live in more than one. Calling it again changes nothing: a value object
Dapper already has a handler for keeps it, including one the application registered itself. What is handled is read
from Dapper's own table, so after `SqlMapper.ResetTypeHandlers()` — between tests, say — calling it again registers
every handler anew. Each handler is closed over its value object at compile time, through the type arguments the
value object's descriptor hands back ([Run-time lookup](runtime-lookup.md#back-to-the-typed-path)), so this package
builds them without dynamic code. Dapper itself does not: it files each handler in a cache it closes over the type at
run time, and reads rows through code it emits, so under native AOT the registration still fails inside Dapper. Native
AOT goes through [Dapper.AOT](#dapperaot).

## Parameters

A value object goes out as its underlying value. One over `string` that says more than its type also declares the
column the [Entity Framework Core](ef-core.md) conventions map it to:

- an [entity identifier](../entity-identifiers.md) as fixed-length, non-Unicode text of its exact length, the
  `char(n)` of `ConfigureEntityIds`;
- a value object declaring `MaxLength` as Unicode text of that length, `nvarchar(34)` for an IBAN on SQL Server.

Left to itself, SqlClient sends a string as `nvarchar` of the value's own length, which also caches one plan per
length, and SQL Server converts a `char` or `varchar` column to compare it with one, which costs the index seek of a
query filtering on an identifier. Npgsql sends every string as `text` whatever it declares, and PostgreSQL compares a
`character(n)` column with `text` by converting the column, which costs its index the same way: cast the parameter in
the query, `WHERE id = @id::bpchar` or `WHERE id = CAST(@id AS character(25))`. Only an identifier is known to be
ASCII, so no other value object goes out as non-Unicode text, which would lose the characters the column's code page
lacks. A value longer than the declared length, which a row read without validation can hold, gets a parameter as
long as itself: both providers would otherwise cut it short, silently.

A value object that never went through `Create` holds the default value, which its type may reject. The handler
refuses such a parameter with a `DataException`, "The value to write is not a valid Iban: …", before the command is
sent, rather than store what every later read would trust. Dapper hands the handler the value object whether the
parameter is an `Iban` or an `Iban?` holding one, so the handler cannot tell a column that takes a `NULL` from one that
does not, and refuses either way: an `Iban?` holding nothing is what writes a `NULL`. Over a value type, a type that
accepts its zero, an `Amount` with a minimum of 0, writes it.

## What a read trusts

A column the provider returns as the underlying type is read with `CreateUnchecked`, on the same reasoning as the
[Entity Framework Core](ef-core.md#validation-on-read) read path: this application validated the value when it wrote
it. Some providers return another type of the date and time family than the underlying one, and those are converted
first: SQL Server returns a `DateTime` for a `date` column and a `TimeSpan` for a `time` column, Npgsql a `DateOnly`
and a `TimeOnly` for them, and a UTC `DateTime` for a `timestamptz`. A `DateTime` that says nothing of its zone — a
SQL Server `datetime2`, a PostgreSQL `timestamp` — cannot become a `DateTimeOffset`, and is refused. So is any value
the handler cannot convert to the underlying type, another type or one out of its range: each throws a
`DataException` naming the type the provider returned and the value object it was read into.

A column holding text where the underlying type is not text, or the reverse, is the exception: the value object did
not write it. Text read into a value object whose underlying type is not `string` — a Guid or a number kept in a
text column — is parsed the way the value object parses text, so it is normalized and validated. A number or a
`Guid` read into a value object over `string` — the digits of a reference kept in a numeric column, a reference kept
in a `uuid` or `uniqueidentifier` column — is turned into text, then normalized and validated through `TryCreate`. A
`Guid` becomes text in its `D` form, lowercase, as `Guid.ToString()` writes it. Either way, a value the value object refuses throws a `DataException`
carrying the rule's message.

## Generic value objects

Dapper looks a handler up by the exact type, ahead of any query, and the constructions of a generic value object are
known only to the application. `AddValueObjectHandlers` handles only the constructions something resolved before it
ran, which depends on the order of start-up; register each construction a query reads or writes, once, at start-up:

```csharp skip
ValueObjectDapper.AddValueObjectHandler<Reference<PurchaseOrder>, string>();
```

It registers the same handler, for the value object and its nullable form, closed at compile time with no assembly
scan, and keeps a handler the application registered itself. It works for a value object that is not generic too. The
parameter of a construction declares its column as any other value object's does, from the length the type declares,
`TSelf.Schema`, without asking the registry.

## 128-bit value objects

No ADO.NET provider takes an `Int128` or a `UInt128` as a parameter, or returns one, so `AddValueObjectHandlers`
registers no handler for a value object over either: the column it lands in, and the conversion to it, are yours to
choose, with a handler of your own, before or after the call:

```csharp skip
SqlMapper.AddTypeHandler(new LedgerBalanceHandler());

internal sealed class LedgerBalanceHandler : SqlMapper.TypeHandler<LedgerBalance>
{
    public override void SetValue(IDbDataParameter parameter, LedgerBalance value)
        => parameter.Value = (decimal)value.Value;

    public override LedgerBalance Parse(object value) => LedgerBalance.Create((Int128)(decimal)value);
}
```

The choice is the one [Entity Framework Core](ef-core.md#128-bit-value-objects) leaves you: a numeric column
compares as numbers do but carries no more than `System.Decimal` holds, and a text column holds the whole range but
compares as text does.

## NULL

A `NULL` column reads as `null` into an optional value object, `Iban?`, whether it is the result of a single-column
query or a member of a mapped type:

```csharp skip
var iban = await connection.QuerySingleAsync<Iban?>(
    "SELECT a.iban FROM customers c LEFT JOIN accounts a ON a.customer_id = c.id WHERE c.id = @id",
    new { id });   // null when the customer has no account
```

Read into the value object itself, what happens depends on where it lands, as it does for an `int`:

- In a single-column query — `QuerySingleAsync<Iban>` — it throws a `DataException`.
- In a member of a mapped type, or a parameter of the constructor Dapper maps it through, Dapper checks for `NULL`
  before it calls the handler, and never calls it. The member is left as an uninitialized value object —
  `IsDefault` is `true`, and no rule ever ran — and nothing throws.

So a nullable column belongs in a nullable member: declare `Iban?` wherever the query can return `NULL`, an outer
join included.

An optional value object holding nothing goes out as a `NULL` parameter.

## Dapper.AOT

Dapper.AOT 1.1.0 intercepts the calls it can, at build time, and ignores the handlers in `SqlMapper`'s table, so
`AddValueObjectHandlers` does not reach them: a parameter fails with
`No mapping exists from object type Probe.Domain.OrderId`, and a read goes through `GetFieldValue<Iban>`. Declare each
handler at module level, and keep `AddValueObjectHandlers` for the calls Dapper.AOT does not intercept:

```csharp skip
[module: DapperAot]
[module: TypeHandler(typeof(Iban), typeof(ValueObjectTypeHandler<Iban, string>))]
[module: TypeHandler(typeof(Quantity), typeof(ValueObjectTypeHandler<Quantity, int>))]
```

`QuerySingle<Iban>` stays a build error, `DAP037`, since Dapper.AOT takes a value object for a row type: query the
underlying type, and `Create` the value object from it.
