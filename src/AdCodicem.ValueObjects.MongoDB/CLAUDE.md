# AdCodicem.ValueObjects.MongoDB

User-facing behaviour: `website/docs/how-to/mongodb.md`. Not AOT-compatible.

- Reads are strict, through `TryCreate`, unless the serializer is trusted (`ValueObjectBson.Register(trusted: true, …)`):
  a collection has no schema and is often written by more than one program. A refusal is a `FormatException` on read and
  a `BsonSerializationException` on write, each inside the driver's own exception of the same type.
- MongoDB.Driver's serializer registry is process-wide and caches for good. A value object the driver serializes before
  `ValueObjectBson.Register` keeps the class map it was given, which writes `{}`, for the rest of the process, so
  `Register` fails and names it: it reads `BsonClassMap.GetRegisteredClassMaps()`, which caches nothing, and asks
  `ValueObjectRegistry.IsValueObject` of each type, so a generic construction counts. An application's own serializer,
  `GuidSerializer(GuidRepresentation.Standard)` above all, is registered before it.
- The internal checks take a `BsonSerializerRegistry` as a parameter, so a test runs them on a fresh registry.
- `ValueObjectBsonSchema` writes a rule of `TSelf.Schema` into the `$jsonSchema` validator only where the server refuses
  no value the type accepts: the pattern in PCRE2 through `PcrePattern`, each class escape listed from .NET's own Unicode
  tables; the `enum` of a closed set of text only where `KnownValueSpellings` finds no other stored spelling, by the
  `Comparison` the schema states: always under `Ordinal`, never under a culture's, which takes spellings of the same
  length too, and under `OrdinalIgnoreCase` (or none stated) only where the type stores neither another case of a known
  value, nor a character sharing its upper case (`µ` for `μ`), nor the value followed by a soft hyphen. It thereby
  loosens two rules OpenAPI and the language-model profile keep: the case-insensitive `enum` and a `minLength` counted in
  UTF-16 code units.
- MongoDB.Bson has an internal `UInt128` of its own, which a `cref` to `UInt128` in a file importing `MongoDB.Bson`
  resolves to (CS0419): write `System.UInt128` there.
- A query's `ToString()` reports a translation the serializer refused instead of throwing it: a test of a refused query
  constant renders a `Builders` filter.
- The namespace hides the root `MongoDB` namespace (root `CLAUDE.md`): name the driver's types through usings.
