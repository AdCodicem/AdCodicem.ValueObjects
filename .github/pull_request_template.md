## What changed

<!-- One or two sentences: the diff says how, say why. -->

## Checklist

- [ ] `dotnet test tests/AdCodicem.ValueObjects.UnitTests` and `dotnet test tests/AdCodicem.ValueObjects.GeneratorTests` pass (the integration suite needs Docker).
- [ ] A change to the surface a consumer writes against — an option on `[ValueObject<T>]` or `[EntityId]`, a hook
      interface, a diagnostic, an error code, an extension method — is reflected in `README.md`, the matching page
      under `website/docs/`, and the agent skill in `skills/value-objects/`.
- [ ] A new diagnostic is listed in `AnalyzerReleases.Unshipped.md`, or RS2008 fails the build.
- [ ] A benchmark claim in the documentation still matches a run, or it was updated.
