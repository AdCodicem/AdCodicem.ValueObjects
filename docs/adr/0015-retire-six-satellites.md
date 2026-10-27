# 15. Retire six satellites: Marten, Wolverine.Http, HotChocolate.Data, Orleans, CommandLine and CsvHelper

Date: 2026-10-27

## Status

Accepted. Applies [ADR-0014](0014-return-to-one-version-for-every-package.md): with one version for every package,
a satellite whose framework moves on a calendar of its own, or whose code reaches that framework's internals, costs
every release; this record retires the six that cost the most for the fewest users.

## Context

The thirty-eight packages were reviewed one by one: their source, their tests, their CI, the third-party APIs they call
and how public those APIs really are. None of the six below has a stable release: each exists on nuget.org as previews
of 0.3.0 only, so no stable consumer depends on one.

| Package | Source | Tests | What it costs beyond its code |
| --- | --- | --- | --- |
| `Marten` | 1,275 lines, and `VO0048`'s analyzer, 360 lines, in the core generator | about 5,300 lines | `[UnsafeAccessor]` on the internal `StoreOptions.ValueTypes`; `Marten.Internal.CompiledQueries` in four files; a 494-line test pinning Marten's private query planning; its own `native AOT (Marten)` job, 16 to 19 minutes a run on `main` against 4 to 5 for every other native application; a PostgreSQL service; JasperFx's source generators stripped from the build. |
| `Wolverine.Http` | 871 lines | about 3,000 lines, and a fixture application | Three `[UnsafeAccessor]`s on Wolverine's internals (`HttpChain.IsFormData`, `RequestType`, `ReassignCreator`); a helper that the code Wolverine generates in the consumer's application calls, so its signature is frozen; Roslyn 5.0.0 pinned beside the generator's 4.14 for the fixture; a codegen pass in `native-aot.sh`. |
| `HotChocolate.Data` | 611 lines | about 1,750 lines | `IExtendedType` of `HotChocolate.Internal`; no native code for the filtering of a struct; an integration test over two databases. |
| `Orleans` | 582 lines | about 1,700 lines, a grains fixture and a CI family of its own | Orleans does not start natively; Orleans' Roslyn 5 metadata, which kept a unit project off the generator's Roslyn and put `Microsoft.CodeAnalysis.Analyzers` into a shipped nuspec. |
| `CommandLine` | 872 lines | about 2,000 lines | A workaround of System.CommandLine's sealed `ParseError` and its internal arity rules, on a 3.0 still a release candidate. |
| `CsvHelper` | 340 lines | about 1,950 lines | CsvHelper's record API cannot map a value object at all; no native code. |

Together: about 4,900 lines of shipped source, about 15,800 lines of tests, one CI job, one CI family, two fixtures, a
second Roslyn and the JasperFx build workarounds. Neither CsvHelper nor System.CommandLine reads `TypeDescriptor`, so
the generated `TypeConverter` does not stand in for those two packages.

Among the satellites that stay, `AspNetCore.NewtonsoftJson` derives from `Microsoft.AspNetCore.JsonPatch.Internal`, and
`HotChocolate` and `FluentValidation` use one internal type each. They serve wider audiences on frameworks with steadier
calendars, and are kept.

## Decision

We will remove the six packages, their tests, their CI and their documentation, in the next minor, 0.3.0.

- **Removed**: the six projects under `src/`, their tests in the unit, integration, native AOT and compatibility suites,
  the Orleans family and project of the unit suite, the `Grains` and `WolverineHttp` fixtures, the `native AOT (Marten)`
  job, the Roslyn 5.0.0 pins, the JasperFx targets, their pins, Dependabot rules and shipped dependencies, and their
  pages.
- **`VO0048` is retired** with the Marten package. Its identifier is never reused (`AnalyzerReleases` tracked it as
  unshipped; `website/docs/reference/diagnostics.md` lists it among the retired ones).
- **`VO0043` keeps reporting Orleans' `[GenerateSerializer]`** on a value object, since the attribute still loses the
  value without the package; its remedy no longer names one, and says to give the value object a surrogate of the
  author's.
- **`ValueObjectScalarOptions.QuoteScalarNameIn`** stays in the HotChocolate package: it is HotChocolate's API, and an
  application filtering with HotChocolate.Data on its own still uses it.
- **nuget.org**: the maintainer deprecates every preview of the six packages (reason *Legacy*, no alternate package, a
  message linking this record), never unlists them, as `docs/maintaining.md` says.

### Rejected

- **Keeping them on a train of their own.** ADR-0014 withdraws the trains.
- **Keeping Marten without its native AOT job.** The job is what proves the package's claim to native AOT; without it
  the claim is untested, and the internals it reaches stay.
- **Retiring JSON Patch from `AspNetCore.NewtonsoftJson` too.** It reaches internals as well, but serves applications
  still on Newtonsoft.Json's MVC; it is the next candidate if its upstream moves.

## Consequences

- 0.3.0 ships thirty-two packages. A preview user of one of the six keeps the preview, which nuget.org keeps listed and
  marks deprecated; nothing replaces it here.
- `heavy.yml`'s native AOT work no longer waits on Marten, and the unit suite has ten projects.
- An application on Marten, Wolverine, Orleans, System.CommandLine or CsvHelper writes its own adapter over `TryCreate`
  and `TryParse`, the paths every package of this repository goes through.
