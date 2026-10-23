# Maintaining

One-time setup that lives in GitHub and nuget.org settings rather than in this repository, and the routines the
publishing workflows leave to a person. Several workflows here succeed while doing nothing until the matching switch
is on, so this list is worth checking if something looks wired up but never happens.

## Publishing a preview

`preview.yml` publishes a preview of every package to nuget.org every Monday at 07:15, Paris time, and whenever it is
dispatched (**Actions → preview → Run workflow**, from `main`: a dispatch from another branch stops at its first
job). It publishes only when a package input changed since the version nuget.org has from the nearest commit, and
then all thirty-eight packages at one version, or none.
[ADR-0009](adr/0009-publish-previews-weekly-when-a-package-input-changed.md) has the reasoning.

Each run says what it decided. The **compute the version** job prints the version semantic-release would give the
next release, and **decide what to publish** writes a summary, *Preview: publish*, *repair* or *none*, with the
reason and the package inputs that changed:

- **publish**: the tests run on `HEAD`, the pack job packs it at `<next release>-preview.<commits since the last stable
  tag>`, `heavy.yml` runs native AOT, Marten's and the integration suite on that commit, and the .NET 11 island over
  those packages, and the publish job attests and pushes it. Count about 25 minutes, Marten's native AOT job setting the
  time.
- **repair**: a previous push stopped midway, so nuget.org has the base version for some packages only. The run
  packs that commit again at that version, tests included, and pushes only the missing packages. Whatever `HEAD`
  changed since waits for the next run, so dispatch again once the repair is green. A version packed before the release
  trains (ADR 0012) cannot be repaired by a later run, whose pack step takes arguments that commit's scripts do not:
  the pack job fails and says so. Complete it with **Re-run failed jobs** on the publish job of the run that stopped
  midway, as below.
- **none**: nothing that ships changed. The tests, the pack and the push are skipped.

Whatever it decided, a run whose jobs all succeeded redeploys the site, labelled with the version on nuget.org that
describes the commit it builds. A run with a failed or cancelled job deploys nothing.

### When a run is red

