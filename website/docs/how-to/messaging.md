---
title: Use With Messaging and Job Libraries
sidebar_label: Messaging and jobs
slug: /how-to/messaging
description: Send value objects through MassTransit, NServiceBus, Rebus, Kafka with Confluent's Schema Registry, the Azure messaging SDKs, Durable Functions, Dapr, Hangfire and Quartz as bare values, validated when they are received.
---

# Use with messaging and job libraries

None of these libraries needs a package written for it, but for the schema Confluent's serdes register with the
[Schema Registry](#kafka-with-confluents-schema-registry). One that serializes with System.Text.Json writes a value object
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

A saga correlates on a `Guid`: `CorrelateById(x => x.Message.OrderId.Value)` (not run). MassTransit.MessagePack is
[not covered](./messagepack.md#not-covered), `AdCodicem.ValueObjects.MessagePack` included: it writes each value object
as a wrapper holding `Value` and the private `_value`, and reads it back without validation, through a resolver no
application can reach. MassTransit 9 needs a commercial licence, and was not run.

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

## Kafka with Confluent's Schema Registry

Confluent's JSON serdes, `JsonSerializer<T>` and `JsonDeserializer<T>` of Confluent.SchemaRegistry.Serdes.Json,
serialize with Newtonsoft.Json, register the JSON Schema NJsonSchema generates for the message with the Schema Registry,
and validate every message against it before producing it. NJsonSchema describes a value object as the bare `string`
of its type converter, whatever its rules, so that with `ValueObjectConverter` in the settings the producer refuses its
own message, `Schema validation failed for properties: [#/Quantity, #/Consent, ...]`, one path per number and boolean
the converter writes as such. `AdCodicem.ValueObjects.NJsonSchema` describes each one as its underlying type instead,
with its rules: the converter first, then the package, and the same settings to both serdes.

```bash
dotnet add package AdCodicem.ValueObjects.NJsonSchema
```

```csharp skip
var schemaSettings = new NewtonsoftJsonSchemaGeneratorSettings();
schemaSettings.SerializerSettings.AddValueObjects();   // AdCodicem.ValueObjects.NewtonsoftJson
schemaSettings.AddValueObjects();                      // AdCodicem.ValueObjects.NJsonSchema

var serializer = new JsonSerializer<Order>(registry, new JsonSerializerConfig(), schemaSettings);
var deserializer = new JsonDeserializer<Order>(registry, new JsonDeserializerConfig(), schemaSettings);
```

Run on Confluent.SchemaRegistry.Serdes.Json 2.16.0 over a substitute registry client, no registry server:

- **The schema registered** holds one definition per value object, referred to wherever it is used, its underlying
  type with the pattern, lengths, bounds, `enum`, example (`x-example` in draft-04, which has no `examples`) and names
  of known values declared on it. A nullable member is the reference beside `null`, `oneOf`.
- **The message** carries numbers and booleans as such, `"Quantity":7`, `"Consent":true`, and passes the producer's
  validation. One holding a value its type refuses, a default `CustomerId`, is never produced: the converter refuses
  to write it, `JsonSerializationException` carrying `value_object.required`. A message whose value its type refuses,
  tampered with or written by another program, is refused by the consumer's converter with the code of the rule,
  `value_object.too_short` for an IBAN of two characters, which `ValueObjectErrors.TryGetCode` reads off the
  `JsonSerializationException`. Confluent's own `InvalidDataException`, from its validation, carries no code.
- **Without the converter**, the schema describes what the settings write, the type converter's text: numbers and
  booleans travel as strings, `"7"` and `"True"`, a refusal carries no code, and the default consumer cannot read a
  `DateTimeOffset` value object back, as [Dates](./json.md#dates) explains. Add the converter.
- **A value object is a member.** `JsonSerializer<T>` and `JsonDeserializer<T>` take a class, so a value object cannot be
  the key or the value type of a topic itself: it travels inside a message.
- **What Confluent validates.** It runs NJsonSchema's validator with no format validator, so `format: email` checks
  nothing. That validator throws `InvalidCastException` on a JSON integer beyond the range of a `long`, whatever the
  schema says, a `ulong` value object above `long.MaxValue` among them, with or without this package. It compares a
  closed set's values by their text, a real read as a `double`, so the `enum` of a closed set of decimals lists each as
  a double, `20.0` for `20.00`, and one no double holds rounded, `0.3333333333333333` for a third written to 28
  places, which the type refuses: give such a set values a double holds. An OpenAPI document, which no such validator
  reads, lists each as written.

**Moving a subject to the new schema.** A topic whose subject already holds the schema NJsonSchema wrote without the
package holds a bare `string` for each value object. Under the registry's `BACKWARD` compatibility, read from the
source of Confluent's JSON Schema checker and not run against a registry, every rule the new schema adds is a change it
refuses, `PATTERN_ADDED`, `MIN_LENGTH_ADDED`, `MAX_LENGTH_ADDED`, `MINIMUM_ADDED`, `MAXIMUM_ADDED`, and `TYPE_CHANGED`
for a number or a boolean, a `string` until then. Register it under a new subject, or change the subject's compatibility level for the
switch alone. A consumer with the converter reads the old numbers, `"7"` as 7, but not the old booleans, `"True"`.

## Azure Service Bus, Event Hubs, Event Grid and Storage Queues

A body built with `BinaryData.FromObjectAsJson` goes through System.Text.Json: it carries bare values, and is
validated when read. Received by an Azure Functions trigger, a message's binding data, its identifier or its insertion
time, binds to a value object through [the Functions converter](./azure.md#azure-functions-isolated-worker). A value
object put in `ApplicationProperties` or `EventData.Properties` throws at send,
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
