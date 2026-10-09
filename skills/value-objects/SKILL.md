---
name: value-objects
description: Author and wire single-value DDD value objects with AdCodicem.ValueObjects on .NET — [ValueObject<T>] structs, [EntityId] public identifiers, the normalize/validate/format hook interfaces, and the JSON, EF Core, ASP.NET Core, Dapper, MongoDB.Driver (with a $jsonSchema validator and entity identifiers minted on insert), MessagePack and SignalR, FluentValidation, OpenAPI (built-in stack and Swashbuckle), Serilog, Microsoft.Extensions.AI (tools, structured output) and Model Context Protocol servers (tool schemas, refused tool arguments) integrations, valid test data for AutoFixture, Bogus and FsCheck, and XML serialization (XmlSerializer, DataContractSerializer) opted into per assembly. Use whenever a C# project references AdCodicem.ValueObjects, whenever a primitive is being wrapped in a domain type to address primitive obsession (IBAN, email, reference code, money, strongly-typed identifier), and whenever a VO0001–VO0038 diagnostic needs fixing.
license: MIT
---

# AdCodicem.ValueObjects

A `readonly partial struct` marked `[ValueObject<T>]` gets its entire implementation from a Roslyn incremental
generator and crosses every boundary as its underlying type: an IBAN is a JSON string, a `VARCHAR`, and a
query-string parameter — never an object wrapper.

