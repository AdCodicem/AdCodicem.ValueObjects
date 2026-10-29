---
title: Format a Value
sidebar_label: Formatting
slug: /how-to/formatting
description: Give a value object named formats, such as a printed or masked IBAN, with an allocation-free formatter hook.
---

# Format a value

By default a value object formats as its underlying value. An IBAN is stored in its electronic form, but people
read it in groups of four, and logs should only show its last digits. A formatter hook adds named formats.

```csharp
[ValueObject<string>(MinLength = 15, MaxLength = 34)]
public readonly partial struct Bban : IValueObjectFormatter<string>
{
    public static class Formats
    {
        public const string Electronic = "E";
        public const string Masked = "M";
    }

    public static bool TryFormatValue(
        in string value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        _ = provider;

        if (destination.Length < value.Length)
        {
            charsWritten = 0;
            return false;
        }

        value.CopyTo(destination);
        if (format is "M" or "m")
        {
            destination[2..(value.Length - 4)].Fill('*');
        }

        charsWritten = value.Length;
        return true;
    }
}
```

```csharp skip
var bban = Bban.Create("30006000011234567890189");

bban.ToString()                            // "30006000011234567890189", the hook's default format
bban.ToString(Bban.Formats.Masked, null)   // "30*****************0189"
$"{bban:M}"                                // the same, through ISpanFormattable, with no intermediate string
```

## The rules of the hook

- **It takes over formatting entirely**, including the empty and `null` format. Handle the default case —
  here, anything but `M` writes the value as it is. `ToString()`, `$"{bban}"` and `ToString(null, provider)` all
  write what the hook writes for it.
- **Return `false` when the destination is too small, and only then.** The generated `ToString(format, provider)`
  calls again with a pooled buffer twice as large; that is the framework contract, not an error. A hook still
  refusing a buffer of 1,048,576 characters is one that never succeeds, and `ToString` throws a `FormatException`
  rather than write some other text in its place. Throw `FormatException` yourself for a format you do not support.
- **Write a string value as it is, and nothing is allocated.** When the text the hook writes for a `string` value
  object is the value it holds, as `Bban`'s default format is, `ToString` returns that string rather than a copy.
- The `Formats` class is a convention, not a requirement: named constants spare callers a magic letter.

`IValueObjectFormatter<TValue>` writes into a span and allocates nothing. For a rule whose output is naturally a
`string`, `IValueObjectStringFormatter<TValue>` takes `FormatValue(in value, format, provider)` instead, and costs
that string: `TryFormat`, and so interpolation, copies it into the destination. When a type declares both, the
string formatter wins, in `ToString` and in `TryFormat` alike, and the span formatter is never called.

Formatting never affects the wire: JSON, a dictionary key included, the database and model binding always carry
the underlying value. Nor does it reach a log through a message template: Serilog applies no format to a string, so
`{Bban:M}` writes the whole value, with or without the Serilog package. Log `bban.ToString(Bban.Formats.Masked, null)`
where only the last digits may appear ([Logging](./logging.md#what-a-value-object-writes)).
