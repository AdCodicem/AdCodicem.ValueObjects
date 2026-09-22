# Maintaining

One-time setup that lives in GitHub and nuget.org settings rather than in this repository. Several workflows
here succeed while doing nothing until the matching switch is on, so this list is worth checking if something
looks wired up but never happens.

## Cutting a stable release

`v0.1.0` is the version baseline: a stable tag placed by hand on `main`, so semantic-release counts from `0.1.0`
instead of starting at `1.0.0`. It is not a release — no `0.1.0` package exists, and nuget.org has only the two
previews. The reasoning and the measurements are in
[ADR-0003](adr/0003-hybrid-release-manual-stable-continuous-preview.md).

Run **Actions → release → Run workflow** with `dry_run` ticked and read the version it computes before running
it for real. Publishing to nuget.org cannot be undone — a package can be delisted, never unpublished.

A real run also freezes the documentation. It commits `website/versioned_docs/` along with the changelog, then
redeploys the site from the new tag ([ADR-0005](adr/0005-version-the-documentation-site.md)). The dry run
skips that step, like every other prepare step, so the first stable release is the first time it runs on a
runner. After the first real release, check four things on the site:
- `/docs/` names the new line in the version selector, for example `0.2.x (0.2.0)`;
- the **Preview** button leads to `/docs/preview/`;
- the "no stable release yet" bar is gone;
- the release commit contains `website/versioned_docs/version-<line>/api/`.

If the snapshot step fails, semantic-release stops before publishing anything, so the fix is to correct it and
dispatch again.

## GitHub settings

| Setting | Where | Without it |
|---|---|---|
| Allow auto-merge | Settings → General → Pull Requests | `dependabot-auto-merge.yml` runs green and merges nothing. |
| CI as a required status check | Settings → Rules / Branch protection on `main` | Auto-merge can merge a red build. |
| Allow GitHub Actions to create and approve pull requests | Settings → Actions → General | Not needed for auto-merge, but required if a workflow is ever made to open PRs. |
| Discussions | Settings → General → Features | `.github/DISCUSSION_TEMPLATE/q-a.yml` and the issue-template link to Discussions go nowhere. |
| Pages source: GitHub Actions | Settings → Pages | `deploy-docs.yml` uploads an artifact that is never served. |
| `github-pages` environment limited to `main` (the default) | Settings → Environments | Nothing breaks, but a `deploy docs` dispatched from another branch could replace the live site. Every deployment this repository makes runs from `main`: `ci.yml` on push, `release.yml` dispatched there. |
| Code scanning: **default setup**, left enabled | Settings → Code security → Code scanning | This repository uses CodeQL's default setup. There is deliberately no `codeql.yml`: an advanced configuration cannot upload its results while default setup is on — GitHub rejects the SARIF with *"CodeQL analyses from advanced configurations cannot be processed when the default setup is enabled"*. Only add a workflow if you first disable default setup, and only if you need something it cannot do (custom query packs, or a manual build for a solution autobuild cannot handle). |
| GitHub Sponsors | Account settings | `.github/FUNDING.yml` has no effect. |

`@semantic-release/git` pushes the changelog commit to `main`. If `main` is protected, either allow the
`github-actions[bot]` actor to bypass the rule, or give `release.yml` a token that can.

## Supply chain and the OpenSSF scorecard

`scorecards.yml` publishes to the OpenSSF API weekly, which is what backs the README badge. Most of what it
measures is settled in this repository — see
[ADR-0004](adr/0004-pin-the-supply-chain-by-digest-not-nuget-lock-files.md) — but four things live in settings
or in another service and cannot be fixed by a commit.

| What | Where | Why it matters |
|---|---|---|
| **Private vulnerability reporting**, left enabled | Settings → Code security | `SECURITY.md` links straight to `/security/advisories/new`. With reporting disabled that link 404s and the policy tells a reporter to do something they cannot. |
| **OpenSSF Best Practices entry** | [bestpractices.dev](https://www.bestpractices.dev/) → sign in with GitHub → add `https://github.com/AdCodicem/AdCodicem.ValueObjects` | The `CII-Best-Practices` check is looked up by repository URL. Creating the entry scores *InProgress*; answering the questionnaire honestly reaches *Passing*, which is the realistic ceiling for a single-maintainer project. |
| **Weekly `github-actions` Dependabot updates**, left enabled | `.github/dependabot.yml` | Dependabot raises **no security alerts for actions pinned to a SHA**, only for ones on a floating version. The weekly version update is what replaces that alerting, so switching it off silently removes the safety net the pinning depends on. |
| **Branch protection**, before handing Scorecard a token that can read it | Settings → Rules | The `Branch-Protection` check currently errors and is *excluded from the average*. Giving `scorecards.yml` a `repo_token` with `Administration: Read-only`, or creating an ACTIVE ruleset on `main`, makes it readable — and it then enters the average at the heaviest weight. Its tiers are hard-gated: without "block deletions" and "block force-pushes" nothing above them counts, so enabling the read before the protection exists *lowers* the score rather than raising it. |

Two checks stay low on purpose. `Code-Review` counts changesets approved by someone other than their author,
which a single maintainer cannot honestly produce — auto-approving pull requests with a bot would move the
number without moving the thing it measures. `Signed-Releases` needs signatures or SLSA provenance attached to
the GitHub Release assets; it is worth doing, but it is a change to `release.yml`, the one workflow whose
mistakes cannot be undone, so it belongs in its own piece of work rather than in a hardening sweep.

## Secrets and external services

| What | Where | Used by |
|---|---|---|
| **NuGet trusted publisher** — add GitHub owner `AdCodicem`, this repository, and workflow `ci.yml` **and** `release.yml` | nuget.org → each package → Manage → Trusted Publishers | The OIDC exchange in `ci.yml` (previews) and `release.yml` (stable). No stored API key. |
| **`nuget` environment** — no protection rules | Settings → Environments | `ci.yml`'s preview job targets it. Adding a required reviewer here would gate every merge's preview too, not just stable releases -- use `nuget-stable` for that instead. |
| **`nuget-stable` environment** — required reviewer(s) | Settings → Environments | `release.yml`'s release job targets it. This is the approval gate: `workflow_dispatch` starts the job, but it waits for a reviewer before the OIDC exchange and `npx semantic-release` run. |
| `CODECOV_TOKEN` | codecov.io → link the repository, then Settings → Secrets → Actions | The coverage upload in `ci.yml`. It is set to `fail_ci_if_error: false`, so without the token CI stays green and coverage is simply missing. |

The trusted publisher has to name **both** workflows: `ci.yml` pushes previews on every merge, `release.yml`
pushes the stable release. Registering only one leaves the other failing at the OIDC exchange. If a trusted
publisher entry constrains itself to an environment, point `release.yml`'s entry at `nuget-stable`, not `nuget`.
