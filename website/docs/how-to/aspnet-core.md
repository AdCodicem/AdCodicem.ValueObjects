---
title: Use with ASP.NET Core
sidebar_label: ASP.NET Core
slug: /how-to/aspnet-core
description: Bind value objects from routes, query strings, headers and bodies in MVC and minimal APIs, know what a rejection answers in each, and return RFC 9457 problem details carrying the violated rule from MVC controllers.
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
and a status, once `AddProblemDetails()` is registered, and `AddValidation()` adds nothing to it. In Development,
minimal APIs throw a `BadHttpRequestException` instead (`RouteHandlerOptions.ThrowOnBadRequest`), which
`UseExceptionHandler` answers with a 500. The [problem details carrying the rule](#problem-details-carrying-the-rule)
are MVC's, so a client of an application that also validates DataAnnotations meets two shapes of 400: a full
validation problem for those, and the bare one for a rejected value object.

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
`Guid?`: `?sku=` binds `null` to a nullable parameter, where the reflection-based binding answers 400.

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
is absent, and a 400 when it is present and rejected. Empty or white-space text, `?country=` or `?country=%20`, binds
as absent, as MVC binds an `int?` or a `Guid?`. A value object that cannot be `null`, a route segment such as
`CustomerId id`, refuses blank text as MVC refuses it for an `int`: a 400 with "The value ' ' is invalid." and the
code `value_object.required`, rather than an instance no rule has checked.

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
            [ValueObjectProblemDetails.ExtensionName] = result.Errors
                .GroupBy(failure => failure.PropertyName)
                .ToDictionary(member => member.Key, member => member.First().ErrorCode),
        });
```

The validator of the [FluentValidation](./fluentvalidation.md#text-that-must-become-a-value-object) guide stops each
member at its first failure with `Cascade(CascadeMode.Stop)`, so a member is reported once: empty text fails
`NotEmpty()` and never reaches `MustParseAs`, and `errors` holds one message for it. A validator that does not stop,
or that states several rules for one member, can fail a member more than once, which is why the failures are
grouped by member: each member reports the code of the first rule it failed, and the dictionary never meets the same
member twice.
