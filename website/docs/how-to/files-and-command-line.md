---
title: Read and Write Files and Command Lines
sidebar_label: Files and command line
slug: /how-to/files-and-command-line
description: Read and write value objects in CSV with Sep, in spreadsheets with ClosedXML, MiniExcel and EPPlus, and on the command line with Spectre.Console.Cli, reporting the rule a cell or an argument breaks.
---

# Read and write files and command lines

A value object implements `ISpanParsable<T>` and `ISpanFormattable`, and carries a `TypeConverter`, which is what a
library that reads text without knowing the type looks for. Where a library finds one of them, it works as is; where
it does not, the recipe is to write the underlying values and parse them back cell by cell with `TryParse`, which
says which rule a value broke.

## Sep

Sep 0.17.1 works as is, through `ISpanParsable<T>` and `ISpanFormattable`:

```csharp skip
var iban = row["Iban"].Parse<Iban>();   // throws ValueObjectException on a value the type rejects

if (!Quantity.TryParse(row["Quantity"].Span, CultureInfo.InvariantCulture, out var quantity, out var validation))
{
    // validation.ErrorCode names the rule
}
```

## Spectre.Console.Cli

Spectre.Console.Cli binds a value object through its `TypeConverter`, but a rejection prints only
``Error: Failed to convert '500' to Nullable`1.``, and the rule is lost. Unwrap it in an exception handler:

```csharp skip
app.Configure(config => config.SetExceptionHandler((exception, _) =>
{
    if (exception.InnerException is ValueObjectException rejected)
    {
        AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {exception.Message} {rejected.Message} ({rejected.ErrorCode})");
        return 2;
    }

    AnsiConsole.WriteException(exception);
    return -1;
}));
```

An option left out of the command line leaves a non-nullable value-object option default. Declare options `T?`, or
make the value a required `<arg>`.

## ClosedXML, MiniExcel and EPPlus

All three write a value-object property as a text cell, through `ToString()`, so a number or a date lands "stored as
text". Given a sequence of value objects directly, ClosedXML writes their public properties as columns, `Value`
among them, and EPPlus a `Value` column. The probe also found an `IsDefault` column in both, a public property then,
and no longer is (inferred, not re-run). Reading back fails: MiniExcel's `Query<Order>`, EPPlus's
`ToCollection<Order>()` and ClosedXML's `GetValue<Iban>()` all throw.

Export a projection of the underlying values:

```csharp skip
worksheet.Cell(1, 1).InsertTable(orders.Select(order => new
{
    Id = order.Id.Value,
    Iban = order.Iban.Value,
    Quantity = order.Quantity.Value,
    Due = order.Due.Value.ToDateTime(TimeOnly.MinValue),
}));
```

The cells then hold numbers and dates: the probe read them back as
`7 (Number) | 12,5 (Number) | 03/10/2026 00:00:00 (DateTime)`, printed in its own culture. Import cell by cell with
`TryParse`, and report the address of the cell with `validation.ErrorCode`, so the person who filled the sheet knows
which cell broke which rule.

EPPlus 5 and later is under the Polyform Noncommercial licence: commercial use needs a licence.
