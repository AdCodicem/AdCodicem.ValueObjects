# 12. Version the packages in two trains, independently of the frameworks

Date: 2026-10-19

## Status

Proposed. Once accepted:

- **Supersedes** [ADR-0010](0010-version-every-package-in-lockstep-independently-of-dotnet.md). Its ladder for a
  framework's next major, its compatibility island and its floors with no upper bound on the frameworks stand, restated
  below train by train.
- **Amends** [ADR-0003](0003-hybrid-release-manual-stable-continuous-preview.md): the stable track tags each train.
- **Amends** [ADR-0005](0005-version-the-documentation-site.md): the site's lines follow the core train.
- **Amends** [ADR-0009](0009-publish-previews-weekly-when-a-package-input-changed.md): a preview publishes every train
  at its own version, the core first, rather than every package at one.
- The CI that gates each release is
  [ADR-0013](0013-run-a-targeted-pull-request-ci-and-the-heavy-jobs-before-a-release.md).

## Context

ADR-0010 put twelve packages under one version. There are now thirty-eight:

- Twenty-six were added between 2026-10-10 and 2026-10-18, each on the then current major of its framework.
- The last stable release, 0.2.1 (2026-09-30), covers the twelve.
- 322 commits have landed since, and the thirty-eight exist on nuget.org at `0.3.0-preview.322` only.

The core is to promise a 1.0 soon. The satellites sit on frameworks that ship majors on calendars of their own:

- In the twelve months to 2026-10-19, 15 of the 29 tracked dependencies shipped a major, 9 of them outside .NET's
  November release: Wolverine 5 and 6, Orleans 10, ModelContextProtocol 1 and 2, HotChocolate 16, Marten 9, xunit.v3 4.
