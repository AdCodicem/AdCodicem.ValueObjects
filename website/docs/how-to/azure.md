---
title: Use With Azure SDKs
sidebar_label: Azure
slug: /how-to/azure
description: Store value objects in Cosmos DB through the SDK's System.Text.Json serializer, cache them with HybridCache, and find the Azure sections of the other guides.
---

# Use with Azure SDKs

## Cosmos DB SDK

The Cosmos DB SDK v3 serializes with Newtonsoft.Json by default, without the converter, so it goes through the
generated type converter, as [JSON](./json.md#without-the-converter) describes: every value object is written as a
string, a number included, `"Quantity":"3"`, a LINQ query compares strings, `root["Quantity"] > "2"`, and a rejected
value loses its rule. Switch the client to System.Text.Json:

```csharp skip
var client = new CosmosClient(connectionString, new CosmosClientOptions
{
    UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions(),
});
```

Documents then carry bare values, a value its type rejects throws when read, and `x.Quantity > q` becomes
`root["Quantity"] > 2`. Compare value objects in a query, never their `.Value`, which either serializer translates to
`root["Iban"]["Value"]`, a path no document holds. This was checked offline, on what the serializer writes and on the
text of the queries, without a round trip to a server. Under native AOT, give the options a source-generated context
naming `ValueObjectJsonConverterFactory`, as [JSON](./json.md#systemtextjson-source-generated) shows (not run).

## HybridCache

HybridCache needs nothing: its distributed tier holds bare values, read back through the value object's rules when
an entry is loaded into a fresh in-memory tier. An entry written before a rule tightened, and which no longer passes
it, throws on every read until it expires:

```text
JsonException: The value is not a valid CustomerCode: The value does not match the expected format.
```

Put a version in the key prefix, and bump it when a value object's rules tighten. Under native AOT, give HybridCache
a source-generated context naming the factory (not run).

## Elsewhere in the guides

- Azure Service Bus, Event Hubs, Event Grid and Storage Queues, Durable Functions, and Dapr:
  [Messaging and jobs](./messaging.md#azure-service-bus-event-hubs-event-grid-and-storage-queues).
- The Cosmos DB provider for Entity Framework Core: [Other providers](./ef-core.md#other-providers). Azure SQL's
  [Always Encrypted](./ef-core.md#always-encrypted) and [dynamic data masking](./ef-core.md#dynamic-data-masking)
  have sections of their own there.
- Azure Cosmos DB for MongoDB and Azure DocumentDB, through MongoDB.Driver:
  [MongoDB](./mongodb.md#azure-cosmos-db-for-mongodb-and-azure-documentdb).
- Azure.Data.Tables, which leaves every value object of a class it reads default:
  [Where a default instance can come from](../reference/default-instances.md).
