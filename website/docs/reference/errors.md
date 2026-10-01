---
title: Error Codes and Exceptions
sidebar_label: Error codes and exceptions
slug: /reference/errors
description: The stable error codes a rejected value carries, the ValidationResult that holds them, and the exception Create throws.
---

# Error codes and exceptions

A rejected value always carries two things: a stable, machine-readable **code**, and a human-readable
**message**. The code is the contract — it reaches problem details responses and FluentValidation failures, and
a client may branch on it. The message is for people, and may change.

## Framework codes

Defined as constants on `ValueObjectErrorCodes`:

| Code | Raised when |
| --- | --- |
| `value_object.required` | The value is `null`, or an empty string on a type without `AllowEmpty`. |
| `value_object.too_short` | A string is shorter than `MinLength`. |
| `value_object.too_long` | A string is longer than `MaxLength`. |
| `value_object.invalid_format` | The value does not match `Pattern`; also the code of `ValidationResult.InvalidFormat`. |
| `value_object.out_of_range` | The value is below `Minimum` or above `Maximum`; also the code of `ValidationResult.OutOfRange`. |
| `value_object.not_a_known_value` | The value is not one of the known values of a closed set. |
| `value_object.not_parsable` | The text does not even have the shape of the underlying type, so no rule of the type ran. |

## Codes of your own

A validator hook returns `ValidationResult.Failure("delivery_date.sunday", "Nothing is delivered on a Sunday.")`
for a rule of its own. Prefer a specific code of your own over a framework code that means something else: a
client can act on `delivery_date.sunday`, not on a generic `value_object.invalid_format`.

## `ValidationResult`

A `readonly struct` returned by `Validate`, by `TryCreate` and `TryParse` through their `out` parameter, and by
validator hooks.

| Member | |
| --- | --- |
| `static ValidationResult Success` | The success state, which is `default`: accepting a value allocates nothing. |
| `bool IsValid` | `true` on success. |
| `string? ErrorCode`, `string? ErrorMessage` | The code and message of the rule that fired, `null` on success. |
| `static ValidationResult Failure(string errorCode, string errorMessage)` | A rejection with a code of your own. |
| `Required`, `InvalidFormat`, `OutOfRange` | Rejections with the framework code and an optional message. |

Validation is fail-fast: a result carries one reason, the first rule that failed.

## `ValueObjectException`

Thrown by `Create`, by `Parse`, and by an explicit conversion, when the value is rejected. It carries:

| Member | |
| --- | --- |
| `ErrorCode` | The code of the violated rule, the same `TryCreate`, or for `Parse` the four-argument `TryParse`, would have reported. `Parse` throws `value_object.not_parsable` only for text that is not of the underlying type at all. |
| `ValueObjectType` | The value object that refused the value. |
| `AttemptedValue` | The value as it was passed in, before normalization. |
| `Message` | The message of the violated rule, after the text and the type for `Parse`. |

Every integration on a boundary — JSON, model binding, EF Core, Dapper — uses `TryCreate` instead, so the
exception is reserved for code that treats a rejected value as a bug.

## Detecting an uninitialized instance

`IsDefault` is `true` for an instance that was never constructed: one that crossed a boundary the `VO0010`
analyzer cannot see, such as a default array element or another library's deserializer. FluentValidation's
[`NotDefault`](../how-to/fluentvalidation.md#an-uninitialized-value-object) rule checks it at the edge.
