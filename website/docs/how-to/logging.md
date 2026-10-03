---
title: Log Value Objects
sidebar_label: Logging
slug: /how-to/logging
description: What Microsoft.Extensions.Logging, its source generators, OpenTelemetry, NLog and Application Insights record for a value object, and how to keep a number a number.
---

# Log value objects

A logger records a value object as a scalar, never as a wrapper: it formats it, through `ToString()`, as it formats an
`int` or a `Guid`. What is lost on the way is the JSON type of a number, which reaches the sink as a string. Each
section says how to keep it. This library has no logging integration.

## Microsoft.Extensions.Logging

`AddJsonConsole` writes a value object as a string, `"Qty":"42"`, beside `"RawInt":42` for the `int` it wraps. Pass
`.Value` where the type of the JSON value matters:

```csharp skip
logger.LogInformation("Reserved {Qty} of {Sku}", quantity.Value, sku);
```

Never put `[LogProperties]` on a value object parameter. Across assemblies, the logging generator logs the public
properties of the type, so the value comes out as a `qty.Value` tag rather than `qty` (the probe also logged
`qty.IsDefault`, a public property then, and no longer is: inferred, not re-run); in the value object's own project,
it cannot see the generated members, reports `LOGGEN020` and falls back to `ToString()`. A `[TagProvider]` of
your own for the type works. The metrics generator refuses a value object as a tag, `METGEN012`: pass `.Value`.

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
Seq receives what Serilog writes, and this library has no Serilog integration.
