# Authoring reference

## `[ValueObject<TValue>]`, option by option

| Option | Type | Default | Effect |
| --- | --- | --- | --- |
| `Pattern` | `string?` | none | **Deprecated** (`VO0021`, removed in the next major): implement `IValueObjectPatternValidator` instead ([below](#pattern)). Regular expression the **normalized** value must match, built at run time, which native AOT interprets. Also the OpenAPI `pattern`. Malformed → `VO0014`; set beside the hook → `VO0022`. |
| `MinLength` / `MaxLength` | `int` | `-1` (unconstrained) | `string` only (`VO0008` otherwise). Validation, OpenAPI `minLength`/`maxLength`, and the EF Core column size. |
| `Minimum` / `Maximum` | `string?` | none | **Deprecated** (`VO0028`, removed in the next major): implement `IValueObjectMinimum<T>` / `IValueObjectMaximum<T>` instead ([below](#bounds)). Inclusive bounds written as **text**, in the one form of the underlying type ([below](#bounds-and-known-values-written-as-text)). Numbers, `char`, dates, times and durations only: on `string`, `Guid` or `bool` → `VO0004`. Parsed at compile time; any other text, or a value outside the type → `VO0004`; set beside the hook → `VO0029`. |
| `Comparison` | `StringComparison` | `Ordinal` | `string` only. Drives equality, ordering, hashing. Pick `OrdinalIgnoreCase` only when the value is not case-normalized, and make the database collation agree. A value the enum does not define → `VO0020`. |
| `ValueSet` | `ValueSetKind` | `Open` | `Closed` accepts only the declared `[KnownValue]`s. Empty closed set → `VO0005`; a value the enum does not define → `VO0020`. |
| `Arithmetic` | `bool` | `false` | Numeric types only (`VO0007` otherwise). Operators, generic math, `Zero`, `One`, `IsZero`, `Min`, `Max`. |
| `ImplicitConversionToValue` | `bool` | `false` | `string s = iban;` — reading stays terse while construction stays explicit. |
| `ExplicitConversionFromValue` | `bool` | `false` | `(Iban)text` — validates, throws `ValueObjectException` on rejection. |
| `AllowEmpty` | `bool` | `false` | `string` only. Accepts `""`. `null` is rejected regardless: absence is `Iban?`. |
| `AllowDefault` | `bool` | `false` | Silences `VO0010`. Only for a type whose zero state is meaningful, such as a sequence number starting at zero. |
| `SchemaFormat` | `string?` | natural format of the underlying type | OpenAPI `format` (`uuid`, `date`, `int64`, or your own: `iban`, `email`). |
| `Example` | `string?` | none | OpenAPI example, written as text the type parses; published in its JSON form. |
| `Description` | `string?` | XML `<summary>` of the type (`///` or `/** */`), as plain text | OpenAPI description. |

Declarative rules run **before** any hook, so a validator hook only ever sees values that already satisfy them.
The [pattern](#pattern) and the [bounds](#bounds) are declared through hooks, and run among them: the pattern right
after the lengths, the bounds after the pattern.

## Bounds and known values written as text

A `[KnownValue]` given as a string, and the deprecated `Minimum` and `Maximum`, are read at compile time in **one
form per underlying type**, and in no other: no culture, no time zone, no white space around the value. Anything
else is `VO0013` for a known value and `VO0004` for a bound, and the message names the form. A bound hook is quoted in
the message of a rejected value in that form for an integer, a `decimal`, a `char`, a `DateOnly` or a `TimeSpan`,
and in its round-trip form for a real (`1E-05`), a time (`06:00:00.0000000`) or a date and time, a `DateTime`
without its kind.

| Underlying type | Form | Example |
| --- | --- | --- |
| `sbyte`, `short`, `int`, `long`, `Int128` | Digits, with `-` in front when negative. | `"-42"` |
| `byte`, `ushort`, `uint`, `ulong`, `UInt128` | Digits alone. | `"42"` |
| `decimal` | Digits, an optional `-` in front, an optional fraction after `.`. No exponent. | `"-19.99"` |
| `double`, `float` | As `decimal`, plus an optional exponent (`e` or `E`, an optional sign, digits). Finite, and zero only when written as zero. | `"9.1e-31"` |
| `char` | Exactly one character. | `"A"` |
| `DateOnly` | `yyyy-MM-dd` | `"2024-01-31"` |
| `TimeOnly` | `HH:mm`, `HH:mm:ss` or `HH:mm:ss.fffffff`, one to seven digits of fraction. | `"08:30"` |
| `DateTime` | `yyyy-MM-dd` or `yyyy-MM-ddTHH:mm[:ss[.fffffff]]`. Never an offset or `Z`. | `"2024-01-31T08:30"` |
| `DateTimeOffset` | `yyyy-MM-ddTHH:mm[:ss[.fffffff]]` followed by `Z`, `+HH:mm` or `-HH:mm`, always. | `"2024-01-31T08:30+01:00"` |
| `TimeSpan` | `[-][d.]hh:mm:ss[.fffffff]`, the invariant constant format `"c"`. | `"1.12:00:00"` |
| `string` | Any text. Known values only. | `"FR"` |
| `Guid` | Any form `Guid.Parse` reads. Known values only. | `"6f9619ff-8b86-d011-b42d-00c04fc964ff"` |
| `bool` | `true` or `false`, in any case. Known values only. | `"true"` |

The value must exist in the type: `"300"` is no `byte`, `"2023-02-29"` no date, `"25:00"` no time, `"1e-400"`
(which reads as zero) no `double`. A time of day alone is neither a `DateTime` nor a `DateTimeOffset`, since it
would take the date of the day the project is built. `string`, `Guid` and `bool` take no bound at all. A known value may also be a C# constant — `200`, `0.5`,
`'A'`, `true` — which is held to the same form through its invariant text, a `double` or a `float` in round-trip
form. A `typeof(...)`, an enum member, an array and `null` are not values: `VO0013`.

## Normalization

```csharp
[ValueObject<string>(MaxLength = 64)]
public readonly partial struct ProductCode : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}
```

Idempotent, never rejecting, never called with `null`. The generated `Normalize` is the member callers use: it
guards the null and defers to `NormalizeValue`.

### Span normalization, for string value objects on hot paths

Adding `IValueObjectSpanNormalizer` routes parsing and JSON reading straight from the text, so ingesting a
value allocates the normalized string and nothing else — it halves what `TryParse` allocates. Write the
value-typed overload as a one-line delegation so the rule exists once:

```csharp
[ValueObject<string>(MaxLength = 34)]
public readonly partial struct AccountNumber : IValueObjectNormalizer<string>, IValueObjectSpanNormalizer
{
    public static string NormalizeValue(string value) => NormalizeValue(value.AsSpan());

    public static string NormalizeValue(ReadOnlySpan<char> value)
    {
        Span<char> buffer = value.Length <= 64 ? stackalloc char[64] : new char[value.Length];

        var length = 0;
        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character))
            {
                buffer[length++] = char.ToUpperInvariant(character);
            }
        }

        return new string(buffer[..length]);
    }
}
```

## Validation

```csharp
[ValueObject<int>]
public readonly partial struct SequenceNumber
    : IValueObjectMinimum<int>, IValueObjectMaximum<int>, IValueObjectValidator<int>
{
    public static int Minimum => 1;

    public static int Maximum => 999_999_999;

    public static ValidationResult ValidateValue(in int value)
        => value % 2 == 0
            ? ValidationResult.Success
            : ValidationResult.Failure("sequence.odd", "A sequence number must be even.");
}
```

Factories on `ValidationResult`: `Success`, `Failure(errorCode, errorMessage)`, `Required`, `InvalidFormat`,
`OutOfRange`, `TooLong`, `TooShort`. The well-known codes live on `ValueObjectErrorCodes`
(`value_object.required`, `value_object.invalid_format`, `value_object.out_of_range`, `value_object.too_long`,
`value_object.too_short`, `value_object.not_a_known_value`, `value_object.not_parsable`). They are stable
strings that reach ProblemDetails responses, so define your own for domain rules rather than reusing a
framework code that means something else.

`ValidationResult` is a `readonly struct` whose success state is `default`: the happy path allocates nothing.
Never throw from a validator — rejection is a return value.

### Pattern

```csharp
using System.Text.RegularExpressions;

[ValueObject<string>(MaxLength = 3)]
public readonly partial struct CurrencyCode : IValueObjectNormalizer<string>, IValueObjectPatternValidator
{
    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}
```

A string value object declares its format through `IValueObjectPatternValidator`: a `public static partial`
`Regex Pattern` marked `[GeneratedRegex]`, which the regex source generator compiles. That generator only sees
code a person wrote, so the value object generator cannot write `[GeneratedRegex]` itself; the hook takes the one
you write. `[GeneratedRegex]` and `Regex` need `using System.Text.RegularExpressions;`, which is not among the
implicit usings.

The pattern runs on the **normalized** value, after `MinLength` and `MaxLength`, before the known values and
`ValidateValue`. A value it does not match is rejected as `value_object.invalid_format`, with "The value does not
match the expected format." Its text, read off the `[GeneratedRegex]` attribute when the type compiles, is the
OpenAPI `pattern`, so the rule is still declared once: never test it again in `ValidateValue`.

- Always set `matchTimeoutMilliseconds`. Without it, a pathological input holds the thread for as long as the
  match runs (`VO0026`, warning).
- `RegexOptions` do not reach the OpenAPI `pattern`, which is the text alone. `IgnoreCase`, `Multiline`,
  `Singleline` and `IgnorePatternWhitespace` would make clients check values differently from the type
  (`VO0025`, warning): write the rule into the pattern, `[A-Za-z]` rather than `IgnoreCase`.
- `string` only (`VO0023` on any other type), and never on an `[EntityId]`, which owns its format (`VO0024`).
- A malformed regular expression is reported by the regex generator, not by `VO0014`.
- A `static Regex Pattern` written without the interface never runs as the pattern: `VO0011`.

Migrating from the deprecated `Pattern` option is mechanical. Remove `Pattern = "X"` and add the hook with the
same text, `RegexOptions.CultureInvariant` and `matchTimeoutMilliseconds: 1000`: those are the options and the
timeout the option used, so behaviour is unchanged. Before:

```csharp skip
// Reported as VO0021, and removed in the next major.
[ValueObject<string>(MaxLength = 3, Pattern = "^[A-Z]{3}$")]
public readonly partial struct CurrencyCode : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}
```

After: the `CurrencyCode` above. Keeping both is `VO0022`, and the hook wins until the option is removed.

### Bounds

```csharp
[ValueObject<DateOnly>]
public readonly partial struct BirthDate : IValueObjectMinimum<DateOnly>, IValueObjectMaximum<DateOnly>
{
    public static DateOnly Minimum => new(1900, 1, 1);

    public static DateOnly Maximum => new(2100, 12, 31);
}
```

A number, a `char`, a date, a time or a duration declares its inclusive bounds as values of its underlying type,
which the compiler checks: any expression of that type, `decimal` and `Int128` included. The bounds run after the
pattern, before the known values and `ValidateValue`. A value outside them is `value_object.out_of_range`, "The
value must be greater than or equal to 1900-01-01." They are the OpenAPI `minimum` and `maximum` of a number, and
the `x-minimum`, `x-maximum` and a sentence of the description of a value written as a string.

- A bound is a **constant**, written as an expression-bodied property, `=> …`. The check reads it each time and the
  schema once, as the assembly loads, so it must not throw. A bound relative to the clock, "not in the future" or
  "within 90 days", is a rule: write it in `ValidateValue`, reading the time from a `TimeProvider`.
- Never `{ get; } = …`: it is assigned with the other static fields, in declaration order, and a well-known instance
  a static field creates before it is checked against the default of the type.
- `string`, `Guid`, `bool` and `[EntityId]` take no bound, and a hook over another type than the underlying one
  bounds nothing: `VO0030`.
- A `public static` `Minimum` or `Maximum` property of the underlying type written without its interface never runs
  as a bound: `VO0011`, unless the type has a validator or a normalizer, which may enforce it itself.

Migrating from the deprecated `Minimum` and `Maximum` options: remove `Minimum = "X"` and add
`public static T Minimum => X;` with the interface; the code, the check and the schema are unchanged, and the
message quotes the bound in the form above, which differs from the option's text for a real, a time or a date and
time.
Keeping both is `VO0029`, and the hook wins until the options are removed.

## Closed value sets

```csharp
[ValueObject<string>(ValueSet = ValueSetKind.Closed, MinLength = 2, MaxLength = 2, SchemaFormat = "iso-3166-alpha2")]
[KnownValue("France", "FR", Description = "France")]
[KnownValue("Belgium", "BE", Description = "Belgium")]
[KnownValue("Luxembourg", "LU", Description = "Luxembourg")]
public readonly partial struct CountryCode : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}
```

Generates `CountryCode.France`, `CountryCode.KnownValues` (an `ImmutableArray<CountryCode>` in declaration
order), a `FrozenSet` membership check rejecting anything else with `value_object.not_a_known_value`, and the
OpenAPI `enum`. This is how reference-data codes are modelled: a C# `enum` can carry neither validation nor a
stable wire format.

Values that cannot appear as an attribute argument (`Guid`, `decimal`, `DateOnly`) are written as text, in the
form of their type ([table](#bounds-and-known-values-written-as-text)), and parsed at compile time (`VO0013`
when that fails). An unusable member name is `VO0006`. On an **open** set, `[KnownValue]` still generates the constants — they are convenience only.

## Arithmetic

```csharp
[ValueObject<decimal>(Arithmetic = true)]
public readonly partial struct Amount : IValueObjectNormalizer<decimal>, IValueObjectMinimum<decimal>
{
    public static decimal Minimum => 0m;

    public static decimal NormalizeValue(decimal value) => decimal.Round(value, 2, MidpointRounding.ToEven);
}
```

Adds `+`, `-`, `*` and `/` by the underlying type, unary `-`, `Zero`, `One`, `IsZero`, `Min` and `Max`, plus
`INumericValueObject<TSelf, TValue>`. Every result goes back through `Create`, so `Amount.Zero - someAmount`
throws rather than producing a negative amount the type forbids. Division of two value objects returns the bare
underlying type: a ratio is not an amount.

## Formatting

```csharp
[ValueObject<string>(MinLength = 15, MaxLength = 34)]
public readonly partial struct Bban : IValueObjectFormatter<string>
{
    public static class Formats
    {
        public const string Electronic = "E";
        public const string Masked = "M";
    }

    public static bool TryFormatValue(
        in string value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        _ = provider;

        if (destination.Length < value.Length)
        {
            charsWritten = 0;
            return false;
        }

        value.CopyTo(destination);
        if (format is "M" or "m")
        {
            destination[2..(value.Length - 4)].Fill('*');
        }

        charsWritten = value.Length;
        return true;
    }
}
```

Declaring the hook takes over formatting **entirely**, including the empty and `null` format specifier, so
handle the default case: `ToString()` and `$"{bban}"` write what the hook writes for it. Return `false` when the
destination is too small, and only then — that is the framework contract, and the generated
`ToString(format, provider)` retries with a pooled buffer twice as large, up to 1,048,576 characters, then throws
`FormatException`; throw it yourself for a format you do not support. For a `string` value object, text equal to the
value returns the string it holds, without allocating. Prefer `IValueObjectFormatter<TValue>`,
which formats without allocating; `IValueObjectStringFormatter<TValue>` exists for rules whose output is
naturally a `string` — its `TryFormat` copies that string — and wins everywhere when both are declared. A hook
formats text for people only: JSON, dictionary keys included, carries the underlying value.

## Domain members you *should* add

The generator owns construction, conversion, equality and text. Everything else is yours:

```csharp
[ValueObject<Guid>]
public readonly partial struct CustomerId : IValueObjectValidator<Guid>
{
    public static CustomerId New() => CreateUnchecked(Guid.CreateVersion7());

    public static ValidationResult ValidateValue(in Guid value)
        => value == Guid.Empty
            ? ValidationResult.Required("A customer identifier must not be empty.")
            : ValidationResult.Success;
}
```

`CreateUnchecked` is legitimate there: the value was just produced by the application itself. It is never
legitimate for input coming from outside.

## Nesting and generic value objects

A value object declared inside another type requires **every** containing type to be `partial` (`VO0009`). The
containing types can be classes, structs, records or interfaces, generic or not, and the value object can be
`private`, `protected` or `private protected`: the generated registration reaches it through the types around it.
Never `file`-local, nor nested in a `file` type: the generated code reopens the type in a file of its own, where a
file-local type is another type (`VO0019`).

A value object can have type parameters of its own, or sit in a generic type. Each construction is then a value
object of its own, `Reference<PurchaseOrder>` and `Reference<SalesInvoice>` included:

```csharp
public sealed class PurchaseOrder;

[ValueObject<string>(MaxLength = 12)]
public readonly partial struct Reference<TOwner> : IValueObjectNormalizer<string>
    where TOwner : class
{
#pragma warning disable CA1000 // A hook is a static member of the generic type.
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
#pragma warning restore CA1000
}
```

The underlying type is never a type parameter: `[ValueObject<T>]` is no attribute C# accepts. A hook is a static
member of a generic type, which CA1000 reports under `AnalysisLevel` `Recommended`; suppress it there. What else
changes is how the integrations meet the constructions:

- The registration registers the generic definition, and `ValueObjectRegistry.TryResolve` describes each construction
  the first time it is asked for it, by reflection. Under native AOT, register each construction a type-driven
  integration needs: `ValueObjectRegistry.Register<Reference<PurchaseOrder>, string>(Reference<PurchaseOrder>.Schema, new Reference<PurchaseOrder>.ValueJsonConverter());`.
- `ConfigureValueObjects()` maps every construction an entity holds.
- Dapper needs a handler per construction, before any query:
  `ValueObjectDapper.AddValueObjectHandler<Reference<PurchaseOrder>, string>();`.
- Never a `private` or `protected` value object, nor one nested in a `private` or `protected` type, inside a generic
  type: the registration reaches it through the types around it, by name (`VO0019`). Make it `internal` or `public`,
  or move it out of the generic type.
- Never a generic `[EntityId]`, nor one in a generic type: its prefix names one type, which every construction would
  claim (`VO0019`).
- A type parameter is never named after a generated member (`Value`, `Create`, …), nor hidden from the value object by
  a nested type, declared or inherited, or a type parameter of the same name between them, nor named after a nested
  type of `TypeConverter` (`StandardValuesCollection`, `SimplePropertyDescriptor`), which the generated converter
  inherits (`VO0019`).

## Personal data

The message of a `ValueObjectException` never quotes the rejected value; its `AttemptedValue` holds it, and a logger
recording the properties of an exception records it in clear. On a value object that holds personal data, classify
the type with an attribute derived from `DataClassificationAttribute` (Microsoft.Extensions.Compliance.Abstractions,
referenced by the consuming project): the generated `Create` and `Parse` then leave `AttemptedValue` `null`.

```csharp
using Microsoft.Extensions.Compliance.Classification;

public static class Taxonomy
{
    public static DataClassification Personal => new("Shop", nameof(Personal));
}

public sealed class PersonalDataAttribute : DataClassificationAttribute
{
    public PersonalDataAttribute() : base(Taxonomy.Personal) { }
}

[PersonalData]
[ValueObject<string>(MinLength = 6, MaxLength = 9)]
public readonly partial struct PassportNumber;
```

Every derived attribute classifies the type, `UnknownDataClassificationAttribute` included;
`NoDataClassificationAttribute` does not. Never quote the value in the message of a validator hook either: the
message is what every log records.

## Consuming a value object

```csharp
[ValueObject<string>(MaxLength = 254, ImplicitConversionToValue = true)]
public readonly partial struct EmailAddress : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();
}

public static class Registrations
{
    public static string? Register(string input)
    {
        // Throws ValueObjectException on a rejected value.
        var confirmed = EmailAddress.Create("Ada@Example.COM ");

        // Never throws, and says which rule fired.
        if (!EmailAddress.TryCreate(input, out var email, out var validation))
        {
            return validation.ErrorCode;      // e.g. "value_object.too_long"
        }

        // Text in, value object out, same rules, same error codes.
        _ = EmailAddress.TryParse(input, provider: null, out _, out _);

        // Value is always normalized and valid; ImplicitConversionToValue makes reading terse.
        string address = email;

        return string.Equals(address, confirmed.Value, StringComparison.Ordinal) ? null : address;
    }
}
```

`IsDefault` is the runtime guard for an instance that crossed a boundary the `VO0010` analyzer cannot see.
