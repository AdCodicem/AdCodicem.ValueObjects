# Diagnostics

Every rule the generator and the analyzers enforce, and what to do about each. `VO0008`, `VO0011`, `VO0021`,
`VO0025`, `VO0026` and `VO0028` are warnings; everything else is an error. There is no `VO0012`.

| Id | Meaning | Fix |
| --- | --- | --- |
| `VO0001` | The type is not `partial`. | Add `partial`. The generated members are added to the same type. |
| `VO0002` | The type is not a `readonly struct`, or is a record or a `ref struct`. | Declare `readonly partial struct`. A `record struct` is rejected on purpose: `with` and field-wise equality would bypass both validation and the configured comparison. A `class` is rejected because a value object is a value, and a `ref struct` because it can be neither boxed nor a type argument, which the generated members require. |
| `VO0003` | Unsupported underlying type. | Use one of the 22 supported types. Wrap an unsupported shape in your own type instead of forcing it through a single-value value object. |
| `VO0004` | A bound set through the deprecated `Minimum`/`Maximum` options is not written in the form of its underlying type, names no value of it, or is set on a type that takes none. | Migrate to `IValueObjectMinimum<T>`/`IValueObjectMaximum<T>` (`VO0028`), whose bound the compiler checks. Until then, the options are **text**, in the one form `references/authoring.md` gives for each type — `"0"`, `"-9.99"`, `"2020-01-01"`, `"08:30"`, `"2020-01-01T08:30+02:00"` — with no white space around it and never a localized form. The message names the form. The value must fit the type — `"300"` is no `byte`, `"-1"` no `ulong`, `"2023-02-29"` no date — and a `double` or `float` bound must be finite, and zero only when written as zero: `"1e-400"` reads as zero and is refused. A `decimal` takes no exponent. A `DateTime` bound is written without an offset (`"2020-01-01T08:30"`, never `Z` or `+02:00`) and a `DateTimeOffset` bound always with one (`"2020-01-01T08:30+02:00"`), and neither is a time alone: otherwise the instant would depend on the time zone or the day of the build. `string`, `Guid` and `bool` take no bound: constrain a string with `MinLength`, `MaxLength` or `IValueObjectPatternValidator`. |
| `VO0005` | A closed value set declares no value. | Add `[KnownValue]` entries, or drop `ValueSet = ValueSetKind.Closed` — as declared, no value could ever be valid. |
| `VO0006` | A known value has an unusable name. | The first argument of `[KnownValue]` becomes a static property of the type: give it a valid, unique identifier. The message ends with the rule the name broke. Not a keyword (`class`, `default`; a contextual keyword such as `value` is fine). Not a name the generated code uses: the name of the type, a generated member (`Value`, `Schema`, `Create`, `TryCreate`, `Parse`, `KnownValues`, … and `Zero`, `One`, `Min`, `Max`, `Sum` with `Arithmetic`), the metadata name of a generated operator (`op_Equality`, `op_LessThan`, …; `op_Addition`, … with `Arithmetic`; `op_Implicit`, `op_Explicit` with the conversions), or a getter's name (`get_Value`, `get_Schema`, `get_France` beside a known value `France`). Not a name the type already has: a member you declared on it (a hook such as `NormalizeValue`, a field, a nested class) or a member of `object` (`GetType`, `MemberwiseClone`). Not a name whose getter, `get_` followed by it, a member you declared already takes: `France` beside a field, a nested type or a parameterless method `get_France` (a `get_France(int)` overload is fine). Not the name of a type parameter of the type (`T` on `Label<T>`), nor one whose getter a type parameter takes (`Foo` on `Tag<get_Foo>`). Not the name of another known value. |
| `VO0007` | Arithmetic requested on a non-numeric type. | Remove `Arithmetic = true`, or change the underlying type. |
| `VO0008` | Length constraints on a non-string type (warning). | `MinLength`/`MaxLength` only apply to `string`. For a number, implement `IValueObjectMinimum<T>`/`IValueObjectMaximum<T>`. |
| `VO0009` | A containing type is not `partial`. | Every enclosing type must be `partial`, not just the value object. |
| `VO0010` | An uninitialized value object or entity identifier: `default(T)` or `new T()`. | Construct through `Create`, `TryCreate` or `Parse` (or `New()` for an identifier). Express absence as `T?`. Only if the zero state is genuinely meaningful, set `AllowDefault = true` on the type, on `[ValueObject<T>]` or `[EntityId]` alike. In a test that must build one on purpose, disable it on the spot with a comment saying why: `#pragma warning disable VO0010`. Reported where `default` or `new` is written: a parameter `T value = default` once, on its declaration — fix it there (make it `T? value = null`), not at the calls that omit the argument. |
| `VO0011` | A rule written without declaring its hook interface (warning), on a value object or an `[EntityId]`. | Add the interface — `IValueObjectNormalizer<T>`, `IValueObjectValidator<T>`, `IValueObjectFormatter<T>`, `IValueObjectStringFormatter<T>`, `IValueObjectPatternValidator` for a public `static Regex Pattern` on a string value object (left alone when it is not public, or when the type implements another hook, which may already run it), or `IValueObjectMinimum<T>`/`IValueObjectMaximum<T>` for a public static `Minimum`/`Maximum` property of the underlying type on a value object that takes a bound (left alone when the type implements `IValueObjectValidator<T>` or `IValueObjectNormalizer<T>`, which may already check it or clamp to it, or still sets the deprecated option, which is `VO0028`; a field is never reported, since it could not implement the hook). **Until you do, the rule never runs**, and a project without `TreatWarningsAsErrors` ships with validation silently absent. Also fires on the pre-interface names `NormalizeCore`, `ValidateCore`, `TryFormatCore`, `FormatCore`: rename to `NormalizeValue`, `ValidateValue`, `TryFormatValue`, `FormatValue` and declare the interface. On an `[EntityId]` it fires for a validator or a formatter only: never add a normalizer to an identifier, with or without its interface (`VO0017`). |
| `VO0013` | A known value is not written in the form of its underlying type, or is not a value. | A known value given as text follows the form of a bound (`VO0004`), which the message names; on a `Guid` or a `bool` it keeps every form the type reads, without white space around it. A C# constant (`200`, `0.5`, `true`) is held to the same form through its invariant text. It is one value: never `null`, an array, a `typeof(...)` or an enum member. |
| `VO0014` | An invalid regular expression in the deprecated `Pattern` option. | Migrate to `IValueObjectPatternValidator` (`VO0021`): an invalid pattern in a `[GeneratedRegex]` is reported by the regex generator itself. Until then, fix `Pattern`, preferably as a verbatim string (`@"^\d+$"`) so escapes survive. |
| `VO0015` | A malformed entity identifier prefix. | One or more lowercase segments separated by `_`, each opening on a letter: `"acc"`, `"sk_live"`. |
| `VO0016` | Two types claiming the same prefix. | A prefix identifies one type and one only — otherwise an identifier of one kind parses as another, and the confusion the prefix exists to prevent is back. |
| `VO0017` | A normalization hook on an entity identifier. | `[EntityId]` generates the normalization of the format itself and would never call the hook. Remove it, or drop `[EntityId]` and declare an ordinary value object. |
| `VO0018` | Both `[EntityId]` and `[ValueObject<T>]` on one type. | Each generates a whole implementation. Keep the one that describes the type. |
| `VO0019` | The generated code cannot reopen, reach or name the type: it is `file`-local or nested in a `file` type; it is `private`, `protected` or `private protected`, or nested in such a type, inside a generic type; it is a generic `[EntityId]`, or one nested in a generic type; it has a type parameter named after a member the generator writes, or a type around it has one that a nested type or type parameter of the same name hides from it; or it takes the name of a member the generator writes on it (`Value`, `Schema`, `Create`, `Parse`, `op_Equality`, `Zero` with `Arithmetic`, `Prefix` on an `[EntityId]`, …). | Nothing is generated for the type; the message says which rule it broke and what to do. Declare it without `file`, since the generated code reopens it in a file of its own; make a `private` or `protected` type inside a generic one `internal` or `public`, or move it out, since the registration reaches it through the types around it, by name; declare an identifier without type parameters, since its prefix names one type; rename a type parameter the generated code cannot use; and give the type a name of its own. |
| `VO0020` | `Comparison`, `ValueSet` or `Granularity` holds a value its enum does not define, such as `(StringComparison)42`. | Use one of the enum's named members. Nothing is generated for the type until then, rather than a default nobody chose. |
| `VO0021` | The deprecated `Pattern` option of `[ValueObject<T>]` (warning, reported by the compiler). | Implement `IValueObjectPatternValidator` with `[GeneratedRegex("X", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)] public static partial Regex Pattern { get; }` and remove `Pattern = "X"`. The option builds its regular expression at run time, which native AOT interprets, and is removed in the next major. |
| `VO0022` | Both the `Pattern` option and `IValueObjectPatternValidator` on one type. | Remove `Pattern = "..."`: the hook replaces it, and wins until you do. |
| `VO0023` | `IValueObjectPatternValidator` on a value object whose underlying type is not `string`. | A pattern only applies to text, so it would never run. Remove the interface; validate a number or a date in `IValueObjectValidator<T>`. |
| `VO0024` | `IValueObjectPatternValidator` on an `[EntityId]`. | `[EntityId]` validates its format itself and publishes its own OpenAPI pattern. Remove the interface, or drop `[EntityId]` and declare an ordinary value object. Nothing is generated for the type until then. |
| `VO0025` | The `[GeneratedRegex]` behind `Pattern` sets `IgnoreCase`, `Multiline`, `Singleline` or `IgnorePatternWhitespace` (warning). | The OpenAPI `pattern` is the regex's text, which carries no option, so clients would check values differently. Write the rule into the pattern itself: `[A-Za-z]` rather than `IgnoreCase`. |
| `VO0026` | The `[GeneratedRegex]` behind `Pattern` sets no `matchTimeoutMilliseconds` (warning). | Set one, such as `matchTimeoutMilliseconds: 1000`. Without it, a pathological input holds a request thread for as long as the match runs. |
| `VO0027` | `[KnownValue]` on an `[EntityId]`. | An identifier is minted, not chosen from a set, and `[EntityId]` generates no known values: the attribute would be read by no one. Declare a well-known identifier as a static property of the type, `public static AccountId System { get; } = Parse("acc_…", null);`. The identifier still generates. |
| `VO0028` | The deprecated `Minimum` or `Maximum` option of `[ValueObject<T>]` (warning, reported by the compiler). | Implement `IValueObjectMinimum<T>` with `public static T Minimum => …;` (or `IValueObjectMaximum<T>` with `Maximum`) and remove `Minimum = "…"`. The bound becomes a value of the underlying type, which the compiler checks, rather than text read under one grammar per type. The options are removed in the next major. |
| `VO0029` | Both the `Minimum` (or `Maximum`) option and its hook on one type. | Remove `Minimum = "…"`: the hook replaces it, and wins until you do. Keep the static property. |
| `VO0030` | `IValueObjectMinimum<T>` or `IValueObjectMaximum<T>` that cannot bound the type: over a `string`, a `Guid`, a `bool` or an `[EntityId]`, which take no bound, or over another type than the underlying one. | The bound would be declared and never checked. Constrain a string with `MinLength`, `MaxLength` or `IValueObjectPatternValidator`; check a `Guid` or a `bool` in `ValidateValue`, or remove the hook; an `[EntityId]`'s format is fixed, and anything more goes in `ValidateValue`. Otherwise implement the hook over the underlying type itself, `IValueObjectMinimum<long>` on a `[ValueObject<long>]`. The type still generates, without the bound. |

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
  It derives from `FormatException`, whatever path throws it, so a `catch (FormatException)` catches it: put a
  `catch (ValueObjectException)` before one in the same `try`, or it is unreachable (CS0160). Its message names
  the type and the rule, never the value: `'Iban' rejected the supplied text: …` from `Parse`. `AttemptedValue`
  carries the raw value, which an exception logger records in clear, except on a type classified as personal data
  with an attribute derived from `DataClassificationAttribute`, where it is `null` (see `authoring.md`).
