# AdCodicem.ValueObjects.UnitTests.Text

The text family's unit tests (Blazor, CommandLine, CsvHelper, which share `src/Shared/ValueObjectText.cs`), in `Forms/`
(Blazor), `Cli/` (System.CommandLine) and `Csv/` (CsvHelper). The suite's conventions are in
`tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`, each package's traps in its `src/<Package>/CLAUDE.md`.

- Blazor: `Forms/FormsHarness` adds the services before `SetRendererInfo`: bUnit throws `MissingRendererInfoException`
  when a component reads `RendererInfo` first, and refuses a service added once the renderer is asked for. The Razor
  forms and pages are `Fixtures.Blazor`'s, so that this project stays on `Microsoft.NET.Sdk`; its static and prerendered
  pages are served over TestHost.
- `Forms/EcmaScriptPatterns.json`, copied to the output, is held to .NET here (`EcmaScriptPatternTests`) and to Node by
  `.github/scripts/ecmascript-patterns.mjs`, which the `unit (text)` job of `ci.yml` runs on it: change both together.
- System.CommandLine: `Cli/` parses through `Command.Parse` and invokes through `InvocationConfiguration` with writers
  of its own (`CliHarness.Invoke`), never the console; `CliHarness.Refusals` reads each parse error with its code and
  type. A `RootCommand` is named after the test host, so a test asserting an error that names the command parses a
  `Command("app")`. A command with subcommands and no action answers "Required command was not provided.".
  System.CommandLine converts a parent command's arguments, and its options neither `Recursive` nor with a default
  value, only at `GetValue`; the walk's validator reads them when a subcommand is invoked, so a test of
  System.CommandLine's own behaviour there configures its symbols with `AcceptValueObject` alone and walks no command.
- CsvHelper: `Csv/CsvHarness` reads and writes through `StringReader` and `StringWriter`, a context per read or write,
  so nothing is process-wide; its configuration ends lines with `\n`. A single-column row whose field is empty text is a
  blank line, which CsvHelper skips: put the value after a `Line` column (`CsvLine<T>`) or quote it. A refused text is
  written with `CsvHarness.Field`, so that CsvHelper quotes it. A `Csv/` file aliases `CsvHelper.MissingFieldException`,
  which meets `System.MissingFieldException`, and never imports `System.ComponentModel` beside
  `CsvHelper.Configuration.Attributes` (two `TypeConverterAttribute`s). A generated value object's `IsDefault` is an
  explicit implementation: read it through `CsvHarness.IsUninitialized()`.
- The CsvHelper package is referenced under the alias `csvhelper` too, through which `Csv/CsvCultureTests` names its
  copy of `GregorianCultures` (`src/Shared/CLAUDE.md`).
- Every sample is held to this family's three packages from `GeneratedSurface/GeneratedSurfaceTests.Text.cs`, a
  `GeneratedSurfaceTests` part of this project's own, over the checks of `GeneratedSurface/Sample.Text.cs`.
- It links core's `Web/ValidationMessages.*`, `Web/SparseMessages.*` and `WithoutDynamicCode.cs`
  (`Cli/WithoutDynamicCodeCliTests`, `Csv/WithoutDynamicCodeCsvTests`), at the same paths.
