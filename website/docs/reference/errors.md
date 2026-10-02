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
| `value_object.invalid_format` | The value does not match the `Pattern` of `IValueObjectPatternValidator`, or of the deprecated option of the same name; also the code of `ValidationResult.InvalidFormat`. |
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

The integrations on a boundary report a rejected value in their own terms, so the exception is reserved for code
that treats a rejected value as a bug, and for a strict EF Core read:

| Integration | A rejected value |
| --- | --- |
| The System.Text.Json converters | `JsonException`, with the message of the rule. |
| The Newtonsoft.Json converter | `JsonSerializationException`, with the message of the rule. |
| ASP.NET Core model binding | A model state error; the [problem details](../how-to/aspnet-core.md#problem-details-carrying-the-rule) carry its code. |
| FluentValidation, `MustParseAs` and `MustSatisfy` | A validation failure carrying the code. |
| Dapper | `DataException`, for a value it cannot convert, and for text read into a value object over another type, or a number or a `Guid` read into one over `string`, that the value object refuses. |
| EF Core with `strict: true` | `ValueObjectException`, from `Create`: the query fails. |

The other reads do not validate. EF Core by default, and Dapper for a value the provider returns as the underlying
type or as its date and time counterpart, build the value object with `CreateUnchecked`: they read what this
application validated when it wrote it.
[EF Core](../how-to/ef-core.md#validation-on-read) says when to read strictly.

## Detecting an uninitialized instance

`IsDefault` is `true` for an instance that was never constructed: one that crossed a boundary the `VO0010`
analyzer cannot see, such as a default array element or another library's deserializer. FluentValidation's
[`NotDefault`](../how-to/fluentvalidation.md#an-uninitialized-value-object) rule checks it at the edge.
