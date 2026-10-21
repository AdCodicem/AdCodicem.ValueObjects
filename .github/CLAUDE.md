# .github — workflows, scripts, release mechanics

The runbook (one-time settings, routines, the Dependabot procedure) is `docs/maintaining.md`; the reasoning is ADR 0003
(stable track), 0009 (previews) and 0004 (supply chain). This file holds what an edit here must not break.

## Previews (`preview.yml`)

- Runs Mondays at 07:15 Europe/Paris and on dispatch from `main`; the `nuget` environment deploys from `main` only.
- `scripts/preview-gate.sh` decides `publish`, `repair` or `none` against the version nuget.org has from the nearest
  commit, and fails the run rather than guess when a lookup fails. All thirty-eight packages go out at one version, or
  none.
- The version: `scripts/next-version.mjs` runs semantic-release's commit analyzer with `.releaserc.json`, without a
  token, and suffixes `-preview.<commits since the last stable tag>` (`0.3.0-preview.172` leads to `0.3.0`; with no
  commit that releases anything, the next patch). It needs `fetch-depth: 0`.
- MinVer receives it through `MINVERVERSIONOVERRIDE`. An empty override silently falls back to MinVer's own version,
  which `scripts/verify-packages.sh` refuses (only `X.Y.Z-preview.N` passes unless `ALLOW_ANY_VERSION=true`).
- Order: the unit, generator and RDG suites run on that exact commit; a job of its own packs `src/`; `heavy.yml` runs on
  that commit too, its compat job installing the packed set by the artifact's id; the `publish` job, which needs all of
  them, rechecks the set (`verify-packages.sh`) and nuget.org (`preview-gate.sh --recheck`) before logging in, so
  re-running an old run cannot publish a stale version, attests, then pushes.
- `scripts/push-packages.sh` pushes dependencies first and only what nuget.org lacks, in up to three passes, then waits
  until nuget.org lists every package. A push stopped midway is completed by those passes, by **Re-run failed jobs**, or
  by the next run (`repair`).
- Every run that decides, publishing or not, redeploys the site (`website/CLAUDE.md`); a run in which a job failed
  deploys nothing.

## Stable releases (`release.yml`)

- `heavy.yml` runs on the dispatched commit and the packages the `pack` job uploaded, compat blocking
  (`compat-blocks: true`), and the `release` job needs it: it may run without it only on a dry run or when nothing was
  packed, the two cases that skip it. The `nuget-stable` reviewer is asked once it has passed.
- Dispatch `preview.yml` first and start the release once that run is green: the `was it previewed` job
  (`preview-gate.sh --report`) only warns, never blocks, when the release ships package inputs no nuget.org version
  carries.
- The `pack` job holds no credential: it computes the version with `next-version.mjs`, packs `src/` through
  `scripts/release-pack.sh` and freezes the documentation through `scripts/docs-snapshot.sh`. Once the `nuget-stable`
  reviewer approves, the `publish` job checks those files against the digests the pack job output, and semantic-release
  refuses to go on unless its version is the one packed, writes `CHANGELOG.md`, commits it with the snapshot, tags,
  pushes through `push-packages.sh`, and the site is redeployed. No build code runs beside the NuGet key, the App token
  or the OIDC token.
- MinVer and semantic-release read the same `v*` tags; `MINVERVERSIONOVERRIDE` hands MinVer the stable version.
- The GitHub Release links every package to nuget.org from `RELEASE_PACKAGE_IDS`, which the pack job evaluates with
  `scripts/package-ids.sh` and passes in semantic-release's starting environment: the body renders from it, so a prepare
  step cannot feed it. `release-pack.sh` fails before any push when the packed set differs from it.
- `attest provenance` signs the packages and every assembly inside them with a Sigstore SLSA provenance attestation.
  nuget.org re-signs each `.nupkg`, so its digest changes, but leaves the assemblies as packed, which therefore verify
  (`SECURITY.md`). `preview.yml` attests the same subjects before its push.
- Releases are immutable on this repository, so semantic-release creates a draft (`draftRelease`) and
  `attest provenance` publishes it once the bundle is attached; never publish the draft by hand. The draft's URL dies on
  publication, which is why `.releaserc.json` overrides `successComment` to link to the tag.