**You declare the type and its rules. You never write the plumbing.** Everything in
[the generated surface](#what-is-generated--never-write-it-yourself) already exists; writing it by hand is a
compile error or dead code.

```
dotnet add package AdCodicem.ValueObjects
```

## The shape

```csharp
using System.Text.RegularExpressions;

[ValueObject<string>(MaxLength = 254, SchemaFormat = "email")]
public readonly partial struct EmailAddress : IValueObjectNormalizer<string>, IValueObjectPatternValidator
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    public static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();
}
```

Non-negotiable, each one a diagnostic if you get it wrong:

- `readonly partial struct` — never a `class`, never a `record struct`, never a `ref struct`, never a non-`readonly` struct (`VO0002`).
- `partial` on the type *and* on every containing type (`VO0001`, `VO0009`).
- Never `file`-local, nor in a `file` type; a name that is not a generated member's, such as `Value` or `Create`;
  never a generic `[EntityId]`, nor one in a generic type; never `private` or `protected` inside a generic type
  (`VO0019`). Generic value objects, interfaces around one, and `private` or `protected` ones are fine:
  `references/authoring.md` says what changes.
- The underlying type is one of 22: `string`, `Guid`, `bool`, `char`, every built-in integer (`Int128` and
  `UInt128` included), `decimal`, `double`, `float`, `DateOnly`, `TimeOnly`, `DateTime`, `DateTimeOffset`,
  `TimeSpan` (`VO0003`).
- The attribute lives in `AdCodicem.ValueObjects.Annotations`, the hook interfaces in `AdCodicem.ValueObjects`.
  `[GeneratedRegex]` and `Regex` need `using System.Text.RegularExpressions;`, which no implicit using brings in.

## Declare rules on the attribute first

A rule stated on `[ValueObject<T>]` validates the value, sizes the EF Core column and becomes the OpenAPI
schema keyword. A rule buried in code does only the first. Never restate a declared rule in a hook.

A pattern, bounds, known values and the example are the exceptions: each is a value the compiler checks, so it is
declared in code rather than as text on the attribute. A pattern is declared through the
`IValueObjectPatternValidator` hook, and the text of its `[GeneratedRegex]` still becomes the OpenAPI `pattern`.
Bounds are declared through `IValueObjectMinimum<T>` and `IValueObjectMaximum<T>`, as values of the underlying type,
and still become the OpenAPI bounds. Known values are members marked `[KnownValue]`, and the example is
`IValueObjectExample<TSelf>`. Each is a declared rule all the same: written once, never restated in `ValidateValue`.
The `Pattern`, `Minimum`, `Maximum` and `Example` options, written as text, no longer compile (`VO0021`, `VO0028`,
`VO0035`).

| Option | Effect |
| --- | --- |
| `MinLength`, `MaxLength` | Validation, EF column size, OpenAPI `minLength`/`maxLength`. |
| `Minimum`, `Maximum` | **Removed** (`VO0028`, a compile error): implement `IValueObjectMinimum<T>` / `IValueObjectMaximum<T>` instead. |
| `Comparison` | Equality, ordering and hashing for `string` value objects. `Ordinal` by default. |
| `ValueSet = ValueSetKind.Closed` + `[KnownValue]` members | Reference-data codes: frozen membership lookup, schema `enum` whose values generated clients name after the known values. |
| `Arithmetic = true` | Operators and generic math on a numeric type. Every result is re-validated. |
| `ImplicitConversionToValue`, `ExplicitConversionFromValue` | Conversions, opt-in per type. |
| `AllowEmpty`, `AllowDefault` | Loosen the two defaults that exist to catch mistakes. |
| `SchemaFormat`, `Description` | OpenAPI documentation. `Example` is removed (`VO0035`, a compile error): implement `IValueObjectExample<TSelf>`. |

Full table with defaults and worked examples: `references/authoring.md`.

## Rules the attribute cannot express are hook interfaces

Declare the interface. A correctly written rule whose interface is missing **never runs** — the generator
does not look at member names, and `VO0011` is only a warning.

| Interface | Member to implement |
| --- | --- |
| `IValueObjectNormalizer<TValue>` | `public static TValue NormalizeValue(TValue value)` |
| `IValueObjectSpanNormalizer` | `public static string NormalizeValue(ReadOnlySpan<char> value)` — `string` only, alongside the above |
| `IValueObjectPatternValidator` | `[GeneratedRegex("…", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)] public static partial Regex Pattern { get; }` — `string` only; runs after `MinLength`/`MaxLength`, rejects as `value_object.invalid_format`, and its text is the OpenAPI `pattern` |
| `IValueObjectMinimum<TValue>`, `IValueObjectMaximum<TValue>` | `public static TValue Minimum => …;`, `public static TValue Maximum => …;` — numbers, `char`, dates, times and durations; inclusive, checked before `ValidateValue`, rejected as `value_object.out_of_range`, and published as the OpenAPI bounds |
| `IValueObjectValidator<TValue>` | `public static ValidationResult ValidateValue(in TValue value)` |
| `IValueObjectFormatter<TValue>` | `public static bool TryFormatValue(in TValue value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)` |
| `IValueObjectStringFormatter<TValue>` | `public static string FormatValue(in TValue value, ReadOnlySpan<char> format, IFormatProvider? provider)` |
| `IValueObjectExample<TSelf>` | `public static TSelf Example => Create(…);` — the OpenAPI example, an instance of the type itself (`VO0038` otherwise) |

Hook members are `public static` — a static abstract interface member cannot be anything else. `Pattern` is
also `partial`: the regex source generator writes its body.

```csharp
using System.Text.RegularExpressions;

[ValueObject<string>(MinLength = 15, MaxLength = 34)]
public readonly partial struct Iban
    : IValueObjectNormalizer<string>, IValueObjectPatternValidator, IValueObjectValidator<string>
{
    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    public static string NormalizeValue(string value)
        => value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    public static ValidationResult ValidateValue(in string value)
        => Mod97(value) == 1
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");

    private static int Mod97(ReadOnlySpan<char> value)
    {
        var remainder = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[(i + 4) % value.Length];
            remainder = char.IsAsciiDigit(character)
                ? ((remainder * 10) + (character - '0')) % 97
                : ((remainder * 100) + (character - 'A' + 10)) % 97;
        }

        return remainder;
    }
}
```

Contract of the hooks:

- **Normalization is idempotent and never rejects.** `NormalizeValue(NormalizeValue(x))` equals
  `NormalizeValue(x)`. A value that cannot be normalized is rejected by the validator instead.
- **It never sees `null`.** The generated `Normalize` guards first.
- **Validation returns, it does not throw.** `ValidationResult.Success`, or `Failure(code, message)` /
  `Required` / `InvalidFormat` / `OutOfRange` / `TooLong` / `TooShort`. Validation is fail-fast: the first
  violated rule wins, declared rules run before the hook.
- **The pattern runs in the declared rules' place.** After `MinLength`/`MaxLength`, before the known values and
  `ValidateValue`; a value it does not match is `value_object.invalid_format`, "The value does not match the
  expected format." Its text, read off `[GeneratedRegex]` at compile time, is the OpenAPI `pattern`, which
  carries no `RegexOptions`: write case insensitivity into the pattern itself, `[A-Za-z]` (`VO0025`). Always
  set `matchTimeoutMilliseconds` (`VO0026`).
- **A bound is a constant**, an expression-bodied property (`=> …`, never `{ get; } = …`): the check reads it each
  time and the schema once, as the assembly loads. A bound relative to the clock ("not in the future") is a rule,
  written in `ValidateValue` against a `TimeProvider` a test can fix. Over a
  type that takes no bound (`string`, `Guid`, `bool`, an `[EntityId]`) or over another type than the underlying
  one, the hook is `VO0030`.
- **Formatting takes over entirely** when declared, default format included: `ToString()` goes through the hook.
  With both formatter hooks, `FormatValue` answers everywhere.

## What is generated — never write it yourself

Constructor, `Create`, `TryCreate(value, out result)` and `TryCreate(value, out result, out validation)`,
`CreateUnchecked`, `Normalize`, `Validate`, `Parse` and `TryParse` (`string` and `ReadOnlySpan<char>`, with and
without an `IFormatProvider`; a `null` one is the invariant culture, where a `decimal`, `double` or `float` refuses
a group separator, `12,5` or `1,234.5`), the 4-argument `TryParse` reporting *why* text was rejected, `Value`,
`IsDefault` (explicit: read it through `IValueObject<TSelf, TValue>`), `ToString()` /
`ToString(format, provider)` / `TryFormat`, `Equals` / `==` / `!=` / `GetHashCode`, `CompareTo` / `<` / `>` /
`<=` / `>=`, `Schema` (the static member of `IValueObject<TSelf, TValue>`: generic code reads `TSelf.Schema`), the
`System.Text.Json` converter, the `TypeConverter` (text and the underlying value, and
over a number any numeric type, checked: never truncated), and a `[ModuleInitializer]` registration into
`ValueObjectRegistry`. A type with known values also gets `KnownValues`, and the private `Known` factory their
members are initialized through;
`Arithmetic = true` adds the operators plus `Zero`, `One`, `IsZero`, `Min`, `Max`. In an assembly marked
`[assembly: ValueObjectXmlSerialization]`, every value object and identifier also gets an explicit `IXmlSerializable`
and a public static `GetXmlSchema` named by `[XmlSchemaProvider]`; one that implements `IXmlSerializable` itself keeps
its own.

So: **do not** hand-write a constructor, a factory, `Equals`/`GetHashCode`, a `JsonConverter`, a
`TypeConverter`, or an EF `HasConversion` per property. Add only domain members the generator knows nothing
about (`CountryCode => Value[..2]`, a `New()` factory, named format constants).

## Never do these

- `default(Iban)` or `new Iban()`, and the same on an `[EntityId]` — build error `VO0010`, because those bypass
  validation. Construct through `Create`/`TryCreate`; express absence as `Iban?`, never as an empty or default
  instance. A test that needs one disables `VO0010` on that line with a comment saying why. One that slips past the
  analyzer (an entity property never set) is refused by every writer when its type rejects the default (JSON,
  Dapper, MongoDB, MessagePack, XML in an assembly marked `[assembly: ValueObjectXmlSerialization]`, EF Core
  `SaveChanges`); an EF Core `Iban?` column stores `NULL` instead.
- `NormalizeCore` / `ValidateCore` / `TryFormatCore` — the pre-interface names. They compile, they never run.
- `Pattern = "..."` on `[ValueObject<T>]` — a compile error (`VO0021`). Move the text to the hook, with
  `RegexOptions.CultureInvariant` and `matchTimeoutMilliseconds: 1000`; behaviour is unchanged.
- A `static Regex Pattern` without `IValueObjectPatternValidator` — `VO0011`. It never runs as the declared
  pattern and never reaches the schema.
- `Minimum = "..."` or `Maximum = "..."` on `[ValueObject<T>]` — a compile error (`VO0028`): the compiler cannot
  check text. Declare `public static T Minimum => …;` through `IValueObjectMinimum<T>` (and `Maximum` through
  `IValueObjectMaximum<T>`). A `Minimum` or `Maximum` written without its interface never runs: `VO0011`.
- `[KnownValue("France", "FR")]` on the type — a compile error (`VO0034`), which its code fix rewrites. Declare
  `[KnownValue] public static readonly CountryCode France = Known("FR");`, and call `Known` nowhere else (`VO0037`).
  A closed set's static member created through `Create` throws from the type initializer.
- `Example = "..."` on `[ValueObject<T>]` or `[EntityId]` — a compile error (`VO0035`). Implement
  `IValueObjectExample<TSelf>`.
- `CreateUnchecked` on input from outside the application. It validates nothing; it is for the EF read path.
- A Riok.Mapperly mapper, or an options class bound by the configuration binding generator, that holds a value
  object it has no method for — build error `VO0032`: another generator cannot see the generated members, so it
  writes `new Iban()`. Give Mapperly `public static Iban ToIban(string value) => Iban.Create(value);` and its
  reverse through `[UseStaticMapper]`; keep the underlying type in an options class.
- An AutoMapper conversion to a value object with no map. The private constructor takes a tag only the generated
  code supplies, so AutoMapper finds no constructor and throws `AutoMapperMappingException`. Map through the
  factory: `CreateMap<string, Iban>().ConvertUsing(s => Iban.Create(s))`. On `ExplicitConversionFromValue = true`,
  AutoMapper calls the explicit conversion, which validates.
- A separate FluentValidation rule restating length or pattern — defer to the value object
  (`MustParseAs`, `MustSatisfy`).
- A nullable underlying value (`[ValueObject<string?>]`). `null` is always rejected.

## Wiring, once, at the composition root

```csharp skip
builder.Services.AddControllers().AddValueObjects();                                  // AspNetCore
// MVC on Newtonsoft.Json: AddControllers().AddNewtonsoftJson().AddValueObjectsNewtonsoftJson(); // AspNetCore.NewtonsoftJson
builder.Services.Configure<ApiBehaviorOptions>(o => o.AddValueObjectProblemDetails()); // AspNetCore, MVC only
builder.Services.AddProblemDetails();
builder.Services.AddValueObjectHttpProblemDetails();                                   // AspNetCore.Http, minimal APIs
app.UseExceptionHandler();                                                             // where ThrowOnBadRequest is on
app.MapGroup("/api").WithValueObjectProblemDetails();                                  // AspNetCore.Http, minimal APIs
builder.Services.AddOpenApi(o => o.AddValueObjects());                                 // OpenApi
builder.Services.AddSwaggerGen(o => o.AddValueObjects());                              // Swashbuckle, after IncludeXmlComments
ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);                       // Dapper
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));    // MongoDB, first
ValueObjectBson.Register(typeof(Iban).Assembly);                                       // MongoDB, before anything is serialized
EntityIdBson.Register(typeof(AccountId).Assembly);                                     // Identifiers.MongoDB, on insert
var msgpack = MessagePackSerializerOptions.Standard.WithValueObjects();                // MessagePack
builder.Services.AddSignalR().AddMessagePackProtocol(o => o.UseValueObjects());        // MessagePack, client too
new LoggerConfiguration().Enrich.FromLogContext()                                      // Serilog, after FromLogContext
    .Destructure.ValueObjects(o => { o.CaptureAsUnderlyingValue = true; o.Assemblies.Add(typeof(Iban).Assembly); });
// [assembly: ValueObjectXmlSerialization]  in the domain assembly: XmlSerializer, DataContractSerializer, MVC XML formatters

protected override void ConfigureConventions(ModelConfigurationBuilder builder)        // EntityFrameworkCore
    => builder.ConfigureValueObjects(typeof(Iban).Assembly);
```

Minimal APIs need no package to bind: a generated value object implements `IParsable<T>`. They answer a rejected
value with a bare 400, with no parameter, message or code, unless `WithValueObjectProblemDetails()` covers the
endpoints (`AdCodicem.ValueObjects.AspNetCore.Http`, AOT-compatible): a route, query or header value then gets MVC's
problem details with its code, and a JSON body too where `RouteHandlerOptions.ThrowOnBadRequest` is on. Where the
Request Delegate Generator runs (`PublishAot`, `PublishTrimmed`, `EnableRequestDelegateGenerator`), a value object
declared in the project that maps the endpoints lists its contract on its declaration, `: IValueObject<Sku, string>`
(`VO0033`).
Every exception an integration throws for a refused value carries the rule's code: read it with
`ValueObjectErrors.TryGetCode(exception, out var code)`, and see `references/integrations.md` for where each keeps it.
Reflection-based `System.Text.Json` needs nothing either; a source-generated `JsonSerializerContext` needs the
`AdCodicem.ValueObjects.Json` package, and so does a JSON Schema exported for a tool, structured output or an MCP server
(`ValueObjectJsonSchema.TransformSchemaNode`), which otherwise describes every value object as `true`. A
Microsoft.Extensions.AI tool takes `AdCodicem.ValueObjects.AI`: `new AIJsonSchemaCreateOptions().WithValueObjects()` in
its `AIFunctionFactoryOptions`, and `.WithValueObjectValidation()` on the function, which answers a refused argument
with its rule code instead of "Error: Function failed.". A Model Context Protocol server takes
`AdCodicem.ValueObjects.ModelContextProtocol`: `.WithValueObjectTools<OrderTools>()` in place of the SDK's
`.WithTools<OrderTools>()`. Per-package details, EF `strict` mode and problem-details payloads:
`references/integrations.md`.

## Test with the contract kit

```csharp skip
public sealed class IbanContract : ValueObjectContract<Iban, string>   // AdCodicem.ValueObjects.Testing
{
    protected override IEnumerable<string> AcceptedValues => ["FR7630006000011234567890189"];
    protected override IEnumerable<string> RejectedValues => ["", "not-an-iban"];
}
```

That derives over a dozen checks: normalization settles after one pass, equality agrees with the hash code, ordering
agrees with equality, text and JSON round-trip, every entry point rejects a bad value the same way, the type
accepts its declared `Example` and every known value, and `Schema.KnownValueDetails` lists those known values one for
one, in order, each named (these three read `TSelf.Schema`, so a hand-written type is checked too, registered or
not). Add `protected override bool DerivesRejectedValues => true;` and the kit also checks that the type refuses every
value its schema rules out: one character past each length, one step past each bound, a value outside a closed set (a
type whose normalizer clamps fails it). Write it for every value object, then test only the domain behaviour that is
actually yours.

### Valid values for tests

Never write a generator that restates the rules: `AdCodicem.ValueObjects.Testing.Data` draws values the type accepts
from its schema, for any test framework.

```csharp skip
var options = new ValueObjectSamplerOptions()
    .Use<Iban, string>(random => /* an IBAN with its check digits */ ...); // a rule no schema carries: register it
var sampler = new ValueObjectSampler(new Random(seed), options);

var iban = sampler.Next<Iban, string>();               // only values Iban.TryCreate accepts
var edges = sampler.Boundaries<Quantity, short>();     // Minimum, Maximum, lengths, known values
var past = sampler.RejectedValues<Quantity, short>();  // one step past each rule, with the rule it breaks
```

A checksum or another validator is reached only through `TryCreate`: register a generator with `Use<TSelf, TValue>`,
or the sampler falls back on the type's example and, without one, throws `ValueObjectSamplingException`, which names
the registration to add, or the registered generator whose values the type refuses. `ValueObjectSampler.Shrink` proposes simpler accepted values to a property-based testing
library. AutoFixture, Bogus and FsCheck get the same values through `AdCodicem.ValueObjects.AutoFixture`
(`new ValueObjectCustomization()`), `.Bogus` (`RuleForValueObjects()`, called before the other rules) and `.FsCheck`
(`ArbMap.Default.MergeValueObjects(assembly)`): see `references/integrations.md`.

## Reference files

| File | Read it for |
| --- | --- |
| `references/authoring.md` | Every attribute option, known values and closed value sets, the example, arithmetic, formats, span normalization, personal data. |
| `references/integrations.md` | ASP.NET Core, EF Core, JSON and JSON Schema, Dapper, MongoDB.Driver and its `$jsonSchema` validator, MessagePack and SignalR, FluentValidation, OpenAPI (built-in and Swashbuckle), Newtonsoft, Serilog, language models (Microsoft.Extensions.AI), MCP servers, test data for AutoFixture, Bogus and FsCheck. |
| `references/identifiers.md` | `[EntityId]` Stripe-style public identifiers, their EF Core columns and MongoDB id generation, `AnyEntityId`, deterministic tests. |
| `references/diagnostics.md` | `VO0001`–`VO0038`, with the fix for each. |

Published documentation: <https://adcodicem.github.io/AdCodicem.ValueObjects/>
