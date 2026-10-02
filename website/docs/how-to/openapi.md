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
object is then documented as what it is on the wire — its underlying type — carrying every rule declared on it:

| Declared on the type | In the schema |
| --- | --- |
| The underlying type | `type` |
| `SchemaFormat`, or the natural format of the type (`uuid`, `date`, `int64`…) | `format` |
| `MinLength`, `MaxLength` | `minLength`, `maxLength` |
| `IValueObjectPatternValidator`, or the deprecated `Pattern` option | `pattern` |
| `Minimum`, `Maximum` | `minimum`, `maximum` |
| `[KnownValue]` on a closed set | `enum`, each value as the type writes it in JSON |
| `Example` | an example |
| `Description`, or the type's XML `<summary>` | `description` |

So this declaration:

```csharp
using System.Text.RegularExpressions;

[ValueObject<string>(
    MinLength = 15,
    MaxLength = 34,
    SchemaFormat = "iban",
    Example = "FR7630006000011234567890189")]
public readonly partial struct Iban : IValueObjectPatternValidator
{
    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}
```

is documented as a `string` of format `iban`, between 15 and 34 characters, matching the pattern — never as an
object with a `value` property. There is nothing to restate in an annotation, and nothing to keep in sync: the
schema comes from the declaration that validates. The `pattern` keyword is the text of the `[GeneratedRegex]`,
read off the attribute when the type compiles.

That text is all the schema carries. `RegexOptions` are not part of it, so a client checking the `pattern` never
sees them: `IgnoreCase`, `Multiline`, `Singleline` or `IgnorePatternWhitespace` would make it accept or refuse
values the server judges otherwise, and are reported as `VO0025`. Write such a rule into the expression itself,
`[A-Za-z]` rather than `IgnoreCase`, and keep to constructs that mean the same in the ECMA-262 dialect OpenAPI
clients use.

A known value is written by the type's converter, as the type holds it once normalized. A value object written by
hand where no generator runs is described from its annotation instead, as the registry describes it by reflection the
first time it meets the type, registered or not: each known value
goes through the type too, normalized, or parsed when the attribute had to take it as text — a decimal, a `Guid`, a
date — and only one the type cannot parse is listed as written. Its description is the annotation's `Description`
alone: the XML summary a generated value object falls back on is not there to read at run time.

The transformer targets the built-in OpenAPI stack. Swashbuckle is not supported.
