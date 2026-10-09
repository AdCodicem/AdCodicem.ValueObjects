---
title: Test Your Value Objects
sidebar_label: Testing value objects
slug: /how-to/test-value-objects
description: Derive over a dozen behavioural checks for your own value objects from a list of accepted and rejected values with the xUnit contract kit, draw test data each type accepts from the rules it declares, for AutoFixture, Bogus and FsCheck too, and keep value objects bare values in Verify snapshots.
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
- on request, every value the declared schema rules out is refused the same way, and by the JSON converter
  ([below](#values-the-schema-rules-out)). A contract that does not ask reports the check skipped;
- normalization settles after one pass, and creating from an already created value changes nothing;
- equality is reflexive and symmetric, and agrees with the hash code;
- ordering agrees with equality;
- text survives a round trip, and formatting into a span matches formatting into a string;
- JSON carries the bare underlying value, and rejects what the type rejects;
- the type is discoverable at run time;
- every accepted value respects the declared length limits. A type that did not register itself has no declared
  limits to read, and this check reports itself skipped;
- the example the schema publishes, which `IValueObjectExample<TSelf>` declares, is a value the type accepts, created
  again from its underlying value and reported with the code and the message of the rule it breaks; a schema written
  by hand that holds the example as text has it parsed in the invariant culture, as the OpenAPI document reads it. A
  type that declares none reports the check skipped;
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

## Values the schema rules out

The rejected values you list are the ones you thought of. The schema the type declares rules out more, and a contract
can ask the kit to derive them, through `AdCodicem.ValueObjects.Testing.Data`, which the kit depends on:

```csharp skip
public sealed class QuantityContract : ValueObjectContract<Quantity, short>
{
    protected override IEnumerable<short> AcceptedValues => [0, 1000];
    protected override IEnumerable<short> RejectedValues => [];
    protected override bool DerivesRejectedValues => true;
}
```

The check, `Every_value_the_declared_schema_rules_out_is_rejected`, then asserts that `TryCreate` refuses each of
these with a code and a message, that `Create` throws with the same code, and that the JSON converter refuses it:

- a string one character shorter than its `MinLength`, and the empty string beside a `MinLength` of 2 or more;
- a string one character longer than its `MaxLength`;
- a value one step below its `Minimum` and one above its `Maximum`: one unit of the bound's last written digit for a
  `decimal` (0.00 below 0.01, 101 above 100), one unit in the last place for a `double` or a `float`, one tick for a
  date, a time or a duration, one day for a `DateOnly`, never past the type's own extremes;
- a value outside its closed value set, drawn from the rest of its schema and compared with its known values once
  normalized, ignoring case for a string.

A string of the wrong length is otherwise drawn as the pattern asks, so its length alone breaks a rule, and drawn again
while the type's normalizer brings it back to a right length, as trimming does a space at an end. The empty string is
not derived from a type without a `MinLength`, since the schema does not say whether such a type accepts it. The values
are drawn with a fixed seed, so a failure replays. A type whose schema rules nothing out reports the check skipped,
saying whether it declares no rule or only rules no value can be derived to break, a bound at an extreme of its type or
a closed set that holds every value.

The check is off by default, because turning it on adds a test to a contract that passed, and some types fail it on
purpose: a normalizer that clamps a value into the range, or cuts a string to its length, rather than a validator
refusing it, accepts the value one step past the bound, and the check fails naming the rule the type does not enforce.
For a dial from 0 to 10 that clamps:

```text
'-1' breaks Minimum (0) declared on 'Dial' but is accepted.
```

## Generate valid values

A test that needs an order holding an IBAN needs an IBAN its rules accept, and every generator written by hand restates
rules the type already declares. `AdCodicem.ValueObjects.Testing.Data` draws values each type accepts from the rules it
declares, for any test framework, with no dependency beyond the contracts:

```bash
dotnet add package AdCodicem.ValueObjects.Testing.Data
```

```csharp skip
var sampler = new ValueObjectSampler(new Random(42));

var quantity = sampler.Next<Quantity, short>();
var reference = sampler.Next(descriptor); // a type known only at run time, closed through its descriptor
if (!sampler.TryNext<EvenCode, string>(out var code, out var refusal)) { /* refusal.ErrorCode */ }
```

### How a value is drawn

A candidate comes from `TSelf.Schema`, then goes through `TSelf.TryCreate`, so the normalizer and every validator
apply and nothing the type refuses comes out:

- a closed value set gives one of its known values. An open one gives a known value half of the time
  (`KnownValueShare`), so a test meets the common values and the long tail alike;
- a string gets a length between `MinLength` and `MaxLength`, never empty unless its maximum is zero. Without a
  `MaxLength`, it gets at most `MaxExtraLength` characters, 32 by default, beyond the shortest it can be: its
  `MinLength`, the shortest string its pattern matches, or one character when it declares neither. Without a pattern,
  it is drawn from ASCII letters and digits;
- a number, a date, a time or a duration gets a value between `Minimum` and `Maximum`, or across the type's whole range
  on a side left open: an unbounded `double` draws values such as `-1.5e308`. A `char` keeps to printable ASCII on an
  open side, unless the bound declared on the other side lies beyond it, where the open side runs to the type's own
  extreme. A `DateTime` is drawn in UTC, a `DateTimeOffset` with an offset of zero;
- a `Guid` and a `bool` are drawn as they are;
- an underlying type the sampler does not know, a `Uri` for one, draws only from its known values, its example and a
  generator registered for it.

### Patterns

The sampler draws from the pattern the type declares, read by the same reader that writes it for XSD and MongoDB. It
covers literals and escapes, character classes with their ranges, negations and subtractions, `.`, `\d`, `\w` and `\s`
(drawn from ASCII) and their negations, every quantifier, alternation, capturing, non-capturing and named groups, and
anchors. A negated class and `.` draw from printable ASCII. `\b` and `\B` draw nothing, and the pattern itself, which
the type checks, keeps or refuses the candidate. An open quantifier (`*`, `+`, `{n,}`) adds at most `MaxExtraLength`
repetitions to its minimum, unless the declared lengths ask for more.

A pattern no anchor holds at an end, `@acme\.com$` or an unanchored `[0-9]`, matches inside a longer string: when its
matches are shorter than the declared lengths ask, a match is padded at that end with ASCII letters and digits.

A pattern outside that subset, a lookaround, a backreference or a `\p{…}` category, falls back to rejection
sampling: strings of the declared lengths, each checked against the pattern before the type sees it. They are drawn
from the pattern read loosely, without its lookarounds and with each category drawn from the printable ASCII characters
it holds, so `^(?=.{1,64}@)[^@\s]+@…` and `^\p{Lu}{2}-\d{3}$` are drawn alike; a pattern even that reading leaves out,
a backreference for one, from ASCII letters and digits, which rarely match it. `PatternSampler`, in the options, is
tried before the built-in sampler, and returns `null` for a pattern it leaves to it.

### Rules no schema carries

A validator or a normalizer is reached only through `TryCreate`: it can be checked, not drawn. A checksum makes most
candidates fail. After `MaxAttempts` of them, 100 by default, the sampler falls back on the example the type declares,
and failing that throws a `ValueObjectSamplingException` that names the type, the attempts, the code of the rule that
refused the last candidate, and the registration to add:

```text
Could not draw a value of 'EvenCode' its rules accept: the last of 100 candidates drawn from its schema was refused
(checksum), and it declares no example its rules accept. Register a generator of its underlying value:
options.Use<EvenCode, string>(random => ...).
```

Once a generator is registered, the message names it instead when the type refuses every value it gives: `the last of
100 candidates its registered generator gave was refused (checksum)`, then `Make the generator registered with
options.Use<EvenCode, string>(random => ...) give values its rules accept.`

The fallback hides a checksum quietly: an IBAN checked by MOD-97 comes out as its example about a third of the time,
an `[EntityId]`, whose last character is a check character, about one time in twenty-five. Register a generator of
the underlying value, in test code, so the value object itself carries nothing for tests; its values still go through
the type's rules. For `EvenCode`, whose validator wants the digits after `EVEN-` to add up to an even number:

```csharp skip
var options = new ValueObjectSamplerOptions()
    .Use<EvenCode, string>(random =>
    {
        var digits = random.Next(1000).ToString("D3", CultureInfo.InvariantCulture);
        return "EVEN-" + digits + digits.Sum(digit => digit - '0') % 2; // a last digit that makes the sum even
    })
    .Use<AccountId, string>(_ => AccountId.New().Value); // its own clock and entropy, not the sampler's seed

var sampler = new ValueObjectSampler(Random.Shared, options);
```

### Edges, and shrinking

A property over a wide type lands on its edges by chance once in a blue moon. `Boundaries<TSelf, TValue>()` gives the
accepted values at the edges the schema declares, its `Minimum`, its `Maximum`, a string of its `MinLength` and of its
`MaxLength`, and each known value; `RejectedValues<TSelf, TValue>()` gives the values just past them, each with the rule
it breaks, as [the contract kit](#values-the-schema-rules-out) uses them. Mix both into a generator, as this
repository's own property tests do for a quantity bounded 0..1000: `{0, 1000}` and `{-1, 1001}`.

`ValueObjectSampler.Shrink<TSelf, TValue>(value, shrinkUnderlying)` proposes simpler values for a property-based
testing library to shrink a counterexample to: the type's `Minimum`, its first known value and its example, ranked in
that order, then the shrinks of the underlying value the type accepts as they are, those its normalizer would change
left out: upper-cased, the `b` a shrinker proposes for `C` is a `B`, which shrinks back to `C`. From one of the declared
values it proposes only those ranked before it, so every chain of shrinks ends where the underlying shrinker's do.

### Seeds and threads

A sampler is as thread-safe as the `Random` it draws from: `Random.Shared` is, a `Random` created with a seed is not,
and replays the same values given the same calls. Options are read, never written, while a sampler draws.

### CsCheck

CsCheck composes generators by hand; a one-line `Gen` over the sampler, seeded by CsCheck so that a failure replays,
gives it value objects:

```csharp skip
var ibans = Gen.Int.Select(seed => new ValueObjectSampler(new Random(seed), options).Next<Iban, string>());
```

## AutoFixture, Bogus and FsCheck

Left to themselves, the three libraries cannot build a value object its rules accept. AutoFixture calls `Create` with
values of its own: strings built on a `Guid`, longer than a declared `MaxLength`, numbers walked from 1 whatever the
bounds, and always the first known value of a closed set. Bogus leaves a value-object member without a rule at its
default, the instance no rule accepted. FsCheck refuses the type outright, and every type holding one. One package per
library hands each the values the sampler draws, with the same options:

```bash
dotnet add package AdCodicem.ValueObjects.AutoFixture
dotnet add package AdCodicem.ValueObjects.Bogus
dotnet add package AdCodicem.ValueObjects.FsCheck
```

The AutoFixture and Bogus packages find a value object through `ValueObjectRegistry.TryResolve`, closed through its
descriptor: a construction of a generic value object and a value object written by hand are described the first time
they are asked for, and stay registered for the process. The FsCheck package merges the value objects of an assembly the
registry holds, and leaves the others to a merge of their own. A `ValueObjectSamplingException` names the registration
to add in the library's own terms.

### AutoFixture

```csharp skip
var fixture = new Fixture().Customize(new ValueObjectCustomization());

var order = fixture.Create<Order>();   // every value object it holds is one its rules accept
var iban = fixture.Create<Iban>();
```

The customization answers the request for any value object, its nullable form, and each one inside an object, a list or
an array the fixture builds. A registration of the fixture wins, whether it comes before or after the customization, and
so does a specimen builder added to `fixture.Customizations` before it:
`fixture.Register(() => EvenCode.Create(...))` meets a checksum in AutoFixture's terms, as
`ValueObjectSamplerOptions.Use` does in the sampler's, which the customization takes too, with the `Random` to draw
from:

```csharp skip
fixture.Customize(new ValueObjectCustomization(options, new Random(42)));
```

A seeded `Random` replays the same values for the same requests made from one thread. There is no
`[AutoValueObjectData]` attribute for AutoFixture.Xunit3: the customization is one line in a data attribute of your own.

### Bogus

```csharp skip
var orders = new Faker<Order>()
    .StrictMode(true)
    .RuleForValueObjects()
    .RuleFor(o => o.Note, f => f.Lorem.Sentence());

var quantity = new Faker().ValueObject<Quantity>();
var iban = new Faker().ValueObject<Iban, string>(f => f.Finance.Iban()); // still checked through TryCreate
```

`RuleForValueObjects` gives a rule to every member of `T` whose type is a value object, its nullable form (a value,
never `null`), an array or a `List<T>` of it (one to three values), through Bogus's `RuleForType`, so `StrictMode(true)`
is met. Call it first: Bogus keeps the last rule of a member, so a rule written after it wins, and one written before
is replaced. An array or a list of a nullable value object is left to your rules.

Each value object takes one number from the faker's `Randomizer`, which seeds the sampler drawing it: `UseSeed`
replays the same objects. `new Faker<Iban>()` does not compile, since `Faker<T>` wants a class (CS0452): a value object
on its own comes from `faker.ValueObject<TSelf>()`, and from a semantic generator of Bogus's with
`faker.ValueObject<TSelf, TValue>(generator)`, whose candidates are kept only once the type accepts them, the example it
declares coming out when none is.

### FsCheck

```csharp skip
var arbitraries = ArbMap.Default
    .MergeValueObjects(typeof(Iban).Assembly, options)
    .MergeValueObject<Reference<PurchaseOrder>, string>();

Prop.ForAll(arbitraries.ArbFor<Order>(), order => /* ... */).Check(config);
Prop.ForAll(ValueObjectArbitrary.For<Quantity, short>(), quantity => /* ... */).Check(config);
```

The package depends on the bare FsCheck package, and a property runs from a plain `[Fact]` through
`Check(Config.QuickThrowOnFailure)`, or from the `[Property]` of FsCheck.Xunit.v3, the package for xUnit v3
(FsCheck.Xunit is for xUnit 2). `[Property]` takes no map: its `Arbitrary` option names a class of yours whose static
members return the arbitraries, `ValueObjectArbitrary.For<TSelf, TValue>()` each, from which FsCheck derives the types
holding them.

```csharp skip
public static class ValueObjectArbitraries
{
    public static Arbitrary<Quantity> Quantity() => ValueObjectArbitrary.For<Quantity, short>();
}

[Property(Arbitrary = [typeof(ValueObjectArbitraries)])]
public bool A_line_holds_a_quantity_in_range(OrderLine line) => line.Quantity.Value is >= 0 and <= 1000;
```

`MergeValueObjects` merges one arbitrary per value object the assembly's generated registration lists, and per value
object of that assembly the registry holds by then, and so derives every type holding them; the other types stay
FsCheck's. A construction of a generic value object, even one the registry holds, and a value object written by hand
that nothing registered, are left out: merge each with `MergeValueObject<TSelf, TValue>()`, or FsCheck refuses the type
holding it, "not handled automatically", naming it.

Each arbitrary draws its seed from FsCheck, so a failing run replays from the seed FsCheck reports. One value in four is
one of the type's [boundaries](#edges-and-shrinking). A counterexample shrinks through `ValueObjectSampler.Shrink`:
towards the type's `Minimum`, its first known value and its example, then through FsCheck's own shrinker of the
underlying value, keeping only what the type accepts as it is. FsCheck has a shrinker for the integers up to 64 bits,
`float`, `double`, `decimal`, `char`, `string`, `DateTime`, `DateTimeOffset` and `TimeSpan`, and none of its own for
`bool`, `Guid`, the 128-bit integers, `DateOnly` or `TimeOnly`: a value object over one of those shrinks to its declared
values alone. Once a shrink reaches a declared value, it goes on only to the declared values ranked before it: a
property failing from 100 on, over an amount whose example is `1250.00`, shrinks to that example rather than to 100,
while one failing from 50 on, over a quantity bounded 0..1000 with no example, shrinks to 50. FsCheck stops any run at
5,000 shrinks. Nothing of a value object is read until its arbitrary draws, so merging an assembly runs no type
initializer.

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
