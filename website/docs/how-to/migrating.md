---
title: Migrate From Another Library
sidebar_label: Migrate from another library
slug: /how-to/migrating
description: Move to AdCodicem.ValueObjects from bare primitives, hand-written value objects, Vogen, StronglyTypedId or Thinktecture.Runtime.Extensions, one type at a time.
---

# Migrate from another library

A migration can go one type at a time: a value object from this library sits next to a primitive or another
library's type without either noticing. The wire format does not change either — every library on this page
writes a single-value object as its bare underlying value — so API clients see nothing.

After converting a type, point the [contract kit](./test-value-objects.md) at it with the values your old tests
used: it checks that the new type accepts and rejects what the old one did, and round-trips through text and
JSON the same way.

## From bare primitives

This is the common case, and the one the library exists for.

1. **Declare the type**, moving the rules scattered through validators and controllers onto it: lengths on
   the attribute, bounds in `IValueObjectMinimum<T>` and `IValueObjectMaximum<T>`, a pattern in
   `IValueObjectPatternValidator`, the rest in `ValidateValue`, any trimming or upper-casing in `NormalizeValue`.
2. **Change the entity and the contracts**, property by property. JSON bodies, route segments and query strings
   keep the same shape.
3. **Map it in EF Core** with `ConfigureValueObjects`. Then read the next migration carefully: a `MaxLength` on the
   type now sizes the column, so a column that was unbounded becomes bounded. That is usually the point, but it
   is a schema change, and existing rows must fit.
4. **Delete what the type now guarantees**: the format checks in validators, the `Trim()` calls, the
   `[MaxLength]` and `[RegularExpression]` attributes, the OpenAPI annotations. For payloads that still carry raw
   text, [`MustParseAs`](./fluentvalidation.md) replaces them without restating the rules.

## From hand-written value objects

Keep the rules, delete the plumbing.

- Move the checks into `NormalizeValue` and `ValidateValue`, and turn each thrown exception into a returned
  `ValidationResult` with a code of your own.
- Delete the constructor, `Equals`, `GetHashCode`, the operators, `ToString`, `Parse`, and every hand-written
  `JsonConverter`, `TypeConverter`, EF Core converter and model binder: the generator writes all of them.
- A `record struct` or a class becomes a `readonly partial struct`. Where a class could be `null`, use `T?`.

## From Vogen

| Vogen | AdCodicem.ValueObjects |
| --- | --- |
| `[ValueObject<string>] partial class` or `struct` | `[ValueObject<string>] readonly partial struct` |
| `private static string NormalizeInput(string input)` | `public static string NormalizeValue(string value)`, with `IValueObjectNormalizer<string>` |
| `private static Validation Validate(string input)` | `public static ValidationResult ValidateValue(in string value)`, with `IValueObjectValidator<string>` |
| `Validation.Ok` / `Validation.Invalid("…")` | `ValidationResult.Success` / `ValidationResult.Failure("code", "…")` |
| `Iban.From(raw)` | `Iban.Create(raw)` |
| `Iban.TryFrom(raw, out var iban)` | `Iban.TryCreate(raw, out var iban)` |
| `ValueObjectOrError<Iban> result = Iban.TryFrom(raw)` | `Iban.TryCreate(raw, out var iban, out var validation)` |
| `ValueObjectValidationException` | `ValueObjectException`, which also carries `ErrorCode` |
| Explicit casts, both ways by default | Opt in with `ExplicitConversionFromValue` and `ImplicitConversionToValue` |
| `Conversions.EfCoreValueConverter`, `HasVogenConversion()` | `ConfigureValueObjects(assembly)`, once |
| `Conversions.DapperTypeHandler` | `ValueObjectDapper.AddValueObjectHandlers(assembly)`, once |
| `Conversions.NewtonsoftJson` | `ValueObjectConverter` in the serializer settings |
| `new VogenTypesFactory()` in the options of a source-generated context | `[JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]` |
| A length or pattern check inside `Validate` | `MinLength`, `MaxLength` on the attribute; a `[GeneratedRegex]` through `IValueObjectPatternValidator` |

Side by side, a normalized and validated IBAN:

```csharp skip
// Vogen
[ValueObject<string>(conversions: Conversions.Default | Conversions.EfCoreValueConverter)]
public partial class Iban
{
    private static string NormalizeInput(string input) => input.Replace(" ", "").ToUpperInvariant();

    private static Validation Validate(string input)
        => input.Length is >= 15 and <= 34
            ? Validation.Ok
            : Validation.Invalid("An IBAN has between 15 and 34 characters.");
}
```

```csharp
// AdCodicem.ValueObjects
[ValueObject<string>(MinLength = 15, MaxLength = 34)]
public readonly partial struct Iban : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Replace(" ", "").ToUpperInvariant();
}
```

