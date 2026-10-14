using System.Globalization;
using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.Blazor;

// Value objects whose formatting hook writes text their parser does not read back, or reads back to another value: the
// input shows each one's bare underlying value instead, in the form that parser reads.

/// <summary>A storey, printed <c>storey 3</c> by a string formatter alone.</summary>
[ValueObject<int>]
public readonly partial struct Storey : IValueObjectStringFormatter<int>
{
    /// <inheritdoc />
    public static string FormatValue(in int value, ReadOnlySpan<char> format, IFormatProvider? provider)
        => format.IsEmpty ? "storey " + value.ToString(provider) : value.ToString(format.ToString(), provider);
}

/// <summary>A holiday, printed <c>14 October 2026</c>.</summary>
[ValueObject<DateOnly>]
public readonly partial struct Holiday : IValueObjectFormatter<DateOnly>
{
    /// <inheritdoc />
    public static bool TryFormatValue(in DateOnly value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => value.TryFormat(destination, out charsWritten, format.IsEmpty ? "d MMMM yyyy" : format, CultureInfo.InvariantCulture);
}

/// <summary>A departure, printed to the minute, <c>10:30</c>.</summary>
[ValueObject<TimeOnly>]
public readonly partial struct Departure : IValueObjectFormatter<TimeOnly>
{
    /// <inheritdoc />
    public static bool TryFormatValue(in TimeOnly value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => value.TryFormat(destination, out charsWritten, format.IsEmpty ? "HH:mm" : format, CultureInfo.InvariantCulture);
}

/// <summary>When a parcel was stamped, printed to the minute and without its kind, <c>2026-10-14 10:30</c>.</summary>
[ValueObject<DateTime>]
public readonly partial struct StampedAt : IValueObjectStringFormatter<DateTime>
{
    /// <inheritdoc />
    public static string FormatValue(in DateTime value, ReadOnlySpan<char> format, IFormatProvider? provider)
        => value.ToString(format.IsEmpty ? "yyyy-MM-dd HH:mm" : format.ToString(), CultureInfo.InvariantCulture);
}

/// <summary>When a contract was signed, printed to the minute, <c>2026-10-14 10:30 +02:00</c>.</summary>
[ValueObject<DateTimeOffset>]
public readonly partial struct SignedAt : IValueObjectFormatter<DateTimeOffset>
{
    /// <inheritdoc />
    public static bool TryFormatValue(in DateTimeOffset value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => value.TryFormat(destination, out charsWritten, format.IsEmpty ? "yyyy-MM-dd HH:mm zzz" : format, CultureInfo.InvariantCulture);
}

/// <summary>A lapse of time, printed in hours and minutes, <c>1:01</c>.</summary>
[ValueObject<TimeSpan>]
public readonly partial struct Lapse : IValueObjectFormatter<TimeSpan>
{
    /// <inheritdoc />
    public static bool TryFormatValue(in TimeSpan value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => value.TryFormat(destination, out charsWritten, format.IsEmpty ? @"h\:mm" : format, CultureInfo.InvariantCulture);
}
