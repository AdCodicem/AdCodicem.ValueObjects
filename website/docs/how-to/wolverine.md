---
title: Use with Wolverine
sidebar_label: Wolverine
slug: /how-to/wolverine
description: Bind value objects from the route, query, header and form values of Wolverine.Http endpoints through their rules, answer every refused or missing one with the minimal API's problem details and rule codes, and send them in Wolverine messages as bare values.
---

# Use with Wolverine

```bash
dotnet add package AdCodicem.ValueObjects.Wolverine.Http
```

```csharp skip
using AdCodicem.ValueObjects.Wolverine.Http;

builder.Services.AddWolverineHttp();
builder.Services.AddProblemDetails();          // required where JSON serialization runs without reflection, as under native AOT
var app = builder.Build();
app.MapWolverineEndpoints(opts =>
{
    opts.UseValueObjects();                    // or opts.UseValueObjects(messages => messages.UseResource<ValidationMessages>())
    opts.RejectUnparseableQueryValues = true;  // recommended, for the parameters Wolverine still binds itself
});
```

Wolverine.Http binds a route, query, header or form value to any type with a public static `TryParse`, a value object
among them, but answers every failure in its own terms (WolverineFx.Http 6.51.0):

- **A refused or unparseable route value is a 404**, with no message and no code.
- **A refused query value reaches the handler as the default instance** under the default
  `RejectUnparseableQueryValues = false`: `?qty=500` runs the handler with `qty=0`, and so do `?qty=x`, `?qty=` and no
  `qty` at all. Under `RejectUnparseableQueryValues = true`, a refused value is a 400 naming the first parameter alone,
  quoting the refused text, without the rule's code; an absent or empty value still binds the default instance.
- **A header and a form field are never strict**: `X-Customer: abc` and a form field `qty=500` bind the default instance
  in both modes. A header sent twice throws `InvalidOperationException: Sequence contains more than one element`.
- **An array drops what it refuses**: `?qty=1&qty=500` binds `[1]`.
- **`[FromQuery]` on a value object stops the application at startup**: `InvalidOperationException: Sequence contains no
  elements`.
- **A compound handler's `Before` method sees the default instance too.**

`UseValueObjects()` plugs a parameter strategy into Wolverine's code generation, ahead of Wolverine's own. The code
Wolverine writes for an endpoint then calls the package's typed helpers for each value object, so that `TSelf.TryParse`
runs closed at compile time, with no registry, no visitor and no reflection.

## Where a value is read from

Call `UseValueObjects()` inside the callback of `MapWolverineEndpoints`, the one place Wolverine accepts a parameter
strategy. Every parameter whose type is a value object `T`, a `T?`, a `T[]`, a `List<T>`, an `IList<T>`, an
`IReadOnlyList<T>` or an `IEnumerable<T>` is claimed, on the endpoint's method and on its middleware methods (`Before`,
`Load`, `Validate`) alike, and read from the source Wolverine reads it from, in Wolverine's order:

1. a form field, under `[FromForm]`, by its `Name` or the parameter's;
2. a route value: the one `[FromRoute(Name = …)]` names, compared ignoring case as Wolverine compares it, or the one of
   the parameter's name, which wins over `[FromHeader]` and `[FromQuery]`, as in Wolverine;
3. a header, under `[FromHeader]`, by its `Name` or the parameter's;
4. the query string otherwise, under `[FromQuery]`'s `Name` or the parameter's.

A collection binds from the query string or a form only; one bound to a route value or a header is left to Wolverine. A
parameter another strategy binds is left alone: `[AsParameters]`, `[FromBody]`, `[FromServices]`, `[FromKeyedServices]`,
Wolverine's `[NotBody]`, and Wolverine's parameter attributes (`[Entity]`, `[Aggregate]` and the others). A value is
read once per endpoint, so a compound handler's `Before(Quantity qty)` and its handler share the same `qty`; the same
name bound to two value objects in one endpoint stops `MapWolverineEndpoints` with an `InvalidOperationException`.

Wolverine looks a value up again by its source and name for an `After` or `Finally` method, for a route value a
middleware method takes, and for the identity `[Entity]` and the other parameter attributes load by: each finds the
package's read of a value the endpoint's value objects read, so it receives the value the rules accepted, and none runs
for a refused request. A middleware method that a policy registered after `UseValueObjects()` adds and matches the
parameters of (`chain.ApplyParameterMatching(call)` in `ConfigureEndpoints`) has its value objects read and answered
the same way, before it runs.

A generic value object's construction and a value object written by hand bind without registration: the generated code
names the type.

## A refused value

Every refusal of a request is answered at once, with the validation problem a minimal API answers, through
`IProblemDetailsService` when the application registers one:

```text
GET /when?due=x&at=x&consent=x&amount=-1
400 {"title":"One or more validation errors occurred.","status":400,
     "errors":{"due":["The text is not a valid System.DateOnly."],"at":["The text is not a valid System.DateTimeOffset."],
               "consent":["The text is not a valid bool."],"amount":["The value must be greater than or equal to 0."]},
     "errorCodes":{"due":"value_object.not_parsable","at":"value_object.not_parsable",
                   "consent":"value_object.not_parsable","amount":"value_object.out_of_range"}}
```

- Each member is listed under the name it binds from (`X-Customer` for a header named so), with the message of the rule,
  never the refused text, and its code under `errorCodes`, keyed through the HTTP JSON options' dictionary key policy as
  `errors` is. The elements of a collection a request refuses are listed under its name, one message each, with the code
  of the first.
- The answer comes before any middleware method or the handler runs: a `Before`, `Load` or `Validate` method never sees
  a refused value.
