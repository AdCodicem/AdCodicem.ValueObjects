---
title: Log Value Objects
sidebar_label: Logging
slug: /how-to/logging
description: What Serilog, with and without AdCodicem.ValueObjects.Serilog, Microsoft.Extensions.Logging, its source generators, OpenTelemetry, NLog and Application Insights record for a value object, how to keep a number a number, and what Microsoft.Extensions.Telemetry redacts of one classified as personal data.
---

# Log value objects

Most loggers record a value object as a scalar: they format it, through `ToString()`, as they format an `int` or a
`Guid`. What is lost on the way is the JSON type of a number, which reaches the sink as a string. Serilog, asked to
destructure one with `@`, records a structure of its public properties instead. `AdCodicem.ValueObjects.Serilog` logs
a value object through Serilog as the value it carries; for the other loggers, each section says how to keep a number
a number.

## Serilog

```
dotnet add package AdCodicem.ValueObjects.Serilog
```

Without the package, Serilog logs a value object in two wrong shapes. With `@`, `{@Qty}`, and wherever it meets one
inside an object, a collection or a dictionary it destructures, it reflects it into a structure of its public
properties, `{"Value":42,"$type":"Quantity"}`. Without `@`, `{Qty}`, it captures the text `ToString()` writes, so a
`Quantity` of 42 is the JSON string `"42"`, and a Serilog.Expressions filter `Qty > 10` drops the event where it keeps
one carrying a raw `int`. `Destructure.AsScalar<Quantity>()` alone makes both `"42"`.

One call on the destructuring configuration fixes both:

```csharp skip
using AdCodicem.ValueObjects.Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Destructure.ValueObjects(o =>
    {
        o.CaptureAsUnderlyingValue = true;          // {Qty} too, not only {@Qty}
        o.Assemblies.Add(typeof(Iban).Assembly);    // the assemblies declaring value objects
    })
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();
```

```
{"@mt":"Reserved {Qty} of {Sku} for {@Order}","Qty":42,"Sku":"SKU-1","Order":{"Id":"0b6e1c2a-5f0e-4b8e-9a51-6f2b1d3c4e5f","Qty":42,"$type":"Order"}}
```

### Logged with `@`

`Destructure.ValueObjects()` alone adds a destructuring policy: a value object logged with `@`, or met while Serilog
destructures what is logged with `@`, is captured as its underlying value, `"Qty":42`, and Serilog writes it as it
writes that type, a number as a number, a `Guid` or a date as a string. It reads the value through
`IValueObject.GetBoxedValue()`, so every type implementing the marker is covered, a value object written by hand and a
construction of a generic one included, with nothing registered.

Serilog consults no destructuring policy for a value logged without `@`: under the policy alone, `{Qty}` stays the text
`"42"`.

### Logged without `@`

`CaptureAsUnderlyingValue` makes `{Qty}` the underlying value too. It costs a pass over the properties of every event,
which is why it is off by default, and then a numeric value object reaches a filter, a query and a format of the message
template as the number it holds: `Qty > 10` keeps the event, and `{Qty:000}` renders `042`. With Serilog as the
provider behind Microsoft.Extensions.Logging, `LogInformation`, a `[LoggerMessage]` method and a scope go through the
same capture, so their properties are the underlying value too.

The option does two things, which need each other. It makes every value object the registry holds a scalar, which
Serilog captures as it is rather than through `ToString()`, and it adds an enricher that replaces each one an event
holds, in its properties and in the structures, sequences and dictionaries they hold, with its underlying value.

- **The registry is read when the method runs.** Under the JIT, the registration the generator emits for an assembly
  runs once its code is first used, which a logger configured at the top of `Program.cs` comes before: name each
  assembly declaring value objects in `Assemblies`. A native binary has registered all of them at start-up.
- **A construction of a generic value object** is in the registry once it is registered by hand, as native AOT asks,
  or resolved by another integration before the method runs, and the option then makes it a scalar as any other. One
  that is not, and a value object written by hand that nothing registered, are captured as their text until the
  application makes them scalars: `.Destructure.AsScalar<Reference<PurchaseOrder>>()`, which the enricher then
  unwraps, and which changes nothing for a construction the registry already holds. Without the option, never make a
  value object a scalar: Serilog asks its scalar types before any destructuring policy, so `{@Qty}` becomes `"42"`.
