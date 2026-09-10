# AdCodicem.ValueObjects

Single-value DDD value objects for .NET 10, with no reflection and no allocation on the paths that matter.

**[Documentation](https://adcodicem.github.io/AdCodicem.ValueObjects/)**

Declare the type and its rules once; the framework carries them into JSON, the database, model binding and the
OpenAPI document, so they cannot drift apart.

```csharp
[ValueObject<string>(
    MinLength = 15,
    MaxLength = 34,
    Pattern = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
    SchemaFormat = "iban")]
public readonly partial struct Iban
{
    private static string NormalizeCore(string value) => /* strip separators, upper-case */;

    private static ValidationResult ValidateCore(in string value)
        => HasValidCheckDigits(value)
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");
}
```

That declaration generates the constructor, `Create` / `TryCreate` / `CreateUnchecked`, `Parse` / `TryParse`
(string and span), `ToString` / `TryFormat`, equality, ordering, the `System.Text.Json` converter, the
`TypeConverter`, and the runtime registration — around 400 lines you no longer maintain.

```csharp
var iban = Iban.Create("fr76 3000 6000 0112 3456 7890 189");
iban.Value                                  // "FR7630006000011234567890189"
iban.ToString(Iban.Formats.Print, null)     // "FR76 3000 6000 0112 3456 7890 189"

JsonSerializer.Serialize(new { iban })      // {"iban":"FR7630006000011234567890189"}
```

## Packages

| Package | What it gives you |
| --- | --- |
| **`AdCodicem.ValueObjects`** | The one to install: contracts, source generator and analyzers. |
| `AdCodicem.ValueObjects.Abstractions` | The contracts alone, with no dependency at all. |
| `AdCodicem.ValueObjects.Json` | Covers source-generated serializer contexts and hand-written value objects. |
| `AdCodicem.ValueObjects.EntityFrameworkCore` | Converters, comparers, and a convention that maps a whole assembly. |
| `AdCodicem.ValueObjects.AspNetCore` | MVC model binding and RFC 9457 problem details carrying the violated rule. |
| `AdCodicem.ValueObjects.OpenApi` | Schema transformer for the built-in .NET OpenAPI stack. |
| `AdCodicem.ValueObjects.FluentValidation` | Rules that reuse what the value object already enforces. |
| `AdCodicem.ValueObjects.Dapper` | Type handlers for raw SQL. |
| `AdCodicem.ValueObjects.NewtonsoftJson` | Interop with code that has not moved to `System.Text.Json`. |
| `AdCodicem.ValueObjects.Identifiers` | Stripe-style public entity identifiers: `acc_2K7X9…`. |
| `AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore` | Fixed-width, non-Unicode columns for those identifiers. |
| `AdCodicem.ValueObjects.Testing` | An xUnit contract kit for your own value objects. |

## Design decisions worth knowing

**A `readonly partial struct`, not a `record struct`.** A record's `with` expression and field-wise equality
would both bypass validation and the configured comparison. The generator owns equality, ordering and hashing so
that `Comparison = StringComparison.OrdinalIgnoreCase` actually means something.

**A struct, even when the underlying type is a `string`.** Holding 100 000 struct wrappers allocates exactly
what holding 100 000 bare strings allocates, to the byte; the class equivalent costs four times the memory and
twice the time, because a reference type adds 24 bytes of header, method table pointer and field per instance.
The struct gives that back only when it crosses a non-generic boundary and boxes, so the generated equality,
hashing and comparison exist to keep the hot paths generic — dictionary lookups and sorts on value objects
allocate nothing. See [benchmarks/](benchmarks/README.md) for the numbers and for where the struct loses.

**`default(Iban)` is a build error.** A struct can always be brought into existence uninitialized, and that is
the one hole a struct value object cannot close by itself. The `VO0010` analyzer closes it at compile time,
which is what makes the struct representation — zero allocation, no null — safe to choose. Opt out per type with
`AllowDefault = true`.

**Rejection is not an exception.** `Validate` returns a `readonly struct` that allocates nothing when the value
is valid, and every integration — JSON, model binding, EF Core, Dapper — goes through `TryCreate`. `Create`
throws, and is for the call sites that want it. Validation is fail-fast: the first violated rule wins.

**Normalize, then validate, then assign.** So a non-default instance is by construction both normalized and
valid. It happens on construction, on parsing, on deserialization and on model binding — but *not* when
materializing a row from the database, which is the hottest path in most applications and reads values this
same application wrote. `ConfigureValueObjects(strict: true)` turns that back on for a table another system
also writes to.

**Rules are declared once.** `MaxLength = 34` validates the value, sizes the EF Core column, and becomes the
`maxLength` keyword of the OpenAPI schema. `[KnownValue]` entries become named constants, a frozen membership
lookup, and the `enum` keyword of the schema.

## Getting started

```
dotnet add package AdCodicem.ValueObjects
```

Then wire up whichever boundaries you have:

```csharp
builder.Services.AddControllers().AddValueObjects();
builder.Services.Configure<ApiBehaviorOptions>(o => o.AddValueObjectProblemDetails());
builder.Services.AddOpenApi(o => o.AddValueObjects());

protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    => builder.ConfigureValueObjects(typeof(Iban).Assembly);
```

Minimal APIs need nothing: a generated value object implements `IParsable<T>`, which is exactly what minimal API
parameter binding looks for.

### Testing your own value objects

```csharp
public sealed class IbanContract : ValueObjectContract<Iban, string>
{
    protected override IEnumerable<string> AcceptedValues => ["FR7630006000011234567890189"];
    protected override IEnumerable<string> RejectedValues => ["", "not-an-iban"];
}
```

That derives a dozen checks: normalization settles, equality and ordering agree, text and JSON round-trip,
rejected values are rejected the same way by every entry point.

## Authoring reference

### Supported underlying types

`string`, `Guid`, `bool`, `char`, every built-in integer (including `Int128` and `UInt128`, which travel as JSON
strings), `decimal`, `double`, `float`, `DateOnly`, `TimeOnly`, `DateTime`, `DateTimeOffset`, `TimeSpan`.

### Declarative options on `[ValueObject<T>]`

| Option | Effect |
| --- | --- |
| `Pattern`, `MinLength`, `MaxLength` | Validation, EF column size, OpenAPI schema. |
| `Minimum`, `Maximum` | Written in invariant culture, parsed at compile time. |
| `Comparison` | Equality, ordering and hashing for string value objects. Ordinal by default. |
| `ValueSet = Closed` + `[KnownValue]` | Reference-data codes with a frozen lookup and a schema `enum`. Members of a closed set over a reference type are boxed once and shared, so the boxed paths allocate nothing. |
| `Arithmetic` | Operators and generic math for numeric value objects. Every result is re-validated. |
| `ImplicitConversionToValue`, `ExplicitConversionFromValue` | Conversions, opt-in per type. |
| `AllowEmpty`, `AllowDefault` | Loosen the two defaults that exist to catch mistakes. |

### Hooks

A value object declares a rule by implementing an interface, so the compiler checks the signature: a mis-typed
rule fails the build instead of being silently ignored. All are optional, and `VO0011` reports a rule written
without its interface — the one mistake the compiler cannot catch.

| Interface | Member |
| --- | --- |
| `IValueObjectNormalizer<TValue>` | `static TValue NormalizeValue(TValue value)` |
| `IValueObjectSpanNormalizer` | `static string NormalizeValue(ReadOnlySpan<char> value)` — string value objects only |
| `IValueObjectValidator<TValue>` | `static ValidationResult ValidateValue(in TValue value)` |
| `IValueObjectFormatter<TValue>` | `static bool TryFormatValue(in TValue value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)` |
| `IValueObjectStringFormatter<TValue>` | `static string FormatValue(in TValue value, ReadOnlySpan<char> format, IFormatProvider? provider)` |

`NormalizeCore` must be idempotent and must not reject: an unnormalizable value is rejected by `ValidateCore`.
`TryFormatCore`, when present, takes over formatting entirely, including the default format.

Adding `IValueObjectSpanNormalizer` alongside `IValueObjectNormalizer<string>` lets parsing and JSON reading
normalize straight from the text, so ingesting a value allocates the normalized string and nothing else. It
halves what `TryParse` allocates, and makes deserializing a payload of value objects allocate exactly what
deserializing the same payload of primitives does. Write the value-typed overload as a one-line delegation:

```csharp
public readonly partial struct Iban : IValueObjectNormalizer<string>, IValueObjectSpanNormalizer
{
    public static string NormalizeValue(string value) => NormalizeValue(value.AsSpan());

    public static string NormalizeValue(ReadOnlySpan<char> value)
    {
        Span<char> buffer = value.Length <= 64 ? stackalloc char[64] : new char[value.Length];
        // ... write the normalized characters into buffer ...
        return new string(buffer[..length]);
    }
}
```

The rules are public because a static interface member cannot be anything else. `Normalize` remains the member
callers use: it guards against a null underlying value and then defers to `NormalizeValue`.

### Entity identifiers

`AdCodicem.ValueObjects.Identifiers` adds public identifiers in the shape everyone recognizes from Stripe.

```csharp
[EntityId("acc")]
public readonly partial struct AccountId;

var id = AccountId.New();   // acc_2K7X9WQMZ4H3N8VYB6TCR0FGJ0
```

That is a value object like any other — same parsing, same JSON, same column, same contract kit — plus `New()`,
`Prefix`, `Granularity` and `Length`. The prefix is what makes `cus_…` fail to parse as an `AccountId`, so
swapping one identifier for another in a request parameter is refused at the boundary instead of reaching a
repository. It is stored in the database for the same reason: a raw-SQL join between two tables holding bare
bodies would succeed silently.

The body is 105 bits from a CSPRNG, in Crockford Base32, behind a coarse time bucket and followed by a check
character:

- the **time bucket** gives the index a monotonic head, so inserts land at the right edge of the B-tree instead
  of scattering across it. It leaks the creation time at the granularity you choose — `Hour` by default,
  `Minute` or `Day` on request — and nothing finer. It does not make an identifier guessable: the random part
  keeps its full 105 bits regardless;
- the **check character** catches every single mistyped character and almost every adjacent transposition
  offline, before a query is ever sent, and covers the prefix too, so a body copied between two identifier
  types is rejected even by a parser that does not know which prefix to expect;
- the **alphabet** ascends in ASCII, so ordinal comparison — this library's default — sorts identifiers
  chronologically, and its aliases (`I`, `L` → `1`, `O` → `0`) fold on the way in, which makes the stored value
  canonical and takes a case-insensitive column collation out of the correctness path.

Length is fixed per type, so the column is `char(n)` and the OpenAPI `pattern`, `minLength` and `maxLength`
follow from the profile without being declared.

`AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore` turns that fixed width into the narrowest column that
holds it — `char(n)` rather than `varchar(n)`, and non-Unicode, so SQL Server does not silently double it to
`nchar` for an alphabet of 32 ASCII symbols:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    => builder.ConfigureEntityIds(typeof(AccountId).Assembly);
```

A binary collation (`IdCollations.SqlServer`, `IdCollations.PostgreSql`) is worth setting and is a performance
choice rather than a correctness one, precisely because normalization already made the stored value canonical.
What the package deliberately leaves to you is the physical layout: on SQL Server a primary key is clustered by
default, and `IsClustered(false)` confines index churn to the 30-byte index instead of the whole row.

`AnyEntityId` parses whichever registered prefix arrives, for webhooks, deep links and audit trails. It
implements neither `IValueObject` nor `IEntityId`, which is what keeps it out of the EF Core convention: a
polymorphic column cannot be mapped by accident.

`New()` reads an ambient `TimeProvider` and `IdEntropySource`. Tests substitute them without an injected
factory reaching every aggregate:

```csharp
using (ValueObjectIds.Use(fakeClock, deterministicBytes))
{
    var id = AccountId.New();
}
```

The scope is bound to the execution flow, so suites running in parallel do not interfere.

[Entity Identifiers](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/entity-identifiers) carries the
format, the arithmetic behind the widths, and the reasoning — including why there is one identity rather than
an internal surrogate key alongside it.

### Diagnostics

| Id | Severity | Meaning |
| --- | --- | --- |
| `VO0001` | Error | The type is not `partial`. |
| `VO0002` | Error | The type is not a `readonly struct`, or is a record. |
| `VO0003` | Error | Unsupported underlying type. |
| `VO0004` | Error | A bound could not be parsed. |
| `VO0005` | Error | A closed value set declares no value. |
| `VO0006` | Error | A known value has an unusable name. |
| `VO0007` | Error | Arithmetic requested on a non-numeric type. |
| `VO0008` | Warning | Length constraints on a non-string type. |
| `VO0009` | Error | A containing type is not `partial`. |
| `VO0010` | Error | An uninitialized value object. |
| `VO0011` | Warning | A rule written without declaring its hook interface, so the generator will never call it. |
| `VO0013` | Error | A known value could not be converted. |
| `VO0014` | Error | An invalid regular expression. |
| `VO0015` | Error | A malformed entity identifier prefix. |
| `VO0016` | Error | Two types claiming the same prefix. |
| `VO0017` | Error | A normalization hook on an entity identifier, which owns its own. |
| `VO0018` | Error | Both `[EntityId]` and `[ValueObject<T>]` on one type. |

## Repository layout

```
src/          the shipped packages
tests/        unit tests, generator tests, and integration tests on real database engines
samples/      a showcase API exercising the whole chain end to end
benchmarks/   the measurements behind the design decisions above
```

Integration tests start PostgreSQL and SQL Server through Testcontainers, so they need a Docker daemon.

## Building

```
dotnet build
dotnet test tests/AdCodicem.ValueObjects.UnitTests        # no Docker needed
dotnet test tests/AdCodicem.ValueObjects.GeneratorTests  # no Docker needed
dotnet test                                          # everything, Docker required
dotnet pack -c Release
```

Benchmarks are a separate run, and want a quiet machine:

```
cd benchmarks/AdCodicem.ValueObjects.Benchmarks
dotnet run -c Release -- --filter *              # everything
dotnet run -c Release -- --filter *WrapperCost*  # just the struct against class comparison
```

## Contributing

```
pip install pre-commit
pre-commit install
```

installs a `pre-commit` and a `commit-msg` hook that also run in CI (`.github/workflows/lint.yml`):
committed files must stay usable on a case-insensitive, no-symlink Windows checkout, and commit
messages must follow [Conventional Commits](https://www.conventionalcommits.org/).

## Licence

MIT.
