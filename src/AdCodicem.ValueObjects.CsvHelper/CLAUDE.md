# AdCodicem.ValueObjects.CsvHelper — CsvHelper columns

User-facing behaviour: `website/docs/how-to/files-and-command-line.md` (CsvHelper). Tests: `Csv/` of the unit suite
(reads and writes in memory through `CsvHarness`, a context per read or write), and the island's `CsvHelperTests.cs`.
Out of `tests/NativeAot`.

## When CsvHelper reads its converter cache

- CsvHelper reads `TypeConverterCache` when it builds a map: converters added after a context's first `GetRecords` or
  `WriteRecords` change nothing for a type already mapped, and `RegisterClassMap` copies each member's converter at once
  (`ClassMapCollection.SetMapDefaults`), so `AddValueObjects()` comes first; `CsvAssumptionsTests` pins both.
- `ClassMap.AutoMap(CultureInfo)` builds a `CsvContext` of its own, which sees no converter of the reader's: a class map
  calling it in its constructor maps a value object as a record of its own, read as default instances.
  `AutoMap(context)` over a context given `AddValueObjects()`, `csv.Context.AutoMap<T>()`,
  `TypeConverter<ValueObjectTypeConverter<,>>()` and `[TypeConverter(typeof(…))]` work; `CsvAssumptionsTests` pins each.
- `ClassMap.AutoMapMembers` maps a member as a record of its own when its converter's type is exactly
  `DefaultTypeConverter`, which is what `TypeConverterCache.GetConverter` returns for a type no factory claims: the
  factory claims every value object and throws `InvalidOperationException` for one it cannot describe, rather than
  decline it, or CsvHelper reads default instances with nothing thrown.
- `TypeConverterCache` keeps the factory chosen for a type before calling its `Create`, and keeps the converter only
  once `Create` returns: a `Create` that threw is called again the next time, so a registration made since is found
  (`WithoutDynamicCodeCsvTests`).
- CsvHelper creates a converter named by `[TypeConverter]` or `TypeConverter<T>()` through its `ObjectResolver`, which
  needs a public parameterless constructor (`MissingMethodException` otherwise): `ValueObjectTypeConverter<,>` keeps
  one.
- `[TypeConverter]` and `TypeConverter<T>()` set the member's converter as it is, with no `NullableConverter` around it
  (`TypeConverterAttribute.ApplyTo`, `MemberMap.TypeConverter<T>()`): named on a `TSelf?` property, the converter gets
  empty text and the `NullValues`, so it answers `null` for them itself when `memberMapData.Type` is nullable, before
  any rule (`CsvReadTests`). A positional record's parameter reaches it with `new MemberMapData(null)`, whose `Type`
  reads `string`: named on a `TSelf?` parameter, it refuses an empty field as required (`CsvAssumptionsTests`), and the
  guide says to leave that converter to the cache.

## Reading and writing a field

