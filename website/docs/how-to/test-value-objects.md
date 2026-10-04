---
title: Test Your Value Objects
sidebar_label: Testing value objects
slug: /how-to/test-value-objects
description: Derive over a dozen behavioural checks for your own value objects from a list of accepted and rejected values, with the xUnit contract kit, and keep them bare values in Verify snapshots.
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
- every known value is a value of the underlying type that the type accepts. A type that declares none reports the
  check skipped;
- the details of the known values, `Schema.KnownValueDetails`, list them one for one, in the same order, each under
  a name. They hold the names the [OpenAPI document](./openapi.md#names-of-known-values) publishes beside a closed
  set's `enum`, none of which it publishes when they are left out or out of step. A type that declares no known value
  reports the check skipped; one that declares known values and details none of them fails it.

The discoverability and length checks read the registry. They run the generated registration of the type's assembly
first, so they do not depend on another test having used that assembly, which matters for contracts kept in a test
project of their own. The last three read the schema the type declares, `TSelf.Schema`, as generic code does, so they
check a [value object written by hand](./runtime-lookup.md#value-objects-written-by-hand) whether anything registered
it or not: the generator always writes the details in step, and a schema written by hand is where they drift apart.

The generator already refuses an example or a known value its rules refuse wherever it can evaluate them, with
[`VO0031`](../reference/diagnostics.md). The example and known value checks cover what only runs at run time: a pattern, a validator, a
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

## Snapshot tests with Verify

Verify writes a value object as its bare value already, through its `TypeConverter`. Under `UseStrictJson`, though,
numbers and booleans become strings, and a `DateOnly` value object escapes date scrubbing. A converter of a few lines
for Argon, the JSON library Verify uses, fixes both:

```csharp skip
public sealed class ValueObjectArgonConverter : Argon.JsonConverter
{
    public override bool CanConvert(Type type) => typeof(IValueObject).IsAssignableFrom(type) && type.IsValueType;

    public override void WriteJson(Argon.JsonWriter writer, object value, Argon.JsonSerializer serializer)
        => serializer.Serialize(writer, ((IValueObject)value).GetBoxedValue());

    public override object ReadJson(Argon.JsonReader reader, Type type, object? existing, Argon.JsonSerializer serializer)
        => throw new NotSupportedException();
}

public static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Initialize()
        => VerifierSettings.AddExtraSettings(settings => settings.Converters.Add(new ValueObjectArgonConverter()));
}
```

The probe registered it on one `VerifySettings` instance, `settings.AddExtraSettings(…)`, on Verify.XunitV3 30.15.0;
the global form above is the same call. Every Verify version released after 1 September 2026 requires a SponsorCheck
licence property: Verify 33.2.0 fails the build with `SC021` without one.

## Identifiers in tests

`New()` on an `[EntityId]` reads an ambient clock and entropy source. `ValueObjectIds.Use(clock, bytes)` pins
both for the current execution flow, so a test gets the same identifier on every run —
[Public identifiers](../tutorials/public-identifiers.md#test-with-them) shows it.
