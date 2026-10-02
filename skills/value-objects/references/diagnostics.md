# Diagnostics

Every rule the generator and the analyzers enforce, and what to do about each. `VO0008`, `VO0011`, `VO0025` and
`VO0026` are warnings; everything else is an error. There is no `VO0012`.

| Id | Meaning | Fix |
| --- | --- | --- |
| `VO0001` | The type is not `partial`. | Add `partial`. The generated members are added to the same type. |
| `VO0002` | The type is not a `readonly struct`, or is a record or a `ref struct`. | Declare `readonly partial struct`. A `record struct` is rejected on purpose: `with` and field-wise equality would bypass both validation and the configured comparison. A `class` is rejected because a value object is a value, and a `ref struct` because it can be neither boxed nor a type argument, which the generated members require. |
| `VO0003` | Unsupported underlying type. | Use one of the 22 supported types. Wrap an unsupported shape in your own type instead of forcing it through a single-value value object. |
| `VO0004` | A bound is not written in the form of its underlying type, names no value of it, or is set on a type that takes none. | `Minimum`/`Maximum` are **text**, in the one form `references/authoring.md` gives for each type — `"0"`, `"-9.99"`, `"2020-01-01"`, `"08:30"`, `"2020-01-01T08:30+02:00"` — with no white space around it and never a localized form. The message names the form. The value must fit the type — `"300"` is no `byte`, `"-1"` no `ulong`, `"2023-02-29"` no date — and a `double` or `float` bound must be finite, and zero only when written as zero: `"1e-400"` reads as zero and is refused. A `decimal` takes no exponent. A `DateTime` bound is written without an offset (`"2020-01-01T08:30"`, never `Z` or `+02:00`) and a `DateTimeOffset` bound always with one (`"2020-01-01T08:30+02:00"`), and neither is a time alone: otherwise the instant would depend on the time zone or the day of the build. `string`, `Guid` and `bool` take no bound: constrain a string with `MinLength`, `MaxLength` or `Pattern`. |
| `VO0005` | A closed value set declares no value. | Add `[KnownValue]` entries, or drop `ValueSet = ValueSetKind.Closed` — as declared, no value could ever be valid. |
| `VO0006` | A known value has an unusable name. | The first argument of `[KnownValue]` becomes a static property of the type: give it a valid, unique identifier. The message ends with the rule the name broke. Not a keyword (`class`, `default`; a contextual keyword such as `value` is fine). Not a name the generated code uses: `_`, the name of the type, a generated member (`Value`, `Schema`, `Create`, `TryCreate`, `Parse`, `KnownValues`, … and `Zero`, `One`, `Min`, `Max`, `Sum` with `Arithmetic`), the metadata name of a generated operator (`op_Equality`, `op_LessThan`, …; `op_Addition`, … with `Arithmetic`; `op_Implicit`, `op_Explicit` with the conversions), or a getter's name (`get_Value`, `get_Schema`, `get_France` beside a known value `France`). Not a name the type already has: a member you declared on it (a hook such as `NormalizeValue`, a field, a nested class) or a member of `object` (`GetType`, `MemberwiseClone`). Not a name whose getter, `get_` followed by it, a member you declared already takes: `France` beside a field, a nested type or a parameterless method `get_France` (a `get_France(int)` overload is fine). Not the name of another known value. |
| `VO0007` | Arithmetic requested on a non-numeric type. | Remove `Arithmetic = true`, or change the underlying type. |
| `VO0008` | Length constraints on a non-string type (warning). | `MinLength`/`MaxLength` only apply to `string`. For a number, use `Minimum`/`Maximum`. |
| `VO0009` | A containing type is not `partial`. | Every enclosing type must be `partial`, not just the value object. |
| `VO0010` | An uninitialized value object or entity identifier: `default(T)` or `new T()`. | Construct through `Create`, `TryCreate` or `Parse` (or `New()` for an identifier). Express absence as `T?`. Only if the zero state is genuinely meaningful, set `AllowDefault = true` on the type, on `[ValueObject<T>]` or `[EntityId]` alike. In a test that must build one on purpose, disable it on the spot with a comment saying why: `#pragma warning disable VO0010`. Reported where `default` or `new` is written: a parameter `T value = default` once, on its declaration — fix it there (make it `T? value = null`), not at the calls that omit the argument. |
| `VO0011` | A rule written without declaring its hook interface (warning), on a value object or an `[EntityId]`. | Add the interface — `IValueObjectNormalizer<T>`, `IValueObjectValidator<T>`, `IValueObjectFormatter<T>`, `IValueObjectStringFormatter<T>`, or `IValueObjectPatternValidator` for a `static Regex Pattern` on a string value object (left alone when the type implements `IValueObjectValidator<string>`, whose `ValidateValue` may already run it). **Until you do, the rule never runs**, and a project without `TreatWarningsAsErrors` ships with validation silently absent. Also fires on the pre-interface names `NormalizeCore`, `ValidateCore`, `TryFormatCore`, `FormatCore`: rename to `NormalizeValue`, `ValidateValue`, `TryFormatValue`, `FormatValue` and declare the interface. On an `[EntityId]` it fires for a validator or a formatter only: never add a normalizer to an identifier, with or without its interface (`VO0017`). |
| `VO0013` | A known value is not written in the form of its underlying type, or is not a value. | A known value given as text follows the form of a bound (`VO0004`), which the message names; on a `Guid` or a `bool` it keeps every form the type reads, without white space around it. A C# constant (`200`, `0.5`, `true`) is held to the same form through its invariant text. It is one value: never `null`, an array, a `typeof(...)` or an enum member. |
| `VO0014` | An invalid regular expression. | Fix `Pattern`. Prefer a verbatim string (`@"^\d+$"`) so escapes survive. |
| `VO0015` | A malformed entity identifier prefix. | One or more lowercase segments separated by `_`, each opening on a letter: `"acc"`, `"sk_live"`. |
| `VO0016` | Two types claiming the same prefix. | A prefix identifies one type and one only — otherwise an identifier of one kind parses as another, and the confusion the prefix exists to prevent is back. |
| `VO0017` | A normalization hook on an entity identifier. | `[EntityId]` generates the normalization of the format itself and would never call the hook. Remove it, or drop `[EntityId]` and declare an ordinary value object. |
| `VO0018` | Both `[EntityId]` and `[ValueObject<T>]` on one type. | Each generates a whole implementation. Keep the one that describes the type. |
| `VO0019` | The generated code cannot reopen, reach or name the type: it has type parameters, is nested in a generic type or an interface, is `private`, `protected` or `private protected` or nested in such a type, is `file`-local or nested in a `file` type, takes the name of a member the generator writes on it (`Value`, `Schema`, `Create`, `Parse`, `op_Equality`, `Zero` with `Arithmetic`, `Prefix` on an `[EntityId]`, …), or is named `var` or `_` or nested in a type of that name. | Nothing is generated for the type; the message says which rule it broke and what to do. Declare it without type parameters, at namespace level or nested in non-generic classes, structs and records; `internal` or `public`, and every type around it too, since the generated registration refers to it from a class of its own; without `file`, since the generated code reopens it in a file of its own; and under a name of its own. Never name any type `var` or `_` where value objects are declared: the generated statements write both. |
| `VO0020` | `Comparison`, `ValueSet` or `Granularity` holds a value its enum does not define, such as `(StringComparison)42`. | Use one of the enum's named members. Nothing is generated for the type until then, rather than a default nobody chose. |
| `VO0022` | Both the `Pattern` option and `IValueObjectPatternValidator` on one type. | Remove `Pattern = "..."`: the hook replaces it, and wins until you do. |
| `VO0023` | `IValueObjectPatternValidator` on a value object whose underlying type is not `string`. | A pattern only applies to text, so it would never run. Remove the interface; validate a number or a date in `IValueObjectValidator<T>`. |
| `VO0024` | `IValueObjectPatternValidator` on an `[EntityId]`. | `[EntityId]` validates its format itself and publishes its own OpenAPI pattern. Remove the interface, or drop `[EntityId]` and declare an ordinary value object. Nothing is generated for the type until then. |
| `VO0025` | The `[GeneratedRegex]` behind `Pattern` sets `IgnoreCase`, `Multiline`, `Singleline` or `IgnorePatternWhitespace` (warning). | The OpenAPI `pattern` is the regex's text, which carries no option, so clients would check values differently. Write the rule into the pattern itself: `[A-Za-z]` rather than `IgnoreCase`. |
| `VO0026` | The `[GeneratedRegex]` behind `Pattern` sets no `matchTimeoutMilliseconds` (warning). | Set one, such as `matchTimeoutMilliseconds: 1000`. Without it, a pathological input holds a request thread for as long as the match runs. |

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
  code and message of the violated rule: from `Parse`, the code the four-argument `TryParse` reports. Use
  `TryCreate` / `TryParse` at a boundary; let `Create` throw in domain code where a rejected value is a bug.
- `IsDefault` returning `true` — an instance that never went through validation crossed a boundary the
  analyzer cannot see (deserialization of a struct by another library, reflection, a default array element).
  Guard with `FluentValidation`'s `NotDefault`, or check it where the value enters.
- A rejected value reported as `value_object.not_parsable` when you expected your own code — the text did not
  even have the shape of the underlying type, so no rule of yours was ever reached.
