# AdCodicem.ValueObjects.UnitTests.TestData

The testdata family's unit tests (Testing, AutoFixture, Bogus, FsCheck): the contract kit's contracts and
`PropertyTests.cs` at its root, and `TestData/` (AutoFixture, Bogus, FsCheck). The sampler's own tests are core's
(`TestData/` of `tests/AdCodicem.ValueObjects.UnitTests`), Testing.Data being a core project. The suite's conventions
are in `tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`, each package's traps in its `src/<Package>/CLAUDE.md` (none
for Testing).

- Every value object of `Domain/` gets a `ValueObjectContract` overriding `DerivesRejectedValues` to return `true`, in
  one of the contract files at this project's root (`ContractTests.cs`, `UnderlyingTypeContracts.cs`,
  `ClassifiedValueObjectContracts.cs`, `DeclarationContextContracts.cs`, `DocumentedValueObjectContracts.cs`); the
  exceptions are in the root `CLAUDE.md`. `ContractKitTests.cs` holds the kit to what those never meet.
- Property tests (`PropertyTests.cs`) run the laws `IValueObject<TSelf, TValue>` states in prose. A property conditioned
  on "the value was accepted" passes vacuously unless the generator produces accepted values, so each counts how often
  it reached that branch and asserts on it; a range generator biases towards the sampler's `Boundaries` and
  `RejectedValues`, since a wide type never lands on its edge by chance. After changing a generator, re-run the
  mutations its commit message records before trusting the green.
- This project references the bare FsCheck package, as the FsCheck package depends on, never FsCheck.Xunit, whose
  `xunit.extensibility.execution` `[2.4.1, 3.0.0)` dependency conflicts with xunit.v3: the properties run from plain
  `[Fact]`s through `Check(Config)`.
- The FsCheck merge over "every registered type" relies only on `Domain/` types or on a fresh `AssemblyLoadContext`
  copy of `Fixtures.Untouched` (`tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`).
- It links core's `TestData/SamplerFixtures.cs`, at the same path.
