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
| EF Core with `strict: true` | `ValueObjectException`, from `Create`, for a column or an element of a [collection](../how-to/ef-core.md#collections-of-value-objects): the query fails. |
| MongoDB.Driver, through [`AdCodicem.ValueObjects.MongoDB`](../how-to/mongodb.md#reading) | `FormatException`, "The value read is not a valid Iban: …", carrying the code in its `Data`, for a value the type refuses unless the serializer is trusted, and, whatever the trust, for a BSON `null` or a value the serializer of the underlying type cannot read; inside the driver's own `FormatException`, naming the member and its class, when it reads a document. |
| MessagePack, through [`AdCodicem.ValueObjects.MessagePack`](../how-to/messagepack.md#reading) | `MessagePackSerializationException`, "The value read is not a valid Iban: …", carrying the code in its `Data`, for a value the type refuses unless the formatter is trusted, and, whatever the trust, for a `nil` or a value the formatter of the underlying type cannot read; inside MessagePack's own `MessagePackSerializationException`, "Failed to deserialize … value.". A dictionary whose keys normalize to one value object fails with MessagePack's own `ArgumentException`, which carries no code and quotes the key ([value-object keys](../how-to/messagepack.md#security-and-value-object-keys)). Through [SignalR](../how-to/messagepack.md#signalr), the hub method is not invoked: the client gets a `HubException` that names neither the rule nor its code, and the server logs the binding failure at `Debug`, with the code in the exception's chain. |
| `XmlSerializer` and `DataContractSerializer`, in an assembly marked `[assembly: ValueObjectXmlSerialization]` | `XmlException`, with the message of the rule and the line of the element, carrying the code in its `Data`, inside `XmlSerializer`'s `InvalidOperationException` ("There is an error in XML document …") or `DataContractSerializer`'s `SerializationException`: [XML](../how-to/xml.md#reading). |
| A Model Context Protocol tool whose result holds a value its type refuses | The converter's `ValueObjectJsonException` reaches the SDK, which answers the call with `An error occurred invoking 'get_iban'.`, with or without `AdCodicem.ValueObjects.ModelContextProtocol`, which checks arguments, not results. |
| A Model Context Protocol tool result that a client validates against the tool's `outputSchema` | The client's own refusal, for a value its type accepts and the schema, written for what a model sends, does not: another spelling of a known value of a closed set looked up ignoring case, or text whose characters outside the Basic Multilingual Plane bring it under `minLength` ([what it does not cover](../how-to/language-models.md#what-it-does-not-cover)). |
| A tool argument, through [`AdCodicem.ValueObjects.AI`](../how-to/language-models.md#a-refused-argument)'s `WithValueObjectValidation()` | Nothing is thrown: the function returns `{"error":"invalid_argument","argument":…,"code":…,"message":…}`, which `FunctionInvokingChatClient` hands the model as it is, with the converter's message, or `A value is required.` and `value_object.required` for an absent argument or a `null` sent to a value object that cannot be `null`. Where the converter writes no message, for a number the underlying type cannot hold, and where a converter of the application's own, or the underlying contract of a value object written by hand, refuses with an exception of its own, the message is "The value is not a valid Quantity.", and the code the one that exception carries in its `Data`, or `value_object.not_parsable`. Without the wrapper, the converter's `ValueObjectJsonException` reaches `FunctionInvokingChatClient`, which answers the model "Error: Function failed.", followed by the message alone under `IncludeDetailedErrors`. |
| A structured answer, read through `ChatResponse<T>.Result` | The converters' `ValueObjectJsonException`, whose code the application can ask again with ([structured output](../how-to/language-models.md#structured-output)). |
| A Model Context Protocol tool argument, through [`AdCodicem.ValueObjects.ModelContextProtocol`](../how-to/language-models.md#a-refused-argument-as-a-tool-execution-error)'s `WithValueObjectTools` | Nothing is thrown: the server answers a tool execution error, `isError: true`, whose text is `Argument 'quantity' rejected (value_object.out_of_range): …`, and whose structured content is the object above when the tool declares no output schema, its code and message read the same way. Without the package, the SDK answers `An error occurred invoking 'place_order'.`, and the rule and its code are lost. |

The other reads do not validate. EF Core by default, Dapper for a value the provider returns as the underlying type or
as its date and time counterpart, a trusted MongoDB serializer and a trusted MessagePack formatter build the value
object with `CreateUnchecked`: they read what this application validated when it wrote it. A trusted MongoDB serializer
still refuses a BSON `null` and a value the serializer of the underlying type cannot read, which no instance can hold,
and a trusted MessagePack formatter a `nil` and a value the formatter of the underlying type cannot read.
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
| EF Core, an element of a [collection](../how-to/ef-core.md#collections-of-value-objects) of the value object (`List<Iban>`) | As for a property of the value object's type: the `DbUpdateException` around the `ValueObjectException`, and nothing is written. |
| EF Core, an element of a collection of an optional value object over text (`List<Iban?>`) | Nothing is thrown: the element is stored as a JSON `null`, a `NULL` in a PostgreSQL array. |
| EF Core, an element of a collection of an optional value object over a value type (`List<Quantity?>`) | As in a `List<Quantity>`: the `DbUpdateException` around the `ValueObjectException`. Entity Framework Core cannot write the `null` a converter would hand its JSON writer there. |
| MongoDB.Driver, through [`AdCodicem.ValueObjects.MongoDB`](../how-to/mongodb.md#writing) | `BsonSerializationException`, with the same message: from `InsertOne`, `InsertMany` or `ReplaceOne`, inside the driver's own, which names the member; from an update or a query constant, as it is. An `Iban?` holding a default `Iban` is refused too; an `Iban?` holding nothing is written as `null`. Nothing is written. An update through `.Value`, `Update.Set(x => x.Page.Value, 0)`, writes the raw value unchecked: set the value object instead. |
| MessagePack, through [`AdCodicem.ValueObjects.MessagePack`](../how-to/messagepack.md#writing) | `MessagePackSerializationException`, with the same message, inside MessagePack's own, "Failed to serialize … value.", from a value, a dictionary key, and an `Iban?` holding a default `Iban`; an `Iban?` holding nothing is written as `nil`. Through [SignalR](../how-to/messagepack.md#signalr), a .NET client's invocation throws it, and nothing is sent; a hub method's result fails to be written, the server logs it, and closes the connection. |
| `XmlSerializer` and `DataContractSerializer`, in an assembly marked `[assembly: ValueObjectXmlSerialization]` | `XmlException`, "The value to write is not a valid Quantity: …", inside `XmlSerializer`'s `InvalidOperationException` ("There was an error generating the XML document.") or `DataContractSerializer`'s `SerializationException`. |

The message names the type and the rule, never the value, and each exception carries the code of the rule, as
[below](#the-code-in-an-exception).

`AdCodicem.ValueObjects.MongoDB` also refuses a configuration that would lose values, with exceptions that carry no
code. At start-up, `ValueObjectBson.Register` throws an `InvalidOperationException` for a value object over `Guid`
while the serializer of `Guid` has no representation, for a value object the driver has already mapped through a class
map, and when it is called again with the other trust; `Register<TSelf, TValue>()` throws an
`InvalidOperationException` for the first, a `NotSupportedException` for a value object over `Int128` or `UInt128`
with no serializer of the application's own for its underlying type, and the driver's `BsonSerializationException`
when a different serializer is already registered for the type
([what `Register` checks](../how-to/mongodb.md#what-register-checks)). On each document, a value object over `Int128`
or `UInt128` throws a `BsonSerializationException` when it is written or read, unless the application registered a
serializer for its underlying type, and one over `Guid` throws one when it is written, or read from binary, through a
serializer of `Guid` with no representation
([how each underlying type is stored](../how-to/mongodb.md#how-each-underlying-type-is-stored)).
`ValueObjectBsonSchema.For<TDocument>()` throws an `InvalidOperationException` when it is called before
`ValueObjectBson.Register`, or for a document the driver serializes through a serializer of its own rather than a class
map.

A collection given the [validator built from the rules](../how-to/mongodb.md#a-collection-validator-from-the-rules)
refuses another writer's document on the server, and the driver reports it as for any validator: a
`MongoWriteException` from `InsertOne`, `ReplaceOne` or an update, a `MongoBulkWriteException` from `InsertMany`,
whose write error has code 121, "Document failed validation", and details naming the field and the keyword it failed,
`pattern`, `maximum`, `enum`, `required`. The value it refused is in those details, as the server reports it, and no
rule code is: the server checks the keywords, not the value object.

`AdCodicem.ValueObjects.MessagePack` also lets through exceptions that carry no code. A read or a write through options
whose resolver holds no formatter for the underlying type throws MessagePack's `FormatterNotRegisteredException`: a
misconfiguration, not a value refused. Under `MessagePackSecurity.UntrustedData`, which SignalR's hub protocol sets,
MessagePack refuses a dictionary keyed by a value object, or a set of value objects, with a `TypeAccessException`, and a
dictionary whose keys normalize to one value object fails with MessagePack's `ArgumentException`, which quotes the key
([value-object keys](../how-to/messagepack.md#security-and-value-object-keys)). An array or a map that announces more
elements than bytes remain is refused by MessagePack's reader with an `EndOfStreamException`, before any value object
is read. Each comes inside MessagePack's own `MessagePackSerializationException` when the serializer reads or writes.

A log is no such write: nothing reads one back into a value object. The
[Serilog integration](../how-to/logging.md#serilog) logs a default instance as the default of its underlying type,
`0`, `""` or `false`, and throws nothing, whatever it logs.

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
| MongoDB.Driver, a read or a refused write | `FormatException` on read, `BsonSerializationException` on write, inside the driver's exception of the same type, naming the member, when it reads or writes a document | In `Data` |
| MessagePack, a read or a refused write | `MessagePackSerializationException`, inside MessagePack's own of the same type when the serializer reads or writes a value | In `Data` |
| `XmlSerializer` and `DataContractSerializer`, in an assembly that opts into XML serialization | `XmlException`, inside the serializer's `InvalidOperationException` or `SerializationException` | In `Data` |

The code is the one the rule reports. A value that is not of the underlying type at all carries
`value_object.not_parsable`: a JSON token of the wrong kind, a number the underlying type cannot hold, text not of its
shape, a column the Dapper handler cannot convert, an XML element holding elements or no text of the underlying type, a
BSON value of a type, or beyond a range, the serializer of the underlying type cannot read, a MessagePack value of
another type, beyond a range, not of the underlying type's shape, or truncated. A `null` where a value object that
cannot be `null` is expected carries `value_object.required`: a JSON `null`, a SQL `NULL`, a BSON `null` or a
MessagePack `nil` read into an `Iban` rather than an `Iban?`, an element
marked `xsi:nil` that `XmlSerializer` hands a member that cannot be `null` (`DataContractSerializer` refuses that one
itself, without a code). The messages
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

A tool argument refused through `AdCodicem.ValueObjects.AI` throws nothing: the code is the `code` of the result the
model reads ([language models](../how-to/language-models.md#a-refused-argument)). Where reflection-based serialization
is off and the serializer options have no contract for a type, `AIFunctionFactory` and
`ValueObjectResponseFormat.ForJsonSchema<T>()` throw a `NotSupportedException`, which carries no code: the options lack
a source-generated context ([native AOT](../how-to/language-models.md#native-aot-and-reflection-free-serialization)).
A tool argument refused through `AdCodicem.ValueObjects.ModelContextProtocol` throws nothing either: the code is in the
text of the tool execution error, and in its structured content when the tool declares no output schema
([Model Context Protocol servers](../how-to/language-models.md#a-refused-argument-as-a-tool-execution-error)). Where
reflection-based serialization is off and the server's serializer options have no contract for a parameter or a
result, the SDK throws a `NotSupportedException` when the server is first resolved
([without reflection](../how-to/language-models.md#without-reflection)).

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

## A value the test-data sampler cannot draw

`ValueObjectSampler.Next<TSelf, TValue>()` and `ValueObjectSampler.Next(descriptor)`, in
`AdCodicem.ValueObjects.Testing.Data`, throw a `ValueObjectSamplingException`, an `InvalidOperationException`, when no
candidate passed the rules of a type, whether drawn from its schema or from a generator registered for it with
`Use<TSelf, TValue>`, and the type declares no example they accept. It is a failure of test set-up, not a value refused
at a boundary: its `ErrorCode` holds the code of the rule that refused the last candidate, or `null` when no candidate
was drawn at all, `ValueObjectType` the type, `Attempts` the number of candidates tried and `FromGenerator` whether
they came from a registered generator, and nothing goes into `Exception.Data`, so `ValueObjectErrors.TryGetCode` reads
no code from it. Its message holds no candidate value, and ends on the registration that gives the sampler a generator
of the underlying value:

```text
Could not draw a value of 'EvenCode' its rules accept: the last of 100 candidates drawn from its schema was refused
(checksum), and it declares no example its rules accept. Register a generator of its underlying value:
options.Use<EvenCode, string>(random => ...).
```

When a registered generator gave the candidates, the message names that generator rather than asking for one:

```text
Could not draw a value of 'EvenCode' its rules accept: the last of 100 candidates its registered generator gave was
refused (checksum), and it declares no example its rules accept. Make the generator registered with
options.Use<EvenCode, string>(random => ...) give values its rules accept.
```

`TryNext` reports the same without throwing: `false`, and the refusal of the last candidate.

The AutoFixture, Bogus and FsCheck packages throw the same exception, with the last sentence in their own terms when the
candidates came from the schema: `fixture.Register(() => EvenCode.Create(...))` for AutoFixture, which wraps it in its
`ObjectCreationExceptionWithPath`; `faker.ValueObject<EvenCode, string>(f => ...)` for Bogus, from `Generate` or from
`ValueObject`; and `options.Use<EvenCode, string>(random => ...)`, on the options given to `MergeValueObjects` or
`ValueObjectArbitrary.For`, for FsCheck, from the draw, out of the property's check. A generator of Bogus's handed to
`faker.ValueObject<TSelf, TValue>(generator)` whose values the type refuses is named as that call.

Two refusals come before any draw. `faker.ValueObject<TSelf>()` throws an `ArgumentException`,
`'MarkerOnlyValue' is not a value object: it implements no IValueObject<TSelf, TValue> over itself.`, for a type that
carries the `IValueObject` marker and nothing more, and registers nothing. FsCheck refuses a type it has no arbitrary
for with an exception of its own, `The type … is not handled automatically by FsCheck.`, naming it in full, from the
property that needs it or from any type holding it: a construction of a generic value object, or a value object written
by hand that nothing registered, which `MergeValueObjects` leaves out and `MergeValueObject<TSelf, TValue>()` merges.

The contract kit's check of
the values a schema rules out fails with the value and the rule it breaks, `'-1' breaks Minimum (0) declared on 'Dial'
but is accepted.`, a test value and no user input. [Test your value objects](../how-to/test-value-objects.md#rules-no-schema-carries)
says when each happens.

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
