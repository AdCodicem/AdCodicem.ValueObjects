# 13. Run a targeted pull-request CI and the heavy jobs before a release

Date: 2026-10-19

## Status

Proposed. Its concurrency rule is already in place; the rest waits for acceptance. Once accepted:

- Amends [ADR-0010](0010-version-every-package-in-lockstep-independently-of-dotnet.md), which
  [ADR-0012](0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md) proposes to supersede, on one
  point: `compat (.NET 11)` does not join the required checks when .NET 11 ships. It gates every preview and every
  stable release instead.
- Amends [ADR-0009](0009-publish-previews-weekly-when-a-package-input-changed.md): a preview runs this record's heavy
  jobs before it publishes.

## Context

`ci.yml` runs five jobs on every pull request and every push to `main`, with no path filter, no concurrency and no
matrix.

**Durations.**
- Since 2026-10-14, `build and test` takes 7.9 minutes at the median (p90 9.4).
- A whole run takes 18.1 minutes. `native AOT (Marten)` alone sets that time (18.7 minutes), and only seven projects are
  its inputs.
- Inside `build and test`:
  - the integration suite takes 115 seconds, mostly starting containers;
  - the build 94;
  - Format 70;
  - the unit suite 52;
  - the generator suite 47;
  - the compiled model 27.
- The thirty-eight packages build in 16 seconds. The tests are what costs.

**What pull requests change, and what CI catches.**
- 45 % of pull-request runs (46 % of their minutes) changed no .NET code. Markdown is still a test input:
  - `DocumentationSnippetTests` compiles 69 snippets from `README.md`, `website/` and the skill;
  - `SkillCoverageTests` reads the skill;
  - nine classes of the generator suite read the repository.
- 14 of 183 pull-request runs were overtaken by a push before they finished: 149 of 1,143 job-minutes, or 13 %.
- In 40 days, five real defects were caught on a pull request:
  - by the pack (NETSDK1085);
  - by `shipped-dependencies-check.sh`, twice;
  - by the compiled model (NETSDK1004, CS1929/CS0117).

  All five were compile errors. None needed a database or a native publish. 21 of the 28 failed runs had nothing to do
  with their pull request.

**Ruleset and costs.**
- The `Default` ruleset (id 23842638) requires `build and test`, `native AOT`, `workflows` and `conventional commits`,
  up to date with `main`.
- It also requires CodeQL code scanning. Its `Analyze (csharp)` job took 6.4 minutes on #178, a documentation-only pull
  request, and does not run on Dependabot's pull requests.
- The maintainer merges through the ruleset's bypass. The checks therefore mostly hold back Dependabot's auto-merge,
  which has no reviewer.
- GitHub reports a job skipped by its `if` as a success to a ruleset. A path filter that skips a whole workflow leaves
  its required check pending.
- The repository is public: 0 minutes billed.

**Measured locally on a cold cache.**
- The whole solution builds in 92 seconds, and the unit suite's graph of 50 projects in 73.
- A prototype test project for one family (CsvHelper, 9 projects) builds in 17 seconds, with the same 98 tests green.
- Replayed over the 320 commits since 2026-10-01:
  - filtering families with `--filter-query` inside the one unit-test assembly brings the median to 4.0 minutes;
  - test projects per family bring it to 2.4.
- The unit, generator and RDG suites cover 12,779 of the 12,787 lines that Codecov counts as covered on `main`. The
  eight left are the SQL Server and PostgreSQL branches of the EF Core CHECK constraints
  (`CheckConstraintRules.cs:126,165,166,233,331,332,338`, `ValueObjectCheckConstraintConvention.cs:72`).
- `dotnet format … style` takes 63 seconds and checks what the build already holds to error. `EnforceCodeStyleInBuild`
  (`Directory.Build.props:8`) fails the build on the one warning-level style rule (`.editorconfig:77`, IDE0161), as
  verified.
- A NuGet cache would save little: the restore takes 16 seconds, and the full cache weighs 3.3 GB.

