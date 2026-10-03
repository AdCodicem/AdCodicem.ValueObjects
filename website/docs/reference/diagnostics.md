---
title: Diagnostics
sidebar_label: Diagnostics
slug: /reference/diagnostics
description: Every VO diagnostic the generator and the analyzers report, what it means, and how to fix it.
---

# Diagnostics

Every rule the generator and the analyzers enforce. `VO0008`, `VO0011`, `VO0021`, `VO0025`, `VO0026` and
`VO0028` are warnings; every other one is an error. There is no `VO0012`.

| Id | Meaning | Fix |
| --- | --- | --- |
| `VO0001` | The type is not `partial`. | Add `partial`: the generated members are added to the same type. |
| `VO0002` | The type is not a `readonly struct`, or is a record or a `ref struct`. | Declare a `readonly partial struct`. A `record struct` is refused on purpose, because `with` and field-wise equality would bypass validation and the declared comparison; a class, because a value object is a value; a `ref struct`, because it can be neither boxed nor a type argument, and the generated members make it both. |
| `VO0003` | Unsupported underlying type. | Use one of the [supported types](../authoring-guide.md#supported-underlying-types). |
| `VO0004` | A bound set through the deprecated `Minimum` or `Maximum` option is not written in the form of its underlying type, names no value of it, or is set on a type that takes none. | [Move the bound](#moving-off-minimum-and-maximum) to `IValueObjectMinimum<T>` or `IValueObjectMaximum<T>`, whose type the compiler checks. Until then, write `Minimum` and `Maximum` as text in the [form of the type](../authoring-guide.md#bounds-and-known-values-written-as-text), such as `"0"`, `"-9.99"`, `"2020-01-01"` or `"08:30"`, with no white space around it; the message names the form. The value must fit the type, so `"300"` is refused for a `byte`, `"-1"` for a `ulong` and `"2023-02-29"` for a date, and a `double` or `float` bound must be a finite number, and zero only when written as zero: `"1e-400"`, too small for a `double`, reads as zero and is refused. A `decimal` takes no exponent. Write a `DateTime` bound without an offset and a `DateTimeOffset` bound with one, and neither as a time alone; [date and time bounds](#date-and-time-bounds) explains why. A `string`, a `Guid` and a `bool` take no bound: constrain a string with `MinLength`, `MaxLength` or `IValueObjectPatternValidator`. |
| `VO0005` | A closed value set declares no value. | Add `[KnownValue]` entries, or drop `ValueSet = ValueSetKind.Closed`: as declared, no value could be valid. |
| `VO0006` | A known value has an unusable name. | The first argument of `[KnownValue]` becomes a static property of the type: give it a valid, unique C# identifier. The message ends with the rule the name broke. A keyword is refused (`class`, `default`, though a contextual keyword such as `value` is accepted). So is a name the generated code already uses: the name of the type, a member the generator writes (`Value`, `Schema`, `Create`, `TryCreate`, `Parse`, `KnownValues`, and `Zero`, `One`, `Min`, `Max` or `Sum` with `Arithmetic`), the metadata name of one of its operators (`op_Equality`, `op_LessThan` and the other comparisons, `op_Addition` and the other arithmetic operators with `Arithmetic`, `op_Implicit` and `op_Explicit` with the conversions), or the name of a property's getter, `get_` followed by the property's name (`get_Value`, `get_Schema`, and `get_France` beside a known value `France`). So is a name the type already has: a member it declares, such as a hook (`NormalizeValue`), a field or a nested class, or a member of `object` (`GetType`, `MemberwiseClone`). So is a name whose getter, `get_` followed by the name, a member the type declares already takes: `France` beside a field, a nested type or a method without parameters named `get_France`, though a `get_France(int)` overload leaves it free. So is the name of one of its type parameters (`T` on `Label<T>`), or a name whose getter a type parameter takes (`Foo` on `Tag<get_Foo>`). And so is a name another known value already took. [Generated members](./generated-members.md) lists the public ones. |
| `VO0007` | Arithmetic requested on a non-numeric type. | Remove `Arithmetic = true`, or change the underlying type. |
| `VO0008` | Length constraints on a non-string type (warning). | `MinLength` and `MaxLength` apply to `string` only; bound a number through `IValueObjectMinimum<T>` and `IValueObjectMaximum<T>`. |
| `VO0009` | A containing type is not `partial`. | Every enclosing type must be `partial`, not only the value object. |
| `VO0010` | An uninitialized value object or entity identifier: `default(T)` or `new T()`. | Construct through `Create`, `TryCreate` or `Parse` (or `New()` for an identifier), and express absence as `T?`. If the zero state is genuinely meaningful, set `AllowDefault = true` on the type, on `[ValueObject<T>]` or `[EntityId]` alike. It is reported where `default` or `new` is written: a parameter declared `Iban iban = default` is reported once, on its declaration, and not at each call that leaves the argument out. |
| `VO0011` | A rule written without its hook interface (warning), on a value object or an entity identifier. | Declare the interface — `IValueObjectNormalizer<T>`, `IValueObjectValidator<T>`, `IValueObjectFormatter<T>`, `IValueObjectStringFormatter<T>`, `IValueObjectPatternValidator` for a public `static Regex Pattern` on a string value object, which is left alone when it is not public, or when the type implements another hook, since that hook may already run it, or `IValueObjectMinimum<T>` and `IValueObjectMaximum<T>` for a public static `Minimum` or `Maximum` property of the underlying type on a value object that takes a bound, which is left alone when the type implements `IValueObjectValidator<T>` or `IValueObjectNormalizer<T>`, since the validator may already check it and the normalizer clamp to it, or still sets the deprecated option, which `VO0028` reports. A field is left alone too: it could not implement the hook. Until then the rule never runs. On an `[EntityId]` it reports a validator or a formatter; a normalizer there never runs at all, interface or not, and is `VO0017`'s. |
| `VO0013` | A known value is not written in the form of its underlying type, or is not a value. | A `Guid`, a `decimal` or a `DateOnly` is written as text and converted at compile time: write it in the [form of the type](../authoring-guide.md#bounds-and-known-values-written-as-text), which the message names, as a bound would be. A `Guid` or a `bool` keeps every form it reads, without white space around it. A C# constant such as `200` or `0.5` is held to the same form through its invariant text. A known value is a single value: neither `null`, an array, a `typeof(...)` nor an enum member. |
| `VO0014` | An invalid regular expression in the deprecated `Pattern` option. | Fix it, or better, [move it](#moving-off-pattern) to `IValueObjectPatternValidator`. A verbatim string (`@"^\d+$"`) keeps escapes intact. In a `[GeneratedRegex]`, the regex generator reports an invalid pattern itself. |
| `VO0015` | A malformed entity identifier prefix. | One or more lower-case segments separated by `_`, each starting with a letter: `"acc"`, `"sk_live"`. |
| `VO0016` | Two types claim the same prefix. | Give each identifier type its own prefix, or one kind of identifier would parse as another. |
| `VO0017` | A normalization hook on an entity identifier. | `[EntityId]` normalizes its own format and would never call the hook: remove it. |
| `VO0018` | Both `[EntityId]` and `[ValueObject<T>]` on one type. | Each generates a whole implementation; keep the one that describes the type. |
| `VO0019` | The generated code cannot reopen, reach or name the type. | Nothing is generated for the type, and the message says which rule it broke and what to do. The generated code reopens the type in a file of its own, so neither it nor a type around it can be `file`-local. It reaches a `private`, `protected` or `private protected` type through a step nested in each type around it, called by name, so no such type can sit inside a generic type: make it `internal` or `public`, or move it out. An `[EntityId]` takes no type parameters and sits in no generic type, since its prefix names one type, which every construction would claim. The generated code names the types around the value object through their type parameters, from inside it, and from inside the converters it nests in it, so a nested type, declared or inherited, or a type parameter of the same name between them is refused, and so is a type parameter named after a nested type of `TypeConverter`, and it writes members on the type, so neither the type nor one of its type parameters can take the name of one of them: `Value`, `Schema`, `Create`, `Parse`, `op_Equality` and the other names [generated members](./generated-members.md) lists, `Zero` with `Arithmetic`, `Prefix` or `New` on an `[EntityId]`. Its statements name the type of every local and discard nothing, so `var` and `_` are names like any other. [Where a value object can be declared](../authoring-guide.md#where-a-value-object-can-be-declared) lists what is supported. |
| `VO0020` | `Comparison`, `ValueSet` or `Granularity` holds a value its enum does not define, such as `(StringComparison)42`. | Use one of the enum's named members. Nothing is generated for the type until then, rather than a default nobody chose. |
| `VO0021` | The deprecated `Pattern` option of `[ValueObject<T>]` (warning, reported by the compiler). | Implement `IValueObjectPatternValidator` with `[GeneratedRegex("X", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)] public static partial Regex Pattern { get; }` and remove `Pattern = "X"`. The option builds its regular expression at run time, which native AOT interprets, and is removed in the next major version. [Moving off `Pattern`](#moving-off-pattern) shows the change. |
| `VO0022` | Both the `Pattern` option and `IValueObjectPatternValidator` on one type. | Remove `Pattern = "..."`: the hook replaces it, and wins until you do. |
| `VO0023` | `IValueObjectPatternValidator` on a value object whose underlying type is not `string`. | A pattern only applies to text, so it would never run. Remove the interface, and validate a number or a date in `IValueObjectValidator<T>`. |
| `VO0024` | `IValueObjectPatternValidator` on an `[EntityId]`. | `[EntityId]` validates its format itself and publishes its own OpenAPI pattern. Remove the interface, or drop `[EntityId]` and declare an ordinary value object. Nothing is generated for the type until then. |
| `VO0025` | The `[GeneratedRegex]` behind `Pattern` sets `IgnoreCase`, `Multiline`, `Singleline` or `IgnorePatternWhitespace` (warning). | The OpenAPI `pattern` is the text of the regular expression, which carries no option, so clients would check values differently. Write the rule into the pattern itself: `[A-Za-z]` rather than `IgnoreCase`. |
| `VO0026` | The `[GeneratedRegex]` behind `Pattern` sets no `matchTimeoutMilliseconds` (warning). | Set one, such as `matchTimeoutMilliseconds: 1000`. Without it, a pathological input holds a request thread for as long as the match runs. |
| `VO0027` | `[KnownValue]` on an `[EntityId]`. | An identifier is minted, not chosen from a set, and `[EntityId]` generates no known values: the attribute would be read by no one. Declare a well-known identifier as a static property of the type, `public static AccountId System { get; } = Parse("acc_…", null);`. The identifier still generates. |
| `VO0028` | The deprecated `Minimum` or `Maximum` option of `[ValueObject<T>]` (warning, reported by the compiler). | Implement `IValueObjectMinimum<T>` with `public static T Minimum => …;`, or `IValueObjectMaximum<T>` with `Maximum`, and remove `Minimum = "…"`. The bound becomes a value of the underlying type, which the compiler checks, rather than text read under a grammar of its own for each type. The options are removed in the next major version. [Moving off `Minimum` and `Maximum`](#moving-off-minimum-and-maximum) shows the change. |
| `VO0029` | Both the `Minimum` (or `Maximum`) option and its hook on one type. | Remove `Minimum = "…"` and keep the static property: the hook replaces the option, and wins until you do. |
| `VO0030` | `IValueObjectMinimum<T>` or `IValueObjectMaximum<T>` that cannot bound the type. | A `string`, a `Guid`, a `bool` and an `[EntityId]` take no bound, and a hook over another type than the underlying one, `IValueObjectMinimum<int>` on a `[ValueObject<long>]`, would never be checked. Constrain a string with `MinLength`, `MaxLength` or `IValueObjectPatternValidator`; check a `Guid` or a `bool` in `IValueObjectValidator<T>`, or remove the hook; an identifier's format is fixed, and anything more goes in `IValueObjectValidator<T>`. Otherwise implement the hook over the underlying type itself. The type still generates, without the bound. |
| `VO0031` | The `Example` or a known value declared on the type is one its own rules refuse. | Fix the value, or the rule. The example is the OpenAPI example, which generated clients, mock servers and readers take at its word, and a refused known value throws from the type initializer, which the registration of the assembly runs before `Main`: the application would not start. The message names the value, then the code and the message of the rule it breaks, as the type would answer at run time. The generator checks what it can evaluate on its own: an example no form of the underlying type reads, such as `"lots"` on an `int` or `"12,5"` on a `decimal`; `MinLength`, `MaxLength`, and an empty string without `AllowEmpty`; a bound returned as a constant, `public static int Maximum => 100;` written as a getter, an explicit implementation or a constant field alike, or set through the deprecated options; and a closed value set, compared under the type's `Comparison`, except under the current culture, which is the application's. An example is held to these rules when written in the [form of a known value](../authoring-guide.md#bounds-and-known-values-written-as-text), and left alone in any other form the type parses, such as `NaN` or a date and time with `Z`. What runs only at run time is left to the [contract kit](../how-to/test-value-objects.md#what-it-checks), which checks the example and every known value: a pattern, a validator, a bound computed by its hook or initialized as a property, a type with a normalization hook, which may turn a refused value into an accepted one (an example no form of the type reads is still reported), and an `[EntityId]`. |
| `VO0032` | A value object or entity identifier created uninitialized, `default(T)` or `new T()`, in code another source generator wrote: Riok.Mapperly, the configuration binding generator, or a tool you list. | It is reported in the generated file, which cannot be edited, so the fix is on your side: give Mapperly a method that calls `Create`, keep the underlying type in an options class the binding generator binds, or `AllowDefault = true` when the zero state is meaningful. [A value object another generator creates](#a-value-object-another-generator-creates) shows each fix, and how to add a generator to the list through a `.globalconfig`. |

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

## Moving off `Minimum` and `Maximum`

The `Minimum` and `Maximum` options of `[ValueObject<T>]` are deprecated. A bound written as text is read under
a grammar of its own for each underlying type — no white space, no exponent on a `decimal`, an offset on a
`DateTimeOffset` but never on a `DateTime` — which the compiler knows nothing of, so a mistake surfaces as
`VO0004` rather than as a type error, and a bound computed from anything is out of reach. The compiler reports
each use as `VO0028`, a warning that `TreatWarningsAsErrors` turns into an error, and the options are removed in
the next major version.

A value object declared with the options:

```csharp skip
[ValueObject<DateOnly>(Minimum = "1900-01-01", Maximum = "2100-12-31")]
public readonly partial struct BirthDate;
```

declares each bound as a static property of the underlying type instead:

```csharp
[ValueObject<DateOnly>]
public readonly partial struct BirthDate : IValueObjectMinimum<DateOnly>, IValueObjectMaximum<DateOnly>
{
    public static DateOnly Minimum => new(1900, 1, 1);

    public static DateOnly Maximum => new(2100, 12, 31);
}
```

The rule is unchanged: the bounds are inclusive, checked after normalization and before the known values and
`ValidateValue`, and a value outside them is rejected as `value_object.out_of_range` with the message "The value
must be greater than or equal to 1900-01-01." They are still the OpenAPI `minimum` and `maximum`, or for a type
JSON writes as a string, the `x-minimum` and `x-maximum` extensions and a sentence of the description. The message
quotes the bound in one invariant form per type, which is the option's text for an integer, a `decimal`, a `char`,
a `DateOnly` or a `TimeSpan`, but the round-trip form for a real (`1E-05` for `"1e-5"`), a time (`06:00:00.0000000`
for `"06:00"`) or a date and time, a `DateTime` without its kind: a client matching the message of such a type sees
it change.

A bound is a constant, written as an expression-bodied property as above. The check reads it each time it runs, and
the schema once, as the assembly loads, so a bound relative to the clock, such as "not in the future", belongs in
`IValueObjectValidator<T>`, and a bound must not throw. An initialized property, `{ get; } = ...`, is assigned with
the type's other static fields, in declaration order, so an instance a static field declared before it creates
would be checked against the default of the type.

## Date and time bounds

These rules concern the deprecated `Minimum` and `Maximum` options. A bound declared through
`IValueObjectMinimum<T>` or `IValueObjectMaximum<T>` is a value the code builds, `new DateTime(2020, 1, 1)`, so no
text has to be read and no day of the build enters into it. The check compares a `DateTime` bound as a clock
reading, whatever its kind, and the bound is quoted and published without one.

A bound written as text is compiled into the generated code as a fixed instant, so it must mean the same instant on every
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

## A value object another generator creates

Source generators never see each other's output. A generator that builds objects finds a value object without the
`Create`, `Parse` and conversions this generator adds to it, and writes `new T()` in their place, which skips every
rule the type declares. `VO0010` leaves generated code alone, since this generator's own `TryCreate` assigns
`default` on its rejection path, so `VO0032` reports the `new` or the `default` there, in the code of two
generators:

| Generator | What it writes |
| --- | --- |
| Riok.Mapperly | `var target = new global::CountryCode();` for a mapping from a `string` to a value object declared in the mapper's own project. The value of the source is dropped, and every value object of the target is a default instance. |
| The configuration binding generator, which `PublishAot` turns on | `var temp7 = new global::Quantity();` for a property of an options class. The options bind to default instances, and `ValidateOnStart` cannot tell. |

It is reported in the generated file, which cannot be edited: the fix is on your side.

**Mapperly.** Give it a method for each direction, which it picks by source and target type. This works on
Mapperly 4.3.1 and 5.0.0-next.11, whether the value objects are declared in the mapper's project or in a project it
references:

```csharp skip
public static class ValueObjectMappings
{
    public static Iban ToIban(string value) => Iban.Create(value);
    public static string FromIban(Iban value) => value.Value;
    public static OrderId ToOrderId(Guid value) => OrderId.Create(value);
    public static Guid FromOrderId(OrderId value) => value.Value;
}

[Mapper]
[UseStaticMapper(typeof(ValueObjectMappings))]
public partial class OrderMapper
{
    public partial Order ToDomain(OrderDto dto);
    public partial OrderDto ToDto(Order order);
}
```

Write the methods out of a value object too. Without `FromOrderId`, Mapperly can map an `OrderId` to a `Guid` as
`new Guid()`, which is `Guid.Empty`, silently: `VO0032` cannot see it, since a `Guid` is not a value object.

**The configuration binding generator.** Keep the underlying type in the options class and create the value object
where the options are read, or bind the value object in an `IConfigureOptions<T>` of your own that calls `Parse`.
Without the generator, the binder that works by reflection goes through the generated `TypeConverter`, and reports
a value its type rejects.

**Another generator.** The list holds the names these two write as the first argument of `[GeneratedCode]`,
`Riok.Mapperly` and `Microsoft.Extensions.Configuration.Binder.SourceGeneration`. Add others, comma-separated, under
`adcodicem_value_objects.generated_code_tools` in a `.globalconfig` file, which the SDK reads from the project's
directory or from any directory above it:

```ini
is_global = true
adcodicem_value_objects.generated_code_tools = My.Generator, Other.Generator
```

The names add to the list, never replace it, and each must be the tool's name in full and in the same case. The
`[GeneratedCode]` that counts is the nearest one: on the member holding the `new` or the `default`, or else on the
type containing it, and so on outwards. The key is not read from an `.editorconfig`: a `[*.cs]` section reaches a
generated file only when the project's intermediate output directory lies beneath that `.editorconfig`, which the
artifacts output layout, for one, does not. A team that wants none of this suppresses `VO0032` as a whole:
`dotnet_diagnostic.VO0032.severity = none` in that `.globalconfig`, or `<NoWarn>$(NoWarn);VO0032</NoWarn>` in the
project. In an `.editorconfig` section, the same line misses the generated files, for the same reason.

The System.Text.Json generator is not in the list, and should not be added to it. It writes
`ObjectCreator = () => new global::Sku()` into the metadata of a context for every type it is given, and a context
wired as [the JSON guide](../how-to/json.md#systemtextjson-source-generated) shows reads a value object through its
converter, never through that line.

## Suppressing `VO0010` in a test

A test that must build an uninitialized instance disables the diagnostic on the spot, with the reason:

```csharp skip
#pragma warning disable VO0010 // The guard under test must reject an uninitialized instance.
var missing = default(Iban);
#pragma warning restore VO0010
```
