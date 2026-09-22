---
title: Contributing
description: How to build, test and change AdCodicem.ValueObjects, its agent skill and this site.
---

# Contributing

## Repository layout

```
src/          the shipped packages
tests/        unit tests, generator tests, and integration tests on real database engines
samples/      a showcase API exercising the whole chain end to end
benchmarks/   the measurements behind the design decisions
skills/       the agent skill, distributed as a Claude Code plugin through .claude-plugin/
website/      this documentation site
```

## Building

```bash
dotnet build
dotnet test tests/AdCodicem.ValueObjects.UnitTests        # no Docker needed
dotnet test tests/AdCodicem.ValueObjects.GeneratorTests   # no Docker needed
dotnet test                                                # everything, Docker required
dotnet pack -c Release
```

Integration tests start PostgreSQL and SQL Server through Testcontainers, so they need a Docker daemon.

## Commit hygiene

```bash
pip install pre-commit
pre-commit install
```

installs a `pre-commit` and a `commit-msg` hook that also run in CI:

- committed files must stay usable on a case-insensitive, no-symlink Windows checkout;
- commit messages must follow [Conventional Commits](https://www.conventionalcommits.org/).

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

This page is the one part of the site that is not versioned: it describes how to work on `main`, whichever
release you are reading about.

### Versions

`website/docs/` is the **preview**. It describes `main`, and every merge that publishes a preview package to
nuget.org redeploys it under `/docs/preview/`. A stable release freezes it into `website/versioned_docs/` —
one entry per minor while the version is 0.x (`0.3.x`), one per major from 1.0 on (`1.x`) — and `/docs/`
serves the newest of those. So a change to `website/docs/` reaches readers of the stable documentation with the
next release, not before. Until the first stable release exists, `/docs/` serves the preview.

The release workflow writes `versioned_docs/`, `versioned_sidebars/`, `versions.json` and
`released-versions.json`; nothing else should add or remove an entry.

### Correcting a released version

An error in the stable documentation can be corrected before the next release when it would mislead someone
using that release: a snippet that does not compile against it, an option described as doing something it does
not. Fix it in `website/docs/` **and** in `website/versioned_docs/version-<line>/`, in the same pull request.
Both are needed: the next release of that line replaces its snapshot wholesale with a fresh copy of
`website/docs/`, so a fix made only in the snapshot is lost there.

Anything else waits for the next release, and documentation of a feature that exists only on `main` never goes
into a snapshot. `DocumentationSnippetTests` checks `website/docs/` against the current generator and not the
snapshots, which describe an older one, so a snippet fixed in a snapshot has to be checked by hand.

## Licence

MIT.
