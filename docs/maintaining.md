# Maintaining

One-time setup that lives in GitHub and nuget.org settings rather than in this repository, and the routines the
publishing workflows leave to a person. Several workflows here succeed while doing nothing until the matching switch
is on, so this list is worth checking if something looks wired up but never happens.

## Publishing a preview

`preview.yml` publishes a preview of every package to nuget.org every Monday at 07:15, Paris time, and whenever it is
dispatched (**Actions → preview → Run workflow**, from `main`: a dispatch from another branch stops at its first
job). It publishes only when a package input changed since the version nuget.org has from the nearest commit, and
then all fourteen packages at one version, or none.
[ADR-0009](adr/0009-publish-previews-weekly-when-a-package-input-changed.md) has the reasoning.

Each run says what it decided. The **compute the version** job prints the version semantic-release would give the
next release, and **decide what to publish** writes a summary, *Preview: publish*, *repair* or *none*, with the
reason and the package inputs that changed:

- **publish**: the tests run on `HEAD`, the pack job packs it at `<next release>-preview.<commits since the last
  stable tag>`, and the publish job attests and pushes it.
- **repair**: a previous push stopped midway, so nuget.org has the base version for some packages only. The run
  packs that commit again at that version, tests included, and pushes only the missing packages. Whatever `HEAD`
  changed since waits for the next run, so dispatch again once the repair is green.
- **none**: nothing that ships changed. The tests, the pack and the push are skipped.

Whatever it decided, a run whose jobs all succeeded redeploys the site, labelled with the version on nuget.org that
describes the commit it builds. A run with a failed or cancelled job deploys nothing.

### When a run is red

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

