## What changed

<!-- One or two sentences: the diff says how, say why. -->

## Checklist

- [ ] `dotnet test --project tests/AdCodicem.ValueObjects.UnitTests` and `dotnet test --project tests/AdCodicem.ValueObjects.GeneratorTests` pass (the integration suite needs Docker).
- [ ] Codecov's patch check holds, under the rule in `docs/adr/0006-coverage-is-a-signal-not-a-goal.md`: what stays
      uncovered is a defensive branch, a compiler branch or a private member no input reaches, and the pull request says so.
- [ ] A change to the surface a consumer writes against — an option on `[ValueObject<T>]` or `[EntityId]`, a hook
      interface, a diagnostic, an error code, an extension method — is reflected in `README.md`, the matching page
      under `website/docs/`, and the agent skill in `skills/value-objects/`.
- [ ] A new diagnostic is listed in `AnalyzerReleases.Unshipped.md`, or RS2008 fails the build.
- [ ] A benchmark claim in the documentation still matches a run, or it was updated.
