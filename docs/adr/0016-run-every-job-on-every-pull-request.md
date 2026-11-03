# 16. Run every job on every pull request

Date: 2026-11-03

## Status

Accepted. Withdraws the targeting of
[ADR-0013](0013-run-a-targeted-pull-request-ci-and-the-heavy-jobs-before-a-release.md): its plan, its lanes,
`dotnet-affected`, the `ci:heavy` label and the heavy jobs left to after the merge. The rest of ADR-0013 holds: the
test project per family, the `build and test` aggregator, the heavy jobs before every preview and stable release and
after every push to `main`, and `compat (.NET 11)` off the required checks.

## Context

ADR-0013 targeted the pull-request CI when a whole run took 18.1 minutes, set by `native AOT (Marten)`, and
`build and test` 7.9 at the median. [ADR-0015](0015-retire-six-satellites.md) retired Marten and five other
satellites on 2026-10-27, and phase B had split the unit suite into a project per family. Measured on GitHub since:

| Run | Runs | `build and test` green after (median, p90) |
| --- | --- | --- |
| A push to `main`, every family | 23 | 4.6, 6.0 minutes |
| A pull request the plan ran whole | 14 | 4.9, 5.9 |
| A pull request the plan reduced to one to three families | 3 | 1.6, 3.9, 4.7 |
| A pull request changing no .NET code | 38 | 0.3, 2.2 |
| A Dependabot NuGet pull request, `heavy.yml` included, since ADR-0015 | 2 | 4.8, 5.9 |

- The critical path is the `build` job, about 4 minutes: the build 64 seconds, the generator suite 74, the pack and its
  checks 25, the compiled model 19. The `unit (<family>)` jobs take 0.6 to 1.6 minutes beside it, so targeting the
  families saves less than a minute on a pull request that changes .NET code.
- `heavy.yml` on `main` since ADR-0015, five runs: `native AOT` 5.1 minutes at the median, `integration tests` 3.8,
  `compat (.NET 11)` 1.8, the run 5.2. Before it, `native AOT (Marten)` alone took 18.3.
- CodeQL, which the `Default` ruleset requires, runs on every pull request a human opens, those changing only a
  workflow included, in 6.9 minutes at the median (8.2 at p90). No human pull request waits less than that, whatever
  `ci.yml` does: the plan's saving reaches Dependabot's GitHub Actions and npm pull requests alone, 29 of 78 since
  phase B, which auto-merge lands a few minutes later at most.
- No run of `ci.yml` or `heavy.yml` failed between 2026-10-20 and 2026-11-03, cancelled runs aside.
- The targeting costs: `ci-plan.sh`, 568 lines, of which the check of the family table is 90; a rule for every path,
  and a run of everything for a path no rule names; `dotnet-affected`, a third-party tool pinned with its Dependabot
  rule; the gate's `expected` list; a `ci:heavy` label read when a run starts, never by **Re-run jobs**; the
  `Reads=Documentation` trait and `DocumentationTraitTests`, which hold the documentation lane to the tests reading the
  repository; carried-forward Codecov flags judging a patch on another commit's coverage. Thirty-five commits touched
  the plan, its table or `ci.yml` between 2026-10-19 and 2026-10-28.

## Decision

Every pull request will run every job of `ci.yml`, and `heavy.yml`'s `native AOT` and `integration tests`, whatever it
changes.

- **No plan.** A job of its own, `families`, runs `.github/scripts/ci-families-check.sh`: the check of
  `.github/ci-families.json` against the repository that the plan ran first, unchanged, then the lists the matrices
  read, every family, every project of the solution but the unit suite's, and every Codecov flag. `ci-plan.sh`,
  `dotnet-affected` and its Dependabot rule go.
- **Every job runs.** `format`, each `unit (<family>)`, `build` with every suite, the compiled model, the pack and its
  checks, `release tooling`, `documentation site` and `coverage` lose the `if` that read the plan.
- **`heavy.yml` on every pull request.** `ci.yml` calls it on a pull request, never on a push, where `heavy.yml` runs by
  itself. It passes `skip-compat: true`, which skips every step of `compat (.NET 11)` and leaves the job green, since a
  called job skipped by its own `if` can skip the caller's jobs that need the call. The input is negative because a
  push leaves `inputs` empty, and an empty input compares equal to `false`. The `ci:heavy` label and the Dependabot
  NuGet case go.
- **`compat (.NET 11)` stays off pull requests.** It runs after every push to `main`, before every preview and stable
  release, and on dispatch, as ADR-0013 decided: it targets a release candidate until .NET 11 ships, and it adds
  another SDK to every pull request for a break that the upstream release, not the pull request, usually causes.
- **The gate stays, simpler.** `build and test` keeps its name and `if: always()`, and fails unless each job it needs
  succeeded, `heavy` aside on a push, which skips it. It counts its `needs`, so that a job added to the list without
  the count, or the reverse, fails it. The ruleset does not change.
- **`heavy.yml` keeps running after every push to `main`.** The maintainer merges through the ruleset's bypass, which
  can land a commit that no pull request ran.
- **Coverage.** Every run uploads every flag. The flags keep `carryforward`, which now only covers an upload that
  failed.
- **The documentation trait goes.** No lane filters the generator suite any more: `[Trait("Reads", "Documentation")]`
  and `DocumentationTraitTests` hold nothing.

### Rejected

- **Keeping ADR-0013 as built.** It saves time where CodeQL already sets the wait, and its rules, table and tool are the
  most maintained part of the CI.
- **Every job of `ci.yml` on a pull request, `heavy.yml` after the merge.** About 5 minutes rather than 6, under the
  same CodeQL floor, with the label kept, and a native AOT or integration regression found after the merge.
- **A single path rule, "no .NET code changed, nothing runs".** Markdown is a test input (`DocumentationSnippetTests`,
  `SkillCoverageTests`), so the rule would need the documentation lane back, and it would save minutes only for
  Dependabot's GitHub Actions and npm pull requests.
- **`compat (.NET 11)` on every pull request.** See the Decision.
- **Merging the unit suite back into one project.** Not measured here: it would not lengthen the critical path if one
  job built and tested it in under 4 minutes, and would drop nine projects, the links of `tests/UnitTests.props` and
  the family flags. A decision of its own, on measured runs.

## Consequences

- A pull request waits about 6 minutes for `build and test`, under CodeQL's 7 on a human one; a Dependabot GitHub
  Actions or npm pull request about 6 instead of 0.3 before auto-merge lands it.
- A native AOT or integration regression shows on its pull request, not after the merge.
- A run holds 18 jobs at once (`families`, `format`, ten `unit`, `build`, `release tooling`, `documentation site`, three
  of `heavy.yml`), then twelve coverage uploads; with `lint.yml` and CodeQL it passes the twenty concurrent jobs of
  GitHub's free plan, so a job may wait a few seconds.
- A change outside .NET (a workflow, the documentation) runs everything, about 30 job-minutes a run on a public
  repository that pays none.
- `.github/ci-families.json` still changes with every project added or moved, for the unit matrix, `coverage-modules.sh`,
  `RuntimeClosingTests` and the Codecov flags, and `ci-families-check.sh` still fails until it does.
- A break of the next .NET shows after the merge, or on a dispatch of `heavy.yml` on the branch.
