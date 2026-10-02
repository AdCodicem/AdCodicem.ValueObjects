---
title: Diagnostics
sidebar_label: Diagnostics
slug: /reference/diagnostics
description: Every VO diagnostic the generator and the analyzers report, what it means, and how to fix it.
---

# Diagnostics

Every rule the generator and the analyzers enforce. `VO0008`, `VO0011`, `VO0021`, `VO0025` and `VO0026` are
warnings; every other one is an error. There is no `VO0012`.

| Id | Meaning | Fix |
| --- | --- | --- |
| `VO0001` | The type is not `partial`. | Add `partial`: the generated members are added to the same type. |
| `VO0002` | The type is not a `readonly struct`, or is a record or a `ref struct`. | Declare a `readonly partial struct`. A `record struct` is refused on purpose, because `with` and field-wise equality would bypass validation and the declared comparison; a class, because a value object is a value; a `ref struct`, because it can be neither boxed nor a type argument, and the generated members make it both. |
| `VO0003` | Unsupported underlying type. | Use one of the [supported types](../authoring-guide.md#supported-underlying-types). |
| `VO0004` | A bound is not written in the form of its underlying type, names no value of it, or is set on a type that takes none. | Write `Minimum` and `Maximum` as text in the [form of the type](../authoring-guide.md#bounds-and-known-values-written-as-text), such as `"0"`, `"-9.99"`, `"2020-01-01"` or `"08:30"`, with no white space around it; the message names the form. The value must fit the type, so `"300"` is refused for a `byte`, `"-1"` for a `ulong` and `"2023-02-29"` for a date, and a `double` or `float` bound must be a finite number, and zero only when written as zero: `"1e-400"`, too small for a `double`, reads as zero and is refused. A `decimal` takes no exponent. Write a `DateTime` bound without an offset and a `DateTimeOffset` bound with one, and neither as a time alone; [date and time bounds](#date-and-time-bounds) explains why. A `string`, a `Guid` and a `bool` take no bound: constrain a string with `MinLength`, `MaxLength` or `IValueObjectPatternValidator`. |
| `VO0005` | A closed value set declares no value. | Add `[KnownValue]` entries, or drop `ValueSet = ValueSetKind.Closed`: as declared, no value could be valid. |
| `VO0006` | A known value has an unusable name. | The first argument of `[KnownValue]` becomes a static property of the type: give it a valid, unique C# identifier. The message ends with the rule the name broke. A keyword is refused (`class`, `default`, though a contextual keyword such as `value` is accepted). So is a name the generated code already uses: the name of the type, a member the generator writes (`Value`, `Schema`, `Create`, `TryCreate`, `Parse`, `KnownValues`, and `Zero`, `One`, `Min`, `Max` or `Sum` with `Arithmetic`), the metadata name of one of its operators (`op_Equality`, `op_LessThan` and the other comparisons, `op_Addition` and the other arithmetic operators with `Arithmetic`, `op_Implicit` and `op_Explicit` with the conversions), or the name of a property's getter, `get_` followed by the property's name (`get_Value`, `get_Schema`, and `get_France` beside a known value `France`). So is a name the type already has: a member it declares, such as a hook (`NormalizeValue`), a field or a nested class, or a member of `object` (`GetType`, `MemberwiseClone`). So is a name whose getter, `get_` followed by the name, a member the type declares already takes: `France` beside a field, a nested type or a method without parameters named `get_France`, though a `get_France(int)` overload leaves it free. And so is a name another known value already took. [Generated members](./generated-members.md) lists the public ones. |
| `VO0007` | Arithmetic requested on a non-numeric type. | Remove `Arithmetic = true`, or change the underlying type. |
| `VO0008` | Length constraints on a non-string type (warning). | `MinLength` and `MaxLength` apply to `string` only; use `Minimum` and `Maximum` for a number. |
| `VO0009` | A containing type is not `partial`. | Every enclosing type must be `partial`, not only the value object. |
| `VO0010` | An uninitialized value object or entity identifier: `default(T)` or `new T()`. | Construct through `Create`, `TryCreate` or `Parse` (or `New()` for an identifier), and express absence as `T?`. If the zero state is genuinely meaningful, set `AllowDefault = true` on the type, on `[ValueObject<T>]` or `[EntityId]` alike. It is reported where `default` or `new` is written: a parameter declared `Iban iban = default` is reported once, on its declaration, and not at each call that leaves the argument out. |
| `VO0011` | A rule written without its hook interface (warning), on a value object or an entity identifier. | Declare the interface — `IValueObjectNormalizer<T>`, `IValueObjectValidator<T>`, `IValueObjectFormatter<T>`, `IValueObjectStringFormatter<T>`, or `IValueObjectPatternValidator` for a public `static Regex Pattern` on a string value object, which is left alone when it is not public, or when the type implements another hook, since that hook may already run it. Until then the rule never runs. On an `[EntityId]` it reports a validator or a formatter; a normalizer there never runs at all, interface or not, and is `VO0017`'s. |
| `VO0013` | A known value is not written in the form of its underlying type, or is not a value. | A `Guid`, a `decimal` or a `DateOnly` is written as text and converted at compile time: write it in the [form of the type](../authoring-guide.md#bounds-and-known-values-written-as-text), which the message names, as a bound would be. A `Guid` or a `bool` keeps every form it reads, without white space around it. A C# constant such as `200` or `0.5` is held to the same form through its invariant text. A known value is a single value: neither `null`, an array, a `typeof(...)` nor an enum member. |
| `VO0014` | An invalid regular expression in the deprecated `Pattern` option. | Fix it, or better, [move it](#moving-off-pattern) to `IValueObjectPatternValidator`. A verbatim string (`@"^\d+$"`) keeps escapes intact. In a `[GeneratedRegex]`, the regex generator reports an invalid pattern itself. |
| `VO0015` | A malformed entity identifier prefix. | One or more lower-case segments separated by `_`, each starting with a letter: `"acc"`, `"sk_live"`. |
| `VO0016` | Two types claim the same prefix. | Give each identifier type its own prefix, or one kind of identifier would parse as another. |
| `VO0017` | A normalization hook on an entity identifier. | `[EntityId]` normalizes its own format and would never call the hook: remove it. |
| `VO0018` | Both `[EntityId]` and `[ValueObject<T>]` on one type. | Each generates a whole implementation; keep the one that describes the type. |
| `VO0019` | The generated code cannot reopen, reach or name the type. | Nothing is generated for the type, and the message says which rule it broke and what to do. The generated code reopens the type and every type around it by name, so declare it without type parameters, at namespace level or nested in non-generic classes, structs and records, never in an interface. It registers the type from a class of its own, so make it, and every type around it, `internal` or `public`, never `private`, `protected` or `private protected`. It reopens the type in a file of its own, so neither it nor a type around it can be `file`-local. And it writes members on the type, so the type cannot take the name of one of them: `Value`, `Schema`, `Create`, `Parse`, `op_Equality` and the other names [generated members](./generated-members.md) lists, `Zero` with `Arithmetic`, `Prefix` or `New` on an `[EntityId]`. Its statements name the type of every local and discard nothing, so `var` and `_` are names like any other. |
| `VO0020` | `Comparison`, `ValueSet` or `Granularity` holds a value its enum does not define, such as `(StringComparison)42`. | Use one of the enum's named members. Nothing is generated for the type until then, rather than a default nobody chose. |
| `VO0021` | The deprecated `Pattern` option of `[ValueObject<T>]` (warning, reported by the compiler). | Implement `IValueObjectPatternValidator` with `[GeneratedRegex("X", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)] public static partial Regex Pattern { get; }` and remove `Pattern = "X"`. The option builds its regular expression at run time, which native AOT interprets, and is removed in the next major version. [Moving off `Pattern`](#moving-off-pattern) shows the change. |
| `VO0022` | Both the `Pattern` option and `IValueObjectPatternValidator` on one type. | Remove `Pattern = "..."`: the hook replaces it, and wins until you do. |
| `VO0023` | `IValueObjectPatternValidator` on a value object whose underlying type is not `string`. | A pattern only applies to text, so it would never run. Remove the interface, and validate a number or a date in `IValueObjectValidator<T>`. |
| `VO0024` | `IValueObjectPatternValidator` on an `[EntityId]`. | `[EntityId]` validates its format itself and publishes its own OpenAPI pattern. Remove the interface, or drop `[EntityId]` and declare an ordinary value object. Nothing is generated for the type until then. |
| `VO0025` | The `[GeneratedRegex]` behind `Pattern` sets `IgnoreCase`, `Multiline`, `Singleline` or `IgnorePatternWhitespace` (warning). | The OpenAPI `pattern` is the text of the regular expression, which carries no option, so clients would check values differently. Write the rule into the pattern itself: `[A-Za-z]` rather than `IgnoreCase`. |
| `VO0026` | The `[GeneratedRegex]` behind `Pattern` sets no `matchTimeoutMilliseconds` (warning). | Set one, such as `matchTimeoutMilliseconds: 1000`. Without it, a pathological input holds a request thread for as long as the match runs. |
| `VO0027` | `[KnownValue]` on an `[EntityId]`. | An identifier is minted, not chosen from a set, and `[EntityId]` generates no known values: the attribute would be read by no one. Declare a well-known identifier as a static property of the type, `public static AccountId System { get; } = Parse("acc_…", null);`. The identifier still generates. |

`VO0011` deserves its warning more than most. The code it reports compiles and looks right, and in a project
without `TreatWarningsAsErrors` it ships with the rule silently absent. It also recognizes the names hooks had
before they became interfaces — `NormalizeCore`, `ValidateCore`, `TryFormatCore`, `FormatCore` — which should be
renamed to `NormalizeValue`, `ValidateValue`, `TryFormatValue` and `FormatValue`.

## Moving off `Pattern`

The `Pattern` option of `[ValueObject<T>]` is deprecated. It builds its `Regex` at run time with
`RegexOptions.Compiled`, which native AOT cannot honour, so there the expression is interpreted. The compiler
reports each use as `VO0021`, a warning that `TreatWarningsAsErrors` turns into an error, and the option is
removed in the next major version.

A value object declared with the option:

```csharp skip
[ValueObject<string>(MaxLength = 5, Pattern = "^[0-9]{5}$")]
public readonly partial struct PostalCode;
```

moves the expression to `IValueObjectPatternValidator`, as a `[GeneratedRegex]` property the regex source
generator compiles:

```csharp
using System.Text.RegularExpressions;

[ValueObject<string>(MaxLength = 5)]
public readonly partial struct PostalCode : IValueObjectPatternValidator
{
    [GeneratedRegex("^[0-9]{5}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}
```

Those are the options and the timeout the option used, so the behaviour is unchanged: the pattern runs after
`MinLength` and `MaxLength`, before the known values and `ValidateValue`, and a value it does not match is
rejected as `value_object.invalid_format` with the message "The value does not match the expected format." Its
text, read off the attribute when the type compiles, is still the OpenAPI `pattern`. Keep the regular expression
as it was, and add the `using`: `System.Text.RegularExpressions` is not among the implicit usings.

## Date and time bounds

A bound is compiled into the generated code as a fixed instant, so it must mean the same instant on every
machine that builds the project, on every day it is built. The forms of a `DateTime` and a `DateTimeOffset` are
chosen for that, and three kinds of text that would not are refused with `VO0004`:

- A `DateTime` bound written with an offset or `Z`, such as `"2020-01-01T00:00:00+02:00"`. A `DateTime` holds no
  offset, so the instant would have to be converted to some time zone, and the only one at hand is the build
  machine's. Write the clock reading the type holds: `"2020-01-01T00:00:00"`.
- A `DateTimeOffset` bound written without an offset, such as `"2020-01-01"`. It would take the offset of the
  build machine. Write the offset: `"2020-01-01T00:00:00+00:00"` or `"2020-01-01T00:00:00Z"`.
- A time of day written alone on either type, such as `"08:00"` or `"08:00Z"`. It would take the date of the day
  the project is built. Write the date: `"2020-01-01T08:00"`.

The same holds for a known value of either type, refused with `VO0013`.

## Suppressing `VO0010` in a test

A test that must build an uninitialized instance disables the diagnostic on the spot, with the reason:

```csharp skip
#pragma warning disable VO0010 // The guard under test must reject an uninitialized instance.
var missing = default(Iban);
#pragma warning restore VO0010
```
