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

The integration suite starts real PostgreSQL, SQL Server and MongoDB containers, and the Azure Functions host, through
[Testcontainers](https://dotnet.testcontainers.org/), so Docker must be running. Without it, run the three suites
that do not need it, the unit suite as one project per package family, which `.github/ci-families.json` names in each
family's `tests` (the loop reads it with [jq](https://jqlang.org/)):

```bash
for name in $(jq -r '.families[].tests' .github/ci-families.json); do
  dotnet test --project "tests/$name"
done
dotnet test --project tests/AdCodicem.ValueObjects.GeneratorTests
dotnet test --project tests/AdCodicem.ValueObjects.RdgTests
```

The core family's project is `tests/AdCodicem.ValueObjects.UnitTests`, every other family's
`tests/AdCodicem.ValueObjects.UnitTests.<Family>`. To run one family, or one test on the project that holds it:

```bash
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests.Text
dotnet test --project tests/AdCodicem.ValueObjects.UnitTests.Text --filter-method "*The_name_of_the_test*"
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
release on their own, with two exceptions that bump the patch because they change what ships inside every
package: `build(pack)` for the package metadata, and `docs(readme)` for the README, which is also each package's
page on nuget.org. A breaking change is marked with `!` after the type, or a `BREAKING CHANGE:` footer. It bumps
the minor while the version is 0.x, and the major from 1.0 on: before 1.0.0, a minor release may break, deprecate
or remove part of the public API, and only from 1.0.0 on does a breaking change wait for a major.

```
feat(generator): emit a span-based TryParse for numeric underlying types
fix(descriptor): keep the rule that rejected the value instead of not_parsable
build(pack): point packages at the documentation site
feat!: drop the implicit conversion to the underlying type
```

Dependabot's NuGet pull requests follow the same rule: one that bumps a dependency the packages ship is retitled
`fix(deps):`, or `fix(deps)!:` for a new major, and every other one stays `chore(deps):`. The other ecosystems keep
the prefix `.github/dependabot.yml` gives them, none of which releases anything: `ci(deps):` for the release
tooling, the commit-message linter and the actions, and `chore(deps):` or `chore(deps-dev):` for the site's
packages. Every Dependabot pull request is squash-merged, so that its title is the commit that lands.

Both a local hook and a CI check validate this. Install the local one once:

```bash
pip install pre-commit && pre-commit install
```

## Pull requests

1. Branch from `main`.
2. Keep it focused — one logical change.
3. Keep it to 100 commits or fewer. `main` takes a linear history, and a pull request lands with *Rebase and merge*
   so that each Conventional Commit reaches the changelog. GitHub refuses to rebase more than 100 commits, and its
   web UI reports that as a conflict that does not exist. Split larger work into pull requests stacked on one
   another.
4. Add or update tests. The four suites, the unit suite split into a project per package family, have distinct jobs,
   described in [Testing](https://adcodicem.github.io/AdCodicem.ValueObjects/docs/testing); a defect confined to the
   descriptor is invisible to the typed-path unit tests, so check which surface your change actually touches. Codecov
   reports on each pull request that runs the suites: 95 % of the lines it changes covered, the project's coverage down
   by half a point at most. That is a floor, and 100 % of the patch is the aim — but coverage is a signal, not a goal: a
   defensive branch no test reaches stays, a private member is covered through its callers or not at all, and code goes
   only when no input can reach it. The full rule is in [ADR-0006](docs/adr/0006-coverage-is-a-signal-not-a-goal.md).
5. Run `dotnet format whitespace . --folder` before pushing — CI checks it with `--verify-no-changes`, and the build
   holds the code to the style rules.
6. Open the PR and fill in the template.

Three checks must pass before a pull request merges: **build and test** (`ci.yml`), which fails unless every job the
run's plan selects succeeds; **workflows** (`lint.yml`), which runs [actionlint](https://github.com/rhysd/actionlint)
over every workflow, the ones that never run on a pull request included; and **conventional commits** (`lint.yml`),
which checks every commit message of the pull request. The plan reads the files a pull request changes
([ADR-0013](docs/adr/0013-run-a-targeted-pull-request-ci-and-the-heavy-jobs-before-a-release.md)): a change to a
package runs the unit tests of its family and of every family whose project reaches it, a change to the core packages
every family and the generator suite, and the build, the RDG suite, the compiled model and the pack run when their
inputs change; a change to the documentation runs the generator suite's tests that read it, and a change to `ci.yml`,
a global build file or a file no rule names, everything. Its summary on the run page lists what it picked. `ci.yml`
also builds the documentation site when a change reaches it, in a job that is worth a look when red but does not block
the merge.

The heavy jobs run in `heavy.yml`, after the merge on `main`, before every preview and stable release, and on a pull
request labelled `ci:heavy`, where **build and test** waits for them: **native AOT**, which publishes the applications
of `tests/NativeAot` with native AOT and fails on a trimming or AOT warning, the known ones of
protobuf-net's generated code and of Microsoft.AspNetCore.Components aside (each application's `aot-warnings.allowed`),
or on any difference between the native binary and the JIT; the integration suite; and the packages in a .NET 11 application. Ask for the label when your change touches
native AOT, a store, the sample API or the next .NET; the label is read when a run starts, so push after adding it.
`CLAUDE.md` has the command to run actionlint locally, and `tests/NativeAot/CLAUDE.md` those of the native AOT scripts,
which need clang and zlib.

Releases are cut manually by the maintainer from `main`, so a merged pull request publishes nothing by itself: if it
changes what ships in a package, the next weekly preview carries it, and the next stable release ships it.
