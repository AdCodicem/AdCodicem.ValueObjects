---
title: Use with Orleans
sidebar_label: Orleans
slug: /how-to/orleans
description: Register value objects on the serializer of every Orleans silo and client, so that a grain call writes each as the underlying value it replaces, reads it back through its rules, and carries a refusal's rule code back to the caller.
---

# Use with Orleans

```bash
dotnet add package AdCodicem.ValueObjects.Orleans
```

```csharp skip
builder.UseOrleans(silo => silo.Services.AddSerializer(serializer => serializer.AddValueObjects()));
builder.UseOrleansClient(client => client.Services.AddSerializer(serializer => serializer.AddValueObjects()));
```

Orleans has no way to send a value object without the package:

- **Every grain call that takes or returns one fails**, on the caller, before anything is sent:
  `CodecNotFoundException: Could not find a codec for type Shop.Sku.` An argument, a result, a `Quantity?`, a
  `[GenerateSerializer]` class holding one, a construction of a generic value object and one written by hand alike.
  Orleans' code generator and analyzers report nothing at build time.
- **`[GenerateSerializer]` on the value object loses the value**: Orleans writes it with no field and reads back the
  default instance. `VO0043` fails the build on it
  ([A serializer's contract attribute](../reference/diagnostics.md#orleans)).
- **A `ValueObjectException` a grain lets escape** reaches the caller as `CodecNotFoundException: Could not find a codec
  for type AdCodicem.ValueObjects.ValueObjectException.`, with neither the rule nor its code: Orleans serializes an
  exception only from the `Microsoft`, `System` and `Azure` namespaces.

The package needs no Orleans code generator and writes nothing into the build. It depends on
`Microsoft.Orleans.Serialization` alone, so a silo, a client and a library that only serializes take it alike.

## Registering the value objects

`AddValueObjects()` on the serializer builder of every silo and every client: Orleans builds one serializer per process,
and each side reads what the other wrote. It registers three things:

- a codec for every value object, which Orleans asks for the first time it meets the type, and keeps. A value object the
  registry holds is closed over its type arguments through its descriptor
  ([Run-time lookup](runtime-lookup.md#back-to-the-typed-path)), never with `MakeGenericType`;
- a type filter allowing every value object, since Orleans names a value object on the wire whenever its type is not the
  member's own, a grain method's result included: the result travels inside Orleans' `Response<TResult>`, whose type
  names it. Without the filter, a grain method returning a `Quantity` fails on the caller, `Type "Shop.Quantity" is not
  allowed.`;
- a codec for `ValueObjectException`, which carries one a grain lets escape back to the caller
  ([below](#a-valueobjectexception-from-grain-code)).

With the package on the client alone, the silo has no codec. In the `Development` environment, Orleans checks every type
of the grain interfaces when the silo starts, and the silo fails to start with `CodecNotFoundException`; elsewhere, the
silo's own `CodecNotFoundException` reaches the caller of the first call, and a value object in an argument typed
`object` is refused by the silo's type filter.

No copier is registered, and none is needed: Orleans copies a struct whose fields can be copied shallowly, which a value
object's one field always can, before it asks any copier, and its generated copiers assign a value-object member as it
is.

## What a value object writes

A member, an argument, a result or an element typed as the value object is written exactly as the underlying value it
replaces: the codec hands the value to Orleans' codec of the underlying type, with that type as the expected one, so no
type is written. A class of twelve members, value objects alone, nullable, in a list, a dictionary and an array, is
written byte for byte as its twin over the primitives, and each reads the other's bytes. The suite checks this for every
underlying type, `Int128`, `UInt128` and `DateTimeOffset` included, which Orleans writes as it writes the primitives. The
same value object met twice in a message writes its text once and then a reference to it, as the same text would be.

A value object held by a member typed `object` or an interface is written with its type, so that the reader knows what
to build: an object naming the value object, the underlying value as its field 0, and the end of the object, the shape
Orleans' own generated codecs give a struct. A `Sku`, a `List<Sku>`, a `Quantity?[]` and a `Reference<PurchaseOrder>` in
an `object` member read back as themselves, and so does a `Sku` passed to a grain method taking `object`.

Orleans' own route, `AddJsonSerializer(isSupported: ValueObjectRegistry.IsValueObject)`, validates on read too, but
writes each value object as a type-tagged JSON document, 42 bytes for a holder of one `Sku` against 11 for the string,
and cannot read what a primitive member wrote.

## Reading

Reads are strict by default. A grain call crosses processes, and the bytes may come from a client the application does
not control, so a value read goes through `TryCreate`, which normalizes it, and refuses one the type rejects with
Orleans' `SerializerException`:

```text
SerializerException: The value read is not a valid Quantity: The value must be less than or equal to 100.
```

The message names the type and the rule, never the value; the rule's code is in the exception's `Data`, under
`ValueObjectErrors.ErrorCodeKey`, where `ValueObjectErrors.TryGetCode` finds it
([the code in an exception](../reference/errors.md#the-code-in-an-exception)). An element of a list, a value of a
dictionary and a value object in an `object` member are read the same way.

Whatever the trust, two values are refused, since no value object can hold them: a `null` read into a `Sku`, with
`value_object.required`, since a member that can hold nothing is a `Sku?`; and a value Orleans' codec of the underlying
type cannot read, a field of another wire type, a number beyond an `int` or a `char`, a day beyond `DateOnly`'s range,
with `value_object.not_parsable`, without that codec's own exception, whose message may quote the value. Bytes cut short
fail as Orleans' reader fails them, with an `IndexOutOfRangeException` and no code, as they would for the primitive.

`trusted: true` reads through `CreateUnchecked`, for a cluster whose silos and clients are all the application's own:

```csharp skip
silo.Services.AddSerializer(serializer => serializer.AddValueObjects(trusted: true));
```

The trust covers every peer of that silo or client, since Orleans serializes with one serializer per process, and grain
state stored through `OrleansGrainStorageSerializer`. During a rolling deployment, a cluster also reads values written
by a version whose rules were looser, which a strict read refuses.

## Writing

A value object that never went through `Create`, a `Quantity` property never set, holds the default value, which its
type may reject. The codec refuses to write it, with a `SerializerException` carrying the rule's code: a client sending
one gets the exception before anything is sent. A type that accepts its zero, an `Amount` with a minimum of 0, writes
it. Any other instance went through `Create`, or was read by a trusted codec, and is written as it is.

## Refusals at the caller

- **A refused request** fails on the caller with the silo's `SerializerException`, its message and its code intact; its
  stack trace holds the silo's frames. The grain is not invoked, and serves the next call.
- **A refused reply**, a grain returning a value its type rejects, created unchecked, fails on the caller with the same
  exception and code.
- **Between two silos**, a value a grain sends to a grain of another silo is written by the first and read by the
  second, which refuses a value its rules reject; the refusal travels back through the first grain to the client, with
  its code.

## A `ValueObjectException` from grain code

A grain method calling `Quantity.Create(500)` throws a `ValueObjectException`, which reaches the caller with its
message, its stack trace, its `ErrorCode` and its `ValueObjectType`. `AttemptedValue`, which holds the refused value,
does not travel, and neither does the inner exception. A subclass of `ValueObjectException` arrives as a
`ValueObjectException`, its own properties left behind, as Orleans' own exception codec leaves a derived exception's.
The type travels as its name, and is restored only where the caller loads it and its type filters allow it: a value
object only the silo knows reaches the caller with no type, its code intact.

## Local calls and default instances

Within one silo, Orleans copies the arguments of a call rather than serializing them, so a default instance a grain
sends to a grain of the same silo arrives, `IsDefault` true, with no rule run. Across processes, the sender's write
refuses the same value. A member the message leaves out, a field an older version did not write, holds the default
instance, which no rule checks: Orleans never asks the codec for it.

## Generic value objects and value objects written by hand

`AddValueObjects()` describes a construction of a generic value object, `Reference<PurchaseOrder>`, a value object
written by hand, and one in an assembly whose registration has not run, by reflection the first time Orleans asks for
them, which only the JIT can run. Without dynamic code it refuses them with a `NotSupportedException` naming the typed
registration, which serializes one value object without the registry:

```csharp skip
client.Services.AddSerializer(serializer => serializer.AddValueObject<Reference<PurchaseOrder>, string>());
```

`AddValueObject<TSelf, TValue>()` takes `trusted` too, allows its type through the type filter and carries a
`ValueObjectException` back as `AddValueObjects()` does. Each registration answers before the ones made earlier: called
after `AddValueObjects()`, it answers for its type with its own trust, and of two `AddValueObjects()` calls the last
one's trust applies.

## Grain keys

A grain is keyed by the underlying value, which any client chooses, so a grain reads its key back through the rules:

```csharp skip
var grain = grains.GetGrain<IOrderGrain>(orderId.Value);

// In the grain
if (!OrderId.TryCreate(this.GetPrimaryKey(), out var id))
{
    throw new ArgumentException("The grain's key is not an order identifier.");
}
```

## Grain state

Grain state goes through Orleans' storage serializers, not through the codec, except for
`OrleansGrainStorageSerializer`:

| Storage serializer | What a value object writes | A value its rules refuse |
| --- | --- | --- |
| `JsonGrainStorageSerializer`, the default, over Newtonsoft.Json | the type converter's text, `"Quantity":"3"` | `JsonSerializationException`, with no code |
| The same, with `AddValueObjects()` of AdCodicem.ValueObjects.NewtonsoftJson on `OrleansJsonSerializerOptions` | the underlying value, `"Quantity":3` | `JsonSerializationException`, carrying the code |
| `SystemTextJsonGrainStorageSerializer` | the underlying value, through the generated converter | `ValueObjectJsonException`, carrying the code |
| `OrleansGrainStorageSerializer` | this package's bytes | `SerializerException`, its code in `Data` |

```csharp skip
builder.Services.Configure<OrleansJsonSerializerOptions>(options => options.JsonSerializerSettings.AddValueObjects());
```

The Newtonsoft.Json row was checked on the serializer, not through a storage provider in a running silo.

## Versioning

The type name a value object carries where Orleans writes its type is its CLR name and assembly, `Shop.Sku,Shop`, and a
grain method returning a value object writes it in every result. Moving or renaming a value object therefore breaks
calls between two versions of a cluster during a rolling deployment. Orleans' `[Alias]` on the value object replaces the
name on the wire; Orleans' generator registers it in a project referencing Microsoft.Orleans.Sdk, and `VO0043` leaves it
alone:

```csharp skip
[ValueObject<string>(MaxLength = 8)]
[Alias("shop.sku")]
public readonly partial struct Sku;
```

## Native AOT

The package's own code is AOT-compatible: its typed path holds no reflection, and its boxed path describes an
unregistered value object by reflection only where the runtime supports dynamic code. Orleans 10.4 does not start in a
native binary, though: `AddSerializer` fails to load Orleans' own assemblies, and under the JIT with dynamic code off
Orleans' dependency injection cannot resolve the codec of a value type, an `int?` without this library included. The
native AOT application CI publishes leaves the package out until Orleans runs natively.

## Not covered

- **Streams, observers, `IAsyncEnumerable` grain methods and broadcast channels** holding value objects: not checked.
- **Orleans 9 and the releases before 10.4.0**: 10.4.0 is the floor.
- **A real rolling deployment** with a renamed value object: not run.
- **`[GenerateSerializer]` on a value object**, and Orleans' surrogates with `[RegisterConverter]`: a surrogate turns
  the value into a nested object on the wire and loses a refusal to `CodecNotFoundException`; `VO0043` fails the build
  on the attribute, so remove it ([A serializer's contract attribute](../reference/diagnostics.md#orleans)).
