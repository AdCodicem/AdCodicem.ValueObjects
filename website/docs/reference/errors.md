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
message is for people, and may change: it is English, and an application that answers in its users' language looks a
message up [by the code](#a-message-in-the-applications-language) instead.

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

## A message in the application's language

The message of a rule is English, and stays so for the developers who read exceptions and logs. An application that
answers its users in another language translates it by code, and the integrations look the translation up when they
refuse a value: `AddValueObjects(messages => messages.UseResource<T>())` on MVC and
`WithValueObjectProblemDetails(messages => messages.UseResource<T>())` on a minimal API
([ASP.NET Core](../how-to/aspnet-core.md#messages-in-the-applications-language)),
`UseValueObjectProblemDetails(messages => messages.UseResource<T>())` on an Azure Functions HTTP trigger
([Azure](../how-to/azure.md#messages-in-the-requests-language)), `AddValueObjects(options => options.UseResource<T>())`
on a HotChocolate schema ([GraphQL](../how-to/graphql.md#messages-in-the-applications-language)),
`AddValueObjectInputs(options => options.UseResource<T>())` for the inputs of a Blazor form
([Blazor](../how-to/blazor.md#messages-in-the-applications-language)),
`AcceptValueObjects(options => options.UseResource<T>())` on the options and arguments of a System.CommandLine command
([System.CommandLine](../how-to/files-and-command-line.md#messages-in-the-applications-language)), FluentValidation's
`LanguageManager` under `MustParseAs`, `MustSatisfy` and `NotDefault`
([FluentValidation](../how-to/fluentvalidation.md#messages-in-another-language)), and `ErrorMessageResourceType` named
alone on DataAnnotations' `[ParsesAs]` and `[NotDefault]`
([DataAnnotations](../how-to/data-annotations.md#messages-in-another-language)). No translation ships with the
packages: each application writes the messages it wants, and a key it leaves out keeps the English message.

The key of a code is `ValueObject.` followed by the code, as `ValueObjectMessages.KeyFor` gives it:
`ValueObject.value_object.too_short`, `ValueObject.value_object.id.invalid_length` for an
[entity identifier](../entity-identifiers.md), `ValueObject.delivery_date.sunday` for a code of your own. For
`value_object.out_of_range` on a type declaring one bound alone, a more specific key is looked up first:
`ValueObject.value_object.out_of_range.minimum` when the type declares a minimum alone, `.maximum` when it declares a
maximum alone, then the key of the code. The key follows the bounds the type declares, not the side the value crossed,
which a refusal does not tell. `ValueObjectMessages.KeysFor` lists them in that order.

A message quotes arguments the rules the type declares supply, through `ValueObjectMessages.ArgumentsFor`: each has a
name, which FluentValidation quotes as a placeholder, and a position, which an `IStringLocalizer` formats with
`string.Format`.

| Code | Named (FluentValidation) | Positional (MVC, minimal APIs, Azure Functions, HotChocolate, Blazor inputs, System.CommandLine, DataAnnotations) |
| --- | --- | --- |
| Every code | `{PropertyName}`, `{ValueObject}` | `{0}`, the member |
| `value_object.too_short` | `{MinLength}` | `{1}` |
| `value_object.too_long` | `{MaxLength}` | `{1}` |
| `value_object.out_of_range` | `{Minimum}`, `{Maximum}` | `{1}`, `{2}` |
| `value_object.not_a_known_value` | `{KnownValues}` | `{1}` |

- `{ValueObject}` is the name of the value object type, without the arity of a generic one: `Reference` for
  `Reference<PurchaseOrder>`. `{0}` is the member the refusal is listed under: the display name MVC gives it, the
  name a minimal API value binds from, the name of the parameter of an Azure Functions HTTP trigger, the argument, the
  input field or the variable of a GraphQL refusal (the scalar's name for a variable's default value, which HotChocolate
  names no variable for, and for an operand of a HotChocolate.Data filter under `AddValueObjectFiltering()`, or any
  input value `QuoteScalarNameIn` selects), the field of a refused output, the `DisplayName` of a Blazor input, or else its field's
  name, the alias of a System.CommandLine option as typed or the name of an argument, followed by the token's position
  from zero where the symbol received more than one, `--ibans[1]` for the second, and bare for one, the display name of
  a member DataAnnotations validates, or, in
  a request body, the name of
  the value object, never the JSON path, which holds the key a dictionary keyed by a value object
  refused.
- Each argument is text, formatted in the current culture: a bound is read back from the invariant text the schema
  holds and written for the culture, `0,01` in French, a date in the culture's order, and the known values are joined
  with the culture's list separator followed by a space, `, ` in English and `; ` in French, where the comma separates
  the decimals. A rule the type does not declare, the other bound of a range of one, is an empty text, and a format
  given in a placeholder, `{Minimum:N2}`, changes nothing.
- The refused value is never an argument: a message shown to someone else must not quote what was sent.
  FluentValidation's own `{PropertyValue}` still quotes it, as on any rule.
- Every other code, `value_object.required`, `value_object.invalid_format`, `value_object.not_parsable`, an entity
  identifier's or a code of your own, has the arguments of every code alone. A code of the table always has its
  arguments, an empty text for a rule the type does not declare.

The current UI culture chooses the message and the current culture formats the arguments, as .NET does elsewhere: with
`UseRequestLocalization()`, both follow the request. An Azure Functions invocation runs on the host's call, not on the
request's, so the Functions middleware applies the request's cultures itself, those `UseRequestLocalization()` chose or
those the application's `RequestLocalizationOptions` choose from the request
([Azure](../how-to/azure.md#messages-in-the-requests-language)). A template quoting an argument its code does not
supply is the application's error: an `IStringLocalizer` throws its `FormatException`, which the integrations let
through, HotChocolate answering a field it fails with its own unexpected error, while FluentValidation leaves a
placeholder it has no value for as it is. On MVC, minimal APIs and Azure Functions HTTP triggers, a resource read without
`builder.Services.AddLocalization()` throws an `InvalidOperationException` naming the call; on HotChocolate, the request
executor fails to start with that exception. `AddValueObjectInputs` registers the localization services itself, and a
Blazor input whose options were configured by hand without them throws that exception from its change event. The
System.CommandLine options and arguments, which have no services, read the resource through a `ResourceManager` over its
type, as DataAnnotations does; a blank entry keeps the English message.

A French resource for the codes of the framework, as an example:

| Key | Message |
| --- | --- |
| `ValueObject.value_object.required` | `« {0} » est obligatoire.` |
| `ValueObject.value_object.invalid_format` | `Le format de « {0} » n'est pas valide.` |
| `ValueObject.value_object.out_of_range` | `« {0} » doit être compris entre {1} et {2}.` |
| `ValueObject.value_object.out_of_range.minimum` | `« {0} » doit valoir au moins {1}.` |
| `ValueObject.value_object.out_of_range.maximum` | `« {0} » doit valoir au plus {2}.` |
| `ValueObject.value_object.too_long` | `« {0} » : au plus {1} caractères.` |
| `ValueObject.value_object.too_short` | `« {0} » : au moins {1} caractères.` |
| `ValueObject.value_object.not_a_known_value` | `« {0} » doit être l'une des valeurs suivantes : {1}.` |
| `ValueObject.value_object.not_parsable` | `« {0} » n'a pas la forme attendue.` |

A `.maximum` message quotes `{2}`: the positions are fixed, the maximum always second. The same messages serve
FluentValidation's `LanguageManager` once `{0}` reads `{PropertyName}` and each position its name.

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

The DataAnnotations attributes report a refusal with `ValueObjectValidationResult`, which is not this struct but
DataAnnotations' own `System.ComponentModel.DataAnnotations.ValidationResult`, a class, carrying `ErrorCode` and
`ValueObjectType` beside its message and member names:

| Member | |
| --- | --- |
| `string? ErrorMessage`, `IEnumerable<string> MemberNames` | DataAnnotations' own: the message the attribute gives, and the member it is reported under. |
| `string ErrorCode` | The code of the rule that refused the text, `value_object.required` for `[NotDefault]`. |
| `Type ValueObjectType` | The value object whose rule refused it. |

[DataAnnotations](../how-to/data-annotations.md#messages-and-codes) lists which hosts keep it, and so the code.

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
converters and the integrations name the type and the rule, and leave the value out, too. CsvHelper itself puts the
refused field in `TypeConverterException.Text`, and the raw record at the end of its message, unless
`ExceptionMessagesContainRawData` is false
([CsvHelper](../how-to/files-and-command-line.md#what-a-refused-field-looks-like)).

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

The classification on the type does not reach a `[LoggerMessage]` method by itself: Microsoft.Extensions.Telemetry's
logging generator reads one on the parameter or the property only, which `VO0045` asks for, and its code fix writes
([Data classification](../how-to/logging.md#data-classification)).

The integrations on a boundary report a rejected value in their own terms, so the exception is reserved for code
that treats a rejected value as a bug, for a strict EF Core read, and for an EF Core write
[refusing a value](#a-value-refused-on-write); the Orleans package carries one a grain lets escape back to the caller of
the grain, its code and type kept:

| Integration | A rejected value |
| --- | --- |
| The System.Text.Json converters | `ValueObjectJsonException`, a `JsonException` with the message of the rule, carrying the type and the code. |
| The Newtonsoft.Json converter | `JsonSerializationException`, with the message of the rule, carrying the code in its `Data`. |
| Newtonsoft.Json [without the converter](../how-to/json.md#without-the-converter) | `JsonSerializationException`, "Error converting value…", around an `ArgumentException` that names neither the rule nor its code. |
| ASP.NET Core MVC model binding | A model state error; the [problem details](../how-to/aspnet-core.md#problem-details-carrying-the-rule) carry its code, for a route, query, header or form value and for a value inside a JSON body, read by System.Text.Json, or by Newtonsoft.Json through [`AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson`](../how-to/aspnet-core.md#a-body-read-by-newtonsoftjson), keyed as MVC keys its errors, which is the empty key for a `[FromHeader]` parameter naming no header unless a route, query or form value carries the parameter's name, and for the root of a body Newtonsoft.Json reads; a body Newtonsoft.Json reads without that package records no code, and neither does a value object Newtonsoft.Json reads as a dictionary key, through its type converter. Neither package throws anything of its own. Without the package's binder, MVC binds through the type converter, which throws `ValueObjectException`, and reports it with the message it gives bad input for an `int`, such as "The value 'ZZ' is not valid.", and no code. |
| A JSON Patch document, Newtonsoft.Json version, applied through [`TryApplyTo`](../how-to/aspnet-core.md#a-json-patch-document) | A model state error under the operation's `path`, or its `from` for a `copy` or a `move` refused at its source, the code recorded beside it for the problem details; or, through the overload taking a handler, a call to it with the error and the code. A `test` whose value the rules refuse carries the code, as an `add` or a `replace` does; one that does not match, and an error no value explains, carry no code. Nothing is thrown for what the client sent; a `null` argument throws `ArgumentNullException`. Through MVC's own `ApplyTo(objectToApplyTo, ModelState)`, JSON Patch's message, which quotes the value, under the type name of the object, with no code. |
| Minimal API parameter binding | A [bare 400](../how-to/aspnet-core.md#minimal-apis), naming neither the parameter nor the rule; in Development, a `BadHttpRequestException`, which `UseExceptionHandler` answers with a 500, and which wraps the converter's exception for a body. On the endpoints `AdCodicem.ValueObjects.AspNetCore.Http` covers, a [validation problem](../how-to/aspnet-core.md#problem-details-for-minimal-apis) carrying the code of each refused route, query or header value, and of a refused body where `ThrowOnBadRequest` is on, whose exception its handler answers; the package throws nothing of its own. |
| FluentValidation, `MustParseAs` and `MustSatisfy` | A validation failure carrying the code. |
| DataAnnotations, [`[ParsesAs]` and `[NotDefault]`](../how-to/data-annotations.md#messages-and-codes) | A `ValueObjectValidationResult` carrying the code, which `Validator.TryValidateObject`, `TryValidateProperty`, `TryValidateValue` and `ObservableValidator.GetErrors` keep; `Validator.ValidateObject`, `ValidateProperty` and `ValidateValue` throw DataAnnotations' `ValidationException` around it. MVC's model state, the errors of `AddValidation()` and the failures of options validation keep the message alone. A member of another type than the attribute takes, or a type that is no value object or that native AOT finds unregistered, throws `InvalidOperationException` when a value is validated, which carries no code: it reports a misuse, `VO0039` to `VO0042` at build time, never a broken rule. .NET 10's `AddValidation()` answers that exception with a 400 carrying its message, and .NET 11's lets it out of the request. |
| Dapper | `DataException`, carrying the code in its `Data`, for a value it cannot convert, and for text read into a value object over another type, or a number or a `Guid` read into one over `string`, that the value object refuses. |
| EF Core with `strict: true` | `ValueObjectException`, from `Create`, for a column or an element of a [collection](../how-to/ef-core.md#collections-of-value-objects): the query fails. |
| MongoDB.Driver, through [`AdCodicem.ValueObjects.MongoDB`](../how-to/mongodb.md#reading) | `FormatException`, "The value read is not a valid Iban: …", carrying the code in its `Data`, for a value the type refuses unless the serializer is trusted, and, whatever the trust, for a BSON `null` or a value the serializer of the underlying type cannot read; inside the driver's own `FormatException`, naming the member and its class, when it reads a document. |
| MessagePack, through [`AdCodicem.ValueObjects.MessagePack`](../how-to/messagepack.md#reading) | `MessagePackSerializationException`, "The value read is not a valid Iban: …", carrying the code in its `Data`, for a value the type refuses unless the formatter is trusted, and, whatever the trust, for a `nil` or a value the formatter of the underlying type cannot read; inside MessagePack's own `MessagePackSerializationException`, "Failed to deserialize … value.". A dictionary whose keys normalize to one value object fails with MessagePack's own `ArgumentException`, which carries no code and quotes the key ([value-object keys](../how-to/messagepack.md#security-and-value-object-keys)). Through [SignalR](../how-to/messagepack.md#signalr), the hub method is not invoked: the client gets a `HubException` that names neither the rule nor its code, and the server logs the binding failure at `Debug`, with the code in the exception's chain. |
| protobuf-net, through [`AdCodicem.ValueObjects.ProtobufNet`](../how-to/protobuf-net.md#reading) | `ProtoException`, "The value read is not a valid Iban: …", carrying the code in its `Data`, for a value the type refuses unless the serializer is trusted, and, whatever the trust, for a value protobuf-net's serializer of the underlying type cannot read, "The protobuf value read cannot be read as Iban, a value object over String.", with `value_object.not_parsable`; protobuf-net lets it through as it is. A field the message leaves out is never read: the member holds the default instance, which no rule checked ([members a sender leaves out](../how-to/protobuf-net.md#members-a-sender-leaves-out)). Through a protobuf-net.Grpc [code-first service](../how-to/protobuf-net.md#code-first-grpc) whose configuration is `ValueObjectBinderConfiguration.ForServer`, a refused request is answered `INVALID_ARGUMENT`, with the message of the refusal and a `google.rpc.ErrorInfo` carrying the code; without it, `UNKNOWN`, "Exception was thrown by handler.". A reply the client's value objects refuse surfaces on the client as an `RpcException`, `INTERNAL`, whose inner `ProtoException` carries the code. |
| An Orleans grain call, through [`AdCodicem.ValueObjects.Orleans`](../how-to/orleans.md#reading) | `SerializerException`, "The value read is not a valid Quantity: …", carrying the code in its `Data`, for a value the type refuses unless the codec is trusted, and, whatever the trust, for a `null`, with `value_object.required`, or a value Orleans' codec of the underlying type cannot read, "The Orleans value read cannot be read as Quantity, a value object over Int32.", with `value_object.not_parsable`. A refused request fails on the caller with the silo's exception, its code intact, and the grain is not invoked; a refused reply fails on the caller the same way ([refusals at the caller](../how-to/orleans.md#refusals-at-the-caller)). A member the message leaves out is never read: it holds the default instance, which no rule checked. A `ValueObjectException` a grain lets escape reaches the caller with its code and type, never its `AttemptedValue` ([from grain code](../how-to/orleans.md#a-valueobjectexception-from-grain-code)). |
| A gRPC service's handler, through [`AdCodicem.ValueObjects.Grpc`](../how-to/grpc.md) | `ValueObjectBadRequest.ThrowIfAny()` throws an `RpcException`, `INVALID_ARGUMENT`, "The request holds values its value objects refuse.", whose `google.rpc.BadRequest` holds a field violation per refused value, its `reason` the code in upper snake case and its `description` the rule's message. A `ValueObjectException` that escapes a handler, contract-first or code-first, is answered by `ValueObjectExceptionInterceptor` with an `RpcException`, `INVALID_ARGUMENT`, with the exception's message and a `google.rpc.ErrorInfo` carrying the code; without it, `UNKNOWN`, "Exception was thrown by handler.". |
| `XmlSerializer` and `DataContractSerializer`, in an assembly marked `[assembly: ValueObjectXmlSerialization]` | `XmlException`, with the message of the rule and the line of the element, carrying the code in its `Data`, inside `XmlSerializer`'s `InvalidOperationException` ("There is an error in XML document …") or `DataContractSerializer`'s `SerializationException`: [XML](../how-to/xml.md#reading). |
| A Model Context Protocol tool whose result holds a value its type refuses | The converter's `ValueObjectJsonException` reaches the SDK, which answers the call with `An error occurred invoking 'get_iban'.`, with or without `AdCodicem.ValueObjects.ModelContextProtocol`, which checks arguments, not results. |
| A Model Context Protocol tool result that a client validates against the tool's `outputSchema` | The client's own refusal, for a value its type accepts and the schema, written for what a model sends, does not: another spelling of a known value of a closed set looked up ignoring case, or text whose characters outside the Basic Multilingual Plane bring it under `minLength` ([what it does not cover](../how-to/language-models.md#what-it-does-not-cover)). |
| A tool argument, through [`AdCodicem.ValueObjects.AI`](../how-to/language-models.md#a-refused-argument)'s `WithValueObjectValidation()` | Nothing is thrown: the function returns `{"error":"invalid_argument","argument":…,"code":…,"message":…}`, which `FunctionInvokingChatClient` hands the model as it is, with the converter's message, or `A value is required.` and `value_object.required` for an absent argument or a `null` sent to a value object that cannot be `null`. Where the converter writes no message, for a number the underlying type cannot hold, and where a converter of the application's own, or the underlying contract of a value object written by hand, refuses with an exception of its own, the message is "The value is not a valid Quantity.", and the code the one that exception carries in its `Data`, or `value_object.not_parsable`. Without the wrapper, the converter's `ValueObjectJsonException` reaches `FunctionInvokingChatClient`, which answers the model "Error: Function failed.", followed by the message alone under `IncludeDetailedErrors`. |
| A structured answer, read through `ChatResponse<T>.Result` | The converters' `ValueObjectJsonException`, whose code the application can ask again with ([structured output](../how-to/language-models.md#structured-output)). |
| A Model Context Protocol tool argument, through [`AdCodicem.ValueObjects.ModelContextProtocol`](../how-to/language-models.md#a-refused-argument-as-a-tool-execution-error)'s `WithValueObjectTools` | Nothing is thrown: the server answers a tool execution error, `isError: true`, whose text is `Argument 'quantity' rejected (value_object.out_of_range): …`, and whose structured content is the object above when the tool declares no output schema, its code and message read the same way. Without the package, the SDK answers `An error occurred invoking 'place_order'.`, and the rule and its code are lost. |
| An Azure Functions isolated worker binding, through [`AdCodicem.ValueObjects.AzureFunctions`](../how-to/azure.md#route-values-query-values-and-binding-data)'s `UseValueObjects()` | The converter fails the parameter with a `FormatException`, "The value is not a valid Quantity: The value must be less than or equal to 100. (value_object.out_of_range)", carrying the code in its `Data` and at the end of its message; the worker keeps that text alone, in a `FunctionInputConverterException` with no inner exception, which fails the invocation. Over HTTP, with [`UseValueObjectProblemDetails()`](../how-to/azure.md#problem-details-for-http-triggers) after `ConfigureFunctionsWebApplication()`, a validation problem carrying the code of each refused route or query value, and of a `[FromBody]` value object where the MVC package's `AddValueObjects()` reads the body; the middleware throws nothing of its own. Without the package, the worker's JSON conversion refuses the text, a valid `Guid` or date included, and the host logs the reader's message, which can quote part of the value: `'not-a-guid' is an invalid JSON literal`. |
| A Kafka message, through Confluent's JSON serdes with the schema of [`AdCodicem.ValueObjects.NJsonSchema`](../how-to/messaging.md#kafka-with-confluents-schema-registry) | The package refuses nothing: `JsonDeserializer<T>` reads through the settings' `ValueObjectConverter`, whose `JsonSerializationException` carries the code in its `Data`, and `JsonSerializer<T>` refuses to write a value its type refuses the same way. Confluent's own `InvalidDataException`, "Schema validation failed for properties: […]", from the validation of a message against the registered schema, carries none. |
| A GraphQL argument, input field or variable, through [`AdCodicem.ValueObjects.HotChocolate`](../how-to/graphql.md#a-refused-value)'s `AddValueObjects()`, or a filter operand, through [`AdCodicem.ValueObjects.HotChocolate.Data`](../how-to/graphql.md#filtering-and-sorting-with-hotchocolatedata)'s `AddValueObjectFiltering()` | `LeafCoercionException`, carrying the code in its `Data`, whose GraphQL error has the message of the rule, the code as `extensions.code`, and a `ValueObjectException` with the type and the code on `IError.Exception`: a field error at the argument's `inputPath`, a filter operand's at its operation, `["eq"]` or `["in", 1]`, a request error at the variable's `path`. A variable of another JSON kind is `value_object.not_parsable`; a literal of another kind and a `null` literal are its own validation errors, with no code, and a `null` variable its `HC0018`; a `null` text fragment of a filter is its `HC0026`, as for a `String` member, with no code. Without the package, a schema taking a value object as an argument does not start: ``No compatible constructor found for input type type `Shop.Iban`.``. |
| A Blazor form's text, through [`AdCodicem.ValueObjects.Blazor`](../how-to/blazor.md#the-code)'s `InputValueObject` | Nothing is thrown: the refused text stays in the field, the rule's message goes under it through the form's `EditContext`, and `TryGetValueObjectErrorCode` reads the code; empty text a value object that cannot be `null` refuses is `value_object.required`. A type the registry does not describe, one that is no value object, or, where the runtime supports no dynamic code, a value object written by hand or a construction of a generic one that nothing registered, throws `InvalidOperationException` at the input's first render, naming the registration native AOT needs, which carries no code: it reports a misuse, never a broken rule. A statically rendered form is mapped by the framework first, which answers a refused value with its own message, quoting the text, and no code ([static rendering](../how-to/blazor.md#static-rendering)). |
| A System.CommandLine option or argument, through [`AdCodicem.ValueObjects.CommandLine`](../how-to/files-and-command-line.md#what-a-refusal-looks-like)'s `AcceptValueObjects()` or `AcceptValueObject()` | Nothing is thrown: a parse error naming the option, by the alias typed, or the argument, and giving the rule's message, never the text, one per refused token, under its position when the symbol received more than one, the code and the type kept beside it, which `TryGetValueObjectErrorCode` and `TryGetValueObjectType` read; an invocation writes it to standard error, shows the help, and returns 1 without running the action. An option or an argument of a command above the subcommand invoked, which System.CommandLine reads only at `GetValue` unless the option is `Recursive` or either has a default value, is read while the subcommand is validated once `AcceptValueObjects()` reached it. `GetValue` on a refused symbol throws System.CommandLine's `InvalidOperationException`, built from the message alone, which carries no code: a parent command's symbol configured by `AcceptValueObject()` alone, under a subcommand no walk reached, gets there inside the action. Misuse throws `InvalidOperationException` when the symbol is configured, which carries no code: it reports a misuse, never a broken rule. `AcceptValueObject()` throws for a symbol whose type holds no value object, and for one that already has a `CustomParser`; both calls throw for a value object in a shape they do not read, and for a value object the registry cannot describe, naming the registration native AOT needs: where the runtime supports no dynamic code, a value object written by hand or a construction of a generic one that nothing registered, even beside a `CustomParser` of the application's own. System.CommandLine's own errors, an option `Required` and left out among them, carry no code. |
| A CSV field, through [`AdCodicem.ValueObjects.CsvHelper`](../how-to/files-and-command-line.md#csvhelper)'s `AddValueObjects()` or `AddValueObject<TSelf, TValue>()` | CsvHelper's `TypeConverterException`, "The field is not a valid Iban: …", naming the type and the rule, never the field, carrying the code in its `Data`, which `GetRecords` lets through as it is and `ReadingExceptionOccurred` sees; an empty field, or one a short row lacks under `MissingFieldFound = null`, read into a value object that refuses empty text is `value_object.required`, "An empty field cannot be read as Iban; declare the member as a nullable Iban? instead."; a rule with an empty message gives "The field is not a valid Iban.", and so does a parser written by hand that refuses without a reason, with `value_object.not_parsable`. Its `Text` holds the field, empty text for one a short row lacks, unless `ExceptionMessagesContainRawData` is false, and CsvHelper's message ends with the raw record unless it is. A map's `Default(…, useOnConversionFailure: true)` reads the default instead, with no code, for a default of the value object or a `null` one for a nullable member. A type the factory cannot describe, a value object written by hand or a construction of a generic one nothing registered where the runtime supports no dynamic code, throws `InvalidOperationException` when CsvHelper first meets it, with no code: at header validation, as it is, and with no header inside a `ReaderException` per row; `WriteRecords` and `WriteRecord` wrap it in a `WriterException`, and `GetField<T>` and `WriteField<T>` throw it as it is. Without the package, a value object reads as a default instance, with nothing thrown, and through a class map CsvHelper's own `TypeConverterException`, "The conversion cannot be performed.", carries no code. |

The other reads do not validate. EF Core by default, Dapper for a value the provider returns as the underlying type or
as its date and time counterpart, a trusted MongoDB serializer, a trusted MessagePack formatter, a trusted protobuf-net
serializer and a trusted Orleans codec build the value object with `CreateUnchecked`: they read what this application
validated when it wrote it. A trusted MongoDB serializer still refuses a BSON `null` and a value the serializer of the
underlying type cannot read, which no instance can hold, a trusted MessagePack formatter a `nil` and a value the
formatter of the underlying type cannot read, a trusted protobuf-net serializer a value protobuf-net's serializer of the
underlying type cannot read, and a trusted Orleans codec a `null` and a value Orleans' codec of the underlying type
cannot read.
[EF Core](../how-to/ef-core.md#validation-on-read) says when to read strictly. A `.Value` an EF Core query projects
under [`UseValueObjectQueries()`](../how-to/ef-core.md#the-value-in-a-query) is read from the column as it stands, in a
strict context too: it builds no value object, so nothing validates it. A context whose options call it, started on a
service provider of the application's own, named by `UseInternalServiceProvider`, that does not hold its translator,
throws an `InvalidOperationException` as it starts, which carries no code: Entity Framework Core adds no service of an
options extension to such a provider, and would leave `.Value` untranslated without a word.

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
| protobuf-net, through [`AdCodicem.ValueObjects.ProtobufNet`](../how-to/protobuf-net.md#writing) | `ProtoException`, with the same message, as it is: from a value, a nullable one holding a default instance, an element of a list and a dictionary key; a `TSelf?` holding nothing is written as nothing. Through a code-first gRPC service, a reply the server cannot write is answered `UNKNOWN`, "Exception was thrown by handler.". |
| Orleans, through [`AdCodicem.ValueObjects.Orleans`](../how-to/orleans.md#writing) | `SerializerException`, with the same message, as it is: from an argument, a result, a member, an element or a member typed `object` holding one. A client sending one gets it before anything is sent; a grain returning one fails its reply. A call between two grains of one silo copies its arguments instead, so a default reaches the callee there ([local calls](../how-to/orleans.md#local-calls-and-default-instances)). |
| `XmlSerializer` and `DataContractSerializer`, in an assembly marked `[assembly: ValueObjectXmlSerialization]` | `XmlException`, "The value to write is not a valid Quantity: …", inside `XmlSerializer`'s `InvalidOperationException` ("There was an error generating the XML document.") or `DataContractSerializer`'s `SerializationException`. |
| HotChocolate, through [`AdCodicem.ValueObjects.HotChocolate`](../how-to/graphql.md#a-refused-value) | The field's error, with the message of the rule and HotChocolate's own code, `EXEC_INVALID_LEAF_VALUE`; the `ValueObjectException` on `IError.Exception` keeps the rule's code. A real that is not finite is refused the same way, as no GraphQL Float holds it. |
| CsvHelper, through [`AdCodicem.ValueObjects.CsvHelper`](../how-to/files-and-command-line.md#what-is-written) | `TypeConverterException`, with the same message, as it is: from `WriteRecords`, `WriteRecord` and `WriteField`, for a value and for a `TSelf?` holding a default instance; a `TSelf?` holding nothing is written as empty text, or as the first of its `NullValues`. The fields written before it stay in the writer's buffer, which disposing the writer flushes. |

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

A table given [CHECK constraints from the rules](../how-to/ef-core.md#check-constraints-from-the-rules) refuses another
writer's row on the server, and an invalid instance a lax read let in that the application writes back. Through Entity
Framework Core, `SaveChanges` throws a `DbUpdateException` around the provider's error: a `SqlException` with number
547 on SQL Server, a `PostgresException` with SQL state `23514` on PostgreSQL, a `SqliteException` with error code 19
on SQLite. Each names the constraint, `CK_{table}_{column}_{rule}`, and so the rule, which every name ends with,
followed by eight hexadecimal digits when the name was shortened to the provider's limit, or kept apart from a name
already taken: by a check constraint of the model, one the application declared, in the same table or, on SQL Server,
in the schema, or by another
table's, and the column, unless the name was shortened. With
[`UseValueObjectCheckErrors()`](../how-to/ef-core.md#when-a-constraint-fires) on the options of the context, the
provider's error carries the code of that rule in its `Data`, as [below](#the-code-in-an-exception): the code
`Validate` reports for it, `value_object.too_short` for `MinLength`, `value_object.out_of_range` for `Minimum` and
`Maximum`, `value_object.not_a_known_value` for `KnownValues`, `value_object.invalid_format` for `Pattern`. The error
is neither replaced nor wrapped, and its message is the provider's. Without the call, it carries none, and
`ValueObjectCheckConstraints.TryGetCode` reads the code off it, as it does off an error the call cannot reach: on SQL
Server, that of a raw query reading back the rows it writes, which SqlClient reports while the rows are read. Only an
error reporting a violated CHECK constraint is read: one naming a constraint for another reason, a migration's "There
is already an object named …", carries no code. A migration adding a constraint to a table that already holds a row
it refuses fails with the same error, "The ALTER TABLE statement conflicted with the CHECK constraint" on SQL Server,
and the same code. With
[patterns](../how-to/ef-core.md#patterns) named, building the model throws an `InvalidOperationException`, which
carries no code, for a pattern the dialect would read apart from .NET, naming the value object, the construct, the
dialect and the remedies: "AddValueObjectCheckConstraints cannot write the pattern of EmailAddress for PostgreSql: it
holds a negated class, [^…], which .NET matches over one UTF-16 code unit and the database over a whole character: list
the characters it accepts. Write the pattern within what .NET and the database read alike, leave it out with
`ExcludePattern<EmailAddress>()`, or leave Patterns at None." A hook whose regular expression sets an option the text
does not carry, any but `CultureInvariant`, `Compiled`, `ExplicitCapture` and `NonBacktracking`, fails it with a
message of its own, naming the option: "…its regular expression sets IgnoreCase, which the text of the pattern does not
carry, and the database would read the text as .NET reads it with no option. Write the pattern so that it means the
same without them, …". The first use of the context throws it, and so do `dotnet ef migrations add` and
`dotnet ef dbcontext optimize`. A dialect `ValueObjectPatternDialect` does not define is an
`ArgumentOutOfRangeException` when the conventions are configured.

`AdCodicem.ValueObjects.MessagePack` also lets through exceptions that carry no code. A read or a write through options
whose resolver holds no formatter for the underlying type throws MessagePack's `FormatterNotRegisteredException`: a
misconfiguration, not a value refused. Under `MessagePackSecurity.UntrustedData`, which SignalR's hub protocol sets,
MessagePack refuses a dictionary keyed by a value object, or a set of value objects, with a `TypeAccessException`, and a
dictionary whose keys normalize to one value object fails with MessagePack's `ArgumentException`, which quotes the key
([value-object keys](../how-to/messagepack.md#security-and-value-object-keys)). An array or a map that announces more
elements than bytes remain is refused by MessagePack's reader with an `EndOfStreamException`, before any value object
is read. Each comes inside MessagePack's own `MessagePackSerializationException` when the serializer reads or writes.

`AdCodicem.ValueObjects.ProtobufNet` lets through protobuf-net's own exceptions, which carry no code. A value object
nobody registered fails the first message holding one with protobuf-net's `InvalidOperationException`, "No serializer
defined for type: Shop.Iban"; a registration after the model has written one fails with another, "The type cannot be
changed once a serializer has been generated for Shop.Iban". `AddValueObject` throws an `InvalidOperationException`
for a value object the model already serializes through another serializer, naming both, and a `NotSupportedException`
for a value object over `Int128`, `UInt128` or `DateTimeOffset`, which protobuf-net has no serializer for, as the
serializer does when an attribute names it on such a type. A `null` in a `List<Iban?>` without `[NullWrappedValue]` is
protobuf-net's `NullReferenceException`, as in a `List<int?>`, and a stream disposed while a value is read is the
stream's `ObjectDisposedException`.

`AdCodicem.ValueObjects.Orleans` lets through Orleans' own exceptions, which carry no code. A side without the package
has no codec: Orleans' `CodecNotFoundException`, "Could not find a codec for type Shop.Sku.", which a silo in the
`Development` environment throws as it starts, and otherwise at the first call, returned to the caller; a
`ValueObjectCodec<TSelf, TValue>` an application registers by hand comes without the type filter, Orleans'
`InvalidOperationException`, `Type "Shop.Quantity" is not allowed.`. Bytes cut short fail with the reader's
`IndexOutOfRangeException`. A text a peer over the primitives repeats in its message, read into a `Sku?`, fails with
Orleans' `ReferenceNotFoundException`, its codec of a nullable value reading a reference only as `null`
([what a value object writes](../how-to/orleans.md#what-a-value-object-writes)). Two misuses, never a broken rule, are
refused with a `NotSupportedException`: where the runtime supports no dynamic code, a value object nothing registered, a
construction of a generic one or one written by hand, the message naming `AddValueObject<TSelf, TValue>()`; and on
write, a member, an argument or a result typed as a subclass of `ValueObjectException`, which would arrive as a
`ValueObjectException`, the message asking for `ValueObjectException` or `Exception`.

A log is no such write: nothing reads one back into a value object. The
[Serilog integration](../how-to/logging.md#serilog) logs a default instance as the default of its underlying type,
`0`, `""` or `false`, and throws nothing, whatever it logs.

### An entity added without its key

Under `ConfigureValueObjects`, a primary key of one property over a `Guid` value object, which is no foreign key and
which nothing generates, refuses an entity added without it ([A Guid key left
unset](../how-to/ef-core.md#a-guid-key-left-unset)), whatever the value object's rules, and the exception carries
`value_object.required` in its `Data`, as [below](#the-code-in-an-exception). Entity Framework Core asks for the key,
and so throws, in `Add`, in `Attach`, `Update` and `Remove`, which take such an entity for a new one, and in
`DetectChanges` or `SaveChanges` for a new entity found through a navigation. Nothing is written, and the refused entity
is not tracked; in a graph, the entities attached before it stay tracked, so clear the change tracker before saving the
same context again.

| Situation | Exception |
| --- | --- |
| An entity added without its `Guid` key | `InvalidOperationException`, carrying `value_object.required` in its `Data`, "'Shipment' was added without its key: 'Shipment.Id' holds default(ParcelId), Guid.Empty, and nothing generates it. Assign the key in the domain, or have it generated with HasValueObjectKeyGeneration()." The first name is the entity added, the second the key's declaring type: the root of a hierarchy, or the principal whose key a dependent shares. |
| A GUID `HasValueObjectKeyGeneration()` generated, which the value object refuses | `ValueObjectException` from `Add`, with the code of the rule, as from `Create`. |
| `HasData` seeding a `Guid` key with `Guid.Empty` | Entity Framework Core's `InvalidOperationException`, while the model is built: "The seed entity for entity type 'Shipment' cannot be added because a default value was provided for the required property 'Id'." |
| An entity added without an integer key `HasValueObjectKeyGeneration()` generates, on a provider that is not relational, Entity Framework Core's in-memory database for one | `InvalidOperationException` from `Add`, "'Folio' was added with its key left to the database, but 'Folio.Id' is numbered by the identity column of a relational database, and Microsoft.EntityFrameworkCore.InMemory is not one. …", which carries no code: the model asks for what the provider cannot do, and no value was refused. |
| An integer key the domain set, generated as an identity column, on SQL Server | Entity Framework Core's `DbUpdateException` from `SaveChanges`, around SQL Server's refusal to insert a value into an identity column while `IDENTITY_INSERT` is off. |
| A migration turning an existing integer key into an identity column, on SQL Server | Entity Framework Core's `InvalidOperationException`: "To change the IDENTITY property of a column, the column needs to be dropped and recreated." |

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
| EF Core, an entity added without its `Guid` key, under `ConfigureValueObjects` | `InvalidOperationException`, `value_object.required` | In `Data` |
| EF Core, a row a CHECK constraint from the rules refused, under `UseValueObjectCheckErrors()` | The provider's `SqlException`, `PostgresException` or `SqliteException`, inside the `DbUpdateException` of `SaveChanges`, as it is from `ExecuteUpdate`, raw SQL or a migration; on SQL Server, a raw query reading back the rows it writes, over `OUTPUT`, fails with an error that carries none, which `ValueObjectCheckConstraints.TryGetCode` reads | In `Data` |
| MongoDB.Driver, a read or a refused write | `FormatException` on read, `BsonSerializationException` on write, inside the driver's exception of the same type, naming the member, when it reads or writes a document | In `Data` |
| MessagePack, a read or a refused write | `MessagePackSerializationException`, inside MessagePack's own of the same type when the serializer reads or writes a value | In `Data` |
| protobuf-net, a read or a refused write | `ProtoException`, as it is | In `Data` |
| Orleans, a read or a refused write | `SerializerException`, as it is, returned to the caller of a grain for a refused request | In `Data` |
| Orleans, a `ValueObjectException` a grain lets escape | `ValueObjectException`, rebuilt on the caller without `AttemptedValue` | `ErrorCode` |
| A protobuf-net.Grpc code-first service, a refused request, through `ValueObjectBinderConfiguration.ForServer` | `RpcException`, `INVALID_ARGUMENT`, whose trailers carry a `google.rpc.ErrorInfo` | The `ErrorInfo`'s `reason` and `metadata["code"]` on the client; `Data` on the server |
| A gRPC handler's refusal, through `ValueObjectExceptionInterceptor` | `RpcException`, `INVALID_ARGUMENT`, whose trailers carry a `google.rpc.ErrorInfo` | As above |
| A gRPC request's fields, through `ValueObjectBadRequest` | `RpcException`, `INVALID_ARGUMENT`, whose trailers carry a `google.rpc.BadRequest` | Each violation's `reason` on the client; the first code in `Data` on the server |
| `XmlSerializer` and `DataContractSerializer`, in an assembly that opts into XML serialization | `XmlException`, inside the serializer's `InvalidOperationException` or `SerializationException` | In `Data` |
| The Azure Functions converter | `FormatException`, whose text alone the worker keeps, in its `FunctionInputConverterException` | In `Data`, and at the end of the message |
| The HotChocolate scalars, and the operands of a HotChocolate.Data filter | `LeafCoercionException`, whose GraphQL error carries a `ValueObjectException` on `IError.Exception` | In `Data`; the error's `Exception` in `ErrorCode`, and its `extensions.code` but for a refused output |
| DataAnnotations' throwing `Validator` methods, for a refusal of `[ParsesAs]` or `[NotDefault]` | `ValidationException`, which DataAnnotations raises around the result | `ErrorCode` of its `ValueObjectValidationResult` |
| CsvHelper, a refused field or a refused write | `TypeConverterException`, a `CsvHelperException`, as it is | In `Data` |

A Blazor input throws nothing for refused text, and so has no row here: the rule's message goes under the field, through
the form's `EditContext`, and `TryGetValueObjectErrorCode` reads the code from it, as the table of rejected values under
[`ValueObjectException`](#valueobjectexception) says. A System.CommandLine option or argument throws nothing for refused
text either: the refusal is a parse error, whose code `TryGetValueObjectErrorCode` reads.

The code is the one the rule reports. A value that is not of the underlying type at all carries
`value_object.not_parsable`: a JSON token of the wrong kind, a number the underlying type cannot hold, text not of its
shape, a column the Dapper handler cannot convert, an XML element holding elements or no text of the underlying type, a
BSON value of a type, or beyond a range, the serializer of the underlying type cannot read, a MessagePack value of
another type, beyond a range, not of the underlying type's shape, or truncated, a protobuf field of another wire type,
beyond a range, not of the underlying type's shape, of a negative length, or truncated, an Orleans field of another wire
type or beyond a range. A `null` where a value object that cannot be `null` is expected carries
`value_object.required`: a JSON `null`, a SQL `NULL`, a BSON `null`, a MessagePack `nil` or an Orleans `null` read into
an `Iban` rather than an `Iban?`, an empty CSV field read into one, whatever its parser says of no text, an element
marked `xsi:nil` that `XmlSerializer` hands a member that cannot be `null` (`DataContractSerializer` refuses that one
itself, without a code). So does an entity [added without its key](#an-entity-added-without-its-key). The messages
are the ones the exceptions carried before; where the System.Text.Json reader itself cannot read a token, as for a
number beyond the range of an `int`, the message is still the serializer's own, "The JSON value could not be converted
to …", followed by the path. No exception carries the refused value; the error of a database provider keeps the
message its provider wrote, which on PostgreSQL holds the refused row when the connection string sets
`Include Error Detail`.

`ValueObjectErrors.TryGetCode` reads the code from any of these, and from an exception that wraps one: ASP.NET Core
wraps a body it cannot read in a `BadHttpRequestException`, and Entity Framework Core a refused write in a
`DbUpdateException`. It reads `ValueObjectException.ErrorCode`, then `ValueObjectJsonException.ErrorCode`, then the
`ValueObjectValidationResult` a DataAnnotations `ValidationException` carries, then `Data` under
`ValueObjectErrors.ErrorCodeKey`, on each exception of the chain in turn, and the first code found wins.
DataAnnotations raises its `ValidationException` itself, so its `Data` holds nothing, and its `Value` holds the value it
validated, as for any attribute: log it only where the value may be logged.
An exception that reports no broken rule carries none; the provider's error for a row a CHECK constraint from the rules
refused, which no value object raised, carries one under `UseValueObjectCheckErrors()`.

```csharp skip
catch (Exception exception) when (ValueObjectErrors.TryGetCode(exception, out var code))
{
    logger.LogWarning(exception, "A value was refused: {Code}", code);
}
```

The Azure Functions converter's `FormatException` is the one whose message carries the code too: the worker turns the
failure of a parameter into the text of the `FunctionInputConverterException` it throws, with no inner exception, so
neither the converter's exception nor its `Data` reaches a middleware, a log or an alert, and `TryGetCode` reads
nothing on the worker's exception. The code at the end of the text, `(value_object.out_of_range)`, is what survives; an
HTTP trigger answered through `AdCodicem.ValueObjects.AzureFunctions.AspNetCore` carries it under `errorCodes`
([Azure Functions](../how-to/azure.md#azure-functions-isolated-worker)).

HotChocolate keeps the `LeafCoercionException` to itself and hands an error filter the GraphQL error alone: an error
filter of the application's own reads the code with `ValueObjectErrors.TryGetCode(error.Exception, out var code)`, an
output refusal's included, whose `extensions.code` HotChocolate replaces with its own
([GraphQL](../how-to/graphql.md#a-refused-value)).

The two HotChocolate packages refuse a schema they cannot serve at start, inside HotChocolate's `SchemaException`, with
exceptions that carry no code. Where the runtime supports no dynamic code, as under native AOT, the scalars refuse a
value object written by hand, or a construction of a generic one, that nothing registered with a
`NotSupportedException` naming `ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema)` and
`AddValueObjectScalar<TSelf, TValue>()`, and the HotChocolate.Data filters with one naming
`BindValueObject<TSelf, TValue>()` beside them
([GraphQL](../how-to/graphql.md#generic-value-objects-and-value-objects-written-by-hand-in-a-filter)).
`ValueObjectTextOperationFilterInputType<TSelf>` bound to a value object that is not over `string` throws an
`InvalidOperationException`, ``'Shop.Quantity' is not a value object over string``, and bound without
`IncludeTextOperations()`, or in a named convention, meets HotChocolate's own ``For the field contains of type
IbanOperationFilterInput was no handler found.`` ([binding by hand](../how-to/graphql.md#binding-by-hand)). A text
operation run without `UseValueObjectQueries()` fails at its first query, never at start, with HotChocolate's
"Unexpected Execution Error" around Entity Framework Core's "could not be translated"
([the text operations](../how-to/graphql.md#the-text-operations-on-request)).

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

A protobuf-net.Grpc code-first service configured with `ValueObjectBinderConfiguration.ForServer` answers a refused
request with this reason in a `google.rpc.ErrorInfo`, its `metadata` holding the code and the value object, inside the
`google.rpc.Status` of its `grpc-status-details-bin` trailer, which holds no `ErrorInfo` for a code that maps to none
([code-first gRPC](../how-to/protobuf-net.md#code-first-grpc)).
`AdCodicem.ValueObjects.Grpc` writes it the same way for a refusal that escapes a handler, and as the `reason` of each
field violation `ValueObjectBadRequest` records, left empty for a code that maps to none
([gRPC services](../how-to/grpc.md)).

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
[`NotDefault`](../how-to/fluentvalidation.md#an-uninitialized-value-object) rule and DataAnnotations'
[`[NotDefault]`](../how-to/data-annotations.md#an-uninitialized-value-object), which `[Required]` is not, since a struct
is never `null`, check it at the edge, and so refuse a valid zero. The writers do not stop at it: they validate the default, and refuse only the value its type rejects
([above](#a-value-refused-on-write)).

A generated value object implements it explicitly, as a member of `IValueObject<TSelf, TValue>`, so a tool reading
the public properties of the type (a logger destructuring it, a schema generator, an exporter) does not publish it.
Generic code constrained on the interface reads it without boxing; code holding the concrete type casts to the
interface, `((IValueObject<Iban, string>)iban).IsDefault`, which boxes.
