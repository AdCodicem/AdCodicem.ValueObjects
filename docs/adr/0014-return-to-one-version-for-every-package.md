# 14. Return to one version for every package

Date: 2026-10-27

## Status

Accepted.

- **Supersedes** [ADR-0012](0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md): the two
  release trains are withdrawn.
- **Restores** [ADR-0010](0010-version-every-package-in-lockstep-independently-of-dotnet.md): every package is
  released at one version, its ladder, its compatibility island and its floors with no upper bound unchanged.
- Withdraws ADR-0012's amendments of [ADR-0003](0003-hybrid-release-manual-stable-continuous-preview.md),
  [ADR-0005](0005-version-the-documentation-site.md) and
  [ADR-0009](0009-publish-previews-weekly-when-a-package-input-changed.md), and the train points of
  [ADR-0013](0013-run-a-targeted-pull-request-ci-and-the-heavy-jobs-before-a-release.md).

## Context

ADR-0012 was accepted on 2026-10-19 and built from 2026-10-22 to 2026-10-25, in pull requests #207 to #227:

- a list of the trains, `src/release-trains.json`, read by MSBuild through a regular expression and by a script
  module, `release-trains.mjs`, which attributed every commit to the trains whose package inputs it changed;
- a version per train in previews (`next-version.mjs --trains`, a preview gate deciding train by train, the core
  pushed and listed before the satellites);
- a semantic-release plugin and a driver releasing one train, with resume and sync logic, rehearsed in `release.yml`'s
  dry runs only;
- a strict repack of the train a run does not release, held to exactly its last release's API;
- an upper bound on every dependency on one of our packages, and a weekly issue listing the majors Dependabot holds
  back.

That is about 7,800 lines of scripts, tests, workflow steps and documentation, for one maintainer. No train ever
released apart: nuget.org has `0.3.0-preview.322` of every package, the last one-version preview. The first preview
deciding per train, on 2026-10-26, pushed `AdCodicem.ValueObjects.Abstractions` `0.3.0-preview.403` and then failed:
nuget.org refused the next package's dependency range, `[0.3.0-preview.403, 0.4.0-0)`, whose upper bound it does not
read as a version. Its fix (#228) was still open.

What the trains buy is on stable releases only, and only once the core is frozen at 1.0 while the satellites follow
their frameworks' majors. The core is not at 1.0, and every weekly preview window measured touched both trains. At the
same time the satellite set is reviewed, its costliest packages, those whose frameworks ship majors on calendars of
their own and those reaching their framework's internals, being the first candidates for retirement. Fewer satellites
on fast frameworks weaken the case for a train of their own.

## Decision

We will release every package at one version again, as ADR-0010 decided, and remove the release trains.

- **Removed.** `src/release-trains.json`, `release-trains.mjs`, the per-train paths of `next-version.mjs`,
  `preview-gate.sh`, `release-pack.sh`, `verify-packages.sh`, `push-packages.sh`, `docs-snapshot.sh` and the
  workflows, the semantic-release plugin and driver and their configuration, `RELEASE_TRAIN_VERSIONS` and the build's
  refusal of `MINVERVERSIONOVERRIDE`, `CHANGELOG.satellites.md`, the `train` of `.github/ci-families.json`, the
  satellites' documentation version, and the `satellites-v0.2.1` tag.
- **Removed too, though they hold without trains.**
  - The upper bound on our own dependencies: a package asks for the others at its own version or later, a bare floor,
    as before. A consumer references every package at one version, which the README asks; mixing versions restores
    without a warning, as ADR-0012 measured.
  - The weekly check of the majors Dependabot holds back: a security fix published only on a new major opens no pull
    request, and GitHub's Dependabot alert is the signal.
- **Kept.**
  - A README per package: the main package's `src/AdCodicem.ValueObjects/README.md`, every other package
    `src/PackageReadme.md` filled with its id and description (`_WritePackageReadme`), checked by
    `verify-packages.sh`. The repository's `README.md` is no package input.
  - The tests holding every satellite API the core's analyzers name by string to the satellite itself
    (`SerializerContractDiagnosticTests`, `OrleansRemedyNameTests`, `MartenCompiledQueryDiagnosticTests`): nothing else
    compiles those strings against the packages.
  - Three fixes of the release chain that the one version needs too: the `split2` override of git-log-parser, without
    which semantic-release lost commits of a long range; the cut of a GitHub Release's notes below GitHub's limit; and
    the changelog's title on top. `release-config.test.mjs` holds them.
  - Dependabot's ignore rules for the majors of every shipped dependency and the minors of Roslyn, and its groups of
    families pinned together, which ADR-0012 described and which predate it.
  - A change to `src/Directory.Build.*` runs everything in the pull-request plan.
- **The partial preview.** `0.3.0-preview.403` stays on nuget.org for `Abstractions` alone. Its commit packs only with
  the build this record removes, so no run can complete it: `preview-gate.sh` lists it as abandoned and never takes it
  as a base. The next preview, packed at a greater height, outnumbers it.

### Rejected

- **Finishing ADR-0012.** It needed #228, the first stable release of each train, and a contributor following six
  more lines of rules for every commit, for a gain that waits on the core's 1.0.
- **Keeping the bounds alone.** They are what nuget.org refused, they need a closing version no prerelease of ours may
  take, and under one version they only turn a mix of our versions into NU1608, a warning.
- **Reverting commit by commit.** The commits kept above edited the same files; the removal is written as a few
  commits instead, each file brought back to its state before ADR-0012's build, then given what is kept.

## Consequences

- A framework's major that forces a floor up is again a breaking change of every package: a minor while the major is
  0, a major of everything from 1.0 on. Retiring the satellites on the fastest frameworks is the answer, not trains.
  If, after 1.0, one satellite forces two majors in a year, the question of a version of its own comes back, with
  this record's numbers.
- Consumers read one number. The README and the site name one version and one support table.
- The release chain is the one of ADR-0003, ADR-0009 and ADR-0010 again, plus `release-config.test.mjs`, which
  `release tooling` runs.
