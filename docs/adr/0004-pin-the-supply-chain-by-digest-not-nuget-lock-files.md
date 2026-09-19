# 4. Pin the supply chain by digest, and not with NuGet lock files

Date: 2026-09-19

## Status

Accepted

## Context

Everything this repository publishes is built by GitHub Actions, and the release path holds real authority: the
`release` job carries `contents: write` and an OIDC token it exchanges for a nuget.org API key. Whoever can
change what runs in that job can publish a package under this name, and a package on nuget.org can be delisted
but never unpublished.

Until now every step named a floating tag — `actions/checkout@v7`, `ossf/scorecard-action@v2.4.4` — and a tag
is a mutable pointer. Moving it is a normal operation for a maintainer and an obvious one for anyone who takes
over an action's repository. `pip install conventional-pre-commit`, in the lint workflow, resolved to whatever
PyPI served that minute.

OpenSSF Scorecard measures exactly this as its `Pinned-Dependencies` check, and scored it 1 out of 10: 0 of 27
GitHub-owned action references pinned, 0 of 6 third-party, 0 of 2 `dotnet restore` invocations, 0 of 1 `pip
install`. The check was the second-largest scoring gap in the report, and unlike most of the others it is
fixable by a commit rather than by a settings change or a second maintainer.

The `dotnet restore` half of that finding is the interesting one. Scorecard counts a restore as pinned when the
command line carries `--locked-mode`, which is only meaningful with a committed `packages.lock.json` per
project. That is the standard answer, and for this repository it does not work.

## Decision

We will pin by digest everything whose digest we can honestly hold, and we will not adopt NuGet lock files.

- **Every GitHub Action is pinned to a 40-character commit SHA**, with the release it came from as a
  same-line comment (`uses: actions/checkout@3d3c42e… # v7.0.1`). Dependabot reads that comment to derive the
  semver bump, so the existing `actions` group keeps separating minor and patch from major.
- **`conventional-pre-commit` is installed with `--require-hashes`** from
  `.github/requirements/requirements.txt`, which records the sha256 of both the wheel and the sdist.
- **`dotnet restore` keeps running without `--locked-mode`**, and no `packages.lock.json` is committed.

Three of the sixteen actions publish annotated tags, so their pin is the dereferenced commit
(`refs/tags/vX.Y.Z^{}`) and not the tag object: `codecov/codecov-action`, `ossf/scorecard-action` and
`github/codeql-action`. Re-pinning one by hand without the `^{}` produces a SHA that GitHub will refuse to
resolve.

### Why not lock files

Two properties of this repository make a committed lock file inconsistent by construction, both confirmed by
running it rather than by reading about it:

1. **`src/Directory.Build.props` references `Microsoft.SourceLink.GitHub` under
   `Condition="'$(GITHUB_ACTIONS)' == 'true'"`.** A restore-affecting condition on an environment variable
   means the package graph on a contributor's machine is not the graph on the runner. Lock files generated
   locally and then restored with `GITHUB_ACTIONS=true` fail `NU1004` on all thirteen `src/` projects, on the
   very first CI run and with no dependency update involved.
2. **The implicit package versions track the SDK.** The generated lock files record
   `Microsoft.NET.ILLink.Tasks:[10.0.12, )` and `Microsoft.SourceLink.GitHub >= 10.0.401` — the runtime and
   feature band of whichever SDK performed the restore. `global.json` sets `rollForward: latestFeature`, so
   the runner image moving to a newer 10.0.x SDK changes those versions and breaks locked-mode restore.
   Microsoft's own guidance is to set `rollForward: disable` before checking in a lock file, which would pin
   every contributor to one exact SDK patch.

On top of that, Dependabot regenerates the lock file only for the project whose manifest it edited. Under
Central Package Management it edits one file, `Directory.Packages.props`, while N lock files go stale — the
case reported in dependabot-core#13950 and still open. With `dependabot-auto-merge.yml` in place, the visible
symptom would be a weekly dependency queue that quietly stops moving.

Adding `--locked-mode` *without* lock files was considered and rejected outright: with no lock file present
the flag is a silent no-op — verified — so it would raise the score by two points while changing nothing about
what gets restored. That is a worse outcome than the lower score, because it converts a true signal into a
false one.

## Consequences

`Pinned-Dependencies` goes from 1 to 8 rather than to 10. The two `dotnet restore` invocations remain the only
dependencies Scorecard counts as unpinned, and they will keep showing up in every report. That is a deliberate,
recorded acceptance rather than an oversight, which is the reason this file exists.

What replaces the lock file is not nothing: Central Package Management already puts every version in one
reviewed file, transitive pinning is on, and Dependabot proposes the bumps weekly. What is genuinely lost is
transitive-version reproducibility — two restores months apart can resolve different transitive versions.

Pinning by SHA costs something too, and it is worth naming: **Dependabot does not raise security alerts for
actions pinned to a SHA**, only for ones using semantic versioning. The weekly `github-actions` update is what
replaces that, so it must not be disabled.

The pinning is also not transitive, and claiming otherwise would be wrong. `ossf/scorecard-action` runs
`docker://ghcr.io/ossf/scorecard-action:v2.4.4`, a mutable image tag; `pre-commit/action` is a composite action
that internally uses `actions/cache@v4` and `pip install pre-commit`, neither pinned; `codecov/codecov-action`
downloads its CLI at run time. Pinning the action fixes the manifest we consume, not every layer beneath it.
Only the upstream projects can close those.

Finally, the decision is worth revisiting if the `GITHUB_ACTIONS` condition on SourceLink goes away and
`global.json` moves to `rollForward: disable` — at that point lock files become consistent, and the remaining
objection is only the Dependabot fan-out.
