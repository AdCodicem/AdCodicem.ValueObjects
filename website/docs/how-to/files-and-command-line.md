---
title: Read and Write Files and Command Lines
sidebar_label: Files and command line
slug: /how-to/files-and-command-line
description: Read and write value objects in CSV with Sep and with CsvHelper, through AdCodicem.ValueObjects.CsvHelper, in spreadsheets with ClosedXML, MiniExcel and EPPlus, and on the command line with System.CommandLine, through AdCodicem.ValueObjects.CommandLine, and Spectre.Console.Cli, reporting the rule a cell or an argument breaks.
---

# Read and write files and command lines

A value object implements `ISpanParsable<T>` and `ISpanFormattable`, and carries a `TypeConverter`, which is what a
library that reads text without knowing the type looks for. Where a library finds one of them, it works as is; where
it does not, the recipe is to write the underlying values and parse them back cell by cell with `TryParse`, which
says which rule a value broke. CsvHelper and System.CommandLine read none of them: `AdCodicem.ValueObjects.CsvHelper`
maps each value object as one CSV column read through its rules, and `AdCodicem.ValueObjects.CommandLine` reads the
options and arguments of a command through them.

## Sep

Sep 0.17.1 works as is, through `ISpanParsable<T>` and `ISpanFormattable`:

```csharp skip
var iban = row["Iban"].Parse<Iban>();   // throws ValueObjectException on a value the type rejects

if (!Quantity.TryParse(row["Quantity"].Span, CultureInfo.InvariantCulture, out var quantity, out var validation))
{
    // validation.ErrorCode names the rule
}
```

## CsvHelper

CsvHelper 33.1 reads no `TypeDescriptor`, and looks a type's converter up in a cache of its own, which holds none for a
value object. Without the package, CsvHelper maps a value object as a record of its own:

- it writes a column per public property of the value object, `Value` among them, rather than one named after the
  member, so the header holds `Value` columns and none of the members' names;
- it reads each value object back as a default instance, from its own file as from one whose header names the members,
  with nothing thrown, since `Value` has no setter;
- with a class map naming the member, or through `GetField<Iban>`, it writes the text but refuses every field on read
  with "The conversion cannot be performed.", naming no rule and carrying no code;
- it writes a default instance as values its type rejects, `00000000-0000-0000-0000-000000000000` or empty text.

`AdCodicem.ValueObjects.CsvHelper` maps each value object as one column, written as its own text and read through its
rules:

```bash
dotnet add package AdCodicem.ValueObjects.CsvHelper
```

### Registration

```csharp skip
using AdCodicem.ValueObjects.CsvHelper;

using var reader = new CsvReader(textReader, CultureInfo.InvariantCulture);
reader.Context.TypeConverterCache.AddValueObjects();   // before the first GetRecords or RegisterClassMap
var orders = reader.GetRecords<Order>().ToList();       // one column per value object, read through its rules

using var writer = new CsvWriter(textWriter, CultureInfo.InvariantCulture);
writer.Context.TypeConverterCache.AddValueObjects();   // the writer's context needs it too
writer.WriteRecords(orders);
```

`AddValueObjects()` adds a converter factory, which CsvHelper asks before its own the first time it meets a type: it
claims every value object, never its nullable type, around which CsvHelper's own `NullableConverter` puts the value
object's converter, and closes that converter from the registry when CsvHelper meets the type. A value object is found
however late its assembly's module initializer runs, which a walk of the registry when the reader is created would miss.
`AddValueObject<TSelf, TValue>()` adds the converter of one value object, asking no registry; it answers before any
factory.

CsvHelper reads the cache when it builds a map, and the package cannot change that. Register on the context of every
reader and writer, before its first `GetRecords`, `WriteRecords` or `RegisterClassMap`:

- a converter added after the first read changes nothing for a type already mapped: the records keep reading default
  instances;
