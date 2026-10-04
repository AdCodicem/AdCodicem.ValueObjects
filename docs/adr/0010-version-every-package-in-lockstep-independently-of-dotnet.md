# 10. Version every package in lockstep, independently of .NET

Date: 2026-10-03

## Status

Accepted

## Context

The twelve packages are released together: semantic-release computes one version from the Conventional Commits,
`release-pack.sh` packs every packable project at it, and the GitHub Release links all twelve. Several of them sit on
a framework that ships a major every November. `AdCodicem.ValueObjects.EntityFrameworkCore` and
`AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore` depend on Entity Framework Core,
`AdCodicem.ValueObjects.AspNetCore` on ASP.NET Core, `AdCodicem.ValueObjects.OpenApi` on
`Microsoft.AspNetCore.OpenApi` and on `Microsoft.OpenApi`, which has majors of its own, and the generator is compiled
against Roslyn. .NET 11 is at its first release candidate, published on 2026-09-08; its release is expected around
2026-11-10, a date not confirmed.

What a package asks of its dependencies is a floor. `CentralPackageTransitivePinningEnabled` makes every package
`Directory.Packages.props` pins a direct dependency of the package that pulls it, so each nuspec names the central
version as its minimum, with no upper bound: `Microsoft.EntityFrameworkCore` 10.0.12 or later, for instance. Every
bump of a shipped dependency raises a floor.

Libraries in the same position answer the next framework major in two ways:

- **Follow its major.** Npgsql's EF Core provider, Pomelo's and `EFCore.NamingConventions` number their releases after
  EF Core's and bound it: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 asks for EF Core `[10.0.4, 11.0.0)`,
  `EFCore.NamingConventions` 10.0.1 for `[10.0.1, 11.0.0)`. Their users wait for a new major of theirs at each
  EF Core major. They are providers, built on EF Core's internals.
- **Keep a version of their own.** Vogen and StronglyTypedId do. So does Thinktecture.Runtime.Extensions, but it
  ships one EF Core package per EF Core major: `Thinktecture.Runtime.Extensions.EntityFrameworkCore8`, `…9` and
  `…10`, all at 10.5.0.

What the packages built for .NET 10 do on .NET 11 was measured rather than assumed. A test project outside the
solution, `tests/Compat`, installs the twelve packages as a consumer does into a `net11.0` application on SDK
11.0.100-rc.1: the generator ran in that SDK's compiler (Roslyn 5.11), and the 53 tests that need no container
passed, over Entity Framework Core 11 on SQLite, System.Text.Json source generation, ASP.NET Core model binding,
`Microsoft.AspNetCore.OpenApi` 11, FluentValidation, Dapper, Newtonsoft.Json and the contract kit. Its PostgreSQL tests
passed against a local server; its SQL Server tests run only in CI. Yet the frameworks do break their binary
interface between majors: ApiCompat reports 230 breaking differences between `Microsoft.EntityFrameworkCore` 10.0.12
and 11.0.0-rc.1, 97 for `Microsoft.EntityFrameworkCore.Relational`, and 31 between `Microsoft.OpenApi` 2.12.2 and
3.10.0, the version `Microsoft.AspNetCore.OpenApi` 11 brings. None of them is on a path the integrations take: the
schema transformer, compiled against `Microsoft.OpenApi` 2, wrote every keyword it writes on 3.10.0. System.Text.Json
reports no breaking difference from one major to the next, 6.0 to 10.0.

## Decision

We will release the twelve packages under one SemVer version, driven by our own API alone and never aligned with the
major of .NET or of EF Core, and support a framework's next major in the same packages, never through a package per
framework major.

