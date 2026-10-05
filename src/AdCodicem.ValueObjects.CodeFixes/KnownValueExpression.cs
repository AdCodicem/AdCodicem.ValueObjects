using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Generators.Internal;
using AdCodicem.ValueObjects.Generators.Model;

namespace AdCodicem.ValueObjects.CodeFixes;

/// <summary>
/// Writes the value a <c>[KnownValue]</c> on the type declared, as a constant or as text, as the C# expression of its
/// underlying type that <c>Known</c> takes.
/// </summary>
/// <remarks>
/// The value is read as the generator read it, in the one form of its type (<see cref="LiteralFactory"/>), and written
/// as a person would write it: <c>new DateOnly(1970, 1, 1)</c>, <c>new TimeSpan(1, 12, 0, 0)</c>, <c>0.2m</c>. Names are
/// fully qualified, for the clean-up of the code action to shorten. A value that does not read, which the generator
/// refused too, has no expression.
/// </remarks>
internal static class KnownValueExpression
{
    private const string System = "global::System";

    private static readonly Regex OffsetLiteral = new(
        @"^new global::System\.DateTimeOffset\((?<local>-?\d+)L, new global::System\.TimeSpan\((?<offset>-?\d+)L\)\)$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Writes a value as an expression of the underlying type.
    /// </summary>
    /// <param name="underlying">The underlying type.</param>
    /// <param name="value">The value as the attribute held it: a constant, or text.</param>
    /// <returns>The expression, or <see langword="null"/> when the value does not read as one of the type.</returns>
    public static string? Write(UnderlyingType underlying, object? value)
    {
        if (!LiteralFactory.TryCreate(underlying, value, out var literal, out var key))
        {
            return null;
        }

        return underlying.Kind switch
        {
            // An integer constant converts to any integer type that holds it, so the digits alone are enough, up to the
            // widest literal C# has.
            UnderlyingKind.SByte or UnderlyingKind.Byte or UnderlyingKind.Int16 or UnderlyingKind.UInt16
                or UnderlyingKind.Int32 or UnderlyingKind.UInt32 or UnderlyingKind.Int64 or UnderlyingKind.UInt64
                => Digits((BigInteger)key),
            UnderlyingKind.Int128 or UnderlyingKind.UInt128
                => (BigInteger)key >= long.MinValue && (BigInteger)key <= ulong.MaxValue ? Digits((BigInteger)key) : literal,
            UnderlyingKind.TimeOnly => TimeOfDay((long)key) ?? literal,
            UnderlyingKind.DateTime => DateAndTime((long)key) ?? literal,
            UnderlyingKind.DateTimeOffset => Instant(literal) ?? literal,
            UnderlyingKind.TimeSpan => Duration((long)key) ?? literal,
            _ => literal,
        };
    }

    private static string Digits(BigInteger number) => number.ToString(CultureInfo.InvariantCulture);

    private static string? TimeOfDay(long ticks)
        => Clock(ticks, secondsRequired: false) is { } clock ? $"new {System}.TimeOnly({clock})" : null;

    private static string? DateAndTime(long ticks)
    {
        var moment = new DateTime(ticks, DateTimeKind.Unspecified);
        var date = Invariant($"{moment.Year}, {moment.Month}, {moment.Day}");
        if (moment.TimeOfDay == TimeSpan.Zero)
        {
            return $"new {System}.DateTime({date})";
        }

        return Clock(moment.TimeOfDay.Ticks, secondsRequired: true) is { } clock
            ? $"new {System}.DateTime({date}, {clock})"
            : null;
    }

    private static string? Instant(string literal)
    {
        var match = OffsetLiteral.Match(literal);
        if (!match.Success)
        {
            return null;
        }

        var moment = new DateTime(long.Parse(match.Groups["local"].Value, CultureInfo.InvariantCulture), DateTimeKind.Unspecified);
        var offset = new TimeSpan(long.Parse(match.Groups["offset"].Value, CultureInfo.InvariantCulture));
        var written = offset == TimeSpan.Zero
            ? $"{System}.TimeSpan.Zero"
            : Invariant($"new {System}.TimeSpan({offset.Hours}, {offset.Minutes}, 0)");

        return Clock(moment.TimeOfDay.Ticks, secondsRequired: true) is { } clock
            ? Invariant($"new {System}.DateTimeOffset({moment.Year}, {moment.Month}, {moment.Day}, {clock}, {written})")
            : null;
    }

    private static string? Duration(long ticks)
    {
        if (ticks == long.MinValue)
        {
            return null;
        }

        var magnitude = new TimeSpan(Math.Abs(ticks));
        var parts = SubSecond(magnitude.Ticks % TimeSpan.TicksPerSecond);
        if (parts is null)
        {
            return null;
        }

        var written = magnitude.Days == 0 && parts.Length == 0
            ? Invariant($"new {System}.TimeSpan({magnitude.Hours}, {magnitude.Minutes}, {magnitude.Seconds})")
            : Invariant($"new {System}.TimeSpan({magnitude.Days}, {magnitude.Hours}, {magnitude.Minutes}, {magnitude.Seconds}{Join(parts)})");

        return ticks < 0 ? "-" + written : written;
    }

    /// <summary>
    /// Writes a time of day as the arguments of a constructor: hours and minutes, then seconds, milliseconds and
    /// microseconds as far as they are needed.
    /// </summary>
    private static string? Clock(long ticks, bool secondsRequired)
    {
        var time = new TimeSpan(ticks);
        var parts = SubSecond(ticks % TimeSpan.TicksPerSecond);
        if (parts is null)
        {
            return null;
        }

        return !secondsRequired && time.Seconds == 0 && parts.Length == 0
            ? Invariant($"{time.Hours}, {time.Minutes}")
            : Invariant($"{time.Hours}, {time.Minutes}, {time.Seconds}{Join(parts)}");
    }

    /// <summary>
    /// Splits the part of a second into milliseconds and microseconds, as far as they are needed, or none when it holds
    /// ticks finer than a microsecond, which no constructor of a date or a time takes beside its other parts.
    /// </summary>
    private static int[]? SubSecond(long ticks)
    {
        if (ticks % 10 != 0)
        {
            return null;
        }

        var milliseconds = (int)(ticks / TimeSpan.TicksPerMillisecond);
        var microseconds = (int)(ticks % TimeSpan.TicksPerMillisecond / 10);

        return microseconds != 0 ? [milliseconds, microseconds] : milliseconds != 0 ? [milliseconds] : [];
    }

    private static string Join(int[] parts)
        => string.Concat(parts.Select(static part => ", " + part.ToString(CultureInfo.InvariantCulture)));

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
