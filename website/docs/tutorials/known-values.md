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
[KnownValue("France", "FR", Description = "France")]
[KnownValue("Belgium", "BE", Description = "Belgium")]
[KnownValue("Luxembourg", "LU", Description = "Luxembourg")]
public readonly partial struct CountryCode : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}
```

Each `[KnownValue]` takes a member name and a value. The generator turns them into:

- **Named constants**: `CountryCode.France`, `CountryCode.Belgium`, `CountryCode.Luxembourg`.
- **`CountryCode.KnownValues`**: every known value, as an `ImmutableArray<CountryCode>` in declaration order.
- **A membership check**: with `ValueSet = ValueSetKind.Closed`, anything else is rejected with
  `value_object.not_a_known_value`, through a frozen lookup.
- **An OpenAPI `enum`**: `["FR", "BE", "LU"]`, so clients see the accepted values without anyone restating them.

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

Without `ValueSet = ValueSetKind.Closed`, the set is open: the constants and `KnownValues` are still generated,
but any value that satisfies the other rules is accepted. That suits a list the domain names only in part, such
as the currencies the application treats specially among all ISO 4217 codes.

## Values that are not strings

An attribute argument can only be a constant, so a `Guid`, a `decimal` or a `DateOnly` is written as text, in
the form the [authoring reference](../authoring-guide.md#bounds-and-known-values-written-as-text) gives for its
type, and converted at compile time. A value that does not convert is `VO0013`; a member name
that is not a valid C# identifier is `VO0006`; a closed set with no value at all is `VO0005`, since no value
could ever be valid.

Next: [From request to database](./request-to-database.md), which puts these types behind an API.
