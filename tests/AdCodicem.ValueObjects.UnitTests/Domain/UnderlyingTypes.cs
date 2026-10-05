namespace AdCodicem.ValueObjects.UnitTests.Domain;

// One value object for each underlying type the rest of the domain does not use, and for each option or hook it
// does not declare, so that the code the generator emits for each of the 22 underlying types runs at least once
// rather than only compiling (docs/adr/0006-coverage-is-a-signal-not-a-goal.md). The bounds and options are
// chosen to reach the emitted branches; the names only keep the tests readable.
//
// tests/NativeAot links this file into the domain of an application CI publishes with native AOT, and of a model it
// compiles with `dotnet ef dbcontext optimize`: a value object added here goes into that application's AppJsonContext
// too, which reports one it lacks.

// Its example is implemented explicitly, off the public surface of the type.

/// <summary>Whether a customer agreed to be contacted.</summary>
[ValueObject<bool>]
public readonly partial struct Consent : IValueObjectExample<Consent>
{
    static Consent IValueObjectExample<Consent>.Example => Create(true);
}

/// <summary>A school grade, from A to F.</summary>
[ValueObject<char>]
public readonly partial struct Grade : IValueObjectNormalizer<char>, IValueObjectMinimum<char>, IValueObjectMaximum<char>
{
    public static char Minimum => 'A';

    public static char Maximum => 'F';

    public static char NormalizeValue(char value) => char.ToUpperInvariant(value);
}

/// <summary>A thermostat adjustment, in degrees.</summary>
[ValueObject<sbyte>(Arithmetic = true)]
public readonly partial struct Adjustment : IValueObjectMinimum<sbyte>, IValueObjectMaximum<sbyte>
{
    public static sbyte Minimum => -10;

    public static sbyte Maximum => 10;
}

/// <summary>A score out of a hundred.</summary>
[ValueObject<byte>(Arithmetic = true)]
public readonly partial struct Score : IValueObjectMaximum<byte>
{
    public static byte Maximum => 100;
}

/// <summary>A TCP port.</summary>
[ValueObject<ushort>(Arithmetic = true)]
public readonly partial struct Port : IValueObjectMinimum<ushort>, IValueObjectExample<Port>
{
    public static ushort Minimum => 1;

    public static Port Example => Create(8080);
}

/// <summary>A page number, counted from one, with the first page named in an open value set.</summary>
[ValueObject<int>(Arithmetic = true)]
public readonly partial struct PageNumber : IValueObjectMinimum<int>
{
    // An auto-property, unlike the others: its initializer runs before the known value it bounds, declared after it, is
    // created.
    public static int Minimum { get; } = 1;

    [KnownValue(Description = "The first page.")]
    public static readonly PageNumber First = Known(1);
}

/// <summary>A sequence number, which starts at zero.</summary>
[ValueObject<uint>(Arithmetic = true, AllowDefault = true, ExplicitConversionFromValue = true)]
public readonly partial struct SequenceNumber;

/// <summary>The size of a file, in bytes.</summary>
[ValueObject<long>(Arithmetic = true)]
public readonly partial struct FileSize : IValueObjectMinimum<long>
{
    public static long Minimum => 0;
}

/// <summary>A count of bytes transferred.</summary>
[ValueObject<ulong>(Arithmetic = true, ImplicitConversionToValue = true)]
public readonly partial struct ByteCount;