- `IsDefault` returning `true` — an instance equal to `default(TSelf)` crossed a boundary the analyzer cannot see
  (deserialization of a struct by another library, reflection, a default array element). Over a value type, a valid
  zero (`Amount.Create(0m)`, `Guid.Empty`) reads `true` as well, and `FluentValidation`'s `NotDefault` refuses it
  as `value_object.required`: guard with `NotDefault` over a `string`, or where the rules refuse the zero anyway.
- `The value to write is not a valid Iban: …` — a writer met an uninitialized instance (an entity property never
  set, a default array element, a message built from raw values) whose default value the type rejects, and refused
  it: `JsonException` from System.Text.Json, `JsonSerializationException` from Newtonsoft.Json, `DataException` from
  Dapper, `DbUpdateException` around a `ValueObjectException` from EF Core `SaveChanges`. Set the value, or declare
  the member `Iban?`: EF Core stores a refused optional one as `NULL`, and a `null` one is written as `null` or `NULL`
  everywhere.
- `'Iban' does not contain a definition for 'IsDefault'` (CS1061) — the generated implementation is explicit. Read
  it through `IValueObject<TSelf, TValue>`: a method constrained on it, or `((IValueObject<Iban, string>)iban).IsDefault`.
- A rejected value reported as `value_object.not_parsable` when you expected your own code — the text did not
  even have the shape of the underlying type, so no rule of yours was ever reached. `12,5` or `1,234.5` over a
  `decimal`, `double` or `float`, read with no provider or the invariant culture, is such text: pass the culture the
  text was written in. The `TypeConverter` reports it too for a number of another numeric type the underlying type
  cannot hold whole (`7.5` or `70000` for a `short`): it never truncates.