- **A heavy job failed** (`heavy / native AOT`, `heavy / native AOT (Marten)`, `heavy / integration tests`): nothing was
  published. The same job on `main`, run by `heavy.yml` after the merge, has usually shown it first. Fix it on `main`
  and dispatch again; **Re-run failed jobs** helps only with a job that died before its tests ran. A **repair** run
  tests the base commit, whose code no fix on `main` changes: complete that version instead, with **Re-run failed jobs**
  on the publish job of the run that stopped midway, or by pushing the missing packages of its `packages-<version>`
  artifact by hand, dependencies first, then dispatch again. A failed test of `heavy / compat (.NET 11)`, or its .NET 11
  SDK failing to install, only informs until .NET 11 ships ([below](#the-net-compatibility-island)).
- **The publish job failed.** Use **Re-run failed jobs** on that run. It pushes the same bytes, already tested and
  attested, and only what nuget.org still lacks. If a newer run has published since, its recheck refuses ("something
  was published since"): dispatch `preview.yml` instead, which decides again.
- **The wait for nuget.org timed out.** The push succeeded, but nuget.org had not listed every package, or served
  every symbol package, within 30 minutes. Re-run the failed job once nuget.org has caught up; it pushes nothing
  that is already there.
- **A symbol package is still missing.** Re-run the failed job, which pushes it. If nuget.org's validation rejected
  it, and e-mailed the account to say so, a re-run cannot help: the version stays without symbols, which blocks
  nothing, since the gate does not look for them. Uploading the `.snupkg` from the run's `packages-<version>`
  artifact through nuget.org's upload page is the manual way.
- **The plan failed right after a stable release** ("v… is tagged, but nuget.org has no version of these packages
  from … on"). nuget.org has not listed the release yet: wait, and dispatch again.
- **The plan failed because the version "is already on nuget.org (…), packed from another commit".** A preview left
  above a later, lower stable release, after a revert, holds the number this one computed. The next commit on `main`
  moves the number.
- **The pack failed on the API baseline (`CP0001` and the like).** The commits since the last release are typed as
  fixes, so the preview is held to that release's API. A breaking change typed `fix` is the usual cause: it needs a
  `!`.

### The version

The version is the next release's, as semantic-release would compute it, suffixed `-preview.<N>`, where `N` counts
the commits since the last stable tag: `0.3.0-preview.172` is the 172nd commit after `v0.2.1`, leading to `0.3.0`.
After a feature is reverted the next preview can be lower than one already published; the plan warns, and NuGet keeps
offering the higher one as the latest prerelease until a higher version ships.

### Routines

- **After a merge**, `heavy.yml` runs on `main`: native AOT, Marten's, the integration suite and the .NET 11 island,
  which no pull request runs unless it is labelled `ci:heavy` or Dependabot opened it to update a NuGet package. A newer
  push cancels an older one's run, so a red run names the tip of the push it tested, which may be later than the commit
  that broke it; it also holds back the next preview, which runs the same jobs. Label a pull request `ci:heavy`, then
  push to it, when it touches native AOT, a store, the sample API or the island; the label is read when a run starts, so
  **Re-run jobs** does not see it.
- **On a pull request**, `ci.yml` runs what its `plan` job picks from the files it changes (ADR 0013,
  `.github/scripts/ci-plan.sh`): the unit tests of the package families those files reach, each family's project of
  the unit suite in a job of its own, the generator and RDG suites, the compiled model, the pack and the site only when
  their inputs change; the plan's summary on the run page lists what it picked and why. A push to `main` runs
  everything. A pull request adding a project adds it to `AdCodicem.ValueObjects.slnx`. One under `src/` also goes into
  a family's `projects` in `.github/ci-families.json`, and that family's project of the unit suite references it; a new
  family's project, `tests/AdCodicem.ValueObjects.UnitTests.<Name>`, imports `..\UnitTests.props`
  (`tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`) and goes into the family's `tests` and the `InternalsVisibleTo`
  entries of `src/Directory.Build.props`. Either then replaces `codecov.yml`'s flags between its `ci-families` markers
  with what `.github/scripts/ci-plan.sh --codecov-flags` prints, or the plan fails; the plan checks neither the import
  nor the `InternalsVisibleTo` entry, and the entry changes every package's bytes (`src/Directory.Build.props` is a
  package input), so the next preview publishes. A folder added to a project of the unit suite needs none of this.
- **Before a stable release**, dispatch `preview.yml` if package inputs changed since the last preview, and start the
  release once that run is green (see [Cutting a stable release](#cutting-a-stable-release)).
- **After merging a security update of a dependency the packages ship**, dispatch `preview.yml` rather than wait for
  Monday, and consider a release: previews receive no fixes of their own (`SECURITY.md`). A release is possible only
  if the update landed as `fix(deps)` ([Dependabot](#dependabot)): a `chore(deps)` commit gives semantic-release
  nothing to release.
- **When an advisory is published on a package of the NuGet graph**, every restore turns red once nuget.org's audit
  data carries it, on `main`, on every pull request that builds and in `preview.yml`, and in the island for a stable
  package it takes, but not for a release candidate's build ([below](#the-net-compatibility-island)): NuGetAudit reads
  every package of the graph, transitive ones included, from level `low`, and its warnings (`NU1901` to `NU1904`) are
  errors here, as every warning is (`TreatWarningsAsErrors`). nuget.org updates those files a few times a day at most,
  after GitHub's advisory database, so the Dependabot alerts and the push message show the advisory first: fix it
  then, before restores fail ([Dependabot](#dependabot)).
- **On Microsoft's patch day**, the second Tuesday of the month, move the framework's patch by hand, in one commit:
  every Microsoft `PackageVersion` on the current patch, 10.0.13 today, `dotnet-ef` with them, the
  `Microsoft.Extensions.Options` and `Microsoft.Extensions.DependencyInjection.Abstractions` pins among them. The
  `dotnet` group cannot: Entity Framework Core's and SignalR's packages pull in the `Microsoft.Extensions.Options` of
  their own patch, which the pin holds back, so their update fails its restore with NU1109 ([Dependabot](#dependabot)).
  Type it `fix(deps)` when a shipped package's floor moves.
- **When an Orleans minor raises its Roslyn floors**, move the Analyzers pin with it by hand.
  `Microsoft.Orleans.Serialization` lists `Microsoft.CodeAnalysis.Analyzers`, which the central pin must reach (NU1109
  below it, and no pull request), and `Microsoft.CodeAnalysis.Common`, which no pin follows: no project reaching
  Orleans references `Microsoft.CodeAnalysis.CSharp`, whose 4.14.0 would refuse that Common (NU1608). Keep it so: a
  test of `tests/AdCodicem.ValueObjects.UnitTests.Orleans` compiling C# would need a `Microsoft.CodeAnalysis.CSharp`
  equal to Orleans' Common, through an entry conditioned on that project, which an Orleans minor would then move. The
  generator's `Microsoft.CodeAnalysis.CSharp` 4.14.0 stays where it is. The Analyzers pin is a dependency of the
  Orleans package, so the commit is `fix(deps)`.
- **When a WolverineFx.RuntimeCompilation update asks for a later Roslyn**, move the Wolverine.Http fixture
  application's two conditional `PackageVersion`s, `Microsoft.CodeAnalysis.CSharp` and `CSharp.Workspaces`, with it by
  hand: its JasperFx.RuntimeCompiler asks for both at its Roslyn exactly (NU1109 below it, and no pull request). Both
  entries are the fixture's alone, and the Hosts project of the unit suite references the fixture with
  `ReferenceOutputAssembly="false"`, which brings neither there. They reach no package, so the commit is
  `chore(deps)`. The island pins `WolverineFx.RuntimeCompilation` at the version of the package's `WolverineFx.Http`
  floor: move it with the floor.
- **After 60 days without activity in the repository**, GitHub disables the schedule of a public repository's
  workflow. Turn it back on with `gh workflow enable preview.yml`.
- **Failures of a scheduled run are notified to whoever last changed its cron line**, not to whoever dispatches it.
  If the run page names an actor other than you, push a commit of your own that touches the cron line, or watch the
  workflow.

### Checking a run's provenance

The first time `preview.yml` publishes, and after any change to its publish job, dispatch it rather than wait for
Monday, and check:

1. the thirty-eight packages are on nuget.org at the version the run printed, and the publish job finished its wait for
   the listing;
2. an assembly restored from nuget.org verifies, and the certificate comes from Sigstore's public-good instance:

   ```sh
   gh attestation verify ~/.nuget/packages/adcodicem.valueobjects.json/<version>/lib/net10.0/AdCodicem.ValueObjects.Json.dll \
     --repo AdCodicem/AdCodicem.ValueObjects \
     --signer-workflow AdCodicem/AdCodicem.ValueObjects/.github/workflows/preview.yml --format json
   ```

3. `/docs/preview/` reads `Preview (<version>)`.

Once the first publish from `preview.yml` has succeeded, delete the Trusted Publishing policy of `ci.yml` on
nuget.org (see [Secrets and external services](#secrets-and-external-services)). Check the next Monday's run too:
it should decide *none* if nothing changed, redeploy the site, and name you as its actor.

## Cutting a stable release

`v0.1.0` is the version baseline: a stable tag placed by hand on `main`, so semantic-release counts from `0.1.0`
instead of starting at `1.0.0`. It is not a release — no `0.1.0` package exists, and nuget.org then had only the two
previews. The reasoning and the measurements are in
[ADR-0003](adr/0003-hybrid-release-manual-stable-continuous-preview.md).

Before releasing, make sure what ships has been previewed: if package inputs changed since the last preview,
dispatch **preview** from `main` and wait until it is green. Its publish job ends only once nuget.org lists every
package, and nuget.org needs minutes for that, so a release started earlier would compare against a nuget.org that
has not caught up.

Then run **Actions → release → Run workflow** with `dry_run` ticked and read the version it computes before running
it for real. Publishing to nuget.org cannot be undone — a package can be delisted, never unpublished. The **was it
previewed** job warns on the run page when the release ships package inputs that no version on nuget.org carries;
it never blocks the release, and it runs before the release job so that its warning is there when the `nuget-stable`
reviewer approves. The **suites**, **pack** and **heavy** jobs must pass: the release job waits for all three. The
pack job, which holds no credential, packs only the packable projects under `src/` and freezes the documentation;
**heavy** runs `heavy.yml` on the same commit, native AOT, Marten's and the integration suite built from it and the
.NET 11 island over the packed packages, compat blocking the release whatever .NET 11 it targets, and is skipped on a
dry run; the release
job builds nothing, checks what the pack job produced against its digests, and pushes it. Count about 25 minutes
before the `nuget-stable` reviewer is asked: Marten's native AOT job sets that time.

The pack job freezes the documentation on every run, the dry run included. A real run commits
`website/versioned_docs/` along with the changelog, then redeploys the site from the new tag
([ADR-0005](adr/0005-version-the-documentation-site.md)). After the first real release, check four things on the
site:
- `/docs/` names the new line in the version selector, for example `0.2.x (0.2.0)`;
- the **Preview** button leads to `/docs/preview/`;
- the "no stable release yet" bar is gone;
- the release commit contains `website/versioned_docs/version-<line>/api/`.

If the pack or the snapshot fails, the pack job fails and the release job never starts, so nothing is published:
correct it and dispatch again. If the release is published but its **publish documentation** job fails, use **Re-run
failed jobs** on that run: it keeps the tag the release job computed. Dispatching **preview** from `main` works too:
it redeploys the site, labelled with the version on nuget.org, once nuget.org lists the release; before that, its
plan fails on purpose.

If semantic-release fails in its publish step, after it has tagged, nothing completes the release by itself.
semantic-release tags and pushes the changelog commit before it pushes the packages, and creates the GitHub Release
after them. `push-packages.sh` pushes in dependency order, with retries, so what reached nuget.org is a prefix of the
set that can be restored; the rest is in the run's `release-packages` artifact. Push the missing packages by hand, in
the order the log prints ("Push order: …"), then create the GitHub Release from the tag by hand, with that artifact's
`.nupkg` and `.snupkg` files as its assets. That release carries no provenance attestation and cannot get one. The
**attest provenance** job runs only when the release job reports a tag, which a failed semantic-release never does,
and a re-run releases nothing. Only a run of `release.yml` signs as `release.yml`, which is what the
`--signer-workflow` check in a release's notes requires. Say in its notes that it is not attested, rather than
copying the verification commands a release normally carries. Until the release is complete, `preview.yml` refuses
to publish ("complete it by hand").

The GitHub Release ends with a table linking each package to its version page on nuget.org, and carries the
`.nupkg` and `.snupkg` files as assets, signed by a SLSA build provenance attestation that the **attest
provenance** job adds (`AdCodicem.ValueObjects.<version>.sigstore.json`). Its subjects are the packages and every
assembly inside them, so a package restored from nuget.org, whose `.nupkg` nuget.org has re-signed, is checked
through its assemblies (`SECURITY.md`). That holds from `0.3.0-preview.<N>` and `0.3.0` on, the first versions
published since the assemblies became subjects; earlier releases attest at most their `.nupkg` and `.snupkg`, and
earlier previews nothing. After a release, check that its attestation covers the assemblies, with
`gh attestation verify` on a restored DLL and
`--signer-workflow AdCodicem/AdCodicem.ValueObjects/.github/workflows/release.yml`.

Releases are immutable on this repository, and a published release refuses any new asset, so semantic-release only
creates a **draft** (`draftRelease` in `.releaserc.json`): the attest provenance job attaches the bundle to it and
then publishes it. Until that job succeeds, the release exists only as a draft, even though nuget.org already has the
packages. If it fails, use **Re-run failed jobs**: dispatching the release again would find nothing to release and
skip it. Do not publish the draft by hand either, or the release freezes without its bundle. The table is not written
by hand: `.github/scripts/package-ids.sh` reads every packable project under `src/` in the pack job, which hands the
list to semantic-release, and `release-pack.sh` fails the pack job, before anything is pushed, if that list does not
match the packages it built. The links can answer 404 for a few minutes after the release, until nuget.org has
indexed the push. In the other direction, each package's release notes on nuget.org point to its GitHub Release, or
to `CHANGELOG.md` for a preview, which has no release of its own (`src/Directory.Build.props`).

## GitHub settings

| Setting | Where | Without it |
|---|---|---|
| Allow auto-merge | Settings → General → Pull Requests | `dependabot-auto-merge.yml` runs green and merges nothing. |
| Squash merging allowed, with the pull request title as its default commit message, beside rebase merging | Settings → General → Pull Requests | A Dependabot pull request retitled `fix(deps)` lands as Dependabot's `chore(deps)` commit and releases nothing. Rebase merging stays the way every other pull request lands. |
| Required status checks **`build and test`** (`ci.yml`), **`workflows`** and **`conventional commits`** (`lint.yml`), and no code coverage rule | Settings → Rules → Rulesets → `Default` (id 23842638) | Auto-merge can merge a red build, or one that breaks native AOT, and a red `fix(deps)` would then release a broken patch. The checks are job names: renaming any of these jobs leaves every pull request waiting for a check that never reports. **`build and test`** is `ci.yml`'s aggregator, which fails unless its plan and every job the plan expects succeeded: `format`, a `unit (<family>)` job per family the plan selects, the `build` job (the build, the generator and RDG suites, the compiled model and the pack the plan selects), `release tooling` when the release tooling changes, and `heavy.yml` on a pull request labelled `ci:heavy` or a Dependabot NuGet update ([ADR-0013](adr/0013-run-a-targeted-pull-request-ci-and-the-heavy-jobs-before-a-release.md)). A job that must block a merge goes into its `needs`, never into this list. The jobs of `heavy.yml` (`native AOT`, `native AOT (Marten)`, `integration tests`, `compat (.NET 11)`) never join them: they run after the merge, and gate every stable release, and every preview but compat, which gates previews once .NET 11 ships. No workflow reports coverage to GitHub, so a code coverage rule would measure nothing: Codecov's `codecov/patch` and `codecov/project` are the coverage signal ([ADR-0006](adr/0006-coverage-is-a-signal-not-a-goal.md)). |
| Allow GitHub Actions to create and approve pull requests | Settings → Actions → General | Not needed for auto-merge, but required if a workflow is ever made to open PRs. |
| Discussions | Settings → General → Features | `.github/DISCUSSION_TEMPLATE/q-a.yml` and the issue-template link to Discussions go nowhere. |
| Pages source: GitHub Actions | Settings → Pages | `deploy-docs.yml` uploads an artifact that is never served. |
| `github-pages` environment limited to `main` (the default) | Settings → Environments | Nothing breaks, but a deployment from another branch could replace the live site. Every deployment this repository makes runs from `main`: `preview.yml`, whose first job stops a dispatch from any other branch, and `release.yml`. `deploy-docs.yml` can no longer be dispatched, and `ci.yml`'s **documentation site** job, which builds the site on a pull request reaching it and on `main`, deploys nothing. |
| Immutable releases | Settings → General → Releases | Nothing breaks, but release assets and tags could be altered after publication. With it on, a published release takes no new asset, which is why `release.yml` publishes a draft only after the attest provenance job has attached its bundle; turning it off does not require undoing that. |
| Code scanning: **default setup**, left enabled | Settings → Code security → Code scanning | This repository uses CodeQL's default setup. There is deliberately no `codeql.yml`: an advanced configuration cannot upload its results while default setup is on — GitHub rejects the SARIF with *"CodeQL analyses from advanced configurations cannot be processed when the default setup is enabled"*. Only add a workflow if you first disable default setup, and only if you need something it cannot do (custom query packs, or a manual build for a solution autobuild cannot handle). |
| GitHub Sponsors | Account settings | `.github/FUNDING.yml` has no effect. |

`@semantic-release/git` pushes the changelog commit and the tag to `main`, which the ruleset on `main` rejects
(*"GH013: Changes must be made through a pull request"*). A ruleset's bypass list takes repository roles,
teams, GitHub Apps and deploy keys, but not the `GITHUB_TOKEN` a workflow runs with, so `release.yml` pushes
as a dedicated GitHub App instead:

1. Create a GitHub App (Settings → Developer settings → GitHub Apps → New), with no webhook, repository
   permissions **Contents**, **Issues** and **Pull requests** set to *Read and write*, installable on this
   account only. Generate a private key.
2. Install it on this repository only.
3. Add the App to the bypass list of the ruleset on `main` (Settings → Rules → Rulesets), mode *Always allow*.
4. On the `nuget-stable` environment, add the variable `RELEASE_APP_CLIENT_ID` (the App's Client ID) and the
   secret `RELEASE_APP_PRIVATE_KEY` (the whole `.pem`). Keeping them on the environment rather than the
   repository means the key is only readable once the required reviewer has approved the run.

Without them the release job stops at its first step, before anything is published.

## Dependabot

`.github/dependabot.yml` proposes updates weekly. What it does differently from its defaults:

- **A week of cooldown.** A version is proposed once it has been public for seven days, on the `nuget`,
  both `npm` and the `github-actions` entries, so that a broken or compromised release has time to be pulled first.
  Security updates ignore the cooldown and open at once.
- **No framework major.** Semver-major updates of `Microsoft.EntityFrameworkCore*`, `Microsoft.AspNetCore.*`,
  `Microsoft.Extensions.*`, `Microsoft.OpenApi`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `HotChocolate*`,
  `Microsoft.Orleans.*` and `NJsonSchema*`, whose packages share one version and move together, in the `dotnet` group
  or, for Microsoft's patch, by hand ([Routines](#routines)), are ignored, and so are those of `System.CommandLine`,
  whose 3.0 ships with .NET 11 and runs the package built against 2.0 (the island runs it), of `Marten`, whose next
  major is a floor to decide, since the package reaches one of its internals, of `WolverineFx*`, whose packages share
  one version and whose next major is a floor to decide, since the Wolverine.Http package reaches three of its
  internals, and of five packages whose majors follow
  one of them: `dotnet-ef`, the local tool pinned to EF Core's version; `NSwag.*`, which the tests take and whose majors
  move with NJsonSchema's; `Swashbuckle.AspNetCore*`, whose next major moves to `Microsoft.OpenApi` 3;
  `Serilog.Extensions.Logging`, which only the tests use and whose majors follow .NET's, each built on the
  `Microsoft.Extensions.Logging` of its major; and `System.Security.Cryptography.Pkcs`, a test-only pin on .NET's
  version line whose next major targets `net10.0` too, removed once `Microsoft.Data.SqlClient` asks for a patched
  version. The same goes for every other dependency `.github/shipped-dependencies` lists, test-only packages of the
  same families moving with them (`xunit.v3*`, `protobuf-net*`, `Microsoft.Azure.Functions.Worker*`,
  `ModelContextProtocol*`), and for the minors of `Microsoft.CodeAnalysis.CSharp*` too: its `major.minor` is the
  oldest compiler that loads the generator, which a minor raises as a major does (#190). Dependabot no longer proposes
  those majors: `held-back-majors.yml` lists them every Monday, before the preview, in one open issue titled
  "Dependency majors Dependabot holds back", against the newest stable version nuget.org or npm lists, and closes it
  once nothing is held back. Its Roslyn rows (4.14.0, and the Wolverine.Http fixture's 5.0.0) stand by design. A new
  major is supported by a decision, a dependency floor or a target framework
  ([ADR-0010](adr/0010-version-every-package-in-lockstep-independently-of-dotnet.md)), never by a bump. Dependabot's
  NuGet updater applies these rules to security updates too: a security fix that exists only on a new major of one of
  them opens no pull request, and its alert stays open. Treat it as a deliberate migration under ADR-0010. A fix on the
  current major opens at once.
- **Families in groups of their own.** `xunit.v3*`, `Microsoft.CodeAnalysis*`, `HotChocolate*` and
  `ModelContextProtocol*` require one another at exactly one version, so a bump of some of them without the rest fails
  the restore with NU1608 (#189). Each family's minor and patch updates come in a pull request of their own, ahead of
  the `dotnet` group, so that a red family holds back no other update. A group does not make Dependabot update every
  member: if one is left behind, bump the family by hand, in one commit, and check it with `dotnet restore`.
- **`fix(deps)` for what ships.** Dependabot takes one `nuget` entry per directory, so every NuGet pull request opens
  as `chore(deps)`, unless Dependabot groups it, as #166, which then carries no prefix and has to be retitled by hand
  (the grouped security update below). `dependabot-auto-merge.yml` retitles it `fix(deps): …` when it updates a
  dependency listed in
  `.github/shipped-dependencies`, and `fix(deps)!: …` when that update is a new major, so that merging it releases a
  patch, or a breaking change. The list holds the dependencies the nuspecs name, plus `Microsoft.CodeAnalysis.CSharp`
  and `PolySharp`, compiled into the generator; a step of `ci.yml` fails when it no longer matches the packed
  nuspecs. That workflow runs on `pull_request_target` and only ever checks out the base branch, sparsely, never the
  pull request's code. A grouped pull request that mixes shipped and test-only dependencies becomes one `fix(deps)`.
- **A security update that pins a new transitive dependency.** Dependabot can fix a vulnerable transitive dependency
  of a shipped package by adding a `<PackageVersion>` to `Directory.Packages.props`, which transitive pinning makes a
  dependency of the package. If `.github/shipped-dependencies` does not name it yet, the pull request opens as
  `chore(deps)` with auto-merge queued, unless Dependabot groups it, as #166, which then carries no prefix, and
  the `build` job, and so **build and test**, fails on the list. `dependabot-auto-merge.yml` reads
  the list from the base branch and ignores events you cause, so it cannot retitle the pull request. In this order:
  retitle it yourself, `fix(deps): …`, or `fix(deps)!: …` when it crosses a major; then push the list line to its
  branch. The title you set holds, and the auto-merge still queued squashes the pull request under it once CI is
  green. Do not merge the list line in a pull request of its own: nothing on `main` ships that ID yet, so the check
  fails there too. Closing the Dependabot pull request and landing the pin and the line together in a
  `fix(deps): pin <id> to <version>` of your own works as well.
- **An update that needs a transitive pin moved.** `Microsoft.Extensions.Options` and
  `Microsoft.Extensions.DependencyInjection.Abstractions` are pinned in `Directory.Packages.props` and bumped by hand,
  as the list below says. When the new version of a package asks for a later version of either, the update, a security
  update or the `dotnet` group's patch alike, fails its restore with NU1109 ("Detected package downgrade … to centrally
  defined …"), Dependabot logs it and opens no pull request for it, and a security alert stays open. That is how the
  .NET advisories of 2026-10-13 went: SignalR's MessagePack hub protocol 10.0.13 brings
  `Microsoft.AspNetCore.SignalR.Common` 10.0.13, which asks for `Microsoft.Extensions.Options` 10.0.13; Entity Framework
  Core 10.0.13 reaches it through `Microsoft.Extensions.Logging` and `Microsoft.Extensions.Caching.Memory` 10.0.13. Move
  the pins with it by hand, with the rest of the framework's patch so that the Microsoft packages stay on one version
  ([Routines](#routines)), in a `fix(deps)` commit of your own when a shipped package's floor moves.
- **A grouped security update can open with no prefix.** Dependabot's NuGet security run of 2026-10-14 opened its fix as
  one pull request titled after a group, "Bump the nuget group with 1 update", without the `chore(deps)` prefix (#166,
  the `System.Security.Cryptography.Pkcs` pin), while the site's npm security updates have opened one per package, with
  their prefix (#145, `chore(deps): Bump http-cache-semantics from 4.2.0 to 4.3.0 in /website`). Such a pull request
  fails its **conventional commits** check, and a squash would put its title on `main`. `dependabot-auto-merge.yml`
  replaces only a title that starts with `chore(deps): `, so it neither retitles one that updates a shipped dependency
  `fix(deps)`, which would then land under the group's title and release nothing, nor gives one that does not a prefix
  at all; it still queues auto-merge, which waits on that red check, one the `Default` ruleset requires, so the pull
  request never merges by itself. Retitle it by hand, `fix(deps): …` or `chore(deps): …` as
  `.github/shipped-dependencies` decides, since its title becomes the squashed commit, and squash it through the bypass.
- **Audit warnings stay errors.** An advisory published anywhere in the graph breaks this build overnight
  ([Routines](#routines)), and that red build is the earliest signal, so `NU1901` to `NU1904` are deliberately not in
  `WarningsNotAsErrors`, which would leave the alerts as the only one. An advisory that has no fix, or that cannot
  reach this repository's use of the package, is silenced one at a time, with
  `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-…" />` in `Directory.Build.props`, or the island's
  own, and a comment saying why and when to remove it: the documented, reviewable way.
- **Alerts GitHub dismisses on its own.** Every package of the root `package-lock.json`, the release tooling, is
  development-scoped, and GitHub's preset rule "Dismiss low impact issues for development-scoped dependencies", on by
  default for a public repository, dismisses the alerts on them whose weaknesses all lie in its list. The push
  message's count leaves those out; the alerts page lists them as dismissed.
- **Squash, always.** Auto-merge squashes minor and patch updates. Merge a major by hand, after reading it, and
  squash it too: a rebase merge keeps Dependabot's `chore(deps)` commit and releases nothing. A `fix(deps)!` releases
  a minor while the version is 0.x. Auto-merge is queued once, when Dependabot opens the pull request: **Disable
  auto-merge** on it holds it, through Dependabot's later rebases, until you merge it yourself.
- **protobuf-net and the native AOT job.** The application of `tests/NativeAot/AdCodicem.ValueObjects.NativeAot.ProtobufNet`
  publishes the model protobuf-net's generator writes, which warns at each call into a serializer; the job lets through
  the warnings its `aot-warnings.allowed` lists, one extended regular expression per line, and fails on any other. A
  protobuf-net update whose generated code or annotations warn otherwise fails the job, naming the new warning: read
  it, and move the list with the update when the warning is protobuf-net's own, never one of this library's code.
- **Marten and the native AOT (Marten) job.** The application of
  `tests/NativeAot/AdCodicem.ValueObjects.NativeAot.Marten` runs in a job of its own, `native AOT (Marten)`, against its
  PostgreSQL service container, beside `native AOT`, which it would take from about 6 minutes to about 27: the native
  AOT compiler alone takes about 20 minutes over Marten, on one core. Both are jobs of `heavy.yml`, which run on every
  Dependabot NuGet pull request and are never required checks
  ([ADR-0013](adr/0013-run-a-targeted-pull-request-ci-and-the-heavy-jobs-before-a-release.md)). Keep its timeout ahead
  of Marten's compile time (45 minutes today). Marten, JasperFx, Weasel and FastExpressionCompiler warn for themselves,
  which its `aot-warnings.allowed` lists by their source paths and assemblies: a Marten update that warns otherwise
  fails the job, naming the warning; move the list with the update when the warning is Marten's own, never one of this
  library's code.
- **Marten's internals.** The Marten package reaches Marten's internal list of value types through `[UnsafeAccessor]`,
  keeps every value object out of it so that Marten refuses a compiled query taking one, lists its nullable type for the
  duplicated field, which finds a value type by the type its member reports, and binds a compiled query's collections
  through Marten's planning steps; `MartenInternalsTests`, in `Persistence/` of
  `tests/AdCodicem.ValueObjects.UnitTests.Data`, pins each. A Marten update failing it moved one of them: adapt the
  package in the same pull request (`src/AdCodicem.ValueObjects.Marten/CLAUDE.md`), never silence the test. The same
  class pins the member types Marten binds in a compiled query and its refusal of an identity's `Value`, which
  `VO0048`'s message rests on: when one of those fails, adapt the message in
  `src/AdCodicem.ValueObjects.Generators/Analyzers/MartenCompiledQueryAnalyzer.cs` and the forms the Marten guide, the
  diagnostics reference and the skill list.
- **Wolverine and the native AOT job.** The main application of `tests/NativeAot` serves Wolverine.Http endpoints in
  Wolverine's static mode, over the code `native-aot.sh` has `codegen write` write into `artifacts/wolverine-codegen`
  before both runs, never committed. A Wolverine update that changes what that code needs shows there: in the codegen
  pass, in the publish's warnings, or as a difference between the runs. Wolverine.Http's minor and patch updates come
  weekly in the `dotnet` group, and each moves the package's floor, so its pull request is `fix(deps)`: the maintainer
  chose this on 2026-10-18, the repository's rule for every shipped dependency, over ignoring WolverineFx's minors and
  moving the floor by hand. Revisit it if the churn weighs.
- **Wolverine's internals.** The Wolverine.Http package sets `HttpChain.IsFormData` and `RequestType` through
  `[UnsafeAccessor]`, as Wolverine's own `[FromForm]` binding does, takes over the variables Wolverine's factories create
  through `HttpElementVariable.ReassignCreator`, as Wolverine's own `[AsParameters]` binding does, and relies on Wolverine
  describing ApiExplorer's parameters and looking a value up again by name from those variables' lists;
  `WolverineHttpStrategyTests`, `WolverineHttpCodeTests` and `WolverineHttpDocumentTests` pin them. A Wolverine update
  failing them moved one: adapt the package in the same pull request
  (`src/AdCodicem.ValueObjects.Wolverine.Http/CLAUDE.md`), never silence the test.
- **Microsoft.AspNetCore.Components and the native AOT job.** The main application of `tests/NativeAot` renders Blazor
  components, and Components warns for itself at assembly level, `IL2104` and `IL3053`, which that application's
  `aot-warnings.allowed` lists. Those warnings come from the runtime pack the SDK resolves, so an SDK or ASP.NET Core
  patch that makes Components, Microsoft.JSInterop or another framework assembly warn otherwise fails the job, naming
  the warning: add the line when the warning is the framework's own, never one of this library's assemblies.

Some versions are bumped by hand, because Dependabot never sees them:

- the compatibility island under `tests/Compat` ([below](#the-net-compatibility-island));
- `Microsoft.OpenApi`, `Microsoft.Extensions.Options` and `Microsoft.Extensions.DependencyInjection.Abstractions`,
  which `Directory.Packages.props` pins for the packages that pull them transitively;
- actionlint, which `lint.yml` downloads as a binary: change `ACTIONLINT_VERSION` and `ACTIONLINT_SHA256` together,
  taking the digest of `actionlint_<version>_linux_amd64.tar.gz` from the release's checksums file.
- the Azurite image the integration suite's Functions fixture names at an exact version
  (`tests/AdCodicem.ValueObjects.IntegrationTests/Fixtures/FunctionsHostFixture.cs`). The Functions host's image,
  `mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0`, is a moving tag, as `mongo:7.0` is, and
  moves to the next .NET's tag with the target framework.
- `mongo:7.0`, which the integration suite and the compatibility island run in place of `mongo:8.0`: every MongoDB 8.x
  refuses to start on a Linux kernel 6.19 or later ([SERVER-121912](https://jira.mongodb.org/browse/SERVER-121912)),
  which GitHub's runners run since their image of 2026-10-18. Once an 8.x starts there, move both fixtures back to
  `mongo:8.0` and the support tables of `README.md` and `website/docs/packages.md` back to MongoDB 8, removing their
  footnote.

## Supply chain and the OpenSSF scorecard

`scorecards.yml` publishes to the OpenSSF API weekly, which is what backs the README badge. Most of what it
measures is settled in this repository — see
[ADR-0004](adr/0004-pin-the-supply-chain-by-digest-not-nuget-lock-files.md) — but four things live in settings
or in another service and cannot be fixed by a commit.

| What | Where | Why it matters |
|---|---|---|
| **Private vulnerability reporting**, left enabled | Settings → Code security | `SECURITY.md` links straight to `/security/advisories/new`. With reporting disabled that link 404s and the policy tells a reporter to do something they cannot. |
| **OpenSSF Best Practices entry** | [bestpractices.dev](https://www.bestpractices.dev/) → sign in with GitHub → add `https://github.com/AdCodicem/AdCodicem.ValueObjects` | The `CII-Best-Practices` check is looked up by repository URL. Creating the entry scores *InProgress*; answering the questionnaire honestly reaches *Passing*, which is the realistic ceiling for a single-maintainer project. |
| **Weekly `github-actions` Dependabot updates**, left enabled | `.github/dependabot.yml` | Dependabot raises **no security alerts for actions pinned to a SHA**, only for ones on a floating version. The weekly version update is what replaces that alerting, so switching it off silently removes the safety net the pinning depends on. Its seven-day cooldown delays each update by a week. |
| **Branch protection**, before handing Scorecard a token that can read it | Settings → Rules | The `Branch-Protection` check currently errors and is *excluded from the average*. Giving `scorecards.yml` a `repo_token` with `Administration: Read-only`, or creating an ACTIVE ruleset on `main`, makes it readable — and it then enters the average at the heaviest weight. Its tiers are hard-gated: without "block deletions" and "block force-pushes" nothing above them counts, so enabling the read before the protection exists *lowers* the score rather than raising it. |

One more pin lives in a workflow rather than in Dependabot's reach: actionlint, which `lint.yml` downloads at the
version `ACTIONLINT_VERSION` names and checks against `ACTIONLINT_SHA256` before running it. It is bumped by hand
([Dependabot](#dependabot)).

One check stays low on purpose. `Code-Review` counts changesets approved by someone other than their author,
which a single maintainer cannot honestly produce — auto-approving pull requests with a bot would move the
number without moving the thing it measures.

`Signed-Releases` looks for signatures or SLSA provenance among the GitHub Release assets of the most recent
releases. Each stable release now attaches a Sigstore provenance bundle, but releases cut before that carry
none, so the score climbs as new releases replace them rather than all at once.

## Secrets and external services

| What | Where | Used by |
|---|---|---|
| **NuGet Trusted Publishing policies**, one per workflow file, each bound to its environment: `preview.yml` with `nuget`, `release.yml` with `nuget-stable`. Owner `AdCodicem`, repository `AdCodicem.ValueObjects`, package glob `AdCodicem.ValueObjects*`, new packages and new versions allowed. | nuget.org → your user name → Trusted Publishing | The OIDC exchange of `preview.yml`'s publish job and `release.yml`'s release job (`NuGet/login`, `user: AdCodicem`). No stored API key. |
| **`nuget` environment** — deployment branches limited to `main`, no required reviewer | Settings → Environments | `preview.yml`'s publish job. A required reviewer here would hold every weekly preview until someone clicks; use `nuget-stable` for that instead. |
| **`nuget-stable` environment** — deployment branches limited to `main`, required reviewer(s) | Settings → Environments | `release.yml`'s release job. This is the approval gate: `workflow_dispatch` starts the workflow, but the job waits for a reviewer before the OIDC exchange and `npx semantic-release` run. |
| `RELEASE_APP_CLIENT_ID` (variable) and `RELEASE_APP_PRIVATE_KEY` (secret) on `nuget-stable` | Settings → Environments → `nuget-stable` | `release.yml`, to push the release commit and tag past the ruleset on `main` as a GitHub App. See [Repository settings](#github-settings) for creating the App. |
| `CODECOV_TOKEN` | codecov.io → link the repository, then Settings → Secrets → Actions | The coverage uploads of `ci.yml`'s `coverage` job, one per Codecov flag of what the run tested, and of `heavy.yml`'s integration job on `main` and on a pull request labelled `ci:heavy`, which `ci.yml` hands the secret to. A public repository uploads without it — CI run 128 logged `Token length: 0` and the report still arrived —, so the secret is read only if it exists, which a private repository, or Codecov refusing tokenless uploads, would need. `fail_ci_if_error: false` keeps CI green when an upload is refused. Codecov posts `codecov/patch` and `codecov/project` on each pull request that uploads coverage, against the floors in `codecov.yml` ([ADR-0006](adr/0006-coverage-is-a-signal-not-a-goal.md)). |

Trusted Publishing is set at the account level on nuget.org, not per package, and a policy names a workflow file
and an environment: a run of any other file, or of the same file in another environment, is refused at the OIDC
exchange. Renaming `preview.yml` or `release.yml`, or moving a publish job to another environment, therefore needs
the policy changed on nuget.org first. After adding a policy, check that it reads *active*, not *temporarily active*,
and that `user: AdCodicem` in the workflows names the user who created it.

`ci.yml` published previews until `preview.yml` replaced it, under a policy of its own bound to `nuget`. Add the
`preview.yml` policy before that change merges, and delete the `ci.yml` one once `preview.yml` has published its first
preview ([Checking a run's provenance](#checking-a-runs-provenance)).

## The .NET compatibility island

`tests/Compat` installs the packages a commit packs into `net11.0` applications on the .NET 11 release candidate, and
`heavy.yml`'s `compat (.NET 11)` job runs it after every push to `main`, before every preview and stable release, on
the packages they publish, and on a pull request labelled `ci:heavy`
([ADR-0010](adr/0010-version-every-package-in-lockstep-independently-of-dotnet.md),
[ADR-0013](adr/0013-run-a-targeted-pull-request-ci-and-the-heavy-jobs-before-a-release.md)). It holds two projects: the
main one, and `tests/Compat/Swashbuckle`, which installs `AdCodicem.ValueObjects.Swashbuckle` apart, since Swashbuckle
10 fails on the `Microsoft.OpenApi` 3 the main project's `Microsoft.AspNetCore.OpenApi` 11 brings; the job runs both,
the second also when the first fails. The second takes Swashbuckle and `Microsoft.OpenApi` at the package's floors,
so it has no version to move at a release candidate. Dependabot never sees the island, so it moves by hand:

- **At each release candidate of .NET 11**, one pull request moves together `tests/Compat/global.json` (the exact SDK
  version), the seven `11.0.0-rc…` packages of `tests/Compat/Directory.Packages.props`
  (`Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.SqlServer`,
  `Microsoft.AspNetCore.Mvc.NewtonsoftJson`, `Microsoft.AspNetCore.OpenApi`, `Microsoft.AspNetCore.TestHost`,
  `Microsoft.AspNetCore.SignalR.Protocols.MessagePack`, `Microsoft.AspNetCore.SignalR.Client`),
  `Npgsql.EntityFrameworkCore.PostgreSQL`, and `System.CommandLine`, whose `3.0.0-rc…` version carries .NET's build
  number and moves to `3.0.0` when .NET 11 ships. Npgsql's provider pins EF Core to the exact build it was compiled
  against, so moving one without the other fails the restore with NU1107. When Npgsql has not published its build for
  the new release candidate yet, the SDK and the five ASP.NET Core packages move alone, and EF Core's two packages and
  Npgsql's provider follow together once it has: the island is on the RC2 SDK and ASP.NET Core packages, EF Core and
  Npgsql on RC1, since 2026-10-14.
- **When an advisory names a release candidate's package**, nothing flags it. Dependabot raises no alert, the island
  being out of its graph, and the island's restore does not fail either: nuget.org's audit data closes the release
  candidate's range at its bare label, `[11.0.0-preview.5, 11.0.0-rc.1]` for GHSA-86xp-94pg-8r6r, and NuGet sorts that
  label below every build published under it, `11.0.0-rc.1.26425.128` among them. So the island's RC1 build of
  `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` restored clean, though GitHub's advisory covers it until RC2,
  which fixes it. Check the advisories of Microsoft's patch day against `tests/Compat/Directory.Packages.props` by hand.
- **When .NET 11 ships** (expected around 2026-11-10, not confirmed):
  - `global.json` becomes `"version": "11.0.100"`, `"rollForward": "latestFeature"`, `"allowPrerelease": false`,
    after which `setup-dotnet` installs the latest 11.0 SDK by itself;
  - the eight packages move to `11.0.0`, Npgsql's provider included;
  - `System.CommandLine` moves to `3.0.0`;
  - `compat (.NET 11)` blocks previews from then on: set `compat-blocks: true` on `preview.yml`'s `heavy` job. It
    stays out of the required status checks, and informs only on a pull request and on `main`
    ([ADR-0013](adr/0013-run-a-targeted-pull-request-ci-and-the-heavy-jobs-before-a-release.md)).
- **When .NET 12 previews arrive**, moving the island to the next major, or adding a second one, is a decision of its
  own. Renaming the job changes the name of its check, which no ruleset requires.
- **`librdkafka.redist` moves with `Confluent.SchemaRegistry.Serdes.Json`**, in the island as in the repository's
  `Directory.Packages.props`, where Dependabot's `dotnet` group moves them together: the island and the unit suite's
  `tests/AdCodicem.ValueObjects.UnitTests.Serialization` reference the Kafka client's native library only to leave it
  out of their output, and `Confluent.Kafka` asks for its own version of it or later, so a pin left behind fails the
  restore (NU1605).

Two services walk the repository rather than the solution, and do see it. CodeQL's default setup downloads every SDK
a `global.json` names, the island's release candidate included, and analyses its code. GitHub's automatic dependency
submission restores every project file from the root with the .NET 10 SDK and fails on the first restore that fails:
each of the island's project files leaves itself empty on an SDK that cannot target `net11.0`, so that restore
succeeds with nothing to submit. Watch both after a change to one of them.