- `v0.1.0` is a baseline tag placed by hand, not a release (no `0.1.0` package exists): semantic-release ignores
  prerelease tags on a stable branch and would otherwise have started at `1.0.0`.

## Package inputs and shipped dependencies

- `scripts/preview-gate.sh`'s `inputs` lists what changes a package: `src/` (the solution filter included;
  `src/**/AnalyzerReleases.*.md` and `src/**/CLAUDE.md` excluded), `README.md`, `icon.png`, the root
  `Directory.Build.*` files, `global.json`, `nuget.config`, `.gitattributes`, `scripts/release-pack.sh`, and
  `Directory.Packages.props`: any change outside its `<PackageVersion>` elements, and the `<PackageVersion>` of any
  package in the restore graph of `src/`, build-time ones included (MinVer, SourceLink, PolySharp, Roslyn). That is wider
  than `shipped-dependencies`, so a `chore(deps)` bump of MinVer still publishes a preview. Add anything else that
  changes a package's bytes or metadata, or it never reaches a preview.
- `shipped-dependencies` must match the packed nuspecs: `ci.yml` (`scripts/shipped-dependencies-check.sh`) fails on a
  nuspec dependency the list lacks or a line matching nothing. `dependabot-auto-merge.yml` retitles a pull request
  updating a listed dependency `fix(deps):`, or `fix(deps)!:` across a major. Dependabot pull requests are squash-merged,
  so that the title becomes the commit semantic-release reads; a rebase keeps `chore(deps)` and releases nothing.
- A Dependabot security update pinning a transitive dependency the list does not name yet opens as `chore(deps)` (with
  no prefix when grouped) and fails `build`, so `build and test`. The workflow reads the list from the base branch and
  ignores your events, so retitle the pull request `fix(deps): …` first, then push the list line to its branch. Never
  land the line in a pull request of its own: nothing on `main` ships that ID yet, so the check fails there too.
- An update needing a later `Microsoft.Extensions.Options` or `Microsoft.Extensions.DependencyInjection.Abstractions`,
  transitive pins Dependabot never moves, fails its restore with NU1109 and opens no pull request: move those pins by hand
  with the rest of Microsoft's patch, in a `fix(deps)` commit (`docs/maintaining.md`, Routines).

## Workflows

- Pin every third-party action to a commit SHA with the release as a same-line comment
  (`uses: actions/checkout@3d3c42e… # v7.0.1`): Dependabot derives the semver bump from the comment, and a pin without one
  falls out of the `actions` group and may auto-merge as a non-major. Dependabot's `github-actions` entry lists
  `/.github/actions/*` beside `/`, since it does not look into a composite action's folder.
- `codecov/codecov-action`, `ossf/scorecard-action` and `github/codeql-action` publish annotated tags: re-pin with
  `git ls-remote <repo> 'refs/tags/vX.Y.Z^{}'`. Without `^{}` you get the tag object's SHA, which GitHub refuses.
- A local reusable workflow (`./.github/workflows/deploy-docs.yml`) or action (`./.github/actions/build-site`) stays
  unpinned: GitHub rejects `@ref` on either.
- actionlint is a downloaded binary, not an action: bump `ACTIONLINT_VERSION` and `ACTIONLINT_SHA256` together in
  `lint.yml`'s `workflows` job, the digest taken from the release's checksums file; Dependabot never updates it.
  `actionlint.yaml` silences `queue: max`, which actionlint does not know yet, in `deploy-docs.yml` and `preview.yml`.
- Trusted Publishing is keyed on a workflow file and an environment (`preview.yml` with `nuget`, `release.yml` with
  `nuget-stable`): change the nuget.org policy before renaming either file or moving its publish job.
- Required checks are job names: `build and test` (`ci.yml`), `workflows` and `conventional commits` (`lint.yml`). Never
  rename them without updating the `Default` ruleset. No job of `heavy.yml` is one, nor ever becomes one (ADR 0013).
