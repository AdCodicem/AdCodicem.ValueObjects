# AdCodicem.ValueObjects.AutoFixture

- Stay on AutoFixture 4.18.1 while 5.0 is a release candidate: a stable package depending on a prerelease is NU5104,
  which fails the pack. Dependabot proposes 5.0 as `fix(deps)!` once it ships.
- The specimen builder resolves through the registry: a test meets hand-written value objects only as `Declared<,>`
  constructions over declarations it owns (`tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`).
- The namespace hides the root `AutoFixture` namespace (root `CLAUDE.md`).
