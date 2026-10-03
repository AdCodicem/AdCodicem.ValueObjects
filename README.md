# AdCodicem.ValueObjects

[![ci](https://github.com/AdCodicem/AdCodicem.ValueObjects/actions/workflows/ci.yml/badge.svg)](https://github.com/AdCodicem/AdCodicem.ValueObjects/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AdCodicem.ValueObjects.svg?logo=nuget)](https://www.nuget.org/packages/AdCodicem.ValueObjects)
[![codecov](https://codecov.io/gh/AdCodicem/AdCodicem.ValueObjects/branch/main/graph/badge.svg)](https://codecov.io/gh/AdCodicem/AdCodicem.ValueObjects)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/AdCodicem/AdCodicem.ValueObjects/badge)](https://scorecard.dev/viewer/?uri=github.com/AdCodicem/AdCodicem.ValueObjects)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/LICENSE)

An answer to primitive obsession for .NET 10 and later: single-value DDD value objects, generated at compile time,
with no reflection and no allocation on the paths that matter.

**[Documentation](https://adcodicem.github.io/AdCodicem.ValueObjects/)**

## Primitive obsession

```csharp skip
Task PayAsync(string customerId, string iban, decimal amount);
```

A call to it compiles with the two strings swapped, `"hello"` passes for a bank account, and the signature says
nothing about what an IBAN is. So every layer says it again: the controller checks the format, a migration
guesses the column width, the OpenAPI document settles for `string`, and nothing keeps the three in agreement.
That is primitive obsession — domain concepts carried as bare `string`, `int` and `Guid`.

The remedy is well known: give each concept a type that cannot hold an invalid value. It stays rare because the
type is only the start. It also needs equality, parsing, formatting, a JSON converter, an EF Core value
converter, a model binder and a schema — a few hundred lines per concept, which is why codebases drift back to
`string`.

Here the type costs one declaration. Its rules are written once and carried into JSON, the database, model
binding and the OpenAPI document, so they cannot drift apart. This compiles as it stands:

```csharp
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects;
using AdCodicem.ValueObjects.Annotations;

namespace Banking;

[ValueObject<string>(
    MinLength = 15,
    MaxLength = 34,
    SchemaFormat = "iban")]
public readonly partial struct Iban
    : IValueObjectNormalizer<string>, IValueObjectPatternValidator, IValueObjectValidator<string>
{
    // Runs first, on every way in: "fr76 3000 6000 …" and "FR7630006000…" are the same account.
    public static string NormalizeValue(string value)
        => value.Replace(" ", "").Replace("-", "").ToUpperInvariant();

    // Runs once the declared length holds. Compiled at build time, and published as the OpenAPI pattern.
    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    // Runs once the declared length and pattern hold: the ISO 7064 MOD-97-10 check digits.
    public static ValidationResult ValidateValue(in string value)
    {
        var remainder = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[(i + 4) % value.Length];
            remainder = char.IsAsciiDigit(c)
                ? ((remainder * 10) + (c - '0')) % 97
                : ((remainder * 100) + (c - 'A' + 10)) % 97;
        }

        return remainder == 1
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");
    }
}
```

That declaration generates the constructor, `Create` / `TryCreate` / `CreateUnchecked`, `Parse` / `TryParse`
(string and span), `ToString` / `TryFormat`, equality, ordering, the `System.Text.Json` converter, the
`TypeConverter`, and the runtime registration — around 400 lines you no longer maintain.

```csharp skip
var iban = Iban.Create("fr76 3000 6000 0112 3456 7890 189");
iban.Value                                  // "FR7630006000011234567890189"
Iban.TryCreate("FR00 0000", out _)          // false: rejection is not an exception
JsonSerializer.Serialize(new { iban })      // {"iban":"FR7630006000011234567890189"}

Task PayAsync(CustomerId customer, Iban iban, decimal amount);   // swapping the two no longer compiles
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

## Supported frameworks

Every package targets `net10.0`, so it installs into a project on .NET 10 or any later version. The twelve are
released together under one version number: reference the same version of each. Their dependencies are minimums
with no upper bound, and the exact minimum of each is in the package's dependency list on nuget.org. A framework's
next major is supported by these same packages, never by a package per framework version
([ADR-0010](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/docs/adr/0010-version-every-package-in-lockstep-independently-of-dotnet.md)).

| Package | Target | Built and tested against | On the next .NET¹ |
| --- | --- | --- | --- |
| `AdCodicem.ValueObjects` | `net10.0` | the .NET 10 SDK | the .NET 11 SDK, whose compiler runs the generator |
| `AdCodicem.ValueObjects.Abstractions` | `net10.0` | .NET 10 | .NET 11 |
| `AdCodicem.ValueObjects.Json` | `net10.0` | .NET 10, source generation included | .NET 11, source generation included |
| `AdCodicem.ValueObjects.EntityFrameworkCore` | `net10.0` | EF Core 10, on PostgreSQL and SQL Server | EF Core 11, on SQLite, PostgreSQL and SQL Server |
| `AdCodicem.ValueObjects.AspNetCore` | `net10.0` | ASP.NET Core 10 | ASP.NET Core 11 |
| `AdCodicem.ValueObjects.OpenApi` | `net10.0` | ASP.NET Core 10, with `Microsoft.OpenApi` 2 | ASP.NET Core 11, with `Microsoft.OpenApi` 3 |
| `AdCodicem.ValueObjects.FluentValidation` | `net10.0` | FluentValidation 12 | FluentValidation 12 on .NET 11 |
| `AdCodicem.ValueObjects.Dapper` | `net10.0` | Dapper 2.1, on PostgreSQL and SQL Server | Dapper 2.1, on SQLite, PostgreSQL and SQL Server |
| `AdCodicem.ValueObjects.NewtonsoftJson` | `net10.0` | Newtonsoft.Json 13 | Newtonsoft.Json 13 on .NET 11 |
| `AdCodicem.ValueObjects.Identifiers` | `net10.0` | .NET 10 | .NET 11 |
| `AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore` | `net10.0` | EF Core 10, on PostgreSQL and SQL Server | EF Core 11, on SQLite, PostgreSQL and SQL Server |
| `AdCodicem.ValueObjects.Testing` | `net10.0` | xUnit v3 4 | xUnit v3 4 on .NET 11 |

¹ On the .NET 11 release candidate, by a CI job that installs the packages each commit builds into a `net11.0`
application. It informs and blocks nothing until .NET 11 ships.

## Trying a preview

Between stable releases, a preview of every package is published to nuget.org when something a package ships has
changed, checked every week. It carries the number of the release it leads to, `0.3.0-preview.172` for instance,
and every package is published at that version:

```
dotnet add package AdCodicem.ValueObjects --prerelease
```

Previews receive no fixes of their own: a fix reaches the next preview and the next release. Every package, and every
assembly inside it, carries a signed build provenance attestation; [`SECURITY.md`](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/SECURITY.md#verifying-a-package) says how to check one.

## Design decisions worth knowing

**A `readonly partial struct`, not a `record struct`.** A record's `with` expression and field-wise equality
would both bypass validation and the configured comparison. The generator owns equality, ordering and hashing so
that `Comparison = StringComparison.OrdinalIgnoreCase` actually means something.

**A struct, even when the underlying type is a `string`.** Holding 100 000 struct wrappers allocates exactly
what holding 100 000 bare strings allocates, to the byte; the class equivalent costs four times the memory and
2.3x the time, because a reference type adds 24 bytes of header, method table pointer and field per instance.
The struct gives that back only when it crosses a non-generic boundary and boxes, so the generated equality,
hashing and comparison exist to keep the hot paths generic — dictionary lookups and sorts on value objects
allocate nothing. See [benchmarks/](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/benchmarks/README.md) for the numbers and for where the struct loses.

**`default(Iban)` is a build error.** A struct can always be brought into existence uninitialized, and that is
the one hole a struct value object cannot close by itself. The `VO0010` analyzer closes it at compile time,
which is what makes the struct representation — zero allocation, no null — safe to choose. Opt out per type with
`AllowDefault = true`. What reaches a boundary the analyzer cannot see — an entity property never set, a default
array element — is not written as it stands: the JSON converters, the Dapper handler and the EF Core converters
refuse an uninitialized instance whose value its type rejects, and an optional EF Core column stores a `NULL`
instead.

**Rejection is not an exception.** `Validate` returns a `readonly struct` that allocates nothing when the value
is valid. The integrations that take outside input go through `TryCreate` or `TryParse` and report a refusal in
their own terms: a JSON exception, a model state error, a FluentValidation failure, a Dapper `DataException`.
`Create` throws `ValueObjectException`, and is for the call sites that want it; a strict EF Core read goes through
it, and fails the query. Validation is fail-fast: the first violated rule wins.

**Normalize, then validate, then assign.** So a non-default instance is by construction both normalized and
valid. It happens on construction, on parsing, on deserialization and on model binding — but *not* when
materializing a row from the database, which is the hottest path in most applications and reads values this
same application wrote. `ConfigureValueObjects(strict: true)` turns that back on for a table another system
also writes to.

**Rules are declared once.** `MaxLength = 34` validates the value, sizes the EF Core column, and becomes the
`maxLength` keyword of the OpenAPI schema. The `[GeneratedRegex]` behind `IValueObjectPatternValidator`
validates the value, and its text becomes the `pattern` keyword. `[KnownValue]` entries become named constants,
a frozen membership lookup, and the `enum` keyword of the schema.

## Compared with other libraries

Vogen, StronglyTypedId and Thinktecture.Runtime.Extensions generate value objects too, and each is the better
choice for some projects: an older target framework, a class or an arbitrary underlying type, smart enums and
unions. What sets this one apart is that a rule declared on the type also reaches the EF Core column and the
OpenAPI schema, and that a rejection carries a stable error code all the way to the API response.
[The comparison](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/preview/explanation/comparison) has the
full table, including where the others are stronger, and
[the migration guide](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/preview/how-to/migrating) maps each
library's surface onto this one.

## Getting started

```
dotnet add package AdCodicem.ValueObjects
```

Then wire up whichever boundaries you have:

```csharp skip
builder.Services.AddControllers().AddValueObjects();
builder.Services.Configure<ApiBehaviorOptions>(o => o.AddValueObjectProblemDetails());
builder.Services.AddOpenApi(o => o.AddValueObjects());

protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    => builder.ConfigureValueObjects(typeof(Iban).Assembly);
```

Minimal APIs need nothing: a generated value object implements `IParsable<T>`, which is exactly what minimal API
parameter binding looks for.

### Testing your own value objects

```csharp skip
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
| `MinLength`, `MaxLength` | Validation, EF column size, OpenAPI schema. |
| `Pattern` | Deprecated (`VO0021`): a regular expression built at run time, which native AOT interprets. Implement `IValueObjectPatternValidator` instead. Removed in the next major version. |
| `Minimum`, `Maximum` | Deprecated (`VO0028`): inclusive bounds written as text in the one form of the underlying type. Implement `IValueObjectMinimum<T>` and `IValueObjectMaximum<T>` instead. Removed in the next major version. |
| `Comparison` | Equality, ordering and hashing for string value objects. Ordinal by default. |
| `ValueSet = Closed` + `[KnownValue]` | Reference-data codes with a frozen lookup and a schema `enum`. Members of a closed set over a reference type are boxed once and shared, so the boxed paths allocate nothing. |
| `Arithmetic` | Operators and generic math for numeric value objects. Every result is re-validated. |
| `ImplicitConversionToValue`, `ExplicitConversionFromValue` | Conversions, opt-in per type. |
| `AllowEmpty`, `AllowDefault` | Loosen the two defaults that exist to catch mistakes. |

The deprecated `Minimum` and `Maximum` options are text because an attribute argument cannot be a `decimal` or a
date, and each underlying type reads them in one form and no other: digits for an integer, with `-` in front when negative (`"-42"`); a
`decimal` with an optional fraction after `.` (`"-19.99"`), and a `double` or a `float` with an optional exponent as
well (`"9.1e-31"`), finite, and zero only when written as zero; one character for a `char`; `yyyy-MM-dd` for a
`DateOnly`; `HH:mm`, `HH:mm:ss` or `HH:mm:ss.fffffff` for a `TimeOnly`; a date, or a date and a time after `T`,
without an offset for a `DateTime` (`"2024-01-31T08:30"`); a date and a time followed by `Z`, `+HH:mm` or `-HH:mm`
for a `DateTimeOffset`; and `[-][d.]hh:mm:ss[.fffffff]` for a `TimeSpan`. No white space, no culture, no time zone:
the same declaration compiles to the same bound on every machine. Any other text, or a value the type cannot hold,
is `VO0004`, and the message names the form. A `string`, a `Guid` and a `bool` take no bound, which is `VO0004`
too: constrain a string with `MinLength`, `MaxLength` or `IValueObjectPatternValidator`. A `[KnownValue]` written
as text is read in the same form, and refused with `VO0013`.

### Hooks

A value object declares a rule by implementing an interface, so the compiler checks the signature: a mis-typed
rule fails the build instead of being silently ignored. All are optional, and `VO0011` reports a rule written
without its interface — the one mistake the compiler cannot catch.

| Interface | Member |
| --- | --- |
| `IValueObjectNormalizer<TValue>` | `static TValue NormalizeValue(TValue value)` |
| `IValueObjectSpanNormalizer` | `static string NormalizeValue(ReadOnlySpan<char> value)` — string value objects only |
| `IValueObjectPatternValidator` | `static Regex Pattern { get; }`, written as a `[GeneratedRegex]` partial property — string value objects only |
| `IValueObjectValidator<TValue>` | `static ValidationResult ValidateValue(in TValue value)` |
| `IValueObjectMinimum<TValue>`, `IValueObjectMaximum<TValue>` | `static TValue Minimum { get; }`, `static TValue Maximum { get; }` — numbers, `char`, dates, times and durations |
| `IValueObjectFormatter<TValue>` | `static bool TryFormatValue(in TValue value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)` |
| `IValueObjectStringFormatter<TValue>` | `static string FormatValue(in TValue value, ReadOnlySpan<char> format, IFormatProvider? provider)` |

`NormalizeValue` must be idempotent and must not reject: an unnormalizable value is rejected by
`ValidateValue`. A formatting hook, when present, takes over formatting entirely, including the default format:
`ToString()`, `ToString(format, provider)`, `TryFormat` and interpolation all write what it writes. When a type
declares both, `FormatValue` answers everywhere and `TryFormatValue` is never called; `TryFormat` then copies the
string `FormatValue` returns. Formatting stops at text for people: JSON, dictionary keys included, and the
database carry the underlying value.

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

`IValueObjectPatternValidator` takes a `[GeneratedRegex]` you write, as in the IBAN above, because the regex
source generator compiles only code a person wrote: one source generator never sees another's output, so this
one cannot write the attribute for you. The pattern runs after `MinLength` and `MaxLength`, before the known
values and `ValidateValue`, and rejects a value as `value_object.invalid_format`. Its text, read off the attribute
when the type compiles, is also the OpenAPI `pattern`. That text carries no `RegexOptions`, so `VO0025` warns on
`IgnoreCase`, `Multiline`, `Singleline` and `IgnorePatternWhitespace`: write such a rule into the pattern itself.
`VO0026` warns on a missing `matchTimeoutMilliseconds`. `Regex` lives in `System.Text.RegularExpressions`, which
is not among the implicit usings.

The hook replaces the `Pattern` option, which builds its regular expression at run time, where native AOT
interprets it. The option is deprecated (`VO0021`) and removed in the next major version. To migrate, move the
expression from `Pattern = "X"` into
`[GeneratedRegex("X", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)] public static partial Regex Pattern { get; }`:
those are the options and the timeout the option used, so behaviour does not change. Declaring both is `VO0022`.

`IValueObjectMinimum<TValue>` and `IValueObjectMaximum<TValue>` declare inclusive bounds as values of the
underlying type, so the compiler checks them and any expression of that type builds them:

```csharp
[ValueObject<DateOnly>]
public readonly partial struct BirthDate : IValueObjectMinimum<DateOnly>, IValueObjectMaximum<DateOnly>
{
    public static DateOnly Minimum => new(1900, 1, 1);

    public static DateOnly Maximum => new(2100, 12, 31);
}
```

They run after the pattern, before the known values and `ValidateValue`, and reject a value as
`value_object.out_of_range`. They are also the OpenAPI `minimum` and `maximum`, or the `x-minimum` and `x-maximum`
extensions for a type JSON writes as a string. A bound is a constant, written as an expression-bodied property: the
check reads it each time and the schema once, as the assembly loads, so a bound relative to the clock is a rule for
`ValidateValue`. Over a `string`, a `Guid`, a `bool`, an
`[EntityId]` or another type than the underlying one, the hooks are `VO0030`. They replace the `Minimum` and
`Maximum` options, deprecated (`VO0028`) and removed in the next major version; declaring an option and its hook is
`VO0029`, and the hook wins.

The rules are public because a static interface member cannot be anything else. `Normalize` remains the member
callers use: it guards against a null underlying value and then defers to `NormalizeValue`.

### Entity identifiers

`AdCodicem.ValueObjects.Identifiers` adds public identifiers in the shape everyone recognizes from Stripe.

```csharp skip
[EntityId("acc")]
public readonly partial struct AccountId;

var id = AccountId.New();   // acc_1kcv3ahrz6dmv29gqy5cv
```

That is a value object like any other — same parsing, same JSON, same column, same contract kit — plus `New()`,
`Prefix`, `Granularity` and `Length`. The prefix is what makes `cus_…` fail to parse as an `AccountId`, so
swapping one identifier for another in a request parameter is refused at the boundary instead of reaching a
repository. It is stored in the database for the same reason: a raw-SQL join between two tables holding bare
bodies would succeed silently.

The body is 80 bits from a CSPRNG, in Crockford Base32, behind a coarse time bucket and followed by a check
character:

- the **time bucket** gives the index a monotonic head, so inserts land at the right edge of the B-tree instead
  of scattering across it. It leaks the creation time at the granularity you choose — `Hour` by default,
  `Minute` or `Day` on request — and nothing finer. It does not make an identifier guessable: the random part
  keeps its full 80 bits regardless;
- the **check character** catches every single mistyped character and almost every adjacent transposition
  offline, before a query is ever sent, and covers the prefix too, so a body copied between two identifier
  types is rejected even by a parser that does not know which prefix to expect;
- the **alphabet** ascends in ASCII, so ordinal comparison — this library's default — sorts identifiers
  chronologically. It is lower case, so an identifier is one unbroken token; upper case and the aliases
  (`i`, `l` → `1`, `o` → `0`) fold on the way in, which makes the stored value canonical and takes a
  case-insensitive column collation out of the correctness path. Crockford's optional hyphen is not accepted:
  one identifier, one spelling.

Length is fixed per type, so the column is `char(n)` and the OpenAPI `pattern`, `minLength` and `maxLength`
follow from the profile without being declared.

`AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore` turns that fixed width into the narrowest column that
holds it — `char(n)` rather than `varchar(n)`, and non-Unicode, so SQL Server does not silently double it to
`nchar` for an alphabet of 32 ASCII symbols:

```csharp skip
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

```csharp skip
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
| `VO0002` | Error | The type is not a `readonly struct`: a class, an interface, a record, a `ref struct`, or a struct without `readonly`. |
| `VO0003` | Error | Unsupported underlying type. |
| `VO0004` | Error | A bound set through the deprecated `Minimum` or `Maximum` option is not written in the one form of its underlying type, names no value of it, or is set on a `string`, a `Guid` or a `bool`, which take none. |
| `VO0005` | Error | A closed value set declares no value. |
| `VO0006` | Error | A known value has an unusable name. |
| `VO0007` | Error | Arithmetic requested on a non-numeric type. |
| `VO0008` | Warning | Length constraints on a non-string type. |
| `VO0009` | Error | A containing type is not `partial`. |
| `VO0010` | Error | An uninitialized value object. |
| `VO0011` | Warning | A rule written without declaring its hook interface, so the generator will never call it. |
| `VO0013` | Error | A known value is not written in the one form of its underlying type, names no value of it, or is no value at all: `null`, an array, a `typeof(...)`, an enum member. |
| `VO0014` | Error | An invalid regular expression in the deprecated `Pattern` option. The regex generator reports one in a `[GeneratedRegex]` itself. |
| `VO0015` | Error | A malformed entity identifier prefix. |
| `VO0016` | Error | Two types claiming the same prefix. |
| `VO0017` | Error | A normalization hook on an entity identifier, which owns its own. |
| `VO0018` | Error | Both `[EntityId]` and `[ValueObject<T>]` on one type. |
| `VO0019` | Error | The generated code cannot reopen, reach or name the type: it, or a type around it, is `file`-local; it is `private` or `protected`, or nested in such a type, inside a generic type; it is a generic `[EntityId]`, or one in a generic type; it has a type parameter it cannot use; or it is named after a member the generator writes on it. |
| `VO0020` | Error | `Comparison`, `ValueSet` or `Granularity` holds a value its enum does not define. |
| `VO0021` | Warning | The deprecated `Pattern` option of `[ValueObject<T>]`, reported by the compiler. Implement `IValueObjectPatternValidator` with `[GeneratedRegex("X", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)] public static partial Regex Pattern { get; }` and remove `Pattern = "X"`. The option builds its regular expression at run time, which native AOT interprets, and is removed in the next major. |
| `VO0022` | Error | Both the `Pattern` option and `IValueObjectPatternValidator` on one type. The hook wins. |
| `VO0023` | Error | `IValueObjectPatternValidator` on a value object whose underlying type is not `string`. |
| `VO0024` | Error | `IValueObjectPatternValidator` on an `[EntityId]`, which validates and publishes its own format. |
| `VO0025` | Warning | The `[GeneratedRegex]` behind `Pattern` sets `IgnoreCase`, `Multiline`, `Singleline` or `IgnorePatternWhitespace`, which the OpenAPI `pattern` cannot carry. |
| `VO0026` | Warning | The `[GeneratedRegex]` behind `Pattern` sets no `matchTimeoutMilliseconds`. |
| `VO0027` | Error | `[KnownValue]` on an `[EntityId]`, which generates no known values. |
| `VO0028` | Warning | The deprecated `Minimum` or `Maximum` option of `[ValueObject<T>]`, reported by the compiler. Implement `IValueObjectMinimum<T>` or `IValueObjectMaximum<T>` with a static property of the underlying type and remove the option. It is removed in the next major. |
| `VO0029` | Error | Both the `Minimum` (or `Maximum`) option and its hook on one type. The hook wins. |
| `VO0030` | Error | `IValueObjectMinimum<T>` or `IValueObjectMaximum<T>` over a type that takes no bound, or over another type than the underlying one. |

## Using it with an AI coding agent

Because the whole implementation is generated, a model that has never seen this library guesses the surface
wrong: a hand-written factory, a `record struct`, a `JsonConverter` nobody needs, a rule that never runs
because its interface was not declared. `skills/value-objects/` states that surface as an agent skill — the
attribute options, the hook interfaces, the wiring of each integration, and every `VO00xx` diagnostic with its
fix. In Claude Code:

```
/plugin marketplace add AdCodicem/AdCodicem.ValueObjects
/plugin install adcodicem-valueobjects@adcodicem
```

Any other agent can read the same files straight from the repository — they are plain Markdown. Every C#
snippet in them is compiled by the generator test suite, so the skill cannot drift away from the generator
without failing the build.

## Repository layout

```
src/          the shipped packages
tests/        unit tests, generator tests, and integration tests on real database engines
  Compat/     the packed packages in a .NET 11 application, outside the solution
samples/      a showcase API exercising the whole chain end to end
benchmarks/   the measurements behind the design decisions above
skills/       the agent skill, and the plugin manifest that distributes it
```

Integration tests start PostgreSQL and SQL Server through Testcontainers, so they need a Docker daemon.

## Building

```
dotnet build
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests        # no Docker needed
dotnet test --project tests/AdCodicem.ValueObjects.GeneratorTests  # no Docker needed
dotnet test                                                    # everything, Docker required
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