- **The enricher sees what the enrichers before it added.** Call `Enrich.FromLogContext()`, and any enricher that adds
  value objects, before `Destructure.ValueObjects`: a property pushed on the log context by an enricher added after it
  is the value object's text, even pushed with `destructureObjects: true`. A property given through `ForContext`, or a
  Microsoft.Extensions.Logging scope, is added by a child logger, whose enrichers run first, and is the underlying value
  either way.

### What a value object writes

What the primitive it replaces would write: the package hands Serilog the underlying value, and Serilog formats it as
it formats that type.

- **A formatting hook does not apply.** A `Celsius` whose `ToString()` writes `21 °C` is logged as `21`, as on every
  other boundary. Stringification, `{$Temp}`, writes the hook's text, `"21 °C"`; so does logging `temp.ToString()`.
- **A named format of a value object over text is ignored**, a mask included: `{Iban:M}` writes the whole IBAN,
  package or not, since Serilog applies no format to a string. Log `iban.ToString("M", null)` where only the last
  digits may appear.
- **A value object over `Int128` or `UInt128`** is its digits in a JSON string, with or without `@`, as Serilog writes
  the bare number logged without `@`; Serilog destructures the bare number into an empty structure,
  `{"$type":"Int128"}`. A message renders it unquoted, and `{Message:lj}` quoted.
- **`ToMaximumStringLength` does not shorten it:** the policy hands Serilog the value itself, which it writes whole.
- **An application's own destructuring of a value object** runs only when declared first. Serilog runs destructuring
  policies in the order they were declared, so a `.Destructure.ByTransforming<Iban>(…)`, or a policy of the
  application's, a mask included, goes before `Destructure.ValueObjects()`, or the package's policy captures the value
  first. Under `CaptureAsUnderlyingValue`, none of the application's runs for a value object the registry holds,
  whatever the order and wherever it is met, since Serilog asks its scalar types before any policy: log the masked
  text, `iban.ToString("M", null)`, instead.
- **A dictionary keyed by a value object** is a list of key and value pairs, as Serilog captures any dictionary whose
  key is not a type it knows as a scalar, unless the application declares it a dictionary,
  `.Destructure.AsDictionary<Dictionary<Quantity, int>>()`, which the package then makes work with `@` too. Under the
  option, a key stays the value object, which a sink writes as its text, where another key of the same dictionary
  already is its underlying value, since a dictionary holds no two equal keys.
- **A default instance** is logged as the default of its underlying type, `0`, `""` or `false`: nothing reads a log
  back into a value object.
- **`AnyEntityId`** is no value object, and Serilog destructures it with `@` as a struct of its public properties.
  Log it without `@`.
