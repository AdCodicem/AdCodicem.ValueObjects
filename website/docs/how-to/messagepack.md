---
title: Use with MessagePack and SignalR
sidebar_label: MessagePack and SignalR
slug: /how-to/messagepack
description: Put a resolver in front of MessagePack's that writes every value object as the bare value its underlying type writes, reads it back through its rules, and refuses to write an uninitialized value object its type rejects, on SignalR's MessagePack hub protocol too.
---

# Use with MessagePack and SignalR

```bash
dotnet add package AdCodicem.ValueObjects.MessagePack
```

```csharp skip
var options = MessagePackSerializerOptions.Standard.WithValueObjects();
var bytes = MessagePackSerializer.Serialize(order, options);

builder.Services.AddSignalR().AddMessagePackProtocol(options => options.UseValueObjects());
```

MessagePack has no way to treat a value object as its underlying value, and what happens without the package depends
on the resolver:

- **`StandardResolver`**, MessagePack's default, knows no formatter for a value object: the first `Serialize` of one, or
  of an object holding one, throws `FormatterNotRegisteredException`.
- **`ContractlessStandardResolver`**, which writes any type through its public members, finds none it can use on the
  generated struct: a value object is written as an empty map, `{}`, and read back as a default instance. Nothing
  throws, and no rule ran.
- **SignalR's MessagePack hub protocol** uses a contractless resolver of its own: a value object passed to a hub method
  goes on the wire as `{}`, and the hub receives a default `Iban` and a `Quantity` of `0`, whatever minimum the type
  declares. SignalR's JSON hub protocol is not affected: System.Text.Json honours the converter the generator writes.

