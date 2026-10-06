---
title: Error Codes and Exceptions
sidebar_label: Error codes and exceptions
slug: /reference/errors
description: The stable error codes a rejected value carries, the ValidationResult that holds them, the exception Create throws, and how every integration's exception carries the code.
---

# Error codes and exceptions

A rejected value always carries two things: a stable, machine-readable **code**, and a human-readable
**message**. The code is the contract — it reaches the problem details of MVC controllers and minimal APIs,
FluentValidation failures and [every exception an integration throws](#the-code-in-an-exception), and a client may
branch on it. The
message is for people, and may change.

## Framework codes

Defined as constants on `ValueObjectErrorCodes`:

| Code | Raised when |
| --- | --- |
| `value_object.required` | The value is `null`, or an empty string on a type without `AllowEmpty`. |
| `value_object.too_short` | A string is shorter than `MinLength`. |
| `value_object.too_long` | A string is longer than `MaxLength`. |
| `value_object.invalid_format` | The value does not match the `Pattern` of `IValueObjectPatternValidator`; also the code of `ValidationResult.InvalidFormat`. |
| `value_object.out_of_range` | The value is below the bound of `IValueObjectMinimum<T>` or above that of `IValueObjectMaximum<T>`; also the code of `ValidationResult.OutOfRange`. |
| `value_object.not_a_known_value` | The value is not one of the known values of a closed set. |
| `value_object.not_parsable` | The text does not even have the shape of the underlying type, so no rule of the type ran. That includes a group separator in a `decimal`, a `double` or a `float` read with no provider or the invariant culture: `12,5` is not 125. The type converter reports it too for a number of another numeric type that the underlying type cannot hold whole: out of its range, or with a fraction for an integer. |

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

Thrown by `Create`, by `Parse`, and by an explicit conversion, when the value is rejected. It derives from
`FormatException`, the exception `IParsable<T>.Parse` documents for text it refuses, so code written against that
contract catches a value object's rejection. It carries:

| Member | |
| --- | --- |
| `ErrorCode` | The code of the violated rule, the same `TryCreate`, or for `Parse` the four-argument `TryParse`, would have reported. `Parse` throws `value_object.not_parsable` only for text that is not of the underlying type at all. |
| `ValueObjectType` | The value object that refused the value. |
| `AttemptedValue` | The value as it was passed in, before normalization, and the text for `Parse`. `null` on a value object [classified as personal data](#personal-data-in-an-exception). |
| `Message` | The type, then the message of the violated rule: `'Iban' rejected the supplied value: …` from `Create`, `'Iban' rejected the supplied text: …` from `Parse`, `'Quantity' rejected the supplied number: …` from the type converter, for a number its underlying type cannot hold. It never quotes the rejected value. |

Every path throws this one type, including `Create`, the arithmetic of a numeric value object and the other paths
that take a value rather than text: a value in range for `int` but outside the declared bounds raises a
`FormatException` too. A `catch (FormatException)` therefore catches it, and placed before a
`catch (ValueObjectException)` of the same `try`, it makes that clause unreachable, which is compile error CS0160:
put the `catch (ValueObjectException)` first.

### Personal data in an exception

The message of the exception names the type and the rule, never the value, on every path: a mistyped IBAN, email
address or telephone number does not reach the logs, traces and error reports that record a message. The
converters and the integrations name the type and the rule, and leave the value out, too.

`AttemptedValue` does hold the value. An exception logger that records the public properties of an exception, as
Serilog.Exceptions does, records it in clear. On a value object that holds personal data, classify the type with an
attribute of your own derived from `DataClassificationAttribute`, from Microsoft.Extensions.Compliance.Abstractions:
the generated `Create` and `Parse` then leave `AttemptedValue` `null`. The library recognizes the attribute by its
base, so it adds no dependency and no option to `[ValueObject<T>]`.

```csharp
using Microsoft.Extensions.Compliance.Classification;

public static class Taxonomy
{
    public static DataClassification Personal => new("Shop", nameof(Personal));
}

public sealed class PersonalDataAttribute : DataClassificationAttribute
{
    public PersonalDataAttribute() : base(Taxonomy.Personal) { }
}

[PersonalData]
[ValueObject<string>(MaxLength = 254, SchemaFormat = "email")]
public readonly partial struct EmailAddress : IValueObjectNormalizer<string>, IValueObjectPatternValidator
{
    public static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

// EmailAddress.Parse("ada@example") throws a ValueObjectException whose message names EmailAddress and the rule,
// and whose AttemptedValue is null.
```

Any attribute derived from `DataClassificationAttribute` classifies the type, `UnknownDataClassificationAttribute`
included; `NoDataClassificationAttribute`, which says the data is not sensitive, does not. The type converter of a
generic value object reads the same attribute on the generic definition. On a type left unclassified,
`AttemptedValue` carries the raw value, by design: it is what a caller debugging a rejection needs.

The integrations on a boundary report a rejected value in their own terms, so the exception is reserved for code
that treats a rejected value as a bug, for a strict EF Core read, and for an EF Core write
[refusing a value](#a-value-refused-on-write):

| Integration | A rejected value |
| --- | --- |
| The System.Text.Json converters | `ValueObjectJsonException`, a `JsonException` with the message of the rule, carrying the type and the code. |
| The Newtonsoft.Json converter | `JsonSerializationException`, with the message of the rule, carrying the code in its `Data`. |
| Newtonsoft.Json [without the converter](../how-to/json.md#without-the-converter) | `JsonSerializationException`, "Error converting value…", around an `ArgumentException` that names neither the rule nor its code. |
| ASP.NET Core MVC model binding | A model state error; the [problem details](../how-to/aspnet-core.md#problem-details-carrying-the-rule) carry its code, for a route, query, header or form value and for a value inside a JSON body, read by System.Text.Json, or by Newtonsoft.Json through [`AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson`](../how-to/aspnet-core.md#a-body-read-by-newtonsoftjson), keyed as MVC keys its errors; a body Newtonsoft.Json reads without that package records no code, and neither does a value object Newtonsoft.Json reads as a dictionary key, through its type converter. Neither package throws anything of its own. Without the package's binder, MVC binds through the type converter, which throws `ValueObjectException`, and reports it with the message it gives bad input for an `int`, such as "The value 'ZZ' is not valid.", and no code. |
| Minimal API parameter binding | A [bare 400](../how-to/aspnet-core.md#minimal-apis), naming neither the parameter nor the rule; in Development, a `BadHttpRequestException`, which `UseExceptionHandler` answers with a 500, and which wraps the converter's exception for a body. On the endpoints `AdCodicem.ValueObjects.AspNetCore.Http` covers, a [validation problem](../how-to/aspnet-core.md#problem-details-for-minimal-apis) carrying the code of each refused route, query or header value, and of a refused body where `ThrowOnBadRequest` is on, whose exception its handler answers; the package throws nothing of its own. |
| FluentValidation, `MustParseAs` and `MustSatisfy` | A validation failure carrying the code. |
| Dapper | `DataException`, carrying the code in its `Data`, for a value it cannot convert, and for text read into a value object over another type, or a number or a `Guid` read into one over `string`, that the value object refuses. |
| EF Core with `strict: true` | `ValueObjectException`, from `Create`: the query fails. |

The other reads do not validate. EF Core by default, and Dapper for a value the provider returns as the underlying
type or as its date and time counterpart, build the value object with `CreateUnchecked`: they read what this
application validated when it wrote it.
[EF Core](../how-to/ef-core.md#validation-on-read) says when to read strictly.

## A value refused on write

An instance that never went through `Create` — an entity property never set, a default array element, a message
built from raw values — holds the default value, which its type may reject. Every writer refuses such a value rather
than write it: a read trusts what it finds, so an empty `Iban` stored as it stands would come back from every later
read as an instance holding it, and a message carrying it would fault the service reading it.

Over a value type, the default and a constructed zero are the same instance, so the writer validates the default: a
type that accepts its zero, an `Amount` with a minimum of 0 or an unconstrained `Guid`, writes it, and a type that
must never hold `Guid.Empty` says so with a validator. Any other instance went through `Create`, and is written
without being validated again.

| Integration | A refused write |
| --- | --- |
| The System.Text.Json converters | `ValueObjectJsonException`, "The value to write is not a valid Iban: …", from a value and from a dictionary key. |
| The Newtonsoft.Json converter | `JsonSerializationException`, with the same message. |
| Dapper | `DataException`, with the same message, before the command is sent. Dapper hands the handler the value object whether the parameter is an `Iban` or an `Iban?` holding one, so the handler cannot tell whether the column takes a `NULL`, and refuses either way; an `Iban?` holding nothing is written as `NULL`. |
| EF Core, a property of the value object's type | `SaveChanges` throws a `DbUpdateException` whose inner exception is the `ValueObjectException`, with the same message, the code and the type, and no `AttemptedValue`. Nothing is written. |
| EF Core, an optional property (`Iban?`) | Nothing is thrown: the column takes a `NULL`, and stores one. |

The message names the type and the rule, never the value, and each exception carries the code of the rule, as
[below](#the-code-in-an-exception).

## The code in an exception

Every exception an integration throws for a value it refuses carries the code of the rule, in the exception type its
ecosystem expects, so that code which catches it can tell `value_object.too_short` from `iban.check_digits` without
reading English:

| Integration | Exception | The code |
| --- | --- | --- |
| The System.Text.Json converters: the generated one, the one for a value object written by hand, and `AnyEntityId`'s | `ValueObjectJsonException`, a sealed `JsonException` | `ErrorCode`, beside `ValueObjectType`, and in `Data` |
| The Newtonsoft.Json converter | `JsonSerializationException` | In `Data` |
| Dapper | `DataException` | In `Data` |
| EF Core, a refused write or a strict read | `ValueObjectException`, inside the `DbUpdateException` or the exception the query fails with | `ErrorCode` |

The code is the one the rule reports. A value that is not of the underlying type at all carries
`value_object.not_parsable`: a JSON token of the wrong kind, a number the underlying type cannot hold, text not of its
shape, a column the Dapper handler cannot convert. A `null` where a value object that cannot be `null` is expected
carries `value_object.required`: a JSON `null`, a SQL `NULL` read into an `Iban` rather than an `Iban?`. The messages
are the ones the exceptions carried before; where the System.Text.Json reader itself cannot read a token, as for a
number beyond the range of an `int`, the message is still the serializer's own, "The JSON value could not be converted
to …", followed by the path. No exception carries the refused value.

`ValueObjectErrors.TryGetCode` reads the code from any of these, and from an exception that wraps one: ASP.NET Core
wraps a body it cannot read in a `BadHttpRequestException`, and Entity Framework Core a refused write in a
`DbUpdateException`. It reads `ValueObjectException.ErrorCode`, then `ValueObjectJsonException.ErrorCode`, then
`Data` under `ValueObjectErrors.ErrorCodeKey`, on each exception of the chain in turn, and the first code found wins.
An exception no value object raised carries none.

```csharp skip
catch (Exception exception) when (ValueObjectErrors.TryGetCode(exception, out var code))
{
    logger.LogWarning(exception, "A value was refused: {Code}", code);
}
```

A logger that writes out the data of an exception finds the code there too, under the key
`AdCodicem.ValueObjects.ErrorCode`. An integration of your own stores a code the same way,
`exception.Data[ValueObjectErrors.ErrorCodeKey] = code`, and its exceptions are read like the library's.

### A code for gRPC

gRPC's error model takes a reason in upper snake case: `google.rpc.ErrorInfo.reason` and
`google.rpc.BadRequest.FieldViolation.reason` must match `[A-Z][A-Z0-9_]+[A-Z0-9]`, and an `ErrorInfo` reason is at
most 63 characters long. `ValueObjectErrorCodes.ToUpperSnakeCase` maps a code to one, the same way in every service,
so a client in another language branches on the result:

| Code | Reason |
| --- | --- |
| `value_object.too_long` | `VALUE_OBJECT_TOO_LONG` |
| `iban.check_digits` | `IBAN_CHECK_DIGITS` |

ASCII letters are upper-cased and digits kept, any run of other characters becomes one underscore, and an underscore
left at either end is dropped. A code that maps to no valid reason, one that starts with a digit or that comes out
longer than 63 characters, throws an `ArgumentException` rather than being truncated or given a prefix, which could
make two codes one.

## Detecting an uninitialized instance

`IsDefault` is `true` for an instance equal to `default(TSelf)`, such as one that crossed a boundary the `VO0010`
analyzer cannot see: a default array element or another library's deserializer.
[Where a default instance can come from](./default-instances.md) lists the sources observed, and what closes each.
Over a `string`, that is exactly an instance that never went through `Create`. Over a value type, a constructed
instance holding the type's zero (`Amount.Create(0m)`, `OrderId.Create(Guid.Empty)`) equals the default too and reads
`true`; only the analyzer and validation tell them apart. FluentValidation's
[`NotDefault`](../how-to/fluentvalidation.md#an-uninitialized-value-object) rule checks it at the edge, and so refuses
a valid zero. The writers do not stop at it: they validate the default, and refuse only the value its type rejects
([above](#a-value-refused-on-write)).

A generated value object implements it explicitly, as a member of `IValueObject<TSelf, TValue>`, so a tool reading
the public properties of the type (a logger destructuring it, a schema generator, an exporter) does not publish it.
Generic code constrained on the interface reads it without boxing; code holding the concrete type casts to the
interface, `((IValueObject<Iban, string>)iban).IsDefault`, which boxes.
