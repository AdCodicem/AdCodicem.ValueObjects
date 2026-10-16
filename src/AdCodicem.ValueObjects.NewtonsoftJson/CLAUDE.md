# AdCodicem.ValueObjects.NewtonsoftJson — `ValueObjectConverter`, `ValueObjectContractResolver`

- Newtonsoft.Json keeps a member's contract on its container's contract. `ValueObjectContractResolver` answers each value
  object, and its nullable type, with a contract of its own carrying the converter, keeps a wrapped contract that carries
  a converter already (a `[JsonConverter]` on the type), and answers every other type through the resolver it wraps,
  **whose contracts it never changes**: `CamelCasePropertyNamesContractResolver` caches in a static field, MVC's default
  resolver is one static instance for every set of MVC's settings, and JSON Patch hands every document one resolver,
  through which it reads an object value's members.
- A container's contract stays the wrapped resolver's, and the first serializer to meet a member caches the member's
  contract on it, so a serializer over the wrapper and another over the same wrapped resolver see each other's member
  contracts, in either order (a unit test pins both): the resolver's unit tests use holders and resolvers of their own.
- A contract's converter wins over the settings' converters, one reason neither `AddValueObjects()` nor
  `AddValueObjectsNewtonsoftJson()` wraps a resolver. The guide gives a JSON Patch document a `ValueObjectContractResolver`
  the application keeps, never a wrapper of its own resolver (`website/docs/how-to/json.md`).
