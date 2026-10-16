# AdCodicem.ValueObjects.HotChocolate — scalars

User-facing behaviour: `website/docs/how-to/graphql.md`. Tests: `GraphQL/` of the unit suite (`GraphQLHarness`, executors
built in process). AOT-compatible itself, but out of `tests/NativeAot`: HotChocolate's own publish warns (IL2104,
IL3053, IL3000, IL3002) and its binary starts only with HotChocolate.Types rooted
(ChilliCream/graphql-platform#10566), and the application too when its types are built by reflection.

## Schema building

- Without the package HotChocolate infers an input object type from a value object it meets as an argument, finds no
  constructor a field fills (the generated one takes an `UncheckedTag`, and it considers private ones) and fails the
  schema at start: a test that forgets `AddValueObjects()` fails at `GetRequestExecutorAsync`, never at a request.
- A type discovery handler is created per schema build, and a type system object belongs to the schema that completes
  it: `AddValueObjectScalar` adds a factory, never an instance, and the handler keeps its scalars per build. It closes the
  scalar of each value object the schema meets through the visitor; `AddValueObjectScalar<TSelf, TValue>()` asks no
  registry.
- `TypeDiscoveryHandler` lives in `HotChocolate.Internal`: a 16.x minor may change it, which Dependabot's grouped minor
  and patch updates run the suite against first; HotChocolate's majors are ignored, 17 being a decision.
- The options of every `AddValueObjects` call on a schema are one `ValueObjectScalarOptions`, found through an internal
  `ValueObjectScalarRegistration` singleton of the application's services keyed by the schema's name, and handed to the
  scalars through the schema builder's `Features`, which each reads in `CreateConfiguration`.
- The scalar derives the non-generic `ScalarType` so that a Float takes an Int literal: HotChocolate's validation refuses
  a literal of another kind by `SerializationType` before a scalar reads it, but hands a variable of another JSON kind to
  `CoerceInputValue`.
- HotChocolate builds the `List<TSelf>` of a list argument with `MakeGenericType`, which
  `ValueObjectScalarType.Instantiations` names for native AOT. Where the runtime supports no dynamic code, a value object
  nothing registered is refused with a `NotSupportedException` naming the typed registrations, inside HotChocolate's
  `SchemaException` at start.

## Refusals and messages

- A refused input throws a `LeafCoercionException` carrying the code in its `Data`, which the GraphQL error carries as
  `extensions.code`, with the internal `ValueObjectScalarRefusal` (a `ValueObjectException`) on `IError.Exception`.
  HotChocolate overwrites the code of a refused **output** with `EXEC_INVALID_LEAF_VALUE`.
- A field error filter that throws becomes HotChocolate's "Unexpected Execution Error"; a variable's leaves the
  execution. Filters run in registration order.
- Error filters live in the schema services, built with the executor: the localizing filter resolves the application's
  `IStringLocalizerFactory` through `GetRootServiceProvider()`, in the execution's culture, and the executor fails to start
  when `UseResource()` lacks `AddLocalization()`.
- HotChocolate reads a variable's default value under the path `["root"]`, which the filter quotes as the scalar's name,
  telling it from a variable named `$root`, read from its JSON value, by the refusal's `Literal`. 16.7's previews report
  it under `extensions.inputPath` with no path: revisit the filter at that bump.
- `ValueObjectScalarOptions.QuoteScalarNameIn(predicate)` quotes the scalar's name for a value refused in the argument or
  input field the predicate selects: the filter looks up the error's `coordinate`, which HotChocolate keeps as text
  (`IbanOperationFilterInput.eq`), sets to the innermost input value, and leaves out for a variable it coerces before any
  field. That is how HotChocolate.Data reaches every operation with no reference from this package to it; without a
  predicate nothing changes.
- A text scalar's `Pattern` comes from its `[GeneratedRegex]` text, which HotChocolate publishes as `@serializeAs` under
  `ApplySerializeAsToScalars`, beside the wire pattern of a `TimeSpan`, a `TimeOnly` or a `DateTime`.

## Usings

- The namespace hides the root `HotChocolate` namespace (root `CLAUDE.md`). With `using HotChocolate;`, `Path` is
  ambiguous with `System.IO.Path` (alias it), and `HotChocolate.Types` holds a `Tag`.
- HotChocolate.AspNetCore adds global usings of `HotChocolate`, `HotChocolate.Types`, `HotChocolate.Types.Relay` and
  `GreenDonut` under `ImplicitUsings`, whose `DefaultValueAttribute` collides with `System.ComponentModel`'s: a project
  referencing it sets `HotChocolateImplicitUsings` to `disable`, as the unit suite does.
