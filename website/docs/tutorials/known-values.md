---
title: Known Values
sidebar_label: Known values
slug: /tutorials/known-values
description: Model reference-data codes as value objects with named known values, a closed set, and an OpenAPI enum.
---

# Known values

Country codes, currency codes, status codes: reference data is usually a short list of values that the domain
names and the wire carries as text. A C# `enum` names them but carries neither validation nor a stable wire
format; a bare `string` carries the format and nothing else. A value object with known values does both.

## Declare the values

```csharp
[ValueObject<string>(ValueSet = ValueSetKind.Closed, MinLength = 2, MaxLength = 2, SchemaFormat = "iso-3166-alpha2")]
public readonly partial struct CountryCode : IValueObjectNormalizer<string>
{
    /// <summary>France.</summary>
    [KnownValue]
    public static readonly CountryCode France = Known("FR");

    [KnownValue(Description = "Belgium")]
    public static readonly CountryCode Belgium = Known("BE");

    [KnownValue(Description = "Luxembourg")]
    public static readonly CountryCode Luxembourg = Known("LU");

    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}
```

A known value is a member you write, marked `[KnownValue]`: a `static readonly` field, or a static property with a
getter alone, of the value object's own type and of any accessibility. You name it, and you initialize it through
`Known`, which the generator writes on the type and the compiler checks like any other call: `Known(42)` does not
compile on a `CountryCode`. `Known` normalizes the value and applies every rule of the type but membership, which a
known value satisfies by declaration. Its description is the `Description` of the attribute, or else the
`<summary>` of the member.

The generator reads those members and adds:

- **`CountryCode.KnownValues`**: every known value, as an `ImmutableArray<CountryCode>` in declaration order.
- **A membership check**: with `ValueSet = ValueSetKind.Closed`, anything else is rejected with
  `value_object.not_a_known_value`, through a frozen lookup built from the members.
- **An OpenAPI `enum`**: `["FR", "BE", "LU"]`, so clients see the accepted values without anyone restating them,
  with the names beside them, `France`, `Belgium`, `Luxembourg`, which a generated client names the members of its
  enumeration after, and each description ([names of known values](../how-to/openapi.md#names-of-known-values)).
  Renaming a known value therefore renames that member in every client generated afterwards.

## Use it

```csharp skip
var country = CountryCode.Create(" be ");       // normalized, then found in the set
country == CountryCode.Belgium                  // true

CountryCode.TryCreate("ZZ", out _, out var validation);
validation.ErrorCode                            // "value_object.not_a_known_value"

foreach (var known in CountryCode.KnownValues)  // FR, BE, LU
{
}
```

The membership check runs after normalization and the declared rules, like every other rule. `" be "` is
accepted because it becomes `"BE"` first.

## Open sets

Without `ValueSet = ValueSetKind.Closed`, the set is open: the known values and `KnownValues` are still there, but
any value that satisfies the other rules is accepted, and the schema lists no `enum`. That suits a list the domain names only in part, such
as the currencies the application treats specially among all ISO 4217 codes.

## Values that are not strings

The value is an expression of the underlying type, which the compiler checks, so a `Guid`, a `decimal` or a
`DateOnly` is written as itself:

```csharp
[ValueObject<decimal>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct VatRate
{
    [KnownValue]
    public static readonly VatRate Standard = Known(0.20m);

    [KnownValue]
    public static readonly VatRate Reduced = Known(0.055m);
}
```

A closed set with no known value at all is `VO0005`, since no value could ever be valid. A member marked
`[KnownValue]` that is not a static, read-only member of the type initialized through `Known` is `VO0036`, and a call
to `Known` anywhere else is `VO0037`: it would create a value the closed set refuses. A static member of a closed set
created through `Create` instead throws as the type initializes, since the lookup it would be checked against is built
from the known values, after them.

A value the type's own rules refuse, longer than its `MaxLength` say, is `VO0031` when it is a constant: each known
value is created as the type initializes, which the registration of the assembly runs as it loads, so a refused one
would stop the application. A value the compiler does not evaluate, `Known(new DateOnly(2024, 1, 31))`, is checked
by the [contract kit](../how-to/test-value-objects.md) instead.

## Migrating from known values written as text

Earlier versions declared a known value on the type, `[KnownValue("France", "FR")]`, its value written as text the
generator parsed. That form no longer compiles (`VO0034`). Its code fix rewrites each attribute into the member above,
the value written as an expression of the underlying type, `new Guid("…")` or `new DateOnly(2024, 1, 31)`, and fixes
every one in a document, a project or the solution at once.

Next: [From request to database](./request-to-database.md), which puts these types behind an API.
