# 7. Deprecate Pattern for a source-generated regex hook

Date: 2026-10-02

## Status

Accepted. Amended by [ADR-0011](0011-declare-known-values-and-examples-as-typed-members.md): the `Pattern`
option is a compile error from 0.3.0 on, the first stable release to carry the hook, rather than a warning until
the next major version.

## Context

`[ValueObject<T>]` takes a `Pattern` option, a regular expression the normalized value of a string value object
must match. It is declared once: it validates, and the generator also emits it as the `pattern` keyword of the
OpenAPI schema.

The generator cannot compile that expression at build time. The natural tool is `[GeneratedRegex]`, but source
generators never observe each other's output, so the regex source generator does not see an attribute this
generator writes. [ADR-0002](0002-readonly-struct-generated-by-a-source-generator.md) recorded that cost: the
option compiles a static `Regex` at run time, with `RegexOptions.CultureInvariant`, a one-second match timeout
and `RegexOptions.Compiled`.

`RegexOptions.Compiled` emits IL at run time. Native AOT cannot, so it ignores the option and interprets the
expression. A consumer publishing with native AOT pays for that on every value validated, and has no way to
choose otherwise.

`PatternBenchmarks` measured the option against the same expression as a `[GeneratedRegex]` the consumer writes
(one run each, recorded in `benchmarks/README.md` under "Checking a pattern"):

| Measured | `Pattern` option | `[GeneratedRegex]` |
| --- | --- | --- |
| IBAN, validated through `TryCreate` under native AOT | 290.5 ns | 213.3 ns |
| Five-digit postal code, validated through `TryCreate` under native AOT | 72.4 ns | 37.2 ns |
| The regex engine alone, IBAN shape, under native AOT | 123.6 ns | 62.0 ns |

Under the JIT, where `RegexOptions.Compiled` does apply, the source-generated expression gains only 7 to 9 ns. A
shape checked by hand in `IValueObjectValidator<string>` is 1.6 to 12 times as fast as the source-generated
expression, so a regular expression was never the fastest way to check a shape; it is the declarative one, and
the one that reaches the schema.

The regex source generator does see code a person wrote. A `[GeneratedRegex]` partial property on the value object
itself compiles at build time, under the JIT and native AOT alike. What it loses is the declaration in one place:
the generator has to find the expression on the type, and the schema has to be read from it.

## Decision

We will add a hook, `IValueObjectPatternValidator`, whose member the consumer writes as a source-generated regular
expression:

```csharp skip
[GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
public static partial Regex Pattern { get; }
```

and we will deprecate the `Pattern` option in a minor release, then remove it at the next major version.

- **The rule keeps its place and its error.** The pattern runs after `MinLength` and `MaxLength`, before the known
  values and `IValueObjectValidator<T>.ValidateValue`, and rejects as `value_object.invalid_format` with the
  message the option used, "The value does not match the expected format."
- **The rule is still declared once.** The generator reads the expression off the `[GeneratedRegex]` attribute
  when the type compiles and emits it as the schema's `pattern`, a literal, as the option's was.
- **The option is marked `[Obsolete]` with the diagnostic id `VO0021`**, so the compiler reports it as a warning
  wherever it is set. Its message names the migration: remove `Pattern = "X"` and write
  `[GeneratedRegex("X", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]`, the options and the
  timeout the option used, so behaviour does not change.
- **The generator guards the hook.** Declaring both the hook and the option is `VO0022`, an error, and the hook
  wins. The hook on a value object that is not a string is `VO0023`, and on an `[EntityId]`, which owns its
  format, `VO0024`. A `RegexOptions` the schema's text cannot carry — `IgnoreCase`, `Multiline`, `Singleline`,
  `IgnorePatternWhitespace` — is `VO0025`, a warning, and a `[GeneratedRegex]` without `matchTimeoutMilliseconds`
  is `VO0026`, a warning. `VO0011` reports a public `static Regex Pattern` written without the interface, except on
  a type that implements another hook, such as a validator or a normalizer, which may already run it.

ADR-0002 stays as it was written: its decision stands, and the run-time `Regex` it mentions is now the deprecated
path rather than the only one.

### Rejected

- **Keep the option and do nothing.** Native AOT consumers would keep paying for an interpreted expression, for a
  constraint of the tooling rather than of their code.
- **Remove the option in the same release.** Every consumer setting it would fail to compile on a minor upgrade.
  The obsolete warning leaves until the next major version to migrate.
- **Always read the schema's pattern from `Pattern.ToString()` at run time.** It remains the fallback for a
  `Pattern` written without the attribute, but as the rule it would build a regular expression merely to describe
  a type.

## Consequences

- Under native AOT, a migrated pattern runs its regex engine in about half the time. Every consumer gets a regular
  expression checked at build time: an invalid one is reported by the regex generator, so `VO0014` now concerns
  only the deprecated option.
- The declaration is longer. A one-line `Pattern = "X"` becomes an interface on the type, a `using
  System.Text.RegularExpressions;`, which is not among the implicit usings, and a two-line partial property.
- The match timeout is now the consumer's to write. The option fixed one second; the hook fixes nothing, and only
  `VO0026` stands between a pathological input and a request thread held for as long as the match runs.
- **A consumer with `TreatWarningsAsErrors` breaks on upgrade.** `VO0021` is a warning, so such a build fails
  until the option is migrated or `VO0021` is suppressed. That is the intended pressure, but it arrives in a minor
  version, and the release notes have to say so.
- `VO0021` is the `DiagnosticId` of an `[Obsolete]`, which the compiler reports. No descriptor declares it, so it
  appears in no analyzer release file and is documented by hand.
- The generator tests run the framework's regex generator beside this one, so that a snippet implementing the hook
  compiles. Its version is the SDK's, copied from the targeting pack by the `CopyRegexGenerator` target.
- Until the next major version, the generator carries both paths, and the run-time `Regex` stays tested.
