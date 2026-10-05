# 11. Declare known values and examples as typed members

Date: 2026-10-05

## Status

Accepted. Amends [ADR-0007](0007-deprecate-pattern-for-a-source-generated-regex-hook.md) and
[ADR-0008](0008-deprecate-text-bounds-for-typed-bound-hooks.md): `Pattern`, `Minimum` and `Maximum` are compile
errors from 0.3.0 on, rather than warnings until the next major version.

## Context

Several arguments of this library's attributes are read under rules of its own rather than by the compiler:

- **`[KnownValue(name, value)]` on the type.** The name is an identifier the generator creates, so the generator
  reproduces the compiler's rules for it: a valid identifier, not a keyword, no collision with a member of the type,
  with a type parameter, with the getter name `get_` followed by it, or with another known value (`VO0006`). The
  value is an `object`. A `Guid`, a `decimal`, a `DateOnly`, a `TimeOnly`, a `DateTime`, a `DateTimeOffset` or a
  `TimeSpan` cannot be an attribute argument, so it is written as text, in the one form of its type that the
  generator reads (`VO0013`).
- **`Example`, on `[ValueObject<T>]` and `[EntityId]`.** It is text. The generator parses it at compile time to check
  it against the type's rules (`VO0031`), and both schema transformers parse it again at run time to write it as the
  type writes it in JSON.
- **`Pattern`, `Minimum` and `Maximum`.** ADR-0007 and ADR-0008 replaced them with hooks, and deprecated them as
  warnings until the next major version. They are still in the generator, along with `VO0004` and the grammar of
  bounds written as text.

An attribute argument is a constant of a few types, and an attribute property has one type. Neither limit applies to
a member the author writes: a static field or property of the value object has a name the compiler checks, a type
the compiler checks, and an initializer that any expression of that type can supply. An IDE completes, renames and
finds references to it.

Three facts constrain the change:

1. **Nothing deprecated has shipped in a stable release yet.** v0.2.1 has `Pattern`, `Minimum` and `Maximum`
   without `[Obsolete]`, and none of the hooks. The deprecations exist only on `main` and in the previews, so 0.3.0
   is the first stable release to carry the hooks, whatever happens to the options.
2. **While the major is 0, a minor may break the public API** (README, Versioning).
3. **The author's part of a value object initializes before the generated part.** The language leaves the order of
   static field initializers across partial declarations unspecified. Roslyn follows the order of the syntax
   trees, and a generator's trees come after the source files. That is why a closed set cannot build a known value
   with `Create`: the membership lookup would be built from those very values, after them. It also explains a
   failure that already exists. `public static readonly CountryCode Default = Create("FR");` on a closed value
   object makes `Validate` read the lookup before the generated part has assigned it. It throws a
   `NullReferenceException` from the type initializer, and the module's registration turns that into an
   application that does not start.

## Decision

We will declare a known value as a static member the author writes, mark it `[KnownValue]` and initialize it
through a generated factory, `Known`. We will declare the example through a hook, `IValueObjectExample<TSelf>`. Every
attribute argument read as text will become a compile error in 0.3.0.

```csharp skip
[ValueObject<string>(ValueSet = ValueSetKind.Closed, MinLength = 2, MaxLength = 2)]
public readonly partial struct CountryCode : IValueObjectNormalizer<string>, IValueObjectExample<CountryCode>
{
    /// <summary>Metropolitan France.</summary>
    [KnownValue]
    public static readonly CountryCode France = Known("FR");

    [KnownValue(Description = "Belgium")]
    public static CountryCode Belgium { get; } = Known("BE");

    public static CountryCode Example => France;

    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}

[ValueObject<DateOnly>]
public readonly partial struct ReportingDate
{
    [KnownValue]
    public static readonly ReportingDate Epoch = Known(new DateOnly(1970, 1, 1));
}
```

- **A known value is a member.**
  - `[KnownValue]` goes on a `static readonly` field or on a static get-only auto-property, of any accessibility.
    Its type is the value object and its initializer is `Known(...)`.
  - Its name is the member's, which the compiler checks. Its value is an expression of the underlying type, which
    the compiler types.
  - `[KnownValue]` keeps `Description`. Without it, the `<summary>` of the member describes the value, as the type's
    `<summary>` describes the type when `[ValueObject<T>]` sets no `Description`.
  - A member that is not static, that can be written, that is of another type, or that is not initialized through
    `Known` is `VO0036`, an error, and generates nothing.
  - On an `[EntityId]`, `VO0027` still reports it. A closed set with no known value is still `VO0005`.
- **`Known` builds a known value.**
  - Every value object but an `[EntityId]` gets a `private static TSelf Known(TValue value)`.
  - `Known` normalizes the value and applies every rule but membership, since the value is a member by declaration.
    A value the rules refuse throws `ValueObjectException` from the type initializer, as a refused known value does
    today.
  - Called anywhere but in the initializer of a `[KnownValue]` member, `Known` would build an instance that a closed
    set refuses. That call is `VO0037`, an error.
- **The generated members read the declared ones.**
  - The membership lookup, `KnownValues`, `Schema.KnownValues` and `Schema.KnownValueDetails` are static fields of
    the generated part, initialized from the members after the author's part has initialized them.
  - `KnownValues` and the OpenAPI `enum` list the members in the order the compiler lists them: declaration order
    within a file, then the order of the files for a type split across partial declarations.
  - A closed set's `Validate`, called before the lookup exists, now throws an `InvalidOperationException` that says
    so. Such a call comes from another static initializer of the type calling `Create`, which today fails with a
    `NullReferenceException`.
