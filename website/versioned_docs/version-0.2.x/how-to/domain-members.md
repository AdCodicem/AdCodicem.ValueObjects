---
title: Add Domain Behaviour
sidebar_label: Domain behaviour
slug: /how-to/domain-members
description: Add your own factories, properties and methods next to the generated members of a value object.
---

# Add domain behaviour

The generator owns construction, conversion, equality and text. Everything else about the concept is yours, and
lives in the same `partial` declaration.

## A factory of your own

```csharp
[ValueObject<Guid>]
public readonly partial struct CustomerId : IValueObjectValidator<Guid>
{
    public static CustomerId New() => CreateUnchecked(Guid.CreateVersion7());

    public static ValidationResult ValidateValue(in Guid value)
        => value == Guid.Empty
            ? ValidationResult.Required("A customer identifier must not be empty.")
            : ValidationResult.Success;
}
```

`CreateUnchecked` skips normalization and validation. It is legitimate here because the application produced
the value itself, and never for input that comes from outside — a request, a file, a message.

## Properties derived from the value

```csharp
[ValueObject<string>(MinLength = 15, MaxLength = 34, Pattern = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
public readonly partial struct Iban
{
    public string CountryCode => Value[..2];
}
```

`Value` is always normalized and valid on an instance that was constructed, so a derived property can rely on
the rules: this one needs no length check, because `MinLength = 15` has run.

## What not to add

- **A constructor.** The generator owns it, so that no path skips the rules.
- **Fields.** A value object holds one value; a concept made of several belongs in an ordinary type that holds
  several value objects.
- **Equality members.** The generated ones honour the declared `Comparison`; a hand-written `Equals` would not.

Next: [Test your value objects](./test-value-objects.md), which checks the generated and the hand-written parts
together.