- The codes are also recorded on the request, where a middleware, a log or `CustomizeProblemDetails` reads them with
  `ValueObjectProblemDetails.GetErrorCodes(httpContext)`.
- **A refused route value is a 400**, where Wolverine alone answers 404. A route constraint, `{qty:int}`, still answers
  404 for text it refuses, since routing matches no endpoint; `/lines/500` reaches the binding and its 400.
- Where reflection-based serialization is off, as under native AOT, `MapWolverineEndpoints` throws an
  `InvalidOperationException` naming `AddProblemDetails()` when the HTTP JSON options cannot write the validation problem,
  rather than answer each refusal with a 500.

## Absent and empty values

- An absent value that cannot be `null` is refused, `value_object.required`, "A value is required.", unless the
  parameter has a default value, as a minimal API decides. An optional route segment, `{qty?}`, does not make a value
  object optional: declare the parameter `T?`, which binds `null` when the value is absent.
- An absent collection binds an empty one.
- Empty text is parsed, nullable or not: `?country=` is refused by the type's rule, `value_object.required`, "The value
  must not be empty.", and `?qty=` over a number is `value_object.not_parsable`. Only an absent value binds `null`.
- A query key or a header sent twice is parsed as its values joined by a comma, as a minimal API reads it, and refused by
  the rules of most types; a collection reads each value.

## Messages in the request's language

`UseValueObjects(messages => messages.UseResource<T>())` reads the message of each code from the application's
resource, as the minimal API package does ([Messages in the application's
language](./aspnet-core.md#messages-in-the-applications-language)). A Wolverine endpoint runs in the ASP.NET Core
pipeline, so `UseRequestLocalization()` sets the request's culture as for any endpoint; `AddLocalization()` registers the
localizer the lookup needs, and without it a refusal throws an `InvalidOperationException` naming the call. With the
French example of the [errors reference](../reference/errors.md#a-message-in-the-applications-language):

```text
GET /items?qty=500         Accept-Language: fr-FR   →  « qty » doit être compris entre 1 et 100.
GET /optional?country=XX   Accept-Language: fr-FR   →  « country » doit être l'une des valeurs suivantes : FR; DE.
```

A code the resource has no message for keeps the English message; the codes never change.

## What stays with Wolverine

- **`[AsParameters]` members.** Wolverine binds them itself, without asking a strategy: leniently under the default, or
  with its own 400, which carries no code, under `RejectUnparseableQueryValues = true`. So do the members of a complex
  `[FromQuery]` or `[FromForm]` type, and a parameter of an `After` or `Finally` method reading a value no value object
  of the endpoint reads. A property of a complex `[FromQuery]` or `[FromForm]` type bound to the very name a value
  object of the endpoint reads stays at its default: take the value from the parameter. Set
  `RejectUnparseableQueryValues = true` for them, as Wolverine itself recommends; it also silences the warning
  Wolverine logs at startup while the flag is off, which describing a value object's query parameter for OpenAPI
  triggers, though the package binds that parameter strictly whatever the flag.
- **The JSON body.** It is read through the generated converter, under the HTTP JSON options, so a refused body is a 400,
  but Wolverine writes it itself: `"title":"Invalid JSON format"`, the rule's message in `detail`, and neither the code
  nor the member, since Wolverine hands `IProblemDetailsService` no exception.

## OpenAPI

Nothing to add: Wolverine's ApiExplorer descriptions name the value object as each route, query, header and form
parameter's type, and the [OpenAPI transformer and the Swashbuckle filters](./openapi.md) describe it with its rules, as
for a minimal API. A path parameter is `required`; Wolverine marks no query, header or form parameter required.

## Static mode

Wolverine's static mode (`TypeLoadMode.Static`) runs the code `codegen write` wrote beforehand, compiled into the
application. That code calls the package by name, and static mode never notices code written by another version: **run
`codegen write` again after installing or upgrading the package**, or the endpoints keep the binding the code was written
with, Wolverine's own included.

## Native AOT

The package is AOT-compatible: the code Wolverine writes names each value object, and the strategy runs at startup, when
Wolverine builds its endpoints, in static mode too. Under native AOT, Wolverine requires static mode and its
pre-generated code, and the application calls `builder.Services.AddProblemDetails()`, which lets the HTTP JSON options
write the validation problem without reflection. CI publishes an application serving Wolverine.Http endpoints this way,
its code written by `codegen write` first, and compares what the native binary answers with what it answers under the
JIT.

## Antiforgery

A form value keeps its endpoint marked as reading a form, as Wolverine's own `[FromForm]` binding marks it, so that
`AutoAntiforgeryOnFormEndpoints()` still requires an antiforgery token of it.

## Messaging

Wolverine's messaging needs no package. Its default serializer is System.Text.Json, which writes a value object as its
underlying value and reads it back through the generated converter: a message holding a value its type refuses fails,
and over a transport it moves to the error queue, the logged exception being the converter's `ValueObjectJsonException`,
from which `ValueObjectErrors.TryGetCode` reads the code. A member the payload leaves out holds the default instance:
declare it `T?` or `required` ([A member missing from the payload](./json.md#a-member-missing-from-the-payload)).

## Not covered

- F# endpoints: the frames the package adds to the generated code write C# alone.
- `[Entity]` and the Marten aggregate attributes of WolverineFx.Http.Marten over a value-object identifier, loaded from
  a store: the strategy leaves the attributed parameter to them. Where the endpoint takes the identifier as a value
  object too, their lookup by name finds the package's read; otherwise their loader reads the identifier itself.
- The dead-letter record a durable message store keeps for a refused message.
