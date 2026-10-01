using System.Globalization;

namespace AdCodicem.ValueObjects.UnitTests.Domain;

// Value objects whose formatting hook writes, by default, something other than the bare value, so that a test can
// tell which member answered: the hook, or the underlying value.

/// <summary>
/// A floor of a building, which declares both formatting hooks with different outputs: the string formatter is
/// the one that answers.
/// </summary>
[ValueObject<int>(Minimum = "-5", Maximum = "200")]
public readonly partial struct Floor : IValueObjectFormatter<int>, IValueObjectStringFormatter<int>
{
    public static string FormatValue(in int value, ReadOnlySpan<char> format, IFormatProvider? provider)
        => format.IsEmpty
            ? "floor " + value.ToString(provider)
            : value.ToString(format.ToString(), provider);

    /// <summary>Writes the bare number, which the string formatter takes precedence over.</summary>
    public static bool TryFormatValue(
        in int value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
        => value.TryFormat(destination, out charsWritten, format, provider);
}

/// <summary>A temperature in degrees Celsius, printed with its unit unless a numeric format is asked for.</summary>
[ValueObject<int>(Minimum = "-273")]
public readonly partial struct Celsius : IValueObjectFormatter<int>
{
    public static bool TryFormatValue(
        in int value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
        => format.IsEmpty
            ? destination.TryWrite(CultureInfo.InvariantCulture, $"{value} °C", out charsWritten)
            : value.TryFormat(destination, out charsWritten, format, provider);
}
