---
title: Document in OpenAPI
sidebar_label: OpenAPI
slug: /how-to/openapi
description: Document value objects in the built-in .NET OpenAPI document as their underlying type, with the length, pattern, bounds and known values declared on them.
---

# Document in OpenAPI

```bash
dotnet add package AdCodicem.ValueObjects.OpenApi
```

```csharp skip
builder.Services.AddOpenApi(options => options.AddValueObjects());
```

That registers a schema transformer on the built-in .NET OpenAPI stack (`Microsoft.AspNetCore.OpenApi`). A value
object in a request or response body is then documented as what it is on the wire — its underlying type — carrying
every rule declared on it. A route, query or header parameter is not: it is documented as a plain `string`, whatever
the underlying type and without the rules, and a collection of value objects, a `List<Iban>`, as an array whose
items are not described:

| Declared on the type | In the schema |
| --- | --- |
| The underlying type | `type` |
| `SchemaFormat`, or the natural format of the type (`uuid`, `date`, `int64`…) | `format` |
| `MinLength`, `MaxLength` | `minLength`, `maxLength` |
| `Pattern` | `pattern` |
| `Minimum`, `Maximum` | `minimum`, `maximum` |
| `[KnownValue]` on a closed set | `enum` |
| `Example` | an example |
| `Description`, or the type's XML `<summary>` | `description` |

So this declaration:

```csharp
[ValueObject<string>(
    MinLength = 15,
    MaxLength = 34,
    Pattern = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
    SchemaFormat = "iban",
    Example = "FR7630006000011234567890189")]
public readonly partial struct Iban;
```

is documented as a `string` of format `iban`, between 15 and 34 characters, matching the pattern — never as an
object with a `value` property. There is nothing to restate in an annotation, and nothing to keep in sync: the
schema comes from the declaration that validates.

The transformer targets the built-in OpenAPI stack. Swashbuckle is not supported.