The length check needs no code any more, and it now sizes the column and documents the schema too.

Three differences to plan for:

- **EF Core reads.** Vogen validates values read from the database by default; this library does not. If other
  systems write to your tables, keep that behaviour with `ConfigureValueObjects(strict: true, …)`.
- **Instances.** A Vogen instance may deliberately hold a value that `Validate` would refuse, as a sentinel. There
  is no such escape hatch here: express absence as `Iban?`, and name valid values with
  [`[KnownValue]`](../tutorials/known-values.md).
- **Underlying types.** Vogen wraps any type; this library supports 22. A value object over a `Uri` or a type of
  your own has no direct equivalent.

## From StronglyTypedId

| StronglyTypedId | AdCodicem.ValueObjects |
| --- | --- |
| `[StronglyTypedId] partial struct OrderId` (a `Guid`) | `[ValueObject<Guid>] readonly partial struct OrderId` |
| `[StronglyTypedId(Template.String)]` | `[ValueObject<string>]` |
| `new OrderId(guid)` | `OrderId.Create(guid)`: the constructor is private, so the rules cannot be skipped |
| `OrderId.New()` | A `New()` of your own, below — or an [`[EntityId]`](../tutorials/public-identifiers.md) for an identifier clients see |
| `OrderId.Empty` | `OrderId?` for absence; `default(OrderId)` is now a build error |
| `new OrderId.EfCoreValueConverter()`, one per type | `ConfigureValueObjects(assembly)`, once |
| `SqlMapper.AddTypeHandler(new OrderId.DapperTypeHandler())`, one per type | `ValueObjectDapper.AddValueObjectHandlers(assembly)`, once |
| Converters passed to a source-generated context's options | `[JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]` |

```csharp
[ValueObject<Guid>]
public readonly partial struct OrderId : IValueObjectValidator<Guid>
{
    public static OrderId New() => CreateUnchecked(Guid.CreateVersion7());

    public static ValidationResult ValidateValue(in Guid value)
        => value == Guid.Empty
            ? ValidationResult.Required("An order identifier must not be empty.")
            : ValidationResult.Success;
}
```

The validator is new: StronglyTypedId has no validation, so an empty `Guid` was a valid identifier. Once the
types are converted, remove the `StronglyTypedId` and `StronglyTypedId.Templates` packages and any `.typedid`
templates.

## From Thinktecture.Runtime.Extensions

- **The validation hook splits in two.** `ValidateFactoryArguments(ref ValidationError? validationError, ref string value)`
  both normalizes, by assigning `value`, and validates. Move the assignment into `NormalizeValue` and the checks
  into `ValidateValue`, returning a `ValidationResult` instead of setting an error.
- **String comparison changes.** Thinktecture compares string keys case-insensitively by default; this library
  compares ordinally. To keep the behaviour, declare `Comparison = StringComparison.OrdinalIgnoreCase` — or,
  better, [normalize the case](./string-comparison.md) so the stored value is canonical.
- **A class becomes a `readonly partial struct`.** `null` checks become `T?`.
- **EF Core.** `UseThinktectureValueConverters()` becomes `ConfigureValueObjects(assembly)` in
  `ConfigureConventions`. Both skip validation on read by default.
- **Smart enums and unions stay where they are.** Only single-value value objects have an equivalent here; a
  closed set of codes can become a value object with [known values](../tutorials/known-values.md).

## From the `Pattern` option of an earlier version

The `Pattern` option of `[ValueObject<T>]` is deprecated and removed in the next major version. It builds its
`Regex` at run time, which native AOT interprets, and the compiler reports each use as `VO0021`: a warning, so an
error under `TreatWarningsAsErrors`. Move each pattern to `IValueObjectPatternValidator`, one type at a time.

```csharp skip
[ValueObject<string>(MinLength = 15, MaxLength = 34, Pattern = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
public readonly partial struct Iban;
```

becomes:

```csharp
using System.Text.RegularExpressions;

[ValueObject<string>(MinLength = 15, MaxLength = 34)]
public readonly partial struct Iban : IValueObjectPatternValidator
{
    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}
```

Copy the expression as it was, and keep `RegexOptions.CultureInvariant` and the timeout of 1000 milliseconds:
they are what the option used, so the type accepts and rejects the same values, with the same
`value_object.invalid_format` and the same message, and its OpenAPI `pattern` is unchanged. The contract kit,
pointed at the type with the values its tests already use, checks it. Leaving `Pattern = "..."` beside the hook
is `VO0022`; [Diagnostics](../reference/diagnostics.md#moving-off-pattern) has the rest.