/// <summary>A balance in the smallest unit of a currency, wider than 64 bits.</summary>
[ValueObject<Int128>(Arithmetic = true)]
public readonly partial struct LedgerBalance : IValueObjectMinimum<Int128>, IValueObjectMaximum<Int128>
{
    public static Int128 Minimum => -Maximum;

    public static Int128 Maximum => Int128.Parse("1000000000000000000000", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>The fingerprint of a document's content.</summary>
[ValueObject<UInt128>(Arithmetic = true)]
public readonly partial struct Fingerprint;

/// <summary>A latitude, in degrees.</summary>
[ValueObject<double>(Arithmetic = true)]
public readonly partial struct Latitude : IValueObjectMinimum<double>, IValueObjectMaximum<double>
{
    public static double Minimum => -90;

    public static double Maximum => 90;
}

/// <summary>A ratio between zero and one.</summary>
[ValueObject<float>(Arithmetic = true)]
public readonly partial struct Ratio : IValueObjectMinimum<float>, IValueObjectMaximum<float>
{
    public static float Minimum => 0;

    public static float Maximum => 1;
}

/// <summary>The time a shop opens.</summary>
[ValueObject<TimeOnly>]
public readonly partial struct OpeningTime : IValueObjectMinimum<TimeOnly>, IValueObjectMaximum<TimeOnly>
{
    public static TimeOnly Minimum => new(6, 0);

    public static TimeOnly Maximum => new(12, 0);
}

/// <summary>When a record was written.</summary>
[ValueObject<DateTime>]
public readonly partial struct RecordedAt : IValueObjectMinimum<DateTime>, IValueObjectMaximum<DateTime>
{
    public static DateTime Minimum => new(2000, 1, 1);

    public static DateTime Maximum => new(2099, 12, 31);
}

/// <summary>When an event occurred, with the offset it occurred at.</summary>
[ValueObject<DateTimeOffset>]
public readonly partial struct OccurredAt : IValueObjectMinimum<DateTimeOffset>
{
    public static DateTimeOffset Minimum => new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

/// <summary>How long a task took.</summary>
[ValueObject<TimeSpan>]
public readonly partial struct Duration : IValueObjectMinimum<TimeSpan>, IValueObjectMaximum<TimeSpan>, IValueObjectExample<Duration>
{
    public static TimeSpan Minimum => TimeSpan.Zero;

    public static TimeSpan Maximum => TimeSpan.FromDays(1);

    public static Duration Example => Create(new TimeSpan(1, 30, 0));
}

/// <summary>The day a contract takes effect, never before the first day the ledger covers.</summary>
[ValueObject<DateOnly>]
public readonly partial struct EffectiveDate : IValueObjectMinimum<DateOnly>
{
    // Created while the type initializes, before the bound below is assigned, so checked against its default: a bound
    // kept from that first read would let every earlier day through for good.
    public static readonly EffectiveDate LedgerStart = Create(new DateOnly(2000, 1, 1));

    public static DateOnly Minimum { get; } = new(2000, 1, 1);
}

/// <summary>A measuring tolerance, as a fraction no larger than one.</summary>
[ValueObject<double>]
public readonly partial struct Tolerance : IValueObjectMaximum<double>
{
    public static double Maximum => 1;
}

/// <summary>An international phone number, printed in groups by its own formatter.</summary>
[ValueObject<string>]
public readonly partial struct PhoneNumber : IValueObjectStringFormatter<string>, IValueObjectPatternValidator
{
    [System.Text.RegularExpressions.GeneratedRegex(
        @"^\+[0-9]{6,15}$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    public static partial System.Text.RegularExpressions.Regex Pattern { get; }

    /// <summary>The grouped format: the country part, then groups of three digits.</summary>
    public const string Grouped = "G";

    public static string FormatValue(in string value, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        _ = provider;
        if (!format.Equals(Grouped, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var grouped = new System.Text.StringBuilder(value[..3]);
        for (var i = 3; i < value.Length; i += 3)
        {
            grouped.Append(' ').Append(value.AsSpan(i, Math.Min(3, value.Length - i)));
        }

        return grouped.ToString();
    }
}

/// <summary>A free-text label, which may be empty, and whose wide formats outgrow the emitted stack buffer.</summary>
[ValueObject<string>(MaxLength = 200, AllowEmpty = true)]
public readonly partial struct Label : IValueObjectFormatter<string>
{
    /// <summary>The wide format: every character followed by a space, twice the label's length.</summary>
    public const string Wide = "W";

    /// <summary>The wider format: every character followed by three spaces, four times the label's length.</summary>
    public const string Wider = "WW";

    /// <summary>A format no destination is ever large enough for.</summary>
    public const string Unbounded = "U";

    public static bool TryFormatValue(
        in string value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        _ = provider;
        charsWritten = 0;
        if (format.Equals(Unbounded, StringComparison.Ordinal))
        {
            return false;
        }

        var spaces = format.Equals(Wider, StringComparison.Ordinal) ? 3 : format.Equals(Wide, StringComparison.Ordinal) ? 1 : 0;
        if (destination.Length < value.Length * (spaces + 1))
        {
            return false;
        }

        foreach (var character in value)
        {
            destination[charsWritten++] = character;
            for (var i = 0; i < spaces; i++)
            {
                destination[charsWritten++] = ' ';
            }
        }

        return true;
    }
}

// One known value is a field, the other a property, described by its summary.

/// <summary>The status of a document, from a closed set whose spelling does not matter.</summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.OrdinalIgnoreCase)]
public readonly partial struct DocumentStatus
{
    [KnownValue]
    public static readonly DocumentStatus Draft = Known("draft");

    /// <summary>Signed off, and no longer edited.</summary>
    [KnownValue]
    public static DocumentStatus Final { get; } = Known("final");
}
