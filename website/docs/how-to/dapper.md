---
title: Use with Dapper
sidebar_label: Dapper
slug: /how-to/dapper
description: Register Dapper type handlers for every value object of an assembly, so raw SQL reads and writes the underlying value.
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
Pass several assemblies if the value objects live in more than one. Calling it again changes nothing.

## What a read trusts

A column the provider returns as the underlying type is read with `CreateUnchecked`, on the same reasoning as the
[Entity Framework Core](ef-core.md#validation-on-read) read path: this application validated the value when it wrote
it. Some providers return another type of the date and time family than the underlying one, and those are converted
first: SQL Server returns a `DateTime` for a `date` column and a `TimeSpan` for a `time` column, Npgsql a `DateOnly`
and a `TimeOnly` for them, and a UTC `DateTime` for a `timestamptz`. A `DateTime` that says nothing of its zone
cannot become a `DateTimeOffset`, and is refused.

Text read into a value object whose underlying type is not `string` — a Guid or a number kept in a text column — is
the exception: it is parsed the way the value object parses text, so it is normalized and validated. Text the value
object refuses throws a `DataException` carrying the rule's message.

## NULL

A `NULL` column reads as `null` into an optional value object, `Iban?`. Read into the value object itself, it
throws a `DataException`, as Dapper does for an `int`:

```csharp skip
var iban = await connection.QuerySingleAsync<Iban?>(
    "SELECT a.iban FROM customers c LEFT JOIN accounts a ON a.customer_id = c.id WHERE c.id = @id",
    new { id });   // null when the customer has no account
```

An optional value object holding nothing goes out as a `NULL` parameter.
