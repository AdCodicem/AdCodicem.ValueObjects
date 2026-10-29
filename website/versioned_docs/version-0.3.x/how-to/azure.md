---
title: Use With Azure
sidebar_label: Azure
slug: /how-to/azure
description: Bind value objects in the Azure Functions isolated worker, with problem details for its HTTP triggers, store them in Cosmos DB through the SDK's System.Text.Json serializer, cache them with HybridCache, and find the Azure sections of the other guides.
---

# Use with Azure

## Azure Functions isolated worker

In the isolated worker, a route value, a query value and other binding data, such as a queue message's `Id` or
`InsertionTime`, reach a function parameter through the worker's input converters, none of which reads the
`[TypeConverter]` the generator emits: the text falls through to the worker's JSON conversion, which reads it as JSON.
A valid `Guid`, text or a date is refused, and the invocation fails with a 500 and an empty body. An `int` value object
binds `7` only because `7` is valid JSON, and `007` fails, as it does for a plain `int`. A value its rules refuse is a
500 too, its rule and its code lost in the host's log, which quotes part of the value:
`'not-a-guid' is an invalid JSON literal`.

Two packages, each opted into:

```bash
dotnet add package AdCodicem.ValueObjects.AzureFunctions             # any application of the worker
dotnet add package AdCodicem.ValueObjects.AzureFunctions.AspNetCore  # HTTP triggers on the ASP.NET Core integration
```

```csharp skip
var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.UseValueObjects();                // route values, query values and binding data through TryParse
builder.UseValueObjectProblemDetails();   // a refusal answered 400 with its code: after ConfigureFunctionsWebApplication()
builder.Build().Run();
```

With `HostBuilder`, both go inside the integration's call:

```csharp skip
new HostBuilder()
    .ConfigureFunctionsWebApplication(worker => worker.UseValueObjects().UseValueObjectProblemDetails())
    .Build()
    .Run();
```

The converter's package needs the worker alone: a queue- or timer-triggered application calls `UseValueObjects()` and
takes no ASP.NET Core. Calling either twice registers once.

### Route values, query values and binding data

`UseValueObjects()` puts `ValueObjectInputConverter` first among the worker's registered converters, or, called before
`ConfigureFunctionsWebApplication()`, right behind the ASP.NET Core integration's own converter, which binds an
`HttpRequest` and an `HttpRequestData` alone. It reads a parameter whose type, nullable or not, is a value object, from
the text the host bound to it:

- through the type's `TryParse`, in the invariant culture: `/api/orders/3f2504e0-…`, `?customer=ABC-12345`,
  `/api/lines/007`, `/api/due/2026-10-13` and a queue message's `Id` and `DequeueCount` bind;
- then, when the text is refused and is a JSON string literal, as the string it encodes, whose verdict then stands: the
  host sends binding data of another type than `string` as JSON, so a queue message's `InsertionTime` arrives as
  `"2026-10-13T10:13:54+00:00"`, quotes included;
- for a value object over `decimal`, when the text is refused and is a JSON number, as the number the worker's JSON
  conversion reads, whose verdict then stands: a decimal's `TryParse` takes no exponent, and the host writes a
  message's number through a double, so `{"Rate":0.0000001}` hands a `Rate` parameter `1E-07`, and `/api/amounts/1e2`
  binds 100;
- empty text is parsed too, and refused by the type's own rule, for a nullable value object too: `?customer=` is
  `value_object.required` for a value object over text, `value_object.not_parsable` for one over a `Guid`;
- an absent value is left to the worker, which binds `null` to a nullable value object and its default value to an
  optional parameter, and fails a required one with its own message, "Could not populate the value for 'customer'
  parameter".

A generated value object registers itself. One written by hand, or a construction of a generic one, is described by
reflection the first time the converter meets it, under the JIT the worker runs on.