## Decision

On a pull request, we will run only the jobs for the kinds of files it changes, behind the one required check that
`ci.yml` already reports. The heavy jobs will run before every preview and stable release, and after every push to
`main`.

- **Concurrency.**
  - `ci.yml` and `lint.yml` set `group: ${{ github.workflow }}-${{ github.event.pull_request.number || github.sha }}`
    and `cancel-in-progress: ${{ github.event_name == 'pull_request' }}`.
  - A push to a pull request cancels the run it overtakes. A push to `main` keeps its run.
  - Concurrency groups are repository-wide, hence the workflow's name in the key. Without it, `ci.yml` and `lint.yml`
    would cancel each other's required checks.
  - A reusable workflow sets its concurrency on its jobs, since `github.workflow` there is its caller's name.
- **A plan by path.** The first job of `ci.yml`, `plan`, runs `.github/scripts/ci-plan.sh` on `git diff --name-only
  <base>...<head>` and picks lanes, with no third-party tool:
  - **none**: `CLAUDE.md` files, `docs/**`, root Markdown files other than `README.md`, `.github/ISSUE_TEMPLATE/**`,
    `.claude-plugin/**`, `.github/dependabot.yml`, and workflows other than `ci.yml` and `heavy.yml`. Only `lint.yml`
    checks them.
  - **documentation**: `README.md`, `website/docs/**` and `skills/value-objects/**`. The generator suite runs, filtered
    on `[Trait("Reads", "Documentation")]`; the nine classes that read the repository carry it, and a test holds them to
    it. The site job also runs when `website/` changed.
  - **site**: `website/**`, `docs/docfx/**` and `.github/actions/build-site/**`. The `documentation site` job runs.
  - **.NET**: every other path, an unknown one included, so that a missing entry costs time and never a check.

  A push to `main`, a dispatch, and a call from `preview.yml` or `release.yml` run every lane.
- **The .NET lane** keeps everything that caught a real defect, and leaves out what needs a container, a native compiler
  or the next SDK. It runs:
  - whitespace formatting in a job of its own (`dotnet format whitespace . --folder --verify-no-changes`), without the
    style pass;
  - the build of the whole solution, which compiles the native applications and the integration suite too;
  - the unit, generator and RDG suites, in parallel;
  - the compiled model, generated and compiled but without SQL Server (`compiled-model.sh compile`);
  - the pack and its checks: every packable project packed, `shipped-dependencies-check.sh`, `coverage-modules.sh`, the
    train check of ADR-0012, and `next-version.mjs` for each train when the release tooling changed;
  - the documentation site;
  - one Codecov upload, as today.
- **One required check.**
  - `build and test` becomes an aggregator that runs with `if: always()`, and the build job is renamed `build`.
  - The aggregator fails when `plan` did not succeed, when a job it needs failed or was cancelled, or when a job that
    `plan` listed in its `expected` output did not succeed. So an `if` written wrong cannot pass a pull request.
  - The ruleset keeps `build and test`, `workflows` and `conventional commits`, and drops `native AOT`.
  - The order: the aggregator lands first, under the name the ruleset already requires; then `native AOT` leaves the
    ruleset; then the heavy jobs leave pull requests. No pull request is ever left waiting for a check that never
    reports.
- **The heavy jobs move to `heavy.yml`**, a reusable workflow: `native AOT`, `native AOT (Marten)`, `integration tests`
  with the compiled model's SQL Server round trip (`compiled-model.sh jit`), and `compat (.NET 11)`.
  - It takes the commit, the packages to install and the version of each train (ADR-0012).
  - It completes the island's package folder with any train the run does not release, downloaded from nuget.org at the
    version the other train was packed against. So the island always installs bytes that nuget.org has, or will have.
  - It runs in `preview.yml` and `release.yml`, on the packages the run packed, as a job the publish job needs. While
    one of its jobs fails, nothing reaches nuget.org and the site is not redeployed.
  - It runs after every push to `main`, superseded runs cancelled, so that a regression shows within about twenty
    minutes, on its commit.
  - It runs on a pull request labelled `ci:heavy`, which `plan` reads from the event when the run starts. The label is
    not a trigger: a run started by labelling would cancel the real run.
  - It runs on dispatch, and on every Dependabot NuGet pull request, which auto-merge lands with no human reading it.
  - `compat (.NET 11)` only informs until .NET 11's general availability, because the island targets a release candidate
    until then. After it, it blocks previews. It always blocks a stable release, and it is never a required check of a
    pull request.
