---
title: Compare Strings Without Case
sidebar_label: String comparison
slug: /how-to/string-comparison
description: Choose between normalizing a string value object and declaring a case-insensitive comparison, and keep the database collation in agreement.
---

# Compare strings without case

Two users type the same email address with different capitals. There are two ways to make the value object treat
them as one, and the first is usually the right one.

## Normalize, and keep the ordinal comparison

```csharp
[ValueObject<string>(MaxLength = 254)]
public readonly partial struct EmailAddress : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();
}
```

Every instance holds the lower-case form, so the default ordinal comparison is already correct — and so is a
unique index in the database, a `GROUP BY`, a hash set, and any system downstream that compares the values
without knowing the rule. The value is canonical wherever it goes.

## Declare the comparison, and keep the original spelling

When the original spelling must be preserved — a display name, a code a partner system echoes back
verbatim — declare the comparison instead:

```csharp
[ValueObject<string>(MaxLength = 64, Comparison = StringComparison.OrdinalIgnoreCase)]
public readonly partial struct PartnerReference;
```

`Comparison` drives the generated equality, ordering and hashing together, so `==`, `Equals`, `GetHashCode`,
`CompareTo` and a dictionary lookup all agree. It is also why the type is a struct the generator owns rather
than a `record struct`, whose field-wise equality would ignore it.

The EF Core comparer follows the same rule, so change tracking agrees with the application. The database does
not: give the column a case-insensitive collation, or a query and the application will disagree about which
values are equal.

`Comparison` applies to string value objects only.
