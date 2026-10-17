# AdCodicem.ValueObjects.Orleans

User-facing behaviour: `website/docs/how-to/orleans.md`. Tests: `Grains/` of the unit suite (serializers built in
process, an in-process cluster through `GrainCluster`), over the grains and holders of
`tests/Fixtures/AdCodicem.ValueObjects.Fixtures.Grains`, and the compatibility island's `OrleansTests.cs`.

## Codecs and registration

- Reads are strict unless trusted (`AddValueObjects(trusted: true)`, `AddValueObject<TSelf, TValue>(trusted: true)`),
  and Orleans builds one serializer per service provider: the trust covers every peer of the silo or client, and grain
  state stored through `OrleansGrainStorageSerializer`. A `null` is refused whatever the trust.
- `CodecProvider` asks a specializable codec only for a type none of its own field codecs serves, in service order. A
  field codec of `TypeManifestOptions` is looked up by the generic definition of a constructed type, so a construction
  of a generic value object needs a specializable codec (`ValueObjectCodecRegistration`), never `AddFieldCodec`.
- Each registration inserts its specializable codec at index 0 of the services: the last call answers first, its trust
  applying, and a typed registration made after `AddValueObjects` answers for its type.
- The type filter is needed once codecs exist: a grain method's result travels in `Response<TResult>`, which names its
  type, as do a `TSelf?`, a construction and a member typed `object`. `AddValueObject` allows its own type
  (`AddAllowedType`); a field codec registered through `AddFieldCodec` has its type allowed by Orleans itself, which is
  why `ValueObjectException` needs no filter entry.
- A value object in a member typed `object` or an interface is a tagged object, its underlying value as field 0, as
  Orleans' own structs are; both sides call `ReferenceCodec.MarkValueField`, or the reference ids of the rest of the
  message shift.
- `IFieldCodec<T>.WriteField`'s value carries `[AllowNull]` and `ReadValue` returns `[MaybeNull]`: implement them with
  `[AllowNull]` and a nullable return, or nullable analysis fails the build (CS8767, CS8603).
- The read catches `SerializerException` (another wire type or length), `OverflowException` (a narrower type) and
  `ArgumentException` (a date or a time out of range), each reached by a test; Orleans' reader throws
  `IndexOutOfRangeException` for truncated bytes, which the codec lets through.
- Orleans copies the arguments of a call within one silo (a value object and every exception are shallow-copyable), so a
  default its type refuses reaches a grain of the same silo; across processes the sender's write refuses it. No copier
  is registered: Orleans' `ShallowCopier` answers before any `ISpecializableCopier`.
- In the Development environment, which `InProcessTestCluster` runs in, Orleans' `SerializerConfigurationValidator`
  resolves the codec of every type of the grain interfaces at silo start: without the package, the silo fails to start
  with `CodecNotFoundException`; elsewhere the first call fails.

## `ValueObjectException` across a call

- Orleans serializes an exception only from the `Microsoft`, `System` and `Azure` namespaces:
  `ValueObjectExceptionCodec` carries the message, the stack trace, the code and the type's name, never
  `AttemptedValue`.
- It writes by value, never as a reference: Orleans' `ExceptionCodec` refuses a reference field typed `Exception`. It
  implements `IDerivedTypeCodec`, without which a subclass never reaches it; the subclass is written as
  `ValueObjectException`.
- The type travels as `RuntimeTypeNameFormatter.Format(type)`: `TypeConverter.Format(type, allowAllTypes)` ignores its
  second argument in 10.4.0 and throws for a type the writer refuses. `TypeConverter.TryParse` throws
  `TypeLoadException` for an assembly the reader does not load and `InvalidOperationException` for a type its filters
  refuse or a name cut short, both caught into a `null` type.

## Dependencies, native AOT and usings

- Out of `tests/NativeAot`: Orleans 10.4.0 fails natively inside `AddSerializer`, and with dynamic code off its
  dependency injection refuses `IFieldCodec<T>` for a value type `T` (an open generic service) before the package is
  asked. `WithoutDynamicCode` covers the selector's `NotSupportedException` under the JIT, through roots and members
  typed `object` only.
- `Microsoft.Orleans.Serialization` lists Roslyn 5 packages its assembly never loads, and
  `Microsoft.CodeAnalysis.Analyzers` 5.9.0: the central pin sits there, and transitive pinning makes it a dependency of
  this package (`.github/shipped-dependencies`); the generator stays on its own Roslyn (root `CLAUDE.md`).
- Orleans' code generator, its analyzers and global usings of `Orleans`, `Orleans.Hosting` and `Orleans.Runtime` reach
  any project referencing `Microsoft.Orleans.TestingHost` (and, by their nuspecs, `Server` and `Client`), through
  `Microsoft.Orleans.Sdk`'s `include="All"` dependencies and `buildTransitive` targets, the unit suite included (its
  `CLAUDE.md`).
- The namespace hides the root `Orleans` namespace (root `CLAUDE.md`): name Orleans' types through usings, or an alias
  where a qualified name is needed; `{global::Orleans…}` inside an interpolated string is CS0103, the colon starting a
  format.
