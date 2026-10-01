# Authoring reference

## `[ValueObject<TValue>]`, option by option

| Option | Type | Default | Effect |
| --- | --- | --- | --- |
| `Pattern` | `string?` | none | Regular expression the **normalized** value must match. Also the OpenAPI `pattern`. Malformed → `VO0014`. |
| `MinLength` / `MaxLength` | `int` | `-1` (unconstrained) | `string` only (`VO0008` otherwise). Validation, OpenAPI `minLength`/`maxLength`, and the EF Core column size. |
| `Minimum` / `Maximum` | `string?` | none | Inclusive bounds written as **text**, in the one form of the underlying type ([below](#bounds-and-known-values-written-as-text)), so `decimal`, `DateOnly` and `TimeSpan` keep full precision. Numbers, `char`, dates, times and durations only: on `string`, `Guid` or `bool` → `VO0004`. Parsed at compile time; any other text, or a value outside the type → `VO0004`. |
| `Comparison` | `StringComparison` | `Ordinal` | `string` only. Drives equality, ordering, hashing. Pick `OrdinalIgnoreCase` only when the value is not case-normalized, and make the database collation agree. A value the enum does not define → `VO0020`. |
| `ValueSet` | `ValueSetKind` | `Open` | `Closed` accepts only the declared `[KnownValue]`s. Empty closed set → `VO0005`; a value the enum does not define → `VO0020`. |
| `Arithmetic` | `bool` | `false` | Numeric types only (`VO0007` otherwise). Operators, generic math, `Zero`, `One`, `IsZero`, `Min`, `Max`. |
| `ImplicitConversionToValue` | `bool` | `false` | `string s = iban;` — reading stays terse while construction stays explicit. |
| `ExplicitConversionFromValue` | `bool` | `false` | `(Iban)text` — validates, throws `ValueObjectException` on rejection. |
| `AllowEmpty` | `bool` | `false` | `string` only. Accepts `""`. `null` is rejected regardless: absence is `Iban?`. |
| `AllowDefault` | `bool` | `false` | Silences `VO0010`. Only for a type whose zero state is meaningful, such as a sequence number starting at zero. |
| `SchemaFormat` | `string?` | natural format of the underlying type | OpenAPI `format` (`uuid`, `date`, `int64`, or your own: `iban`, `email`). |
| `Example` | `string?` | none | OpenAPI example. |
| `Description` | `string?` | XML `<summary>` of the type | OpenAPI description. |

Declarative rules run **before** any hook, so a validator hook only ever sees values that already satisfy them.

## Bounds and known values written as text

`Minimum`, `Maximum` and a `[KnownValue]` given as a string are read at compile time in **one form per
underlying type**, and in no other: no culture, no time zone, no white space around the value. Anything else is
`VO0004` for a bound and `VO0013` for a known value, and the message names the form.

| Underlying type | Form | Example |
| --- | --- | --- |
| `sbyte`, `short`, `int`, `long`, `Int128` | Digits, with `-` in front when negative. | `"-42"` |
| `byte`, `ushort`, `uint`, `ulong`, `UInt128` | Digits alone. | `"42"` |
| `decimal` | Digits, an optional `-` in front, an optional fraction after `.`. No exponent. | `"-19.99"` |
| `double`, `float` | As `decimal`, plus an optional exponent (`e` or `E`, an optional sign, digits). Finite, and zero only when written as zero. | `"9.1e-31"` |
| `char` | Exactly one character. | `"A"` |
| `DateOnly` | `yyyy-MM-dd` | `"2024-01-31"` |
| `TimeOnly` | `HH:mm`, `HH:mm:ss` or `HH:mm:ss.fffffff`, one to seven digits of fraction. | `"08:30"` |
| `DateTime` | `yyyy-MM-dd` or `yyyy-MM-ddTHH:mm[:ss[.fffffff]]`. Never an offset or `Z`. | `"2024-01-31T08:30"` |
| `DateTimeOffset` | `yyyy-MM-ddTHH:mm[:ss[.fffffff]]` followed by `Z`, `+HH:mm` or `-HH:mm`, always. | `"2024-01-31T08:30+01:00"` |
| `TimeSpan` | `[-][d.]hh:mm:ss[.fffffff]`, the invariant constant format `"c"`. | `"1.12:00:00"` |
| `string` | Any text. Known values only. | `"FR"` |
| `Guid` | Any form `Guid.Parse` reads. Known values only. | `"6f9619ff-8b86-d011-b42d-00c04fc964ff"` |
| `bool` | `true` or `false`, in any case. Known values only. | `"true"` |

The value must exist in the type: `"300"` is no `byte`, `"2023-02-29"` no date, `"25:00"` no time, `"1e-400"`
(which reads as zero) no `double`. A time of day alone is neither a `DateTime` nor a `DateTimeOffset`, since it
would take the date of the day the project is built. `string`, `Guid` and `bool` take no bound at all. A known value may also be a C# constant — `200`, `0.5`,
`'A'`, `true` — which is held to the same form through its invariant text, a `double` or a `float` in round-trip
form. A `typeof(...)`, an enum member, an array and `null` are not values: `VO0013`.

## Normalization

```csharp
[ValueObject<string>(MaxLength = 64)]
public readonly partial struct ProductCode : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}
```

Idempotent, never rejecting, never called with `null`. The generated `Normalize` is the member callers use: it
guards the null and defers to `NormalizeValue`.

### Span normalization, for string value objects on hot paths

Adding `IValueObjectSpanNormalizer` routes parsing and JSON reading straight from the text, so ingesting a
value allocates the normalized string and nothing else — it halves what `TryParse` allocates. Write the
value-typed overload as a one-line delegation so the rule exists once:

```csharp
[ValueObject<string>(MaxLength = 34)]
public readonly partial struct AccountNumber : IValueObjectNormalizer<string>, IValueObjectSpanNormalizer
{
    public static string NormalizeValue(string value) => NormalizeValue(value.AsSpan());

    public static string NormalizeValue(ReadOnlySpan<char> value)
    {
        Span<char> buffer = value.Length <= 64 ? stackalloc char[64] : new char[value.Length];

        var length = 0;
        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character))
            {
                buffer[length++] = char.ToUpperInvariant(character);
            }
        }

        return new string(buffer[..length]);
    }
}
```

## Validation

```csharp
[ValueObject<int>(Minimum = "1", Maximum = "999999999")]
public readonly partial struct SequenceNumber : IValueObjectValidator<int>
{
    public static ValidationResult ValidateValue(in int value)
        => value % 2 == 0
            ? ValidationResult.Success
            : ValidationResult.Failure("sequence.odd", "A sequence number must be even.");
}
```

Factories on `ValidationResult`: `Success`, `Failure(errorCode, errorMessage)`, `Required`, `InvalidFormat`,
`OutOfRange`, `TooLong`, `TooShort`. The well-known codes live on `ValueObjectErrorCodes`
(`value_object.required`, `value_object.invalid_format`, `value_object.out_of_range`, `value_object.too_long`,
`value_object.too_short`, `value_object.not_a_known_value`, `value_object.not_parsable`). They are stable
strings that reach ProblemDetails responses, so define your own for domain rules rather than reusing a
framework code that means something else.

`ValidationResult` is a `readonly struct` whose success state is `default`: the happy path allocates nothing.
Never throw from a validator — rejection is a return value.

## Closed value sets

```csharp
[ValueObject<string>(ValueSet = ValueSetKind.Closed, MinLength = 2, MaxLength = 2, SchemaFormat = "iso-3166-alpha2")]
[KnownValue("France", "FR", Description = "France")]
[KnownValue("Belgium", "BE", Description = "Belgium")]
[KnownValue("Luxembourg", "LU", Description = "Luxembourg")]
public readonly partial struct CountryCode : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}
```

Generates `CountryCode.France`, `CountryCode.KnownValues` (an `ImmutableArray<CountryCode>` in declaration
order), a `FrozenSet` membership check rejecting anything else with `value_object.not_a_known_value`, and the
OpenAPI `enum`. This is how reference-data codes are modelled: a C# `enum` can carry neither validation nor a
stable wire format.

Values that cannot appear as an attribute argument (`Guid`, `decimal`, `DateOnly`) are written as text, in the
form of their type ([table](#bounds-and-known-values-written-as-text)), and parsed at compile time (`VO0013`
when that fails). An unusable member name is `VO0006`. On an **open** set, `[KnownValue]` still generates the constants — they are convenience only.

## Arithmetic

```csharp
[ValueObject<decimal>(Arithmetic = true, Minimum = "0")]
public readonly partial struct Amount : IValueObjectNormalizer<decimal>
{
    public static decimal NormalizeValue(decimal value) => decimal.Round(value, 2, MidpointRounding.ToEven);
}
```

Adds `+`, `-`, `*` and `/` by the underlying type, unary `-`, `Zero`, `One`, `IsZero`, `Min` and `Max`, plus
`INumericValueObject<TSelf, TValue>`. Every result goes back through `Create`, so `Amount.Zero - someAmount`
throws rather than producing a negative amount the type forbids. Division of two value objects returns the bare
underlying type: a ratio is not an amount.

## Formatting

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

Declaring the hook takes over formatting **entirely**, including the empty and `null` format specifier, so
handle the default case: `ToString()` and `$"{bban}"` write what the hook writes for it. Return `false` when the
destination is too small, and only then — that is the framework contract, and the generated
`ToString(format, provider)` retries with a pooled buffer twice as large, up to 1,048,576 characters, then throws
`FormatException`; throw it yourself for a format you do not support. For a `string` value object, text equal to the
value returns the string it holds, without allocating. Prefer `IValueObjectFormatter<TValue>`,
which formats without allocating; `IValueObjectStringFormatter<TValue>` exists for rules whose output is
naturally a `string` — its `TryFormat` copies that string — and wins everywhere when both are declared. A hook
formats text for people only: JSON, dictionary keys included, carries the underlying value.

## Domain members you *should* add

The generator owns construction, conversion, equality and text. Everything else is yours:

```csharp
[ValueObject<Guid>]
public readonly partial struct CustomerId : IValueObjectValidator<Guid>
{
    public static CustomerId New() => CreateUnchecked(Guid.CreateVersion7());

    public static ValidationResult ValidateValue(in Guid value)
        => value == Guid.Empty
            ? ValidationResult.Required("A customer identifier must not be empty.")
            : ValidationResult.Success;
}
```

`CreateUnchecked` is legitimate there: the value was just produced by the application itself. It is never
legitimate for input coming from outside.

## Nesting

A value object declared inside another type requires **every** containing type to be `partial` (`VO0009`). The
containing types are classes, structs or records without type parameters: nesting in a generic type or in an
interface is `VO0019`, and so is a value object with type parameters of its own. The value object and every type
around it are `internal` or `public` — never `private`, `protected` or `private protected` — and never `file`-local
(`VO0019`): the generated registration and the generated file reach them from outside.

## Consuming a value object

```csharp
[ValueObject<string>(MaxLength = 254, ImplicitConversionToValue = true)]
public readonly partial struct EmailAddress : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();
}

public static class Registrations
{
    public static string? Register(string input)
    {
        // Throws ValueObjectException on a rejected value.
        var confirmed = EmailAddress.Create("Ada@Example.COM ");

        // Never throws, and says which rule fired.
        if (!EmailAddress.TryCreate(input, out var email, out var validation))
        {
            return validation.ErrorCode;      // e.g. "value_object.too_long"
        }

        // Text in, value object out, same rules, same error codes.
        _ = EmailAddress.TryParse(input, provider: null, out _, out _);

        // Value is always normalized and valid; ImplicitConversionToValue makes reading terse.
        string address = email;

        return string.Equals(address, confirmed.Value, StringComparison.Ordinal) ? null : address;
    }
}
```

`IsDefault` is the runtime guard for an instance that crossed a boundary the `VO0010` analyzer cannot see.
