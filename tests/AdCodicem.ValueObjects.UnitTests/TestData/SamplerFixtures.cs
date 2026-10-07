using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.TestData;

// Value objects the sampler is tested on, beside those of Domain/: rules it draws from one at a time, the patterns of the
// issue, the edges of the underlying types, and rules no schema carries. None is in Domain/, so none has a contract or a
// place in the JSON context of the native AOT application.

/// <summary>
/// Digits written after <c>EVEN-</c> that add up to an even number: a rule no schema carries, which no candidate drawn from
/// its schema meets.
/// </summary>
[ValueObject<string>(MaxLength = 12)]
public readonly partial struct EvenCode : IValueObjectValidator<string>
{
    public static ValidationResult ValidateValue(in string value) => EvenCodes.Validate(value);
}

/// <summary>The same rule, with an example: the sampler falls back on it.</summary>
[ValueObject<string>(MaxLength = 12)]
public readonly partial struct ExampledEvenCode : IValueObjectValidator<string>, IValueObjectExample<ExampledEvenCode>
{
    public static ExampledEvenCode Example => Create("EVEN-1234");

    public static ValidationResult ValidateValue(in string value) => EvenCodes.Validate(value);
}

/// <summary>The rule of <see cref="EvenCode"/>, and a generator of the codes it accepts.</summary>
public static class EvenCodes
{
    public static ValidationResult Validate(string value)
        => value.StartsWith("EVEN-", StringComparison.Ordinal)
           && value.Length > 5
           && value[5..].All(char.IsAsciiDigit)
           && value[5..].Sum(static digit => digit - '0') % 2 == 0
            ? ValidationResult.Success
            : ValidationResult.Failure("checksum", "The digits of the code do not add up to an even number.");

    public static string Draw(Random random)
    {
        var digits = random.Next(1000).ToString("D3", CultureInfo.InvariantCulture);
        return "EVEN-" + digits + (digits.Sum(static digit => digit - '0') % 2 == 0 ? "0" : "1");
    }
}

/// <summary>A code whose normalizer counts its calls, one per candidate: lengths and a pattern it is drawn from.</summary>
[ValueObject<string>(MinLength = 3, MaxLength = 8)]
public readonly partial struct CountedCode : IValueObjectNormalizer<string>, IValueObjectPatternValidator
{
    [ThreadStatic]
    private static int _calls;

    /// <summary>Gets how many candidates this thread normalized.</summary>
    public static int Calls => _calls;

    [GeneratedRegex("^[a-z]+[0-9]?$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    public static void ResetCalls() => _calls = 0;

    public static string NormalizeValue(string value)
    {
        _calls++;
        return value;
    }
}

/// <summary>A number whose normalizer counts its calls, one per candidate: bounds it is drawn between.</summary>
[ValueObject<int>]
public readonly partial struct CountedNumber : IValueObjectNormalizer<int>, IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    [ThreadStatic]
    private static int _calls;

    /// <summary>Gets how many candidates this thread normalized.</summary>
    public static int Calls => _calls;

    public static int Minimum => -5;

    public static int Maximum => 5;

    public static void ResetCalls() => _calls = 0;

    public static int NormalizeValue(int value)
    {
        _calls++;
        return value;
    }
}

/// <summary>A string of at least four characters with no maximum.</summary>
[ValueObject<string>(MinLength = 4)]
public readonly partial struct Remark;

/// <summary>A string with no declared length.</summary>
[ValueObject<string>]
public readonly partial struct FreeText;

/// <summary>A price, from one cent to 999.99, whose bounds step by a cent.</summary>
[ValueObject<decimal>]
public readonly partial struct Price : IValueObjectMinimum<decimal>, IValueObjectMaximum<decimal>
{
    public static decimal Minimum => 0.01m;

    public static decimal Maximum => 999.99m;
}

/// <summary>A decimal with no bound, drawn across the whole type.</summary>
[ValueObject<decimal>]
public readonly partial struct UnboundedDecimal;

/// <summary>A decimal bounded at the extremes of the type, past which no step is taken.</summary>
[ValueObject<decimal>]
public readonly partial struct CappedDecimal : IValueObjectMinimum<decimal>, IValueObjectMaximum<decimal>
{
    public static decimal Minimum => decimal.MinValue;

    public static decimal Maximum => decimal.MaxValue;
}

/// <summary>An integer bounded at the extremes of the type, past which no step is taken.</summary>
[ValueObject<int>]
public readonly partial struct ExtremeBounds : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    public static int Minimum => int.MinValue;

    public static int Maximum => int.MaxValue;
}

