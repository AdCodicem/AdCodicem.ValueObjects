# AdCodicem.ValueObjects.Bogus

- Bogus keeps the last rule of a member, so `RuleForValueObjects` replaces a rule written before it, and a rule written
  after it wins: `BogusTests` pins both orders (`website/docs/how-to/test-value-objects.md`).
- `RuleForValueObjects` and `ValueObject<TSelf>` resolve through the registry: a test meets hand-written value objects only
  as `Declared<,>` constructions over declarations it owns.
- The namespace hides the root `Bogus` namespace (root `CLAUDE.md`).
