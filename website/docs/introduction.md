---
title: Introduction
sidebar_label: Introduction
slug: /introduction
---

# AdCodicem.ValueObjects

Single-value DDD value objects for .NET 10, with no reflection and no allocation on the paths that matter.

Declare the type and its rules once; the framework carries them into JSON, the database, model binding and the
OpenAPI document, so they cannot drift apart.

```csharp
[ValueObject<string>(
    MinLength = 15,
    MaxLength = 34,
    Pattern = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
    SchemaFormat = "iban")]
public readonly partial struct Iban
{
    private static string NormalizeCore(string value) => /* strip separators, upper-case */;

    private static ValidationResult ValidateCore(in string value)
        => HasValidCheckDigits(value)
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");
}
```

That declaration generates the constructor, `Create` / `TryCreate` / `CreateUnchecked`, `Parse` / `TryParse`
(string and span), `ToString` / `TryFormat`, equality, ordering, the `System.Text.Json` converter, the
`TypeConverter`, and the runtime registration — around 400 lines you no longer maintain.

```csharp
var iban = Iban.Create("fr76 3000 6000 0112 3456 7890 189");
iban.Value                                  // "FR7630006000011234567890189"
iban.ToString(Iban.Formats.Print, null)     // "FR76 3000 6000 0112 3456 7890 189"

JsonSerializer.Serialize(new { iban })      // {"iban":"FR7630006000011234567890189"}
```

An IBAN crosses every boundary as its underlying type: a JSON string, a `VARCHAR`, a query-string parameter —
never an object wrapper. Consumers define their own value objects; this framework ships the generator.

Next: [Getting Started](./getting-started.md).