/// <summary>A time from midnight, before which no step wraps around to the evening before.</summary>
[ValueObject<TimeOnly>]
public readonly partial struct MidnightTime : IValueObjectMinimum<TimeOnly>
{
    public static TimeOnly Minimum => TimeOnly.MinValue;
}

/// <summary>An instant bounded by instants written with offsets, which the sampler compares as instants.</summary>
[ValueObject<DateTimeOffset>]
public readonly partial struct OffsetBounded : IValueObjectMinimum<DateTimeOffset>, IValueObjectMaximum<DateTimeOffset>
{
    public static DateTimeOffset Minimum => new(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));

    public static DateTimeOffset Maximum => new(2026, 1, 2, 0, 0, 0, TimeSpan.FromHours(-5));
}

/// <summary>A character bounded at both extremes of the type, past which no step is taken.</summary>
[ValueObject<char>]
public readonly partial struct AnyCharacter : IValueObjectMinimum<char>, IValueObjectMaximum<char>
{
    public static char Minimum => char.MinValue;

    public static char Maximum => char.MaxValue;
}

/// <summary>A time up to the last tick of the day, after which no step wraps around to midnight.</summary>
[ValueObject<TimeOnly>]
public readonly partial struct LatestTime : IValueObjectMaximum<TimeOnly>
{
    public static TimeOnly Maximum => TimeOnly.MaxValue;
}

/// <summary>A date up to the last the type holds.</summary>
[ValueObject<DateOnly>]
public readonly partial struct LatestDate : IValueObjectMaximum<DateOnly>
{
    public static DateOnly Maximum => DateOnly.MaxValue;
}

/// <summary>A clock reading up to the last the type holds.</summary>
[ValueObject<DateTime>]
public readonly partial struct LatestInstant : IValueObjectMaximum<DateTime>
{
    public static DateTime Maximum => DateTime.MaxValue;
}

/// <summary>A duration bounded at both extremes of the type.</summary>
[ValueObject<TimeSpan>]
public readonly partial struct AnyDuration : IValueObjectMinimum<TimeSpan>, IValueObjectMaximum<TimeSpan>
{
    public static TimeSpan Minimum => TimeSpan.MinValue;

    public static TimeSpan Maximum => TimeSpan.MaxValue;
}

