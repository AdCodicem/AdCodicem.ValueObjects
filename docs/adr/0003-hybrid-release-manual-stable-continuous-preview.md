# 3. Publish previews continuously, cut stable releases by hand

Date: 2026-09-14

## Status

Accepted

## Context

Until now, versioning was MinVer alone: the version is derived from git tags, and pushing a `v*` tag triggered
the publish job. That works, and published `0.1.0-preview.1` and `0.1.0-preview.2`. What it does not give is a
changelog, or any relationship between the version number and what actually changed — the maintainer picks the
number by hand and has to remember what the bump means.

Stock semantic-release gives both: it derives the version from Conventional Commits, which this repository
already enforces in `lint.yml` and in a local `pre-commit` hook, and it generates `CHANGELOG.md`. Its default
shape, though, is to publish on every merge to `main`. For a library, that has a specific and unrecoverable
failure mode: NuGet does not allow unpublishing, only delisting. A commit typed `feat:` in a pull request that
was really documentation would ship ten packages to nuget.org, permanently. The version number would also be
decided by commit message discipline alone, with no moment at which a human looks at the whole release and
agrees to it.

Conversely, publishing nothing between stable releases means there is no way to try a change before it is
permanent.

## Decision

We will split the two concerns:

- **Every merge to `main` publishes a preview.** MinVer keeps doing what it already does — with no stable tag
  at `HEAD` it produces `0.1.1-preview.0.N` — and `ci.yml` pushes that to nuget.org with `--skip-duplicate`.
  No human decision, no changelog, nothing permanent about the number.
- **A stable release is a `workflow_dispatch`.** `release.yml` runs semantic-release, which computes the
  version from the commits since the last stable tag, writes `CHANGELOG.md`, packs, pushes, commits the
  changelog, tags `vX.Y.Z`, creates the GitHub Release and deploys the documentation.

The two are bridged by MinVer's `MINVERVERSIONOVERRIDE` environment variable: semantic-release computes the
stable version and hands it to MinVer, which steps aside. Both tools read the same `v*` tags, so they cannot
disagree about what the last release was.

The `tags: ['v*']` trigger and the `publish` job are removed from `ci.yml`. Without that, the tag
semantic-release creates would trigger a second publish of the version it just pushed.

## Consequences

The version number and the changelog are computed from commit history, so they cannot drift from it — and the
irreversible step still requires a person to click it. `PackageValidationBaselineVersion` also becomes
correct for free: semantic-release knows `lastRelease.version`, which is exactly the baseline the API
compatibility check wants, and which MinVer alone could only supply with a `git describe` script.

What this costs:

- **Node tooling in the release path of a C# repository.** semantic-release and six plugins, plus a root
  `package.json` that is a new Dependabot surface. It touches no source; it orchestrates.
- **Two ways to produce a version number**, which is more moving parts than either tool alone. The
  `MINVERVERSIONOVERRIDE` bridge is the load-bearing part; if it silently stopped working, a stable release
  would be packed with a preview version number.
- **Preview version churn on nuget.org.** Ten packages on every merge. Publishing previews to GitHub Packages
  instead would keep nuget.org clean, at the cost of requiring consumers to authenticate to try one. Chosen
  deliberately in favour of previews being trivially consumable.
- **A malformed commit still produces no release**, silently — the standard semantic-release trap. The
  Conventional Commits check on every pull request is what keeps it from happening, and the manual trigger
  means the maintainer notices when the computed version is not what they expected.

Because the repository is pre-1.0 and the last tag is a prerelease, the version semantic-release computes for
the first stable release must be verified with a dry run rather than assumed.
