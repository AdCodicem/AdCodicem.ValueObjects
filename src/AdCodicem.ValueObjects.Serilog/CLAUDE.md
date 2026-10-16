# AdCodicem.ValueObjects.Serilog

- It closes nothing and visits no descriptor: the policy and the enricher read a value Serilog has already boxed through
  the `IValueObject` marker, and the option of `Destructure.ValueObjects` reads the `ValueObjectType` of each descriptor
  `ValueObjectRegistry.GetRegistered()` holds, which it hands Serilog as a scalar type.
- That option reads the registry when it runs, under the JIT before most module initializers have: tests assert only on
  types registered for certain (`Domain/`) or absent for certain (a fresh `AssemblyLoadContext` copy of
  `Fixtures.Untouched`, a construction over a marker type private to the test), and never `Register` a shared
  hand-written type.
- Serilog destructures no object under trimming: its `buildTransitive/Serilog.targets` sets
  `Serilog.Capturing.IsStructureValueSupported` to false under `PublishTrimmed`, which `PublishAot` implies, so a native
  binary logs `{@object}` as its `ToString()`, and a bare `Int128` as its text (`website/docs/how-to/logging.md`). The
  native AOT application sets the same switch for its JIT run.
- Serilog.Expressions warns under native AOT (IL2104, IL3053): it stays in the unit suite, out of `tests/NativeAot`.
- The namespace hides the root `Serilog` namespace (root `CLAUDE.md`); a file importing `Serilog` beside
  `Microsoft.Extensions.Logging` aliases one `ILogger` (CS0104).
