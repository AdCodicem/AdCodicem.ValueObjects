---
name: value-objects
description: Author and wire single-value DDD value objects with AdCodicem.ValueObjects on .NET — [ValueObject<T>] structs, [EntityId] public identifiers, the normalize/validate/format hook interfaces, and the JSON, EF Core, ASP.NET Core, Dapper, FluentValidation and OpenAPI integrations. Use whenever a C# project references AdCodicem.ValueObjects, whenever a primitive is being wrapped in a domain type (IBAN, email, reference code, money, strongly-typed identifier), and whenever a VO0001–VO0018 diagnostic needs fixing.
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
[ValueObject<string>(MaxLength = 254, Pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$", SchemaFormat = "email")]
public readonly partial struct EmailAddress : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();
}
```

Non-negotiable, each one a diagnostic if you get it wrong:

- `readonly partial struct` — never a `class`, never a `record struct`, never a non-`readonly` struct (`VO0002`).
- `partial` on the type *and* on every containing type (`VO0001`, `VO0009`).
- The underlying type is one of 22: `string`, `Guid`, `bool`, `char`, every built-in integer (`Int128` and
  `UInt128` included), `decimal`, `double`, `float`, `DateOnly`, `TimeOnly`, `DateTime`, `DateTimeOffset`,
  `TimeSpan` (`VO0003`).
- The attribute lives in `AdCodicem.ValueObjects.Annotations`, the hook interfaces in `AdCodicem.ValueObjects`.

## Declare rules on the attribute first

A rule stated on `[ValueObject<T>]` validates the value, sizes the EF Core column and becomes the OpenAPI
schema keyword. A rule buried in code does only the first. Never restate a declared rule in a hook.

| Option | Effect |
| --- | --- |
| `Pattern`, `MinLength`, `MaxLength` | Validation, EF column size, OpenAPI `pattern`/`minLength`/`maxLength`. |
| `Minimum`, `Maximum` | Inclusive bounds, written as invariant-culture **text**, parsed at compile time. |
| `Comparison` | Equality, ordering and hashing for `string` value objects. `Ordinal` by default. |
| `ValueSet = ValueSetKind.Closed` + `[KnownValue]` | Reference-data codes: frozen membership lookup, named constants, schema `enum`. |
| `Arithmetic = true` | Operators and generic math on a numeric type. Every result is re-validated. |
| `ImplicitConversionToValue`, `ExplicitConversionFromValue` | Conversions, opt-in per type. |
| `AllowEmpty`, `AllowDefault` | Loosen the two defaults that exist to catch mistakes. |
| `SchemaFormat`, `Example`, `Description` | OpenAPI documentation. |

Full table with defaults and worked examples: `references/authoring.md`.

## Rules the attribute cannot express are hook interfaces

Declare the interface. A correctly written rule whose interface is missing **never runs** — the generator
does not look at member names, and `VO0011` is only a warning.

| Interface | Member to implement |
| --- | --- |
| `IValueObjectNormalizer<TValue>` | `public static TValue NormalizeValue(TValue value)` |
| `IValueObjectSpanNormalizer` | `public static string NormalizeValue(ReadOnlySpan<char> value)` — `string` only, alongside the above |
| `IValueObjectValidator<TValue>` | `public static ValidationResult ValidateValue(in TValue value)` |
| `IValueObjectFormatter<TValue>` | `public static bool TryFormatValue(in TValue value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)` |
| `IValueObjectStringFormatter<TValue>` | `public static string FormatValue(in TValue value, ReadOnlySpan<char> format, IFormatProvider? provider)` |

Hook members are `public static` — a static abstract interface member cannot be anything else.

```csharp
[ValueObject<string>(MinLength = 15, MaxLength = 34, Pattern = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
public readonly partial struct Iban : IValueObjectNormalizer<string>, IValueObjectValidator<string>
{
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
- **Formatting takes over entirely** when declared, default format included.

## What is generated — never write it yourself

Constructor, `Create`, `TryCreate(value, out result)` and `TryCreate(value, out result, out validation)`,
`CreateUnchecked`, `Normalize`, `Validate`, `Parse` and `TryParse` (`string` and `ReadOnlySpan<char>`, with and
without an `IFormatProvider`), the 4-argument `TryParse` reporting *why* text was rejected, `Value`,
`IsDefault`, `ToString()` / `ToString(format, provider)` / `TryFormat`, `Equals` / `==` / `!=` / `GetHashCode`,
`CompareTo` / `<` / `>` / `<=` / `>=`, `Schema`, the `System.Text.Json` converter, the `TypeConverter`, and a
`[ModuleInitializer]` registration into `ValueObjectRegistry`. Closed sets also get their named constants and
`KnownValues`; `Arithmetic = true` adds the operators plus `Zero`, `One`, `IsZero`, `Min`, `Max`.

So: **do not** hand-write a constructor, a factory, `Equals`/`GetHashCode`, a `JsonConverter`, a
`TypeConverter`, or an EF `HasConversion` per property. Add only domain members the generator knows nothing
about (`CountryCode => Value[..2]`, a `New()` factory, named format constants).

## Never do these

- `default(Iban)` or `new Iban()` — build error `VO0010`, because those bypass validation. Construct through
  `Create`/`TryCreate`; express absence as `Iban?`, never as an empty or default instance. A test that needs
  one disables `VO0010` on that line with a comment saying why.
- `NormalizeCore` / `ValidateCore` / `TryFormatCore` — the pre-interface names. They compile, they never run.
- `CreateUnchecked` on input from outside the application. It validates nothing; it is for the EF read path.
- A separate FluentValidation rule restating length or pattern — defer to the value object
  (`MustParseAs`, `MustSatisfy`).
- A nullable underlying value (`[ValueObject<string?>]`). `null` is always rejected.

## Wiring, once, at the composition root

```csharp skip
builder.Services.AddControllers().AddValueObjects();                                  // AspNetCore
builder.Services.Configure<ApiBehaviorOptions>(o => o.AddValueObjectProblemDetails()); // AspNetCore
builder.Services.AddOpenApi(o => o.AddValueObjects());                                 // OpenApi
ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);                       // Dapper

protected override void ConfigureConventions(ModelConfigurationBuilder builder)        // EntityFrameworkCore
    => builder.ConfigureValueObjects(typeof(Iban).Assembly);
```

Minimal APIs need nothing: a generated value object implements `IParsable<T>`. Reflection-based
`System.Text.Json` needs nothing either; a source-generated `JsonSerializerContext` needs the
`AdCodicem.ValueObjects.Json` package. Per-package details, EF `strict` mode and problem-details payloads:
`references/integrations.md`.

## Test with the contract kit

```csharp skip
public sealed class IbanContract : ValueObjectContract<Iban, string>   // AdCodicem.ValueObjects.Testing
{
    protected override IEnumerable<string> AcceptedValues => ["FR7630006000011234567890189"];
    protected override IEnumerable<string> RejectedValues => ["", "not-an-iban"];
}
```

That derives a dozen checks: normalization settles after one pass, equality agrees with the hash code, ordering
agrees with equality, text and JSON round-trip, and every entry point rejects a bad value the same way. Write
it for every value object, then test only the domain behaviour that is actually yours.

## Reference files

| File | Read it for |
| --- | --- |
| `references/authoring.md` | Every attribute option, closed value sets, arithmetic, formats, span normalization. |
| `references/integrations.md` | ASP.NET Core, EF Core, JSON, Dapper, FluentValidation, OpenAPI, Newtonsoft. |
| `references/identifiers.md` | `[EntityId]` Stripe-style public identifiers, `AnyEntityId`, deterministic tests. |
| `references/diagnostics.md` | `VO0001`–`VO0018`, with the fix for each. |

Published documentation: <https://adcodicem.github.io/AdCodicem.ValueObjects/>
