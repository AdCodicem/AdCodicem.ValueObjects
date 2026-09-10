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
