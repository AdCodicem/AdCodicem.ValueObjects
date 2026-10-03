---
title: Use with ASP.NET Core
sidebar_label: ASP.NET Core
slug: /how-to/aspnet-core
description: Bind value objects from routes, query strings, headers and bodies in MVC and minimal APIs, and return RFC 9457 problem details carrying the violated rule.
---

# Use with ASP.NET Core

## Minimal APIs

Binding needs no package. A generated value object implements `IParsable<T>` and `ISpanParsable<T>`, which is
exactly what minimal API parameter binding looks for, and its `[JsonConverter]` covers request and response bodies:

```csharp skip
app.MapGet("/accounts/{iban}", (Iban iban) => /* … */);
```

A value the value object rejects is answered with a 400 before the handler runs, and that 400 says nothing of why.
It names no parameter and carries no message and no code: its body is empty, or holds bare problem details, a title
and a status, once `AddProblemDetails()` is registered. In Development, minimal APIs throw a
`BadHttpRequestException` instead (`RouteHandlerOptions.ThrowOnBadRequest`), which `UseExceptionHandler` answers with a
500. The [problem details carrying the rule](#problem-details-carrying-the-rule) are MVC's. Empty text is not MVC's
rule either: under the reflection-based binding, `?country=` for a `CountryCode?` is a 400, as it is for an `int?`.

Where the Request Delegate Generator writes the binding, in every build of a project that sets `PublishAot` or
`PublishTrimmed`, it does not see the `IParsable<T>` the generator adds to a value object declared in the same
project: a route value is then answered with a 400, and a query value is bound to `null`. Declare the value objects
in another project, a domain project, or list the contract on the value object's own declaration,
`public readonly partial struct Sku : IValueObject<Sku, string>;`.

## MVC controllers

```bash
dotnet add package AdCodicem.ValueObjects.AspNetCore
```

```csharp skip
builder.Services.AddControllers().AddValueObjects();
```

That registers a model binder for value objects — routes, query strings, headers, forms — and the JSON options
for bodies. The binder is closed over each concrete type, so binding costs one `TryParse`. An application that
configures MVC directly can call `AddValueObjects()` on `MvcOptions` instead; that one adds the binder only.

Nullable value objects bind as you would expect: `[FromQuery] CountryCode? country` is `null` when the parameter
is absent, and a 400 when it is present and rejected.

## Problem details carrying the rule

For MVC controllers, which the package serves:

```csharp skip
builder.Services.Configure<ApiBehaviorOptions>(options => options.AddValueObjectProblemDetails());
```

When model binding rejects a value, the automatic 400 response gains an `errorCodes` member mapping each rejected
parameter to the stable code of the rule it violated:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "country": ["The value is not one of the accepted values."] },
  "errorCodes": { "country": "value_object.not_a_known_value" }
}
```

A client branches on `value_object.not_a_known_value`, not on English. The member name is available as
`ValueObjectProblemDetails.ExtensionName`.

This covers what the MVC model binder rejects: route values, query strings, headers and forms. A value inside a JSON
body is rejected by the serializer, and the 400 carries its message but no code. `ApiBehaviorOptions` is MVC's, and
minimal APIs never read it, so their rejections keep the [bare 400](#minimal-apis).

## Codes for a payload you validate yourself

For a payload that carries raw text — an inbound message from another system, say — validate it with
[FluentValidation](./fluentvalidation.md) and put the codes under the same member, so every 400 of the API has
the same shape:

```csharp skip
var result = validator.Validate(request);

return result.IsValid
    ? Results.Accepted()
    : Results.ValidationProblem(
        result.ToDictionary(),
        extensions: new Dictionary<string, object?>
        {
            [ValueObjectProblemDetails.ExtensionName] =
                result.Errors.ToDictionary(failure => failure.PropertyName, failure => failure.ErrorCode),
        });
```