`WithValueObjects()` returns options whose resolver is a composite of `ValueObjectResolver` and the options' own: the
value-object resolver answers first, for every value object whichever assembly declares it, and the options' resolver
answers every other type. Nothing else of the options changes, their `Security` and `Compression` included. Each
formatter is a `ValueObjectFormatter<TSelf, TValue>`, closed over its value object at compile time, through the type
arguments its descriptor hands back ([Run-time lookup](runtime-lookup.md#back-to-the-typed-path)), and built once.

Code that passes no options goes through `MessagePackSerializer.DefaultOptions`, which the application can set once:

```csharp skip
MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard.WithValueObjects();
```

On a contractless resolver, the same call: `ContractlessStandardResolver.Options.WithValueObjects()`. Called twice, the
last call answers value objects first, so its trust is the one that applies.

## What a value object writes

The formatter writes `value.Value` through the formatter the options' resolver holds for the underlying type, so a value
object writes exactly the bytes the primitive it replaces writes under the same options, and reads back what that
primitive reads back. A `Native*` resolver placed in the chain is honoured:

| Underlying type | `MessagePackSerializerOptions.Standard` | With `NativeGuidResolver`, `NativeDecimalResolver`, `NativeDateTimeResolver` |
| --- | --- | --- |
| `string` | a string | the same |
| `bool` | a boolean | the same |
| `char` | an integer, its UTF-16 code unit | the same |
| the integers, `Int128` and `UInt128` aside | the shortest integer that holds it | the same |
| `Int128`, `UInt128` | 16 bytes, as binary | the same |
| `float`, `double` | a float 32, a float 64 | the same |
| `decimal` | a string, `"12.345"` | 16 bytes, as binary |
| `Guid` | a 36-character string, 38 bytes on the wire | 16 bytes, as binary, 18 bytes on the wire |
| `DateTime` | a timestamp extension, read back in UTC | its `ToBinary()` 64-bit form, its kind kept |
| `DateTimeOffset` | an array of a timestamp and the offset in minutes | the same |
| `DateOnly` | its day number | the same |
| `TimeOnly`, `TimeSpan` | their ticks | the same |

The suite checks this byte for byte for every underlying type, under both option sets. A formatting hook of the value
object, `IValueObjectFormatter<T>`, never reaches the wire. A value object written by hand over a type of its own
choosing, a `Uri`, goes through the formatter MessagePack holds for that type.

A `TSelf?` is MessagePack's own `NullableFormatter`: one holding nothing is written as `nil` and read back as
`null`, and one holding a value asks the resolver for the value object's formatter. An object declared for MessagePack,
`[MessagePackObject]` with `[Key]` members, a collection, or a dictionary keyed by a value object writes each value
object as its value, so an object of value objects is written as the same object of the primitives they replace:

```text
["6f1c2a6e-1f0e-4c39-9d43-2b6c7f1b2a10","FR7630006000011234567890189",3,null,{"DE89370400440532013000":7}]
```

## Reading

Reads are strict by default. MessagePack is a wire format, and the bytes may come from another service or a client, so
a value read goes through `TryCreate`, which normalizes it — a lowercase IBAN with spaces reads as
`FR7630006000011234567890189` — and refuses one the type rejects:

```text
MessagePackSerializationException: Failed to deserialize Shop.Order value.
 ---> MessagePackSerializationException: The value read is not a valid Iban: The value must be at least 15 characters
long.
```

MessagePack wraps the formatter's exception, which carries the rule's code in its `Data`, in its own;
`ValueObjectErrors.TryGetCode` finds the code through the wrapping
([the code in an exception](../reference/errors.md#the-code-in-an-exception)). No message quotes the value read.

A trusted formatter reads through `CreateUnchecked`, for bytes the application alone wrote, a cache among them, where
validating each value again only costs time:

```csharp skip
var cacheOptions = MessagePackSerializerOptions.Standard.WithValueObjects(trusted: true);
```

Whatever the trust, two values are refused, since no value object can hold them: a `nil` read into an `Iban`, with
`value_object.required`, since a member that can hold nothing is an `Iban?`; and a value the formatter of the underlying
type cannot read — a string where a number is expected, a number beyond the range of a `byte` or a `char`, a day beyond
`DateOnly`'s range, a 64-bit form beyond `DateTime`'s under `NativeDateTimeResolver`, text no `Uri` parses, bytes cut
inside the value or before it — with `value_object.not_parsable`, without the formatter's own exception, whose message
may quote the value. Any other exception of that formatter, and options holding no formatter for the underlying type at
all (`FormatterNotRegisteredException`), propagate as they are: they are a misconfiguration, not a value refused. An
array or a map that announces more elements than bytes remain is refused by MessagePack's reader before any value
object is read, with an `EndOfStreamException` and no code, as an array of primitives is.

## Writing

A value object that never went through `Create`, an `Iban` property never set, holds the default value, which its type
may reject. The formatter refuses to write it, with a `MessagePackSerializationException` carrying the rule's code,
inside MessagePack's own "Failed to serialize … value.": alone, as a dictionary key, and inside an `Iban?` holding a
default `Iban`. A type that accepts its zero, a `Quantity` with a minimum of 0, writes it. Any other instance went
through `Create`, or was read by a trusted formatter, and is written as it is.

## Security and value-object keys

MessagePack's `MessagePackSecurity.UntrustedData`, which SignalR's options set, and recommend for options that replace
them, guards a dictionary or a set against hash collisions with a comparer of its own, which it has only
for the primitive types. A dictionary keyed by a value object, or a `HashSet` of value objects, is then refused on read:

```text
MessagePackSerializationException: Failed to deserialize System.Collections.Generic.Dictionary`2[…] value.
 ---> TypeAccessException: No hash-resistant equality comparer available for type: Shop.Iban
```

The package adds no comparer: a value object's equality may differ from its underlying value's, as one declared with
`Comparison = StringComparison.OrdinalIgnoreCase` does, so hashing the underlying value would break such a type. Key
such a member by the underlying type, or give the options a security of your own that compares a value object as its
own equality does, which costs those keys MessagePack's protection against collisions:

```csharp skip
public sealed class ValueObjectKeysSecurity(MessagePackSecurity copyFrom) : MessagePackSecurity(copyFrom)
{
    protected override IEqualityComparer<T> GetHashCollisionResistantEqualityComparer<T>()
        => ValueObjectRegistry.IsValueObject(typeof(T))
            ? EqualityComparer<T>.Default
            : base.GetHashCollisionResistantEqualityComparer<T>();

    protected override MessagePackSecurity Clone() => new ValueObjectKeysSecurity(this);
}

options = options.WithSecurity(new ValueObjectKeysSecurity(MessagePackSecurity.UntrustedData));
```

Keys are read through the rules too, whatever the security, so two keys the sender held apart can normalize to one
value object: `FR7630006000011234567890189` and `fr76 3000 6000 0112 3456 7890 189`. MessagePack's dictionary formatter
then fails to add the second, with an `ArgumentException` of its own that carries no code and quotes the key, "An item
with the same key has already been added. Key: FR7630006000011234567890189", which a log of the exception then holds; a
`HashSet` keeps one of them. The package cannot step in: each key is refused or accepted alone, and the dictionary adds
them. A trusted read normalizes nothing, and keeps both.

## SignalR

`UseValueObjects()` puts the resolver in front of the hub protocol's own, keeping its `UntrustedData` security, and
returns the same options. Both ends need it, the server and the .NET client:

```csharp skip
builder.Services.AddSignalR().AddMessagePackProtocol(options => options.UseValueObjects());

var connection = new HubConnectionBuilder()
    .WithUrl(url)
    .AddMessagePackProtocol(options => options.UseValueObjects())
    .Build();
```

A value object then travels as its value, a hub method receives the value sent, and a `Quantity?` sent as `null`
arrives `null`. An argument the value object refuses fails the invocation before the hub method runs:

- The client gets a `HubException`, "Failed to invoke 'Add' due to an error on the server.", and with
  `EnableDetailedErrors` the binding failure after it, "InvalidDataException: Error binding arguments. Make sure that
  the types of the provided values match the types of the hub method being invoked." The rule's code reaches the client
  in neither case.
- The server logs the failure at `Debug`, category `Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcher`, event
  `InvalidHubParameters`, "Parameters to hub method 'Add' are incorrect.", with an `InvalidDataException` whose chain
  carries the code, which `ValueObjectErrors.TryGetCode` finds.

A result the client's formatter refuses fails the invocation on the client, with a `HubException`, "Error trying to
deserialize result to Quantity.", and no code. An uninitialized value object its type rejects is refused before it is
sent: on the client, the invocation throws the `MessagePackSerializationException` of [Writing](#writing), carrying the
code, and the connection stays open; on the server, a hub method returning one fails to write its result, the server
logs `FailedWritingMessage` at `Error`, category `Microsoft.AspNetCore.SignalR.HubConnectionContext`, with the code in
the exception's chain, and closes the connection, which the client reports as a `HubException`, "The server closed the
connection with the following error: Connection closed with an error." A client that does not call `UseValueObjects()`
writes `{}`, which a server that does refuses, with `value_object.not_parsable` in its log, rather than read a default
instance. A dictionary keyed by a value object, as a hub method's parameter, is refused under the hub protocol's
security, as [above](#security-and-value-object-keys).

The JavaScript client's MessagePack protocol sends the values a script holds, strings and numbers, which is what the
server reads; this was not run. `UseValueObjects(trusted: true)` reads without validation, which suits a hub only when
the application wrote every message it reads.

The package depends on the hub protocol, so an application that uses MessagePack alone gets
`Microsoft.AspNetCore.SignalR.Protocols.MessagePack` and SignalR's common packages too: a separate package for the
SignalR call would not earn its place.

## Generic value objects and value objects written by hand

A construction of a generic value object, `Reference<PurchaseOrder>`, a value object written by hand, and one in an
assembly whose registration has not run, are described by reflection the first time MessagePack asks for them, which
only the JIT can run. The resolver keeps the formatter it built for each.

## Native AOT

The package is not AOT-compatible, and the native AOT application CI publishes does not use it:

- MessagePack 3.1 carries no trimming annotation, and a native publish that uses it warns for the whole assembly
  (`IL2104`, `IL3053`).
- MessagePack builds the formatters of a `Nullable<T>`, a collection and a dictionary by reflection
  (`DynamicGenericResolver`), which fails in a native binary for any struct, a value object included.
- SignalR's MessagePack hub protocol reads arguments through MessagePack's non-generic API, which requires dynamic
  code, and `AddMessagePackProtocol` is marked `[RequiresUnreferencedCode]`. Microsoft supports only the JSON hub
  protocol under native AOT.

The package's own formatters are closed through the descriptor's visitor, never with `MakeGenericType`, and a bare value
object round trips in a native binary through them; what fails is MessagePack's and SignalR's.

## Not covered

- **MassTransit.MessagePack.** It writes each value object as a map holding `Value` and the private `_value`, and
  reads it back without validation, through a resolver that is internal: only a `[MessagePackFormatter]` on the type
  could reach it. Use MassTransit's default System.Text.Json serializer, which writes the bare values and validates
  them ([Messaging](messaging.md#masstransit)).
- **MsgPack003.** MessagePack's analyzer comes with MessagePack, and so with this package: it fails the build of a
  `[MessagePackObject]` type holding a value object declared in the same assembly, since no formatter it can see
  covers the value object, and the resolver is chosen at run time. Declare the value objects in a referenced project,
  the usual domain layout, or assume them formattable, one attribute per type:

  ```csharp skip
  [assembly: MessagePackAssumedFormattable(typeof(Iban))]
  ```

- **Compile-time composition.** `[MessagePackKnownFormatter]` cannot name the formatter: MessagePack's generator ignores
  a generic formatter, and naming several closed constructions of one crashes its analyzer and its generator. An
  application that composes its resolver at compile time lists `ValueObjectResolver.Instance` first in its composite;
  this was not run.
- **`MessagePackSerializer.Typeless`**, whose default options hold no resolver of the application's: it refuses a
  value object ("can't find matched constructor").
- **`AnyEntityId`**, which is not a value object: the resolver leaves it to the options' own, and `StandardResolver`
  has no formatter for it. Send the identifier's text.
- **Nerdbank.MessagePack**, a different library, built on a source generator, which would need converters of its own.
