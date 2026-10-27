# AdCodicem.ValueObjects.UnitTests.Text

The text family's unit tests (Blazor), in `Forms/`. The suite's conventions are in
`tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`, the package's traps in `src/AdCodicem.ValueObjects.Blazor/CLAUDE.md`.

- Blazor: `Forms/FormsHarness` adds the services before `SetRendererInfo`: bUnit throws `MissingRendererInfoException`
  when a component reads `RendererInfo` first, and refuses a service added once the renderer is asked for. The Razor
  forms and pages are `Fixtures.Blazor`'s, so that this project stays on `Microsoft.NET.Sdk`; its static and prerendered
  pages are served over TestHost.
- `Forms/EcmaScriptPatterns.json`, copied to the output, is held to .NET here (`EcmaScriptPatternTests`) and to Node by
  `.github/scripts/ecmascript-patterns.mjs`, which the `unit (text)` job of `ci.yml` runs on it: change both together.
- Every sample is held to the Blazor package from `GeneratedSurface/GeneratedSurfaceTests.Text.cs`, a
  `GeneratedSurfaceTests` part of this project's own, over the check of `GeneratedSurface/Sample.Text.cs`.
- It links core's `Web/ValidationMessages.*`, `Web/SparseMessages.*` and `WithoutDynamicCode.cs`, at the same paths.
