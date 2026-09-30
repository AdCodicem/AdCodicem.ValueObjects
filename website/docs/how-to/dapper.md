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
Pass several assemblies if the value objects live in more than one.