/// <summary>A closed set of both booleans: nothing lies outside it.</summary>
[ValueObject<bool>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct EveryAnswer
{
    [KnownValue]
    public static readonly EveryAnswer Yes = Known(true);

    [KnownValue]
    public static readonly EveryAnswer No = Known(false);
}

/// <summary>A closed set of one letter, compared ignoring case: its lower-case spelling is no value outside it.</summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, MinLength = 1, MaxLength = 1, Comparison = StringComparison.OrdinalIgnoreCase)]
public readonly partial struct CaseFolded
{
    [KnownValue]
    public static readonly CaseFolded A = Known("A");
}

/// <summary>A closed set whose normalizer maps every value onto its one member: nothing lies outside it.</summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct Everything : IValueObjectNormalizer<string>
{
    [KnownValue]
    public static readonly Everything One = Known("ONE");

    public static string NormalizeValue(string value) => "ONE";
}

/// <summary>A character with no bound: printable ASCII.</summary>
[ValueObject<char>]
public readonly partial struct UnboundedChar;

/// <summary>A character from an accented letter up, beyond printable ASCII: the open side takes the type's maximum.</summary>
[ValueObject<char>]
public readonly partial struct AccentedChar : IValueObjectMinimum<char>
{
    public static char Minimum => '\u00E9';
}

/// <summary>A control character, below printable ASCII: the open side takes the type's minimum.</summary>
[ValueObject<char>]
public readonly partial struct ControlChar : IValueObjectMaximum<char>
{
    public static char Maximum => '\u001F';
}

/// <summary>A character up to a lower-case z: the open side keeps to printable ASCII.</summary>
[ValueObject<char>]
public readonly partial struct LowerChar : IValueObjectMaximum<char>
{
    public static char Maximum => 'z';
}

/// <summary>Three upper-case letters, through a Unicode category the built-in sampler leaves to rejection sampling.</summary>
[ValueObject<string>(MinLength = 3, MaxLength = 3)]
public readonly partial struct UpperThree : IValueObjectPatternValidator
{
    [GeneratedRegex(@"^\p{Lu}{3}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>Eight letters or digits, one a digit, through a lookahead the reader refuses.</summary>
[ValueObject<string>(MinLength = 8, MaxLength = 8)]
public readonly partial struct PasswordLike : IValueObjectPatternValidator
{
    [GeneratedRegex(@"^(?=.*\d)[A-Za-z\d]{8}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>The same pattern, with no declared length: only the pattern holds the eight characters.</summary>
[ValueObject<string>]
public readonly partial struct PasswordLikeWithoutLengths : IValueObjectPatternValidator
{
    [GeneratedRegex(@"^(?=.*\d)[A-Za-z\d]{8}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>A country code in a named group, then digits.</summary>
[ValueObject<string>]
public readonly partial struct NamedGroup : IValueObjectPatternValidator
{
    [GeneratedRegex(@"^(?<cc>[A-Z]{2})\d+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>One of two prefixes, then two digits.</summary>
[ValueObject<string>]
public readonly partial struct Alternated : IValueObjectPatternValidator
{
    [GeneratedRegex(@"^(?:FR|BE)\d{2}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>Three letters, a dash, three digits.</summary>
[ValueObject<string>]
public readonly partial struct Dashed : IValueObjectPatternValidator
{
    [GeneratedRegex(@"^[A-Z]{3}-\d{3}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>Consonants, through a class subtraction, and an optional suffix.</summary>
[ValueObject<string>]
public readonly partial struct Subtracted : IValueObjectPatternValidator
{
    [GeneratedRegex(@"^[a-z-[aeiou]]{4,6}(?:\.[0-9a-f]{2})?$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>A word between word boundaries, anchors that draw nothing.</summary>
[ValueObject<string>]
public readonly partial struct WordBounded : IValueObjectPatternValidator
{
    [GeneratedRegex(@"^\bab+\b$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>
/// Five characters or more holding a digit, through an unanchored pattern whose whole matches are one character long:
/// the built-in sampler pads a match to the lengths, at either end.
/// </summary>
[ValueObject<string>(MinLength = 5)]
public readonly partial struct HoldsADigit : IValueObjectPatternValidator
{
    [GeneratedRegex("[0-9]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>An address of an e-mail domain, held at its end only, longer than any match of its pattern.</summary>
[ValueObject<string>(MinLength = 12)]
public readonly partial struct CorporateEmail : IValueObjectPatternValidator, IValueObjectExample<CorporateEmail>
{
    public static CorporateEmail Example => Create("jane@acme.com");

    [GeneratedRegex(@"@acme\.com$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>A number starting with a dialling prefix, held at its start only, longer than any match of its pattern.</summary>
[ValueObject<string>(MinLength = 10)]
public readonly partial struct DialledNumber : IValueObjectPatternValidator, IValueObjectExample<DialledNumber>
{
    public static DialledNumber Example => Create("555-0100 ext");

    [GeneratedRegex(@"^\d{3}-\d{4}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>Three characters or more holding an at sign, held at neither end, with no example to fall back on.</summary>
[ValueObject<string>(MinLength = 3)]
public readonly partial struct HoldsAnAtSign : IValueObjectPatternValidator
{
    [GeneratedRegex("@", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>A digest written in 64 hexadecimal digits, with no declared length: only the pattern holds its length.</summary>
[ValueObject<string>]
public readonly partial struct Digest : IValueObjectPatternValidator
{
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>A UUID written as text, 36 characters, with no declared length.</summary>
[ValueObject<string>]
public readonly partial struct UuidText : IValueObjectPatternValidator
{
    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>A token of 40 letters or digits, beside a declared minimum of one character.</summary>
[ValueObject<string>(MinLength = 1)]
public readonly partial struct LongToken : IValueObjectPatternValidator
{
    [GeneratedRegex("^[A-Za-z0-9]{40}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>
/// An e-mail address whose local part a lookahead holds to 64 characters, as such patterns do: outside the subset, and
/// drawn from its pattern read without the lookahead.
/// </summary>
[ValueObject<string>(MaxLength = 254)]
public readonly partial struct GuardedEmail : IValueObjectPatternValidator, IValueObjectExample<GuardedEmail>
{
    public static GuardedEmail Example => Create("someone@example.com");

    [GeneratedRegex(@"^(?=.{1,64}@)[^@\s]+@[^@\s]+\.[a-z]{2,}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>A number plate, through upper-case letters of a Unicode category and dashes: outside the subset.</summary>
[ValueObject<string>(MinLength = 9, MaxLength = 9)]
public readonly partial struct Plate : IValueObjectPatternValidator, IValueObjectExample<Plate>
{
    public static Plate Example => Create("AB-123-CD");

    [GeneratedRegex(@"^\p{Lu}{2}-\d{3}-\p{Lu}{2}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>A comment its normalizer trims, whose pattern lets a space stand at either end of what is drawn.</summary>
[ValueObject<string>(MaxLength = 10)]
public readonly partial struct Comment : IValueObjectNormalizer<string>, IValueObjectPatternValidator
{
    [GeneratedRegex("^[^<>]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    public static string NormalizeValue(string value) => value.Trim();
}

/// <summary>A name its normalizer trims, of letters and spaces.</summary>
[ValueObject<string>(MinLength = 2, MaxLength = 12)]
public readonly partial struct PersonName : IValueObjectNormalizer<string>, IValueObjectPatternValidator
{
    [GeneratedRegex("^[A-Za-z ]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    public static string NormalizeValue(string value) => value.Trim();
}

/// <summary>A code its normalizer cuts to five characters, rather than its validator refusing a longer one.</summary>
[ValueObject<string>(MaxLength = 5)]
public readonly partial struct CutCode : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Length > 5 ? value[..5] : value;
}

/// <summary>Pairs of letters, of an odd length nothing matches: no string of its declared lengths can be drawn.</summary>
[ValueObject<string>(MinLength = 3, MaxLength = 3)]
public readonly partial struct OddPairs : IValueObjectPatternValidator
{
    [GeneratedRegex("^(ab)+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>A schema a value object written by hand declares, with the rule its <c>Validate</c> enforces.</summary>
/// <typeparam name="TValue">The underlying type.</typeparam>
public interface IDeclaration<TValue>
{
    static abstract ValueObjectSchema Schema { get; }

    /// <summary>Validates a value; anything but <see langword="null"/> by default.</summary>
    static virtual ValidationResult Validate(TValue value)
        => value is null ? ValidationResult.Required() : ValidationResult.Success;

    /// <summary>Reads text; nothing by default.</summary>
    static virtual bool TryRead(string text, out TValue value)
    {
        value = default!;
        return false;
    }

    /// <summary>Normalizes a value; leaves it as it is by default.</summary>
    static virtual TValue NormalizeValue(TValue value) => value;

    /// <summary>Validates a value <c>Create</c> is given, if not as <see cref="Validate"/> does; <see langword="null"/> for the same.</summary>
    static virtual ValidationResult? ValidateOnCreate(TValue value) => null;
}

/// <summary>
/// A value object written by hand over any underlying type, whose schema and rule <typeparamref name="TDeclaration"/>
/// declares: the schemas the generator never writes. Nothing registers it, and the sampler never asks the registry.
/// </summary>
/// <typeparam name="TDeclaration">The declaration.</typeparam>
/// <typeparam name="TValue">The underlying type.</typeparam>
/// <remarks>Its JSON converter reads any value unchecked, as a converter written carelessly does.</remarks>
#pragma warning disable CA1000 // The members of a value object are static, and the generic type is the point of the fixture.
[JsonConverter(typeof(UncheckedJsonConverterFactory))]
public readonly struct Declared<TDeclaration, TValue> : IValueObject<Declared<TDeclaration, TValue>, TValue>
    where TDeclaration : IDeclaration<TValue>
{
    private readonly TValue _value;

    private Declared(TValue value) => _value = value;

    public static ValueObjectSchema Schema => TDeclaration.Schema;

    public TValue Value => _value;

    public bool IsDefault => EqualityComparer<TValue>.Default.Equals(_value, default!);

    public static TValue Normalize(TValue value) => TDeclaration.NormalizeValue(value);

    public static ValidationResult Validate(in TValue value) => TDeclaration.Validate(value);

    public static Declared<TDeclaration, TValue> Create(TValue value)
    {
        (TDeclaration.ValidateOnCreate(value) ?? Validate(value)).ThrowIfInvalid(typeof(Declared<TDeclaration, TValue>), value);

        return new(value);
    }

    public static bool TryCreate(TValue value, out Declared<TDeclaration, TValue> result) => TryCreate(value, out result, out _);

    public static bool TryCreate(TValue value, out Declared<TDeclaration, TValue> result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new(value) : default;

        return validation.IsValid;
    }

    public static Declared<TDeclaration, TValue> CreateUnchecked(TValue value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out Declared<TDeclaration, TValue> result, out ValidationResult validation)
    {
        if (TDeclaration.TryRead(text.ToString(), out var value))
        {
            return TryCreate(value, out result, out validation);
        }

        result = default;
        validation = ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, "The text is not a value.");

        return false;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Declared<TDeclaration, TValue> result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out Declared<TDeclaration, TValue> result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static Declared<TDeclaration, TValue> Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException("The text is not a value.");

    public static Declared<TDeclaration, TValue> Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    public bool Equals(Declared<TDeclaration, TValue> other) => EqualityComparer<TValue>.Default.Equals(_value, other._value);

    public override bool Equals(object? obj) => obj is Declared<TDeclaration, TValue> other && Equals(other);

    public override int GetHashCode() => _value is null ? 0 : EqualityComparer<TValue>.Default.GetHashCode(_value);

    public int CompareTo(Declared<TDeclaration, TValue> other) => Comparer<TValue>.Default.Compare(_value, other._value);

    public override string ToString() => ToString(null, null);

    public string ToString(string? format, IFormatProvider? formatProvider) => Convert.ToString(_value, CultureInfo.InvariantCulture) ?? string.Empty;

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        var text = ToString();
        charsWritten = text.TryCopyTo(destination) ? text.Length : 0;

        return charsWritten == text.Length;
    }

    public static bool operator ==(Declared<TDeclaration, TValue> left, Declared<TDeclaration, TValue> right) => left.Equals(right);

    public static bool operator !=(Declared<TDeclaration, TValue> left, Declared<TDeclaration, TValue> right) => !left.Equals(right);

    public static bool operator <(Declared<TDeclaration, TValue> left, Declared<TDeclaration, TValue> right) => left.CompareTo(right) < 0;

    public static bool operator >(Declared<TDeclaration, TValue> left, Declared<TDeclaration, TValue> right) => left.CompareTo(right) > 0;

    public static bool operator <=(Declared<TDeclaration, TValue> left, Declared<TDeclaration, TValue> right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Declared<TDeclaration, TValue> left, Declared<TDeclaration, TValue> right) => left.CompareTo(right) >= 0;
}
#pragma warning restore CA1000

/// <summary>Reads and writes a <see cref="Declared{TDeclaration, TValue}"/> as its value, read through no rule.</summary>
public sealed class UncheckedJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Declared<,>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(typeof(UncheckedJsonConverter<,>).MakeGenericType(typeToConvert.GetGenericArguments()))!;

    private sealed class UncheckedJsonConverter<TDeclaration, TValue> : JsonConverter<Declared<TDeclaration, TValue>>
        where TDeclaration : IDeclaration<TValue>
    {
        public override Declared<TDeclaration, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => Declared<TDeclaration, TValue>.CreateUnchecked(JsonSerializer.Deserialize<TValue>(ref reader, options)!);

        public override void Write(Utf8JsonWriter writer, Declared<TDeclaration, TValue> value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value.Value, options);
    }
}

/// <summary>An integer whose example is written as text, and whose rule accepts the example alone.</summary>
public sealed class TextExample : IDeclaration<int>
{
    public static ValueObjectSchema Schema { get; } = new() { Example = "42" };

    public static ValidationResult Validate(int value) => value == 42 ? ValidationResult.Success : ValidationResult.OutOfRange("Only 42.");

    public static bool TryRead(string text, out int value) => int.TryParse(text, CultureInfo.InvariantCulture, out value);
}

/// <summary>A string whose pattern .NET does not compile: the reader refuses it, and nothing checks a candidate against it.</summary>
public sealed class InvalidPattern : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { Pattern = "(unclosed", MinLength = 2, MaxLength = 6 };
}

/// <summary>A real whose minimum reads as an infinity and whose maximum reads as nothing: both are ignored.</summary>
public sealed class UnreadableBounds : IDeclaration<double>
{
    public static ValueObjectSchema Schema { get; } = new() { Minimum = "1e999", Maximum = "one" };
}

/// <summary>A closed set whose known values are mostly of another type than the underlying one.</summary>
public sealed class MistypedKnownValues : IDeclaration<int>
{
    public static ValueObjectSchema Schema { get; } = new() { IsClosedValueSet = true, KnownValues = [1L, "2", 3] };

    public static ValidationResult Validate(int value) => value is 1 or 2 or 3 ? ValidationResult.Success : ValidationResult.Failure(ValueObjectErrorCodes.NotAKnownValue, "Not known.");
}

/// <summary>Known values left at the default of their array: none.</summary>
public sealed class DefaultKnownValues : IDeclaration<int>
{
    public static ValueObjectSchema Schema { get; } = new() { KnownValues = default, Minimum = "0", Maximum = "9" };
}

/// <summary>A link: an underlying type the sampler draws no value of, with no example.</summary>
public sealed class Link : IDeclaration<Uri>
{
    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;
}

/// <summary>A closed set of links: nothing can be drawn outside it.</summary>
public sealed class ClosedLinks : IDeclaration<Uri>
{
    public static ValueObjectSchema Schema { get; } = new() { IsClosedValueSet = true, KnownValues = [new Uri("https://example.com/")] };
}

/// <summary>A string no longer than nothing.</summary>
public sealed class ZeroLength : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { MaxLength = 0 };
}

/// <summary>A string whose lengths are below zero, as no schema the generator writes holds.</summary>
public sealed class NegativeLength : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { MinLength = -3, MaxLength = -1 };
}

/// <summary>An integer from 0 whose rule refuses its own minimum, and whose example is its minimum's neighbour.</summary>
public sealed class RefusedMinimum : IDeclaration<int>
{
    public static ValueObjectSchema Schema { get; } = new() { Minimum = "0", Maximum = "100", Example = 1 };

    public static ValidationResult Validate(int value) => value > 0 ? ValidationResult.Success : ValidationResult.OutOfRange("Above zero.");
}

/// <summary>An integer from 0 to 10, whose JSON converter reads any value: the JSON check of the contract kit refuses it.</summary>
public sealed class LaxJson : IDeclaration<int>
{
    public static ValueObjectSchema Schema { get; } = new() { Minimum = "0", Maximum = "10" };

    public static ValidationResult Validate(int value) => value is >= 0 and <= 10 ? ValidationResult.Success : ValidationResult.OutOfRange("From 0 to 10.");
}

/// <summary>An integer whose example is its minimum, proposed once when shrinking.</summary>
public sealed class ExampleAtMinimum : IDeclaration<int>
{
    public static ValueObjectSchema Schema { get; } = new() { Minimum = "5", Maximum = "100", Example = 5 };
}

/// <summary>A real whose bounds are the wrong way round: no value lies between them.</summary>
public sealed class InvertedReal : IDeclaration<double>
{
    public static ValueObjectSchema Schema { get; } = new() { Minimum = "10", Maximum = "1" };

    public static ValidationResult Validate(double value) => ValidationResult.OutOfRange("No value lies from 10 to 1.");
}

/// <summary>A decimal whose bounds are the wrong way round: no value lies between them.</summary>
public sealed class InvertedDecimal : IDeclaration<decimal>
{
    public static ValueObjectSchema Schema { get; } = new() { Minimum = "10", Maximum = "1" };

    public static ValidationResult Validate(decimal value) => ValidationResult.OutOfRange("No value lies from 10 to 1.");
}

/// <summary>An integer from 0 whose refusal carries a blank code: the contract kit refuses it.</summary>
public sealed class BlankCode : IDeclaration<int>
{
    public static ValueObjectSchema Schema { get; } = new() { Minimum = "0" };

    public static ValidationResult Validate(int value) => value >= 0 ? ValidationResult.Success : ValidationResult.Failure(" ", "Below zero.");
}

/// <summary>An integer from 0 whose refusal carries no message: the contract kit refuses it.</summary>
public sealed class BlankMessage : IDeclaration<int>
{
    public static ValueObjectSchema Schema { get; } = new() { Minimum = "0" };

    public static ValidationResult Validate(int value) => value >= 0 ? ValidationResult.Success : ValidationResult.Failure(ValueObjectErrorCodes.OutOfRange, string.Empty);
}

/// <summary>An integer from 0 whose <c>Create</c> throws another code than <c>TryCreate</c> reports: the contract kit refuses it.</summary>
public sealed class TwoCodes : IDeclaration<int>
{
    public static ValueObjectSchema Schema { get; } = new() { Minimum = "0" };

    public static ValidationResult Validate(int value) => value >= 0 ? ValidationResult.Success : ValidationResult.OutOfRange("Below zero.");

    public static ValidationResult? ValidateOnCreate(int value) => value >= 0 ? null : ValidationResult.Failure("negative", "Below zero.");
}

/// <summary>A string of at most three characters whose normalizer gives nothing back, as one written carelessly does.</summary>
public sealed class NullNormalized : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { MaxLength = 3 };

    public static string NormalizeValue(string value) => null!;
}
