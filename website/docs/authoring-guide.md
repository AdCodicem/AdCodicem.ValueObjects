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
| `Pattern` | `string?` | none | **Deprecated** (`VO0021`), and removed in the next major version: implement [`IValueObjectPatternValidator`](#a-pattern) instead. Regular expression the **normalized** value must match, built at run time. Also the OpenAPI `pattern`. Read as written, white space included; an empty one is absent. Invalid → `VO0014`. |
| `MinLength`, `MaxLength` | `int` | `-1`, unconstrained | `string` only (`VO0008` otherwise). Validation, OpenAPI `minLength` / `maxLength`, and the EF Core column size. |
| `Minimum`, `Maximum` | `string?` | none | **Deprecated** (`VO0028`), and removed in the next major version: implement [`IValueObjectMinimum<T>` and `IValueObjectMaximum<T>`](#bounds) instead. Inclusive bounds written as **text**, in the [one form of the underlying type](#bounds-and-known-values-written-as-text), so `decimal`, `DateOnly` and `TimeSpan` keep full precision. Numbers, `char`, dates, times and durations only: a bound on `string`, `Guid` or `bool` → `VO0004`. Parsed at compile time; any other text, or a value outside the type → `VO0004`. Also OpenAPI `minimum` / `maximum`, or `x-minimum` / `x-maximum` and a sentence of the description for a value written as a JSON string. |
| `Comparison` | `StringComparison` | `Ordinal` | `string` only. Drives equality, ordering and hashing together. A value the enum does not define → `VO0020`. |
| `ValueSet` | `ValueSetKind` | `Open` | `Closed` accepts only the declared `[KnownValue]`s, through a frozen lookup, and becomes the schema `enum`. Members of a closed set over a reference type are boxed once and shared, so the boxed paths allocate nothing. A value the enum does not define → `VO0020`. |
| `Arithmetic` | `bool` | `false` | Numeric types only (`VO0007` otherwise). Operators and generic math; every result is validated again. |
| `ImplicitConversionToValue` | `bool` | `false` | `string s = iban;` |
| `ExplicitConversionFromValue` | `bool` | `false` | `(Iban)text`, validating like `Create`. |
| `AllowEmpty` | `bool` | `false` | `string` only. Accepts `""`; `null` is still rejected, since absence is `T?`. |
| `AllowDefault` | `bool` | `false` | Silences `VO0010`, for a type whose zero state is meaningful. |
| `SchemaFormat` | `string?` | the natural format of the type | OpenAPI `format`: `uuid`, `date`, `int64`, or your own such as `iban` or `email`. A `TimeSpan` has none: it is documented with the pattern of its constant form, not as an ISO 8601 `duration`. |
| `Example` | `string?` | none | OpenAPI example, written as text the type parses in the invariant culture. A value the type's own rules refuse → `VO0031`: see [declared values](#declared-values-the-type-must-accept). |
| `Description` | `string?` | the type's XML `<summary>`, as plain text: a `<see cref>` reads as the name it refers to, a `<c>` as its text | OpenAPI description. |

Declared rules, and then the pattern, run before `ValidateValue`, so a validator only ever sees values that
already satisfy them.
[Validation and normalization](./tutorials/validation-and-normalization.md#the-order-things-run-in) gives the
exact order.

## Bounds and known values written as text

An attribute argument can only be a constant of a few types, so the deprecated `Minimum` and `Maximum` options and
the known value of a `Guid`, a `decimal` or a date are written as text and read at compile time. Each underlying type reads **one
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

## Declared values the type must accept

The example and every known value are values the type must accept. The example is the OpenAPI example, which
generated clients, mock servers and readers take at its word. A known value is created through `Create` as the type
initializes, which the registration of the assembly does before `Main`, so a refused one stops the application.

`VO0031` refuses either at compile time wherever the generator can evaluate the rule on its own, and names the value,
the code and the message of the rule as the type would answer at run time:

- an example no form of the underlying type reads, such as `"lots"` on an `int`;
- `MinLength`, `MaxLength`, and an empty string without `AllowEmpty`;
- a bound returned as a constant, `public static int Maximum => 100;`, or set through the deprecated options;
- a closed value set, compared under the type's `Comparison`.

An example is held to these rules when it is written in the [form of a known value](#bounds-and-known-values-written-as-text).
Written in another form the type parses, such as `NaN` or a date and time with `Z`, it is left to run time. So is
what only runs there: a pattern, a validator, a bound computed by its hook, the format of an `[EntityId]`, and every
rule of a type with a normalization hook, which may turn a refused value into an accepted one. The
[contract kit](./how-to/test-value-objects.md#what-it-checks) checks the example and every known value at run time.

```csharp
[ValueObject<int>(Example = "40")]
public readonly partial struct Quantity : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    public static int Minimum => 1;

    public static int Maximum => 100;
}
```

With `Example = "5000"`, the build fails: `The Example '5000' declared on 'Quantity' is refused by its own type
(value_object.out_of_range): The value must be less than or equal to 100.`

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
| `IValueObjectMinimum<TValue>` | `static TValue Minimum { get; }` — numbers, `char`, dates, times and durations |
| `IValueObjectMaximum<TValue>` | `static TValue Maximum { get; }` — numbers, `char`, dates, times and durations |
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

### Bounds

`IValueObjectMinimum<TValue>` and `IValueObjectMaximum<TValue>` declare the inclusive bounds of a value object as
values of its underlying type, so the compiler checks their type and any expression of that type can build them:

```csharp
[ValueObject<DateOnly>]
public readonly partial struct BirthDate : IValueObjectMinimum<DateOnly>, IValueObjectMaximum<DateOnly>
{
    public static DateOnly Minimum => new(1900, 1, 1);

    public static DateOnly Maximum => new(2100, 12, 31);
}
```

The bounds run on the normalized value, after the pattern and before the known values and `ValidateValue`. A
value outside them is rejected as `value_object.out_of_range`, with the message "The value must be greater than
or equal to 1900-01-01." A `double` or a `float` bound also rejects `NaN`, which compares false with everything.
The rule is declared once: the bounds are the OpenAPI `minimum` and `maximum`, or, for a type JSON writes as a
string, the `x-minimum` and `x-maximum` extensions and a sentence of the description, written as the JSON
converter writes the type.

A bound is a constant. The check reads it each time it runs, which costs nothing for a constant, folded into the
comparison by the JIT, and the schema reads it once, when the type initializes, which the generated registration
does as the assembly loads. A bound that changed would therefore be checked against a value the schema does not
publish, and a bound must neither throw nor read the state of the application. A bound relative to the clock, such
as "not in the future" or "within 90 days", is a rule rather than a bound: implement it in
`IValueObjectValidator<T>`.

Write a bound as an expression-bodied property, as above. An initialized property, `{ get; } = ...`, is assigned
with the type's other static fields, in the order they are declared, so an instance a static field declared before
it creates, `public static readonly BirthDate Earliest = Create(new(1900, 1, 1));`, would be checked against the
default of the type.

The message quotes the bound in one invariant form per type: as written for an integer, a `decimal`, a `char`, a
`DateOnly` or a `TimeSpan`, and in its round-trip form for a real (`1E-05`), a time (`06:00:00.0000000`) or a date
and time, a `DateTime` without its kind, since the check compares clock readings.

The hooks apply to numbers, `char`, dates, times and durations, and over the underlying type itself. Over a
`string`, a `Guid`, a `bool` or an `[EntityId]`, or over another type, `IValueObjectMinimum<int>` on a
`[ValueObject<long>]`, they are `VO0030`. A public static `Minimum` or `Maximum` of the underlying type written
without its interface is `VO0011`, unless the type implements `IValueObjectValidator<T>`, which may already check
it.

The hooks replace the `Minimum` and `Maximum` options, whose bound is text read under a grammar of its own for each
type. Declaring an option and its hook on one type is `VO0029`, and the hook wins.
[Moving off `Minimum` and `Maximum`](./reference/diagnostics.md#moving-off-minimum-and-maximum) shows the change.

## Where a value object can be declared

At namespace level, or nested in any class, struct, record or interface, generic or not, with every type around it
`partial` (`VO0009`). The value object, or a type around it, can be `private`, `protected` or `private protected`: the
generated registration reaches it through a step nested in each type around it, down to the one that sees it, without
running their static constructors. Only a `file`-local type is out of reach, since the generated code reopens the type
in a file of its own (`VO0019`), and so is a `private` or `protected` type inside a generic one, whose step the
registration could only reach through a construction.

A value object can have type parameters of its own, or sit in a generic type. Each construction is then a value object
of its own, with its own rules, converters and registry entry:

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

The underlying type is fixed, since C# accepts no type parameter as an attribute's type argument. A hook is a static
member of the generic type, which the CA1000 analyzer reports at the `Recommended` analysis level; the
generic type is the point of the declaration, so suppress it there.

The registration knows none of the constructions an application will use, so it registers the generic definition, and
the registry describes each construction the first time it is asked for it, by reflection: the JSON converter
factory, the OpenAPI transformer and the model binder find one as they find any value object.
[Entity Framework Core](./how-to/ef-core.md#generic-value-objects) maps each construction a property holds, and
[Dapper](./how-to/dapper.md#generic-value-objects) needs a handler per construction, before any query. Under native
AOT, where describing a construction by reflection is out of reach, register each construction a type-driven
integration needs:

```csharp skip
ValueObjectRegistry.Register<Reference<PurchaseOrder>, string>(
    Reference<PurchaseOrder>.Schema,
    new Reference<PurchaseOrder>.ValueJsonConverter());
```

An `[EntityId]` is never generic, nor nested in a generic type: its prefix names one type, which every construction
would claim (`VO0019`). Neither is a type parameter named after a member the generator writes, such as `Value`, nor
one of a type around the value object hidden from it by a nested type, declared or inherited, or a type parameter of
the same name, nor one named after a nested type of `TypeConverter`, such as `StandardValuesCollection`, which the
generated converter inherits.

## Diagnostics

The generator and the analyzers report `VO0001` to `VO0030`. [Diagnostics](./reference/diagnostics.md) lists
each one with its fix.

Next: [Generated members](./reference/generated-members.md), for what the generator writes from all of this.
