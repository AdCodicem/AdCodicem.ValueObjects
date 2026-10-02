---
title: Getting Started
sidebar_label: Getting started
slug: /getting-started
description: Install AdCodicem.ValueObjects, declare a first value object, and see what the source generator writes for it.
---

# Getting started

This tutorial installs the package, declares a first value object, and looks at what the generator writes for
it. It needs the .NET 10 SDK and takes a few minutes.

## Install the package

```bash
dotnet add package AdCodicem.ValueObjects
```

That one package holds the contracts, the source generator and the analyzers. The integrations — EF Core,
ASP.NET Core, OpenAPI, source-generated JSON and the others — are separate packages, added when you reach that
boundary; [Packages](./packages.md) lists them.

## Declare a value object

An email address makes a good first candidate: it has a format, it should not care how the user typed it, and
it is routinely passed around as a bare `string`.

```csharp
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects;
using AdCodicem.ValueObjects.Annotations;

namespace Shop;

[ValueObject<string>(MaxLength = 254)]
public readonly partial struct EmailAddress : IValueObjectNormalizer<string>, IValueObjectPatternValidator
{
    public static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}
```

Three things make it a value object:

- **`readonly partial struct`.** `partial`, because the generator adds the implementation to the same type. A
  `readonly struct`, because a value object is a value: [Design decisions](./design-decisions.md) explains why
  neither a class nor a `record struct` is accepted.
- **`[ValueObject<string>]`** names the underlying type and declares the rules that need no code — here a maximum
  length.
- **The hook interfaces** declare the rules that do need code. The interface is how the generator finds the
  member, and how the compiler checks its signature. `IValueObjectNormalizer<string>` finds `NormalizeValue`.
  `IValueObjectPatternValidator` finds `Pattern`, a `[GeneratedRegex]` property that the .NET regex source
  generator compiles; its text is also the pattern the OpenAPI schema publishes.

Two namespaces are all a declaration needs: `AdCodicem.ValueObjects` for the contracts and hook interfaces,
`AdCodicem.ValueObjects.Annotations` for the attributes. Most projects add them as global usings. A pattern adds a
third, `System.Text.RegularExpressions`, which is not among the implicit usings.

## Use it

```csharp skip
var email = EmailAddress.Create("  Ada@Example.COM ");
email.Value                     // "ada@example.com"

EmailAddress.TryCreate("not an email", out _, out var validation)   // false, and nothing thrown
validation.ErrorCode            // "value_object.invalid_format"
validation.ErrorMessage         // "The value does not match the expected format."

EmailAddress.Create("not an email");   // throws ValueObjectException, carrying the same code
```

Every way in — `Create`, `TryCreate`, `Parse`, `TryParse`, JSON deserialization, model binding — runs the same
steps in the same order: **normalize**, check the declared rules and the pattern, run your own validator if there
is one, then assign. So an `EmailAddress` that exists is normalized and valid; no code that receives one has to
check it again.

`Create` throws, and suits domain code where a rejected value is a bug. `TryCreate` returns the reason instead of
throwing; the integrations go through it, or through `TryParse`, wherever outside input arrives.

## What the generator wrote

Nothing else is needed. From that declaration the generator produced:

- `Value`, `Create`, `TryCreate` and `CreateUnchecked`;
- `Normalize` and `Validate`, which run your rules and the declared ones;
- `Parse` and `TryParse` from a `string` or a span, and `ToString` / `TryFormat`;
- equality, hashing and ordering, with their operators;
- a `System.Text.Json` converter and a `TypeConverter`, so the value travels as a bare JSON string;
- a registration that makes the type discoverable at run time.

[Generated members](./reference/generated-members.md) lists them precisely. To read the code itself, set
`<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` in the project: the files land under
`obj/…/generated/AdCodicem.ValueObjects.Generators/`.

## `default` is a build error

A struct can always be brought into existence without its constructor, which would skip every rule:

```csharp skip
EmailAddress missing = default;         // error VO0010
var alsoMissing = new EmailAddress();   // error VO0010
```

The `VO0010` analyzer makes both a build error. Absence is expressed the usual way, with `EmailAddress?`.

## Next steps

- [Validation and normalization](./tutorials/validation-and-normalization.md) — the declared rules and the hooks,
  in the order they run.
- [From request to database](./tutorials/request-to-database.md) — the same types through ASP.NET Core, the
  OpenAPI document and EF Core.
- [Primitive obsession](./explanation/primitive-obsession.md) — the problem all of this answers.
