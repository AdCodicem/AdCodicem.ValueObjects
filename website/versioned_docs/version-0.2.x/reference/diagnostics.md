---
title: Diagnostics
sidebar_label: Diagnostics
slug: /reference/diagnostics
description: Every VO diagnostic the generator and the analyzers report, what it means, and how to fix it.
---

# Diagnostics

Every rule the generator and the analyzers enforce. `VO0008` and `VO0011` are warnings; every other one is an
error. There is no `VO0012`.

| Id | Meaning | Fix |
| --- | --- | --- |
| `VO0001` | The type is not `partial`. | Add `partial`: the generated members are added to the same type. |
| `VO0002` | The type is not a `readonly struct`, or is a record. | Declare a `readonly partial struct`. A `record struct` is refused on purpose, because `with` and field-wise equality would bypass validation and the declared comparison; a class, because a value object is a value. |
| `VO0003` | Unsupported underlying type. | Use one of the [supported types](../authoring-guide.md#supported-underlying-types). |
| `VO0004` | A bound could not be parsed. | Write `Minimum` and `Maximum` as invariant-culture text: `"0"`, `"9.99"`, `"2020-01-01"`. |
| `VO0005` | A closed value set declares no value. | Add `[KnownValue]` entries, or drop `ValueSet = ValueSetKind.Closed`: as declared, no value could be valid. |
| `VO0006` | A known value has an unusable name. | The first argument of `[KnownValue]` becomes a member: give it a valid, unique C# identifier. |
| `VO0007` | Arithmetic requested on a non-numeric type. | Remove `Arithmetic = true`, or change the underlying type. |
| `VO0008` | Length constraints on a non-string type (warning). | `MinLength` and `MaxLength` apply to `string` only; use `Minimum` and `Maximum` for a number. |
| `VO0009` | A containing type is not `partial`. | Every enclosing type must be `partial`, not only the value object. |
| `VO0010` | An uninitialized value object: `default(T)` or `new T()`. | Construct through `Create`, `TryCreate` or `Parse`, and express absence as `T?`. If the zero state is genuinely meaningful, set `AllowDefault = true` on the type. |
| `VO0011` | A rule written without its hook interface (warning). | Declare the interface — `IValueObjectNormalizer<T>`, `IValueObjectValidator<T>`, `IValueObjectFormatter<T>`, `IValueObjectStringFormatter<T>`. Until then the rule never runs. |
| `VO0013` | A known value could not be converted. | A `Guid`, a `decimal` or a `DateOnly` is written as invariant-culture text and converted at compile time; fix the text. |
| `VO0014` | An invalid regular expression. | Fix `Pattern`. A verbatim string (`@"^\d+$"`) keeps escapes intact. |
| `VO0015` | A malformed entity identifier prefix. | One or more lower-case segments separated by `_`, each starting with a letter: `"acc"`, `"sk_live"`. |
| `VO0016` | Two types claim the same prefix. | Give each identifier type its own prefix, or one kind of identifier would parse as another. |
| `VO0017` | A normalization hook on an entity identifier. | `[EntityId]` normalizes its own format and would never call the hook: remove it. |
| `VO0018` | Both `[EntityId]` and `[ValueObject<T>]` on one type. | Each generates a whole implementation; keep the one that describes the type. |

`VO0011` deserves its warning more than most. The code it reports compiles and looks right, and in a project
without `TreatWarningsAsErrors` it ships with the rule silently absent. It also recognizes the names hooks had
before they became interfaces — `NormalizeCore`, `ValidateCore`, `TryFormatCore`, `FormatCore` — which should be
renamed to `NormalizeValue`, `ValidateValue`, `TryFormatValue` and `FormatValue`.

## Suppressing `VO0010` in a test

A test that must build an uninitialized instance disables the diagnostic on the spot, with the reason:

```csharp skip
#pragma warning disable VO0010 // The guard under test must reject an uninitialized instance.
var missing = default(Iban);
#pragma warning restore VO0010
```
