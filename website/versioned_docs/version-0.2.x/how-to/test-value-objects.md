---
title: Test Your Value Objects
sidebar_label: Testing value objects
slug: /how-to/test-value-objects
description: Derive a dozen behavioural checks for your own value objects from a list of accepted and rejected values, with the xUnit contract kit.
---

# Test your value objects

The generated code is tested in this repository. Your rules are not: a normalizer that is not idempotent, or a
pattern that rejects a value the validator was written to accept, is a bug in your type. The contract kit finds
those from a short list of examples.

```bash
dotnet add package AdCodicem.ValueObjects.Testing
```

The kit is built on xUnit v3.

## Declare a contract

```csharp skip
public sealed class IbanContract : ValueObjectContract<Iban, string>
{
    protected override IEnumerable<string> AcceptedValues =>
        ["FR7630006000011234567890189", "DE89370400440532013000"];

    protected override IEnumerable<string> RejectedValues =>
        ["", "not-an-iban", "FR7630006000011234567890188"];
}
```

Give at least two distinct accepted values, so ordering can be checked, and rejected values that exercise each
rule — here an empty string, a wrong shape, and a wrong check digit.

## What it checks

Each is an xUnit test in your suite:

- every accepted value produces an initialized instance;
- every rejected value is refused by `TryCreate` with a stable code and a message, and `Create` throws with that
  same code;
- normalization settles after one pass, and creating from an already created value changes nothing;
- equality is reflexive and symmetric, and agrees with the hash code;
- ordering agrees with equality;
- text survives a round trip, and formatting into a span matches formatting into a string;
- JSON carries the bare underlying value, and rejects what the type rejects;
- the type is discoverable at run time;
- every accepted value respects the declared length limits.

## Constructing an invalid instance on purpose

A test that needs an uninitialized instance — to check a guard, say — trips `VO0010`, which is a build error.
Disable it on the spot, with a comment saying why:

```csharp skip
#pragma warning disable VO0010 // The guard under test must reject an uninitialized instance.
var missing = default(Iban);
#pragma warning restore VO0010
```

## Identifiers in tests

`New()` on an `[EntityId]` reads an ambient clock and entropy source. `ValueObjectIds.Use(clock, bytes)` pins
both for the current execution flow, so a test gets the same identifier on every run —
[Public identifiers](../tutorials/public-identifiers.md#test-with-them) shows it.
