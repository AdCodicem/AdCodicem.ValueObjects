# Maintaining

One-time setup that lives in GitHub and nuget.org settings rather than in this repository. Several workflows
here succeed while doing nothing until the matching switch is on, so this list is worth checking if something
looks wired up but never happens.

## Before the first stable release

Create the version baseline tag, or `release.yml` publishes `1.0.0` instead of `0.1.0`. The reasoning and the
measurements are in [ADR-0003](adr/0003-hybrid-release-manual-stable-continuous-preview.md).

```bash
git tag -a v0.0.0 "$(git rev-list --max-parents=0 main)" -m "Version baseline for semantic-release."
git push origin v0.0.0
```

Then run **Actions → release → Run workflow** with `dry_run` ticked and read the version it computes before
running it for real. Publishing to nuget.org cannot be undone — a package can be delisted, never unpublished.

## GitHub settings

| Setting | Where | Without it |
|---|---|---|
| Allow auto-merge | Settings → General → Pull Requests | `dependabot-auto-merge.yml` runs green and merges nothing. |
| CI as a required status check | Settings → Rules / Branch protection on `main` | Auto-merge can merge a red build. |
| Allow GitHub Actions to create and approve pull requests | Settings → Actions → General | Not needed for auto-merge, but required if a workflow is ever made to open PRs. |
| Discussions | Settings → General → Features | `.github/DISCUSSION_TEMPLATE/q-a.yml` and the issue-template link to Discussions go nowhere. |
| Pages source: GitHub Actions | Settings → Pages | `deploy-docs.yml` uploads an artifact that is never served. |
| Code scanning: **default setup**, left enabled | Settings → Code security → Code scanning | This repository uses CodeQL's default setup. There is deliberately no `codeql.yml`: an advanced configuration cannot upload its results while default setup is on — GitHub rejects the SARIF with *"CodeQL analyses from advanced configurations cannot be processed when the default setup is enabled"*. Only add a workflow if you first disable default setup, and only if you need something it cannot do (custom query packs, or a manual build for a solution autobuild cannot handle). |
| GitHub Sponsors | Account settings | `.github/FUNDING.yml` has no effect. |

`@semantic-release/git` pushes the changelog commit to `main`. If `main` is protected, either allow the
`github-actions[bot]` actor to bypass the rule, or give `release.yml` a token that can.

## Secrets and external services

| What | Where | Used by |
|---|---|---|
| **NuGet trusted publisher** — add GitHub owner `AdCodicem`, this repository, and workflow `ci.yml` **and** `release.yml` | nuget.org → each package → Manage → Trusted Publishers | The OIDC exchange in `ci.yml` (previews) and `release.yml` (stable). No stored API key. |
| **`nuget` environment** | Settings → Environments | Both publish jobs target it; add a required reviewer here if you want a second gate on previews. |
| `CODECOV_TOKEN` | codecov.io → link the repository, then Settings → Secrets → Actions | The coverage upload in `ci.yml`. It is set to `fail_ci_if_error: false`, so without the token CI stays green and coverage is simply missing. |

The trusted publisher has to name **both** workflows: `ci.yml` pushes previews on every merge, `release.yml`
pushes the stable release. Registering only one leaves the other failing at the OIDC exchange.
