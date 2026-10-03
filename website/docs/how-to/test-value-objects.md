---
title: Test Your Value Objects
sidebar_label: Testing value objects
slug: /how-to/test-value-objects
description: Derive over a dozen behavioural checks for your own value objects from a list of accepted and rejected values, with the xUnit contract kit.
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
- every accepted value respects the declared length limits. A type that did not register itself has no declared
  limits to read, and this check reports itself skipped;
- the declared `Example` is a value the type accepts, parsed in the invariant culture as the OpenAPI document reads
  it, and reported with the code and the message of the rule it breaks. A type that declares none reports the check
  skipped;
- every known value is a value the type accepts. A type that declares none reports the check skipped.

The last four read the registry. They run the generated registration of the type's assembly first, so they do not
depend on another test having used that assembly, which matters for contracts kept in a test project of their own.

The generator already refuses an example or a known value its rules refuse wherever it can evaluate them, with
[`VO0031`](../reference/diagnostics.md). The two checks cover what only runs at run time: a pattern, a validator, a
bound computed by its hook, a normalization, the format of an `[EntityId]`, and an example written in another form
than the one a known value takes. A refused known value throws from the type initializer. On a construction of a
generic value object, which initializes when first used, the check names the rule and the value object behind the
`TypeInitializationException`. Any other value object is initialized by the registration of its assembly, as soon as
anything of the assembly is used: every check of every type of the assembly then fails before it starts, and the
innermost exception of the `TypeInitializationException` the runner reports names the type and the rule.

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