- **Coverage.** The .NET lane makes one upload; `heavy.yml` uploads the integration suite's coverage after a push to
  `main`. There are no per-family flags and no carry-forward.
- **Targeting by family waits for test projects per family.**
  - Targeting inside the one unit-test assembly saves too little to pay for a family table and its filters.
  - The unit suite splits by family, starting with Orleans, whose Roslyn 5.0.0 pins condition `Directory.Packages.props`
    on the project, when one of these happens:
    - a second restore conflict confined to the tests, after #189;
    - an agent's edit, build and test loop measured as blocking (an edit to one test file rebuilds the unit suite in
      38.7 seconds, and a family's project in 6.7);
    - a family whose jobs dominate the .NET lane.
  - That split amends ADR-0006, which rejected a fourth test suite.

### Rejected

- **`paths` or `paths-ignore` on the workflow.** A required check stays pending when the workflow is skipped, and
  Markdown is a test input.
- **Families inside the one unit-test assembly**: eleven families, `--filter-query`, `dotnet-affected` 7.0.1, and one
  Codecov flag per family with carry-forward.
  - It brings the median to 4.0 minutes, against about 4 for the .NET lane run as parallel jobs. Both are under CodeQL's
    6.4 minutes on a human pull request.
  - It adds a third-party tool and a table to maintain.
  - A test named outside its family's filter runs nowhere.
  - Carried-forward coverage of a changed shared file is shifted by the commit.
- **A required check per family or per heavy job.** Every new or renamed family would touch the ruleset.
- **Running the heavy jobs nightly instead of after each push.** A regression would be found hours later, among several
  merges.
- **`compat (.NET 11)` as a required check once .NET 11 ships**, as ADR-0010 planned. It would put a container job and
  the next SDK on every pull request.
- **A NuGet cache.** It would save and restore 3.3 GB to spare a 16-second restore, with no lock file to key it on
  (ADR-0004).
- **A merge queue.** Not evaluated: the repository belongs to a user account, and the strict up-to-date policy already
  serializes the merges that need it.

## Consequences

Pull requests wait less, within a floor:

- A pull request that changes .NET code waits about 4 minutes for its checks instead of 7.9.
- A documentation change waits about 2.4 minutes, and a change outside both under one. These are estimates from the
  replay.
- CodeQL keeps a floor of about 6.4 minutes on a human pull request. Dependabot's pull requests, which CodeQL skips,
  gain the most.

Heavy regressions move after the merge:

- A native AOT, Marten, integration or .NET 11 regression is no longer seen on its pull request. It shows about twenty
  minutes after the merge, or before it when the author adds `ci:heavy`.
- A preview's critical path grows by the Marten job, about 19 minutes.
- A flaky heavy job holds back the Monday preview until "Re-run failed jobs"; nothing is published in the meantime.

Documentation must follow:

- Several files change: `docs/maintaining.md` (line 195, and lines 427 to 433), `.github/CLAUDE.md` (line 86), the root
  `CLAUDE.md` (line 271 and its formatting commands), and the comment in `dependabot-auto-merge.yml` (lines 79 to 81).
- They must say that the required checks are `build and test`, `workflows` and `conventional commits`, and that `compat
  (.NET 11)` is a release gate.
- The path table of `ci-plan.sh` is a registry that an agent edits when it adds a new kind of file.

On a pull request that changes one of the eight lines that only the integration suite covers, those lines count as
missed in `codecov/patch`. No ruleset requires that check.
