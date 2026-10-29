---
title: Validation and Normalization
sidebar_label: Validation and normalization
slug: /tutorials/validation-and-normalization
description: Declare rules on the attribute, add normalization and validation hooks, and follow the order in which a value object runs them.
---

# Validation and normalization

A value object is only worth having if it refuses what it should refuse. This tutorial builds up the rules of
two types, one step at a time, and ends with the order in which they run.

## Rules that need no code

Most rules are shapes: a length, a range. A length is declared on the attribute.

```csharp
[ValueObject<string>(MinLength = 8, MaxLength = 8)]
public readonly partial struct ProductCode;
```

`ProductCode.TryCreate("ABC-1234", out _)` succeeds, while `"ABC-12"` fails with `value_object.too_short`. A
declared rule does more than validate: `MaxLength` also sizes the EF Core column, and `MinLength` and
`MaxLength` both appear in the OpenAPI schema, so the rule is stated once for every boundary.

Numbers, dates and times take bounds instead. A bound is a value of the underlying type, declared through
`IValueObjectMinimum<T>` and `IValueObjectMaximum<T>`, so the compiler checks its type and any expression of that
type can build it:

```csharp
[ValueObject<int>]
public readonly partial struct Quantity : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    public static int Minimum => 1;

    public static int Maximum => 999;
}
```

`Quantity.TryCreate(0, out _)` fails with `value_object.out_of_range`, and the OpenAPI schema publishes both bounds
as `minimum` and `maximum`. A bound is a constant, written as an expression-bodied property; a bound relative to the
clock is a rule, and belongs in a validator, [below](#rules-that-need-code). The `Minimum` and `Maximum`
options of `[ValueObject<T>]` once did this job, with the bound written as text; they no longer compile (`VO0028`).

## A pattern

A format is a shape too, but a regular expression is compiled by the .NET regex source generator, which only
reads code a person wrote: the value object generator cannot write it for you. A pattern is therefore declared
through `IValueObjectPatternValidator`, as a `[GeneratedRegex]` property named `Pattern`:

```csharp
[ValueObject<string>(MinLength = 8, MaxLength = 8)]
public readonly partial struct ProductCode : IValueObjectPatternValidator
{
    [GeneratedRegex("^[A-Z]{3}-[0-9]{4}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}
```

Now `"abc-1234"` fails with `value_object.invalid_format`. The file needs `using System.Text.RegularExpressions;`,
which is not among the implicit usings. The rule is still stated once: the value object generator reads the
pattern's text off `[GeneratedRegex]` when the type compiles, and the OpenAPI schema publishes it beside the
lengths. Keep both arguments: `CultureInvariant` makes the match independent of the culture, and without
`matchTimeoutMilliseconds` a pathological input could hold a thread, which `VO0026` reports. The
[authoring reference](../authoring-guide.md#a-pattern) covers the rest. The `Pattern` option of
`[ValueObject<T>]` once did this job; it built its regular expression at run time, and no longer compiles (`VO0021`).

## Normalizing what comes in

`"abc-1234"` is plainly the same product as `"ABC-1234"`. Rejecting it would be pedantic; accepting both
spellings as different values would be worse. Normalization brings every spelling to one:

```csharp
[ValueObject<string>(MinLength = 8, MaxLength = 8)]
public readonly partial struct ProductCode : IValueObjectNormalizer<string>, IValueObjectPatternValidator
{
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();

    [GeneratedRegex("^[A-Z]{3}-[0-9]{4}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}
```

Normalization runs **before** validation, so the pattern only ever sees upper case, and the stored value is
canonical: equality, hashing, a unique index and a `GROUP BY` all agree without anyone passing a comparer.

A normalizer has three obligations:

- **It is idempotent.** Normalizing a normalized value changes nothing; the contract kit checks it.
- **It never rejects.** A value that cannot be normalized is left as it is, for validation to refuse.
- **It is never called with `null`.** The generated `Normalize` guards the null before calling your method.

For a `string` value object on a hot path, `IValueObjectSpanNormalizer` lets parsing and JSON reading normalize
straight from the incoming text; the [authoring reference](../authoring-guide.md#hooks) shows how.

## Rules that need code

Some rules are not shapes. A delivery date must not fall on a Sunday; an IBAN must have correct check digits.
Those go in a validator:

```csharp
[ValueObject<DateOnly>]
public readonly partial struct DeliveryDate : IValueObjectMinimum<DateOnly>, IValueObjectValidator<DateOnly>
{
    public static DateOnly Minimum => new(2020, 1, 1);

    public static ValidationResult ValidateValue(in DateOnly value)
        => value.DayOfWeek == DayOfWeek.Sunday
            ? ValidationResult.Failure("delivery_date.sunday", "Nothing is delivered on a Sunday.")
            : ValidationResult.Success;
}
```

`ValidationResult` is a `readonly struct` whose success state is `default`, so accepting a value allocates
nothing. Rejecting one is a return value, never an exception: the validator must not throw.

The factories cover the common cases — `Required`, `InvalidFormat`, `OutOfRange`, `TooShort`, `TooLong` — and
reuse the framework's error codes. For a rule of your own, use `Failure` with a code of your own, as above. The
code is what an API client branches on, so it should be stable and specific: `delivery_date.sunday` tells a
client something `value_object.invalid_format` does not.

## The order things run in

Every entry point — `Create`, `TryCreate`, `Parse`, `TryParse`, JSON, model binding — runs the same sequence, and
stops at the first rule that fails:

1. `NormalizeValue`, if the type declares it (a `null` goes past it untouched).
2. `null` is rejected with `value_object.required`, and so is an empty string unless `AllowEmpty = true` — which
   includes a string that normalization emptied, such as `"   "` after a `Trim`.
3. `MinLength`, then `MaxLength`.
4. The pattern of `IValueObjectPatternValidator`.
5. The bound of `IValueObjectMinimum<T>`, then of `IValueObjectMaximum<T>`.
6. Membership of a closed set of [known values](./known-values.md).
7. `ValidateValue`, if the type declares it.

So a validator only ever sees a value that already satisfies every declared rule. The IBAN check-digit
validator on the [introduction](../introduction.md) indexes into the value without checking its length first,
because `MinLength = 15` has already run.

Because validation is fail-fast, a rejection carries exactly one reason: the first rule that failed.

## When the interface is missing

A hook is found through its interface, not its name. Write `NormalizeValue` without declaring
`IValueObjectNormalizer<string>` and the code compiles, but the generator never calls it. The `VO0011` warning
reports exactly that mistake. It reports a public static `Regex Pattern` written without
`IValueObjectPatternValidator` too, unless the type implements another hook, which may run it itself.

Next: [Known values](./known-values.md), for types whose accepted values are a fixed list.
