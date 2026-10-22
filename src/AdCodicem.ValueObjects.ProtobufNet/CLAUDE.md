# AdCodicem.ValueObjects.ProtobufNet

User-facing behaviour: `website/docs/how-to/protobuf-net.md`. Tests: `BinarySerialization/Proto*` and `CodeFirstGrpc*` of
`tests/AdCodicem.ValueObjects.UnitTests.Serialization`, `tests/NativeAot/AdCodicem.ValueObjects.NativeAot.ProtobufNet`.

## Models and serializers

- protobuf-net creates a custom serializer from its type and keeps one per type for the process, so trust and
  compatibility level are type arguments: the profiles of `ValueObjectProtoProfiles` (`Level200`, `Level240`, `Level300`,
  each with a `Trusted…` twin). Reads are strict unless trusted (`AddValueObjects(trusted: true, …)`).
- protobuf-net adds a member's type to a model only when it carries a contract attribute, so nothing registers lazily:
  `AddValueObjects` walks the registry once, naming each closed serializer type with `typeof` over the visitor's type
  arguments, and a construction of a generic value object, or one written by hand, needs `AddValueObject<TSelf, TValue>()`.
- Register before the first use: a model's `DefaultCompatibilityLevel` cannot change once a type is added
  (`RuntimeTypeModel.Default` is `Level200` for good), and a `MetaType` freezes once its serializer is built.
- protobuf-net calls `ISerializer<TSelf?>` only for the elements of a `[NullWrappedValue]` list; a nullable member goes
  through `ISerializer<TSelf>`.
- It calls a custom serializer whatever `DataFormat` a member declares, so a `ZigZag`, `FixedSize` or `WellKnown` member
  is written as `DataFormat.Default`, which nothing reports and the guide states. It writes a root primitive at
  `Level200` whatever the model's level, where a root value object follows its profile.
- Its inbuilt serializers throw `InvalidOperationException` for a negative length and for a `decimal` as text none parses,
  quoting the text: `ValueObjectProtoWire` refuses it as `value_object.not_parsable`, with every other read exception
  but an `ObjectDisposedException`. A refusal is a `ProtoException`.
- The `.proto` export names a value object by its `MetaType`: the package sets `Name` and `Origin`, and `ProtoScalars`
  the scalar and its import.

## Analyzers and native AOT

- protobuf-net.Core's generator and analyzers reach every project referencing this package, by project reference too:
  `[ProtoModel]` is `PBN9001`, an error unless suppressed; once a project declares one, every `Serializer.*` call is
  `PBN3010`; its generated code fails to compile a `TSelf?` member or a dictionary of value objects (CS1503 in its own
  file); `PBN2008` flags a Google.Protobuf type in a code-first contract and `PBN0022` a `[ProtoMember]` with an
  initializer.
- Its `[DynamicallyAccessedMembers]` on `SerializerCache.Get` reach the nested converters of every value object, so a
  generated model warns at each call (IL2026, IL2111, IL3050, and IL2104, IL3053 for protobuf-net.Core): its native
  application is apart (`tests/NativeAot/CLAUDE.md`). The package's own helpers into a `TypeModel` carry protobuf-net's
  `[DynamicallyAccessedMembers]`, or ILC refuses them (IL2091).
- The runtime model and protobuf-net.Grpc emit code, unannotated upstream: `AddValueObjects`, `AddValueObject` and
  `ForServer` serve the JIT alone and carry `[RequiresDynamicCode]`; the serializers run natively.

## Code-first gRPC (`ValueObjectBinderConfiguration`)

- Grpc.AspNetCore reads a request before any interceptor runs and answers its deserializer's exception `UNKNOWN`, so
  `ForServer`'s marshaller answers a refused request itself: `INVALID_ARGUMENT`, with a `google.rpc.ErrorInfo` in the
  `grpc-status-details-bin` trailer (reason `ToUpperSnakeCase(code)`, metadata `code` and `valueObject`, no reason for a
  code that maps to none) written by hand in `RpcStatusDetails`, the `RpcException` carrying the code in its `Data`. A
  reply the server cannot write stays `UNKNOWN`.
- protobuf-net.Grpc builds a client over a configuration created over a model eagerly, throwing a
  `TargetInvocationException` around "No marshaller available" for a message the model cannot serialize; its default
  client is built lazily and fails its first call with `NotSupportedException` ("Specified method is not supported.").
- `AdCodicem.ValueObjects.Metadata` hides `Grpc.Core.Metadata` (CS0118): alias it, as `RpcStatusDetails` does.
