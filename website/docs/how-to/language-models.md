---
title: Language Models, Tools and Structured Output
sidebar_label: Language models
slug: /how-to/language-models
description: Describe value objects in the tool and structured-output schemas Microsoft.Extensions.AI sends a model, and in the tools of a Model Context Protocol server, and answer an argument a value object refuses with its rule code, with AdCodicem.ValueObjects.AI and AdCodicem.ValueObjects.ModelContextProtocol; Agent Framework included.
---

# Language models: tools and structured output

A model that calls a tool, or answers in a structured form, is told what to send by a JSON Schema, and what it sends
is bound to your method's parameters, or to the type of the answer, through System.Text.Json. A value object has a
converter of its own, so neither step knows what it accepts: the schema says nothing, and a refusal never reaches the
model. `AdCodicem.ValueObjects.AI` carries the rules to both, for Microsoft.Extensions.AI and what is built on it, and
`AdCodicem.ValueObjects.ModelContextProtocol` to the tools of a [Model Context Protocol server](#model-context-protocol-servers).

This page is about the models an application calls. [Using an AI coding agent](./ai-agents.md) is about the agent that
writes the code.

## Without the package

`AIFunctionFactory.Create` builds a tool's schema from the `JsonTypeInfo` of each parameter, through
`AIJsonUtilities.CreateJsonSchema`, and a type with a converter of its own has none there. Each value-object
parameter is published as `true`, the schema that accepts anything; a `[Description]` on the parameter alone survives:

```json
{"type":"object","properties":{"iban":{"description":"The account to debit."},"quantity":true,"country":true},"required":["iban","quantity","country"]}
```

Under OpenAI's strict mode, Microsoft.Extensions.AI.OpenAI sends each such parameter as `{}`. The model gets no length,
pattern, bound or allowed value, and guesses.

Binding does validate: the generated converter runs for each argument, and refuses a value its rules reject. But the
refusal never reaches the model. `FunctionInvokingChatClient` answers the exception with `Error: Function failed.`;
`IncludeDetailedErrors = true` adds its message, but also the message of every exception any tool throws, which is why
it is off by default, and still gives the model no code. A JSON `null` is worse: Microsoft.Extensions.AI hands it to a
value object that cannot be `null` as its uninitialized instance, which no rule checked, and the method runs with it.

Structured output has the same gap: `GetResponseAsync<T>` sends a `json_schema` whose value objects are `true`, and a
`List<Iban>` is `"items": {}`. Its schema options are private, so no transform reaches them.

## Tools

```
dotnet add package AdCodicem.ValueObjects.AI
```

Two calls per tool: the schema options, and the wrapper.

```csharp skip
using AdCodicem.ValueObjects.AI;

AIFunction placeOrder = AIFunctionFactory.Create(
        tools.PlaceOrder,
        new AIFunctionFactoryOptions { JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions().WithValueObjects() })
    .WithValueObjectValidation();

// FunctionInvokingChatClient calls the tools the model asks for, and hands it their results.
IChatClient client = chatClient.AsBuilder().UseFunctionInvocation().Build();
var response = await client.GetResponseAsync(messages, new ChatOptions { Tools = [placeOrder] });
```

`WithValueObjects()` returns a copy of the options, every other setting kept, with a transform that describes each
value object, after any transform the options already carry; `null` starts from `AIJsonSchemaCreateOptions.Default`,
which stays as it is. `WithValueObjectValidation()` wraps the function, once: wrapping it again returns it.

### What the schema says

Each value object, alone, nullable, or in a collection, a dictionary or an object, is described as the
[JSON Schema core](./json.md#json-schema) describes it for a language model
(`ValueObjectJsonSchemaProfile.LanguageModel`): its JSON type alone, its lengths, its pattern, its numeric bounds, a
closed set as `enum`, a date bound as a sentence of the description, its description after the parameter's, and its
example. A format JSON Schema does not define, `iban`, `int32` or `decimal`, moves into the description:

```json
"iban": {
  "description": "The account to debit.\n\nAn international bank account number.\n\nFormat: iban.",
  "type": "string",
  "pattern": "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
  "minLength": 15,
  "maxLength": 34,
  "examples": ["FR7630006000011234567890189"]
},
"quantity": {"description": "A quantity ordered.\n\nFormat: int32.", "type": "integer", "minimum": 1, "maximum": 100},
"country": {"description": "A country the shop delivers to.", "type": "string", "enum": ["FR", "DE"]}
```

Under strict mode, Microsoft.Extensions.AI.OpenAI moves what strict mode refuses, the lengths, the pattern, the
bounds and the format, into the description, and keeps `type`, `enum` and `examples`; the rules still reach the model:

```json
"quantity": {"description": "A quantity ordered.\n\nFormat: int32.\nminimum: 1\nmaximum: 100", "type": "integer"}
```

### A refused argument

Before the function binds its arguments, the wrapper reads each argument of a value-object parameter as the binding
reads it, through the contract the function's serializer options hold for the parameter: the value object's own
converter, its wire form, a number read from text where the options allow it, and its rules, through `TryCreate`. An
argument is refused exactly when binding it would throw. The first refused, in the order of the parameters, is
returned instead of calling the function, and `FunctionInvokingChatClient` hands the model a result as it is:

```json
{"error":"invalid_argument","argument":"quantity","code":"value_object.out_of_range","message":"The value is not a valid Quantity: The value must be less than or equal to 100."}
```

- `error` is always `invalid_argument`, which tells a refusal from a result that happens to be an object.
- `argument` is the name the model sent it under, an `[AIParameterName]` included.
- `code` is the rule's [code](../reference/errors.md#framework-codes), `value_object.not_parsable` for a token the value
  object cannot read at all, and `value_object.required` for an absent argument the parameter has no default for, and
  for a JSON `null` sent to a value object that cannot be `null`.
- `message` is the converter's, which names the value object and the rule, never the value sent; an absent argument,
  and a `null` the host hands over as a C# `null`, as the OpenAI adapter does, get `A value is required.`. Where the
  converter writes none, for a number its underlying type cannot hold, which System.Text.Json would describe by the
  path of the value, a dictionary's keys included, and where a converter of your own, or System.Text.Json reading the
  underlying value of a value object written by hand, refuses with an exception of its own, the message is
  `The value is not a valid Quantity.`, naming the value object alone, and the code the one that exception carries in
  its `Data`, or `value_object.not_parsable`. A value object's own `IValueObjectValidator<T>` message is yours to keep
  free of the value, as everywhere else.

An absent argument whose parameter has a default, `= null` or `[DefaultValue]`, and a `null` for a nullable value
object pass, as the binding fills them in. A collection, a dictionary or an object parameter is read through its
contract too, and refused under the parameter's name for a value object it holds; any other error, a string where its
`int` member goes, is left to the function, which throws it as it would without the wrapper. A value object an
application passes itself is left to the binding as it is. An argument a host hands over as text or as a number,
which the binding converts through a JSON round trip of its own rather than refuses, is read as the binding converts
it: text that may be JSON as JSON first, then the value written as JSON through its own type and read back. It is
refused only when neither reading takes it, which leaves the binding nothing but a value of the wrong type to call the
method with.

Validation fails fast, as everywhere in the library: a model that sends two refused arguments corrects the first,
then learns about the second. An argument that passes is validated again by the binding, which costs one more
`TryCreate` on a call that waits for a model.

## Structured output

`ValueObjectResponseFormat.ForJsonSchema<T>()` builds the response format `ChatResponseFormat.ForJsonSchema<T>()`
builds, its name and description defaulted the same way, with the rules of every value object the answer holds, a
list's items included. The answer is then read the way `GetResponseAsync<T>` reads it:

```csharp skip
ChatResponse response = await chatClient.GetResponseAsync(
    messages,
    new ChatOptions { ResponseFormat = ValueObjectResponseFormat.ForJsonSchema<Order>(AppJsonContext.Default.Options) });

try
{
    var order = new ChatResponse<Order>(response, AppJsonContext.Default.Options).Result;
}
catch (ValueObjectJsonException exception)
{
    // The model is constrained before it answers, and the same rules check the answer while it is read.
    ValueObjectErrors.TryGetCode(exception, out var code); // ask again, with the code
}
```

`ChatResponse<T>` and `GetResponseAsync<T>` live in Microsoft.Extensions.AI rather than in its abstractions, which the
package alone depends on: there is no one-call helper, and no retry loop, which the code is enough to write.

## Agent Framework

Agent Framework's `ChatClientAgent` calls its tools through `FunctionInvokingChatClient`, so a tool built with
`AIFunctionFactory` takes the same two calls: the model reads the rules in the tool's schema, and the rule a refused
argument broke, with its code.

```csharp skip
AIAgent agent = new ChatClientAgent(
    chatClient,
    instructions: "You place orders.",
    tools:
    [
        AIFunctionFactory.Create(
                tools.PlaceOrder,
                new AIFunctionFactoryOptions { JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions().WithValueObjects() })
            .WithValueObjectValidation(),
    ]);
```

Its `RunAsync<T>` replaces the response format, one its run options carry included, with
`ChatResponseFormat.ForJsonSchema<T>`, which no schema option reaches: the model would read every value object of the
answer as the schema that accepts anything. Hand the format to a plain `RunAsync` through its run options instead, and
read the answer with `AgentResponse<T>`, through the same serializer options:

```csharp skip
AgentResponse response = await agent.RunAsync(
    "Plan the order.",
    options: new AgentRunOptions { ResponseFormat = ValueObjectResponseFormat.ForJsonSchema<Order>(AppJsonContext.Default.Options) });

var order = new AgentResponse<Order>(response, AppJsonContext.Default.Options).Result; // throws ValueObjectJsonException
```

`RunAsync<T>` also wraps the schema of an answer that is no object, a list or a lone value object, in an object under
`data`, and unwraps the answer; this way does neither, so give such an answer a record of its own. This was run on
Agent Framework 1.24.0, with a scripted model in place of a provider.

## Native AOT and reflection-free serialization

The package is AOT-compatible. No generator is involved: `AIFunctionFactory` reads the method behind a tool at run
time, and the wrapper reads the same parameters once, when it wraps the function, then each argument through the
function's own contracts.

Where reflection-based serialization is disabled, as native AOT disables it, `AIFunctionFactory` over its default
options throws a `NotSupportedException` for any parameter type its own context does not know, `System.Guid` included,
package or not, and `ValueObjectResponseFormat.ForJsonSchema<T>()` throws one for an answer its options do not know.
Hand them options whose resolver is a source-generated context naming `ValueObjectJsonConverterFactory`, the types of
the parameters, value objects included, and the type of the result or of the answer; the value objects' underlying
types need not be listed, since the generated converter reads them itself. Both the schema and the validation then
work:

```csharp skip
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, Converters = [typeof(ValueObjectJsonConverterFactory)])]
[JsonSerializable(typeof(Iban))]
[JsonSerializable(typeof(Quantity))]
[JsonSerializable(typeof(CountryCode))]
[JsonSerializable(typeof(string))]
internal partial class ToolsJsonContext : JsonSerializerContext;

var options = new AIFunctionFactoryOptions
{
    SerializerOptions = ToolsJsonContext.Default.Options,
    JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions().WithValueObjects(),
};
```

A value object nothing registered, one written by hand or a construction of a generic one, has to be registered at
start-up there, as [serializing it](./json.md#systemtextjson-source-generated) asks already.

## Model Context Protocol servers

A server built with the C# Model Context Protocol SDK describes its tools from the same `JsonTypeInfo`, and has the
same two gaps. Registered with `WithTools<OrderTools>()`, each value-object parameter is `true` in the tool's
`inputSchema`, and, for a tool marked `UseStructuredContent`, each value object of its result is `true` in its
`outputSchema`, a `List<Iban>` `"items": {}`:

```json
{"type":"object","properties":{"iban":{"description":"The account to debit."},"quantity":true,"country":true,"delivery":{"default":null}},"required":["iban","quantity","country"]}
```

A refused argument fails the binding, and the SDK answers with a tool execution error that names the tool alone,
`An error occurred invoking 'place_order'.`: it shows the message of an `McpException` only. The SDK's `WithTools`
methods take no schema options, so none of this can be configured from outside.

```
dotnet add package AdCodicem.ValueObjects.ModelContextProtocol
```

```csharp skip
using AdCodicem.ValueObjects.ModelContextProtocol;

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithValueObjectTools<OrderTools>();
```

`WithValueObjectTools<T>()` mirrors the SDK's `WithTools<T>()`: it finds the same `[McpServerTool]` methods, static and
instance, public or not, and creates an instance of `T` for each call of an instance method, even when `T` is a service,
its constructor's parameters resolved from the services of the call, disposing it after.
`WithValueObjectTools(IEnumerable<Type>)`, which takes a static class too, and `WithValueObjectToolsFromAssembly()`
mirror the SDK's `WithTools(IEnumerable<Type>)` and `WithToolsFromAssembly()`, and are not trimming-safe, as the SDK's
are not. The SDK's `WithTools<T>(T target)`, which calls the methods of one instance, has no counterpart: create its
tools [by hand](#tools-created-by-hand), with that instance as their target. The package depends on
`AdCodicem.ValueObjects.AI` and ModelContextProtocol 2.

### What the server lists

Each value object a tool takes is described in its `inputSchema` [as above](#what-the-schema-says), under the
language-model profile, after the description a `[Description]` gives the parameter. With `UseStructuredContent`, each
value object of the result is described the same way in its `outputSchema`, the type an `OutputSchemaType` names
included.

The tools stay the SDK's own, which matters for the clients in use today: on a protocol version before `2026-07-28`,
the SDK rewrites the output schema of a tool that returns a value object alone into an object,
`{"type":"object","properties":{"result":…},"required":["result"]}`, the rules kept inside, and wraps the structured
content likewise, as those versions ask. It does so for its own tools only, which is why the package never wraps one.

### A refused argument, as a tool execution error

Before the tool binds its arguments, each value-object argument is read [as above](#a-refused-argument), through the
contract the tool's serializer options hold for its parameter, `McpJsonUtilities.DefaultOptions` by default, which
reads a number sent as text. The first refused, in the order of the parameters, is answered with a tool execution
error, the kind the protocol asks a server to use for an argument a model can correct, and the tool is not called, nor
an instance of its type created:

```json
{
  "isError": true,
  "content": [{"type": "text", "text": "Argument 'quantity' rejected (value_object.out_of_range): The value is not a valid Quantity: The value must be less than or equal to 100."}],
  "structuredContent": {"error": "invalid_argument", "argument": "quantity", "code": "value_object.out_of_range", "message": "The value is not a valid Quantity: The value must be less than or equal to 100."}
}
```

- The text always names the argument and carries the code and the message; the structured content is the object the
  Microsoft.Extensions.AI wrapper answers with, its `code` and `message` read the same way, never holding the value
  sent.
- A tool that declares an output schema, through `UseStructuredContent`, gets the text alone: the specification asks
  that structured content conform to the output schema, and a refusal does not.
- An absent argument the parameter has no default for, or a call that sends no arguments, is `value_object.required`,
  `A value is required.`; a JSON `null` for a value object that cannot be `null` is refused by its converter, as
  `value_object.required` too. A `null` for a nullable value object, and an absent argument with a default, pass.
- A list, a dictionary or an object is answered for a value object it holds, under the parameter's name; any other
  error is left to the SDK, which answers as it does without the package.
- The parameters the SDK binds itself, the `McpServer`, the `RequestContext<CallToolRequestParams>`, an
  `IProgress<ProgressNotificationValue>`, the `CancellationToken` and services, are never read.

The check is a call-tool filter, added once to the server's options however many registrations ask for it, and after
every filter the options are configured with: it runs inside the application's own filters and
ModelContextProtocol.AspNetCore's authorization, right before the tool binds its arguments, over stdio as over HTTP. A
filter added later, in `HttpServerTransportOptions.ConfigureSessionOptions`, runs inside it
([below](#what-it-does-not-cover)). A tool registered otherwise, the SDK's own or one without a value object, is passed
on as it is.

### Tools created by hand

`ValueObjectMcpServerTool.Create(method, target, options)` creates a tool as `McpServerTool.Create` does, with the rules
in its schemas: the options are copied, never changed, and a transform their `SchemaCreateOptions` carry runs first.
Register it with `WithValueObjectTools(tools)`. The SDK's `WithTools(tools)` alone publishes its schemas, but its
arguments are checked only on a server one of the `WithValueObjectTools` methods configured, or whose options were
given the check, as below.

```csharp skip
McpServerTool placeOrder = ValueObjectMcpServerTool.Create(
    typeof(OrderTools).GetMethod(nameof(OrderTools.PlaceOrder))!,
    orderTools,
    new McpServerToolCreateOptions { Name = "place_order", Destructive = false });

builder.Services.AddMcpServer().WithStdioServerTransport().WithValueObjectTools([placeOrder]);
```

For a delegate, pass its `Method` and its `Target`. A tool built some other way publishes the rules with
`SchemaCreateOptions = new AIJsonSchemaCreateOptions().WithValueObjects()` in its `McpServerToolCreateOptions`, and
nothing checks its arguments.

A server created without dependency injection, from options built by hand, gets the check from
`AddValueObjectValidation()`, which places it last among the call-tool filters of the options, once, before the server
is created from them:

```csharp skip
var options = new McpServerOptions { ToolCollection = [placeOrder] };
await using var server = McpServer.Create(new StdioServerTransport("orders"), options.AddValueObjectValidation());
```

### Without reflection

`WithValueObjectTools<T>()` carries the SDK's own trimming annotation, and the package is AOT-compatible. Where
reflection-based serialization is disabled, as native AOT disables it, the SDK's default options describe no value
object, and the server throws a `NotSupportedException` when it is first resolved. Hand the registration a copy of the
SDK's options whose resolver chain starts with a source-generated context listing the types of the parameters and
results, and whose converters hold the converter factory:

```csharp skip
var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
options.TypeInfoResolverChain.Insert(0, ToolsJsonContext.Default);
options.Converters.Add(new ValueObjectJsonConverterFactory());

builder.Services.AddMcpServer().WithStdioServerTransport().WithValueObjectTools<OrderTools>(options);
```

The factory goes into the converters of the options, not only into the attribute of the context: a context resolving
types for options other than its own applies the converters of those options, and the System.Text.Json generator does
not see the converter of a value object declared in the context's own project.

### What it does not cover

- The SDK refuses call-tool filters beside an explicit `CallToolWithAlternateHandler`, which is experimental
  (`MCPEXP002`), and throws when it creates the server: at its start over stdio; over HTTP, where the application
  starts as usual, for each session, and for each request when the server is stateless.
- A call-tool filter added in `HttpServerTransportOptions.ConfigureSessionOptions`, which runs for each session once
  the options are configured, comes after the check, and runs inside it: it never sees a refused call. Call
  `options.AddValueObjectValidation()` after adding it there, which moves the check after it.
- The SDK's `WithTools<T>(T target)` has no counterpart: create each tool with
  `ValueObjectMcpServerTool.Create(method, target)` and register them with `WithValueObjectTools(tools)`.
- A tool wrapped after it was created, in a `DelegatingMcpServerTool` for instance, is matched as the wrapper and not
  checked; the SDK no longer rewrites its output schema for older clients either.
- Prompts and resources are not covered: only tools.
- The HTTP transport is not run by the tests, which use streams in process; it was run by hand over
  ModelContextProtocol.AspNetCore 2.2, stateful and stateless, with and without its authorization filters.
- The `outputSchema` is written under the language-model profile, which describes what a model should send, and can
  be narrower than what the type holds. A closed set looked up ignoring case, or as a culture compares, lists its known
  values in `enum`, while the value object keeps the spelling it was given, `"Final"` for `final`, and a closed set of
  dates and times, whose equality ignores the offset or the kind, keeps the one it was given; `minLength` is the
  declared `MinLength`, which .NET counts in UTF-16 code units and JSON Schema in characters, an emoji counting two in
  one and one in the other. A client that validates the structured content against the schema refuses a result holding
  such a value: a normalizer that returns each known value in its declared spelling keeps a closed set within its
  `enum`.
- With `UseStructuredContent`, the SDK lists a nullable member of the result in `outputSchema.required`, and leaves it
  out of the structured content when it is `null`: a client that validates the result against the schema refuses it.
  That is the SDK's own behaviour, with or without the package.

## Limits

Of the Microsoft.Extensions.AI package; the Model Context Protocol package's are [above](#what-it-does-not-cover).


- Whether OpenAI accepts `examples` in strict mode has not been checked against the live API; Microsoft.Extensions.AI
  keeps it on the wire.
- The path of a value object refused inside a collection or an object is not reported: `argument` names the parameter,
  and the message the value object.
- A parameter that a `ConfigureParameterBinding` callback binds while the schema still lists it is checked as the
  serializer would read it.
- There is no middleware that wraps every tool of a `ChatOptions`, or a `FunctionInvokingChatClient`, at once: each
  function is wrapped where it is created.
