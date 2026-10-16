# AdCodicem.ValueObjects.Identifiers.MongoDB

- Id generators are process-wide, and a class map takes its id member's generator once, when it is built, never again:
  `EntityIdBson.Register` must run before any class map of an entity exists, in practice right after
  `ValueObjectBson.Register`. Every suite's static MongoDB registration calls both, in that order.
- `EntityIdBson.Register` closes each id generator through `EntityIdDescriptor.Accept` and reaches `TId.New()`.
- A test of an id generator registered or kept closes `HandWrittenId<TProfile>` over a profile of its own.
