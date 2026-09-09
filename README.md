# AdCodicem.ValueObjects

Single-value DDD value objects for .NET 10, with no reflection and no allocation on the paths that matter.

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
| `AdCodicem.ValueObjects.Testing` | An xUnit contract kit for your own value objects. |

## Design decisions worth knowing

**A `readonly partial struct`, not a `record struct`.** A record's `with` expression and field-wise equality
would both bypass validation and the configured comparison. The generator owns equality, ordering and hashing so
that `Comparison = StringComparison.OrdinalIgnoreCase` actually means something.

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
| `ValueSet = Closed` + `[KnownValue]` | Reference-data codes with a frozen lookup and a schema `enum`. |
| `Arithmetic` | Operators and generic math for numeric value objects. Every result is re-validated. |
| `ImplicitConversionToValue`, `ExplicitConversionFromValue` | Conversions, opt-in per type. |
| `AllowEmpty`, `AllowDefault` | Loosen the two defaults that exist to catch mistakes. |

### Hooks

Declared on the partial struct and detected by name; all optional.

| Hook | Signature |
| --- | --- |
| `NormalizeCore` | `private static TValue NormalizeCore(TValue value)` |
| `ValidateCore` | `private static ValidationResult ValidateCore(in TValue value)` |
| `TryFormatCore` | `private static bool TryFormatCore(in TValue value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)` |
| `FormatCore` | `private static string FormatCore(in TValue value, ReadOnlySpan<char> format, IFormatProvider? provider)` |

`NormalizeCore` must be idempotent and must not reject: an unnormalizable value is rejected by `ValidateCore`.
`TryFormatCore`, when present, takes over formatting entirely, including the default format.

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
| `VO0011` | Warning | A member named like a hook but missing the `Core` suffix. |
| `VO0013` | Error | A known value could not be converted. |
| `VO0014` | Error | An invalid regular expression. |

## Repository layout

```
src/        the shipped packages
tests/      unit tests, and integration tests on real database engines
samples/    a showcase API exercising the whole chain end to end
```

Integration tests start PostgreSQL and SQL Server through Testcontainers, so they need a Docker daemon.

## Building

```
dotnet build
dotnet test tests/AdCodicem.ValueObjects.UnitTests   # no Docker needed
dotnet test                                          # everything, Docker required
dotnet pack -c Release
```

## Licence

MIT.