- Under one version, a floor that such a major forces up is a breaking change of every package. While the major is 0
  that is a minor (`.releaserc.json`'s first `releaseRules` entry, `{ "breaking": true, "release": "minor" }`); from 1.0
  on it is a major of the core.
- One number cannot both hold a stable core and let twenty-eight integrations follow their frameworks.

Nothing in NuGet enforces the lockstep anyway:

- `dotnet pack` writes a `ProjectReference` as a floor with no upper bound: the Marten package asks for
  `AdCodicem.ValueObjects.Json` at its own version or later.
- A project that mixes `0.3.0-preview.283`, `0.2.1` and `0.1.0-preview.2` restores without a warning.

What holds the core together, and what crosses its edge:

- **The generated code calls `ValueObjectJsonRegistry.Register` and `EntityIdRegistry.Register`**
  (`RegistrationEmitter.cs:96,177`), so the generator, `Abstractions`, `Json` and `Identifiers` move together.
- **No `ProjectReference` goes from a core project**, as listed below, to another package.
- **The core's analyzers know four satellites.** They name satellite APIs by string: `ValueObjectProtoSerializer` of the
  ProtobufNet package, and the Orleans and MessagePack registration methods
  (`SerializerContractAnalyzer.cs:45,48,54,250`). VO0048 carries Marten 9.50's query internals; it is in the core on
  purpose, because it reports whether or not the compilation references the satellite
  (`MartenCompiledQueryAnalyzer.cs:46`).
- **Shared source files.** Thirteen files of `src/Shared` are linked 51 times into 26 projects. Nine of them are linked
  on both sides of the edge.

A prototype on a worktree of `41ba247`, with SDK 10.0.402, measured what two trains would do:

- **Attribution.** Each of the 322 commits since `v0.2.1` was attributed to the packages whose inputs it changes. Of the
  177 commits that release something:
  - 104 change the core alone;
  - 25 change the other packages alone;
  - 40 change both;
  - 8 change nothing that ships.

  Today, each of the 177 releases all thirty-eight packages. All five weekly preview windows between `0.3.0-preview.172`
  and `.322` changed both sides.
- **Tags.** MinVer takes a tag prefix per project. With a `satellites-v0.2.1` tag on `68abdc7`, the commit of `v0.2.1`,
  the satellites version at `0.2.2-preview.0.324`; without that tag, at `0.0.0-preview.0.411`, with no warning.
- **Bounded dependencies reach the nuspec.** A target run after NuGet's `_GetProjectReferenceVersions` rewrites the
  `ProjectVersion` of each reference, and the pack task writes it as a range: `AdCodicem.ValueObjects.Json [1.2.0,
  2.0.0)` in the nuspec of Marten 0.4.0.
- **What a consumer sees.** A consumer pairing that package with Json 2.0.0 gets NU1608. With `2.0.0-preview.1`,
  nothing: that prerelease sorts below 2.0.0.
- **Prereleases and upper bounds.** A stable package packed against a prerelease of ours fails with NU5104, an error
  under `TreatWarningsAsErrors`. A stable package with the upper bound `2.0.0-0` fails the same way. A prerelease
  package with that bound packs, and its consumer gets NU1608 on a 2.0 prerelease.
- **Bounding the satellites' references alone is not enough.** It leaves the core's own references bare:
  `AdCodicem.ValueObjects` 2.0.0 next to `AdCodicem.ValueObjects.Bogus` 0.4.0 restored `Testing.Data` 1.2.0 over
  `Abstractions` 2.0.0 without a warning.
- **Version computation.** semantic-release 25.0.9, with a local plugin that filters commits by train, found 185 commits
  for the core and 97 for the satellites in a dry run, and 0.3.0 for both. These are the versions `next-version.mjs`
  computes with the same filter.

## Decision

We will release the packages in two trains. Each train has one SemVer version, driven by its own API and never aligned
with a framework's, and the core is always released first. A framework's next major is supported in the same packages,
train by train, with no maintenance line.

- **Two trains.**
  - `core`: `AdCodicem.ValueObjects` (with the generator and the code fixes inside), `Abstractions`, `Json`,
    `Identifiers`, `Testing.Data`, `AspNetCore`, `AspNetCore.Http`, `EntityFrameworkCore`,
    `Identifiers.EntityFrameworkCore` and `OpenApi`. That is ten packages, eleven once the contract kit's kernel ships
    (below).
  - `satellites`: every other package, twenty-eight today.
  - A core project never references a satellite. A satellite may reference the core and other satellites.
- **One list.** `src/release-trains.json` names the core's projects, and every other packable project rides the
  satellites.
  - MSBuild reads the file through `src/Directory.Build.props` and sets `ReleaseTrain`, `MinVerTagPrefix`, and the
    version from `RELEASE_TRAIN_VERSIONS` (`core=X satellites=Y`), which replaces `MINVERVERSIONOVERRIDE`.
  - A project that cannot find the file fails its evaluation rather than default to a train.
  - One script module, `.github/scripts/release-trains.mjs`, answers "which trains does this change release". The
    version computation, the semantic-release plugin, the preview gate and the pull-request plan all import it.
  - The list lives under `src/` because the preview gate evaluates older commits from a `git archive` of `src` and the
    root build files only.
- **Tags.**
  - Core: `v<version>`. This keeps `v0.1.0`…`v0.2.1` as its history, and every `^v[0-9]` pattern of the scripts keeps
    working.
  - Satellites: `satellites-v<version>`; any later train: `<train>-v<version>`.
  - `satellites-v0.2.1`, placed by hand on `68abdc7`, is the satellites' baseline, as `v0.1.0` was the first one
    (ADR-0003).
  - Each train has one changelog (`CHANGELOG.md`, `CHANGELOG.satellites.md`), one GitHub Release and one provenance
    bundle per release. "Latest" is the core's.
- **A commit releases the trains whose package inputs it changes.** The package inputs, and the trains they release:
  - a project's folder: that project's train;
  - a file of `src/Shared`: every train with a project that links it;
  - a `<PackageVersion>` of `Directory.Packages.props`: every train whose restore graph holds that package; any other
    change to that file: every train;
  - the global build inputs, for every train: the root `Directory.Build.*`, `src/Directory.Build.*`, `global.json`,
    `nuget.config`, `.gitattributes`, `icon.png`, `src/PackageReadme.md`, `src/release-trains.json` and
    `release-pack.sh`;
  - the solution filter: the train of a project it adds or removes.

  Tests, documentation and workflows release nothing, and neither does the repository's `README.md`, which stops being
  embedded. The main package carries a README of its own; every other package carries `src/PackageReadme.md`.

  The commit's type sets the release type in each train it touches, as `.releaserc.json` rates it. A releasing commit
  changes one train, `src/Shared` and the global inputs aside. A core pull request may carry, as separate commits, the
  satellite adaptations that the cross-package guards require of it.
- **Every dependency on one of our own packages is bounded** by the next version that may break it, within a train or
  across trains.
  - From 1.0 on the bound is `[V, (M+1).0.0)`, and while the major is 0 it is `[V, 0.(m+1).0)`, where V is the version
    packed against.
  - A prerelease package closes the range at `-0` (`[1.2.0, 2.0.0-0)`), which keeps a prerelease of the next breaking
    version out. A stable package cannot, because of NU5104.
  - The target `_BoundOwnPackageDependencies` writes these ranges.
  - `verify-packages.sh` refuses a nuspec whose dependency on `AdCodicem.ValueObjects*` has no upper bound, or names a
    version that is neither in the run's packages nor listed by nuget.org.
- **The core is released first.**
  - A stable release evaluates both trains on one commit, the core first, and pushes the core before the satellites.
  - When the core has nothing to release, the satellites are packed against its last stable version. That only works if
    the core built at that commit has exactly that version's API, so strict package validation against it is run; if the
    API differs, the run fails and asks for a core release.
  - A stable satellite is never packed against a prerelease of the core: NU5104 refuses it.
  - If one train fails after the other was released, a new dispatch completes it, and the attestation of each released
    train runs on its own.
- **The satellites break when the core's line moves.** The line is the core's major, or `0.<minor>` while the major is
  0.
  - A satellites version packed against a new line is a breaking version, even with no breaking commit of its own,
    because its bound excludes the new core.
  - One function computes a train's release type, with its breaking rule and this line rule. The version computation and
    the semantic-release plugin both use it.
  - The core's version is always passed in, never guessed from a tag.
- **0.x, then 1.0, train by train.**
  - Each train holds its breaking rule in `src/release-trains.json`: `minor` while it is 0, `major` from its 1.0 on. The
    graduation pull request sets `major` for that train alone.
  - The core goes first. The satellites stay 0.x and go to 1.0 together, later.
- **Frameworks: a floor with no upper bound, train by train.** A dependency on a package we do not own stays a floor at
  the version built against, with no upper bound, as [Microsoft's library
  guidance](https://learn.microsoft.com/dotnet/standard/library-guidance/dependencies#nuget-dependency-version-ranges)
  asks.
  - **.NET.** Every package targets the oldest LTS of .NET that Microsoft still supports: `net10.0`, until 2028-11-14.
    So a package runs on the current .NET and on that LTS at once. The floor moves to `net12.0` when .NET 10 leaves
    support, as a breaking change of both trains on the same day.
  - **Rung 0, then rung 1.** One target framework, with floors on the current majors. A second target framework is added
    only when the code has to differ for a framework major **and** that major has a target framework of its own:
    `OpenApi` on `Microsoft.OpenApi` 3, for instance. Adding a target framework is a minor.
  - **No target framework ahead of need.** A `net11.0` group asking for EF Core 11.0.0 fails with NU1107 beside Npgsql's
    provider 10.
  - **A framework with no LTS** (Marten, Wolverine, HotChocolate, Orleans, ModelContextProtocol, xunit.v3,
    Swashbuckle…):
    - the floor stays on its current major for as long as the compatibility tests pass on the next one;
    - when one assembly can no longer serve both majors, the floor moves within four weeks of the next major's general
      availability, as a breaking change of that package's train;
    - users of the previous major keep the version before the move.
  - **Roslyn.** The generator stays built against `Microsoft.CodeAnalysis.CSharp` 4.14.0 throughout 1.x. If it needs a
    later API, it ships one analyzer per Roslyn version under `analyzers/dotnet/roslyn<major>.<minor>/cs`, and never
    next to `analyzers/dotnet/cs`, which would load both.
  - **No maintenance line, no package per framework major, no version aligned with a framework's.** `SECURITY.md`
    supports the latest stable release of each train.
  - **The compatibility island.** `tests/Compat` installs both trains as a consumer does, each at its exact version,
    from `artifacts/packages` alone, on the next .NET. It runs before every preview and stable release (ADR-0013).
  - **Dependabot.**
    - It ignores the semver-major updates of every dependency a package ships.
    - It also ignores the semver-minor updates of `Microsoft.CodeAnalysis.CSharp*`, whose `major.minor` is the oldest
      compiler that loads the generator.
    - Families whose packages require one another exactly (`xunit.v3*`, `Microsoft.CodeAnalysis*`, `HotChocolate*`,
      `ModelContextProtocol*`) update in groups of their own.
    - A weekly check lists the majors that it holds back.
- **The contract kit splits along the trains.**
  - A framework-free kernel, `AdCodicem.ValueObjects.Testing.Contracts`, joins the core next to `Testing.Data`.
  - `AdCodicem.ValueObjects.Testing` stays the xunit.v3 adapter, in the satellites. Its users change nothing, and
    xunit's majors stay out of the core.
  - The adapter runs the kernel's checks as one theory, a row per check, so that a check the kernel adds in a minor
    shows up with no new version of the adapter. A check that can fail a contract that passed before is opt-in in a
    minor, and on by default only in a major of the core.
- **Families.** After their common 1.0, satellites may leave in families of their own, each with a `<family>-v` tag and
  a changelog. A family leaves when one of these holds:
  - its framework forced two breaking versions in twelve months;
  - it reads internals of a framework whose next major is in preview;
  - a co-maintainer takes it;
  - it held a satellites release back more than fourteen days.

  A package moves to another train only when that train's next version is above the package's last published one.
- **Co-maintainers and retirement.** Co-maintainers own satellites through `CODEOWNERS`, and the maintainer alone
  publishes. A satellite that is obsolete, little used or too costly is deprecated on nuget.org, never unlisted, and
  removed in a later version of its train. Each retirement records its measurements in a record of its own.
- **The documentation site's lines follow the core** (`0.3.x`, then `1.x`), and each line names the satellites version
  it describes.

### Rejected

- **One version for all thirty-eight (ADR-0010).** A satellite's floor move would break the core's version, and a 1.0
  core could not coexist with 0.x satellites.
- **A version per package.** That is 38 lines of tags, changelogs, ApiCompat baselines and releases, plus a
  compatibility matrix to test, for one maintainer. The five weekly windows measured touched both trains anyway.
- **A repository for the satellites.** The cross-package guards (`GeneratedSurface`, `RuntimeClosingTests`,
  `DocumentationSnippetTests`, `SkillCoverageTests`) and the per-folder `CLAUDE.md` files assume one tree. Renaming a
  satellite API that the core names by string would also break the core silently.
- **release-please with `linked-versions`.** Its components are folders, so a satellites version would take 28 tags. It
  cannot attribute one `src/Shared` file to several packages, has no .NET workspace, and replaces the manual stable
  dispatch with release pull requests.
- **The bound `[1.0.0, 2.0.0)` taken literally, a floor of 1.0.0 for every satellite.** A satellite built against an API
  added in 1.2 would declare itself compatible with 1.0 and fail at run time, with no restore error.
- **Bounding the satellites' references only, or bounding at the next major while the major is 0.** As measured above, a
  core 2.0 then restores silently next to a satellite built for 1.x, and a breaking core 0.4 enters a 0.3 range.
- **An upper bound of `2.0.0-0` on stable packages.** NU5104.
- **The train list in `.github/`, read by a regular expression; or a property in each project file.** The first fails
  the preview gate's evaluation of older commits and depends on the order of the JSON keys. The second is a second
  registry that can drift from the first.
- **Following a framework's major, a package per framework major, maintenance branches, or targeting a framework ahead
  of need.** All rejected for the reasons given in ADR-0010.

## Consequences

Consumers read two numbers, and the floor of a satellite is usually the latest core:

- A project with one Central Package Management property for every package needs two (`AdCodicemCoreVersion` and
  `AdCodicemSatellitesVersion`) from the first release where the trains differ. The README says so, and the support
  table gains a train column.
- A satellite's floor is the core it was packed against, usually the latest core release. Updating a satellite therefore
  usually updates the core: a project that pins an older core directly gets NU1605.
- Inside the satellites train, all packages move together. `AspNetCore.NewtonsoftJson` 0.4.0 next to `NJsonSchema` 0.5.0
  fails with NU1107, by design.

NuGet does not keep the trains consistent; the release order and the bounds do:

- NU1608 is only a warning for a consumer who does not treat warnings as errors.
- A 2.0 prerelease of the core enters a stable satellite's range silently.
- The README must not claim that NuGet enforces the core's range.

A commit that changes both trains releases both:

- Once a train is 1.x, a breaking commit in a `src/Shared` file that the train links breaks that train too. So a core
  change and the satellite adaptations it needs land as separate commits.
- Weekly previews still publish both trains while both are being built. The gain is on stable releases, and it grows
  once the core is frozen.

The release chain grows and loses its single-version assumptions:

- It gains one list, one script module and one semantic-release plugin.
- It loses the single-version assumptions of `release-pack.sh`, `verify-packages.sh`, `preview-gate.sh` and
  `release.yml`.
- A contributor needs six lines of the root `CLAUDE.md` to follow the per-train rules.

The rules of ADR-0010 that stand still apply, now train by train: floors, the ladder, the island and Dependabot's major
ignores. Two changes follow from the ignores:

- The ignores now cover every shipped dependency, and Roslyn's minors too. A security fix published only on a new major,
  or on a new minor of Roslyn, therefore opens no pull request. The weekly check and GitHub's alert surface it.
- ADR-0010's consequence that "a minor of Roslyn auto-merges as `fix(deps)`" no longer holds.

The core's analyzers keep naming satellite APIs by string. A test compiled against the real satellite assemblies guards
each name, and Marten 10 may need a core patch to VO0048.

The contract kit's kernel adds a package, so there are thirty-nine.

The 1.0 of the core is a release of its own, after the first release under this record and after the core's public API
is tracked. Until then, an API that is not yet ready for the 1.0 promise carries `[Experimental]`.
