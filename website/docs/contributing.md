---
title: Contributing
sidebar_label: Contributing
slug: /contributing
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

- `SkillDocumentationTests` compiles every C# snippet of the skill through the generator harness and runs the
  analyzers over it, so a snippet that stopped being valid fails the build. A block that cannot compile on its
  own — wiring examples naming packages the test project does not reference — is tagged ` ```csharp skip `.
- `SkillCoverageTests` reflects over the shipped assemblies and fails when an attribute option, a hook member, a
  diagnostic identifier or a well-known error code is missing from the skill.

Keep it prescriptive: the reasoning, the benchmarks and the design decisions belong on this site, and the skill
stays short enough to be worth loading.

## This site

The site itself is a Docusaurus project under `website/`:

```bash
cd website
npm ci
npm start          # local dev server with hot reload
npm run build       # production build, fails on a broken internal link
```

It deploys to GitHub Pages automatically on every push to `main` that touches `website/`.

## Licence

MIT.
