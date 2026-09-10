---
title: Authoring Guide
sidebar_label: Authoring Guide
slug: /authoring-guide
---

# Authoring reference

## Supported underlying types

`string`, `Guid`, `bool`, `char`, every built-in integer (including `Int128` and `UInt128`, which travel as
JSON strings), `decimal`, `double`, `float`, `DateOnly`, `TimeOnly`, `DateTime`, `DateTimeOffset`, `TimeSpan`.

## Declarative options on `[ValueObject<T>]`

| Option | Effect |
| --- | --- |
| `Pattern`, `MinLength`, `MaxLength` | Validation, EF column size, OpenAPI schema. |
| `Minimum`, `Maximum` | Written in invariant culture, parsed at compile time. |
| `Comparison` | Equality, ordering and hashing for string value objects. Ordinal by default. |
| `ValueSet = Closed` + `[KnownValue]` | Reference-data codes with a frozen lookup and a schema `enum`. Members of a closed set over a reference type are boxed once and shared, so the boxed paths allocate nothing. |
| `Arithmetic` | Operators and generic math for numeric value objects. Every result is re-validated. |
| `ImplicitConversionToValue`, `ExplicitConversionFromValue` | Conversions, opt-in per type. |
| `AllowEmpty`, `AllowDefault` | Loosen the two defaults that exist to catch mistakes. |

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

`NormalizeCore` must be idempotent and must not reject: an unnormalizable value is rejected by `ValidateCore`.
`TryFormatCore`, when present, takes over formatting entirely, including the default format.

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

| Id | Severity | Meaning |
| --- | --- | --- |
| `VO0001` | Error | The type is not `partial`. |
| `VO0002` | Error | The type is not a `readonly struct`, or is a record. |
| `VO0003` | Error | Unsupported underlying type. |
| `VO0004` | Error | A bound could not be parsed. |
| `VO0005` | Error | A closed value set declares no value. |
| `VO0006` | Error | A known value has an unusable name. |
| `VO0007` | Error | Arithmetic requested on a non-numeric type. |
| `VO0008` | Warning | Length constraints on a non-string type. |
| `VO0009` | Error | A containing type is not `partial`. |
| `VO0010` | Error | An uninitialized value object. |
| `VO0011` | Warning | A rule written without declaring its hook interface, so the generator will never call it. |
| `VO0013` | Error | A known value could not be converted. |
| `VO0014` | Error | An invalid regular expression. |
| `VO0015` | Error | A malformed entity identifier prefix. |
| `VO0016` | Error | Two types claiming the same prefix. |
| `VO0017` | Error | A normalization hook on an entity identifier, which owns its own. |
| `VO0018` | Error | Both `[EntityId]` and `[ValueObject<T>]` on one type. |

Next: [Design Decisions](./design-decisions.md), for the reasoning behind the shape of this surface.
