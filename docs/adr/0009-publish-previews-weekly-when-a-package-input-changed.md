# 9. Publish a preview weekly, only when a package input changed

Date: 2026-10-03

## Status

Accepted. Supersedes the preview track of [ADR-0003](0003-hybrid-release-manual-stable-continuous-preview.md);
amends [ADR-0004](0004-pin-the-supply-chain-by-digest-not-nuget-lock-files.md) and
[ADR-0005](0005-version-the-documentation-site.md).

## Context

[ADR-0003](0003-hybrid-release-manual-stable-continuous-preview.md) made every merge to `main` publish a preview:
`ci.yml` packed the whole solution at MinVer's version and pushed every package with `--skip-duplicate`. That record
accepted the churn on nuget.org as the price of trying a change before it is permanent. Measured over the 32 previews
published from 2026-09-15 to 2026-10-03, the price was higher than churn:

- **Volume.** Twelve packages each: 384 package versions in under three weeks, on a registry that keeps every one of
  them for good. A package can be delisted, never unpublished.
- **Most of them changed nothing.** Compared entry by entry with the version before it, 20 of the 32 were the same
  packages under a new number: a merge of documentation, tests or CI published twelve versions anyway. The
  generator's assembly, which no two builds produce bit for bit alike, was compared by what it contains.
- **The numbers misled.** MinVer names the next patch. The latest preview was `0.2.2-preview.0.172` while the
  commits since `v0.2.1` held features and the next release was `0.3.0`, so the number said nothing about what the
  preview led to.
- **A set could stay incomplete.** nuget.org has no transaction: a push of twelve packages is twelve requests, and
  a run that stops halfway leaves the version on some of them only. `0.1.0-preview.1` exists for seven of the twelve
  IDs, though all twelve projects existed at that commit, and nothing ever completed it.
- **No provenance for a preview**, and for a stable release only the `.nupkg` and `.snupkg` files attached to the
  GitHub Release. nuget.org adds its repository signature to every package it accepts, which changes the file's
  digest, so a package restored from nuget.org could not be checked against any attestation.
- **The job that published built everything.** It restored the whole solution, test projects, Testcontainers,
  benchmarks and sample included: 145 packages and 932 MB, where the packable projects need 42 and 226 MB.

`ci.yml` also redeployed the site after each preview, and its call to `deploy-docs.yml` was the only pull-request
check that workflow had: GitHub validates a called workflow when the caller runs
([ADR-0005](0005-version-the-documentation-site.md)).

## Decision

We will publish previews from a workflow of their own, `preview.yml`, every week and on demand, and only when
something a package is built from has changed since the version nuget.org has from the nearest commit. The packages
move together: all twelve at one version, or none. `ci.yml` publishes nothing and deploys nothing any more.

### When

On a schedule, Monday at 07:15 in Paris (`timezone: Europe/Paris`, so summer time does not move it; not on the hour,
when scheduled runs are most often delayed), and on `workflow_dispatch`. From `main` only: the first job stops a
dispatch from any other branch, and the `nuget` environment deploys from `main` alone. One run at a time
(`concurrency: preview`, `queue: max`), never cancelled, since a push stopped halfway is exactly the partial set the
next run would have to complete, and no waiting run dropped for a later one: by default a dispatch from another branch,
or a re-run of an older run, would replace a run waiting on `main`. A run that is out of date when its turn comes stops
at the plan's ancestry check or at the publish job's recheck. There is no input to force a publish.

### The jobs

1. **compute the version** (`version`) installs semantic-release's npm packages, without install scripts, and runs
   `.github/scripts/next-version.mjs`. Its only outputs are two strings, the version and the release type. None of
   the jobs that decide, test, pack or push runs third-party JavaScript, so none of it shares a machine with the
   decision of which commit is tested, packed and pushed.
2. **decide what to publish** (`plan`) runs `.github/scripts/preview-gate.sh`, which needs no npm. It takes the
   release type only as major, minor or patch, and computes the version again from it, the last stable tag and the
   commits since: that job can choose between those three, never the number.
3. **build and test** (`test`) runs the three suites on the commit the plan chose, while **pack** (`pack`) packs
   it. Both first check that the commit is the one the run started on or an ancestor of it.
4. **publish to nuget.org** (`publish`), in the `nuget` environment, verifies, attests and pushes.
5. **publish preview documentation** (`docs`) redeploys the site.

### What counts as a package input

`preview-gate.sh` holds the list:

- `src/` except its `AnalyzerReleases` files, the solution filter the pack goes through included, `README.md`,
  `icon.png`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Build.rsp`, `global.json`, `nuget.config`
  in any case, `.gitattributes`, and `.github/scripts/release-pack.sh`, which holds the pack command line;
- `Directory.Packages.props`, filtered. A change outside its `<PackageVersion>` elements counts. A `<PackageVersion>`
  change counts only for a package in the restore graph of the projects under `src/`: what they reference, what that
  pulls in, and the build-time packages, MinVer, PolySharp, Roslyn and SourceLink. A bump of a test, benchmark or
  sample package publishes nothing.

Nothing else reaches a package: not `docs/`, `website/`, `tests/` (the compatibility island included), `samples/`
or `benchmarks/`; not the workflows; not `LICENSE`, which the package replaces with a license expression; not
`.editorconfig` or the analyzer release-tracking files under `src/`, which change diagnostics only; not the solution,
which selects projects and changes nothing in them. A file that comes to change what a package carries has to join
the list.

### Publish, repair or none

The gate decides from nuget.org, not from the previous run of the workflow: a run can stop after part of its push, a
stable release publishes too, and nuget.org is the one record that survives both. Its **base** is, of the versions
nuget.org has from the last stable tag merged into `HEAD` on, across every package ID, the one packed from the
commit nearest to `HEAD`, read off the `<repository commit="…"/>` SourceLink writes into each nuspec. Not the highest
in SemVer order: a feature reverted after its preview shipped leaves that preview above every later version, and
every later commit would differ from it.

- **repair**: a package the base commit packed lacks the base version, so a push stopped midway. The run packs the
  base commit again at the base version, tests included, and pushes only the IDs nuget.org lacks, before anything
  newer is published. Whatever `HEAD` changed since waits for the next run.
- **publish**: a package input changed between the base commit and `HEAD`.
- **none**: nothing a package is built from changed.

Every lookup that fails fails the run rather than guessing: a guess that publishes adds a version to nuget.org for
good, and a failed run publishes nothing and shows in the Actions tab. That includes a stable tag from which nuget.org
has no version at all, which means the release's push failed (semantic-release tags before it publishes) or that
nuget.org has not listed it yet.

### The version

`next-version.mjs` computes the version semantic-release would give the next stable release of `HEAD`, without a
token: it runs the commit analyzer semantic-release itself loads, at the version `package-lock.json` pins, with the
analyzer entry of `.releaserc.json` read at that commit, over the commits since the last stable tag. The preview is
that version suffixed `-preview.<N>`, `N` counting the commits since that tag, so `0.3.0-preview.172` leads to
`0.3.0`; with no commit that would release anything, the next patch. Over the 217 commits from `v0.1.0` to `main`,
and 21 made-up histories, it agrees with semantic-release's own core. MinVer receives the version through
`MINVERVERSIONOVERRIDE`, as in a stable release.

The plan does not take that version on trust, since the job that computed it ran third-party JavaScript: it computes
it again from the release type, the last stable tag and the commits since, and fails on any other. While
`.releaserc.json` rates a breaking change a minor, the analyzer cannot answer major, and the plan refuses one. A
version nuget.org already has fails the plan: it was packed from another commit. A version below the newest one on
nuget.org, after a revert, is published with a warning.

### All or nothing

- **Checked twice before the first push.** `verify-packages.sh` runs in the pack job and again in the publish job:
  one version of the `X.Y.Z-preview.N` shape, in every file name and every nuspec, and exactly the packable projects
  under `src/`, nothing more. An empty `MINVERVERSIONOVERRIDE`, on which MinVer silently falls back to its own
  version, stops there.
- **Pushed in dependency order.** `push-packages.sh` orders the set by the dependencies between its packages, and
  stops a pass at its first failure, so whatever a stopped push leaves on nuget.org can be restored: no package is
  published before one it depends on. Each pass asks nuget.org which IDs carry the version and pushes only the others;
  three passes, the second after a minute, the third after two.
- **Completed at the same version.** By those passes within the job; by **Re-run failed jobs**, which pushes the same
  bytes, already tested and attested; or by the next run, in repair mode.
- **Listed before the job ends.** After the push the job waits, up to 30 minutes, until nuget.org lists every package
  and serves every symbol package, so that the next run, the documentation label and `release.yml`'s check all read
  a nuget.org that has caught up.
- **A missing symbol package fails the job** rather than becoming a repair. "Re-run failed jobs" pushes it. A
  `.snupkg` that nuget.org's validation rejects would otherwise be repaired, and rejected, every week, and hold back
  every preview of `HEAD`.

### What reaches the push

- **Only the packable projects.** The pack job restores, builds and packs `src/AdCodicem.ValueObjects.Packages.slnf`,
  a solution filter over the projects under `src/`: no test project, no Testcontainers, no Codecov. `release-pack.sh`
  packs previews and stable releases alike, and holds each package to its own API baseline: a package nuget.org lacks
  at the last release, one added since, is packed without one instead of failing the pack.
- **The bytes the pack job produced.** The pack job runs beside the test job, which runs third-party code, and any
  job of a run can delete an artifact by name and upload another. So the publish job downloads the artifact by the
  id the pack job's upload returned, and checks every file against the list of SHA-256 digests the pack job wrote as a
  job output, which no other job can write.
- **A plan that is still current.** "Re-run failed jobs" on an old run reuses its plan. Before logging in, the
  publish job runs `preview-gate.sh --recheck`: if nuget.org now gives another base than the plan saw, because a
  later run published since, it stops rather than push a stale version. A version this very run left half
  published passes.

### Provenance

Every `.nupkg`, every `.snupkg` and every assembly inside them is a subject of a SLSA build provenance attestation,
signed through Sigstore with the job's OIDC token. A preview is attested in its publish job, before the push, so that
nothing reaches nuget.org unattested; a stable release in `release.yml`'s attest provenance job, which also attaches
the bundle to the GitHub Release. nuget.org re-signs each `.nupkg` but leaves the files inside as they were packed,
and NuGet extracts them unchanged, so an assembly restored from nuget.org verifies with `gh attestation verify`
(`SECURITY.md`). Versions published before this decision carry no attestation for their assemblies. A repair is
the one case where the attestation does not name the commit a file was built from (see the consequences below).

### The documentation

- Every run that decided redeploys `/docs/preview/`, whether it published, repaired or found nothing to do, labelled
  with the version on nuget.org that describes the commit it builds: the version just published, or the base. A
  repair that leaves `HEAD` with package changes nuget.org does not have yet builds the base commit instead. A run
  in which any job failed, or was cancelled, deploys nothing, so the site never describes a `main` nobody can
  install.
- `deploy-docs.yml` is called only: its `ref` and `preview-version` inputs are required, and it has no dispatch and
  no fallback that would look the label up. Dispatching `preview.yml` is how the site is redeployed by hand.
- Pull requests lose no check in the move. `lint.yml`'s `workflows` job runs actionlint, with shellcheck, over every
  workflow, `preview.yml`, `release.yml` and `deploy-docs.yml` included, which never run on a pull request. The
  **documentation site** job of `ci.yml` builds the site, without deploying it, through `.github/actions/build-site`,
  the composite action `deploy-docs.yml` builds with too, so a broken link fails the pull request rather than the
  next Monday.

### `release.yml`

- The three suites run in a job of their own.
- Nothing is built in the job that holds the credentials. A **pack** job, with no environment and no secret, computes
  the version with `next-version.mjs`, packs `src/AdCodicem.ValueObjects.Packages.slnf` through `release-pack.sh`, as
  `preview.yml`'s pack job does, freezes the documentation through `docs-snapshot.sh`, which loads the same projects
  through MSBuild for DocFX, and uploads both, with their SHA-256 digests as job outputs. It holds nothing to
  approve, so a release still waits for a single approval. The release job, in `nuget-stable`, checks the files
  against the digests, puts the snapshot in place, and runs semantic-release, whose prepare step only checks that the
  version it computed is the one packed; it then pushes through `push-packages.sh`. Its own token only reads: every
  write goes through the release App's token, which the checkout no longer leaves in `.git/config`. npm installs
  without install scripts in every job.
- A **was it previewed** job runs `preview-gate.sh --report` and warns, on the run page the `nuget-stable` reviewer
  reads, when the release ships package inputs no version on nuget.org carries, or when the version it compares with
  reached only some of the packages. It never blocks: the release job waits for it but runs whenever the tests and
  the pack passed.
- The attest provenance job adds the assemblies to the subjects, and the docs job labels the preview with the
  version just released.

Dependabot's `nuget`, `npm` and `github-actions` entries propose a version only once it has been public for seven
days, and the `pip` entry after Dependabot's default of three, which amends
[ADR-0004](0004-pin-the-supply-chain-by-digest-not-nuget-lock-files.md); security updates are not delayed.

### Rejected

- **A preview per merge, gated on the same inputs.** Replayed over the pushes since 2026-09-15, the gate alone takes
  32 previews down to 13 and misses none of the 11 whose contents changed; on the weekly schedule, 3. Each preview is
  permanent, and a change landing over several pull requests would publish every intermediate state. A dispatch gives
  a preview at once when one is wanted.
- **A nightly preview behind an approval.** A required reviewer on `nuget` would make every night a click, and an
  approval given every day stops being a decision. The approval stays where it decides something: the stable release.
- **A feed of its own for previews**, keeping nuget.org to stable releases. Azure Artifacts serves a public feed only
  from a public project, which Azure DevOps has
  [retired](https://learn.microsoft.com/azure/devops/organizations/projects/public-projects-retirement): existing ones
  turn private in 2027. GitHub Packages asks for a token to restore even a public package. Trying a preview would
  stop being a `dotnet add package --prerelease`.
- **`semantic-release --dry-run` for the version.** Even in dry-run it checks that it may push, and
  `@semantic-release/github` wants a token: a job that only reads the repository passes neither.
- **MinVer's numbering**, which names the next patch whatever the commits say.
- **Removing the credentials from the environment of the build, in the job that holds them** (`env -u`). Any process
  reads the environment of the processes above it through `/proc`, so the build would still reach the NuGet key, the
  App token and the variables that mint an OIDC token, and the runner's passwordless `sudo` reaches the memory that
  holds the App's private key. A pack job of its own closes it, and needs no second approval: it has no environment.
- **Comparing the packed bytes with nuget.org's.** The generator's assembly differs between any two builds, and the
  decision would need a pack before it could be made.
- **A glob push with `--skip-duplicate`.** It skips the symbol package of every package it skips, and pushes in file
  order, which puts the main package before the one it depends on.
- **A path filter on `ci.yml`.** It sees one push's changes, not what nuget.org lacks, and a failed run is never
  decided again.
- **The Traversal SDK, or a loop over the projects**, to pack `src/` only. A solution filter does it with the SDK
  alone; it cannot glob, and a check in `ci.yml` catches a project left out of it.
- **A force input, or unlisting a preview.** The first would publish what the gate refused; the second hides a
  version that stays installable by number.
- **A repair of missing symbol packages decided by the gate**, for the weekly loop described above.
- **A label looked up on nuget.org when the site is redeployed by hand.** The highest version there does not describe
  the commit built after a revert, nor while `main` has unpublished package inputs.
- **Moving `N` on to the next free number on a collision.** `N` as the height guarantees that a commit always gets
  the same version, which is what lets a later run complete a version an earlier one left half published.

## Consequences

At most one preview a week, plus dispatches. A change that ships waits up to a week for its preview unless someone
dispatches one, and a documentation change reaches `/docs/preview/` on the same schedule. The first run after this
lands publishes `0.3.0-preview.<N>`, since this change touches `src/Directory.Build.props`, `README.md`,
`release-pack.sh` and the solution filter it moves under `src/`.

A stable release wants a green preview first: dispatch `preview.yml` when package inputs changed since the last one,
and start `release.yml` once that run is green, so that its listing wait has passed and the was-it-previewed check
reads a nuget.org that has caught up. Right after a release, until nuget.org lists it, the plan fails closed;
dispatching again once it does is the remedy.

nuget.org's Trusted Publishing policies name a workflow file and an environment: `preview.yml` with `nuget`,
`release.yml` with `nuget-stable`. Renaming either one breaks the OIDC exchange until the policy follows.

A range holding only fixes is validated against the last stable release's API, so a breaking change typed `fix`
fails the preview's pack. That is on purpose.

After a revert the next preview can be lower than one already published, and NuGet offers the higher one as the
latest prerelease until a higher version ships. A preview left above a later, lower stable release can hold the very
number the next preview computes; the plan then fails until the next commit on `main` moves the number.

A repair packs again, and the generator's assembly differs between any two builds, so the main package of a repair
does not have the digest of the copy nuget.org may already hold. Only the missing IDs are pushed, so nothing on
nuget.org changes; the repair's attestation describes its own bytes, and names the commit the repair run started on,
the head of `main`, not the base commit it packed, which the nuspec and the assemblies' informational version name.
The run that first packed that version attested every file before its push, so an assembly that builds identically
from the base commit also carries an attestation naming the base; the generator's assembly never does.

GitHub disables the schedule of a public repository after 60 days without activity, and notifies a scheduled run's
failure to whoever last changed its cron line. `docs/maintaining.md` has both remedies.

What remains open:

- **What still runs beside the stable credentials.** semantic-release's own npm packages and `dotnet nuget push` run
  in the job that holds the NuGet key, the App token and the OIDC token; that is inherent to running semantic-release
  there. A compromised build dependency can no longer steal a credential, but it can still change what the pack job
  packs, which is then attested as built.
- **A stable push that fails after tagging.** semantic-release tags and pushes the changelog before it publishes. If
  `push-packages.sh` gives up after its three passes, no workflow completes the release; the gate then refuses every
  preview ("complete it by hand") until it is completed by hand from the run's `release-packages` artifact, as a
  release without a provenance attestation.
- **nuget.org's listing lag.** During an indexing incident the 30-minute wait can fail a job whose packages are
  published, and the site is then not redeployed. Re-running the job, or the next run, puts it right.
