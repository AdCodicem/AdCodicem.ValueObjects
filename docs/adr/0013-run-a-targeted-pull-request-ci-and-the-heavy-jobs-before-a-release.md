# 13. Run a targeted pull-request CI and the heavy jobs before a release

Date: 2026-10-19

## Status

Accepted.

- Amends [ADR-0010](0010-version-every-package-in-lockstep-independently-of-dotnet.md), which
  [ADR-0012](0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md) supersedes, on one point:
  `compat (.NET 11)` does not join the required checks when .NET 11 ships. It gates every preview and every stable
  release instead.
- Amends [ADR-0009](0009-publish-previews-weekly-when-a-package-input-changed.md): a preview runs this record's heavy
  jobs before it publishes.
- Amends [ADR-0006](0006-coverage-is-a-signal-not-a-goal.md) in its second phase: the unit suite splits into a test
  project per family, where ADR-0006 rejected a fourth test suite.
- Built up to phase A, which differs from the text below on the points of [Phase A as built](#phase-a-as-built).

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

On a pull request, we will run only the jobs of the package families and the kinds of files it changes, behind the one
required check that `ci.yml` already reports. The heavy jobs will run before every preview and stable release, and after
every push to `main`.

- **Concurrency.**
  - `ci.yml` and `lint.yml` set `group: ${{ github.workflow }}-${{ github.event.pull_request.number || github.sha }}`
    and `cancel-in-progress: ${{ github.event_name == 'pull_request' }}`.
  - A push to a pull request cancels the run it overtakes. A push to `main` keeps its run.
  - Concurrency groups are repository-wide, hence the workflow's name in the key. Without it, `ci.yml` and `lint.yml`
    would cancel each other's required checks.
  - A reusable workflow sets its concurrency on its jobs, since `github.workflow` there is its caller's name.
- **Eleven CI families.** `.github/ci-families.json` maps every project under `src/`, and every top-level folder of the
  unit suite, to exactly one family, and every family to one release train (ADR-0012):

  | Family | Projects under `src/` | Train |
  | --- | --- | --- |
  | `core` | AdCodicem.ValueObjects, Generators, CodeFixes, Abstractions, Json, Identifiers, Testing.Data | core |
  | `aspnet` | AspNetCore.Http, AspNetCore, OpenApi | core |
  | `efcore` | EntityFrameworkCore, Identifiers.EntityFrameworkCore | core |
  | `data` | Dapper, MongoDB, Identifiers.MongoDB, Marten | satellites |
  | `serialization` | NewtonsoftJson, AspNetCore.NewtonsoftJson, Swashbuckle, NJsonSchema, MessagePack, ProtobufNet, Grpc | satellites |
  | `orleans` | Orleans | satellites |
  | `hosts` | AzureFunctions, AzureFunctions.AspNetCore, Wolverine.Http | satellites |
  | `graphql` | HotChocolate, HotChocolate.Data | satellites |
  | `text` | Blazor, CommandLine, CsvHelper, which share `ValueObjectText.cs` | satellites |
  | `observability` | Serilog, AI, ModelContextProtocol, FluentValidation | satellites |
  | `testdata` | Testing, AutoFixture, Bogus, FsCheck | satellites |

  Eleven families for forty projects stay under the twenty concurrent jobs of GitHub's free plan.
- **A plan.** The first job of `ci.yml`, `plan`, runs `.github/scripts/ci-plan.sh`, which picks families and lanes:
  - It crosses `git diff <base>...<head>` with `dotnet-affected` 7.0.1, pinned as a local tool in
    `.config/dotnet-tools.json`. The tool follows the MSBuild graph: `ProjectReference`s, the files of `src/Shared` that
    several projects link, a `<PackageVersion>` bump, an imported props file. Replayed over 76 merged pull requests and
    320 commits, it ran without a restore in 0.7 seconds at the median, 15.9 at most.
  - An affected project under `src/` selects its family and every family that depends on it. An affected test project
    with no affected project under `src/` (a bump of xunit or of the coverage tool) selects every family of its suite.
  - A change to a folder of the unit suite selects that folder's family; `Domain/`, `GeneratedSurface/` and the suite's
    project file select every family.
  - Path rules cover what no project holds:
    - **none**: `CLAUDE.md` files, `docs/**`, root Markdown files other than `README.md`, `.github/ISSUE_TEMPLATE/**`,
      `.claude-plugin/**`, `.github/dependabot.yml`, and workflows other than `ci.yml` and `heavy.yml`. Only `lint.yml`
      checks them.
    - **documentation**: `README.md`, `website/docs/**` and `skills/value-objects/**`. The generator suite runs,
      filtered on `[Trait("Reads", "Documentation")]`; the nine classes that read the repository carry it, and a test
      holds them to it.
    - **site**: `website/**`, `docs/docfx/**` and `.github/actions/build-site/**`, and any project under `src/` (DocFX
      reads its API). The `documentation site` job runs.
    - **every family**: the global build inputs (`Directory.Build.*`, `global.json`, `nuget.config`, `.editorconfig`,
      the solution files, `ci.yml`, the plan's script and family table, `codecov.yml`), a change of
      `Directory.Packages.props` outside its `<PackageVersion>` elements, and any path no rule names.
    - **release tooling**: `package.json`, `.releaserc.json` and the version scripts run `next-version.mjs` for each
      train.
    - **heavy inputs**: `tests/NativeAot/**`, `tests/Compat/**`, the integration suite, `samples/**`, the native and
      compiled-model scripts and `heavy.yml` are compiled on the pull request, and a notice asks for `ci:heavy`.
  - When `dotnet-affected` fails (any exit code but 0, and 166 for "nothing affected"), the plan selects every family.
  - At every run, without a build, the plan checks that every project under `src/` and every top-level folder of the
    unit suite belongs to exactly one family, and fails otherwise. So no test can fall outside every filter.
  - A push to `main`, a dispatch, and a call from `preview.yml` or `release.yml` select every family.
- **What a selected family runs.** Every .NET run formats whitespace in a job of its own (`dotnet format whitespace .
  --folder --verify-no-changes`, without the style pass, which the build already enforces) and builds the solution.
  Then, per family:
  - its unit tests;
  - the generator suite in full for `core`, the RDG suite for `aspnet`, the compiled model generated and compiled
    without SQL Server for `efcore` (`compiled-model.sh compile`), and `ecmascript-patterns.mjs` for `text`;
  - with any family, the cross-package guards: `GeneratedSurface`, `RuntimeClosingTests`, `DescriptorTests`, and
    `coverage-modules.sh` over the families of the run, about 18 seconds;
  - when a project under `src/` is affected, the pack and its checks: every packable project packed,
    `shipped-dependencies-check.sh`, and the train check of ADR-0012.

  Everything that caught a real defect on a pull request in the last 40 days still runs there.
- **The unit suite is split in two phases.**
  - **Phase A**, with the plan: each family is a `--filter-query` on the namespaces of its folders, inside the one
    unit-test assembly. Most of the gain comes from targeting and from moving the heavy jobs: a median of 4.0 minutes
    over the replay.
  - **Phase B**: one test project per family, behind a `family` matrix fed by the plan, each with its own
    `InternalsVisibleTo` entry. It starts with `orleans`, `hosts` and `graphql`, whose dependencies condition
    `Directory.Packages.props` on the project name (Roslyn 5.0.0 for Orleans and Wolverine), then the others. A family's
    graph builds cold in 17 seconds, against 73 for the unit suite. An edit to one test file rebuilds in 6.7 seconds,
    against 38.7. The median falls to 2.4 minutes.
  - Phase B amends ADR-0006, which rejected "a fourth test suite": it is the same unit suite, split by family.
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
  - It runs on dispatch, and on a Dependabot NuGet pull request for the heavy jobs that cover its families, since
    auto-merge lands it with no human reading it.
  - `compat (.NET 11)` only informs until .NET 11's general availability, because the island targets a release candidate
    until then. After it, it blocks previews. It always blocks a stable release, and it is never a required check of a
    pull request.
- **Coverage.**
  - Codecov gets one flag per family, plus `generator`, `rdg` and `integration`, each with the `paths` of its projects
    (generated from the family table and the links of `src/Shared`) and `carryforward`.
  - A pull request's unit report is uploaded with the flags of the families that ran; the others carry forward from the
    base.
  - The patch target (95 %) is measured by the families that ran, which hold the changed lines. A change to a file of
    `src/Shared` selects every family that links it, so no changed line is judged on carried-forward coverage.
  - A push to `main` runs every family and refreshes every flag; `heavy.yml` uploads the integration suite's coverage
    there.
  - The ruleset's `code_coverage` rule (minimum 59) goes: no workflow reports coverage to GitHub, so it measures
    nothing, and Codecov is the coverage signal (ADR-0006).

### Rejected

- **`paths` or `paths-ignore` on the workflow.** A required check stays pending when the workflow is skipped, and
  Markdown is a test input.
- **Lanes by path only, with no families.** It brings the median to about 4 minutes too, but a satellite's pull request
  would still build and run the whole unit suite, and phase B, which brings 2.4 minutes, needs the families anyway.
- **A project graph of our own instead of `dotnet-affected`.** It would rewrite MSBuild's evaluation: imports, linked
  files, and the conditions of `Directory.Packages.props` on `$(MSBuildProjectName)`.
- **`dorny/paths-filter`.** It ignores the project graph, the linked files of `src/Shared` and the package bumps, and
  adds a third-party action to pin.
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

- Estimated from the replay: a satellite's pull request waits about 3.1 minutes for its checks in phase A, 2.4 in phase
  B, instead of 7.9; a core pull request 4.0, then 2.4; a documentation change about 2.4; a change outside .NET and the
  site under one.
- CodeQL keeps a floor of about 6.4 minutes on a human pull request. Dependabot's pull requests, which CodeQL skips,
  gain the most.

Heavy regressions move after the merge:

- A native AOT, Marten, integration or .NET 11 regression is no longer seen on its pull request. It shows about twenty
  minutes after the merge, or before it when the author adds `ci:heavy`.
- A preview's critical path grows by the Marten job, about 19 minutes.
- A flaky heavy job holds back the Monday preview until "Re-run failed jobs"; nothing is published in the meantime.

The families are a registry:

- `.github/ci-families.json` changes with every project added or moved; the plan's check fails until it does.
- `dotnet-affected` is a third-party tool with one maintainer upstream. It is pinned, and when it fails the plan runs
  every family.
- Codecov's configuration lists a flag per family, generated from the same table.

Documentation must follow:

- Several files change: `docs/maintaining.md` (line 195, and lines 427 to 433), `.github/CLAUDE.md` (line 86), the root
  `CLAUDE.md` (line 271 and its formatting commands), and the comment in `dependabot-auto-merge.yml` (lines 79 to 81).
- They must say that the required checks are `build and test`, `workflows` and `conventional commits`, and that `compat
  (.NET 11)` is a release gate.

On a pull request that changes one of the eight lines that only the integration suite covers, those lines count as
missed in `codecov/patch`. No ruleset requires that check.

## Phase A as built

The plan, the families, the gate and the coverage flags are built as decided, with these differences:

- The documentation lane runs the tests that read the repository, not the nine classes: the trait sits on
  `DocumentationSnippetTests` and `SkillCoverageTests`, whose every test reads it, and on the nine other tests that
  do. That is 92 tests in about 20 seconds, against 483 in 51 for the nine classes. `DocumentationTraitTests` follows
  each test's IL to the class's `RepositoryRoot()` or `SkillDirectory()` and fails on a reading test without the trait
  or a marked test that reads nothing.
- Any change to `Directory.Packages.props` runs everything, not only one outside its `<PackageVersion>` elements.
  `dotnet-affected` answers "nothing affected" for a pinned transitive package no project references, which a
  Dependabot security update adds, and the shipped-dependencies check must run on it. Dependabot's NuGet pull requests,
  which make nearly every such change, wait for `heavy.yml` anyway.
- A file at the unit suite's root selects every family with a class in the root namespace (`core`, `serialization`,
  `observability`, `testdata`), since a file there may declare a class of any of them, and a helper several families'
  tests use selects every family (`unit.shared`). A file of the suite declares the namespace of its folder, which the
  plan checks, so that the folder a change selects and the namespace a family filters on agree.
- The generator suite also runs in full when `dotnet-affected` marks it (a change to protobuf-net, which it
  references), the RDG suite when it marks that, and the compiled model when its own inputs change.
- When every family runs, so after every merge, `unit-families-check.sh` lists the built suite through every family's
  filter and fails on a test none of them selects.
- Only a push to `main` selects everything as an event: `ci.yml` is neither dispatched nor called, and `preview.yml`
  and `release.yml` run every suite whole themselves.
- `ci.yml` uploads coverage from a matrix job, one upload per flag: Codecov drops a carried-forward upload whole when
  an upload sharing one of its flags arrives, so a report uploaded once under several family flags would lose the
  other families' lines on the next commit.
- The release tooling runs `next-version.mjs` once, in a `release tooling` job the gate needs, until ADR-0012's trains
  exist.
- `coverage-modules.sh` also fails a project of the run's families with no line covered: `RuntimeClosingTests` loads
  nearly every package to read its IL, which reports it without exercising it.
