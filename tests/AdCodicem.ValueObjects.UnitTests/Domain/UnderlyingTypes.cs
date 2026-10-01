namespace AdCodicem.ValueObjects.UnitTests.Domain;

// One value object for each underlying type the rest of the domain does not use, and for each option or hook it
// does not declare, so that the code the generator emits for each of the 22 underlying types runs at least once
// rather than only compiling (docs/adr/0006-coverage-is-a-signal-not-a-goal.md). The bounds and options are
// chosen to reach the emitted branches; the names only keep the tests readable.

/// <summary>Whether a customer agreed to be contacted.</summary>
[ValueObject<bool>]
public readonly partial struct Consent;

/// <summary>A school grade, from A to F.</summary>
[ValueObject<char>(Minimum = "A", Maximum = "F")]
public readonly partial struct Grade : IValueObjectNormalizer<char>
{
    public static char NormalizeValue(char value) => char.ToUpperInvariant(value);
}

/// <summary>A thermostat adjustment, in degrees.</summary>
[ValueObject<sbyte>(Arithmetic = true, Minimum = "-10", Maximum = "10")]
public readonly partial struct Adjustment;

/// <summary>A score out of a hundred.</summary>
[ValueObject<byte>(Arithmetic = true, Maximum = "100")]
public readonly partial struct Score;

/// <summary>A TCP port.</summary>
[ValueObject<ushort>(Arithmetic = true, Minimum = "1")]
public readonly partial struct Port;

/// <summary>A page number, counted from one, with the first page named in an open value set.</summary>
[ValueObject<int>(Arithmetic = true, Minimum = "1")]
[KnownValue("First", 1, Description = "The first page.")]
public readonly partial struct PageNumber;

/// <summary>A sequence number, which starts at zero.</summary>
[ValueObject<uint>(Arithmetic = true, AllowDefault = true, ExplicitConversionFromValue = true)]
public readonly partial struct SequenceNumber;

/// <summary>The size of a file, in bytes.</summary>
[ValueObject<long>(Arithmetic = true, Minimum = "0")]
public readonly partial struct FileSize;

/// <summary>A count of bytes transferred.</summary>
[ValueObject<ulong>(Arithmetic = true, ImplicitConversionToValue = true)]
public readonly partial struct ByteCount;

/// <summary>A balance in the smallest unit of a currency, wider than 64 bits.</summary>
[ValueObject<Int128>(Arithmetic = true, Minimum = "-1000000000000000000000", Maximum = "1000000000000000000000")]
public readonly partial struct LedgerBalance;

/// <summary>The fingerprint of a document's content.</summary>
[ValueObject<UInt128>(Arithmetic = true)]
public readonly partial struct Fingerprint;

/// <summary>A latitude, in degrees.</summary>
[ValueObject<double>(Arithmetic = true, Minimum = "-90", Maximum = "90")]
public readonly partial struct Latitude;

/// <summary>A ratio between zero and one.</summary>
[ValueObject<float>(Arithmetic = true, Minimum = "0", Maximum = "1")]
public readonly partial struct Ratio;

/// <summary>The time a shop opens.</summary>
[ValueObject<TimeOnly>(Minimum = "06:00", Maximum = "12:00")]
public readonly partial struct OpeningTime;

/// <summary>When a record was written.</summary>
[ValueObject<DateTime>(Minimum = "2000-01-01", Maximum = "2099-12-31")]
public readonly partial struct RecordedAt;

/// <summary>When an event occurred, with the offset it occurred at.</summary>
[ValueObject<DateTimeOffset>(Minimum = "2000-01-01T00:00:00+00:00")]
public readonly partial struct OccurredAt;

/// <summary>How long a task took.</summary>
[ValueObject<TimeSpan>(Minimum = "00:00:00", Maximum = "1.00:00:00")]
public readonly partial struct Duration;

/// <summary>An international phone number, printed in groups by its own formatter.</summary>
[ValueObject<string>(Pattern = @"^\+[0-9]{6,15}$")]
public readonly partial struct PhoneNumber : IValueObjectStringFormatter<string>
{
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

/// <summary>A free-text label, which may be empty, and whose wide format outgrows the emitted stack buffer.</summary>
[ValueObject<string>(MaxLength = 200, AllowEmpty = true)]
public readonly partial struct Label : IValueObjectFormatter<string>
{
    /// <summary>The wide format: every character followed by a space, twice the label's length.</summary>
    public const string Wide = "W";

    /// <summary>A format no destination is ever large enough for, which falls back to the plain text.</summary>
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

        var wide = format.Equals(Wide, StringComparison.Ordinal);
        var required = wide ? value.Length * 2 : value.Length;
        if (destination.Length < required)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (wide)
            {
                destination[charsWritten++] = value[i];
                destination[charsWritten++] = ' ';
            }
            else
            {
                destination[charsWritten++] = value[i];
            }
        }

        return true;
    }
}

/// <summary>The status of a document, from a closed set whose spelling does not matter.</summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.OrdinalIgnoreCase)]
[KnownValue("Draft", "draft")]
[KnownValue("Final", "final")]
public readonly partial struct DocumentStatus;
