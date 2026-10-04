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
| `SchemaFormat`, or the natural format of the type (`uuid`, `date`, `int64`…; none for a `TimeSpan`, see below) | `format` |
| `MinLength`, `MaxLength` | `minLength`, `maxLength` |
| `IValueObjectPatternValidator`, or the deprecated `Pattern` option | `pattern` |
| `IValueObjectMinimum<T>`, `IValueObjectMaximum<T>`, or the deprecated `Minimum` and `Maximum` options | `minimum`, `maximum` for a number; for a value written as a string, see below |
| `[KnownValue]` on a closed set | `enum`, each value as the type writes it in JSON; the names in `x-enum-varnames`, `x-enumNames` and `x-ms-enum`, with the descriptions declared, see [below](#names-of-known-values) |
| `Example` | an example, written as the type writes it in JSON; one the type refuses fails the build (`VO0031`) or the [contract kit](./test-value-objects.md#what-it-checks) |
| `Description`, or the type's XML `<summary>` as plain text | `description` |

The same rules reach a JSON Schema exported by System.Text.Json, for a tool, structured output or a contract, through
[`ValueObjectJsonSchema`](./json.md#json-schema), which describes a value object as this document does.

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

JSON Schema applies `minimum` and `maximum` to numbers only, so a value object written as a JSON string — an `Int128`
or a `UInt128`, which a JSON number would round, a `char`, a date, a time or a duration — does not get them: a client
would ignore them. Its bounds go to `x-minimum` and `x-maximum` instead, in the form the type writes them, for tools
that read extensions, and to a sentence after the description, `Between 1900-01-01 and 2100-12-31, inclusive.`, for
the people reading the document.

A `TimeSpan` has no natural format. JSON Schema's `duration` is ISO 8601, `PT1H30M`, while a duration is read and
written in the invariant constant form `[-][d.]hh:mm:ss[.fffffff]`, `01:30:00`: a client generated from a `duration`
format would send what the server refuses. A value object over a `TimeSpan` is therefore documented as the built-in
stack documents a plain `TimeSpan`, a `string` held to the pattern `^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$`, which
its example and its bounds match. A client generator types such a property as a string rather than a duration, as it
types a plain `TimeSpan` of the same document. `SchemaFormat` still sets a `format`, and a pattern declared on the type
replaces this one.

A bound is published as it is declared and enforced, never normalized as an input would be: the check compares the
normalized value with the bound itself. An example is an input, parsed and normalized as the type parses text, then
written by the type's converter; one the type refuses, or one its converter cannot write, such as `NaN` without the
named literals, is published as it was declared rather than failing the document. A known value is written by the
type's converter, as the type holds it once normalized.

The transformer targets the built-in OpenAPI stack. Swashbuckle is not supported.

## Names of known values

A closed set's `enum` lists values, and a client generator names the members of the enumeration it makes from them:
`FR` and `DE` where the server code says `France` and `Germany`, and something mangled for a value such as `01` or
`credit-card`, which is no identifier at all. The transformer publishes the name of each known value beside the
`enum`, in the extension each generator reads:

| Extension | Shape | Read by |
| --- | --- | --- |
| `x-enum-varnames` | the names, in `enum` order | openapi-generator, Scalar |
| `x-enumNames` | the names, in `enum` order | NSwag |
| `x-ms-enum` | `{ "name", "modelAsString": false, "values": [{ "value", "name", "description" }] }` | Kiota, AutoRest |

So this closed set:

```csharp
[ValueObject<string>(ValueSet = ValueSetKind.Closed)]
[KnownValue("France", "FR", Description = "Mainland France and its overseas departments.")]
[KnownValue("Germany", "DE")]
public readonly partial struct CountryCode;
```

is published as:

```json
{
  "enum": ["FR", "DE"],
  "type": "string",
  "x-enum-varnames": ["France", "Germany"],
  "x-enumNames": ["France", "Germany"],
  "x-ms-enum": {
    "name": "CountryCode",
    "modelAsString": false,
    "values": [
      { "value": "FR", "name": "France", "description": "Mainland France and its overseas departments." },
      { "value": "DE", "name": "Germany" }
    ]
  }
}
```

A value in `x-ms-enum` is written as the `enum` writes it, a number as a number and a date in its round-trip form,
and carries a description only where its `[KnownValue]` declares one. The enumeration is named as the value object's
component is, `ReferenceOfPurchaseOrder` for a construction of a generic one, and as that component would be when the
value object is described in place, as a parameter. The object form of `x-enum-descriptions`, keyed by value, which
Scalar and Redocly read, is never written: NSwag refuses the whole document over it.

Kiota 1.35.0 and NSwag 14.7.1, run against such a document, name the members after the known values, `France` rather
than `FR`, Kiota with the description as the member's summary; see [HTTP clients](./http-clients.md). An open value
set has no `enum`, and its known values are not published.

The names are a contract of every client generated from the document: renaming a known value renames the member of
its enumeration in each of them, as renaming the member of a C# `enum` would. There is no option to leave the
extensions out, since a tool that does not know them ignores them. The names come from the schema the type declares,
`Schema.KnownValueDetails`, which the generator fills from the `[KnownValue]` attributes.

## Parameters, collections and dictionaries

A value object is described the same way wherever it appears, not only as a property of a body:

- **A route, query or header parameter**, in a minimal API, an MVC action, a type gathered with `[AsParameters]` or a
  model MVC binds from the query string, nullable or not. ASP.NET Core documents a parameter bound from text as a
  `string`; the transformer gives it the schema of its value object in place, so a `Quantity` parameter is an
  `[integer, string]` with its bounds and the numeric pattern, as a raw `int` parameter of the same document is, and a
  closed set keeps its `enum`. The schema stays in place rather than becoming a reference: it is the parameter's own,
  holding what the parameter's declaration adds, such as a default value. A route constraint is a rule the request
  satisfies too: of its bounds and lengths and the value object's, the stricter stay, and its `regex` stays when the
  value object has no pattern. A parameter ASP.NET Core already documents
  as the value object itself, as a minimal API's header parameter, refers to the component.
- **An element of a collection**, `List<Iban>`, `Quantity[]`, `IReadOnlyList<CountryCode>`, nested ones included,
  and **a value of a dictionary**, `Dictionary<string, Quantity>`: `items` and `additionalProperties` refer to the
  value object's component, as a property of that type does. System.Text.Json leaves them out for a type with a
  converter of its own, so they would otherwise be an array or an object of anything. An element of a nullable value
  object, `List<Quantity?>`, is described in place instead, with `null` added to its type.
- **A key of a dictionary**, `Dictionary<CountryCode, int>`, when the value object is documented as a string: a key is
  written as text, so its rules go to `propertyNames`, which an OpenAPI 3.0 document carries as the
  `x-jsonschema-propertyNames` extension. A key over a number or a boolean is left undescribed, since the name of a
  member is neither.

## Numbers written as text

A value object over a number follows `JsonSerializerOptions.NumberHandling` as its underlying type does, and is
documented as System.Text.Json documents that type under the same options. ASP.NET Core reads numbers written as
text by default (`AllowReadingFromString`), so a number is documented as `[integer, string]` or `[number, string]`
with the pattern a number written as text is held to, as a bare `int` property of the same document is:

- under `AllowReadingFromString` alone, the value is still written as a number, which `minimum` and `maximum`
  describe;
- under `WriteAsString`, it goes out as a string, its example and known values with it, and its bounds are also
  stated as `x-minimum`, `x-maximum` and a sentence, as for any value written as a string;
- over a `double` or a `float`, under `AllowNamedFloatingPointLiterals`, it is the number or one of `"NaN"`,
  `"Infinity"` and `"-Infinity"` (`anyOf`), listing only the literals its bounds let through: a bound refuses `NaN`,
  and the infinity on its side.

## Value objects written by hand

A value object written by hand where no generator runs is described from the `Schema` it declares, as the registry
describes it by reflection the first time it meets the type, unless it was registered with a schema of its own, which is
published as it was built. An annotation it also carries is not read: nothing generates from it there, so its rules,
its known values as the type holds them and its description belong in the schema. A known value of another type than
the underlying one is listed as its text. The names of its known values belong there too, in `KnownValueDetails`, one
`KnownValueInfo` per value of `KnownValues`, in the same order: names that do not list those values one for one are
not published, rather than published beside the wrong value.
