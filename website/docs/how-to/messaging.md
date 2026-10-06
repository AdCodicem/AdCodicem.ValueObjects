---
title: Use With Messaging and Job Libraries
sidebar_label: Messaging and jobs
slug: /how-to/messaging
description: Send value objects through MassTransit, NServiceBus, Rebus, Wolverine, the Azure messaging SDKs, Durable Functions, Dapr, Hangfire and Quartz as bare values, validated when they are received.
---

# Use with messaging and job libraries

None of these libraries needs a package written for it. One that serializes with System.Text.Json writes a value object
as its underlying value, through the converter the generator puts on it, and reads it back through `TryCreate`, so a
value the type rejects fails on receipt rather than reaching a handler. One that serializes with Newtonsoft.Json needs
the converter of `AdCodicem.ValueObjects.NewtonsoftJson`, added by `settings.AddValueObjects()`, as
[JSON](./json.md#newtonsoftjson) explains. Two rules hold for all of them:

- A message contract holding value objects makes its members required, so a producer that drops one fails the
  message rather than deliver a default instance, as
  [A member missing from the payload](./json.md#a-member-missing-from-the-payload) shows.
- A key the library reads as a primitive, a saga correlation property or an entity key, takes the underlying value,
  `.Value`.

Each section names the version it was run against. A recipe that was not run says so.

## MassTransit

On MassTransit 8.5.11, the default System.Text.Json serializer needs nothing: the values travel bare, and a value its
type rejects turns the receive into a fault carrying the rule's message, so the message goes to the error queue.

With Newtonsoft.Json, add the converter on both sides. Without it, an `int` value object goes out as `"7"`, and a
rejection says only `Could not cast or convert`. MassTransit.Newtonsoft sets `DateParseHandling.None` but leaves
`FloatParseHandling` at `Double`, which loses the scale and the digits of a `decimal`
([Numbers](./json.md#numbers)); `AddValueObjects()` sets both:

```csharp skip
x.UsingInMemory((context, cfg) =>
{
    cfg.UseNewtonsoftJsonSerializer();
    cfg.UseNewtonsoftJsonDeserializer();
    cfg.ConfigureNewtonsoftJsonSerializer(settings => settings.AddValueObjects());
    cfg.ConfigureNewtonsoftJsonDeserializer(settings => settings.AddValueObjects());
    cfg.ConfigureEndpoints(context);
});
```

The probe added the converter and the date handling by hand, without the float handling; `AddValueObjects()` adds the
three, and was not run through MassTransit itself.

Give a message initializer value objects, not raw values. Fed raw values, it drops what it cannot convert, and the
message holds defaults: a consumer received `{"orderId":"00000000-…","customer":"","quantity":0}`. The System.Text.Json
converter refuses to write a default its type rejects, so such a message fails to publish with a `JsonException`, and
only a value object that accepts its zero travels, as zero (inferred from the converter, not run).

A saga correlates on a `Guid`: `CorrelateById(x => x.Message.OrderId.Value)` (not run). MassTransit.MessagePack is not
covered: it writes each value object as a wrapper holding `Value` and the private `_value`, and reads it back without
validation. MassTransit 9 needs a commercial licence, and was not run.

## NServiceBus

On NServiceBus 10, `SystemJsonSerializer` needs nothing, and a value its type rejects sends the message to the error
queue without retries, as a `MessageDeserializationException` wrapping the `JsonException`.

A value object cannot be a saga correlation property:

```text
OrderId is not supported for correlated properties ... Guid,String,Int64,UInt64,Int32,UInt32,Int16,UInt16, or use a custom saga finder.
```

Correlate on a primitive property holding `.Value`, or write a saga finder. NServiceBus.Persistence.Sql 9.0.4 stores
saga data with Newtonsoft.Json, so its JSON settings need the converter; that part of its API was not run.

## Rebus

Rebus 8 serializes with System.Text.Json by default: nothing to do.

## Wolverine

On Wolverine 6.45.0, messaging needs nothing. Wolverine.Http binds route and query values through `IParsable<T>`, but
under its default a query value the type rejects reaches the handler as the default, and so does a missing one:

```text
GET /items?qty=500 -> 200 qty 0
GET /items -> 200 qty 0
```

Refuse what does not parse:

```csharp skip
app.MapWolverineEndpoints(options => options.RejectUnparseableQueryValues = true);   // the default from Wolverine 7
```

A rejected value is then a 400, without the rule's code. A missing value still binds the default, as Wolverine
documents, so declare an optional parameter `T?`.

## Azure Service Bus, Event Hubs, Event Grid and Storage Queues

A body built with `BinaryData.FromObjectAsJson` goes through System.Text.Json: it carries bare values, and is
validated when read. A value object put in `ApplicationProperties` or `EventData.Properties` throws at send,
`SerializationException: Serialization failed due to an unsupported type, Domain.OrderId.`. Put the underlying value
there, which AMQP carries natively, and read it back with `TryCreate`. Not `ToString()`: it writes what a formatting
hook writes, which does not always read back.

```csharp skip
message.ApplicationProperties["orderId"] = orderId.Value;

if (received.ApplicationProperties.TryGetValue("orderId", out var raw) && raw is Guid value
    && OrderId.TryCreate(value, out var id, out var validation))
{
    // ...
}
```

## Durable Functions, Durable Task and Dapr Workflow

`JsonDataConverter.Default` is System.Text.Json: inputs and outputs carry bare values, and are validated when read.
Only the converter was run, not an orchestration. An entity key is a string: pass `.Value`.

## Dapr

`DaprClient`, for service invocation, publish and subscribe, and state, serializes with System.Text.Json: nothing to
do. Strongly typed actor remoting goes through `DataContractSerializer`, which writes `<Customer/><OrderId/><Quantity/>`
and reads back defaults, unless the value objects' assembly opts into [XML serialization](xml.md), which makes it write
and validate them as it does any member (expected, not run). Or turn on JSON serialization in the actor proxy options,
as Dapr documents (not run).

## Hangfire

Hangfire 1.8.25 serializes job arguments with Newtonsoft.Json, without the converter by default: numbers go out as
strings, and a rejection loses its rule. Add the converter:

```csharp skip
GlobalConfiguration.Configuration.UseRecommendedSerializerSettings(settings => settings.AddValueObjects());
```

`AddValueObjects()` also sets `FloatParseHandling.Decimal` and `DateParseHandling.None`, which the probe set by hand.
Without the first, a `decimal` argument came back with its scale and digits lost: `12.50` as `12.5`. A job stored
before the converter was added still loads, since the converter reads a number written as a string, as the type
converter wrote it; a boolean value object is the exception, written as `"True"`, so drain the jobs carrying one
first. A tampered argument fails the job with the rule's message.

## Quartz

Quartz 4.3.0 binds job data through the generated `TypeConverter`, but under its default
`PropertyMismatchBehavior.Ignore`, a value the type rejects leaves the property default, and the job runs. Set
`PropertyMismatchBehavior.Throw`, or use `IJob<TInput>`, which reads its input through System.Text.Json and refuses an
invalid payload with the value object's message.