- `RegisterClassMap` copies each member's converter from the cache at once, so a class map registered first keeps
  CsvHelper's own and refuses every field with "The conversion cannot be performed.".

### Class maps

A class map built over the reader's context works: `csv.Context.AutoMap<Order>()` after `AddValueObjects()`, then
customized; `Map(m => m.Iban).TypeConverter<ValueObjectTypeConverter<Iban, string>>()` in a class map; a class map
calling `AutoMap(context)` over a context it gives `AddValueObjects()`, while the reader's context has it too, since
`RegisterClassMap` copies the converters of the reader's.
`[TypeConverter(typeof(ValueObjectTypeConverter<Iban, string>))]` on the member works even through a context with
nothing registered.

A class map calling `AutoMap(CultureInfo)` in its constructor does not: CsvHelper builds a context of its own for it,
which sees none of the reader's converters, so the value objects come back as `Value` columns read as default instances.
Call `AutoMap(context)` over a context given `AddValueObjects()` instead.

Named directly, through `[TypeConverter]` or `TypeConverter<T>()`, the converter is not wrapped in CsvHelper's
`NullableConverter`, so on a `Quantity?` property it answers `null` itself for an empty field and for each of the
member's `NullValues`, as that converter does. A positional record's parameter reaches a converter it names with no
type: on a `Quantity?` parameter, leave the converter to `AddValueObjects()` or `AddValueObject<TSelf, TValue>()`, or
an empty field is refused as `value_object.required`.

### What is written

A value object is written as its own text, which its parser reads back in the member's culture, not as CsvHelper writes
the underlying type. Under the invariant culture:

| | value-object column | primitive column |
| --- | --- | --- |
| A date | `2026-10-03` | `10/03/2026` |
| A `DateTime` | `2026-10-03T14:05:06.7890000Z`, its milliseconds and its kind kept | `10/03/2026 14:05:06` |
| A `TimeOnly` | `08:30:00.0000000` | `08:30` |

A value object whose formatting hook writes text its parser may not read back is written as its underlying value:
`21`, not `21 °C`. A `Quantity?` holding nothing is written as empty text, or as the first of its `NullValues`, `NULL`
when configured: its column stays, so the row reads back. A `Label?` holding the empty instance of a type that accepts
empty text is written as empty text too, and reads back as `null`, whatever its `NullValues`, since CsvHelper answers
`null` for an empty field of a nullable member before any rule. In a file of one column, a row of empty text is a blank
line, which CsvHelper skips on read: set `IgnoreBlankLines = false` to read such a file.

An uninitialized instance its rules refuse is not written: `WriteRecords`, `WriteRecord` and `WriteField` throw
CsvHelper's `TypeConverterException`, "The value to write is not a valid Iban: The value must not be empty.", the
rule's code in its `Data`, as the JSON converters and the Dapper handler refuse one. A type that accepts its zero
writes it: `0` for a default `Latitude`. CsvHelper writes field by field into a buffer that disposing the writer
flushes, so the rows before a refused one, and the start of its own row, reach the output: write to a temporary file,
and move it into place once `WriteRecords` returns.

### What is read

A field is read through `TSelf.TryParse` in the member's culture, a date in the Gregorian calendar whatever the
culture's own, and the member gets the normalized instance: `fr76 3000 6000 0112 3456 7890 189` reads as
`FR7630006000011234567890189`, and `be` as `BE` in a closed set. The field reaches the rules as CsvHelper hands it over:
` Hello ` keeps its spaces unless CsvHelper's `TrimOptions` trims them, and a field of spaces is no empty field, refused
by the parser of a number as `value_object.not_parsable`. Every read is strict: a file comes from outside even when the
application wrote it, since people and spreadsheets edit it, and the package has no trust option.

