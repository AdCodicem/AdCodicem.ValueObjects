---
title: Generated Members
sidebar_label: Generated members
slug: /reference/generated-members
description: Every member and interface the source generator adds to a value object, and the options that add more.
---

# Generated members

What the generator adds to a `readonly partial struct` marked `[ValueObject<TValue>]`. `TSelf` is the value
object, `TValue` its underlying type.

## Interfaces

Every value object implements `IValueObject<TSelf, TValue>`, which brings:

- `IEquatable<TSelf>`, `IComparable<TSelf>` and `IComparable`;
- `ISpanParsable<TSelf>`, and so `IParsable<TSelf>`;
- `ISpanFormattable`, and so `IFormattable`.

`Arithmetic = true` adds `INumericValueObject<TSelf, TValue>`; `[EntityId]` adds `IEntityId<TSelf>`.

## State

| Member | |
| --- | --- |
| `TValue Value` | The underlying value, normalized and valid on any constructed instance. For a `string` value object, an uninitialized instance reads as `""`. |
| `bool IValueObject<TSelf, TValue>.IsDefault` | `true` for an instance equal to `default(TSelf)`: over a `string`, exactly one that never went through `Create`; over a value type, also a constructed instance holding the type's zero (`0`, `Guid.Empty`, …), which only `VO0010` and validation tell apart. The run-time guard where `VO0010` cannot see. Implemented explicitly, so a tool reading public properties finds `Value` alone: generic code constrained on the interface reads it without boxing, code holding the concrete type through a cast to the interface, which boxes. |
| `static ValueObjectSchema Schema` | The declared rules as data: lengths, pattern, bounds, format, known values, description. `KnownValueDetails` lists the known values again, in the same order, each a `KnownValueInfo` holding the value, the name of its member and its description. `Example` is the underlying value of the instance `IValueObjectExample<TSelf>` declares. On a type implementing `IValueObjectPatternValidator`, the pattern is the text of its `[GeneratedRegex]`, read when the type compiles. It implements the static member of `IValueObject<TSelf, TValue>`, so generic code reads it as `TSelf.Schema`, and it is the instance the registration hands the descriptor. |

## Construction

| Member | |
| --- | --- |
| `static TSelf Create(TValue value)` | Normalizes, validates, and throws `ValueObjectException` on rejection. |
| `static bool TryCreate(TValue value, out TSelf result)` | The same, returning `false` instead of throwing. |
| `static bool TryCreate(TValue value, out TSelf result, out ValidationResult validation)` | The same, with the rule that fired. |
| `static TSelf CreateUnchecked(TValue value)` | Skips normalization and validation, for values the application produced itself. |
| `static TValue Normalize(TValue value)` | Runs `NormalizeValue` if the type declares it; passes `null` through. |
| `static ValidationResult Validate(in TValue value)` | The declared rules, including the `Pattern` of `IValueObjectPatternValidator`, then `ValidateValue` if the type declares it. |

The constructor is private: every public way in goes through `Create`, `TryCreate`, `Parse`, `TryParse` or
`CreateUnchecked`. After the value, it takes a required `UncheckedTag` that only the generated code supplies, so a
reflection mapper looking for a constructor that takes the value alone finds none. AutoMapper then throws
`AutoMapperMappingException` instead of wrapping a value no rule has checked: give it a map,
`CreateMap<string, Iban>().ConvertUsing(s => Iban.Create(s))`. On a type declaring `ExplicitConversionFromValue = true`,
it calls the explicit conversion, which validates.

## Text

