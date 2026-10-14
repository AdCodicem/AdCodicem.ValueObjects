---
title: Use with protobuf-net and code-first gRPC
sidebar_label: protobuf-net and code-first gRPC
slug: /how-to/protobuf-net
description: Register a serializer for every value object on a protobuf-net model, so that each is written as protobuf-net writes its underlying type, read back through its rules, named in the .proto export as that type's scalar, and refused with INVALID_ARGUMENT and its rule code by a protobuf-net.Grpc code-first service; and name the same serializer on the value object for protobuf-net's compile-time model under native AOT.
---

# Use with protobuf-net and code-first gRPC

```bash
dotnet add package AdCodicem.ValueObjects.ProtobufNet
```

```csharp skip
RuntimeTypeModel.Default.AddValueObjects(typeof(Iban).Assembly);

builder.Services.AddSingleton(ValueObjectBinderConfiguration.ForServer(RuntimeTypeModel.Default)); // before AddCodeFirstGrpc
builder.Services.AddCodeFirstGrpc();
```

protobuf-net has no way to treat a value object as its underlying value, and protobuf-net reports none of it at build
time or at start-up:

- **Serializing a message fails.** `Serializer.Serialize` on a `[ProtoContract]` holding an `Iban` throws
  `InvalidOperationException: No serializer defined for type: Shop.Iban`.
- **Code-first gRPC services do not work.** protobuf-net.Grpc finds no marshaller for a message holding a value object:
  a client over a configuration of its own cannot be built, "No marshaller available for Shop.AddRequest" inside a
  `TargetInvocationException`, the default client fails its first call, `NotSupportedException`, "Specified method is
  not supported.", and the server maps none of the service's methods, so a client the server could read is answered
  `UNIMPLEMENTED`.
- **The `.proto` export is wrong.** `Serializer.GetProto<Order>()` declares `message Iban {}`, and a client generated from
  it sends an object where the service expects a bare value.