- **A known value is still checked at compile time where the compiler evaluates it.**
  - When the argument of `Known` is a constant (a string, an integer, a `char`, a `bool`, a real or a `decimal`),
    the generator holds it to the rules it can evaluate, as `VO0031` does today.
  - A `Guid`, a date, a time or a duration is built by an expression the compiler does not evaluate. A refused one
    fails the type initializer at start, and the contract kit names the rule it breaks.
- **The example is a hook.**
  - `IValueObjectExample<TSelf>` declares `static abstract TSelf Example { get; }`. The example is an instance, so
    it has been validated, and it can be a known value. An explicit implementation keeps it off the public surface
    of the type.
  - The schema reads the example once, when the type initializes.
  - `ValueObjectSchema.Example` becomes an `object?` holding the underlying value, as `KnownValues` does. The
    transformers write it through the type's converter, as they write a known value, instead of parsing text. A
    string set by a schema written by hand is still read as text.
  - An `[EntityId]` without the hook keeps the example derived from its profile.
  - The hook over another type than the value object is `VO0038`, an error, and the type generates without it.
  - When the getter returns a known value, or `Create` over a constant, the generator checks the example as
    `VO0031` does.
- **The text forms are compile errors in 0.3.0.**
  - The two-argument constructor of `[KnownValue]` becomes `[Obsolete(error: true)]` with the diagnostic id
    `VO0034`, and its message names the member form. A code fix rewrites the attribute into a member, and turns a
    value written as text into an expression of its type: `"1970-01-01"` becomes `new DateOnly(1970, 1, 1)`.
  - `Example` on `[ValueObject<T>]` and on `[EntityId]` becomes `[Obsolete(error: true)]` with `VO0035`, and its
    message names the hook.
  - `Pattern`, `Minimum` and `Maximum` keep `VO0021` and `VO0028`, now as errors.
  - The generator ignores what these members set: nothing it would generate from them survives the compiler. Any
    minor version may remove them before 1.0.0.
- **Six diagnostics retire.**
  - `VO0004` (a bound written as text), `VO0006` (a known value's name), `VO0013` (a known value written as text),
    `VO0014` (the `Pattern` option's expression), `VO0022` (a pattern declared twice) and `VO0029` (a bound
    declared twice).
  - What each one reported can no longer compile. Their ids are not reused.

### Rejected

- **`Create` as the initializer.** It would mean one factory fewer to learn. But in a closed set, `Create` would
  have to skip membership while the type initializes, and so would any other static initializer of the type that
  calls it, silently. An analyzer sees a direct call, not one made through a helper.
- **A partial property the generator implements,
  `[KnownValue("FR")] public static partial CountryCode France { get; }`.**
  The compiler would own the name, and the generator the order of initialization. But the value would stay an
  attribute argument, so it would remain text for seven of the twenty-two underlying types.
- **A generic attribute, `[KnownValue<T>(name, value)]`.** An argument of type `T` must still be a constant, and a
  `decimal`, a `Guid`, a date, a time or a duration cannot be one.
- **A hook listing the values, `static abstract ImmutableArray<TSelf> KnownValues { get; }`.** It types the values
  but names none, so the names the OpenAPI document publishes and `CountryCode.France` are lost.
- **A generated static constructor that builds the lookup.** The language guarantees that it runs after every field
  initializer of every part, whatever the order between the parts. But the author could no longer write a static
  constructor, and the type would lose `beforefieldinit`. A unit test pins the compiler's order instead.
- **The example as a `TValue`, as the bounds are.** It would create nothing as the type initializes. But nothing
  would validate it before the contract kit runs, whereas an instance is validated by construction.
- **The first known value as the default example.** It would change the published document of every type that
  declares known values and no example.
- **Warnings for a minor, as ADR-0007 and ADR-0008 planned.** The warnings never reached a stable release. A stable
  consumer would meet the hooks and the deprecations in the same release anyway, and the generator would carry the
  text grammar for one release more. An error whose message names the replacement guides as well.
- **Removing the members outright.** The compiler would report a missing constructor or property, `CS1729` or
  `CS0117`, and say nothing of the replacement.

## Consequences

- The generator reads no value as text. The grammar of one form per type goes, with `VO0004`, `VO0006`, `VO0013`,
  their tests, and the authoring guide's section on bounds and known values written as text. The code fix keeps
  reading the text forms until the two-argument constructor is removed.
- The compiler owns the names. A keyword, or a collision with a member, a type parameter or a getter, is a compiler
  error, and renaming a known value and finding its references work as for any member.
- The declaration is longer: a member per value instead of one attribute line. It brings documentation, IntelliSense
  and navigation.
- A known value or an example over a `Guid`, a date, a time or a duration is no longer checked at compile time. A
  refused one fails at start, and the contract kit names the rule.
- **Every consumer breaks on the upgrade from 0.2.x.** Each `[KnownValue(name, value)]`, `Example`, `Pattern`,
  `Minimum` and `Maximum` is a compile error. The release notes and the migration guide have to say so. Only the
  known values have a code fix; the other messages name the replacement.
- `ValueObjectSchema.Example` changes type. Code that reads it has to change. A schema written by hand that sets a
  string still compiles.
- A static initializer of a closed value object that creates an instance through `Create` still fails at start, now
  with a message that names the cause.
- The generated code depends on the compiler initializing the generated part last, which a unit test pins.
- `VO0034` and `VO0035` are the `DiagnosticId` of an `[Obsolete]`, which the compiler reports. No descriptor declares
  them, so they are documented by hand, as `VO0021` and `VO0028` are. `VO0036`, `VO0037` and `VO0038` are
  descriptors and are listed in the analyzer release file.
