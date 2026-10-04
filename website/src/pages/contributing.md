---
title: Contributing
description: How to build, test and change AdCodicem.ValueObjects, its agent skill and this site.
---

# Contributing

## Repository layout

```
src/          the shipped packages
tests/        unit tests, generator tests, integration tests on real database engines, and minimal APIs bound by the RDG
  NativeAot/  applications CI publishes with native AOT and compiles an EF Core model for
  Compat/     the packed packages in a .NET 11 application, outside the solution
samples/      a showcase API exercising the whole chain end to end
benchmarks/   the measurements behind the design decisions
skills/       the agent skill, distributed as a Claude Code plugin through .claude-plugin/
website/      this documentation site
```

## Building

```bash
dotnet build
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests        # no Docker needed
dotnet test --project tests/AdCodicem.ValueObjects.GeneratorTests   # no Docker needed
dotnet test --project tests/AdCodicem.ValueObjects.RdgTests         # no Docker needed
dotnet test                                                          # everything, Docker required
dotnet pack -c Release
```

Integration tests start PostgreSQL and SQL Server through Testcontainers, so they need a Docker daemon.

`tests/Compat`, the compatibility island, is not part of the solution and is run from its own folder, with the
.NET 11 SDK its `global.json` names, against packages you have just packed. The commands are on
[How the library is tested](/docs/preview/testing#commands), and the island itself is described there too.

## Coverage

CI uploads the coverage of all four suites to [Codecov](https://codecov.io/gh/AdCodicem/AdCodicem.ValueObjects),
which reports on each pull request against two floors set in `codecov.yml`: 95 % of the lines the pull request
changes, a partial line counting as missed, and the project's coverage down by half a point at most. Only `src/` is
measured. The floor is not the aim; 100 % of each patch is, under a rule that keeps the number honest
([ADR-0006](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/docs/adr/0006-coverage-is-a-signal-not-a-goal.md)):

- every member that is not private is covered as far as it can be — through a natural input where one reaches it
  (a declaration compiled through the generator, a call through the public API, a request through ASP.NET Core, a
  database round trip), otherwise by a test that calls it directly;
- a private member is covered through its callers or not at all, never by reflection;
- a branch the compiler adds that no input can take stays partial rather than being rewritten;
- code is removed only when no input can reach it, never because no test does, and a defensive branch stays;
- a public member nothing calls is a question for the maintainer, not a removal.

Two things the figure cannot show. A package no test loads is missing from the report rather than at 0 %, so CI
fails when a project under `src/` is in no report. And the code the generator emits lands in the consumer's
assembly, outside what Codecov measures: the emitters are counted, what they produce is audited by hand.

## Commit hygiene

```bash
pip install pre-commit
pre-commit install
```

installs a `pre-commit` and a `commit-msg` hook that also run in CI:

- committed files must stay usable on a case-insensitive, no-symlink Windows checkout;
- commit messages must follow [Conventional Commits](https://www.conventionalcommits.org/).

A pull request lands with *Rebase and merge*, so that each of its commits reaches the changelog, and GitHub rebases
at most 100 commits: past that it refuses, and its web UI blames conflicts that do not exist. Keep a pull request to
100 commits or fewer, and split larger work into pull requests stacked on one another.

Two checks must pass before a pull request merges: **build and test**, and **workflows**, which runs
[actionlint](https://github.com/rhysd/actionlint) over every workflow, including those that never run on a pull
request. CI also builds this site and runs the packages in a .NET 11 application, in jobs that do not block the
merge.

Nothing you merge publishes a package by itself. A preview of every package goes to nuget.org each week in which
something a package ships has changed, and a stable release is cut by hand; while the version is 0.x, a breaking
change releases a minor, so a minor may break, deprecate or remove part of the public API; only from 1.0.0 on does
a breaking change wait for a major.

## The agent skill

`skills/value-objects/` is what an AI coding agent reads before writing a value object: the attribute options,
the hook interfaces, what the generator already emits, the wiring of each integration, and every `VO00xx`
diagnostic. Consumers install it with `/plugin marketplace add AdCodicem/AdCodicem.ValueObjects`, so it is a
shipped artefact rather than a note to ourselves.

**A change to the surface a consumer writes against goes in the skill in the same commit** — a new option on
`[ValueObject<T>]`, a new hook interface, a new diagnostic, a new error code, a renamed extension method. Two
test classes in `AdCodicem.ValueObjects.GeneratorTests` make that mechanical rather than a thing to remember:

- `DocumentationSnippetTests` runs the generator and the analyzers over every C# snippet the repository
  publishes — the skill, this site, and the README. A skill snippet must compile outright, since an agent copies
  it verbatim; a snippet here is prose and may elide a body, so what is checked is the declaration the generator
  sees. A wiring or usage fragment is tagged ` ```csharp skip `. This is what would have caught the README
  teaching `NormalizeCore` for months after hooks became interfaces.
- `SkillCoverageTests` reflects over the shipped assemblies and fails when an attribute option, a hook member, a
  diagnostic identifier or a well-known error code is missing from the skill.

Keep it prescriptive: the reasoning, the benchmarks and the design decisions belong on this site, and the skill
stays short enough to be worth loading.

## This site

The site itself is a Docusaurus project under `website/`:

```bash
cd website
npm ci
npm run docs:api   # the API reference, generated from the XML doc comments and not committed
npm start          # local dev server with hot reload
npm run build      # production build, fails on a broken internal link
```

Every pull request builds the site the same way, without deploying it, so a broken link fails there.

This page is the one part of the site that is not versioned: it describes how to work on `main`, whichever
release you are reading about.

### Versions

`website/docs/` is the **preview**. It describes `main`, and `preview.yml` redeploys it under `/docs/preview/` every
Monday and whenever it is dispatched, whether or not the run publishes a package, labelled with the version on
nuget.org that describes it. A stable release freezes it into `website/versioned_docs/` — one entry per minor
while the version is 0.x (`0.3.x`), one per major from 1.0 on (`1.x`) — and `/docs/` serves the newest of those. So
a change to `website/docs/` reaches readers of the preview at the next `preview.yml` run, and readers of the stable
documentation with the next release, not before. Until the first stable release exists, `/docs/` serves the
preview.

The release workflow writes `versioned_docs/`, `versioned_sidebars/`, `versions.json` and
`released-versions.json`; nothing else should add or remove an entry.

The navbar, the footer and the homepage are not versioned, but their links to `/docs/<page>` go to the latest
stable line. Renaming or removing one of those pages therefore breaks the build, which fails on a broken link. It
breaks straight away if the link changes in the same pull request, and at the next release if it does not. Such a
rename needs a client redirect.

### Correcting a released version

An error in the stable documentation can be corrected before the next release when it would mislead someone
using that release: a snippet that does not compile against it, an option described as doing something it does
not. Fix it in `website/docs/` **and** in `website/versioned_docs/version-<line>/`, in the same pull request.
Both are needed: the next release of that line replaces its snapshot wholesale with a fresh copy of
`website/docs/`, so a fix made only in the snapshot is lost there. Once merged, the fix goes live at the next
`preview.yml` run, or at once when the maintainer dispatches it.

Anything else waits for the next release, and documentation of a feature that exists only on `main` never goes
into a snapshot. `DocumentationSnippetTests` checks `website/docs/` against the current generator and not the
snapshots, which describe an older one, so a snippet fixed in a snapshot has to be checked by hand.

## Licence

MIT.
