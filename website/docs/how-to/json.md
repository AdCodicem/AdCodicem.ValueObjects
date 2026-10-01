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

The factory hands each value object the converter generated for it, and the generator registers that converter
only in an assembly that references `AdCodicem.ValueObjects.Json`. Add the package to the assembly that
**declares** the value objects — a domain project, say — and not only to the one that declares the context. A value
object declared without it registers no converter: the factory then builds a general-purpose one by reflection at
run time, which trimming and native AOT do not support, and which writes `Int128` and `UInt128` as JSON numbers
rather than strings.

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
var settings = new JsonSerializerSettings
{
    DateParseHandling = DateParseHandling.None,       // hand the converter the text of every string
    FloatParseHandling = FloatParseHandling.Decimal,  // and every digit of every number
};
settings.Converters.Add(new ValueObjectConverter());
```

One converter covers every value object, with the same rules and the same JSON as System.Text.Json: a number or a
boolean as such, anything else as a string in the same form, `Int128` and `UInt128` included. It reads only the
kind of token it writes, so a number in a string, or a string where a number belongs, is refused as JSON rather
than converted. A rejected value throws a `JsonSerializationException` naming the type and the rule.

Newtonsoft.Json reads each token before a converter sees it, under the serializer's settings, which is what the two
above are for. Left to its defaults, it turns a string that looks like a date into a `DateTime`, and a number with
a fraction into a `double`:

- A `DateTime` value object reads such a date as System.Text.Json reads the text, and so does a `DateTimeOffset`
  value object when the text is in UTC or names no zone. A text with another offset was converted to local time,
  and its offset is lost; a string value object has lost its text. Both are refused rather than read as another
  value, until `DateParseHandling` is `None`.
- A `decimal` value object keeps the fifteen to seventeen digits a `double` carries. `FloatParseHandling.Decimal`
  keeps all of them, as System.Text.Json does; it also gives an `object` or `JToken` member a `decimal` rather than
  a `double` for such a number.