- **Personal data is logged in clear** through Serilog's own API: a value object classified with a
  `DataClassificationAttribute` is logged as any value until a later version reads the classification to redact it.
  Behind Microsoft.Extensions.Logging with `EnableRedaction()`, Serilog receives what Microsoft.Extensions.Telemetry's
  logging generator redacted ([Data classification](#data-classification)). In the study behind this package,
  Destructurama.Attributed's `[LogMasked]` on a property holding a value object masked it (not run with this package).
- **Seq** receives what Serilog writes, so a numeric value object reaches it as a number, which a numeric query compares
  (inferred: Seq was not run).

### Native AOT

The package is AOT-compatible: the policy and the enricher read a value Serilog has already boxed through an interface
check, and close nothing. Serilog's build targets turn object destructuring off under `PublishTrimmed`, which
`PublishAot` implies, so a native binary logs an object with `@` as its `ToString()`, value objects inside included. A
value object alone, or in a collection, is logged as under the JIT.

## Microsoft.Extensions.Logging

`AddJsonConsole` writes a value object as a string, `"Qty":"42"`, beside `"RawInt":42` for the `int` it wraps. With
Serilog as the provider, see [Serilog](#serilog). Otherwise pass `.Value` where the type of the JSON value matters:

```csharp skip
logger.LogInformation("Reserved {Qty} of {Sku}", quantity.Value, sku);
```

Never put `[LogProperties]` on a value object parameter. Across assemblies, the logging generator logs the public
properties of the type, so the value comes out as a `qty.Value` tag rather than `qty`, alone; in the value object's own
project, it cannot see the generated members, reports `LOGGEN020` and falls back to `ToString()`. On a value object
classified as personal data, of another assembly, the `Value` tag is redacted, which
[Data classification](#data-classification) explains. A `[TagProvider]` of your own for the type works. The metrics
generator refuses a value object as a tag, `METGEN012`: pass `.Value`.

### Data classification

Microsoft.Extensions.Telemetry.Abstractions brings Microsoft.Gen.Logging, a `[LoggerMessage]` generator that replaces
Microsoft.Extensions.Logging's own in every project the package reaches, Microsoft.Extensions.Telemetry alone included.
With `EnableRedaction()` on the logging builder and a redactor registered through `AddRedaction`, it hands a parameter,
or a property it expands, carrying an attribute derived from `DataClassificationAttribute` to the redactor of that
classification. It never reads one on the type of a value it logs: a value object
[classified as personal data](../reference/errors.md#personal-data-in-an-exception) on its type, which empties the
`AttemptedValue` of a rejection, is written to the log in clear, whatever redactor the application configures.

So classify the type, for `AttemptedValue`, and each logging site, for the logging generator. `VO0045` warns where a
`[LoggerMessage]` method Microsoft.Gen.Logging writes logs a classified value object without a classification of its
own, and its code fix copies the type's:

```csharp
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Logging;

public static class Taxonomy
{
    public static DataClassification Personal => new("Shop", nameof(Personal));
}

public sealed class PersonalDataAttribute : DataClassificationAttribute
{
    public PersonalDataAttribute() : base(Taxonomy.Personal) { }
}

[PersonalData]
[ValueObject<string>(MaxLength = 254)]
public readonly partial struct EmailAddress;

internal static partial class Log
{
    // EmailAddress email alone is VO0045: the address would be logged in clear.
    [LoggerMessage(Level = LogLevel.Information, Message = "Signed up {Email}")]
    public static partial void SignedUp(ILogger logger, [PersonalData] EmailAddress email);
}
```

- **`[NoDataClassification]`** on the parameter logs the value object in clear on purpose, and silences `VO0045`, as a
  `#pragma` does. Any other classification of the parameter's own wins over the type's, and is left alone:
  `[FinancialData]` on a parameter of a `[PersonalData]` type is masked by the redactor of `FinancialData`. Never write
  the type's attribute beside it: the parameter is then classified by the set of both, which no redactor registered for
  one of them matches, and the redactor provider falls back to its erasing redactor, even where `PersonalData` is hashed.
- **What is reported:** a parameter of the value object's type or its nullable type; a collection of them, a nullable
  one included, which a classified parameter erases whole; a property `[LogProperties]` expands, or a collection of them
  a property holds, which the logging generator writes element by element, reported on the logging parameter with its
  path, `signup.Email`; and a field or a property of a record logged whole. The logging generator reads a classification
  on a record's constructor parameter as the classification of the property of its name, and one on the type it expands,
  or a base type, as the classification of each of its properties: neither is reported.
- **What the code fix writes:** every attribute that classifies the type, in its declared order, on the parameter, on
  the property, or on a record's positional parameter, which the logging generator reads as it reads the property; the
  fix-all of a document classifies each declaration once. It offers nothing for an attribute written with arguments:
  the logging generator builds a classification as `new PersonalDataAttribute().Classification`, whatever the attribute
  carries, so a copy of `[Classified("Health")]` fails to compile in the generated file (`CS7036`). Nor for an attribute
  whose `[AttributeUsage]` keeps it off a parameter or a property, nor for a member declared in another assembly, which
  is classified where it is declared. Declare classification attributes parameterless, with the usage
  `DataClassificationAttribute` declares, as above.
- **A record logged whole** is written through its `ToString()`, which prints its public fields and properties in
  clear, whatever their logging attributes, and classifying one makes the logging generator refuse the method
  (`LOGGEN035`, then `CS8795`): log the record with `[LogProperties]` and classify the property, as the warning says. A
  record that writes its own `ToString()` or `PrintMembers` prints what it likes, and is not read.
- **`[LogProperties]` on a classified value object of another assembly** expands it into its `Value`, which the logging
  generator classifies as the type: the one place it reads a type's classification.
- **`[LogProperties(Transitive = true)]`**, which Microsoft.Extensions.Telemetry 10.11 marks experimental
  (`EXTEXP0003`), expands a value object of another assembly, or one written by hand, into its `Value`, all the public
  properties of one written by hand, and drops the classification written on the property holding it. `VO0046` warns of
  it, and of `[LogProperties]` written on such a property itself, which does the same. It has no code fix, the value
  object being usually another project's: classify its type, which the expansion then applies to `Value`, or drop
  `Transitive` (for `[LogProperties]` on the property, drop that attribute from the property). The expansion goes into
  every other type too, a collection class or struct into its own properties, `Count` among them, and drops the
  classification of a property of any type it expands: `VO0046` reports a value object only. A collection interface,
  `IEnumerable<T>` or `IReadOnlyList<T>`, an array, and a collection with no property of its own are written element by
  element instead. The logging generator reads `Transitive` on the parameter alone: written on a property,
  `[LogProperties(Transitive = true)]` expands it one level, as `[LogProperties]` does.
- **`[TagProvider]` on a property** is never called by the logging generator, which logs the property whole, in clear;
  under `Transitive`, **`[LogPropertyIgnore]`** stops the expansion of a property, not its logging
  (Microsoft.Gen.Logging 10.11.0). Both are reported as any other property: classify the property, or leave it out of
  the type you log.

```csharp skip
public sealed record Transfer([property: PersonalData] Iban From, Iban To);

[LoggerMessage(Level = LogLevel.Information, Message = "Transferred")]
public static partial void Transferred(ILogger logger, [LogProperties] Transfer transfer);
// transfer.From is redacted. With [LogProperties(Transitive = true)], transfer.From.Value is in clear: VO0046.
```

- **What a redactor reads** of a value object is its text: the HMAC redactor hashes it as it hashes its underlying value
  logged under the same name, so a hash still correlates across the logs; a value object with a formatting hook is
  hashed as the hook's text, `21 °C`.
- **Serilog behind Microsoft.Extensions.Logging**, `AddSerilog()` on the logging builder, receives what the redaction
  wrote, `"Email":""` under the erasing redactor. `UseSerilog()` and Serilog.Extensions.Hosting's `AddSerilog()`, which
  replace the logger factory, were not checked. Serilog's own API logs a classified value object in clear.
- **Not reported:** `LogInformation(...)` and the other extension methods, which never redact; `LoggerMessage.Define`
  and scopes, which were not checked; a dictionary of value objects, which the logging generator writes in clear, and a
  classification of the parameter or the property covers; a record held by another record logged whole, by a collection
  logged whole, or by a property `[LogProperties]` expands without `Transitive`, which the logging generator writes
  through that record's `ToString()` inside the text of what holds it: the analysis reads the members of the record a
  parameter logs whole, not of a record nested in it, and a classification of the parameter covers each of them; and
  `[LogProperties]` on a value object of the same project or on a collection, which the logging generator reports itself
  (`LOGGEN020`, `LOGGEN018`), and logs whole.
- **Without Microsoft.Extensions.Telemetry.Abstractions**, Microsoft.Extensions.Logging's generator writes the methods
  and redacts nothing, a classified parameter included, and the analyzer is silent. It is silent on any method another
  generator writes.

## OpenTelemetry

The OTLP exporter writes a numeric value object as `string_value "42"`. Two processors unwrap it, and were verified to
give `int_value 42` on logs and on spans. Metric tags have no such hook: pass `.Value`.

```csharp skip
sealed class UnwrapLogs : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord record)
    {
        if (record.Attributes is { } attributes && attributes.Any(a => a.Value is IValueObject))
        {
            record.Attributes = attributes
                .Select(a => a.Value is IValueObject vo ? new KeyValuePair<string, object?>(a.Key, vo.GetBoxedValue()) : a)
                .ToList();
        }
    }
}

sealed class UnwrapSpans : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        foreach (var tag in activity.TagObjects.ToList())
        {
            if (tag.Value is IValueObject vo)
            {
                activity.SetTag(tag.Key, vo.GetBoxedValue());
            }
        }
    }
}

logging.AddOpenTelemetry(o => o.AddProcessor(new UnwrapLogs()).AddOtlpExporter());
tracing.AddProcessor(new UnwrapSpans()).AddOtlpExporter();
```

## NLog

NLog 6 writes a value object as a string everywhere, under `{X}`, `{@X}` and nested in an object, never as a wrapper.
`RegisterObjectTransformation` has no effect on it, because NLog formats an `IFormattable` before it looks for a
transformation. Pass `.Value` for a number.

## Application Insights

`customDimensions` is a map of strings to strings, so a value object lands there as its text, like any other value.