An empty field is refused with `value_object.required`, "An empty field cannot be read as Quantity; declare the member
as a nullable Quantity? instead.", whatever the type's parser would say of no text, `not_parsable` for a number or a
`Guid`. A type that accepts empty text, `[ValueObject<string>(AllowEmpty = true)]`, reads it. A `Quantity?` member reads
`null` for an empty field and for each of its `NullValues`, which CsvHelper's `NullableConverter` answers before the
converter runs, and the converter itself when [named on the member](#class-maps): on an `Iban` member, `NULL` is text,
refused by the rules.

CsvHelper's options for the underlying type are not read: `Formats`, `NumberStyles`, `BooleanTrueValues` and
`BooleanFalseValues` change nothing, and `1` is no `true`. Only the culture is. CsvHelper keys options by the member's
exact type, so options set for `Amount` do not reach an `Amount?` member: set them for both, as for an `int?`.

A map's default applies as CsvHelper applies it: an empty field takes `Default(Quantity.Create(1))` as it is, and a
default written as text, `[Default("BE")]`, is read through the rules, so `[Default("XX")]` on a country code is
refused. `Default(…, useOnConversionFailure: true)` returns the default in place of a refusal, with no code, where
CsvHelper's own converters would: a default of the value object, or `null` for a nullable member; any other default
leaves the refusal and its code.

### The culture of a file

The culture is the member's: the configuration's (`new CsvConfiguration(culture)`), unless the application set one for
the type (`csv.Context.TypeConverterOptionsCache.GetOptions<Amount>().CultureInfo`) or for the member
(`[CultureInfo("fr-FR")]`, `Map(m => m.Amount).TypeConverterOption.CultureInfo(…)`). A French file reads `12,5` and
`03/10/2026`, and writes `12,50` and `2026-10-03`, which reads back in any culture. Under the invariant culture, a
quoted `12,5` is refused as `value_object.not_parsable`, where CsvHelper's own `decimal` column would read 125. A date
is written in the Gregorian calendar and read back in a culture whose calendar is another, `th-TH`, `fa-IR` or `ar-SA`.
A spreadsheet opening the file parses `2026-10-03` and `08:30:00.0000000` in its own locale, which was not checked.

### What a refused field looks like

A refused field is CsvHelper's own `TypeConverterException`, which `GetRecords` lets through as it is. Its message names
the type and the rule, never the field, before the state of the reader that CsvHelper adds:

```text
The field is not a valid Quantity: The value must be less than or equal to 100.
The field is not a valid Iban: The value must be at least 15 characters long.
An empty field cannot be read as Iban; declare the member as a nullable Iban? instead.
```

Its `Data` carries the rule's code under `ValueObjectErrors.ErrorCodeKey`, which `ValueObjectErrors.TryGetCode` reads. A
rule with an empty message gives "The field is not a valid X.", and so does a parser written by hand that refuses
without a reason, with `value_object.not_parsable`. An import that reports each refused row in the user's language and
goes on handles `ReadingExceptionOccurred`, and looks the message up by code in the application's `IStringLocalizer`,
here `localizer`:

```csharp skip
var refused = new List<string>();
var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ReadingExceptionOccurred = args =>
    {
        if (args.Exception is TypeConverterException refusal && ValueObjectErrors.TryGetCode(refusal, out var code))
        {
            refused.Add($"row {refusal.Context?.Parser?.Row}: {Message(refusal, code)}");
            return false;   // skip the row, keep reading
        }

        return true;        // anything else, a misuse included, stops the read
    },
};

string Message(TypeConverterException refusal, string code)
{
    var column = refusal.MemberMapData.Names[0];
    var type = refusal.MemberMapData.Type;   // Quantity? on a nullable member
    if (ValueObjectRegistry.TryGet(Nullable.GetUnderlyingType(type) ?? type, out var descriptor))
    {
        var arguments = ValueObjectMessages.ArgumentsFor(descriptor, code, CultureInfo.CurrentCulture)
            .Select(static argument => argument.Value!).Prepend(column).ToArray();
        foreach (var key in ValueObjectMessages.KeysFor(descriptor.Schema, code))
        {
            var message = localizer[key, arguments];
            if (!message.ResourceNotFound)
            {
                return message.Value;
            }
        }
    }

    return $"{column}: {code}";   // no entry, or a positional record's parameter, whose map data names no type
}
```

`MemberMapData.Names[0]` is the column, a positional record's parameter included, whose `MemberMapData.Member` is
`null` and whose `MemberMapData.Type` reads `string`. The keys and the arguments are those
[a message in the application's language](../reference/errors.md#a-message-in-the-applications-language) describes,
`{0}` the column here; the package has no options of its own, since CsvHelper shows no message to a person.

`TypeConverterException.Text` holds the refused field, as CsvHelper's own converters set it, empty text for a field a
short row lacks, and CsvHelper ends every message with the raw record.
`CsvConfiguration.ExceptionMessagesContainRawData = false` keeps both out of an exception, and so out of a log: `Text`
then reads "Hidden because ExceptionMessagesContainRawData is false.", as for CsvHelper's own converters.

### Missing columns and short rows

A value object is a member map, not a record of its own, so a column missing from the header is CsvHelper's own
`HeaderValidationException`, "Header with name 'Iban'[0] was not found.", with no code; a `Quantity?` column too, unless
the member carries `[Optional]`. A row cut short under `MissingFieldFound = null` leaves CsvHelper handing the converter
no field: a `Quantity` is refused as `value_object.required`, its `Text` empty, and a `Quantity?` reads `null`.

### Generic and hand-written value objects in a file, and native AOT

The factory finds a construction of a generic value object, `Reference<PurchaseOrder>`, and a value object written by
hand in the registry, which describes them by reflection where dynamic code runs. Under native AOT, register them, or
add their converter with `AddValueObject<TSelf, TValue>()`; otherwise the factory throws `InvalidOperationException`
naming both, with no code, rather than let CsvHelper read default instances. With a header, CsvHelper meets the type at
header validation and lets the exception through; without one, it wraps it in a `ReaderException` per row, which a
`ReadingExceptionOccurred` returning `false` for anything would skip: return `true` for anything but a coded refusal, as
above. `WriteRecords` and `WriteRecord` wrap it in a `WriterException`, "An unexpected error occurred. See inner
exception for details.", and `GetField<T>` and `WriteField<T>` throw it as it is.

The package is AOT-compatible, but CsvHelper's record API is not: auto-mapping and class maps fail in a native binary
with `MissingMethodException`, and CsvHelper's publish warns 17 times (CsvHelper issue #2391). Its `GetField<T>` and
`WriteField<T>` run natively through the package's factory. CI publishes no native application over CsvHelper; the
factory's path without dynamic code runs in a test of its own.

### What the CsvHelper package leaves out

- `CsvDataReader` and dynamic records hand out strings: no value object is involved.
- A collection member (`List<Iban>`, `Iban[]`). With a header, auto-mapping leaves it out of the map. A class map
  naming it hands it to CsvHelper's own collection converters, which the package does not reach: they write each
  element's `ToString()`, so an uninitialized `Iban` is written as empty text and a `Celsius` as `21 °C`, which reads
  back as no element; and they read the columns of the member's name in turn, stopping at the first element its rules
  refuse, with nothing thrown: `3,1001` reads `[3]`, `1001,3` reads `[]`. Without a header, each element is read by
  index through the package's converter, and a refusal carries its code. Map one column per value object instead.
- A value object as the record itself: CsvHelper reads only primitive types as records of one field, so
  `GetRecords<Iban>()` throws "No members are mapped for type", and `WriteRecords` writes the public properties of each
  value object as columns, `CountryCode,Value` for an `Iban`, without the package's converter. Read and write such a
  column with `GetField<Iban>(0)` and `WriteField(iban)`, or put the value object in a class of one member.
- `Formats`, `NumberStyles` and the boolean values of CsvHelper's options, as above.
- CsvHelper's last release is 33.1.0, of 2025-06-02; the package is built against it.

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