- **`[ProtoContract]` on the value object loses data.** protobuf-net then writes it as an empty message, `0A00`, and reads
  it back as a default instance, with nothing thrown. `VO0043` fails the build on it, and its code fix names the
  serializer ([The `[ProtoContract]` trap](#the-protocontract-trap)).

Contract-first services, whose messages Grpc.Tools generates, never carry a value object in a message: their handlers
map fields to value objects, which [Answer refused values in gRPC services](grpc.md) covers.

## Registering the value objects

protobuf-net adds a member's type to a model only when that type carries a contract attribute, so it never meets a value
object it was not told about. `AddValueObjects` tells it, for every value object the registry holds once the generated
registration of the assemblies given has run, at start-up, before the model serializes anything:

```csharp skip
RuntimeTypeModel.Default.AddValueObjects(typeof(Iban).Assembly);       // Serializer.*, protobuf-net.Grpc's default binder

var model = RuntimeTypeModel.Create();                                 // or a model of the application's own
model.AddValueObjects(typeof(Iban).Assembly);
```

Each value object gets a `ValueObjectProtoSerializer<TSelf, TValue, TProfile>`, closed over it at compile time, through the
type arguments its descriptor hands back ([Run-time lookup](runtime-lookup.md#back-to-the-typed-path)). Called twice, the
registration changes nothing; a value object the model already serializes some way keeps it, whether the model was given
its serializer type or the type names one with `[ProtoContract(Serializer = …)]`. `AddValueObject<TSelf, TValue>()` adds
one value object, without reflection, and refuses, naming both, a serializer other than the one the model already
holds:

```csharp skip
model.AddValueObject<Quantity, short>(trusted: true);
```

protobuf-net freezes a type once it has built its serializer, and refuses any change to it after that: register before
the first message goes through the model. A use that failed, a value object nobody registered, freezes nothing.

## What a value object writes

The serializer writes `value.Value` through protobuf-net's own serializer of the underlying type
(`TypeModel.GetInbuiltSerializer<TValue>`), so a value object writes the bytes the primitive it replaces writes, at the
same level, and reads back what that primitive reads back, but for three cases this page states: a valid zero, which it
writes where protobuf-net leaves out the primitive's ([Writing](#writing)); a member's `DataFormat`, which never reaches
it; and a root value object. A formatting hook of the value object never reaches the wire. protobuf-net's compatibility
level changes how four types are written:

| Underlying type | `Level200`, protobuf-net's default | `Level240` | `Level300` |
| --- | --- | --- | --- |
| `Guid` | a `bcl.Guid` message | the same | a 36-character string |
| `decimal` | a `bcl.Decimal` message | the same | a string, `"12.5"` |
| `DateTime` | a `bcl.DateTime` message | a `google.protobuf.Timestamp` | the same |
| `TimeSpan` | a `bcl.TimeSpan` message | a `google.protobuf.Duration` | the same |
| `string`, `bool`, the integers, `char`, `float`, `double`, `DateOnly`, `TimeOnly` | as protobuf-net writes the primitive | the same | the same |

protobuf-net calls a custom serializer whatever `[CompatibilityLevel]` a member, its message or its assembly declares, so
the level a value object is written at is fixed by its serializer type: `AddValueObjects` takes the model's
`DefaultCompatibilityLevel` when it runs, which protobuf-net lets nobody change once a type is added.
`RuntimeTypeModel.Default`'s is `Level200`, and can never change ("The default compatibility level of the default model
cannot be changed"): an application whose messages are at `Level300` registers on a model of its own, its level set first.

```csharp skip
var model = RuntimeTypeModel.Create();
model.DefaultCompatibilityLevel = CompatibilityLevel.Level300;
model.AddValueObjects(typeof(Iban).Assembly);
```

A member's `DataFormat` never reaches a value object either: protobuf-net calls the serializer whatever the member
declares, so the value object is written and read as `DataFormat.Default` writes its underlying type, and nothing
reports it, at build time or at run time. An `int` member declared `[ProtoMember(1, DataFormat = DataFormat.ZigZag)]`
and replaced by a value object changes the wire: a peer's ZigZag `-3`, the byte `05`, is read as `5`, and accepted
wherever the rules allow it. `FixedSize` writes a varint where the primitive writes four bytes, `WellKnown` a
`bcl.DateTime` where it writes a `Timestamp`, and the export follows, `int32` where the primitive's says `sint32`. Such
a member keeps its primitive type, or the `.proto` the peers share changes.

A root value object, the message itself as `Serializer.Serialize(stream, customerId)` writes it, follows its
serializer's level too, where protobuf-net writes a root primitive at `Level200` whatever the model's level. A root
value object over `DateTime` or `TimeSpan` from `Level240`, or over `Guid` or `decimal` at `Level300`, is therefore not
written as the root primitive is, and a root `Guid` or `decimal` value object at `Level300` refuses the root primitive's
bytes as `value_object.not_parsable`. As a member, the same value object writes what the member over the primitive
writes.

protobuf-net creates a serializer named by its type with its parameterless constructor, and keeps one per type for the
whole process, so the trust and the level are type arguments, a profile:

| Serializer | Reads | Writes at |
| --- | --- | --- |
| `ValueObjectProtoSerializer<TSelf, TValue>` | through the rules | `Level200` |
| `ValueObjectProtoSerializer<TSelf, TValue, ValueObjectProtoProfiles.StrictLevel200>`, `…StrictLevel240`, `…StrictLevel300` | through the rules | the level named |
| `ValueObjectProtoSerializer<TSelf, TValue, ValueObjectProtoProfiles.TrustedLevel200>`, `…TrustedLevel240`, `…TrustedLevel300` | without validating | the level named |

A profile of the application's own implements `IValueObjectProtoProfile`, two static members.

A `TSelf?` member holding nothing writes nothing, as an `int?` does. A list, an array and a set write each value object
as its value, a dictionary keyed or valued by value objects as the same dictionary of the primitives, so a message of
value objects is written as the same message of the primitives they replace, byte for byte but for a valid zero and a
member's `DataFormat`; the suite checks it for every underlying type, at the three levels, strict and trusted, a root
value object included.

`Int128`, `UInt128` and `DateTimeOffset` have no serializer in protobuf-net, as plain members either. `AddValueObjects`
leaves their value objects out, so that protobuf-net's own error names the value object when a message holds one;
`AddValueObject` refuses one with a `NotSupportedException` naming its underlying type, as the serializer does when an
attribute names it on such a type.

## Reading

Reads are strict by default. protobuf-net is above all a wire format between services and clients, so a value read goes
through `TryCreate`, which normalizes it — a lowercase IBAN with spaces reads as `FR7630006000011234567890189` — and
refuses one the type rejects:

```text
ProtoException: The value read is not a valid Iban: The value must be at least 15 characters long.
```

protobuf-net lets the exception through as it is, with the rule's code in its `Data` under `ValueObjectErrors.ErrorCodeKey`
([the code in an exception](../reference/errors.md#the-code-in-an-exception)). No message quotes the value read.

A trusted serializer reads through `CreateUnchecked`, for bytes the application alone wrote, a cache or a store among
them, where validating each value again only costs time:

```csharp skip
model.AddValueObjects(trusted: true, typeof(Iban).Assembly);
```

Whatever the trust, a value protobuf-net's serializer of the underlying type cannot read — a field of another wire type,
a number beyond the range of a `short`, a day beyond `DateOnly`'s, ticks beyond `TimeOnly`'s, a `bcl.DateTime` beyond
`DateTime`'s, text no `Uri` parses, bytes cut inside the value, a negative length, a `decimal` written as text at
`Level300` that no `decimal` parses — is refused with `value_object.not_parsable`, without protobuf-net's own exception,
whose message may quote the value, "Unable to parse decimal: 'abc'".

## Writing

A value object that never went through `Create`, an `Iban` property never set, holds the default value, which its type
may reject. The serializer refuses to write it, with a `ProtoException` carrying the rule's code, "The value to write is
not a valid Iban: …", alone, nullable, in a list and as a dictionary key. A type that accepts its zero, a
`SequenceNumber` declared `AllowDefault = true`, writes it, and writes it explicitly, `08 00`, where protobuf-net leaves out
a primitive's zero: a reader of either reads it back the same. Any other instance went through `Create`, or was read by a
trusted serializer, and is written as it is.

## Members a sender leaves out

protobuf-net never calls a serializer for a field the message does not hold. Under proto3 that is every zero and every
empty value a sender of another language omits, and also the zero value of a map entry that a primitive writer left out:
the member then holds the default instance, which no rule checked — an `Iban` property `IsDefault`, a `Quantity` of `0`
whatever minimum its type declares. `[ProtoMember(IsRequired = true)]` changes nothing on read.

Declare a member a sender may leave out as `TSelf?`, which reads as `null` when the field is absent. A list of nullable
value objects keeps its nulls only under `[NullWrappedValue]`, exactly as a `List<int?>` does; without it, protobuf-net
refuses to write a `null` in one, with a `NullReferenceException`, as it refuses one in a `List<int?>`.

## Generic value objects and value objects written by hand

protobuf-net has no hook that sees a member's type, so the package cannot meet a value object it was not given: a
construction of a generic value object, `Reference<PurchaseOrder>`, and a value object written by hand are in the registry
only once something registered them. `AddValueObject` adds each, with no reflection; without it, protobuf-net throws "No
serializer defined for type: Shop.Reference`1[Shop.PurchaseOrder]".

```csharp skip
RuntimeTypeModel.Default.AddValueObject<Reference<PurchaseOrder>, string>();
```

## The `.proto` export

The registration names each value object, in protobuf-net's export, as the scalar protobuf-net names its underlying type
at that level, and gives it the file that defines that scalar, so that `GetSchema` writes `string Account = 1;`, `int32
Quantity = 3;`, `.bcl.Guid Customer = 2;` with `import "protobuf-net/bcl.proto";`, or `.google.protobuf.Timestamp
RecordedAt = 4;` with its `import`, where the same message over the primitives does. A value object whose
`[ProtoContract(Serializer = …)]` names one of this package's serializers over a profile it ships, and no `Name`, is
named the same way by `AddValueObjects`. Two cases stay unnamed, and the export then refers to a message it never
defines, `Sku Sku = 2;` with no `message Sku`: a serializer closed over a profile of the application's own, and the
attribute route on a model nothing registered on. Give the attribute a `Name`, the scalar its underlying type is written
as where it needs no `import` (`string`, `int64`), or register with `AddValueObjects` over a profile the package ships.

Three differences remain, and the package does not rewrite protobuf-net's text to hide them:

- **A dictionary is a list of entries.** A dictionary of value objects is exported as `repeated KeyValuePair_string_int32
  Stock = 1;` with a `message KeyValuePair_string_int32`, rather than `map<string,int32>`. That is the same on the wire, a
  protobuf map being a repeated entry message, but two such dictionaries over other value objects of the same scalars
  export two messages of one name, which protoc refuses.
- **No `[packed = false]`.** A list of numeric value objects lacks the annotation protobuf-net writes for a `List<int>`.
- **A packed list cannot be read.** protobuf-net neither writes nor reads a list of value objects packed, whatever the
  member declares. A client generated from the file sends a `repeated int32` packed, as proto3 does by default, and the
  service cannot read it: a refusal with `value_object.not_parsable`. A `.proto` shared with other languages marks such a
  field `[packed = false]`.

protobuf-net also guesses the `package` line from the namespaces of the types it exports, and counts the value objects
among them: with value objects in another namespace than the messages, the line goes. Name it:

```csharp skip
var proto = model.GetSchema(new SchemaGenerationOptions { Types = { typeof(Order) }, Syntax = ProtoSyntax.Proto3, Package = "shop" });
```

A file shared with other languages can also be exported from the same messages over the primitives. The rules reach
protobuf as validation on read, with their codes, never in the export.

## Code-first gRPC

protobuf-net.Grpc marshals a code-first service's messages through a `BinderConfiguration`, its default one over
`RuntimeTypeModel.Default`, which serves value objects once they are registered there. But Grpc.AspNetCore reads a
request before any interceptor runs, and answers an exception its deserializer throws with `UNKNOWN`, "Exception was
thrown by handler.": the rule is lost. `ValueObjectBinderConfiguration.ForServer` builds the configuration
`AddCodeFirstGrpc()` reads from the services, whose marshaller answers a request value a value object refuses with
`INVALID_ARGUMENT`:

```csharp skip
RuntimeTypeModel.Default.AddValueObjects(typeof(Iban).Assembly);
builder.Services.AddSingleton(ValueObjectBinderConfiguration.ForServer(RuntimeTypeModel.Default, "orders.example.com"));
builder.Services.AddCodeFirstGrpc();
```

The status message names the value object and the rule, never the value, "The value read is not a valid Quantity: The value
must be less than or equal to 1000.", and the trailers carry `grpc-status-details-bin`: a `google.rpc.Status` holding a
`google.rpc.ErrorInfo`, gRPC's rich error model, which any gRPC client reads:

| Field | Value |
| --- | --- |
| `reason` | the code in upper snake case, `VALUE_OBJECT_OUT_OF_RANGE` ([a code for gRPC](../reference/errors.md#a-code-for-grpc)) |
| `domain` | the domain given, `adcodicem.valueobjects` by default |
| `metadata` | `code`, the rule's code; `valueObject`, the value object as C# names it, `Reference<PurchaseOrder>` |

A code of the application's own that maps to no gRPC reason, `9.odd`, is answered `INVALID_ARGUMENT` all the same, with
the `google.rpc.Status` alone, without the `ErrorInfo`. A refused message of a client stream is answered the same way.
Everything else is protobuf-net.Grpc's: the
marshallers of `ProtoBufMarshallerFactory.Create(model)`, then those of Google.Protobuf messages, as its default
configuration; bytes protobuf-net itself cannot read are answered as before. On the server, the `RpcException` carries the
code in its `Data` too, and the refusal as the status's debug exception, for the server's own log.

A .NET client needs nothing of this package beyond the model, which `ClientFactory.Create(BinderConfiguration.Create([ProtoBufMarshallerFactory.Create(model)]))`
takes, or the default factory over `RuntimeTypeModel.Default`; Grpc.StatusProto's `GetRpcStatus()` reads the details:

```csharp skip
catch (RpcException exception) when (exception.StatusCode == StatusCode.InvalidArgument)
{
    var info = exception.GetRpcStatus()?.GetDetail<ErrorInfo>();   // info.Metadata["code"]: value_object.out_of_range
}
```

A reply the client's value objects refuse surfaces as protobuf-net.Grpc reports it, `INTERNAL`, "Error starting gRPC call.
ProtoException: …", whose inner exception carries the code, which `ValueObjectErrors.TryGetCode` reads. A reply the server
cannot write, an uninitialized value object its type rejects, is a fault of the server, which Grpc.AspNetCore answers with
`UNKNOWN`.

A `ValueObjectException` a code-first handler lets escape, a `Create` on a value that broke a rule, is no request value
the marshaller read: Grpc.AspNetCore answers it `UNKNOWN`, as any exception of a handler, unless
`ValueObjectExceptionInterceptor` of `AdCodicem.ValueObjects.Grpc` answers it with the same `ErrorInfo`
([Answer refused values in gRPC services](grpc.md#a-refusal-that-escapes-a-handler)).

## Native AOT

protobuf-net's runtime model reflects over contracts and emits code on first use, and protobuf-net.Grpc emits its
proxies and closes its marshallers by reflection: `AddValueObjects`, `AddValueObject` and `ForServer` serve the JIT
alone, and carry `[RequiresDynamicCode]`, so that a native publish calling one warns `IL3050` there, which neither
library's own annotations would. The serializers themselves carry no reflection, and run in a native binary inside
protobuf-net's compile-time model, which its generator, coming with protobuf-net.Core, writes for a `[ProtoModel]`
partial type and which never consults a runtime model. protobuf-net's generator cannot see what this library's generator
writes, so the author names the serializer on a partial declaration of the value object, in its own assembly, which the
code fix of `VO0043` writes on a bare `[ProtoContract]`:

```csharp skip
[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Iban, string>))]
public readonly partial struct Iban;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<CustomerId, Guid, ValueObjectProtoProfiles.StrictLevel300>))]
public readonly partial struct CustomerId;

#pragma warning disable PBN9001 // protobuf-net's compile-time model is experimental, an error unless suppressed
[ProtoModel]
[ProtoSerializable(typeof(Order))]
public partial class AppModel : TypeModel;
#pragma warning restore PBN9001
```

```csharp skip
AppModel.Instance.Serialize(stream, order);
```

The same attribute serves a runtime model with no registration at all, which leaves the value object unnamed in the
export ([The `.proto` export](#the-proto-export)). CI publishes such a model over every underlying type protobuf-net
writes, and compares the native binary with the JIT. What to know:

- **`PBN9001`.** protobuf-net marks its generator experimental with an error, which every `[ProtoModel]` suppresses. Once
  a project declares one, protobuf-net's analyzer reports every `Serializer.*` call as `PBN3010`, pointing it at the model.
- **Leave out `IsScalar`.** protobuf-net documents `[ProtoContract(IsScalar = true)]` for a serializer of a compiled
  reference, but a wrong one, on a `Guid` value object at `Level200`, where the value is a message, builds without a
  diagnostic and fails every read.
- **No `TSelf?` member, no dictionary of value objects.** protobuf-net's generated code fails to compile for either,
  `CS1503` in its own file; a list compiles, and writes as on a runtime model.
- **No generic value object**: an attribute cannot name a type through a type parameter.
- **No `.proto` export**: `GetSchema` on a generated model throws `NotSupportedException`. Export from a runtime model.
- **Trimming warnings.** Each call of the generated model into a serializer demands, through protobuf-net's
  `[DynamicallyAccessedMembers]`, every member of the value object, its nested types included, which reaches the
  System.Text.Json and `TypeDescriptor` converters the generator nests in it: the publish warns `IL2026`, `IL2111` and
  `IL3050` at those sites, and protobuf-net.Core `IL2104` and `IL3053` for itself. The value object runs as the JIT runs
  it all the same.

## The `[ProtoContract]` trap

A bare `[ProtoContract]` on a value object, the attribute people put on every type they send, makes protobuf-net write it
as a message with no member: `0A00`, read back as a default instance, the value lost with nothing thrown. `AddValueObjects`
run before the model meets the type takes it over, and the value object is written as its value; run after the model has
written one, protobuf-net refuses the change, "The type cannot be changed once a serializer has been generated". Under
protobuf-net's compile-time model, no registration reaches it at all: the serializer generated for the value object
writes nothing.

So the attribute fails the build, `VO0043`, and so does a `Serializer` naming this package's serializer closed over
another value object, which protobuf-net refuses at run time. Its code fix names the serializer, as
[Native AOT](#native-aot) does, `[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Iban, string>))]`, keeping
the attribute's other arguments and a profile already named, or removes the attribute. The serializer it names on a bare
`[ProtoContract]` is the strict profile at `Level200`, as its title says: where `AddValueObjects` took the type over on a
model at `Level240` or `Level300`, or with `trusted: true`, name the profile it used instead,
`ValueObjectProtoSerializer<Iban, string, ValueObjectProtoProfiles.StrictLevel300>` or the like, or remove the attribute,
or a value object over `DateTime` or `TimeSpan`, and at `Level300` over `Guid` or `decimal`, leaves the wire it had
([What a value object writes](#what-a-value-object-writes)). An `AddValueObject<TSelf, TValue>()` of a value object whose
attribute names the two-argument serializer throws at start, naming both: drop the call. One over `Int128`, `UInt128` or
`DateTimeOffset`, which protobuf-net has no serializer for, drops the attribute, generic or not. Any other generic value
object, or one nested in a generic type, cannot name its serializer through a type parameter: remove the attribute, and
register each construction with `AddValueObject<TSelf, TValue>()`. A `Surrogate` and a serializer of the application's
own are left alone ([A serializer's contract attribute](../reference/diagnostics.md#a-serializers-contract-attribute)).

## Not covered

- **`localized_message`.** The status message of a refused request stays in English, and `ForServer` refuses it before
  any handler runs, where a service could build details of its own; a client that answers in its users' language looks
  the message up by the code the `ErrorInfo` carries ([a message in the application's
  language](../reference/errors.md#a-message-in-the-applications-language)).
- **protobuf-net.Grpc's compile-time route**, `[ProtoGrpc]` and `[ProtoService]`, and how a refused request is reported
  there; **protobuf-net.Grpc.Reflection**'s schema generator. Neither was run.
- **Field options** such as protovalidate's `(buf.validate.field)` in the export: the rules stay validation on read.
- **A surrogate** for `Int128`, `UInt128` or `DateTimeOffset` the application registers: not proposed, not run.
- **`AllowParseableTypes`**: protobuf-net does not consult it for a value object, and it would write the formatted text
  of the value, a string whatever the type, never the primitive's wire.
- **Google.Protobuf messages**, which [contract-first services](grpc.md) map to value objects in their handlers.
