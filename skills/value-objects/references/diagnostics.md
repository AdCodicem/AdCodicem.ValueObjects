# Diagnostics

Every rule the generator and the analyzers enforce, and what to do about each. `VO0008` and `VO0011` are
warnings; everything else is an error. There is no `VO0012`.

| Id | Meaning | Fix |
| --- | --- | --- |
| `VO0001` | The type is not `partial`. | Add `partial`. The generated members are added to the same type. |
| `VO0002` | The type is not a `readonly struct`, or is a record. | Declare `readonly partial struct`. A `record struct` is rejected on purpose: `with` and field-wise equality would bypass both validation and the configured comparison. A `class` is rejected because a value object is a value. |
| `VO0003` | Unsupported underlying type. | Use one of the 22 supported types. Wrap an unsupported shape in your own type instead of forcing it through a single-value value object. |
| `VO0004` | A bound could not be parsed. | `Minimum`/`Maximum` are **text**, in invariant culture: `Minimum = "0"`, `Maximum = "9.99"`, `Minimum = "2020-01-01"`. Never a localized form. |
| `VO0005` | A closed value set declares no value. | Add `[KnownValue]` entries, or drop `ValueSet = ValueSetKind.Closed` — as declared, no value could ever be valid. |
| `VO0006` | A known value has an unusable name. | The first argument of `[KnownValue]` becomes a C# member: give it a valid, unique identifier. |
| `VO0007` | Arithmetic requested on a non-numeric type. | Remove `Arithmetic = true`, or change the underlying type. |
| `VO0008` | Length constraints on a non-string type (warning). | `MinLength`/`MaxLength` only apply to `string`. For a number, use `Minimum`/`Maximum`. |
| `VO0009` | A containing type is not `partial`. | Every enclosing type must be `partial`, not just the value object. |
| `VO0010` | An uninitialized value object: `default(T)` or `new T()`. | Construct through `Create`, `TryCreate` or `Parse`. Express absence as `T?`. Only if the zero state is genuinely meaningful, set `AllowDefault = true` on the type. In a test that must build one on purpose, disable it on the spot with a comment saying why: `#pragma warning disable VO0010`. |
| `VO0011` | A rule written without declaring its hook interface (warning). | Add the interface — `IValueObjectNormalizer<T>`, `IValueObjectValidator<T>`, `IValueObjectFormatter<T>`, `IValueObjectStringFormatter<T>`. **Until you do, the rule never runs**, and a project without `TreatWarningsAsErrors` ships with validation silently absent. Also fires on the pre-interface names `NormalizeCore`, `ValidateCore`, `TryFormatCore`, `FormatCore`: rename to `NormalizeValue`, `ValidateValue`, `TryFormatValue`, `FormatValue` and declare the interface. |
| `VO0013` | A known value could not be converted. | Types that cannot be an attribute argument (`Guid`, `decimal`, `DateOnly`) are written as invariant-culture text and parsed at compile time. |
| `VO0014` | An invalid regular expression. | Fix `Pattern`. Prefer a verbatim string (`@"^\d+$"`) so escapes survive. |
| `VO0015` | A malformed entity identifier prefix. | One or more lowercase segments separated by `_`, each opening on a letter: `"acc"`, `"sk_live"`. |
| `VO0016` | Two types claiming the same prefix. | A prefix identifies one type and one only — otherwise an identifier of one kind parses as another, and the confusion the prefix exists to prevent is back. |
| `VO0017` | A normalization hook on an entity identifier. | `[EntityId]` generates the normalization of the format itself and would never call the hook. Remove it, or drop `[EntityId]` and declare an ordinary value object. |
| `VO0018` | Both `[EntityId]` and `[ValueObject<T>]` on one type. | Each generates a whole implementation. Keep the one that describes the type. |

## Diagnosing generated code

Generated sources can be read rather than guessed. Turn them on in the consuming project:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>
```

They land under `obj/Debug/net10.0/generated/AdCodicem.ValueObjects.Generators/` (or under `artifacts/obj/…`
when the repository uses the artifacts output layout). That is the fastest way to answer "is this member
actually generated, and what does it call?".

## Errors at run time rather than at compile time

- `ValueObjectException` — thrown by `Create`, by `Parse`, and by an explicit conversion, carrying the error
  code and message of the violated rule. Use `TryCreate` / `TryParse` at a boundary; let `Create` throw in
  domain code where a rejected value is a bug.
- `IsDefault` returning `true` — an instance that never went through validation crossed a boundary the
  analyzer cannot see (deserialization of a struct by another library, reflection, a default array element).
  Guard with `FluentValidation`'s `NotDefault`, or check it where the value enters.
- A rejected value reported as `value_object.not_parsable` when you expected your own code — the text did not
  even have the shape of the underlying type, so no rule of yours was ever reached.
