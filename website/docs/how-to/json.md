---
title: Serialize to JSON
sidebar_label: JSON
slug: /how-to/json
description: Serialize value objects as their bare underlying value with System.Text.Json, including source-generated contexts, and with Newtonsoft.Json.
---

# Serialize to JSON

A value object travels as its underlying value: an `Iban` is a JSON string, a `Quantity` a JSON number. It is
never wrapped in an object, so a client sees exactly what it would see if the property were a primitive.

## System.Text.Json, reflection-based

Nothing to do. Every generated value object carries its own `[JsonConverter]`, so `JsonSerializer`, ASP.NET
Core's default options and `HttpClient`'s JSON extensions all serialize it as the bare value:

```csharp skip
JsonSerializer.Serialize(new { iban = Iban.Create("FR7630006000011234567890189") });
// {"iban":"FR7630006000011234567890189"}
```

Reading goes through `TryCreate`, so an incoming value is normalized and validated like any other. A rejected
value, or a token of the wrong kind, throws a `JsonException` naming the type and the reason. A value object
also works as a dictionary key.

## System.Text.Json, source-generated

A source-generated `JsonSerializerContext` needs one line more. One source generator never sees another's
output, so the System.Text.Json generator cannot see the `[JsonConverter]` this library emits. Name the
hand-written factory from `AdCodicem.ValueObjects.Json`, which it *can* see:

```bash
dotnet add package AdCodicem.ValueObjects.Json
```

```csharp skip
[JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
[JsonSerializable(typeof(AccountResponse))]
public partial class ApiJsonContext : JsonSerializerContext;
```

It is declared at compile time, on the context, so there is nothing to remember when the options are built.

## Explicit options

The same package adds `AddValueObjects()` to `JsonSerializerOptions`, for a composition root that prefers to say
so, or for options that must cover value objects written by hand against the contracts:

```csharp skip
var options = new JsonSerializerOptions().AddValueObjects();
```

In ASP.NET Core MVC, `AddControllers().AddValueObjects()` already does this for you.

## Large integers

`Int128` and `UInt128` value objects are written as JSON **strings**: a JSON number cannot carry them without
losing precision in most clients.

## Newtonsoft.Json

For code, SDKs and message contracts that have not moved to System.Text.Json:

```bash
dotnet add package AdCodicem.ValueObjects.NewtonsoftJson
```

```csharp skip
var settings = new JsonSerializerSettings();
settings.Converters.Add(new ValueObjectConverter());
```

One converter covers every value object, reading and writing the bare underlying value with the same rules.
