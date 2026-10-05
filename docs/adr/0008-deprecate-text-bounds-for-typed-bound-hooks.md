# 8. Deprecate text bounds for typed bound hooks

Date: 2026-10-02

## Status

Accepted. Amended by [ADR-0011](0011-declare-known-values-and-examples-as-typed-members.md): the `Minimum` and
`Maximum` options are compile errors from 0.3.0 on, the first stable release to carry the hooks, rather than
warnings until the next major version.

## Context

`[ValueObject<T>]` takes `Minimum` and `Maximum` options, the inclusive bounds of a value object over a number, a
`char`, a date, a time or a duration. They are declared once: they validate, and the generator also emits them as
the `minimum` and `maximum` of the OpenAPI schema, or as `x-minimum`, `x-maximum` and a sentence of the
description for a type JSON writes as a string.

An attribute argument can only be a constant of a few types, and neither a `decimal` nor a date is one, so the
options are `string?`. The generator reads each underlying type in one form and no other — digits for an integer,
no exponent on a `decimal`, `yyyy-MM-dd` for a `DateOnly`, an offset always on a `DateTimeOffset` and never on a
`DateTime` — and reports anything else as `VO0004`. That grammar is the generator's own. The compiler knows
nothing of it, an IDE completes nothing in it, and a mistake surfaces as a diagnostic of this library rather than
as a type error. A bound computed from anything, `DateOnly.MinValue.AddYears(1899)` or a constant declared
elsewhere, is out of reach.

A static abstract interface member has neither limit. `static abstract TValue Minimum { get; }` is a value of the
underlying type, which the compiler checks and any expression of that type can build. It is reachable only through
a type parameter, which the generated code already uses for the pattern hook
([ADR-0007](0007-deprecate-pattern-for-a-source-generated-regex-hook.md)).

What the text form gave, the hook has to keep: the bound in the error message, the bound in the schema, and a check
that costs no more than comparing against a literal.

## Decision

We will add two hooks, `IValueObjectMinimum<TValue>` and `IValueObjectMaximum<TValue>`:

```csharp
[ValueObject<DateOnly>]
public readonly partial struct BirthDate : IValueObjectMinimum<DateOnly>, IValueObjectMaximum<DateOnly>
{
    public static DateOnly Minimum => new(1900, 1, 1);

    public static DateOnly Maximum => new(2100, 12, 31);
}
```

and we will deprecate the `Minimum` and `Maximum` options in a minor release, then remove them at the next major
version.

- **A bound is a constant, read where it is checked.** The generated code reads it through
  `ValueObjectBound.Minimum<TSelf, TValue>()`, which returns `TSelf.Minimum` and which the JIT inlines, so an
  expression-bodied constant, `=> new(1900, 1, 1)`, is folded into the comparison and costs what a literal did. The
  schema reads it once, when the type initializes, which the generated registration does as the assembly loads, so a
  bound must neither throw nor depend on the state of the application. A bound relative to the clock, "not in the
  future", is a rule rather than a bound, and belongs in `IValueObjectValidator<T>` with a `TimeProvider` a test can
  fix.
- **The rule keeps its place and its error code.** The bounds run after the pattern and before the known values and
  `ValidateValue`, and reject as `value_object.out_of_range`. The message quotes the bound in the invariant form
  `ValueObjectBound.Text` writes, which is the option's text for an integer, a `decimal`, a `char`, a `DateOnly` or
  a `TimeSpan`, and the round-trip form for a real (`1E-05`), a time (`06:00:00.0000000`) or a date and time, which
  a migration changes. A `DateTime` bound is quoted without its kind, since the check compares clock readings. A
  `double` or a `float` bound still rejects `NaN`.
- **The rule is still declared once.** The generated schema holds the bound as `ValueObjectBound.Text`, the
  invariant round-trip text of the value, which the schema transformer writes as the number, or, for a type written
  as a string, re-writes through the type's own converter, as it already did for the text form.
- **The options are marked `[Obsolete]` with the diagnostic id `VO0028`**, so the compiler reports them as a
  warning wherever they are set, the message naming the hook.
- **The generator guards the hooks.** Declaring an option and its hook is `VO0029`, an error, and the hook wins. A
  hook over a type that takes no bound — `string`, `Guid`, `bool`, an `[EntityId]` — or over another type than the
  underlying one, which the compiler accepts and nothing would check, is `VO0030`, an error, and the type generates
  without it. `VO0011` reports a public static `Minimum` or `Maximum` property of the underlying type written
  without its interface, except on a type that implements `IValueObjectValidator<T>` or `IValueObjectNormalizer<T>`,
  which may already check it or clamp to it, or that still sets the option, which `VO0028` reports. A field is left
  alone: it could not implement the hook, and a constant used in an attribute would have to stop being one.

### Rejected

- **Keep the bound once read, in a `static readonly` field of a static generic class.** It was the first design,
  meant to make even a property that computes its bound cost nothing, and it is wrong: a value object whose static
  fields create an instance, `public static readonly Percentage Full = Create(100);`, validates while its own type
  initializes, the runtime lets that re-entrant read see the bound's backing field before it is assigned, and the
  field kept the default of the type for the life of the process, switching the check off or refusing everything. A
  bound read where it is checked is wrong only for the instances created before it is assigned, which the
  documentation warns about and an expression-bodied bound avoids altogether.
- **Allow a bound that changes.** A property may allocate, read configuration or build a date every time it runs,
  and validation is on every hot path. The rule would also stop being stable: two calls could disagree on what the
  type accepts, and the schema would describe one of them. A bound is a constant by contract, which the
  documentation states and nothing enforces.
- **Typed attribute arguments, `Minimum = 1`, through overloads per type.** An attribute property has one type, and
  the types that need the text form most — `decimal`, the dates, `TimeSpan` — cannot be attribute arguments at
  all.
- **Keep the options alongside the hooks indefinitely.** Two ways to declare one rule, one of them checked by the
  compiler and one by a grammar of this library, and the generator, the schema and the documentation carrying both.
- **Remove the options in the same release.** Every consumer setting them would fail to compile on a minor upgrade.

## Consequences

- A bound is checked by the compiler, completed by the IDE, and can be any expression of the underlying type.
- The declaration is longer: an interface on the type and a property, where `Minimum = "0"` was one argument.
- **A consumer with `TreatWarningsAsErrors` breaks on upgrade.** `VO0028` is a warning, so such a build fails until
  the options are migrated or `VO0028` is suppressed. That is the intended pressure, but it arrives in a minor
  version, and the release notes have to say so.
- `VO0028` is the `DiagnosticId` of an `[Obsolete]`, which the compiler reports. No descriptor declares it, so it
  appears in no analyzer release file and is documented by hand, as `VO0021` is.
- A bound is fixed for the life of the process. A test that needs another bound needs another type.
- An initialized bound, `{ get; } = ...`, is assigned with the type's other static fields, in declaration order, so
  an instance a static field creates before it is checked against the default of the type. The documentation shows
  the expression-bodied form only.
- Until the next major version, the generator carries both paths, and the text form, with `VO0004` and its
  grammar, stays tested.
