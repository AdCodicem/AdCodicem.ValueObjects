---
title: Conversions and Arithmetic
sidebar_label: Conversions and arithmetic
slug: /how-to/conversions-and-arithmetic
description: Opt into implicit and explicit conversions, and into operators and generic math for numeric value objects, without letting a result escape validation.
---

# Conversions and arithmetic

Both are opt-in, per type. A value object that converts silently in both directions is a primitive with extra
steps; each option below loosens one thing, on purpose.

## Reading the value without `.Value`

```csharp
[ValueObject<string>(MaxLength = 254, ImplicitConversionToValue = true)]
public readonly partial struct EmailAddress;
```

```csharp skip
string address = email;   // no .Value
```

Reading stays terse while construction stays explicit. This is the conversion worth turning on for most types:
passing a value object to an API that takes the underlying type is common, and cannot produce an invalid value.

## Constructing with a cast

```csharp
[ValueObject<string>(MaxLength = 254, ExplicitConversionFromValue = true)]
public readonly partial struct EmailAddress;
```

```csharp skip
var email = (EmailAddress)text;   // validates; throws ValueObjectException when rejected
```

The cast runs the same normalization and validation as `Create`, and throws as `Create` does. There is no
implicit conversion from the underlying value: construction that can fail should be visible where it happens.

## Arithmetic on numeric value objects

```csharp
[ValueObject<decimal>(Arithmetic = true)]
public readonly partial struct Amount : IValueObjectNormalizer<decimal>, IValueObjectMinimum<decimal>
{
    public static decimal Minimum => 0m;

    public static decimal NormalizeValue(decimal value) => decimal.Round(value, 2, MidpointRounding.ToEven);
}
```

`Arithmetic = true` adds `+`, `-`, `*` and `/`, unary `-`, `Zero`, `One`, `IsZero`, `Min` and `Max`, and the
`INumericValueObject<TSelf, TValue>` interface for generic code.

**Every result goes back through `Create`.** `Amount.Zero - amount` throws rather than producing a negative
amount the type forbids, and a product is rounded by the normalizer like any other value. Division of one
value object by another returns the bare underlying type: a ratio of two amounts is not an amount.

`Arithmetic` is available on numeric underlying types only; anywhere else it is `VO0007`.