- The text is read and written through `src/Shared/ValueObjectText.cs` (a date in a Gregorian copy of the culture, a
  reason-less refusal as `not_parsable`, the round-trip text and the underlying value's for a formatting hook); the
  empty-field rule and the refused default are this package's own, as the shared file asks. The converter passes
  `unexplained: string.Empty`, so that a reason-less refusal reads "The field is not a valid X." with X through
  `TypeNames`, never the shared fallback's `typeof(TSelf).Name` (``Reference`1``).
- The field reaches `TryParse` as CsvHelper hands it over: trimming is CsvHelper's `TrimOptions`, and only `null` or
  empty text is the empty field (`CsvReadTests` pins spaces kept and a field of spaces refused by the parser).
- `CsvWriter.WriteConvertedField` writes nothing, not even the delimiter, for a `null` field: a `TSelf?` holding nothing
  is written through `base.ConvertToString` (the first `NullValues` entry, or empty text), never `null`.
- CsvHelper's `NullableConverter` answers `null` for empty text and for each of the member's `NullValues` before the
  package's converter runs: `NullValues` work on a `TSelf?` alone, and on a `TSelf` over text `NULL` is text. It tests
  empty text first, so a `TSelf?` holding the empty instance of a type accepting empty text, written as empty text,
  reads back as `null` whatever its `NullValues` (`CsvAssumptionsTests`; the parity leaves such instances out).
- CsvHelper keys `TypeConverterOptionsCache` by the member's exact type: options set for `Amount` do not reach an
  `Amount?` member (`CsvCultureTests`), as for an `int?`.
- CsvHelper's own converters put the field in `TypeConverterException.Text`, or `"Hidden because
  ExceptionMessagesContainRawData is false."` under that setting (`DefaultTypeConverter.CreateTypeConverterException`),
  and empty text for a field a short row lacks, which reaches a converter as `null`: the package does the same, and
  never quotes the field in its message; CsvHelper appends the raw record to every message unless the setting is
  `false`.
- `DefaultTypeConverter.ConvertFromString` returns the map's default under `useOnConversionFailure` only for a default
  of the member's type, or `null` for a nullable member, and throws its code-less "The conversion cannot be performed."
  otherwise: the converter returns the default in the same two cases and throws the coded refusal in every other.
- CsvHelper reads a member's default when the field is empty, before any converter: a default of the member's type as it
  is, and a default written as text (`[Default("BE")]`) through the converter, so the rules apply to it.
- A positional record's parameter reaches the converter with `new MemberMapData(null)`, its name in `Names` and no
  `Member`, and without `UseDefaultOnConversionFailure`, which CsvHelper does not copy for a parameter.
- `GetRecords` rethrows a `CsvHelperException` as it is and wraps anything else, the factory's
  `InvalidOperationException` included, in a `ReaderException` ("An unexpected error occurred."), and `WriteRecords` and
  `WriteRecord` in a `WriterException`: a file with a header meets the misuse at header validation, unwrapped; a
  headerless one once per row, wrapped, which a `ReadingExceptionOccurred` returning `false` would skip. `GetField<T>`
  and `WriteField<T>` let it through as it is (`WithoutDynamicCodeCsvTests`).
- CsvHelper writes field by field into a buffer that disposing the writer flushes: a refused write leaves the rows
  before it and the start of its own row in the output (`CsvWriteTests`); the guide says to write to a temporary file.
- A single-column row whose field is empty text is a blank line, which CsvHelper skips (`IgnoreBlankLines`, `true` by
  default; `CsvAssumptionsTests` pins it): the guide says so, and a test of empty text puts the value after another
  column (`CsvLine<T>`) or quotes it (`""`).

## Shapes that never reach the converter

- CsvHelper's collection converters (`IEnumerableConverter`, `CollectionGenericConverter`) never call the element's
  converter on write (`row.WriteField(item.ToString())`), and read a collection by name through `TryGetField`, which
  swallows a refusal and ends the collection there; by index, without a header, each element goes through the
  converter. Auto-mapping with a header leaves a collection member out (`ClassMap.AutoMapMembers` skips the
  enumerable converters). `CsvAssumptionsTests` pins all three and the guide lists collections as left out; claiming
  `List<TSelf>` and `TSelf[]` in the factory would be a design of its own.
- CsvHelper reads and writes only `IsPrimitive` types as records of one field (`RecordCreatorFactory`,
  `RecordWriterFactory`): `GetRecords<Iban>()` throws "No members are mapped", and `WriteRecords` of value objects
  writes their public properties (`CsvAssumptionsTests`). The guide points at `GetField<T>`, `WriteField<T>` or a class
  of one member.

## Native AOT and names

- CsvHelper 33.1 builds the maps of its record API with `MakeGenericType` (`DefaultClassMap<T>`, `MemberMap<,>`), which
  fails natively with `MissingMethodException`, and its publish warns 17 times; its `GetField<T>` and `WriteField<T>`
  run natively through the factory. The package stays out of `tests/NativeAot`; its no-dynamic-code path runs in
  `WithoutDynamicCodeCsvTests`.
- The namespace hides the root `CsvHelper` namespace (root `CLAUDE.md`): name CsvHelper's types through usings at the
  top of a file. In the unit suite, `CsvHelper.MissingFieldException` meets `System.MissingFieldException` (alias it),
  and a file importing `CsvHelper.Configuration.Attributes` never imports `System.ComponentModel` (two
  `TypeConverterAttribute`s).
