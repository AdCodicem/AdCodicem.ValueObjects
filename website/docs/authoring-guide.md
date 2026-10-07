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
| `Pattern` | `string?` | none | **Removed**: setting it is a compile error (`VO0021`), and nothing reads it. Implement [`IValueObjectPatternValidator`](#a-pattern) instead. Any minor version may remove the property before 1.0.0. |
| `MinLength`, `MaxLength` | `int` | `-1`, unconstrained | `string` only (`VO0008` otherwise). Validation, OpenAPI `minLength` / `maxLength`, and the EF Core column size. |
| `Minimum`, `Maximum` | `string?` | none | **Removed**: setting either is a compile error (`VO0028`), and nothing reads it. Implement [`IValueObjectMinimum<T>` and `IValueObjectMaximum<T>`](#bounds) instead. Any minor version may remove the properties before 1.0.0. |
| `Comparison` | `StringComparison` | `Ordinal` | `string` only. Drives equality, ordering and hashing together. A value the enum does not define → `VO0020`. |
| `ValueSet` | `ValueSetKind` | `Open` | `Closed` accepts only the [known values](#known-values), through a frozen lookup, and becomes the schema `enum`. Members of a closed set over a reference type are boxed once and shared, so the boxed paths allocate nothing. A value the enum does not define → `VO0020`. |
| `Arithmetic` | `bool` | `false` | Numeric types only (`VO0007` otherwise). Operators and generic math; every result is validated again. |
| `ImplicitConversionToValue` | `bool` | `false` | `string s = iban;` |
| `ExplicitConversionFromValue` | `bool` | `false` | `(Iban)text`, validating like `Create`. |
| `AllowEmpty` | `bool` | `false` | `string` only. Accepts `""`; `null` is still rejected, since absence is `T?`. |
| `AllowDefault` | `bool` | `false` | Silences `VO0010`, and `VO0032` in the code another generator writes, for a type whose zero state is meaningful. |
| `SchemaFormat` | `string?` | the natural format of the type | OpenAPI `format`: `uuid`, `date`, `int64`, or your own such as `iban` or `email`. A `TimeSpan` has none: it is documented with the pattern of its constant form, not as an ISO 8601 `duration`. Nor do a `TimeOnly` and a `DateTime`, written without the offset RFC 3339's `time` and `date-time` require: each is documented with the pattern of the form it is written in. Set `date-time` on a `DateTime` whose normalizer guarantees a kind, such as UTC. |
| `Example` | `string?` | none | **Removed**: setting it is a compile error (`VO0035`), and nothing reads it. Implement [`IValueObjectExample<TSelf>`](#an-example) instead. Any minor version may remove the property before 1.0.0. |
| `Description` | `string?` | the type's XML `<summary>`, as plain text: a `<see cref>` reads as the name it refers to, a `<c>` as its text | OpenAPI description. |

Declared rules, and then the pattern, run before `ValidateValue`, so a validator only ever sees values that
already satisfy them.
[Validation and normalization](./tutorials/validation-and-normalization.md#the-order-things-run-in) gives the
exact order.

## Known values

A known value is a member of the type, marked `[KnownValue]`: a `static readonly` field, or a static property with a
getter alone, of the value object's own type, of any accessibility, initialized through `Known` with the value as its
one argument. `Known` is generated on the type, so the compiler checks the type of the value, and a `Guid`, a
`decimal` or a date is written as itself:

```csharp
[ValueObject<DateOnly>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct FiscalYearStart
{
    /// <summary>The calendar year.</summary>
    [KnownValue]
    public static readonly FiscalYearStart January = Known(new DateOnly(2024, 1, 1));

    [KnownValue(Description = "The UK tax year.")]
    public static FiscalYearStart April { get; } = Known(new DateOnly(2024, 4, 6));
}
```

`Known` normalizes the value and applies every rule of the type but membership, which a known value satisfies by
declaration; it is `private`, and calling it anywhere but in the initializer of a known value is `VO0037`. The
generator reads the members in declaration order, the partial declarations in the order the compiler reads them, and
lists them in `KnownValues`, in the schema, and in the frozen lookup of a closed set, which it builds after them. A
member it cannot read as a known value is `VO0036`, and the message names the rule it breaks.
[Known values](./tutorials/known-values.md) walks through a closed set from declaration to OpenAPI document.

## Declared values the type must accept

The example and every known value are values the type must accept. The example is the OpenAPI example, which
generated clients, mock servers and readers take at its word. A known value is created through `Known` as the type
initializes, which the registration of the assembly does as it loads, so a refused one stops the application.

`VO0031` refuses either at compile time wherever the generator can evaluate the rule on its own, and names the value,
the code and the message of the rule as the type would answer at run time. It reads a value the compiler evaluates
to a constant, `Known("FR")` or an example whose getter returns `Create(42)`, against:

- `MinLength`, `MaxLength`, and an empty string without `AllowEmpty`;
- a bound returned as a constant, `public static int Maximum => 100;`;
- a closed value set, compared under the type's `Comparison`, for the example.

What only runs at run time is left there: a value the compiler does not evaluate, such as `new DateOnly(2024, 1, 31)`,
a pattern, a validator, a bound computed by its hook, the format of an `[EntityId]`, and every rule of a type with a
normalization hook, which may turn a refused value into an accepted one. The
[contract kit](./how-to/test-value-objects.md#what-it-checks) checks the example and every known value at run time.

```csharp
[ValueObject<int>]
public readonly partial struct Quantity : IValueObjectMinimum<int>, IValueObjectMaximum<int>, IValueObjectExample<Quantity>
{
    public static int Minimum => 1;

    public static int Maximum => 100;

    public static Quantity Example => Create(40);
}
```

With `Create(5000)`, the build fails: `The Example '5000' declared on 'Quantity' is refused by its own type
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
| `IValueObjectExample<TSelf>` | `static TSelf Example { get; }` — the OpenAPI example, an instance of the type itself |

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

The hook replaces the `Pattern` option, which built its `Regex` at run time with `RegexOptions.Compiled`. Native
AOT cannot compile a regular expression at run time and interprets it instead. Setting the option is now a compile
error, `VO0021`. To migrate, move the option's text into a `[GeneratedRegex]` with
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

The hooks replace the `Minimum` and `Maximum` options, whose bound was text read under a grammar of its own for each
type. Setting either is now a compile error, `VO0028`.
[Moving off `Minimum` and `Maximum`](./reference/diagnostics.md#moving-off-minimum-and-maximum) shows the change.

### An example

`IValueObjectExample<TSelf>` declares the example the OpenAPI document publishes, as an instance of the type:

```csharp
[ValueObject<string>(MinLength = 3, MaxLength = 3)]
public readonly partial struct CurrencyCode : IValueObjectExample<CurrencyCode>
{
    public static CurrencyCode Example => Create("EUR");
}
```

The schema reads it once, as the type initializes, and publishes its underlying value as the JSON converter writes
it. It is created through the type's own rules, so it cannot be a value the type refuses; one that is a constant is
checked at compile time as well ([declared values](#declared-values-the-type-must-accept)). A known value serves as
well, `public static CountryCode Example => France;`. Without the hook, a value object publishes no example, and an
`[EntityId]` one of the right shape, derived from its prefix.

The hook is over the value object itself: `IValueObjectExample<Other>` is `VO0038`, since its example would be
published nowhere. A public static `Example` property of the type written without the interface is `VO0011`. The hook
replaces the `Example` option of `[ValueObject<T>]` and `[EntityId]`, which held the example as text: setting it is
now a compile error, `VO0035`.

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
[Dapper](./how-to/dapper.md#generic-value-objects) needs a handler per construction, before any query.
[MongoDB.Driver](./how-to/mongodb.md#generic-value-objects-and-value-objects-written-by-hand) describes a construction
by reflection the first time it meets it, or takes one registered with `ValueObjectBson.Register<TSelf, TValue>()`.
Under native AOT, where describing a construction by reflection is out of reach, register each construction a
type-driven integration needs; the registration reads its schema off the type:

```csharp skip
ValueObjectRegistry.Register<Reference<PurchaseOrder>, string>(
    static () => new Reference<PurchaseOrder>.ValueJsonConverter());
```

An `[EntityId]` is never generic, nor nested in a generic type: its prefix names one type, which every construction
would claim (`VO0019`). Neither is a type parameter named after a member the generator writes, such as `Value`, nor
one of a type around the value object hidden from it by a nested type, declared or inherited, or a type parameter of
the same name, nor one named after a nested type of `TypeConverter`, such as `StandardValuesCollection`, which the
generated converter inherits.

## Diagnostics

The generator, the analyzers and the compiler report `VO0001` to `VO0038`. [Diagnostics](./reference/diagnostics.md)
lists each one with its fix.

Next: [Generated members](./reference/generated-members.md), for what the generator writes from all of this.
