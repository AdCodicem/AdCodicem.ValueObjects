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
| `bool IsDefault` | `true` for an instance that was never constructed. The run-time guard where `VO0010` cannot see. |
| `static ValueObjectSchema Schema` | The declared rules as data: lengths, pattern, bounds, format, known values, description. On a type implementing `IValueObjectPatternValidator`, the pattern is the text of its `[GeneratedRegex]`, read when the type compiles. |

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
`CreateUnchecked`.

## Text

| Member | |
| --- | --- |
| `static TSelf Parse(string s)`, `Parse(string s, IFormatProvider? provider)`, `Parse(ReadOnlySpan<char> s, IFormatProvider? provider)` | Parses the underlying value from text, then as `Create`. A rejection throws `ValueObjectException` with the code the four-argument `TryParse` reports. Without a formatting hook, it reads back what `ToString` writes: a `DateTime` keeps the kind its round-trip form names, `Z` for UTC, on a machine in any time zone. |
| `static bool TryParse(…, out TSelf result)` | For `string` and `ReadOnlySpan<char>`, with or without a provider. |
| `static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out TSelf result, out ValidationResult validation)` | And with the rule that fired; a `string` converts to the span implicitly. |
| `string ToString()`, `ToString(string? format, IFormatProvider? provider)` | The underlying value or, with a [formatter hook](../how-to/formatting.md), what the hook writes: its default format for `ToString()`, its named formats otherwise. With both formatter hooks, the string formatter answers. A span formatter is offered a larger pooled buffer each time it answers that the destination is too small, up to 1,048,576 characters, past which `ToString` throws `FormatException`; on a `string` value object, text equal to the value returns the string the value object holds. |
| `bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)` | Formats into the destination without allocating, unless the type declares `IValueObjectStringFormatter<TValue>`: the string it returns is then copied into the destination. With both formatter hooks, the string formatter answers here too. |

Text that does not even have the shape of the underlying type is rejected with `value_object.not_parsable`,
before any rule of the type runs.

## Equality and ordering

`Equals(TSelf)`, `Equals(object?)`, `GetHashCode()`, `CompareTo(TSelf)`, and the operators `==`, `!=`, `<`, `>`,
`<=`, `>=`. For a `string` value object they all follow the declared `Comparison`, ordinal by default.

## Serialization and discovery

- A nested `ValueJsonConverter`, applied with `[JsonConverter]`: the value is read and written as its bare
  underlying value, including as a dictionary key, whatever a formatter hook writes.
- A nested `ValueTypeConverter`, applied with `[TypeConverter]`, converting from and to the underlying value and
  its text.
- `[DebuggerDisplay]`, showing the formatted value.
- A registration in a generated `[ModuleInitializer]`, which makes the type available to
  [`ValueObjectRegistry`](../how-to/runtime-lookup.md) without any code of yours.

## Added by options

| Option | Adds |
| --- | --- |
| `[KnownValue("Name", …)]` | `static TSelf Name`, per value, and `static ImmutableArray<TSelf> KnownValues`. |
| `ImplicitConversionToValue = true` | `implicit operator TValue(TSelf)`. |
| `ExplicitConversionFromValue = true` | `explicit operator TSelf(TValue)`, validating like `Create`. |
| `Arithmetic = true` | `+`, `-`, `*`, `/`, unary `-`, `Zero`, `One`, `IsZero`, `Min`, `Max`. |
| `[EntityId("prefix")]` | `New()`, `Prefix`, `Granularity`, `Length`. |
