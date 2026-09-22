# Contributing

Thanks for considering a contribution. This project follows GitHub Flow: `main` is always releasable, work
happens on short-lived branches, and changes land through pull requests.

The long-form version of this guide — repository layout, how the generator is structured, how to read generated
code while debugging — lives on the documentation site:
**[Contributing](https://adcodicem.github.io/AdCodicem.ValueObjects/contributing)**. This file is the short
path to a first pull request.

## Setup

```bash
git clone https://github.com/AdCodicem/AdCodicem.ValueObjects.git
cd AdCodicem.ValueObjects
dotnet build -c Release
dotnet test -c Release
```

The integration suite starts real PostgreSQL and SQL Server containers through
[Testcontainers](https://dotnet.testcontainers.org/), so Docker must be running. Without it, run the two suites
that do not need it:

```bash
dotnet test tests/AdCodicem.ValueObjects.UnitTests
dotnet test tests/AdCodicem.ValueObjects.GeneratorTests
```

`TreatWarningsAsErrors` is on repository-wide — a warning fails the build, including in your editor.

## What a change usually touches

A value object's rules are declared once and carried into JSON, the database, model binding and the OpenAPI
document. That means a change to the surface a consumer writes against — an option on `[ValueObject<T>]` or
`[EntityId]`, a hook interface, a diagnostic, an error code, an extension method — is rarely a one-file change:

- `README.md` and the matching page under `website/docs/`.
- `skills/value-objects/`, the consumer-facing agent skill. `SkillCoverageTests` fails the build when it falls
  behind, and `DocumentationSnippetTests` compiles every published C# snippet through the real generator.
- `AnalyzerReleases.Unshipped.md` for a new diagnostic, or RS2008 fails the build.

The pull request template lists this as a checklist; it is there because these genuinely do drift apart.

## Commit messages — Conventional Commits

Version numbers and the changelog are computed from commit history, so commit messages must follow
[Conventional Commits](https://www.conventionalcommits.org/):

```
<type>[optional scope]: <description>
```

`feat` bumps the minor version, `fix` bumps the patch. `docs`, `refactor`, `test`, `chore` and `ci` trigger no
release on their own. A breaking change is marked with `!` after the type, or a `BREAKING CHANGE:` footer, and
bumps the major.

```
feat(generator): emit a span-based TryParse for numeric underlying types
fix(descriptor): keep the rule that rejected the value instead of not_parsable
feat!: drop the implicit conversion to the underlying type
```

Both a local hook and a CI check validate this. Install the local one once:

```bash
pip install pre-commit && pre-commit install
```

## Pull requests

1. Branch from `main`.
2. Keep it focused — one logical change.
3. Add or update tests. The three suites have distinct jobs, described in
   [Testing](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/testing); a defect confined to the descriptor
   is invisible to the typed-path unit tests, so check which surface your change actually touches.
4. Run `dotnet format` before pushing — CI enforces it.
5. Open the PR and fill in the template.

Releases are cut manually by the maintainer from `main`, so a merged pull request does not publish anything by
itself; it publishes a preview package, and ships in the next stable release.
