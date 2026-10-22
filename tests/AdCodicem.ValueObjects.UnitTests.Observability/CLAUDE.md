# AdCodicem.ValueObjects.UnitTests.Observability

The observability family's unit tests (Serilog, AI, ModelContextProtocol, FluentValidation), in `Logging/` (Serilog,
Microsoft.Gen.Logging's redaction), `LanguageModels/` (Microsoft.Extensions.AI, Agent Framework, the Model Context
Protocol as `Mcp*`) and `FluentValidation*.cs` at its root. The suite's conventions are in
`tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`, each package's traps in its `src/<Package>/CLAUDE.md` (none for
FluentValidation).

- FluentValidation's language manager is global: such tests join the `FluentValidationGlobalOptions` collection and
  restore what they set.
- Logging: this project references Microsoft.Extensions.Telemetry and `Fixtures.Redaction`, which bring
  Microsoft.Gen.Logging, so it sets `DisableMicrosoftExtensionsLoggingSourceGenerator` back to `false` and removes the
  package's analyzers after `ResolveReferences` (`KeepTheDefaultLoggingGenerator`); without both, Microsoft.Gen.Logging
  would pass `PageNumber` as text and `SerilogMicrosoftExtensionsLoggingTests` would fail. The `[LoggerMessage]` methods
  Microsoft.Gen.Logging writes are the fixture's (`tests/Fixtures/CLAUDE.md`).
- MCP: `LanguageModels/McpHarness` runs a server and the SDK's client over a pair of pipes. Experimental-API diagnostics
  (`MEAI001`, `MCPEXP002`) are suppressed around the one statement needing them.
- The OpenAI adapter's strict-mode request is captured by a handler of this project's own
  (`LanguageModels/CapturingHandler.cs`), with no network.
- The Serilog option over "every registered type" relies only on `Domain/` types or on a fresh `AssemblyLoadContext`
  copy of `Fixtures.Untouched` (`tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`).
