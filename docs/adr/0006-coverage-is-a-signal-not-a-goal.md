# 6. Coverage is a signal, not a goal

Date: 2026-10-01

## Status

Accepted

## Context

CI has uploaded the coverage of all three suites to Codecov since the start, with no `codecov.yml`. Codecov
therefore applied its defaults, and nothing said what a pull request owed in coverage, nor how to get there. On
`main` at `13f59d4` Codecov reported 82.6 % — 2,218 of 2,685 lines, 353 missed and 114 partial — and that figure
was wrong in three ways:

1. **Two packages were invisible.** The coverage collector instruments only the assemblies a test process loads.
   No test loaded `AdCodicem.ValueObjects.Dapper` or `AdCodicem.ValueObjects.NewtonsoftJson`, so neither appeared
   in any report: their lines were not counted as missed, they were not counted at all.
2. **The sample was measured.** `samples/AdCodicem.ValueObjects.Sample.Api` ships in no package, yet its four
   files weighed in the figure next to the packages.
3. **The code that matters most is not in it.** What the generator emits — `Create`, `TryParse`, the operators,
   the JSON and type converters — is compiled into the consumer's assembly. In this repository that is the unit
   test assembly, which the collector excludes, and its sources live under `artifacts/obj/`, outside the
   repository, where Codecov could not place them anyway. Codecov sees the emitters, not what they produce.

The sibling repository [AdCodicem.Pdf](https://github.com/AdCodicem/AdCodicem.Pdf) settled the same question with
its maintainer: a `codecov.yml` with a floor, and a written rule for coverage work, under which its `src/` went
from 93.6 % to 99.6 %. The rule transfers as it stands; what counts as a natural input differs.

## Decision

We will hold pull requests to Codecov's floor, aim at full coverage of what can be covered, and say in writing
what stays uncovered and why.

- **`codecov.yml` is the floor.** The lines a pull request adds or changes are 95 % covered, a partial line
  counting as not covered, and the project's coverage drops by half a point at most against the base commit.
  Only `src/` is measured: `tests/`, `samples/` and `benchmarks/` are ignored.
- **The aim is 100 % of each patch, as Codecov counts it.** Every member that is not private — public, internal,
  protected — is covered as far as it can be: through a natural input where one reaches it — a declaration
  compiled through the generator, a call through the public API, a request through ASP.NET Core, a round trip
  through a database —, otherwise by a test that calls it directly, with what no natural input can produce.
- **A private member is covered by its callers or not at all.** A private member, or a member of a private nested
  type, which a test could reach only by reflection, is not tested directly.
- **A branch the compiler adds stays partial.** The default arm of an exhaustive switch expression, a `?.` on a
  value that is never null: neither is rewritten to please the tool.
- **Code goes only when no input can reach it** — conditions that contradict each other, a dead branch, a
  non-public member nothing calls —, never because no test does. A defensive branch stays, covered or not. A
  public member nothing calls is a question for the maintainer, not a removal.
- **Every shipped assembly is measured.** `.github/scripts/coverage-modules.sh` runs after the upload in `ci.yml`
  and fails when a project under `src/` with code of its own is in no report.
- **The unit suite reaches every package.** The satellite packages were exercised only by the integration suite,
  which needs Docker. The unit suite now references them and tests their behaviour directly; the integration suite
  keeps what a real database or a real HTTP pipeline must confirm.
- **The emitted code is audited, not tracked.** It was measured once, with the unit test assembly included, and
  every emitted member no test reached was given one. Nothing in CI follows it: Codecov cannot count files outside
  the repository, and the emitters it does count are the closest proxy it has.

The rule is written in `CLAUDE.md`, `CONTRIBUTING.md` and the site's Contributing page.

### Rejected

- **A blocking 100 % patch target.** Defensive branches and branches the compiler adds would each need a
  hand-made exception, and the target would start pulling code into shape for the tool.
- **Informational statuses only.** A floor nobody enforces drifts; the rule would be read once and forgotten.
- **Keeping the sample in the figure.** It is the end-to-end showcase the integration suite drives, not a
  package, and its coverage says nothing about what consumers install.
- **A fourth test suite for the satellite packages.** Each suite has a distinct job; testing a package's
  behaviour directly is the unit suite's.
- **Tracking emitted code in CI.** A report published as an artifact on every run would be read by nobody: no
  status could hold a pull request to it.

## Consequences

- A pull request that adds code without tests fails `codecov/patch`, and one that deletes tests fails
  `codecov/project`. Whether either blocks a merge is the ruleset's setting, not this file's.
- The first figure after this change is not comparable with the last one before it: the sample left it, and two
  packages entered it at 0 % before their tests did.
- A new package under `src/` fails CI until a test loads it.
- The unit suite now depends on ASP.NET Core, EF Core, Dapper, FluentValidation and Newtonsoft.Json, and builds
  slower for it.
- The emitted code can regress unseen until it is audited again; the emitter tests and the generator's snippets
  are what guard it in between.
