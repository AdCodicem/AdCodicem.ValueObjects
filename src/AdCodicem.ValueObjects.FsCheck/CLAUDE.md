# AdCodicem.ValueObjects.FsCheck

User-facing limits: `website/docs/how-to/test-value-objects.md`. Tests: `TestData/FsCheckTests.cs`, `ShrinkTests.cs`, and
`PropertyTests.cs`, which draws through `ValueObjectArbitrary`.

- `MergeValueObjects` merges what the assembly's generated registration lists, never the constructions of a generic value
  object nor a hand-written type nothing registered, which `MergeValueObject<TSelf, TValue>` merges one by one. Never scan
  the assembly through `TryResolve`: it would register every hand-written type for the process. `FsCheckTests` merges a
  fresh `AssemblyLoadContext` copy of `Fixtures.Untouched`, whose registration only the merge can have run.
- FsCheck stops a run at 5,000 shrinks, draws `bool` and `Guid` with no shrinker, handles neither `DateOnly` nor
  `TimeOnly`, and derives the 128-bit integers by reflection, drawing 0: a value object over one of them shrinks to its
  declared values alone.
- FsCheck's other shrinks reach `ValueObjectSampler.Shrink` only when the normalizer leaves them unchanged. Its character
  shrinker proposes `a`, `b` and `c` for any capital, so an upper-casing normalizer would shrink `B` and `C` to each other
  until the cap: `ShrinkTests` counts the shrinks.
- Its `DateTime` shrinker makes a UTC value, as the sampler draws one, of no kind: `ValueObjectArbitrary` shrinks the value
  of no kind and gives the kind back.
- FsCheck writes `Shrunk:` in a failure only after one shrink at least: a test reading the counterexample falls back on
  `Original:`.
- The namespace hides the root `FsCheck` namespace (root `CLAUDE.md`).