- **Before a stable release**, dispatch `preview.yml` if package inputs changed since the last preview, and start the
  release once that run is green (see [Cutting a stable release](#cutting-a-stable-release)).
- **After merging a security update of a dependency the packages ship**, dispatch `preview.yml` rather than wait for
  Monday, and consider a release: previews receive no fixes of their own (`SECURITY.md`). A release is possible only
  if the update landed as `fix(deps)` ([Dependabot](#dependabot)): a `chore(deps)` commit gives semantic-release
  nothing to release.
- **After 60 days without activity in the repository**, GitHub disables the schedule of a public repository's
  workflow. Turn it back on with `gh workflow enable preview.yml`.
- **Failures of a scheduled run are notified to whoever last changed its cron line**, not to whoever dispatches it.
  If the run page names an actor other than you, push a commit of your own that touches the cron line, or watch the
  workflow.

### Checking a run's provenance

The first time `preview.yml` publishes, and after any change to its publish job, dispatch it rather than wait for
Monday, and check:

1. the fourteen packages are on nuget.org at the version the run printed, and the publish job finished its wait for
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
reviewer approves. The **build and test** and **pack** jobs must pass: the release job waits for both. The pack job,
which holds no credential, packs only the packable projects under `src/` and freezes the documentation; the release
job builds nothing, checks what the pack job produced against its digests, and pushes it.

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
| Required status checks **`build and test`** and **`native AOT`** (`ci.yml`), and **`workflows`** (`lint.yml`) | Settings → Rules → Rulesets → `Default` (id 23842638) | Auto-merge can merge a red build, or one that breaks native AOT, and a red `fix(deps)` would then release a broken patch. The checks are job names: renaming any of these jobs leaves every pull request waiting for a check that never reports. `compat (.NET 11)` joins them when .NET 11 ships ([The .NET compatibility island](#the-net-compatibility-island)), not before. |
| Allow GitHub Actions to create and approve pull requests | Settings → Actions → General | Not needed for auto-merge, but required if a workflow is ever made to open PRs. |
| Discussions | Settings → General → Features | `.github/DISCUSSION_TEMPLATE/q-a.yml` and the issue-template link to Discussions go nowhere. |
| Pages source: GitHub Actions | Settings → Pages | `deploy-docs.yml` uploads an artifact that is never served. |
| `github-pages` environment limited to `main` (the default) | Settings → Environments | Nothing breaks, but a deployment from another branch could replace the live site. Every deployment this repository makes runs from `main`: `preview.yml`, whose first job stops a dispatch from any other branch, and `release.yml`. `deploy-docs.yml` can no longer be dispatched, and `ci.yml`'s **documentation site** job, which builds the site on every pull request, deploys nothing. |
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
  `Microsoft.Extensions.*`, `Microsoft.OpenApi` and `Npgsql.EntityFrameworkCore.PostgreSQL` are ignored: a new major
  is supported by a decision, a dependency floor or a target framework
  ([ADR-0010](adr/0010-version-every-package-in-lockstep-independently-of-dotnet.md)), never by a bump. Dependabot's
  NuGet updater applies these rules to security updates too: a security fix that exists only on a new major of one
  of them opens no pull request, and its alert stays open. Treat it as a deliberate migration under ADR-0010. A fix
  on the current major opens at once.
- **`fix(deps)` for what ships.** Dependabot takes one `nuget` entry per directory, so every NuGet pull request opens
  as `chore(deps)`. `dependabot-auto-merge.yml` retitles it `fix(deps): …` when it updates a dependency listed in
  `.github/shipped-dependencies`, and `fix(deps)!: …` when that update is a new major, so that merging it releases a
  patch, or a breaking change. The list holds the dependencies the nuspecs name, plus `Microsoft.CodeAnalysis.CSharp`
  and `PolySharp`, compiled into the generator; a step of `ci.yml` fails when it no longer matches the packed
  nuspecs. That workflow runs on `pull_request_target` and only ever checks out the base branch, sparsely, never the
  pull request's code. A grouped pull request that mixes shipped and test-only dependencies becomes one `fix(deps)`.
- **A security update that pins a new transitive dependency.** Dependabot can fix a vulnerable transitive dependency
  of a shipped package by adding a `<PackageVersion>` to `Directory.Packages.props`, which transitive pinning makes a
  dependency of the package. If `.github/shipped-dependencies` does not name it yet, the pull request opens as
  `chore(deps)` with auto-merge queued, and **build and test** fails on the list. `dependabot-auto-merge.yml` reads
  the list from the base branch and ignores events you cause, so it cannot retitle the pull request. In this order:
  retitle it yourself, `fix(deps): …`, or `fix(deps)!: …` when it crosses a major; then push the list line to its
  branch. The title you set holds, and the auto-merge still queued squashes the pull request under it once CI is
  green. Do not merge the list line in a pull request of its own: nothing on `main` ships that ID yet, so the check
  fails there too. Closing the Dependabot pull request and landing the pin and the line together in a
  `fix(deps): pin <id> to <version>` of your own works as well.
- **Squash, always.** Auto-merge squashes minor and patch updates. Merge a major by hand, after reading it, and
  squash it too: a rebase merge keeps Dependabot's `chore(deps)` commit and releases nothing. A `fix(deps)!` releases
  a minor while the version is 0.x. Auto-merge is queued once, when Dependabot opens the pull request: **Disable
  auto-merge** on it holds it, through Dependabot's later rebases, until you merge it yourself.

Some versions are bumped by hand, because Dependabot never sees them:

- the compatibility island under `tests/Compat` ([below](#the-net-compatibility-island));
- `Microsoft.OpenApi`, `Microsoft.Extensions.Options` and `Microsoft.Extensions.DependencyInjection.Abstractions`,
  which `Directory.Packages.props` pins for the packages that pull them transitively;
- actionlint, which `lint.yml` downloads as a binary: change `ACTIONLINT_VERSION` and `ACTIONLINT_SHA256` together,
  taking the digest of `actionlint_<version>_linux_amd64.tar.gz` from the release's checksums file.

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
| `CODECOV_TOKEN` | codecov.io → link the repository, then Settings → Secrets → Actions | The coverage upload in `ci.yml`. A public repository uploads without it — CI run 128 logged `Token length: 0` and the report still arrived —, so the secret is read only if it exists, which a private repository, or Codecov refusing tokenless uploads, would need. `fail_ci_if_error: false` keeps CI green when an upload is refused. Codecov posts `codecov/patch` and `codecov/project` on each pull request, against the floors in `codecov.yml` ([ADR-0006](adr/0006-coverage-is-a-signal-not-a-goal.md)). |

Trusted Publishing is set at the account level on nuget.org, not per package, and a policy names a workflow file
and an environment: a run of any other file, or of the same file in another environment, is refused at the OIDC
exchange. Renaming `preview.yml` or `release.yml`, or moving a publish job to another environment, therefore needs
the policy changed on nuget.org first. After adding a policy, check that it reads *active*, not *temporarily active*,
and that `user: AdCodicem` in the workflows names the user who created it.

`ci.yml` published previews until `preview.yml` replaced it, under a policy of its own bound to `nuget`. Add the
`preview.yml` policy before that change merges, and delete the `ci.yml` one once `preview.yml` has published its first
preview ([Checking a run's provenance](#checking-a-runs-provenance)).

## The .NET compatibility island

`tests/Compat` installs the packages each commit packs into a `net11.0` application on the .NET 11 release candidate,
and `ci.yml`'s `compat (.NET 11)` job runs it on every pull request and push to `main`
([ADR-0010](adr/0010-version-every-package-in-lockstep-independently-of-dotnet.md)). Dependabot never sees it, so it
moves by hand:

- **At each release candidate of .NET 11**, one pull request moves together `tests/Compat/global.json` (the exact SDK
  version), the five `11.0.0-rc…` packages of `tests/Compat/Directory.Packages.props`
  (`Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.SqlServer`,
  `Microsoft.AspNetCore.Mvc.NewtonsoftJson`, `Microsoft.AspNetCore.OpenApi`, `Microsoft.AspNetCore.TestHost`) and
  `Npgsql.EntityFrameworkCore.PostgreSQL`. Npgsql's provider pins EF Core to the exact build it was compiled against,
  so moving one without the other fails the restore with NU1107.
- **When .NET 11 ships** (expected around 2026-11-10, not confirmed):
  - `global.json` becomes `"version": "11.0.100"`, `"rollForward": "latestFeature"`, `"allowPrerelease": false`,
    after which `setup-dotnet` installs the latest 11.0 SDK by itself;
  - the six packages move to `11.0.0`, Npgsql's provider included;
  - `compat (.NET 11)` joins the required status checks of the `Default` ruleset (id 23842638);
  - the comment in `ci.yml` that says the job is not a required check goes.
- **When .NET 12 previews arrive**, moving the island to the next major, or adding a second one, is a decision of its
  own. Renaming the job changes the required check.

Two services walk the repository rather than the solution, and do see it. CodeQL's default setup downloads every SDK
a `global.json` names, the island's release candidate included, and analyses its code. GitHub's automatic dependency
submission restores every project file from the root with the .NET 10 SDK and fails on the first restore that fails:
the island's project file leaves itself empty on an SDK that cannot target `net11.0`, so that restore succeeds with
nothing to submit. Watch both after a change to the island's project file.