- **One version for the twelve.** A consumer references the same version of each.
- **Support is stated, not encoded in package IDs.** Each package's target framework, `net10.0` today, says where it
  installs: on .NET 10 or any later version. Its dependencies are minimums with no upper bound, as Microsoft's
  [library guidance](https://learn.microsoft.com/dotnet/standard/library-guidance/dependencies#nuget-dependency-version-ranges)
  recommends: an upper bound fails a restore that pairs the package with anything newer, breaking or not. The
  [support table](../../README.md#supported-frameworks) names, for each package, the framework majors it is built and
  tested against and what the compatibility job checks on the next one. It names majors; the exact minimum of each
  dependency is the package's dependency list on nuget.org.
- **A ladder, climbed only as far as the code requires.**
  - **Rung 0**, where every package is today: one target framework, floors on the current majors. An application on
    a newer framework takes the newer major itself, and the packages run on it.
  - **Rung 1**: the same package targets a second framework, only when its code has to differ for a framework major
    **and** that major has a target framework of its own. `AdCodicem.ValueObjects.OpenApi` is the only candidate:
    `Microsoft.AspNetCore.OpenApi` 11 is for `net11.0` only and brings `Microsoft.OpenApi` 3, so a transformer that
    needed different code for it would carry it in a `net11.0` target.
  - **No target added ahead of need for the EF Core packages.** A `net11.0` dependency group would ask every `net11.0`
    project for EF Core 11, and one that stays on EF Core 10 with Npgsql's provider 10, which caps EF Core below 11,
    would fail to restore with NU1107.
- **When a major cannot be told apart by target framework**, a new major of a dependency on the same target
  framework, the floor stays on the old major for as long as the compatibility job passes on the new one. When a
  binary break means one assembly can no longer serve both, the floor moves to the new major. That is a breaking
  change of ours, released as one, and users of the old major stay on the version before it: there is no maintenance
  line.
- **The compatibility island.** `tests/Compat` holds the next .NET major's application: its own `global.json`, naming
  the release candidate's SDK, which CI installs exactly (`setup-dotnet` ignores `rollForward` for a prerelease
  version) and which `rollForward: latestFeature` lets a later 11.0 SDK stand in for locally, its own
  `Directory.Build.props` and `Directory.Packages.props`, with no transitive pinning, and a `nuget.config` that takes
  the twelve packages from `artifacts/packages` alone, at exactly the version just packed. It is in no solution. The
  `compat (.NET 11)` job of `ci.yml` runs it on every pull request and push to `main`, against the packages the build
  job packed, with PostgreSQL 17 and SQL Server in containers. It informs and blocks nothing until .NET 11 ships,
  then becomes a required check.
- **A breaking change bumps the minor while the major is 0.** The first rule of the commit analyzer's
  `releaseRules` in `.releaserc.json` is `{ "breaking": true, "release": "minor" }`: a `feat!`, a `fix(deps)!` or a
  `BREAKING CHANGE:` footer releases `0.y+1.0`. At 1.0 the rule is **replaced** by
  `{ "breaking": true, "release": "major" }`, not deleted: without a breaking rule of its own, `build(pack)!` and
  `docs(readme)!` would fall back to the patch their scope is rated. Before 1.0.0, a minor may therefore break,
  deprecate or remove part of the public API, deprecated members included: the deprecated `Pattern`, `Minimum` and
  `Maximum` options say that any minor version may remove them before 1.0.0. *(Amended: this bullet first promised
  that a deprecated option would stay until 1.0.0.)* Only from 1.0.0 on does a breaking change wait for a major.
- **Dependabot follows the same lines.**
  - A new major of a framework is supported by a decision, a floor or a target framework, never by a bump: the
    `nuget` entry ignores semver-major updates of `Microsoft.EntityFrameworkCore*`, `Microsoft.AspNetCore.*`,
    `Microsoft.Extensions.*`, `Microsoft.OpenApi` and `Npgsql.EntityFrameworkCore.PostgreSQL`. Dependabot's NuGet
    updater applies those rules to security updates too, so a security fix that exists only on a new major of one of
    them opens no pull request: its alert stays open, and it is handled as a deliberate migration under this record.
    A fix published on the current major opens at once.
  - The `nuget`, `npm` and `github-actions` entries propose a version once it has been public for seven days
    (`cooldown`); security updates are not delayed.
  - A bump of a dependency the packages ship has to release a patch. Dependabot takes one `nuget` entry per directory,
    so that entry keeps the `chore(deps)` prefix, and `dependabot-auto-merge.yml` retitles the pull request
    `fix(deps): …` when one of the dependencies it updates is listed in `.github/shipped-dependencies`, and
    `fix(deps)!: …` when that update is a semver-major. The list holds the eleven dependencies the nuspecs name, plus
    `Microsoft.CodeAnalysis.CSharp` and `PolySharp`, compiled into the generator the main package carries; not MinVer,
    SourceLink or `Microsoft.CodeAnalysis.Analyzers`, which shape the build but not what ships. It names exact IDs,
    and a step of `ci.yml`, run on the packed nuspecs, fails when the two drift apart. Every other NuGet update keeps
    `chore(deps)`, and the other ecosystems keep the prefix `dependabot.yml` gives them, `ci(deps)` or `chore(deps)`;
    none of them releases anything. A Dependabot pull request is squash-merged, so that its title is the commit
    semantic-release reads: auto-merge does so already, and a pull request merged by hand must be squashed too.

### Rejected

- **A package per framework major**, as Thinktecture does for EF Core. The API would be the same in each, a consumer
  would have to pick the right one, and every major would add a package ID to publish for good.
- **A major aligned with the framework's**, as the EF Core providers do. The version would stop saying anything about
  our own API: a breaking change of ours would wait for November or break the alignment. A provider is built on EF
  Core's internals; these integrations call its public surface.
- **Targeting the next framework ahead of need**, for the NU1107 above.
- **Upper bounds on dependencies.** They would stop an application from taking the next major even where nothing
  breaks, which is most of the time.
- **Maintenance branches for an older framework line.** Stable releases are cut from `main` only, and one maintainer
  cannot carry two lines.

## Consequences

A floor rises with every bump of a shipped dependency, so the support table names majors and leaves the exact
minimum to nuget.org, where it cannot drift.

An application held on an older framework major gets no fixes once the floor moves: it stays on the last version
before the move. `SECURITY.md` says the same: only the latest stable release receives fixes.

The island is maintained by hand. Dependabot finds projects through `AdCodicem.ValueObjects.slnx` and never sees it,
and its NuGet image has no .NET 11 SDK. Each release candidate is a pull request that moves the island's
`global.json` and its .NET 11 packages together, Npgsql's provider with EF Core, since it pins EF Core exactly; the
release of .NET 11 moves them to 11.0.0 and makes the job required (`docs/maintaining.md`). When .NET 12 previews
arrive, moving the island to the next major, or adding a second one, is a decision of its own, and renaming the job
changes the required check.

Tools that walk the repository rather than the solution do see the island. CodeQL's default setup downloads every SDK
a `global.json` names and analyses its code. GitHub's automatic dependency submission restores every project file
from the root with the .NET 10 SDK, so the island's project file leaves itself empty on an SDK that cannot target
`net11.0`, and its restore succeeds with nothing to submit.

`Microsoft.CodeAnalysis.CSharp` is on the shipped list, so a minor of Roslyn auto-merges as `fix(deps)` and raises the
compiler the generator needs, and a major is titled `fix(deps)!` and left for a human. A grouped Dependabot pull
request that mixes shipped and test-only dependencies becomes one `fix(deps)`. One merged with a rebase keeps
Dependabot's `chore(deps)` commit and releases nothing.
