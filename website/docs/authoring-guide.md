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
| `Pattern` | `string?` | none | Regular expression the **normalized** value must match. Also the OpenAPI `pattern`. Invalid → `VO0014`. |
| `MinLength`, `MaxLength` | `int` | `-1`, unconstrained | `string` only (`VO0008` otherwise). Validation, OpenAPI `minLength` / `maxLength`, and the EF Core column size. |
| `Minimum`, `Maximum` | `string?` | none | Inclusive bounds in **invariant-culture text**, so `decimal`, `DateOnly` and `TimeSpan` keep full precision. Parsed at compile time; unparsable, or outside the type → `VO0004`. A `DateTime` bound carries no offset and a `DateTimeOffset` bound always does ([why](./reference/diagnostics.md#date-and-time-bounds)). Also OpenAPI `minimum` / `maximum`. |
| `Comparison` | `StringComparison` | `Ordinal` | `string` only. Drives equality, ordering and hashing together. |
| `ValueSet` | `ValueSetKind` | `Open` | `Closed` accepts only the declared `[KnownValue]`s, through a frozen lookup, and becomes the schema `enum`. Members of a closed set over a reference type are boxed once and shared, so the boxed paths allocate nothing. |
| `Arithmetic` | `bool` | `false` | Numeric types only (`VO0007` otherwise). Operators and generic math; every result is validated again. |
| `ImplicitConversionToValue` | `bool` | `false` | `string s = iban;` |
| `ExplicitConversionFromValue` | `bool` | `false` | `(Iban)text`, validating like `Create`. |
| `AllowEmpty` | `bool` | `false` | `string` only. Accepts `""`; `null` is still rejected, since absence is `T?`. |
| `AllowDefault` | `bool` | `false` | Silences `VO0010`, for a type whose zero state is meaningful. |
| `SchemaFormat` | `string?` | the natural format of the type | OpenAPI `format`: `uuid`, `date`, `int64`, or your own such as `iban` or `email`. |
| `Example` | `string?` | none | OpenAPI example. |
| `Description` | `string?` | the type's XML `<summary>` | OpenAPI description. |

Declared rules run before any hook, so a validator only ever sees values that already satisfy them.
[Validation and normalization](./tutorials/validation-and-normalization.md#the-order-things-run-in) gives the
exact order.

## Hooks

A value object declares a rule by implementing an interface, so the compiler checks the signature: a
mis-typed rule fails the build instead of being silently ignored. All are optional, and `VO0011` reports a
rule written without its interface — the one mistake the compiler cannot catch.

| Interface | Member |
| --- | --- |
| `IValueObjectNormalizer<TValue>` | `static TValue NormalizeValue(TValue value)` |
| `IValueObjectSpanNormalizer` | `static string NormalizeValue(ReadOnlySpan<char> value)` — string value objects only |
| `IValueObjectValidator<TValue>` | `static ValidationResult ValidateValue(in TValue value)` |
| `IValueObjectFormatter<TValue>` | `static bool TryFormatValue(in TValue value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)` |
| `IValueObjectStringFormatter<TValue>` | `static string FormatValue(in TValue value, ReadOnlySpan<char> format, IFormatProvider? provider)` |

`NormalizeValue` must be idempotent and must not reject: an unnormalizable value is rejected by
`ValidateValue`. `TryFormatValue`, when present, takes over formatting entirely, including the default format.

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

## Diagnostics

The generator and the analyzers report `VO0001` to `VO0019`. [Diagnostics](./reference/diagnostics.md) lists
each one with its fix.

Next: [Generated members](./reference/generated-members.md), for what the generator writes from all of this.