A refusal fails the parameter with a `FormatException` whose `Data` holds the code under
`ValueObjectErrors.ErrorCodeKey`, and whose message names the type, the rule and its code, never the refused text. The
worker keeps the text of that exception alone, in the `FunctionInputConverterException` it throws with no inner
exception, where `ValueObjectErrors.TryGetCode` finds nothing: the code therefore ends the message, which is what the
host logs and an alert reads.

```text
Error converting 1 input parameters for Function 'Line': Cannot convert input parameter 'qty' to type 'MyApp.Quantity' from type 'System.String'. Error:System.FormatException: The value is not a valid Quantity: The value must be less than or equal to 100. (value_object.out_of_range)
```

For an application that bound value objects through JSON before: text is now read through the rules first, so `007`
binds, and a quoted value bound to a value object over text that accepts quotes keeps them, where JSON stripped them.

### Problem details for HTTP triggers

On the ASP.NET Core integration, `UseValueObjectProblemDetails()` adds a worker middleware that answers an HTTP trigger
whose value objects the worker could not bind with the validation problem a minimal API writes
([problem details for minimal APIs](./aspnet-core.md#problem-details-for-minimal-apis)), every refused value object
listed with the rule it broke, and the code of each under `errorCodes`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "id": ["The text is not a valid System.Guid."], "qty": ["The value must be less than or equal to 100."] },
  "errorCodes": { "id": "value_object.not_parsable", "qty": "value_object.out_of_range" }
}
```

- The worker's failure holds the converter's text alone, so the middleware parses each value-object parameter the
  converter reads again, from the binding data, which holds the route values and every query value as text, as the
  minimal API filter parses its parameters again. A required one with no binding data is listed with "A value is
  required." and `value_object.required`.
- A failure no value object explains keeps the worker's 500: a plain `int` its JSON conversion refuses, `/api/ints/007`,
  or a binding of another kind. Beside a refused value object, such a parameter is not listed, having no rule code.
- The host counts an answered invocation as succeeded, as ASP.NET Core counts a 400, so an alert on failed invocations
  sees fewer. The middleware logs each answer once, through the function's logger, with the parameters and their codes,
  never the values: `Function 'Line' answered 400 for refused value objects: qty (value_object.out_of_range).`

The order matters. The integration's middleware, which executes the answer on the request, has to run outside this
one: call `UseValueObjectProblemDetails()` after `ConfigureFunctionsWebApplication()`, or inside
`ConfigureFunctionsWebApplication(worker => …)`. Registered before it, the middleware would swallow the failure where
nothing writes the answer, and the client would get an empty 200. It tells the two orders apart by the request the
integration sets on the invocation, which is there when the middleware starts only when the integration runs outside
it: otherwise it keeps the worker's 500, for a refused route or query value and a refused body alike, and logs a
warning, once, naming the call to move.

#### A request body

A `[FromBody]` parameter is read by MVC, through the integration's own converter, which the value object converter
never sees. With the MVC package's `AddValueObjects()`, from `AdCodicem.ValueObjects.AspNetCore`, MVC's JSON input
formatter records the code of the value object it refused, and the middleware answers it under its JSON path, beside a
refused route value:

```csharp skip
builder.Services.AddMvc().AddValueObjects();
```

```json
{
  "errors": {
    "$.quantity": ["The value is not a valid Quantity: The value must be less than or equal to 100."],
    "qty": ["The value must be less than or equal to 100."]
  },
  "errorCodes": { "$.quantity": "value_object.out_of_range", "qty": "value_object.out_of_range" }
}
```

Without that call, the integration's `InvalidOperationException`, which holds MVC's messages alone, keeps the worker's
500. System.Text.Json stops at the first refusal, so a body is listed once; its message is MVC's, in the worker's
culture. A body read by Newtonsoft.Json's formatter, which may record several codes, was not checked: each member it
records is given every message of the integration's exception, since nothing tells which belongs to which.

#### Messages in the request's language

The messages can be the application's, read from a resource by the code of the rule, as on a minimal API
([messages in the application's language](./aspnet-core.md#messages-in-the-applications-language)):

```csharp skip
builder.UseValueObjectProblemDetails(messages => messages.UseResource<ValidationMessages>());
builder.Services.AddLocalization();
builder.Services.AddRequestLocalization(options => options
    .SetDefaultCulture("en-US")
    .AddSupportedCultures("en-US", "fr-FR")
    .AddSupportedUICultures("en-US", "fr-FR"));
