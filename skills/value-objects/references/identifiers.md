# Entity identifiers

`AdCodicem.ValueObjects.Identifiers` adds public identifiers in the shape everyone recognizes from Stripe:
`acc_1kcv3ahrz6dmv29gqy5cv`.

```csharp
[EntityId("acc")]
public readonly partial struct AccountId;
```

That is a value object like any other — same parsing, same JSON, same column, same contract kit — plus `New()`,
`Prefix`, `Granularity` and `Length`. It implements `IEntityId<TSelf>`, so it is also an `IValueObject<TSelf, string>`.

```csharp
[EntityId("cus", Granularity = IdGranularity.Minute, Example = "cus_ke1kcv3ahrz6dmv29gqy5cv")]
public readonly partial struct CustomerId;

public static class Minting
{
    public static string Mint()
    {
        var id = CustomerId.New();            // cus_…, minted from the ambient clock and CSPRNG
        _ = CustomerId.Prefix;                // "cus"
        _ = CustomerId.Length;                // total width, prefix and separator included
        _ = CustomerId.TryParse("acc_1kcv3ahrz6dmv29gqy5cv", out _);  // false: wrong prefix

        return id.Value;
    }
}
```

## What the attribute takes

| Member | Default | Effect |
| --- | --- | --- |
| `Prefix` (constructor argument) | required | One or more lowercase segments separated by `_`, each opening on a letter: `"acc"`, `"sk_live"`. Malformed → `VO0015`; claimed twice → `VO0016`. |
| `Granularity` | `IdGranularity.Hour` | Width of the time bucket: `Minute` (6 chars), `Hour` (4), `Day` (3). A value the enum does not define → `VO0020`. |
| `AllowDefault` | `false` | Silences `VO0010` for `default(AccountId)` and `new AccountId()`, as on `[ValueObject<T>]`. Only for a type whose default state is meaningful to the code holding it; absence is `AccountId?`. |
| `Description`, `Example` | none | OpenAPI documentation. |

Choose `Granularity` from the insert rate of the table, aiming for roughly 10⁴–10⁵ rows per bucket — not from
taste. It leaks the creation time at exactly that granularity and nothing finer; the random part keeps its full
80 bits either way, so the identifier never becomes guessable.

An identifier owns its own normalization: declaring a normalizer hook on one is `VO0017`. A validator or a
formatter is declared through its interface, as on any value object; written without it, it never runs and is
`VO0011`. It also owns its format and publishes its own OpenAPI `pattern`, so `IValueObjectPatternValidator` on
one is `VO0024`. Both `[EntityId]` and `[ValueObject<T>]` on the same type is `VO0018`.

`default(AccountId)` and `new AccountId()` are build error `VO0010`, as for any value object: the instance they
produce has an empty `Value` and never went through validation. Mint with `New()`, read with `Parse` or
`TryParse`, and write absence as `AccountId?`.

## Why the prefix is stored, not stripped

`cus_…` fails to parse as an `AccountId`, so swapping one identifier for another in a request parameter is
refused at the boundary instead of reaching a repository. Keeping the prefix in the database gives the same
protection to raw SQL: a join between two tables holding bare bodies would succeed silently.

The body is 80 bits from a CSPRNG in Crockford Base32, behind the time bucket and followed by a check
character. Upper case and the aliases (`i`, `l` → `1`, `o` → `0`) fold on the way in, so the stored value is
canonical; the hyphen Crockford allows is not accepted — one identifier, one spelling. The alphabet ascends in
ASCII, so ordinal comparison sorts identifiers chronologically.

## Entity Framework Core

```csharp skip
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    => builder.ConfigureEntityIds(typeof(AccountId).Assembly);
```

Fixed width earns the narrowest column that holds it: `char(n)` rather than `varchar(n)`, and non-Unicode, so
SQL Server does not silently double it to `nchar` for an alphabet of 32 ASCII symbols. Pass a collation to make
the database compare the way the application does:

```csharp skip
builder.ConfigureEntityIds(IdCollations.SqlServer, typeof(AccountId).Assembly);   // or IdCollations.PostgreSql
builder.Property(e => e.Id).HasEntityIdConversion(IdCollations.PostgreSql);       // one property only
```

The collation is a performance choice rather than a correctness one — precisely because normalization already
made the stored value canonical. What the package leaves to you is the physical layout: on SQL Server a primary
key is clustered by default, and `IsClustered(false)` confines index churn to the 30-byte index instead of the
whole row.

Use `ConfigureEntityIds` **in addition to** `ConfigureValueObjects`: the identifier convention is the one that
produces the narrow fixed-width column.

## `AnyEntityId`, for webhooks, deep links and audit trails

```csharp skip
if (AnyEntityId.TryParse(text, provider: null, out var any) && any.TryConvertTo<AccountId>(out var account))
{
    // any.Prefix, any.ValueObjectType, any.Is<AccountId>(), any.ToValueObject()
}
```

It parses whichever registered prefix arrives. It implements neither `IValueObject` nor `IEntityId`, which is
what keeps it out of the EF Core convention: a polymorphic column cannot be mapped by accident.

Its `Value` is the identifier's own value, never what a formatting hook writes. In JSON it is that bare text. A
JSON `null` throws a `JsonException`: declare `AnyEntityId?` for a reference that may be absent.

## Deterministic tests

`New()` reads an ambient `TimeProvider` and `IdEntropySource`, so nothing has to inject a factory into every
aggregate:

```csharp skip
using (ValueObjectIds.Use(fakeClock, deterministicBytes))
{
    var id = AccountId.New();
}
```

The scope is bound to the execution flow, so suites running in parallel do not interfere.
`ValueObjectIds.Configure(...)` sets the process-wide default instead, for a host that wants one.
