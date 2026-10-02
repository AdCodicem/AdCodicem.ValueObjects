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
also works as a dictionary key. The key carries the underlying value too, in the form the value travels in, whatever a
formatting hook writes, and it is read back the same way and validated like a value: a key the value object rejects
throws a `JsonException` as well.

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

## Newtonsoft.Json

For code, SDKs and message contracts that have not moved to System.Text.Json:

```bash
dotnet add package AdCodicem.ValueObjects.NewtonsoftJson
```

```csharp skip
var settings = new JsonSerializerSettings
{
    DateParseHandling = DateParseHandling.None,   // hand the converter the text of every string
};
settings.Converters.Add(new ValueObjectConverter());
```

One converter covers every value object, with the same rules as System.Text.Json and the same values on the wire:
a number or a boolean as such, anything else as a string in the same form, `Int128` and `UInt128` included. The
text is the same too, but for a whole `decimal`, `double` or `float`: Newtonsoft.Json always writes a real with a
fraction, `1250.0` where System.Text.Json writes `1250`, and each serializer reads the other's text as the same
value. The converter reads only the kind of token it writes, so a number in a string, or a string where a number
belongs, is refused as JSON rather than converted. A rejected value throws a `JsonSerializationException` naming
the type and the rule.

Newtonsoft.Json reads each token before a converter sees it, under the serializer's settings, and its defaults
change what the converter is handed.

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
`DateParseHandling` to `None`, as above, and every string reaches the converter as text.

### Numbers

Under the default `FloatParseHandling`, Newtonsoft.Json reads a number with a fraction or an exponent as a
`double`, and a `decimal` value object keeps the fifteen to seventeen significant digits a `double` carries. When
`decimal` value objects carry more, set `FloatParseHandling.Decimal`, which keeps all of them, as System.Text.Json
does:

```csharp skip
settings.FloatParseHandling = FloatParseHandling.Decimal;   // for decimal value objects only
```

It applies to every number of the payload, and a `decimal` holds a narrower range than a `double`. Under it, a
`double` or `float` value object beyond about ±7.9 × 10²⁸ makes the reader throw a `JsonReaderException`, and one
smaller than a `decimal`'s 28 decimal places keeps only the digits that fit in them — none below about 10⁻²⁸, so it
reads as zero. Leave the default when a payload carries such values. `FloatParseHandling.Decimal` also gives an
`object` or `JToken` member a `decimal` rather than a `double` for such a number.