```

An invocation runs on the host's call, not on the request's, so the worker's culture is never the request's. The
middleware applies the culture of `IRequestCultureFeature`, which `UseRequestLocalization()` sets when the application
adds it to the integration's pipeline through a startup filter, and otherwise lets the application's
`RequestLocalizationOptions` choose one from the request, from `?culture=`, the cookie or `Accept-Language`, as that
middleware would. Registered without options, the framework's defaults keep the current culture.

```csharp skip
builder.Services.AddTransient<IStartupFilter, RequestLocalizationStartupFilter>();

sealed class RequestLocalizationStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseRequestLocalization();
        next(app);
    };
}
```

A key the resource lacks keeps the English message, and a resource named without `AddLocalization()` fails the first
refusal with an `InvalidOperationException` naming the call.

### Payloads

A parameter carrying an input or trigger binding attribute, `[QueueTrigger]`, `[ServiceBusTrigger]` or `[BlobInput]`
among them, is its binding's: its payload goes to the binding's converters and to the worker's serializer, which reads
the generated JSON converter. A message holding value objects binds, and one a value object refuses fails with a
`ValueObjectJsonException` carrying the code. A message that is a bare value object is read as JSON: `"ABC-12345"`
binds, `ABC-12345` fails. To read a raw-text payload through the value object's rules, name the converter on the
parameter, which needs no `UseValueObjects()`:

```csharp skip
[Function("Codes")]
[QueueOutput("results")]
public static string Codes([QueueTrigger("codes")][InputConverter(typeof(ValueObjectInputConverter))] CustomerCode code)
    => code.Value;
```

It reads text alone, a JSON string literal decoded: a payload its binding hands over as bytes, such as a Service Bus
body, stays the binding's (not checked).

### What it does not cover

- The built-in HTTP model, `HttpRequestData` without the ASP.NET Core integration: the converter binds there, and a
  refusal keeps the worker's 500.
- A query parameter named `code`, which the host keeps for function keys and never hands a function.
- An array of value objects, and a header.
- Native AOT, which the isolated worker does not support (azure-functions-dotnet-worker#2899): neither package claims
  AOT compatibility, though each closes its conversion of each value object through the descriptor's visitor.
- The inputs of Durable Functions, which its own serializer reads
  ([Durable Functions](./messaging.md#durable-functions-durable-task-and-dapr-workflow)), and `Azure.Functions.Sdk`,
  Microsoft's newer MSBuild SDK, which was not checked.

Both packages are built and tested against Microsoft.Azure.Functions.Worker 2.52 and the ASP.NET Core integration 2.1,
in Microsoft's `dotnet-isolated` host image. An application on an older worker that installs the converter alone gets
Worker.Core raised to 2.52, as Microsoft's own worker extensions raise it, and the ASP.NET Core package raises the worker
with it.

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

- Azure Service Bus, Event Hubs, Event Grid and Storage Queues, Durable Functions, and Dapr, outside a Functions trigger:
  [Messaging and jobs](./messaging.md#azure-service-bus-event-hubs-event-grid-and-storage-queues).
- The Cosmos DB provider for Entity Framework Core: [Other providers](./ef-core.md#other-providers). Azure SQL's
  [Always Encrypted](./ef-core.md#always-encrypted) and [dynamic data masking](./ef-core.md#dynamic-data-masking)
  have sections of their own there.
- Azure Cosmos DB for MongoDB and Azure DocumentDB, through MongoDB.Driver:
  [MongoDB](./mongodb.md#azure-cosmos-db-for-mongodb-and-azure-documentdb).
- Azure.Data.Tables, which leaves every value object of a class it reads default:
  [Where a default instance can come from](../reference/default-instances.md).
