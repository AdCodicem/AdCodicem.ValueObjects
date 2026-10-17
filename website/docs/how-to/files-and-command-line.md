---
title: Read and Write Files and Command Lines
sidebar_label: Files and command line
slug: /how-to/files-and-command-line
description: Read and write value objects in CSV with Sep, in spreadsheets with ClosedXML, MiniExcel and EPPlus, and on the command line with System.CommandLine, through AdCodicem.ValueObjects.CommandLine, and Spectre.Console.Cli, reporting the rule a cell or an argument breaks.
---

# Read and write files and command lines

A value object implements `ISpanParsable<T>` and `ISpanFormattable`, and carries a `TypeConverter`, which is what a
library that reads text without knowing the type looks for. Where a library finds one of them, it works as is; where
it does not, the recipe is to write the underlying values and parse them back cell by cell with `TryParse`, which
says which rule a value broke. System.CommandLine reads none of them: `AdCodicem.ValueObjects.CommandLine` reads its
options and arguments through the rules.

## Sep

Sep 0.17.1 works as is, through `ISpanParsable<T>` and `ISpanFormattable`:

```csharp skip
var iban = row["Iban"].Parse<Iban>();   // throws ValueObjectException on a value the type rejects

if (!Quantity.TryParse(row["Quantity"].Span, CultureInfo.InvariantCulture, out var quantity, out var validation))
{
    // validation.ErrorCode names the rule
}
```

## System.CommandLine

System.CommandLine 2.0 converts text through a closed table of its own: `bool`, the numbers, the dates and times,
`Guid`, the file system types, enums, and arrays and lists of them. It reads neither `TypeDescriptor` nor
`IParsable<T>`, so, without the package:

- a valid value parses with no error, then `GetValue` throws `InvalidOperationException`, "Cannot parse argument '3' for
  option '--quantity' as expected type 'Domain.Quantity'.": inside an action, an unhandled exception and exit code 1;
- an option of a value object over `bool` is no flag: `--verbose` alone is "Required argument missing for option:
  '--verbose'.";
- an option left out reads as the default instance, which its rules may refuse;
- `AcceptOnlyFromAmong` compares the text before the normalizer runs, and refuses `de` for a country code the type
  upper-cases, with no code.

`AdCodicem.ValueObjects.CommandLine` reads every option and argument of a value object through its rules:

```bash
dotnet add package AdCodicem.ValueObjects.CommandLine
```

### Every option and argument of a command

```csharp skip
using AdCodicem.ValueObjects.CommandLine;

var iban = new Option<Iban>("--iban") { Required = true };
var quantity = new Option<Quantity?>("--quantity");
var countries = new Option<CountryCode[]>("--country") { AllowMultipleArgumentsPerToken = true };
var pay = new Command("pay") { new Argument<Iban>("account") };
var root = new RootCommand("Places an order.") { iban, quantity, countries, pay };

root.AcceptValueObjects();   // every option and argument of a value object, subcommands included
root.SetAction(result => Place(result.GetValue(iban), result.GetValue(quantity), result.GetValue(countries)));
return root.Parse(args).Invoke();
```

