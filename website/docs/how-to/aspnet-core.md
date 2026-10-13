---
title: Use with ASP.NET Core
sidebar_label: ASP.NET Core
slug: /how-to/aspnet-core
description: Bind value objects from routes, query strings, headers and bodies in MVC and minimal APIs, know what a rejection answers in each, and return RFC 9457 problem details carrying the violated rule, from MVC controllers on System.Text.Json or Newtonsoft.Json and from minimal APIs, native AOT included.
---

# Use with ASP.NET Core

## Minimal APIs

Binding needs no package. A generated value object implements `IParsable<T>` and `ISpanParsable<T>`, which is
exactly what minimal API parameter binding looks for, and its `[JsonConverter]` covers request and response bodies:

```csharp skip
app.MapGet("/accounts/{iban}", (Iban iban) => /* … */);
```

An optional value object is declared `T?`: `(CountryCode? country) => …` binds `null` when the request leaves the value
out. A default value is no way to make one optional, and `VO0010` refuses `(CountryCode country = default) => …`: the
Request Delegate Generator hands such a handler a default instance no rule checked, and the reflection-based binding of
ASP.NET Core 10.0.12 fails to build the endpoints, answering every request with a 500.

A value the value object rejects is answered with a 400 before the handler runs, and without the package of
[the problem details for minimal APIs](#problem-details-for-minimal-apis) that 400 says nothing of why.
It names no parameter and carries no message and no code: its body is empty, or holds bare problem details, a title
and a status, once `AddProblemDetails()` is registered, and `AddValidation()` adds nothing to it. In Development,
minimal APIs throw a `BadHttpRequestException` instead (`RouteHandlerOptions.ThrowOnBadRequest`), which
`UseExceptionHandler` answers with a 500. For a body, it wraps the exception the converter refused the value with,
from which `ValueObjectErrors.TryGetCode` reads the code
([the code in an exception](../reference/errors.md#the-code-in-an-exception)); a route or query value is refused
through `TryParse`, which throws nothing, and the exception then carries no code. A client of an application that
also validates DataAnnotations then meets two shapes of 400: a full validation problem for those, and the bare one for
a rejected value object.

Empty text is not MVC's rule either. Under the reflection-based binding, `?country=` for a `CountryCode?` is a 400,
as it is for an `int?` or a `Guid?`, where [MVC](#mvc-controllers) binds it as absent.

That holds wherever a value object comes from, except for one declared in the project that maps the endpoints when
the Request Delegate Generator runs, which the next section covers.

### The Request Delegate Generator

The Request Delegate Generator (RDG) writes minimal API binding at build time, for native AOT. It runs in every build
of a project that sets `PublishAot`, `PublishTrimmed` or `EnableRequestDelegateGenerator`, not only when it
publishes. It is a source generator, and source generators never see each other's output: for a value object
declared in its own project, it sees the struct without the `IParsable<T>` the generator adds, and binds the
parameter from the request body. A route value is then answered with a 400, a query value is bound to `null`
without an error, and the build stays clean.

A value object declared in another project, a domain project where most applications keep them, is not affected:
the RDG reads its interfaces from the compiled assembly. One declared in the endpoints' project lists its contract on
its own declaration, which is the part the RDG reads. The members still come from the generator:

```csharp
[ValueObject<string>(MaxLength = 10)]
public readonly partial struct Sku : IValueObject<Sku, string>;
```

The contract is `IValueObject<TSelf, TValue>`, `INumericValueObject<TSelf, TValue>` for an arithmetic value object,
and `IEntityId<TSelf>` for an identifier; any interface that brings `IParsable<TSelf>` will do. Listing it is
harmless where no RDG runs, and required where one does. In a project where the RDG runs and that references
ASP.NET Core's endpoint routing, a value object listing none is reported as `VO0033`, with a code fix that adds the
contract. Every value object of that project is reported, whether an endpoint binds it or not.

A handler that returns a member the generator writes needs an explicit return type, which the RDG cannot infer from
a member it does not see:

```csharp skip
app.MapGet("/skus/{sku}", string (Sku sku) => sku.Value);
```

Without it, the build fails with `CS0411` and `CS1031` in `GeneratedRouteBuilderExtensions.g.cs`, a file the RDG
writes. Listing the interface does not help there.

The RDG also binds empty text as the reflection-based binding does not, for a value object as for an `int?` or a
`Guid?`: `?sku=` binds `null` to a nullable parameter, where the reflection-based binding answers 400. A parameter that
cannot be `null`, or an element of an array, gets the default instance instead, which no rule has checked, and the
handler runs ([default instances](../reference/default-instances.md)): declare it `T?` and check it. An empty header,
by contrast, is taken for an absent one, and refused when the parameter is required.

### Problem details for minimal APIs

```bash
dotnet add package AdCodicem.ValueObjects.AspNetCore.Http
```

```csharp skip
builder.Services.AddProblemDetails();
builder.Services.AddValueObjectHttpProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();

var api = app.MapGroup("/api").WithValueObjectProblemDetails();
api.MapGet("/lines", (Quantity qty, CountryCode? country) => TypedResults.Ok(/* … */));
```

A value object an endpoint of the group cannot bind is answered with the validation problem MVC writes for it, with
an `errorCodes` member mapping each rejected member to the code of the rule it broke, read from the same `TryParse`
the binder ran:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "qty": ["The value must be less than or equal to 100."] },
  "errorCodes": { "qty": "value_object.out_of_range" }
}
```

The package is AOT-compatible, unlike the MVC one, and depends on nothing but the shared framework and the contracts.
`WithValueObjectProblemDetails()` takes any endpoint convention builder: a route group, one endpoint, or
`app.MapGroup("")` to cover every endpoint mapped on it. ASP.NET Core has no global endpoint filter, so the choice is
made per group, and an endpoint covered twice, by its group and by itself, is covered once.

It covers:

- a route value, a query value and a header, under the name each binds from, which `[FromRoute]`, `[FromQuery]` and
  `[FromHeader]` may set;
- the members of an `[AsParameters]` record or class, under the names they bind from;
- an array of value objects read from the query string, whose refused elements are listed under its name, each with
  its message, and the code of the first, since `errorCodes` maps a member to one code, as MVC's does;
- a required value object that is absent, reported with "A value is required." and `value_object.required`;
- a JSON body, but only where `ThrowOnBadRequest` is on, as [the next section](#a-request-body-and-throwonbadrequest)
  explains.

It leaves the framework's answer in place for what no value object refused: a 400 caused by an `int` or a `Guid`
parameter, or by a body the serializer cannot read at all, is answered as it is without the package, and a parameter
that is not a value object is not listed beside one that is, since it has no rule code to report. A form field and an
array read from headers are not covered. An endpoint whose handler takes no value object gets no filter at all, and an
endpoint that binds one gets a filter that reads the status the binder left, and nothing else unless it is a 400.

Under `AddValidation()`, the framework runs its validation filter before every other: a request that breaks a
DataAnnotations rule as well as a value object is answered with that rule alone. Empty text follows the binder that
ran. The reflection-based binding parses it: it refuses `?country=` for a `CountryCode?`, and the refusal is reported
with the rule that made it. [The Request Delegate Generator](#the-request-delegate-generator) refuses no empty query
text: it binds `null` to a nullable value object, and the default instance, unchecked, to one that cannot be `null`,
an array element included, so nothing is reported. It takes an empty header for an absent one, which is reported with
`value_object.required` when the parameter is required.

#### A request body and ThrowOnBadRequest

With `RouteHandlerOptions.ThrowOnBadRequest` off, the default outside Development, the framework answers a JSON body
it cannot read before any endpoint filter runs, and keeps the exception it read the code from: a refused body keeps
the bare 400. With it on, the default in Development, the binder throws a `BadHttpRequestException` instead, for a
route or query value as for a body, before any filter runs, which `UseExceptionHandler()` would answer with a 500.
`AddValueObjectHttpProblemDetails()` registers an `IExceptionHandler` that answers it with the same problem details,
a body under its JSON path, the key MVC gives it, with the message of the converter:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "$.from": ["The value is not a valid Iban: The value must be at least 15 characters long."] },
  "errorCodes": { "$.from": "value_object.too_short" }
}
```

It reads the code with `ValueObjectErrors.TryGetCode`
([the code in an exception](../reference/errors.md#the-code-in-an-exception)), and never copies the framework's
message, which names the refused text. A dictionary keyed by a value object is the exception: a key it refuses is
listed under the path of that key, which holds the key's text, as MVC lists it. The handler answers the endpoints
`WithValueObjectProblemDetails()` covers and no other, and leaves any other exception to the next handler, a body the
serializer cannot read at all among them, even beside a refused query value: it keeps the framework's answer, as it
does with `ThrowOnBadRequest` off. It needs `AddProblemDetails()` and `app.UseExceptionHandler()` wherever
`ThrowOnBadRequest` is on: in Development, that middleware answers every other exception too, in place of the
developer exception page. Handlers run in the order they are registered, so register it before a handler of your own
that answers every exception.

To give a body its code in Production as well, turn `ThrowOnBadRequest` on there:

```csharp skip
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
```

The package leaves that setting alone: it changes how every binding failure of the application is raised, value
object or not.

#### Native AOT

The filter closes the check of each value object over its type through the descriptor's visitor, at compile time, and
runs in a native binary through the code the Request Delegate Generator writes. A value object nothing registered, one
written by hand or a construction of a generic one, cannot be described there, and its refusal is left unexplained:
register it at start-up as [the JSON guide](./json.md#systemtextjson-source-generated) asks.

A native binary serializes without reflection, so register `AddProblemDetails()`, which chains the framework's
serializer context for problem details into the HTTP JSON options: `errorCodes` is written as a JSON element that
context knows, its keys run through the dictionary key policy of those options, as the keys of `errors` are. Without
it, or a `JsonSerializerContext` of the application's listing `HttpValidationProblemDetails` and `JsonElement`, the
endpoints `WithValueObjectProblemDetails()` covers fail to build, at the first request, with an
`InvalidOperationException` naming `AddProblemDetails()`, rather than answer each refusal with a 500 where the
framework answers a 400.

The messages can be in the application's language too, read from a resource by the code of the rule:
[messages in the application's language](#messages-in-the-applications-language).

`ValueObjectProblemDetails`, which names the member, ships with this package and keeps the namespace
`AdCodicem.ValueObjects.AspNetCore`; the MVC package references it and forwards the type, so that code compiled
against either finds it.

## MVC controllers

```bash
dotnet add package AdCodicem.ValueObjects.AspNetCore
```

```csharp skip
builder.Services.AddControllers().AddValueObjects();
```

That registers a model binder for value objects — routes, query strings, headers, forms — and the JSON options
for bodies, and records the code of a value a body refuses, as the next section shows. The binder is closed over each
concrete type, so binding costs one `TryParse`. An application that configures MVC directly can call
`AddValueObjects()` on `MvcOptions` instead; that one adds the binder only. An application whose MVC reads bodies
with Newtonsoft.Json calls `AddValueObjectsNewtonsoftJson()` instead, from a package of its own
([a body read by Newtonsoft.Json](#a-body-read-by-newtonsoftjson)).

Nullable value objects bind as you would expect: `[FromQuery] CountryCode? country` is `null` when the parameter
is absent, and a 400 when it is present and rejected. Empty or white-space text, `?country=` or `?country=%20`, binds
as absent, as MVC binds an `int?` or a `Guid?`. A value object that cannot be `null`, a route segment such as
`CustomerId id`, refuses blank text as MVC refuses it for an `int`: a 400 with "The value ' ' is invalid." and the
code `value_object.required`, rather than an instance no rule has checked.

A value object an attribute takes from elsewhere is read there, and never from a route, query or form value of the same
name. `[FromHeader(Name = "X-Country")] CountryCode? country` reads the header alone, through MVC's header binder,
which hands the package's binder the header's text: blank or absent, an optional one is `null`; refused, the request is
a 400 with the rule's code, as for a query value; an array, `[FromHeader] Quantity[]`, takes each comma-separated
value. `[FromBody] CountryCode country` reads the body itself, a JSON string, through the input formatter, and a binder
the parameter names, `[ModelBinder(typeof(…))]`, binds it.

A model that keeps the text of a value object holds it to the value object's rules with DataAnnotations'
[`[ParsesAs]`](./data-annotations.md), on a record's constructor parameter, which is where MVC requires a validation
attribute on a record, and a value object a body may leave at its default with `[NotDefault]`, which `[Required]` cannot
catch. MVC reports such a refusal in `errors` with the message of its rule, and without an `errorCodes` entry.

## Problem details carrying the rule

For MVC controllers, with `AdCodicem.ValueObjects.AspNetCore`:

```csharp skip
builder.Services.Configure<ApiBehaviorOptions>(options => options.AddValueObjectProblemDetails());
```

When model binding rejects a value, the automatic 400 response gains an `errorCodes` member mapping each rejected
member to the stable code of the rule it violated:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "country": ["The value is not one of the accepted values."] },
  "errorCodes": { "country": "value_object.not_a_known_value" }
}
```

A client branches on `value_object.not_a_known_value`, not on English, and the message can be in the application's
language: [messages in the application's language](#messages-in-the-applications-language). The member name is available as
`ValueObjectProblemDetails.ExtensionName`. A member refused more than once, as the elements of a query array, which MVC
binds under the array's name, or a key repeated in a body Newtonsoft.Json reads, lists every message in `errors` and
keeps the code of the first in `errorCodes`, the rule its first message states, as a minimal API does.

This covers what the MVC model binder rejects, route values, query strings, headers and forms, under the key MVC gives
its error, as it keys an `int` it refuses: the name of the parameter, or of the member, prefixed as MVC prefixes the
members of a model. A header goes under the name `[FromHeader(Name = …)]` gives it, prefixed likewise; where the
attribute names none, a member's header goes under the member's name, and a parameter's under the empty key, unless a
route, query or form value carries the parameter's name, which is then the key. A value inside a JSON body goes under
its JSON path, the key MVC gives its error, and a value object read from the body itself under the key MVC gives the
root of the body, `$` with System.Text.Json and the empty key with Newtonsoft.Json:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "$.email": ["The value is not a valid EmailAddress: The value does not match the expected format."] },
  "errorCodes": { "$.email": "value_object.invalid_format" }
}
```

A body is read by the serializer, whose exception MVC keeps the message of and drops: `AddValueObjects()` on the MVC
builder puts in place of MVC's System.Text.Json input formatter, at its index, one deriving from it, which reads with
the very `JsonSerializerOptions` the framework's formatter read with and reads the code off that exception before the
exception is dropped. The `errors` member is the one MVC writes without the package, whatever
`JsonOptions.AllowInputFormatterExceptionMessages` says: the message of the exception by default, "The input was not
valid." when it is turned off. Messages configured with `AddValueObjects(messages => …)` replace the message of the
exception with the resource's, under that default
([messages in the application's language](#messages-in-the-applications-language)). A change made to the JSON options
once the application runs reaches it as it reaches the framework's, and the media types and encodings the application
gave the framework's formatter are kept. A System.Text.Json input formatter the application built with options of its
own, or derived, is left as it is, and records no code. A body read by Newtonsoft.Json, after `AddNewtonsoftJson()`,
has no System.Text.Json formatter to replace, and records no code unless `AddValueObjectsNewtonsoftJson()` replaces
Newtonsoft.Json's ([below](#a-body-read-by-newtonsoftjson)); neither does a body when the binder alone was added,
through `AddValueObjects()` on `MvcOptions`.

The swap is a post-configuration of `MvcOptions`. To take the System.Text.Json input formatter out,
`InputFormatters.RemoveType<SystemTextJsonInputFormatter>()` still works in `AddMvcOptions`, in
`Configure<MvcOptions>` or in a `PostConfigure<MvcOptions>` registered before `AddValueObjects()`. In a
`PostConfigure<MvcOptions>` registered after it, that call matches the exact type alone and misses the package's
formatter: remove every formatter that is a `SystemTextJsonInputFormatter` there instead.

```csharp skip
builder.Services.PostConfigure<MvcOptions>(options =>
{
    foreach (var formatter in options.InputFormatters.OfType<SystemTextJsonInputFormatter>().ToList())
    {
        options.InputFormatters.Remove(formatter);
    }
});
```

`ApiBehaviorOptions` is MVC's, and minimal APIs never read it: they get the same problem details from
[a package of their own](#problem-details-for-minimal-apis).

### A body read by Newtonsoft.Json

An application whose MVC reads request bodies with Newtonsoft.Json installs a package of its own: neither
`AdCodicem.ValueObjects.AspNetCore` nor `AdCodicem.ValueObjects.NewtonsoftJson` could hold its formatter without
imposing the other's dependency on every application that installs it.

```bash
dotnet add package AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson
```

```csharp skip
builder.Services.AddControllers()
    .AddNewtonsoftJson()
    .AddValueObjectsNewtonsoftJson();
builder.Services.Configure<ApiBehaviorOptions>(options => options.AddValueObjectProblemDetails());
```

`AddValueObjectsNewtonsoftJson()` calls `AddValueObjects()` itself, so value objects bind from routes, query strings,
headers and forms as above, and an application that calls both, in either order, gets the same. It adds
`ValueObjectConverter` to MVC's `SerializerSettings`, with `DateParseHandling.None` and `FloatParseHandling.Decimal`,
as `AddValueObjects()` on the settings does ([the Newtonsoft.Json guide](./json.md#newtonsoftjson) says why), unless
the settings hold the converter once every configuration of them has run: settings the application configured for
value objects itself, in `AddNewtonsoftJson(options => …)`, before or after this call, are left as it left them.
Without the converter, Newtonsoft.Json reads a value object through its type converter, whose refusal names no rule
and quotes the text the client sent.

Those settings are MVC's own: its Newtonsoft.Json output formatter writes every response with them, and they apply to
every member of every body, not to value objects alone. An application that ran without the converter therefore
changes its wire when it adopts the package. A numeric or boolean value object is answered `7` or `true`, where the
type converter wrote `"7"` or `"True"` ([without the converter](./json.md#without-the-converter)); a number sent as a
string is still read, but a boolean value object sent as `"True"` is refused. A member of type `object` or `JToken`
keeps a string that looks like a date as a string, and reads a number with a fraction as a `decimal`, which refuses
one beyond its range, such as `1e30` ([Numbers](./json.md#numbers)). An application that needs other settings gives
them the converter itself, `AddValueObjects(decimalReals: false)` to read reals as `double`, and the package leaves
them as they are.

It then puts in place of MVC's `NewtonsoftJsonInputFormatter`, at its index, one deriving from it, over the same
settings and the same `MvcNewtonsoftJsonOptions`, with the media types and encodings the application gave the formatter
it replaces. It reads a body as the framework's does, and records the code of each refused value under the key MVC
gives its error, Newtonsoft.Json's path rather than a `$.` one:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "email": ["The value is not a valid EmailAddress: The value does not match the expected format."],
    "lines[2].sku": ["The value is not a valid Sku: The value must be at least 3 characters long."]
  },
  "errorCodes": {
    "email": "value_object.invalid_format",
    "lines[2].sku": "value_object.too_short"
  }
}
```

- Newtonsoft.Json reads on past a refused property of an object it builds by setting its properties, so each refused
  member of such a body is answered with its own code, where System.Text.Json stops at the first. A type it builds
  through its constructor, a positional record, stops at its first refusal, as its errors do.
- The keys are MVC's, quirks included: the value of a positional record that a property of a class holds,
  `{"order":{"reference":"no"}}`, is keyed `order.reference.order`, as its error is.
- No code is recorded past the errors the model state takes, `MvcOptions.MaxModelValidationErrors`, 200 by default,
  so that every code has its error.
- `errors`, the logs and the exceptions that propagate are the framework's, whatever
  `MvcNewtonsoftJsonOptions.AllowInputFormatterExceptionMessages` says when the request is read. An error a handler of
  the serializer settings marks handled records no code, as it records no error.
- A value object used as a dictionary key is read by Newtonsoft.Json through its type converter, never the converter:
  its refusal carries no code, and its message quotes the key.
- A Newtonsoft.Json input formatter the application built over settings of its own, or derived, is left as it is and
  records no code; so is the JSON Patch formatter, whose document holds its values as JSON until `ApplyTo`.
- Without `AddNewtonsoftJson()`, MVC reads with System.Text.Json, and the call amounts to `AddValueObjects()`.

The swap is a post-configuration of `MvcOptions`, as for System.Text.Json: in a `PostConfigure<MvcOptions>` registered
after the call, `InputFormatters.RemoveType<NewtonsoftJsonInputFormatter>()` misses the package's formatter. Remove
every formatter that is a `NewtonsoftJsonInputFormatter` there instead, but for the JSON Patch formatter, which derives
from it and which `RemoveType` keeps, and for any formatter the application derived itself:

```csharp skip
builder.Services.PostConfigure<MvcOptions>(options =>
{
    foreach (var formatter in options.InputFormatters
        .Where(static formatter => formatter is NewtonsoftJsonInputFormatter and not NewtonsoftJsonPatchInputFormatter)
        .ToList())
    {
        options.InputFormatters.Remove(formatter);
    }
});
```

The package is not AOT-compatible, as neither MVC nor Newtonsoft.Json is.

### A body read as XML

The MVC XML formatters, `AddXmlSerializerFormatters()` and `AddXmlDataContractSerializerFormatters()`, read a value
object as `XmlSerializer` or `DataContractSerializer` does: as an empty element and a default instance, unless its
assembly opts into [XML serialization](xml.md), which makes them read and write it as its underlying value, through
its rules. A body holding a value the type refuses is then answered 400 with MVC's own message, "An error occurred
while deserializing input data.", and no code: no formatter of this library records the code of an XML body yet.

## Messages in the application's language

The messages in `errors` are the English ones of the rules. An application that answers in its users' language names a
resource, and every value object an endpoint refuses is answered with the message the resource holds for the code of
the rule, under the key `ValueObject.` followed by the code, with the arguments the type's rules supply. The codes in
`errorCodes` stay the same in every language. The keys, the arguments and a French example are in
[the errors reference](../reference/errors.md#a-message-in-the-applications-language).

For MVC controllers:

```csharp skip
builder.Services.AddLocalization();
builder.Services.AddControllers()
    .AddValueObjects(messages => messages.UseResource<ValidationMessages>());
builder.Services.Configure<ApiBehaviorOptions>(options => options.AddValueObjectProblemDetails());

var app = builder.Build();
app.UseRequestLocalization(options => options
    .SetDefaultCulture("fr-FR")
    .AddSupportedCultures("fr-FR", "en-US")
    .AddSupportedUICultures("fr-FR", "en-US"));
```

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "country": ["« country » doit être l'une des valeurs suivantes : FR; BE; LU."] },
  "errorCodes": { "country": "value_object.not_a_known_value" }
}
```

- A route, query, header or form value is quoted by the display name MVC gives its member, `{0}`, as MVC's own
  messages are, an element of an array by its type's.
- A value a JSON body refuses is translated too, under its JSON path, quoting the name of the value object, never the
  path, which holds the key a dictionary keyed by a value object refused. MVC answers with the formatter's message only
  under `JsonOptions.AllowInputFormatterExceptionMessages`, its default: turned off, a body is answered "The input was
  not valid." whatever the resource holds. A body read by Newtonsoft.Json, through
  `AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson`, keeps its English message.
- Blank text keeps MVC's own message, "The value ' ' is invalid.", which the application localises through
  `ModelBindingMessageProvider`, as it does for every other type.
- `AddValueObjects(messages => …)` exists on `MvcOptions` too, the wiring an application that configures MVC directly
  uses. It configures the binder alone, so a body keeps its English message there, unless `AddValueObjects()` on the
  MVC builder puts the input formatter in place as well, which reads the same messages. The options hold one binder
  provider, whichever of these is called and however often: the last configuration of its messages wins.

For minimal APIs, the convention takes the same option, per endpoint:

```csharp skip
builder.Services.AddLocalization();
builder.Services.AddProblemDetails();
builder.Services.AddValueObjectHttpProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.UseRequestLocalization(options => options.SetDefaultCulture("fr-FR").AddSupportedCultures("fr-FR", "en-US").AddSupportedUICultures("fr-FR", "en-US"));

var api = app.MapGroup("/api").WithValueObjectProblemDetails(messages => messages.UseResource<ValidationMessages>());
```

- A route, query or header value is quoted by the name it binds from, `{0}`, an absent required one included; a body
  the exception handler explains, by the name of the value object, as on MVC.
- The options belong to each endpoint: one covered by its group and by itself takes its own, and a call without options
  keeps the group's.
- The exception handler answers in the culture of the request whatever the order of `UseExceptionHandler()` and
  `UseRequestLocalization()`: in the usual order above, the culture that middleware set is gone by the time the
  exception reaches the handler, which applies the culture of `IRequestCultureFeature` again.
- `AddLocalization()` and a `.resx` resource publish with native AOT without a warning, and the package's lookup reads
  no annotation: the minimal API path runs in a native binary. Under `InvariantGlobalization`, every request runs in the
  invariant culture, which reads the neutral resource, the `.resx` without a culture in its name.

Each target reads the resource through the request's `IStringLocalizerFactory`, which `AddLocalization()` registers:
without it, the first refusal that needs a message throws an `InvalidOperationException` naming the call. The current UI
culture chooses the message, falling back from `fr-FR` to `fr` and then to the neutral resource, and a key the resource
lacks keeps the English message: a resource that holds `ValidationMessages.fr.resx` alone answers every other culture
in English. A message quoting an argument its code does not supply throws its `FormatException`, the application's
error to see rather than hide behind the English text.

The resource is named after a type, an empty class beside a `.resx` file of the same name for each culture:

```csharp skip
namespace MyApp.Resources;

public sealed class ValidationMessages;
```

```xml
<!-- Resources/ValidationMessages.fr.resx -->
<data name="ValueObject.value_object.too_short" xml:space="preserve">
  <value>« {0} » : au moins {1} caractères.</value>
</data>
<data name="ValueObject.value_object.out_of_range.minimum" xml:space="preserve">
  <value>« {0} » doit valoir au moins {1}.</value>
</data>
```

The build names the resource after the root namespace of the project followed by its folder, and the localizer looks
it up after the namespace of the type: keep the type's namespace the root namespace followed by the folder, or every key
reads as missing, with no error.

On .NET 11, the built-in validation of minimal APIs localises its own messages through
`ValidationOptions.LocalizerProvider`, which resolves an `IStringLocalizer` per type. One resource type can serve both,
a key of its own beside each `ValueObject.` key:

```csharp skip
// .NET 11 (Microsoft.Extensions.Validation 11.0.0-rc.1): an API this library, which targets net10.0, does not call.
builder.Services.AddValidation(options =>
    options.LocalizerProvider = (_, factory) => factory.Create(typeof(ValidationMessages)));
```

That validation looks the message of a DataAnnotations attribute naming no resource up under
`{Type}_{Member}_{Attribute}_Error`, `{Type}_{Attribute}_Error` and `{Attribute}_Error`: a key `ParsesAsAttribute_Error`
in that resource gives one template to every rule of every [`[ParsesAs(typeof(T))]`](./data-annotations.md#net-11)
member that names none (``ParsesAsAttribute`2_Error`` for the generic form, `NotDefaultAttribute_Error` for
`[NotDefault]`). An attribute naming `ErrorMessageResourceType` alone keeps the message of each rule, read by its code,
which .NET 11 leaves alone. It also lets an exception an attribute throws out of the request, where .NET 10 answers it
with a 400 carrying the exception's message.

## Codes for a payload you validate yourself

For a payload that carries raw text — an inbound message from another system, say — validate it with
[FluentValidation](./fluentvalidation.md) and put the codes under the same member, so every 400 of the API has
the same shape. `ValueObjectProblemDetails` comes with every ASP.NET Core package, in the namespace
`AdCodicem.ValueObjects.AspNetCore`:

```csharp skip
var result = validator.Validate(request);

return result.IsValid
    ? Results.Accepted()
    : Results.ValidationProblem(
        result.ToDictionary(),
        extensions: new Dictionary<string, object?>
        {
            [ValueObjectProblemDetails.ExtensionName] = result.Errors
                .GroupBy(failure => failure.PropertyName)
                .ToDictionary(member => member.Key, member => member.First().ErrorCode),
        });
```

Under native AOT, the HTTP JSON options write that extension through a serializer context, and the framework's own
for problem details knows no `Dictionary<string, string>`: list it in the application's `JsonSerializerContext`,
chained into `ConfigureHttpJsonOptions`, or build the codes as a `JsonElement`, as the minimal API package does, or the
response fails to serialize.

The validator of the [FluentValidation](./fluentvalidation.md#text-that-must-become-a-value-object) guide stops each
member at its first failure with `Cascade(CascadeMode.Stop)`, so a member is reported once: empty text fails
`NotEmpty()` and never reaches `MustParseAs`, and `errors` holds one message for it. A validator that does not stop,
or that states several rules for one member, can fail a member more than once, which is why the failures are
grouped by member: each member reports the code of the first rule it failed, and the dictionary never meets the same
member twice.