- `build and test` is `ci.yml`'s `gate` job, an aggregator (ADR 0013): `if: always()` and `needs` on `plan`, `build`
  (the build, three suites, the compiled model, the pack and their checks) and `heavy`, failing unless the plan
  succeeded and each job of its `expected` output succeeded; a job left out of `expected` may be skipped, never failed
  or cancelled. Never give it an `if` that can skip it, or let it pass an expected job that failed, was cancelled or
  was skipped: a required check that is skipped reads as a success.
- A job that must block a merge goes into the `gate` job's `needs`, and into `scripts/ci-plan.sh`'s `expected` whenever
  it runs, never into the `Default` ruleset: a check per job would touch the ruleset at every rename (ADR 0013,
  Rejected).
- `heavy.yml` holds `native AOT`, `native AOT (Marten)`, `integration tests` (with `compiled-model.sh jit`, the SQL
  Server round trip) and `compat (.NET 11)`. It runs by itself on every push to `main` and on dispatch, and is called
  by `ci.yml` when `ci-plan.sh` says so (a pull request labelled `ci:heavy`, or one Dependabot opens on a
  `dependabot/nuget/` branch, every heavy job for now), and by `preview.yml` and `release.yml` before their publish
  job. `ci.yml` never calls it on a push: heavy.yml's own push run is the one main gets.
- `heavy.yml`'s jobs set their concurrency each, `heavy-<github.ref on a push, the run's id otherwise>-<job>`, so that
  a push to `main` cancels only an older push's heavy jobs: in a called workflow `github.workflow` is the caller's name,
  and a workflow-level group could match the caller's.
- `ci-plan.sh` reads the labels from the event, as the run starts: a `ci:heavy` label added later is seen by the next
  push, never by **Re-run jobs**. It only raises a notice when a pull request changes what only `heavy.yml` runs
  (`tests/NativeAot/`, `tests/Compat/`, the integration suite and its Functions fixture, `samples/`, `native-aot.sh`,
  `compiled-model.sh`, `heavy.yml`, Markdown aside).
- compat only informs unless the caller passes `compat-blocks: true`: `release.yml` does, `preview.yml` once .NET 11
  ships (`docs/maintaining.md`). Informing, the install of the island's SDK and its test steps take
  `continue-on-error`, each test step has a time limit below the job's, and a warning says it failed, so the job stays
  green: how a caller reads a job that fails under job-level `continue-on-error` is not documented. With no
  `packages-artifact-id`, it packs the commit under test itself; a caller's packages come by the artifact's id, never
  by name, and never with an empty id, which downloads every artifact of the run.
- Every job of `heavy.yml` runs, its conditions on its steps: a called job skipped by its own `if` can skip the
  caller's jobs that need the call even when the rest succeeded (a GitHub bug), and `preview.yml` would publish
  nothing. `preview.yml`'s `publish` and `release.yml`'s `release` jobs check `needs.heavy.result` explicitly.
- A run's artifacts share one namespace with the workflows it calls: `ci.yml`'s `packages` and `test-results`,
  `heavy.yml`'s `native-aot`, `native-aot-marten`, `integration-test-results` and `compat-test-results`, uploaded with
  `overwrite: true` for **Re-run all jobs**. Another job's name used twice fails the second upload.
- No workflow but `ci.yml` names a job `build and test` (`preview.yml` and `release.yml` run theirs as `suites`): a
  ruleset matches a check by its name, and GitHub advises job names unique across workflows.
- After a red `build` or `heavy` job, **Re-run failed jobs** re-runs `build and test` with it; after a cancelled run,
  re-run all jobs or push again. A re-run of a green run keeps the previous `build and test` until the jobs it needs
  end.
- `scripts/ecmascript-patterns.mjs`, run by `ci.yml`'s `documentation site` job, holds the Blazor package's pattern
  table (`tests/AdCodicem.ValueObjects.UnitTests/Forms/EcmaScriptPatterns.json`) to Node's v-flag engine, as
  `EcmaScriptPatternTests` holds it to .NET: change both together.
- `deploy-docs.yml` is `workflow_call` only, called by `preview.yml` and `release.yml`: redeploy the site by dispatching
  `preview.yml`.
