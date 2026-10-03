---
title: Introduction
sidebar_label: Introduction
slug: /introduction
description: An answer to primitive obsession in .NET 10 and later — single-value DDD value objects generated at compile time, whose rules reach JSON, EF Core, model binding and OpenAPI.
---

# AdCodicem.ValueObjects

An answer to primitive obsession for .NET 10 and later: single-value DDD value objects, generated at compile time,
with no reflection and no allocation on the paths that matter.

## Primitive obsession

```csharp skip
Task PayAsync(string customerId, string iban, decimal amount);
```

A call to it compiles with the two strings swapped, `"hello"` passes for a bank account, and the signature says
nothing about what an IBAN is. So every layer says it again: the controller checks the format, a migration
guesses the column width, the OpenAPI document settles for `string`, and nothing keeps the three in agreement.
That is primitive obsession — domain concepts carried as bare `string`, `int` and `Guid`.

The remedy is well known: give each concept a type that cannot hold an invalid value. It stays rare because the
type is only the start. It also needs equality, parsing, formatting, a JSON converter, an EF Core value
converter, a model binder and a schema — a few hundred lines per concept, which is why codebases drift back to
`string`.

Here the type costs one declaration. Its rules are written once and carried into JSON, the database, model
binding and the OpenAPI document, so they cannot drift apart. This compiles as it stands:

```csharp
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects;
using AdCodicem.ValueObjects.Annotations;

namespace Banking;

[ValueObject<string>(
    MinLength = 15,
    MaxLength = 34,
    SchemaFormat = "iban")]
public readonly partial struct Iban
    : IValueObjectNormalizer<string>, IValueObjectPatternValidator, IValueObjectValidator<string>
{
    // Runs first, on every way in: "fr76 3000 6000 …" and "FR7630006000…" are the same account.
    public static string NormalizeValue(string value)
        => value.Replace(" ", "").Replace("-", "").ToUpperInvariant();

    // Runs once the declared length holds. Compiled at build time, and published as the OpenAPI pattern.
    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    // Runs once the declared length and pattern hold: the ISO 7064 MOD-97-10 check digits.
    public static ValidationResult ValidateValue(in string value)
    {
        var remainder = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[(i + 4) % value.Length];
            remainder = char.IsAsciiDigit(c)
                ? ((remainder * 10) + (c - '0')) % 97
                : ((remainder * 100) + (c - 'A' + 10)) % 97;
        }

        return remainder == 1
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");
    }
}
```

That declaration generates the constructor, `Create` / `TryCreate` / `CreateUnchecked`, `Parse` / `TryParse`
(string and span), `ToString` / `TryFormat`, equality, ordering, the `System.Text.Json` converter, the
`TypeConverter`, and the runtime registration — around 400 lines you no longer maintain.

```csharp skip
var iban = Iban.Create("fr76 3000 6000 0112 3456 7890 189");
iban.Value                                  // "FR7630006000011234567890189"
Iban.TryCreate("FR00 0000", out _)          // false: rejection is not an exception
JsonSerializer.Serialize(new { iban })      // {"iban":"FR7630006000011234567890189"}

Task PayAsync(CustomerId customer, Iban iban, decimal amount);   // swapping the two no longer compiles
```

An IBAN crosses every boundary as its underlying type: a JSON string, a `VARCHAR`, a query-string parameter —
never an object wrapper. Consumers define their own value objects; this framework ships the generator.

Already using Vogen, StronglyTypedId or Thinktecture? [How this library compares](./explanation/comparison.md),
and [how to migrate](./how-to/migrating.md).

## How this documentation is organized

- **Tutorials** teach by building something, one step at a time. Start with
  [Getting started](./getting-started.md), then follow the sequence.
- **How-to guides** answer a precise question — wiring EF Core, returning error codes, formatting a value — for
  a reader who already knows the basics.
- **Reference** lists the surface without commentary: attribute options, generated members, error codes,
  diagnostics, and the API reference generated from the source.
- **Explanation** is about why: primitive obsession, the design decisions and their costs, the benchmarks behind
  them, and how this library compares with others.
- The **[FAQ](./faq.md)** gives short answers and points to the page that explains each one.

Next: [Getting started](./getting-started.md).
