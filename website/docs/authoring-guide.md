---
title: Authoring Reference
sidebar_label: Authoring reference
slug: /authoring-guide
description: The supported underlying types, every option of [ValueObject<T>] with its default, and the hook interfaces a value object implements.
---

# Authoring reference

## Supported underlying types

`string`, `Guid`, `bool`, `char`, every built-in integer (including `Int128` and `UInt128`, which travel as
JSON strings), `decimal`, `double`, `float`, `DateOnly`, `TimeOnly`, `DateTime`, `DateTimeOffset`, `TimeSpan`.

## Declarative options on `[ValueObject<T>]`

| Option | Type | Default | Effect |
| --- | --- | --- | --- |
| `Pattern` | `string?` | none | **Deprecated** (`VO0021`), and removed in the next major version: implement [`IValueObjectPatternValidator`](#a-pattern) instead. Regular expression the **normalized** value must match, built at run time. Also the OpenAPI `pattern`. Invalid → `VO0014`. |
| `MinLength`, `MaxLength` | `int` | `-1`, unconstrained | `string` only (`VO0008` otherwise). Validation, OpenAPI `minLength` / `maxLength`, and the EF Core column size. |
| `Minimum`, `Maximum` | `string?` | none | Inclusive bounds written as **text**, in the [one form of the underlying type](#bounds-and-known-values-written-as-text), so `decimal`, `DateOnly` and `TimeSpan` keep full precision. Numbers, `char`, dates, times and durations only: a bound on `string`, `Guid` or `bool` → `VO0004`. Parsed at compile time; any other text, or a value outside the type → `VO0004`. Also OpenAPI `minimum` / `maximum`, or `x-minimum` / `x-maximum` and a sentence of the description for a value written as a JSON string. |
| `Comparison` | `StringComparison` | `Ordinal` | `string` only. Drives equality, ordering and hashing together. A value the enum does not define → `VO0020`. |
| `ValueSet` | `ValueSetKind` | `Open` | `Closed` accepts only the declared `[KnownValue]`s, through a frozen lookup, and becomes the schema `enum`. Members of a closed set over a reference type are boxed once and shared, so the boxed paths allocate nothing. A value the enum does not define → `VO0020`. |
| `Arithmetic` | `bool` | `false` | Numeric types only (`VO0007` otherwise). Operators and generic math; every result is validated again. |
| `ImplicitConversionToValue` | `bool` | `false` | `string s = iban;` |
| `ExplicitConversionFromValue` | `bool` | `false` | `(Iban)text`, validating like `Create`. |
| `AllowEmpty` | `bool` | `false` | `string` only. Accepts `""`; `null` is still rejected, since absence is `T?`. |
| `AllowDefault` | `bool` | `false` | Silences `VO0010`, for a type whose zero state is meaningful. |
| `SchemaFormat` | `string?` | the natural format of the type | OpenAPI `format`: `uuid`, `date`, `int64`, or your own such as `iban` or `email`. |
| `Example` | `string?` | none | OpenAPI example. |
| `Description` | `string?` | the type's XML `<summary>` | OpenAPI description. |

Declared rules, and then the pattern, run before `ValidateValue`, so a validator only ever sees values that
already satisfy them.
[Validation and normalization](./tutorials/validation-and-normalization.md#the-order-things-run-in) gives the
exact order.

## Bounds and known values written as text

An attribute argument can only be a constant of a few types, so `Minimum`, `Maximum` and the known value of a
`Guid`, a `decimal` or a date are written as text and read at compile time. Each underlying type reads **one
form**, and no other: no culture, no time zone, no white space around the value. The same declaration then
compiles to the same value on every machine. Any other text is `VO0004` for a bound and `VO0013` for a known
value, and the message names the form the type expects.

| Underlying type | Form | Example |
| --- | --- | --- |
| `sbyte`, `short`, `int`, `long`, `Int128` | Digits, with `-` in front when negative. | `"-42"` |
| `byte`, `ushort`, `uint`, `ulong`, `UInt128` | Digits alone. | `"42"` |
| `decimal` | Digits, an optional `-` in front, an optional fraction after `.`. No exponent. | `"-19.99"` |
| `double`, `float` | As `decimal`, plus an optional exponent: `e` or `E`, an optional sign, digits. A finite value, and zero only when written as zero. | `"9.1e-31"` |
| `char` | Exactly one character. | `"A"` |
| `DateOnly` | `yyyy-MM-dd` | `"2024-01-31"` |
| `TimeOnly` | `HH:mm`, `HH:mm:ss` or `HH:mm:ss.fffffff`, with one to seven digits of fraction. | `"08:30"` |
| `DateTime` | `yyyy-MM-dd`, or `yyyy-MM-ddTHH:mm` with optional seconds and fraction. Never an offset or `Z`. | `"2024-01-31T08:30"` |
| `DateTimeOffset` | `yyyy-MM-ddTHH:mm` with optional seconds and fraction, then `Z`, `+HH:mm` or `-HH:mm`, always. | `"2024-01-31T08:30+01:00"` |
| `TimeSpan` | `[-][d.]hh:mm:ss[.fffffff]`, the invariant constant format `"c"`. | `"1.12:00:00"` |
| `string` | Any text. Known values only. | `"FR"` |
| `Guid` | Any form `Guid.Parse` reads. Known values only. | `"6f9619ff-8b86-d011-b42d-00c04fc964ff"` |
| `bool` | `true` or `false`, in any case. Known values only. | `"true"` |

The value must also exist in the type: `"300"` is no `byte`, `"2023-02-29"` no date, `"25:00"` no time of day,
and `"1e-400"`, which reads as zero, no `double`.
A time of day written alone is neither a `DateTime` nor a `DateTimeOffset`, since it would take the date of the
day the project is built; [date and time bounds](./reference/diagnostics.md#date-and-time-bounds) explains the
rest. A `string`, a `Guid` and a `bool` have no order, and take no bound.

A known value may also be a C# constant, such as `200`, `0.5`, `'A'` or `true`. It is held to the same form
through its invariant text, a `double` or a `float` in its round-trip form, so `[KnownValue("Ok", 200)]` suits a
`short` and `[KnownValue("Half", 0.5)]` a `decimal`. A `typeof(...)`, an enum member, an array and `null` are not
values, and are `VO0013`.

## Hooks

A value object declares a rule by implementing an interface, so the compiler checks the signature: a
mis-typed rule fails the build instead of being silently ignored. All are optional, and `VO0011` reports a
rule written without its interface — the one mistake the compiler cannot catch.

| Interface | Member |
| --- | --- |
| `IValueObjectNormalizer<TValue>` | `static TValue NormalizeValue(TValue value)` |
| `IValueObjectSpanNormalizer` | `static string NormalizeValue(ReadOnlySpan<char> value)` — string value objects only |
| `IValueObjectPatternValidator` | `static Regex Pattern { get; }`, written as a `[GeneratedRegex]` partial property — string value objects only |
| `IValueObjectValidator<TValue>` | `static ValidationResult ValidateValue(in TValue value)` |
| `IValueObjectFormatter<TValue>` | `static bool TryFormatValue(in TValue value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)` |
| `IValueObjectStringFormatter<TValue>` | `static string FormatValue(in TValue value, ReadOnlySpan<char> format, IFormatProvider? provider)` |

`NormalizeValue` must be idempotent and must not reject: an unnormalizable value is rejected by
`ValidateValue`. A formatting hook, when present, takes over formatting entirely, including the default format:
`ToString()`, `ToString(format, provider)`, `TryFormat` and interpolation all write what it writes. When a type
declares both, `FormatValue` answers everywhere and `TryFormatValue` is never called. Formatting stops at text for
people: JSON, dictionary keys included, and the database carry the underlying value.

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

The rules are public because a static interface member cannot be anything else. `Normalize` remains the
member callers use: it guards against a null underlying value and then defers to `NormalizeValue`.

### A pattern

`IValueObjectPatternValidator` declares the regular expression a string value object must match. Its member is a
`[GeneratedRegex]` partial property, which the .NET regex source generator compiles. The value object generator
cannot write one itself, because a source generator never sees another's output, so the hook asks for the line
of code a person writes:

```csharp
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects;
using AdCodicem.ValueObjects.Annotations;

namespace Catalog;

[ValueObject<string>(MaxLength = 12)]
public readonly partial struct Sku : IValueObjectNormalizer<string>, IValueObjectPatternValidator
{
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();

    [GeneratedRegex("^[A-Z]{3}-[0-9]{4,8}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}
```

The pattern runs on the normalized value, after `MinLength` and `MaxLength` and before the known values and
`ValidateValue`. A value it does not match is rejected as `value_object.invalid_format`, with the message "The
value does not match the expected format." The rule is still declared once: the generator reads the pattern's
text off the `[GeneratedRegex]` attribute when the type compiles, and publishes it as the OpenAPI `pattern`.

That text carries no `RegexOptions`, so a client checking the schema would not apply them. `IgnoreCase`,
`Multiline`, `Singleline` and `IgnorePatternWhitespace` are therefore `VO0025`, a warning: write the rule into
the pattern itself, `[A-Za-z]` rather than `IgnoreCase`. Set a `matchTimeoutMilliseconds` as well, or `VO0026`
warns that a pathological input could hold a request thread for as long as the match runs.

The hook applies to `string` value objects only: on any other underlying type it is `VO0023`, and on an
`[EntityId]`, which owns its format, `VO0024`. A public static `Regex Pattern` written without the interface is
`VO0011`, unless the type implements another hook, which may already run it.

The hook replaces the `Pattern` option, which builds its `Regex` at run time with `RegexOptions.Compiled`. Native
AOT cannot compile a regular expression at run time and interprets it instead. Declaring both on one type is
`VO0022`, and the hook wins. To migrate, move the option's text into a `[GeneratedRegex]` with
`RegexOptions.CultureInvariant` and `matchTimeoutMilliseconds: 1000`: those are the options and the timeout the
option used, so behaviour does not change. [Moving off `Pattern`](./reference/diagnostics.md#moving-off-pattern)
shows the change.

## Diagnostics

The generator and the analyzers report `VO0001` to `VO0026`. [Diagnostics](./reference/diagnostics.md) lists
each one with its fix.

Next: [Generated members](./reference/generated-members.md), for what the generator writes from all of this.