| Member | |
| --- | --- |
| `static TSelf Parse(string s)`, `Parse(string s, IFormatProvider? provider)`, `Parse(ReadOnlySpan<char> s, IFormatProvider? provider)` | Parses the underlying value from text, then as `Create`. A rejection throws `ValueObjectException`, a `FormatException` as `IParsable<T>` documents, with the code the four-argument `TryParse` reports. Without a formatting hook, it reads back what `ToString` writes: a `DateTime` keeps the kind its round-trip form names, `Z` for UTC, on a machine in any time zone. A `null` provider stands for the invariant culture, as it does for `ToString` and `TryFormat`, not for the current culture as it does for `decimal.Parse`. Read with a `null` provider or the invariant culture, a `decimal`, a `double` or a `float` takes no group separator: `12,5` and `1,234.5` are `value_object.not_parsable`, not 125 and 1234.5. Any other culture reads the text with the type's own number styles, so `fr-FR` reads `12,5` as 12.5. |
| `static bool TryParse(…, out TSelf result)` | For `string` and `ReadOnlySpan<char>`, with or without a provider, which it reads as `Parse` does. |
| `static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out TSelf result, out ValidationResult validation)` | And with the rule that fired; a `string` converts to the span implicitly. |
| `string ToString()`, `ToString(string? format, IFormatProvider? provider)` | The underlying value or, with a [formatter hook](../how-to/formatting.md), what the hook writes: its default format for `ToString()`, its named formats otherwise. With both formatter hooks, the string formatter answers. A span formatter is offered a larger pooled buffer each time it answers that the destination is too small, up to 1,048,576 characters, past which `ToString` throws `FormatException`; on a `string` value object, text equal to the value returns the string the value object holds. |
| `bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)` | Formats into the destination without allocating, unless the type declares `IValueObjectStringFormatter<TValue>`: the string it returns is then copied into the destination. With both formatter hooks, the string formatter answers here too. |

Text that does not even have the shape of the underlying type is rejected with `value_object.not_parsable`,
before any rule of the type runs. The integrations that read text pass the invariant culture — model binding and
minimal APIs, configuration through the `TypeConverter`, a JSON dictionary key, a text column read by Dapper — and a
descriptor reads a `null` provider as it, so a query string `?total=12,5` is refused there too.

## Equality and ordering

`Equals(TSelf)`, `Equals(object?)`, `GetHashCode()`, `CompareTo(TSelf)`, and the operators `==`, `!=`, `<`, `>`,
`<=`, `>=`. For a `string` value object they all follow the declared `Comparison`, ordinal by default.

## Serialization and discovery

- A nested `ValueJsonConverter`, applied with `[JsonConverter]`: the value is read and written as its bare
  underlying value, including as a dictionary key, whatever a formatter hook writes. It writes no value the type
  rejects: an uninitialized instance whose default value fails validation is refused
  ([A value refused on write](./errors.md#a-value-refused-on-write)). Every refusal, reading or writing, is a
  `ValueObjectJsonException`, a `JsonException` carrying the type and the code of the rule
  ([the code in an exception](./errors.md#the-code-in-an-exception)).
- A nested `ValueTypeConverter`, applied with `[TypeConverter]`, converting from and to the underlying value and
  its text. Over a number, it converts from and to every numeric type a value object may wrap, `sbyte` to
  `UInt128`, `decimal`, `double` and `float`, since its callers hand it the number they hold: Newtonsoft.Json without
  its converter a `long` for every JSON integer, a numeric control a `decimal`. The conversion is checked. A number
  the underlying type cannot hold whole, out of its range or with a fraction for an integer, is
  `value_object.not_parsable`, never truncated, and one that fits goes through `Create`, so a `long` of 5000 is
  `value_object.out_of_range` for a quantity bounded to 1000. A `double` or a `float` becomes a `decimal` with every
  digit it carries. Converting the value to a numeric type that cannot hold it whole throws `NotSupportedException`.
- `[DebuggerDisplay]`, showing the formatted value.
- A registration in a generated `[ModuleInitializer]`, which makes the type available to
  [`ValueObjectRegistry`](../how-to/runtime-lookup.md) without any code of yours. A `private` or `protected` type is
  registered through a step nested in the types around it, an `internal` class no code of yours needs to call.

On a generic value object, an attribute cannot name a converter through a type parameter, so `[JsonConverter]` names
`GenericValueObjectJsonConverterFactory` and `[TypeConverter]` names `GenericValueObjectTypeConverter`, which reach the
construction they convert through its descriptor, and convert numbers as the generated converter does. The
registration registers the generic definition.

## Added by options

| Option | Adds |
| --- | --- |
| A member marked `[KnownValue]` | `static ImmutableArray<TSelf> KnownValues`, listing the members in declaration order, and the private `static TSelf Known(TValue)` they are initialized through, which applies every rule but membership. The member itself is yours. |
| `ImplicitConversionToValue = true` | `implicit operator TValue(TSelf)`. |
| `ExplicitConversionFromValue = true` | `explicit operator TSelf(TValue)`, validating like `Create`. |
| `Arithmetic = true` | `+`, `-`, `*`, `/`, unary `-`, `Zero`, `One`, `IsZero`, `Min`, `Max`. |
| `[EntityId("prefix")]` | `New()`, `Prefix`, `Granularity`, `Length`. |
