![AdCodicem.ValueObjects — lege artis](https://raw.githubusercontent.com/AdCodicem/AdCodicem.ValueObjects/main/docs/assets/readme-banner.png)

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
| `AdCodicem.ValueObjects.Json` | Covers source-generated serializer contexts and hand-written value objects, and fills in the JSON Schema System.Text.Json exports. |
| `AdCodicem.ValueObjects.EntityFrameworkCore` | Converters, comparers, and a convention that maps a whole assembly. |
| `AdCodicem.ValueObjects.AspNetCore` | MVC model binding and RFC 9457 problem details carrying the violated rule. |
| `AdCodicem.ValueObjects.AspNetCore.Http` | The same problem details for minimal APIs, native AOT included. |
| `AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson` | The same problem details for a body MVC reads with Newtonsoft.Json. |
| `AdCodicem.ValueObjects.OpenApi` | Schema transformer for the built-in .NET OpenAPI stack. |
| `AdCodicem.ValueObjects.Swashbuckle` | Schema and parameter filters for Swashbuckle 10 and later. |
| `AdCodicem.ValueObjects.FluentValidation` | Rules that reuse what the value object already enforces. |
| `AdCodicem.ValueObjects.Dapper` | Type handlers for raw SQL. |
| `AdCodicem.ValueObjects.MongoDB` | MongoDB.Driver serializers: the bare value in BSON, `.Value` in LINQ, strict reads. |
| `AdCodicem.ValueObjects.NewtonsoftJson` | Interop with code that has not moved to `System.Text.Json`. |
| `AdCodicem.ValueObjects.Serilog` | Logs a value object as its underlying value, a number as a number. |
| `AdCodicem.ValueObjects.Identifiers` | Stripe-style public entity identifiers: `acc_2K7X9…`. |
| `AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore` | Fixed-width, non-Unicode columns for those identifiers. |
| `AdCodicem.ValueObjects.Testing` | An xUnit contract kit for your own value objects. |

## Supported frameworks

Every package targets `net10.0`, so it installs into a project on .NET 10 or any later version. The seventeen are
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
| `AdCodicem.ValueObjects.AspNetCore.Http` | `net10.0` | ASP.NET Core 10, reflection-based binding, the Request Delegate Generator and native AOT | ASP.NET Core 11 |
| `AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson` | `net10.0` | ASP.NET Core 10, with `Microsoft.AspNetCore.Mvc.NewtonsoftJson` 10 | ASP.NET Core 11, with `Microsoft.AspNetCore.Mvc.NewtonsoftJson` 11 |
| `AdCodicem.ValueObjects.OpenApi` | `net10.0` | ASP.NET Core 10, with `Microsoft.OpenApi` 2 | ASP.NET Core 11, with `Microsoft.OpenApi` 3 |
| `AdCodicem.ValueObjects.Swashbuckle` | `net10.0` | Swashbuckle 10 on ASP.NET Core 10, with `Microsoft.OpenApi` 2 | Swashbuckle 10 on ASP.NET Core 11, with `Microsoft.OpenApi` 2 |
| `AdCodicem.ValueObjects.FluentValidation` | `net10.0` | FluentValidation 12 | FluentValidation 12 on .NET 11 |
| `AdCodicem.ValueObjects.Dapper` | `net10.0` | Dapper 2.1, on PostgreSQL and SQL Server | Dapper 2.1, on SQLite, PostgreSQL and SQL Server |
| `AdCodicem.ValueObjects.MongoDB` | `net10.0` | MongoDB.Driver 3.12, on MongoDB 8 | MongoDB.Driver 3.12 on .NET 11, on MongoDB 8 |
| `AdCodicem.ValueObjects.NewtonsoftJson` | `net10.0` | Newtonsoft.Json 13 | Newtonsoft.Json 13 on .NET 11 |
| `AdCodicem.ValueObjects.Serilog` | `net10.0` | Serilog 4, through Microsoft.Extensions.Logging too, and native AOT | Serilog 4 on .NET 11 |
| `AdCodicem.ValueObjects.Identifiers` | `net10.0` | .NET 10 | .NET 11 |
| `AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore` | `net10.0` | EF Core 10, on PostgreSQL and SQL Server | EF Core 11, on SQLite, PostgreSQL and SQL Server |
| `AdCodicem.ValueObjects.Testing` | `net10.0` | xUnit v3 4 | xUnit v3 4 on .NET 11 |

¹ On the .NET 11 release candidate, by a CI job that installs the packages each commit builds into `net11.0`
applications, the Swashbuckle package into one of its own. It informs and blocks nothing until .NET 11 ships.

## Versioning

The packages follow semantic versioning from 1.0.0 on: from then, only a major version breaks the public API or
removes a member. Before 1.0.0, the version is `0.<minor>.<patch>`, and a minor version may break part of the public
API, or deprecate or remove part of it, without waiting for a major: read the
[changelog](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/CHANGELOG.md) before taking a new minor. A
patch never breaks anything, before 1.0.0 or after. A member deprecated rather than removed outright is reported by
the compiler wherever it is used, with a diagnostic naming its replacement. A member that is read by nothing any more
stays a while as a compile error that says what replaces it (`VO0021`, `VO0028`, `VO0034`, `VO0035`), and any minor
version may then remove it.

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
`AllowDefault = true`. Another source generator cannot see the generated members, and may write `new Iban()` in
its own output: `VO0032` reports it in the code of Riok.Mapperly and of the configuration binding generator, and
of any generator a `.globalconfig` adds. What reaches a boundary the analyzer cannot see — an entity property never
set, a default array element — is not written as it stands: the JSON converters, the Dapper handler, the MongoDB
serializers and the EF Core converters refuse an uninitialized instance whose value its type rejects, and an optional
EF Core column stores a `NULL` instead.

**Rejection is not an exception.** `Validate` returns a `readonly struct` that allocates nothing when the value
is valid. The integrations that take outside input go through `TryCreate` or `TryParse` and report a refusal in
their own terms: a JSON exception, a model state error, a FluentValidation failure, a Dapper `DataException`, a
MongoDB.Driver `FormatException`. Each
carries the code of the rule, which `ValueObjectErrors.TryGetCode` reads from any of those exceptions, and which
the problem details of an MVC controller carry for a JSON body, read by System.Text.Json or Newtonsoft.Json, as for a
query value, and those of a minimal API for a route, query or header value.
`Create` throws `ValueObjectException`, and is for the call sites that want it; a strict EF Core read goes through
it, and fails the query. Validation is fail-fast: the first violated rule wins.

**Normalize, then validate, then assign.** So a non-default instance is by construction both normalized and
valid. It happens on construction, on parsing, on deserialization and on model binding — but *not* when
materializing a row from the database, which is the hottest path in most applications and reads values this
same application wrote. `ConfigureValueObjects(strict: true)` turns that back on for a table another system
also writes to.

**Rules are declared once.** `MaxLength = 34` validates the value, sizes the EF Core column, and becomes the
`maxLength` keyword of the OpenAPI schema. The `[GeneratedRegex]` behind `IValueObjectPatternValidator`
validates the value, and its text becomes the `pattern` keyword. The members marked `[KnownValue]` become a frozen
membership lookup and the `enum` keyword of the schema, with their names beside it for generated clients.
The same rules fill in the JSON Schema System.Text.Json exports, which AI tools, structured output and MCP servers
describe their parameters with, through `ValueObjectJsonSchema`.

## Compared with other libraries

Vogen, StronglyTypedId and Thinktecture.Runtime.Extensions generate value objects too, and each is the better
choice for some projects: an older target framework, a class or an arbitrary underlying type, smart enums and
unions. What sets this one apart is that a rule declared on the type also reaches the EF Core column and the
OpenAPI schema, and that a rejection carries a stable error code all the way to the response of an MVC controller or
a minimal API.
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
builder.Services.AddOpenApi(o => o.AddValueObjects());       // or, with Swashbuckle 10:
builder.Services.AddSwaggerGen(o => o.AddValueObjects());

protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    => builder.ConfigureValueObjects(typeof(Iban).Assembly);
```

An MVC application whose bodies Newtonsoft.Json reads calls `AddNewtonsoftJson().AddValueObjectsNewtonsoftJson()`
instead of `AddValueObjects()`, from `AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson`: the converter goes into MVC's
settings, which its responses are written with too, and a refused body carries its rule's code as with System.Text.Json
([the ASP.NET Core guide](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/preview/how-to/aspnet-core#a-body-read-by-newtonsoftjson)).

Minimal APIs need no package to bind: a generated value object implements `IParsable<T>`, which is exactly what
minimal API parameter binding looks for. A value it rejects is answered there with a bare 400, naming neither the
parameter nor the rule, unless `AdCodicem.ValueObjects.AspNetCore.Http` covers the endpoints, which then answer with
the problem details MVC writes, carrying the rule's code:

```csharp skip
builder.Services.AddProblemDetails();
builder.Services.AddValueObjectHttpProblemDetails(); // with ThrowOnBadRequest on, as in Development

var app = builder.Build();
app.UseExceptionHandler();

var api = app.MapGroup("/api").WithValueObjectProblemDetails();
```

A route, query or header value is answered so in every environment; a JSON body only where
`RouteHandlerOptions.ThrowOnBadRequest` is on, since the framework otherwise answers a refused body before anything
can learn why
([the ASP.NET Core guide](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/preview/how-to/aspnet-core#problem-details-for-minimal-apis)).
A native binary needs `AddProblemDetails()`, whose serializer context writes the problem details without reflection:
without it, the covered endpoints fail to build, naming the call. Where the Request Delegate Generator
writes that binding, in a project that sets `PublishAot` or `PublishTrimmed`, a value object declared in the project
that maps the endpoints also lists its contract on its declaration,
`public readonly partial struct Sku : IValueObject<Sku, string>;`, because that generator does not see what this one
adds; `VO0033` reports one that does not, and
[the ASP.NET Core guide](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/preview/how-to/aspnet-core#the-request-delegate-generator)
explains it. A value object from another project needs nothing.

Serilog logs a value object with `@` as a structure of its public properties, and without `@` as its text, a number in
quotes that a filter or a query no longer compares as one. `AdCodicem.ValueObjects.Serilog` logs it as the value it
carries, `"Qty":42`, written as Serilog writes that type:

```csharp skip
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Destructure.ValueObjects(o =>
    {
        o.CaptureAsUnderlyingValue = true;          // {Qty} too, not only {@Qty}
        o.Assemblies.Add(typeof(Iban).Assembly);    // the assemblies declaring value objects
    })
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();
```

The option costs a pass over the properties of each event, and sees what the enrichers added before it
([the logging guide](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/preview/how-to/logging#serilog)).

`XmlSerializer` and `DataContractSerializer`, and the MVC XML formatters, CoreWCF and Dapr actor remoting built on them,
write a value object as an empty element and read back a default instance. An assembly that crosses an XML boundary
opts in, and every value object it declares then implements `IXmlSerializable`: it is written as its underlying value,
read back through its rules, a refusal carrying its code, and described in the exported schema with its rules as XSD
facets ([the XML guide](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/preview/how-to/xml)):

```csharp skip
[assembly: ValueObjectXmlSerialization]
```

MongoDB.Driver maps a value object through a class map with no member to set: it writes `{}`, reads back a default
instance, and a filter over one matches every document or none. `AdCodicem.ValueObjects.MongoDB` stores each as the
bare value the serializer of its underlying type writes, so a document reads as it did when the property was a
`string` or a `Guid`; LINQ translates `x.Iban.Value.StartsWith("FR")` on the field itself; and a read goes through the
value object's rules, since a collection is often written by more than one program
([the MongoDB guide](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/preview/how-to/mongodb)):

```csharp skip
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)); // first
ValueObjectBson.Register(typeof(Iban).Assembly);           // or Register(trusted: true, …) for a store of your own
```

### Testing your own value objects

```csharp skip
public sealed class IbanContract : ValueObjectContract<Iban, string>
{
    protected override IEnumerable<string> AcceptedValues => ["FR7630006000011234567890189"];
    protected override IEnumerable<string> RejectedValues => ["", "not-an-iban"];
}
```

That derives over a dozen checks: normalization settles, equality and ordering agree, text and JSON round-trip,
rejected values are rejected the same way by every entry point, the declared example and known values are
values the type accepts, and the schema names each known value in its place.

## Authoring reference

### Supported underlying types

`string`, `Guid`, `bool`, `char`, every built-in integer (including `Int128` and `UInt128`, which travel as JSON
strings), `decimal`, `double`, `float`, `DateOnly`, `TimeOnly`, `DateTime`, `DateTimeOffset`, `TimeSpan`.

### Declarative options on `[ValueObject<T>]`

| Option | Effect |
| --- | --- |
| `MinLength`, `MaxLength` | Validation, EF column size, OpenAPI schema. |
| `Pattern` | Removed: a compile error (`VO0021`), read by nothing. Implement `IValueObjectPatternValidator` instead. Any minor version may remove the property before 1.0.0. |
| `Minimum`, `Maximum` | Removed: a compile error (`VO0028`), read by nothing. Implement `IValueObjectMinimum<T>` and `IValueObjectMaximum<T>` instead. Any minor version may remove the properties before 1.0.0. |
| `Comparison` | Equality, ordering and hashing for string value objects. Ordinal by default. |
| `ValueSet = Closed` + `[KnownValue]` members | Reference-data codes with a frozen lookup and a schema `enum`, whose values a generated client names after the known values (`x-enum-varnames`, `x-enumNames`, `x-ms-enum`), so renaming one renames its member there. Members of a closed set over a reference type are boxed once and shared, so the boxed paths allocate nothing. |
| `Arithmetic` | Operators and generic math for numeric value objects. Every result is re-validated. |
| `ImplicitConversionToValue`, `ExplicitConversionFromValue` | Conversions, opt-in per type. |
| `AllowEmpty`, `AllowDefault` | Loosen the two defaults that exist to catch mistakes. |
| `SchemaFormat`, `Description` | OpenAPI documentation. |
| `Example` | Removed: a compile error (`VO0035`), read by nothing. Implement `IValueObjectExample<TSelf>` instead. Any minor version may remove the property before 1.0.0. |

### Known values

A known value is a member of the type, marked `[KnownValue]` and initialized through the generated `Known`, so the
compiler checks its name and the type of its value:

```csharp
[ValueObject<string>(ValueSet = ValueSetKind.Closed, MinLength = 2, MaxLength = 2)]
public readonly partial struct CountryCode
{
    /// <summary>France.</summary>
    [KnownValue]
    public static readonly CountryCode France = Known("FR");

    [KnownValue(Description = "Belgium")]
    public static readonly CountryCode Belgium = Known("BE");
}
```

It is a `static readonly` field or a static get-only auto-property of the type, of any accessibility. `Known`
applies every rule of the type but membership, which a known value satisfies by declaration, and is called nowhere
else (`VO0037`). The generator lists the known values in `KnownValues`, builds the lookup of a closed set from them,
and publishes them in the schema, each with the `Description` of its attribute or the `<summary>` of its member. A
member it cannot read as a known value is `VO0036`. A known value or an example the type's own rules refuse is
`VO0031` when it is a constant the generator can evaluate the rule on; the contract kit checks the rest.
`[KnownValue("France", "FR")]` on the type, the form that took the value as text, is `VO0034`, and a code fix
rewrites it into the member.

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
| `IValueObjectExample<TSelf>` | `static TSelf Example { get; }` — the OpenAPI example, an instance of the type itself |

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

The hook replaces the `Pattern` option, which built its regular expression at run time, where native AOT
interprets it. Setting the option is now a compile error (`VO0021`). To migrate, move the expression from
`Pattern = "X"` into
`[GeneratedRegex("X", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)] public static partial Regex Pattern { get; }`:
those are the options and the timeout the option used, so behaviour does not change.

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
`Maximum` options, which held the bounds as text and are now a compile error (`VO0028`).

`IValueObjectExample<TSelf>` declares the example the OpenAPI schema publishes, as an instance of the type, which
its rules have accepted: `public static Percentage Example => Create(42);`. Without it, a value object publishes no
example, and an `[EntityId]` one of the right shape. It replaces the `Example` option, which held the example as
text and is now a compile error (`VO0035`).

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

`VO0004`, `VO0006`, `VO0013`, `VO0014`, `VO0022` and `VO0029` reported options written as text, which no longer
compile, and are retired. There is no `VO0012`.

| Id | Severity | Meaning |
| --- | --- | --- |
| `VO0001` | Error | The type is not `partial`. |
| `VO0002` | Error | The type is not a `readonly struct`: a class, an interface, a record, a `ref struct`, or a struct without `readonly`. |
| `VO0003` | Error | Unsupported underlying type. |
| `VO0005` | Error | A closed value set declares no value. |
| `VO0007` | Error | Arithmetic requested on a non-numeric type. |
| `VO0008` | Warning | Length constraints on a non-string type. |
| `VO0009` | Error | A containing type is not `partial`. |
| `VO0010` | Error | An uninitialized value object. |
| `VO0011` | Warning | A rule written without declaring its hook interface, so the generator will never call it. |
| `VO0015` | Error | A malformed entity identifier prefix. |
| `VO0016` | Error | Two types claiming the same prefix. |
| `VO0017` | Error | A normalization hook on an entity identifier, which owns its own. |
| `VO0018` | Error | Both `[EntityId]` and `[ValueObject<T>]` on one type. |
| `VO0019` | Error | The generated code cannot reopen, reach or name the type: it, or a type around it, is `file`-local; it is `private` or `protected`, or nested in such a type, inside a generic type; it is a generic `[EntityId]`, or one in a generic type; it has a type parameter it cannot use; or it is named after a member the generator writes on it. |
| `VO0020` | Error | `Comparison`, `ValueSet` or `Granularity` holds a value its enum does not define. |
| `VO0021` | Error | The `Pattern` option of `[ValueObject<T>]`, reported by the compiler and read by nothing. Implement `IValueObjectPatternValidator` with `[GeneratedRegex("X", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)] public static partial Regex Pattern { get; }` and remove `Pattern = "X"`. Any minor version may remove the option before 1.0.0. |
| `VO0023` | Error | `IValueObjectPatternValidator` on a value object whose underlying type is not `string`. |
| `VO0024` | Error | `IValueObjectPatternValidator` on an `[EntityId]`, which validates and publishes its own format. |
| `VO0025` | Warning | The `[GeneratedRegex]` behind `Pattern` sets `IgnoreCase`, `Multiline`, `Singleline` or `IgnorePatternWhitespace`, which the OpenAPI `pattern` cannot carry. |
| `VO0026` | Warning | The `[GeneratedRegex]` behind `Pattern` sets no `matchTimeoutMilliseconds`. |
| `VO0027` | Error | `[KnownValue]` on a member of an `[EntityId]`, which generates no known values. |
| `VO0028` | Error | The `Minimum` or `Maximum` option of `[ValueObject<T>]`, reported by the compiler and read by nothing. Implement `IValueObjectMinimum<T>` or `IValueObjectMaximum<T>` with a static property of the underlying type and remove the option. Any minor version may remove it before 1.0.0. |
| `VO0030` | Error | `IValueObjectMinimum<T>` or `IValueObjectMaximum<T>` over a type that takes no bound, or over another type than the underlying one. |
| `VO0031` | Error | The example or a known value declared on the type is one its own rules refuse, wherever the generator can evaluate them on a constant: a length, an empty string, a bound returned as a constant, a closed value set. The contract kit checks the rest at run time. |
| `VO0032` | Error | A value object created uninitialized, by `default` or `new T()`, in code another source generator wrote: Riok.Mapperly, the configuration binding generator, or a tool `adcodicem_value_objects.generated_code_tools` names in a `.globalconfig`. |
| `VO0033` | Warning | A value object whose own declaration lists no interface bringing `IParsable<TSelf>`, in a project where the Request Delegate Generator runs and that references ASP.NET Core's endpoint routing: that generator would bind it from the request body. A code fix lists the contract. |
| `VO0034` | Error | `[KnownValue("France", "FR")]` on the type, the form that took the value as text, reported by the compiler and read by nothing. A code fix rewrites it into a member, `[KnownValue] public static readonly CountryCode France = Known("FR");`. Any minor version may remove the form before 1.0.0. |
| `VO0035` | Error | The `Example` option of `[ValueObject<T>]` or `[EntityId]`, reported by the compiler and read by nothing. Implement `IValueObjectExample<TSelf>`. Any minor version may remove it before 1.0.0. |
| `VO0036` | Error | A member marked `[KnownValue]` that is not a `static readonly` field or a static get-only auto-property of the type, initialized by `Known(...)` with the value as its one argument. |
| `VO0037` | Error | `Known` called anywhere but in the initializer of a member marked `[KnownValue]`, which would skip the membership of a closed set. |
| `VO0038` | Error | `IValueObjectExample<T>` over another type than the value object itself. |

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
  Compat/     the packed packages in .NET 11 applications, outside the solution
samples/      a showcase API exercising the whole chain end to end
benchmarks/   the measurements behind the design decisions above
skills/       the agent skill, and the plugin manifest that distributes it
```

Integration tests start PostgreSQL, SQL Server and MongoDB through Testcontainers, so they need a Docker daemon.

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
