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

Most rules are shapes: a length, a pattern, a range. Those are declared on the attribute.

```csharp
[ValueObject<string>(MinLength = 8, MaxLength = 8, Pattern = "^[A-Z]{3}-[0-9]{4}$")]
public readonly partial struct ProductCode;
```

`ProductCode.TryCreate("ABC-1234", out _)` succeeds, while `"ABC-12"` fails with `value_object.too_short` and
`"abc-1234"` with `value_object.invalid_format`. A declared rule does more than validate: `MaxLength` also
sizes the EF Core column and `MaxLength`, `MinLength` and `Pattern` all appear in the OpenAPI schema, so the
rule is stated once for every boundary.

Numbers, dates and times take bounds instead. They are written as invariant-culture text, so a `decimal` or a
`DateOnly` keeps its full precision, and they are parsed at compile time — a bound that does not parse is
`VO0004`:

```csharp
[ValueObject<int>(Minimum = "1", Maximum = "999")]
public readonly partial struct Quantity;
```

## Normalizing what comes in

`"abc-1234"` is plainly the same product as `"ABC-1234"`. Rejecting it would be pedantic; accepting both
spellings as different values would be worse. Normalization brings every spelling to one:

```csharp
[ValueObject<string>(MinLength = 8, MaxLength = 8, Pattern = "^[A-Z]{3}-[0-9]{4}$")]
public readonly partial struct ProductCode : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
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
[ValueObject<DateOnly>(Minimum = "2020-01-01")]
public readonly partial struct DeliveryDate : IValueObjectValidator<DateOnly>
{
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
4. `Pattern`.
5. `Minimum`, then `Maximum`.
6. Membership of a closed set of [known values](./known-values.md).
7. `ValidateValue`, if the type declares it.

So a validator only ever sees a value that already satisfies every declared rule. The IBAN check-digit
validator on the [introduction](../introduction.md) indexes into the value without checking its length first,
because `MinLength = 15` has already run.

Because validation is fail-fast, a rejection carries exactly one reason: the first rule that failed.

## When the interface is missing

A hook is found through its interface, not its name. Write `NormalizeValue` without declaring
`IValueObjectNormalizer<string>` and the code compiles, but the generator never calls it. The `VO0011` warning
reports exactly that mistake.

Next: [Known values](./known-values.md), for types whose accepted values are a fixed list.