`AcceptValueObjects()` reaches the symbols the tree holds when it runs: call it once every option, argument and
subcommand is added. It configures the options and arguments of the command and of its subcommands, each once, so a
parent's `Recursive` option is read through its rules wherever it is used. It leaves alone a symbol of any other type,
and one whose `CustomParser` the application set once the registry describes its value object
([under native AOT](#generic-and-hand-written-value-objects-and-native-aot), register it even then). A symbol holds
the value object, its nullable type, or an array, a `List<T>`, an `IEnumerable<T>` or an `IReadOnlyList<T>` of it. A
value object in another shape, `Iban?[]`, is refused with `InvalidOperationException` when the command is configured,
rather than left to throw at `GetValue`; another collection, `HashSet<Iban>`, is left to System.CommandLine.

One symbol takes `AcceptValueObject()`, its type inferred, so the call names no type argument:
`new Option<Quantity>("--quantity").AcceptValueObject()`. It throws `InvalidOperationException` for a symbol whose type
holds no value object, and for one whose `CustomParser` is set, rather than replace it.

The value is the normalized instance: `fr76 3000 6000 0112 3456 7890 189` reads as `FR7630006000011234567890189`, and
` po-1042 ` as `PO-1042`. One value reads the last token given, as System.CommandLine's own converters do; a collection
reads each token. An arity of the application's own that gives one value several tokens has each of them read through
the rules, the last being the value.

System.CommandLine reads the options and arguments of a parent command only when the action asks for them, unless an
option is `Recursive` or has a default value: on its own, it parses `app --quantity 500 pay FR76…` above with no error,
and `GetValue(quantity)` in an action of `pay` throws. `AcceptValueObjects()` gives each command it reaches a validator
that reads, when that command is invoked, the value-object options and arguments of the commands above it, so that a
refusal there is a parse error like any other. System.CommandLine then leaves the subcommand's own options and arguments
unvalidated: their errors show once the refused value is corrected. A symbol configured by `AcceptValueObject()` is read
there too once `AcceptValueObjects()` reached the subcommand, and otherwise throws at `GetValue`, with no code.

### What a refusal looks like

A refused value is a parse error. An invocation writes it to standard error, shows the help, and returns 1 without
running the action:

```text
Option '--quantity': The value must be less than or equal to 100.
Argument 'account': The value must be at least 15 characters long.
Option '--country', the 2nd value: The value is not one of the accepted values.
```

The error names an option by the alias typed, `-q` for `-q 500`, and an argument by its name, then gives the rule's
message; it never quotes the text. A collection gives one error per refused element, under its position counted from
one, as a person counts, when it received more than one token: `--country XX` alone is `Option '--country': …`. A rule
with an empty message gives `Option '--x' is invalid.`, and a parser written by hand that refuses without a reason
`value_object.not_parsable`, "The text could not be parsed as X.".

A `ParseError` holds a message and nothing else, so the package keeps the rule's code and the value object type beside
each error it reports, for an application that maps a rule to an exit code, or reports it, without reading English:

```csharp skip
foreach (var error in root.Parse(args).Errors)
{
    if (error.TryGetValueObjectErrorCode(out var code))   // value_object.out_of_range
    {
        error.TryGetValueObjectType(out var type);        // typeof(Quantity)
    }
}
```

An error System.CommandLine reports itself carries no code: an option `Required` and left out, a token it does not
recognise. `GetValue` on a refused symbol throws System.CommandLine's `InvalidOperationException`, built from the
message alone, with no code; an invocation never gets there, since it runs no action for a parse with errors, a parent
command's symbol included once `AcceptValueObjects()` reached the subcommand invoked.

### Empty text and an option left out

Empty text, `--iban ""`, is a token like any other: the type's own rule refuses it, `value_object.required` for text and
`value_object.not_parsable` for a number or a `Guid`, and `[ValueObject<string>(AllowEmpty = true)]` accepts it. A
`Quantity?` option given `""` is refused too, never `null`.

System.CommandLine calls no parser for an option left out: a `Quantity?` option left out is `null`, and a `Quantity`
option left out is the default instance, which its rules may refuse. Declare the option `T?`, set `Required`, or give it
a `DefaultValueFactory`, as for Spectre.Console.Cli. An array, a `List<T>` or an `IEnumerable<T>` left out is empty,
and an `IReadOnlyList<T>` left out is `null`, System.CommandLine's own defaults.

### A default value

A `DefaultValueFactory` returns an instance the application made, through `Create`: the package does not read it again,
and `Quantity.CreateUnchecked(500)` comes back as 500, the trust that method states. Text given for the option is read
through the rules. The help shows the default through `ToString()`, so a formatting hook's text appears there:
`[default: 21 °C]`.

### Flags

An option of a value object over `bool` is a flag, as System.CommandLine's own `bool` option is: `--verbose` alone reads
`TryCreate(true)`, and `--verbose false` and `--verbose:false` read `false`. A rule refusing `true` refuses the flag
given alone, with its code. Its `DefaultValueFactory` serves it left out and in the help, never given alone: set it
before `AcceptValueObjects()`, which wraps it, since System.CommandLine calls the factory rather than the parser for an
option given without a value whose type is not `bool`.

One difference stays: System.CommandLine's own `bool` option takes the token after it only when it reads as a boolean,
which the package cannot do, so a positional argument right after a value-object flag is read as the flag's value and
refused. Write the flag after the positional arguments, or `--verbose:true`; or declare a plain `Option<bool>` for a
flag, and create the value object from it. An arity the application set stays, `ArgumentArity.Zero` making
`--verbose false` an unrecognised token; `ExactlyOne`, System.CommandLine's default for any type but `bool`, cannot be
told from one set on purpose and becomes `ZeroOrOne`.

### The culture

The text is read in the invariant culture unless `Culture` names another, as the binders and `[ParsesAs]` read it, so a
script reads the same on every machine. The generated parsers refuse a group separator: `12,5` is
`value_object.not_parsable` rather than 125, and `03/10/2026` is 10 March.

```csharp skip
root.AcceptValueObjects(options => options.Culture = CultureInfo.CurrentCulture);   // a tool typed by people in one locale
```

Under `fr-FR`, `12,5` then reads as 12.5 and `03/10/2026` as 3 October. A `DateOnly`, a `DateTime` and a
`DateTimeOffset` are read in the Gregorian calendar whatever the culture's own, so `2026-10-03` is the same day under
`th-TH`, whose calendar is the Thai Buddhist one.

System.CommandLine reads its own `decimal`, `double` and date options in the current culture, group separators
included: under the invariant culture, its `Option<decimal>` reads `12,5` as 125 and its `Option<double>` reads `0,25`
as 25, with no error (dotnet/command-line-api#1733). A value object beside them in the same command is read in the
culture the options name.

### Help and completions

A symbol with no description takes the value object's summary. The known values become its completions, each documented
by its description, `--country ` completing `BE (Belgium)`, `FR (France)` and `LU (Luxembourg)`, unless the application
gave the symbol completions of its own. The help then labels a closed set's option `<BE|FR|LU>`, and keeps an open
set's name, `<currency>`, unless the application set `HelpName`. A completion is written as the value object reads it
back in the culture, a date in its round-trip form.

`AcceptOnlyFromAmong` is never called: it runs before any parser, compares the text as typed, refusing `de` for a type
that upper-cases it, and answers without a code. The rule refuses an unknown value with
`value_object.not_a_known_value`, and the help printed after the error lists the values. The range, the lengths and the
pattern are not written into the help, where System.CommandLine has no place for them beside the description.

### Messages in the application's language

```csharp skip
root.AcceptValueObjects(options => options.UseResource<Messages>());
```

A refusal's message is then looked up in the resources of `Messages` under the key of its code,
`ValueObject.value_object.out_of_range`, in the current UI culture, as
[a message in the application's language](../reference/errors.md#a-message-in-the-applications-language) describes. A
command line seldom has a service provider, so the lookup goes through a `ResourceManager` over the type, whose base
name is the type's full name, the one MVC's and the minimal APIs' `UseResource<T>()` read while
`LocalizationOptions.ResourcesPath` is unset: one resource then serves them all. With a `ResourcesPath`, give the
command line a type whose full name is the resource's manifest name, as
[for the DataAnnotations attributes](./data-annotations.md#messages-in-another-language). `{0}` is the option's alias as
typed or the argument's name, followed by the token's position from zero where the symbol received more than one,
`--ibans[1]` for the second, as the gRPC package's element paths and MVC's keys count, and bare for a single token, so a
template cannot rely on the brackets; the arguments from `{1}` are formatted in the current culture. A key the resource
lacks, or a blank entry, keeps the English message:

```text
« --quantity » doit être compris entre 1 et 100.
« --ibans[1] » : au moins 15 caractères.
```

System.CommandLine's own messages follow `CurrentUICulture` through its satellite assemblies, some of them still in
English ("Option '--name' is required.").

### Generic and hand-written value objects, and native AOT

The value object behind a symbol is found in the registry, which holds every value object the generator wrote. A
construction of a generic value object and a value object written by hand are described by reflection where dynamic code
runs; under native AOT, register them before the symbols are configured, or `AcceptValueObjects()` throws
`InvalidOperationException` naming the registration, even for a symbol whose `CustomParser` the application set: the
walk finds the value object before it can read that parser, which System.CommandLine's non-generic `Option` and
`Argument` do not expose.

```csharp skip
ValueObjectRegistry.Register<Reference<PurchaseOrder>, string>(static () => new Reference<PurchaseOrder>.ValueJsonConverter());
ValueObjectRegistry.Register<HandWrittenCode, string>(HandWrittenCode.Schema);
```

The package is AOT-compatible: each symbol closes its parser over the value object through the descriptor's visitor,
and CI's native AOT application parses a command of every registered value object as a native binary.

### System.CommandLine 3.0

The package is built against System.CommandLine 2.0 and runs on 3.0, whose version follows .NET 11's: 3.0 adds three
members to 2.0's public API, and the compatibility island runs the package as packed on its release candidate.

### Not covered

- Interactive shells: completion through `dotnet-suggest` or the `[suggest]` directive is not tested, beyond
  `ParseResult.GetCompletions`, nor how a Windows console prints `«` or `°`.
- A typed `AcceptValueObject<TSelf, TValue>()`, which would need no registry, is not in this version, nor a way to
  write the rules into the help.
- Spectre.Console.Cli, below, needs no package.

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
