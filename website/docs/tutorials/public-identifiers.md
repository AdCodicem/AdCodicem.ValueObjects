---
title: Public Identifiers
sidebar_label: Public identifiers
slug: /tutorials/public-identifiers
description: Declare Stripe-style public identifiers such as acc_1kcv3ahrz6dmv29gqy5cv, mint them, and store them in a narrow EF Core column.
---

# Public identifiers

An identifier that clients see — in a URL, a webhook, a support ticket — has needs of its own. It should say
what it identifies, survive being read over the phone, refuse to be mistaken for another kind of identifier,
and still make a good primary key. `AdCodicem.ValueObjects.Identifiers` generates identifiers in the shape made
familiar by Stripe: `acc_1kcv3ahrz6dmv29gqy5cv`.

```bash
dotnet add package AdCodicem.ValueObjects.Identifiers
dotnet add package AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore
```

## Declare one

```csharp
[EntityId("acc")]
public readonly partial struct AccountId;
```

That is the whole declaration. `AccountId` is a value object like any other — the same parsing, JSON, model
binding, OpenAPI schema and contract kit — with a few members of its own:

```csharp skip
var id = AccountId.New();       // acc_1kcv3ahrz6dmv29gqy5cv, from the ambient clock and a CSPRNG
AccountId.Prefix                // "acc"
AccountId.Length                // the total width, prefix and separator included

AccountId.TryParse("cus_1kcv3ahrz6dmv29gqy5cv", out _)   // false: the prefix belongs to another type
```

## What is in the value

After the prefix and the separator comes a body in Crockford Base32:

- **a time bucket**, so that new identifiers sort after old ones and inserts land at the end of the index rather
  than all over it. It reveals the creation time at the granularity you choose — `Hour` by default — and
  nothing finer;
- **80 random bits**, which is what makes the identifier impossible to guess, whatever the granularity;
- **a check character**, which catches any single mistyped character before a query is sent, and covers the
  prefix too.

Upper case and the look-alikes `i`, `l` and `o` are folded on the way in, so the stored value is canonical and
compares ordinally.

Choose the granularity from the insert rate of the table, not from taste:

```csharp
[EntityId("cus", Granularity = IdGranularity.Minute, Example = "cus_ke1kcv3ahrz6dmv29gqy5c8")]
public readonly partial struct CustomerId;
```

## Store it

Map identifiers with their own convention, alongside the value object one:

```csharp skip
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
{
    builder.ConfigureValueObjects(typeof(AccountId).Assembly);
    builder.ConfigureEntityIds(typeof(AccountId).Assembly);
}
```

Because the width is fixed and the alphabet is ASCII, the column is `char(n)` rather than `varchar(n)`, and
never `nchar`. The prefix is stored with the body on purpose: a raw SQL join between two tables of bare bodies
would succeed silently, and one between prefixed values cannot.

A binary collation makes the database compare the way the application does. It is a performance choice, since
normalization already made the values canonical:

```csharp skip
builder.ConfigureEntityIds(IdCollations.PostgreSql, typeof(AccountId).Assembly);   // or IdCollations.SqlServer
```

The identifier convention also sets the converter of the identifiers, after the value object one. A context that
[validates what it reads](../how-to/ef-core.md#validation-on-read) says so to both calls, or its identifiers are read
without validation:

```csharp skip
builder.ConfigureValueObjects(strict: true, typeof(AccountId).Assembly);
builder.ConfigureEntityIds(IdCollations.PostgreSql, strict: true, typeof(AccountId).Assembly);
```

## Test with them

`New()` reads an ambient clock and entropy source, so a test can pin both without injecting a factory into
every aggregate:

```csharp skip
using (ValueObjectIds.Use(fakeClock, deterministicBytes))
{
    var id = AccountId.New();   // the same value on every run
}
```

The scope follows the execution flow, so tests running in parallel do not see each other's settings.

## Accepting any identifier

A webhook or an audit trail may receive an identifier of any registered kind. `AnyEntityId` parses whichever
prefix arrives, and converts to the concrete type once you know which one it is:

```csharp skip
if (AnyEntityId.TryParse(text, provider: null, out var any) && any.TryConvertTo<AccountId>(out var account))
{
}
```

[Entity identifiers](../entity-identifiers.md) explains the format in depth: the widths, the check character,
and the alternatives that were turned down.
