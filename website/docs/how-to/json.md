---
title: Serialize to JSON
sidebar_label: JSON
slug: /how-to/json
description: Serialize value objects as their bare underlying value with System.Text.Json, including source-generated contexts, and with Newtonsoft.Json, and describe them in the JSON Schema System.Text.Json exports.
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
value, or a token of the wrong kind, throws a `ValueObjectJsonException`, a `JsonException` naming the type and the
reason, and carrying the code of the rule ([the code in an exception](../reference/errors.md#the-code-in-an-exception)).
A value object also works as a dictionary key. The key carries the underlying value too, in the form the value travels
in, whatever a formatting hook writes, and it is read back the same way and validated like a value: a key the value
object rejects throws a `ValueObjectJsonException` as well.

Writing refuses what reading would. An instance that never went through `Create` — a default array element, a member
of a message no one set — holds the default value, and when its type rejects it, the converter throws a
`ValueObjectJsonException`, "The value to write is not a valid Iban: …", from a value and from a key alike, rather
than put on the wire a value the service reading it would refuse. Over a value type, nothing tells the default from a
constructed zero, so a type that accepts its zero, an `Amount` with a minimum of 0 or an unconstrained `Guid`, writes
it.

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

The factory hands each value object the converter generated for it, which the generator registers with the value
object's descriptor. The package belongs in the assembly that declares the context: a domain project declaring the
value objects needs only `AdCodicem.ValueObjects`, the contracts and the generator, and its converters reach the
context all the same, with nothing built by reflection, which trimming and native AOT would not support. Only a value object written by hand has no generated
converter, and gets a general-purpose one from the factory instead.

A construction of a generic value object, `Reference<PurchaseOrder>`, is serialized as any other value object, by
either serializer. Its registration registers the generic definition alone, though, so the factory finds the generated
converter of the construction by reflection, the first time it meets it. Under native AOT, register the constructions
the context serializes, as [Where a value object can be declared](../authoring-guide.md#where-a-value-object-can-be-declared)
shows.

## Explicit options

The same package adds `AddValueObjects()` to `JsonSerializerOptions`, for a composition root that prefers to say
so, or for options that must cover value objects written by hand against the contracts:

```csharp skip
var options = new JsonSerializerOptions().AddValueObjects();
```

In ASP.NET Core MVC, `AddControllers().AddValueObjects()` already does this for you.

## A member missing from the payload

System.Text.Json leaves a member the payload does not carry as it is, so a value object member holds its default,
which no rule checks. Contracts evolve, and a producer that drops a property otherwise delivers a default instance to
a consumer that never validated it. Make the members of every message contract holding value objects required:

```csharp skip
var options = new JsonSerializerOptions { RespectRequiredConstructorParameters = true };

public sealed record OrderSubmitted(OrderId OrderId, CustomerCode Customer, Quantity Quantity);

// {"OrderId":"3f2504e0-…"} throws a JsonException: JSON deserialization for type 'OrderSubmitted' was missing
// required properties including: 'Customer', 'Quantity'.
```

`RespectRequiredConstructorParameters` covers the parameters of a constructor, a positional record's included, which
is the form that was run here. For a settable property, `[JsonRequired]` and the C# `required` modifier are
System.Text.Json's documented equivalents. The JSON payload is one source among others:
[Where a default instance can come from](../reference/default-instances.md) lists them.

## Large integers

`Int128` and `UInt128` value objects are written as JSON **strings**: a JSON number cannot carry them without
losing precision in most clients.

## Number handling

A value object over a number follows `JsonSerializerOptions.NumberHandling` as its underlying type does, since
System.Text.Json leaves the number handling of a custom converter to the converter:

- `AllowReadingFromString` reads `"1250.00"` as well as `1250.00`, and reads the text as System.Text.Json reads a
  number from text: whole, with no white space, no group separator and no culture, so `"1,000"` and `" 5"` are
  refused, as they are for the bare value;
- `WriteAsString` writes every number as a string, `"1250.00"`, and an indented writer lays it out as any other;
- over a `double` or a `float`, `AllowNamedFloatingPointLiterals` writes `NaN` and the infinities as `"NaN"`,
  `"Infinity"` and `"-Infinity"`, and either reading option reads them back. `WriteAsString` writes them as text
  too; with neither, writing one throws, as it does for the bare value.

The [OpenAPI document](./openapi.md) describes what these options put on the wire, as it describes the bare value.

What is read still goes through the value object's rules: a `Latitude` bounded to ±90 refuses `"NaN"` whatever the
options let the reader read.

## JSON Schema

System.Text.Json's `JsonSchemaExporter`, which the schemas of Microsoft.Extensions.AI tools and structured output, of
the Model Context Protocol SDK and of Semantic Kernel functions are built on, describes a type its converter serializes
as `true`, the schema that accepts anything. Every value object comes out that way, a `List<Iban>` as an array with no
`items`, and a `DeliveryDate?` loses the `null` it accepts. `ValueObjectJsonSchema`, in `AdCodicem.ValueObjects.Json`,
fills each of them in from the rules its type declares, the ones the OpenAPI document publishes:

```csharp skip
var schema = JsonSchemaExporter.GetJsonSchemaAsNode(
    ApiJsonContext.Default.Options,
    typeof(Order),
    new JsonSchemaExporterOptions { TransformSchemaNode = ValueObjectJsonSchema.TransformSchemaNode });
```

```json
"iban": {
  "description": "An International Bank Account Number.",
  "type": "string",
  "format": "iban",
  "pattern": "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
  "minLength": 15,
  "maxLength": 34,
  "examples": ["FR7630006000011234567890189"]
}
```

| Declared | Keyword |
| --- | --- |
| Underlying type | `type`: `boolean`, `integer`, `number`, or `string` for text, a character, a `Guid`, a date or a time, and `Int128` or `UInt128`, which travel as strings |
| A nullable value object | `"null"` added to `type`, and to `enum` |
| `MinLength`, `MaxLength` | `minLength`, `maxLength` |
| `IValueObjectPatternValidator` | `pattern`; a `TimeSpan` declaring none gets the pattern of the constant form it is written in |
| `IValueObjectMinimum<T>`, `IValueObjectMaximum<T>` | `minimum`, `maximum` on a number; a sentence of the `description` on anything written as a string, a date or a character, which those keywords cannot bound |
| A closed set of `[KnownValue]` | `enum`, each value written by the type's own converter |
| The summary, or `Description` | `description` |
| `Example` | `examples`, written by the converter |
| `SchemaFormat`, or the type's own | `format`, as the profile below says |

A collection or a dictionary of value objects gets its `items` or `additionalProperties`, which the exporter leaves
out, and a dictionary keyed by a value object written as a string gets the key's rules in `propertyNames`. A value
object is described in place, never as a `$ref`. The schema the exporter, or a host, hands over is completed rather
than replaced: a description already there, from a `[Description]` on a tool's parameter for instance, comes first,
the value object's after it, and a keyword the value object does not declare, such as a `default`, stays.

### Two profiles

The transform above uses `ValueObjectJsonSchemaProfile.OpenApi`, which describes what the serializer reads and writes
under the options, as the [OpenAPI document](./openapi.md) does: under `AllowReadingFromString`, the default of
ASP.NET Core and of `JsonSerializerDefaults.Web`, a number is `["string", "integer"]` with the pattern a number is
written in, and a format such as `int32`, `decimal` or `iban` stays in `format`.

`ValueObjectJsonSchemaProfile.LanguageModel` describes what a model should send. A number is a number alone, which the
serializer reads under any options, and its example and known values are numbers. A format JSON Schema defines,
`uuid`, `date`, `date-time`, `email` and the others, stays; any other moves into the description, where a model reads
it and a provider that refuses an unknown format does not see it. A closed set names each of its values there too, with
the description the value was declared with:

```csharp skip
var options = new JsonSchemaExporterOptions
{
    TransformSchemaNode = ValueObjectJsonSchema.CreateTransform(ValueObjectJsonSchemaProfile.LanguageModel),
};
```

```json
"vat": {
  "description": "A value-added tax rate, in percent.\n\nFormat: decimal.\n\n20.0: Standard\n5.5: Reduced — Food, books and medicine.",
  "type": "number",
  "enum": [20.0, 5.5]
}
```

### In a host

Every host built on the exporter hands a transform of its own the `JsonTypeInfo` of each node, whatever it calls its
context, and `ValueObjectJsonSchema.Apply` takes it. With Microsoft.Extensions.AI:

```csharp skip
var schemaOptions = new AIJsonSchemaCreateOptions
{
    TransformSchemaNode = (context, schema) => ValueObjectJsonSchema.Apply(
        context.TypeInfo, schema, ValueObjectJsonSchemaProfile.LanguageModel),
};

var pay = AIFunctionFactory.Create(PayAsync, new AIFunctionFactoryOptions { JsonSchemaCreateOptions = schemaOptions });
```

The Model Context Protocol SDK takes the same options for a tool's `inputSchema`, through its
`McpServerToolCreateOptions.SchemaCreateOptions`.

A value object is found in the registry its generated registration fills, without reflection. One nothing registered,
a value object written by hand or a construction of a generic one, is described by reflection where the runtime
supports dynamic code; under native AOT it is left as the exporter described it until it is registered, as
serializing it through the factory asks already.

## Newtonsoft.Json

For code, SDKs and message contracts that have not moved to System.Text.Json:

```bash
dotnet add package AdCodicem.ValueObjects.NewtonsoftJson
```

```csharp skip
var settings = new JsonSerializerSettings().AddValueObjects();
```

`AddValueObjects()` adds `ValueObjectConverter`, once, and sets the two settings Newtonsoft.Json reads value objects
best under: `DateParseHandling.None`, which hands the converter the text of every string, and
`FloatParseHandling.Decimal`, which keeps every digit of a `decimal` ([Numbers](#numbers) says when to leave it off).
A host that hands you its settings takes the same call:

```csharp skip
GlobalConfiguration.Configuration.UseRecommendedSerializerSettings(settings => settings.AddValueObjects());   // Hangfire
builder.Services.AddControllers().AddNewtonsoftJson(options => options.SerializerSettings.AddValueObjects()); // MVC
```

One converter covers every value object, with the same rules as System.Text.Json and the same values on the wire:
a number or a boolean as such, anything else as a string in the same form, `Int128` and `UInt128` included. The
text is the same too, but for a whole `decimal`, `double` or `float`: Newtonsoft.Json always writes a real with a
fraction, `1250.0` where System.Text.Json writes `1250`, and each serializer reads the other's text as the same
value. A number written as a string is read as well, `"7"` as `7`, as System.Text.Json reads one under
`AllowReadingFromString`: whole, with no white space, no group separator and no culture, so `"1,000"` and `" 5"` are
refused. `NaN` and the infinities are refused, in a string or not. Otherwise the converter reads only the kind of
token it writes, so a string where a boolean belongs, or a number where a string belongs, is refused as JSON rather
than converted. A rejected value throws a `JsonSerializationException` naming the type and the rule, and so does a
value the type rejects on its way out, as System.Text.Json refuses to write it; the code of the rule travels in its
`Data`, where `ValueObjectErrors.TryGetCode` reads it.

Newtonsoft.Json reads each token before a converter sees it, under the serializer's settings, and its defaults
change what the converter is handed.

### Without the converter

Newtonsoft.Json falls back to the type converter the generator writes on every value object. A host that serializes
through Newtonsoft.Json on settings of its own takes this path until its settings get the converter: the
[Azure Cosmos DB SDK v3](./azure.md#cosmos-db-sdk) and [Hangfire](./messaging.md#hangfire) both do by default.
Newtonsoft.Json then writes each value object as a string, a number included, `"Quantity":"7"`, and reads a string,
or a number of any numeric type, through the value object's rules. For a number, the two modes read each other's
data: a host without the converter reads the numbers System.Text.Json and the converter write, and the converter
reads the numbers a host stored without it, which can add it without draining what it stored first. A boolean is the
exception: the type converter writes `"True"`, a string where the converter reads a boolean.

The converter is still the one to use. System.Text.Json on its default options refuses a number written as a
string. A rejection loses its rule: Newtonsoft.Json reports `Could not cast or convert from System.String to
Quantity`, with no `ValueObjectException` in the chain. And a JSON integer beyond the range of a `long`, which
Newtonsoft.Json reads as a `BigInteger`, is converted by no type converter, so a `ulong` value object above
`long.MaxValue` written as a number is not read without the converter.

### Dates

Under the default `DateParseHandling`, Newtonsoft.Json turns a string that looks like a date into a `DateTime`, and
converts one written with an offset to local time. What a value object makes of that date:

- A `DateTime` value object reads it as System.Text.Json reads the text.
- A `DateTimeOffset` value object reads it as System.Text.Json reads the text when the text ends in `Z` or names
  no zone, and refuses any text with an explicit offset, which the date has lost. That includes its own output: a
  `DateTimeOffset` is always written with its offset, `+00:00` for UTC, so under the default settings it cannot be
  read back.
- A string value object has lost its text, and refuses it.

A date that no longer says what the text said is refused rather than read as another value. Set
`DateParseHandling` to `None`, as `AddValueObjects()` does, and every string reaches the converter as text.

### Numbers

Under the default `FloatParseHandling`, Newtonsoft.Json reads a number with a fraction or an exponent as a
`double`, and a `decimal` value object keeps the fifteen to seventeen significant digits a `double` carries, and loses
its scale: a stored `12.50` reads back as 12.5, and `1234567890123456789.12` as 1234567890123456800. Any payload
carrying an amount of money therefore needs `FloatParseHandling.Decimal`, whatever its number of digits, and a host
that sets its own deserializer settings may leave it at `Double`. `AddValueObjects()` sets it, which keeps every digit
and the scale, as System.Text.Json does.

It applies to every number of the payload, and a `decimal` holds a narrower range than a `double`. Under it, a
`double` or `float` value object beyond about ±7.9 × 10²⁸ makes the reader throw a `JsonReaderException`, and one
smaller than a `decimal`'s 28 decimal places keeps only the digits that fit in them — none below about 10⁻²⁸, so it
reads as zero. When a payload carries such values, leave the float handling as the settings have it:

```csharp skip
var settings = new JsonSerializerSettings().AddValueObjects(decimalReals: false);
```

`FloatParseHandling.Decimal` also gives an `object` or `JToken` member a `decimal` rather than a `double` for such a
number.
