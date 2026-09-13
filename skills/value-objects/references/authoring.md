# Authoring reference

## `[ValueObject<TValue>]`, option by option

| Option | Type | Default | Effect |
| --- | --- | --- | --- |
| `Pattern` | `string?` | none | Regular expression the **normalized** value must match. Also the OpenAPI `pattern`. Malformed → `VO0014`. |
| `MinLength` / `MaxLength` | `int` | `-1` (unconstrained) | `string` only (`VO0008` otherwise). Validation, OpenAPI `minLength`/`maxLength`, and the EF Core column size. |
| `Minimum` / `Maximum` | `string?` | none | Inclusive bounds written in **invariant-culture text**, so `decimal`, `DateOnly` and `TimeSpan` keep full precision. Parsed at compile time; unparsable → `VO0004`. |
| `Comparison` | `StringComparison` | `Ordinal` | `string` only. Drives equality, ordering, hashing. Pick `OrdinalIgnoreCase` only when the value is not case-normalized, and make the database collation agree. |
| `ValueSet` | `ValueSetKind` | `Open` | `Closed` accepts only the declared `[KnownValue]`s. Empty closed set → `VO0005`. |
| `Arithmetic` | `bool` | `false` | Numeric types only (`VO0007` otherwise). Operators, generic math, `Zero`, `One`, `IsZero`, `Min`, `Max`. |
| `ImplicitConversionToValue` | `bool` | `false` | `string s = iban;` — reading stays terse while construction stays explicit. |
| `ExplicitConversionFromValue` | `bool` | `false` | `(Iban)text` — validates, throws `ValueObjectException` on rejection. |
| `AllowEmpty` | `bool` | `false` | `string` only. Accepts `""`. `null` is rejected regardless: absence is `Iban?`. |
| `AllowDefault` | `bool` | `false` | Silences `VO0010`. Only for a type whose zero state is meaningful, such as a sequence number starting at zero. |
| `SchemaFormat` | `string?` | natural format of the underlying type | OpenAPI `format` (`uuid`, `date`, `int64`, or your own: `iban`, `email`). |
| `Example` | `string?` | none | OpenAPI example. |
| `Description` | `string?` | XML `<summary>` of the type | OpenAPI description. |

Declarative rules run **before** any hook, so a validator hook only ever sees values that already satisfy them.

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

Values that cannot appear as an attribute argument (`Guid`, `decimal`, `DateOnly`) are written as
invariant-culture text and parsed at compile time (`VO0013` when that fails). An unusable member name is
`VO0006`. On an **open** set, `[KnownValue]` still generates the constants — they are convenience only.

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
handle the default case. Return `false` when the destination is too small — that is the framework contract, and
the generated `ToString(format, provider)` grows a buffer and retries. Prefer `IValueObjectFormatter<TValue>`,
which formats without allocating; `IValueObjectStringFormatter<TValue>` exists for rules whose output is
naturally a `string`, and wins when both are declared.

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

A value object declared inside another type requires **every** containing type to be `partial` (`VO0009`).

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
