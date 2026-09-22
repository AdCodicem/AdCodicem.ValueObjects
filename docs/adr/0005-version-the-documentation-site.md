# 5. Version the documentation site with Docusaurus' own versioning

Date: 2026-09-22

## Status

Accepted

## Context

Releases run on two tracks ([ADR-0003](0003-hybrid-release-manual-stable-continuous-preview.md)): every merge to
`main` publishes a preview, and a stable release is a deliberate dispatch. The documentation site had one
track. `deploy-docs.yml` redeployed it on every push that touched `website/`, so the site described `main`, which
is to say the preview, while presenting itself as *the* documentation. Someone who installed the stable package
could read about an option their version does not have and have no way to tell.

What is wanted: the stable documentation by default, a button to the documentation of the latest preview, and
the documentation of every stable version still reachable through a version selector.

Four facts about this repository constrain the answer:

1. **GitHub Pages is published from Actions**, and each deployment replaces the whole site. Nothing
   accumulates between deployments unless Pages is switched to serving a branch.
2. **The API reference is generated, not committed.** DocFX writes it into `website/docs/api/`, which is
   gitignored. It is 486 KB of the roughly 530 KB a copy of the documentation weighs, 92 %. A page frozen for a
   version has to carry the API reference of that version, or it describes one release's prose next to another
   release's signatures.
3. **Every merge publishes a preview.** `ci.yml` has no path filter, so documentation that follows the preview
   is redeployed on every merge.
4. **Stable releases are cut only from `main`**, by semantic-release, so a release is always the newest
   version that exists. No maintenance branch releases an older line.

No stable release exists yet: `v0.1.0` is a baseline tag with no package behind it.

## Decision

We will version the site with Docusaurus' docs versioning. Each stable release commits a snapshot.

- **`website/docs/` is the preview.** It is Docusaurus' `current` version. It is served under `/docs/preview/`
  and labelled with the MinVer version of the commit it was built from, for example `Preview (0.4.0-preview.0.12)`.
  It carries the "unreleased" banner and is `noindex`. Until the first stable release it is the only version, so
  it is served at `/docs/` under an announcement bar that says no stable release exists yet.
- **A stable release freezes it.** `.github/scripts/docs-snapshot.sh` runs in semantic-release's prepare step:
  it generates the API reference, then runs `docusaurus docs:version`. `@semantic-release/git` commits the
  result with the changelog, in the commit the release tag points at. A dry run skips prepare and freezes
  nothing.
- **There is one entry per line of versions, not per release.** A line is `0.<minor>.x` while the major is 0,
  because a 0.x minor is the breaking channel (the convention `release-pack.sh` already follows when it skips
  package validation), and `<major>.x` from 1.0 on. A release in an existing line replaces that line's snapshot
  wholesale. Every line is kept.
- **The newest line is served at `/docs/`**, older ones at `/docs/<line>/`. The selector lists only the stable
  lines, each labelled with the exact release it was frozen at (`0.3.x (0.3.2)`, from
  `website/released-versions.json`). A **Preview** link in the navbar leads to the preview.
- **Every deployment rebuilds everything.** `ci.yml` calls `deploy-docs.yml` after the preview push has
  succeeded, so the preview documentation always describes an installable package. `release.yml` calls it on the
  release tag, since only that commit contains the new snapshot. Each deployment writes `deployment.json`. The
  next one reads it back and stands down when the live site was built from a descendant of its own commit,
  which happens when two merges' CI runs finish out of order.
- **Two things stay outside the versioning.** The Contributing page, now a plain page, describes how to work on
  `main`. The homepage is not versioned either, but its example is a partial frozen with the docs, and the
  homepage renders the copy from the latest stable snapshot.
- **A released page can be corrected** by a pull request when the error misleads users of that release, and
  the same fix must go into `website/docs/`, since the next snapshot of the line replaces the old one.

### Rejected

- **Rebuilding every version from its tag at each deployment.** Nothing generated would be committed. But every
  old tag would have to keep building with the toolchain of the day: the SDK that `global.json` rolls forward
  to, DocFX, Node and the next Docusaurus major. The deployment would get slower with every release and break
  on the first incompatible upgrade of any of them, and the version selector would be hand-written.
- **Accumulating builds on a `gh-pages` branch**, the way `mike` does for MkDocs. Each version would stay
  exactly as published, and the build time would stay constant. But it needs Pages switched from Actions to a
  branch and a hand-written selector that reads a manifest at run time. A published version could no longer be
  corrected, and an old build's navbar would never learn about newer versions without that run-time selector.
- **One entry per release** (`0.2.0`, `0.2.1`, …). semantic-release cuts a patch for every `fix` commit. Each one
  would add about 530 KB to the repository and a selector entry that differs from its neighbour by one bug fix.
- **One entry per major, 0.x included.** It would put the whole pre-1.0 history, where minors break, in one
  entry, and distinguish nothing.
- **No archive, stable and preview only.** It is the cheapest option, with nothing committed, and it covers two of
  the three needs. It was turned down because older lines are expected to stay in use.
- **A permanent URL per version, with `/docs/` redirecting to the current one.** Shared links would stay pinned
  to their version. But every page of the latest version would become a client-side redirect, and the links in
  the README and the agent skill would freeze to whatever version was current when they were followed.
- **A Stable ⇄ Preview toggle that keeps the current page.** It needs a swizzled navbar component that has to be
  carried across Docusaurus upgrades. The version selector already keeps the page when switching between stable
  lines. The Preview link leads to the preview's introduction.
- **An install command in the preview banner.** It needs `DocVersionBanner` swizzled. The label already carries
  the exact version, which is the information the command would repeat.

## Consequences

The repository grows by about 530 KB for each new line, and each line adds one more copy of the site to every
build. The build time grows roughly linearly with the number of lines. For reference, the single-version site had 82
pages and built in about 15 seconds on a laptop when this was written. All lines are kept for now. If the site
build ever takes several minutes, the remedy is to drop the oldest lines: delete their folder, their sidebar and
their entry in `versions.json`.

Stable readers get documentation changes at the next release, not at merge time. That is the point, but it
also means a correction to a released page is made in two places. `DocumentationSnippetTests` checks only
`website/docs/`, so a snippet corrected in a snapshot is checked by hand. The snapshots describe older releases
and cannot be compiled against the current generator.

Old versions are rendered by today's Docusaurus and theme. That is what keeps their navbar and selector
current, but a Docusaurus upgrade that rejects some old Markdown fails the whole build, and the fix then lands in
the snapshot.

The release path is only exercised by a real release, because a dry run skips prepare. The snapshot script can
be run locally against a made-up version, and `docs/maintaining.md` lists what to check after the first real
run. If the script fails, semantic-release stops before publishing anything.

The preview documentation now waits for the preview package. A change that touches only `website/` goes through
the full CI before it is deployed, and a failed push to nuget.org holds the documentation back as well. When two
merges land close together, the older deployment waiting in the `pages` concurrency group is cancelled in
favour of the newer one, so a CI run on `main` can end as *cancelled* without anything being wrong.

At the moment of a release the preview and the stable line are the same commit, so the preview reads
`Preview (0.3.2)` until the next merge.
