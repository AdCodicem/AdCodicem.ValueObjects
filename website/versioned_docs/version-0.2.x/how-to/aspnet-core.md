---
title: Use with ASP.NET Core
sidebar_label: ASP.NET Core
slug: /how-to/aspnet-core
description: Bind value objects from routes, query strings, headers and bodies in MVC and minimal APIs, and return RFC 9457 problem details carrying the violated rule.
---

# Use with ASP.NET Core

## Minimal APIs

Nothing to install. A generated value object implements `IParsable<T>` and `ISpanParsable<T>`, which is exactly
what minimal API parameter binding looks for, and its `[JsonConverter]` covers request and response bodies:

```csharp skip
app.MapGet("/accounts/{iban}", (Iban iban) => /* … */);
```

A value that fails to parse is answered with a 400 before the handler runs.

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

This covers what the model binder rejects: route values, query strings, headers and forms. A value inside a JSON
body is rejected by the serializer, and the 400 carries its message but no code.

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
