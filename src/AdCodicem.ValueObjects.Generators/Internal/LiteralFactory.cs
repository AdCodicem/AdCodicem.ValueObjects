using System;
using System.Globalization;
using System.Numerics;
using AdCodicem.ValueObjects.Generators.Model;

namespace AdCodicem.ValueObjects.Generators.Internal;

/// <summary>
/// Turns attribute arguments into C# literal expressions of the underlying type.
/// </summary>
/// <remarks>
/// <para>
/// Attribute arguments can only carry a handful of constant types, so bounds and known values of types such as
/// <c>Guid</c>, <c>decimal</c> or <c>DateOnly</c> are written as text. They are parsed here, at compile time,
/// which means a malformed bound is a build error rather than a start-up exception.
/// </para>
/// <para>
/// Each type is read in one canonical form, the one <see cref="UnderlyingType.LiteralForm"/> describes, and in no
/// other: no white space around it, no culture, no time zone, and nothing a parser would fill in from the build
/// machine, such as today's date for a time written alone. The same text therefore compiles to the same literal
/// on every machine. A <c>string</c>, a <c>Guid</c> and a <c>bool</c> keep every form they read, short of the white
/// space around it.
/// </para>
/// </remarks>
internal static class LiteralFactory
{
    // The generator targets netstandard2.0, which has neither Int128 nor UInt128 to read the limits from.
    private static readonly BigInteger Int128Maximum = (BigInteger.One << 127) - 1;

    private static readonly BigInteger Int128Minimum = -(BigInteger.One << 127);

    private static readonly BigInteger UInt128Maximum = (BigInteger.One << 128) - 1;

    /// <summary>
    /// Builds the C# literal expression for a value of the given underlying type.
    /// </summary>
    /// <param name="underlying">Underlying type descriptor.</param>
    /// <param name="value">Value read from the attribute argument.</param>
    /// <param name="literal">The literal expression when the value is convertible.</param>
    /// <returns><see langword="true"/> when the value could be converted.</returns>
    public static bool TryCreate(UnderlyingType underlying, object? value, out string literal)
        => TryCreate(underlying, value, out literal, out _);

    /// <summary>
    /// Builds the C# literal expression for a value of the given underlying type, and the value it names in a form
    /// the generator can compare with another value of that type.
    /// </summary>
    /// <param name="underlying">Underlying type descriptor.</param>
    /// <param name="value">Value read from the attribute argument.</param>
    /// <param name="literal">The literal expression when the value is convertible.</param>
    /// <param name="key">
    /// The value itself when it is convertible, ordered and compared as the generated code orders and compares it: the
    /// text of a string, a <see cref="BigInteger"/> for every integer type, a <see cref="decimal"/>, a
    /// <see cref="double"/> for a <c>double</c> or a <c>float</c>, the ticks of a date, a time or a duration, the
    /// ticks of the instant a <c>DateTimeOffset</c> names, and the value for a <c>Guid</c>, a <c>bool</c> or a
    /// <c>char</c>.
    /// </param>
    /// <returns><see langword="true"/> when the value could be converted.</returns>
    public static bool TryCreate(UnderlyingType underlying, object? value, out string literal, out IComparable key)
    {
        literal = string.Empty;
        key = string.Empty;
        if (value is null)
        {
            return false;
        }

        var text = Text(value);
        if (text is null)
        {
            return false;
        }

        var position = 0;
        switch (underlying.Kind)
        {
            case UnderlyingKind.String:
                literal = Quote(text);
                key = text;
                return true;

            case UnderlyingKind.Guid:
                // Every form Guid reads is kept, but not the white space it would trim.
                if (HasSurroundingWhiteSpace(text) || !Guid.TryParse(text, out var guid))
                {
                    return false;
                }

                literal = $"new global::System.Guid({Quote(guid.ToString("D", CultureInfo.InvariantCulture))})";
                key = guid;
                return true;

            case UnderlyingKind.Boolean:
                // Read in any case, as bool reads it, but without the white space and the trailing nulls it would trim.
                if (string.Equals(text, bool.TrueString, StringComparison.OrdinalIgnoreCase))
                {
                    literal = "true";
                    key = true;
                    return true;
                }

                if (string.Equals(text, bool.FalseString, StringComparison.OrdinalIgnoreCase))
                {
                    literal = "false";
                    key = false;
                    return true;
                }

                return false;

            case UnderlyingKind.Char:
                if (text.Length != 1)
                {
                    return false;
                }

                literal = $"'{Escape(text)}'";
                key = text[0];
                return true;

            case UnderlyingKind.SByte:
                return TryInteger(text, sbyte.MinValue, sbyte.MaxValue, suffix: string.Empty, cast: underlying.Keyword, out literal, out key);

            case UnderlyingKind.Byte:
                return TryInteger(text, byte.MinValue, byte.MaxValue, suffix: string.Empty, cast: underlying.Keyword, out literal, out key);

            case UnderlyingKind.Int16:
                return TryInteger(text, short.MinValue, short.MaxValue, suffix: string.Empty, cast: underlying.Keyword, out literal, out key);

            case UnderlyingKind.UInt16:
                return TryInteger(text, ushort.MinValue, ushort.MaxValue, suffix: string.Empty, cast: underlying.Keyword, out literal, out key);

            case UnderlyingKind.Int32:
                return TryInteger(text, int.MinValue, int.MaxValue, suffix: string.Empty, cast: underlying.Keyword, out literal, out key);

            case UnderlyingKind.UInt32:
                return TryInteger(text, uint.MinValue, uint.MaxValue, "U", cast: null, out literal, out key);

            case UnderlyingKind.Int64:
                return TryInteger(text, long.MinValue, long.MaxValue, "L", cast: null, out literal, out key);

            case UnderlyingKind.UInt64:
                return TryInteger(text, ulong.MinValue, ulong.MaxValue, "UL", cast: null, out literal, out key);

            case UnderlyingKind.Int128:
                return TryInteger128(text, underlying, Int128Minimum, Int128Maximum, out literal, out key);

            case UnderlyingKind.UInt128:
                return TryInteger128(text, underlying, BigInteger.Zero, UInt128Maximum, out literal, out key);

            case UnderlyingKind.Decimal:
                if (!IsNumber(text, exponent: false)
                    || !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var dec))
                {
                    return false;
                }

                literal = dec.ToString(CultureInfo.InvariantCulture) + "m";
                key = dec;
                return true;

            case UnderlyingKind.Double:
                // The form leaves out NaN and the infinities, which have no literal: written with its suffix, each
                // would be an identifier the compiler cannot find. Text past double.MaxValue reads as an infinity
                // on .NET and does not read at all on .NET Framework, where the compiler may run, so both results
                // are checked, without a short circuit. Text too small for the type reads as zero, which it does not
                // name any more than the infinity names text too large, so only text written as zero may read so.
                if (!IsNumber(text, exponent: true)
                    || !(double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var dbl) & !double.IsInfinity(dbl))
                    || (dbl == 0d && !IsWrittenAsZero(text)))
                {
                    return false;
                }

                literal = RoundTrip(dbl) + "d";
                key = dbl;
                return true;

            case UnderlyingKind.Single:
                if (!IsNumber(text, exponent: true)
                    || !(float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var flt) & !float.IsInfinity(flt))
                    || (flt == 0f && !IsWrittenAsZero(text)))
                {
                    return false;
                }

                literal = RoundTrip(flt) + "f";

                // Widened, which is exact and keeps the order: the generated code compares the float itself.
                key = (double)flt;
                return true;

            case UnderlyingKind.DateOnly:
                if (!TryReadDate(text, ref position, out var date) || position != text.Length)
                {
                    return false;
                }

                literal = $"new global::System.DateOnly({date.Year}, {date.Month}, {date.Day})";
                key = date.Ticks;
                return true;

            case UnderlyingKind.TimeOnly:
                if (!TryReadTimeOfDay(text, ref position, secondsRequired: false, out var timeOfDay) || position != text.Length)
                {
                    return false;
                }

                literal = $"new global::System.TimeOnly({timeOfDay}L)";
                key = timeOfDay;
                return true;

            case UnderlyingKind.DateTime:
                // A DateTime bound is a reading of the clock. Written with an offset or Z, it would have to be
                // converted to some time zone, and the only one at hand is the build machine's: an offset is text
                // past the end of the form.
                if (!TryReadDateAndTime(text, ref position, timeRequired: false, out var clock) || position != text.Length)
                {
                    return false;
                }

                literal = $"new global::System.DateTime({clock}L, global::System.DateTimeKind.Unspecified)";

                // A DateTime compares its clock reading, whatever its kind.
                key = clock;
                return true;

            case UnderlyingKind.DateTimeOffset:
                // Without an offset, the text would take the offset of the build machine. The instant the clock
                // reading and the offset name together must itself be a DateTime.
                if (!TryReadDateAndTime(text, ref position, timeRequired: true, out var local)
                    || !TryReadOffset(text, ref position, out var offset)
                    || position != text.Length
                    || local - offset < DateTime.MinValue.Ticks
                    || local - offset > DateTime.MaxValue.Ticks)
                {
                    return false;
                }

                literal = $"new global::System.DateTimeOffset({local}L, new global::System.TimeSpan({offset}L))";

                // A DateTimeOffset compares the instant it names, whatever its offset.
                key = local - offset;
                return true;

            case UnderlyingKind.TimeSpan:
                if (!TryReadTimeSpan(text, out var duration))
                {
                    return false;
                }

                literal = $"new global::System.TimeSpan({duration}L)";
                key = duration;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Quotes a string as a verbatim-free C# literal.
    /// </summary>
    /// <param name="value">Value to quote.</param>
    /// <returns>The literal expression.</returns>
    public static string Quote(string value) => $"\"{Escape(value)}\"";

    /// <summary>
    /// Writes an attribute argument as the text it is read from: a string as it is, any other constant in its
    /// invariant form.
    /// </summary>
    /// <param name="value">A constant read from an attribute argument.</param>
    /// <returns>Its text, or <see langword="null"/> when it has none.</returns>
    public static string? Text(object value) => value switch
    {
        string written => written,

        // The compiler may run on .NET Framework, as in Visual Studio, whose default form of a double keeps
        // 15 significant digits and of a float 7, and so names a neighbouring value. The round-trip form
        // names the value itself.
        double real => RoundTrip(real),
        float single => RoundTrip(single),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// Whether the parser the generated <c>Parse</c> calls in the invariant culture can read the text, in any of the
    /// forms it takes, not only the one form <see cref="TryCreate(UnderlyingType, object?, out string)"/> reads.
    /// </summary>
    /// <remarks>
    /// The generator targets netstandard2.0, which has neither <c>DateOnly</c> nor <c>TimeOnly</c>, so a date and a time
    /// of day are read as a <see cref="DateTime"/>, whose parser reads every form theirs do, and more: text it refuses
    /// they refuse too. A real too large for its type reads as an infinity on .NET and does not read at all on
    /// .NET Framework, where the compiler may run, so a number written as digits is readable whatever it holds.
    /// </remarks>
    /// <param name="underlying">Underlying type descriptor.</param>
    /// <param name="text">The text, as written.</param>
    /// <returns><see langword="false"/> when no form of the type is the text.</returns>
    public static bool IsReadable(UnderlyingType underlying, string text)
    {
        var invariant = CultureInfo.InvariantCulture;
        return underlying.Kind switch
        {
            UnderlyingKind.String => true,
            UnderlyingKind.Char => text.Length == 1,
            UnderlyingKind.Boolean => bool.TryParse(text, out _),
            UnderlyingKind.Guid => Guid.TryParse(text, out _),
            UnderlyingKind.SByte => IsIntegerWithin(text, sbyte.MinValue, sbyte.MaxValue),
            UnderlyingKind.Byte => IsIntegerWithin(text, byte.MinValue, byte.MaxValue),
            UnderlyingKind.Int16 => IsIntegerWithin(text, short.MinValue, short.MaxValue),
            UnderlyingKind.UInt16 => IsIntegerWithin(text, ushort.MinValue, ushort.MaxValue),
            UnderlyingKind.Int32 => IsIntegerWithin(text, int.MinValue, int.MaxValue),
            UnderlyingKind.UInt32 => IsIntegerWithin(text, uint.MinValue, uint.MaxValue),
            UnderlyingKind.Int64 => IsIntegerWithin(text, long.MinValue, long.MaxValue),
            UnderlyingKind.UInt64 => IsIntegerWithin(text, ulong.MinValue, ulong.MaxValue),
            UnderlyingKind.Int128 => IsIntegerWithin(text, Int128Minimum, Int128Maximum),
            UnderlyingKind.UInt128 => IsIntegerWithin(text, BigInteger.Zero, UInt128Maximum),
            UnderlyingKind.Decimal => decimal.TryParse(text, NumberStyles.Number & ~NumberStyles.AllowThousands, invariant, out _),
            UnderlyingKind.Double => double.TryParse(text, NumberStyles.Float, invariant, out _) || IsWrittenAsReal(text),
            UnderlyingKind.Single => float.TryParse(text, NumberStyles.Float, invariant, out _) || IsWrittenAsReal(text),
            UnderlyingKind.DateTimeOffset => DateTimeOffset.TryParse(text, invariant, DateTimeStyles.None, out _),
            UnderlyingKind.TimeSpan => TimeSpan.TryParse(text, invariant, out _),
            _ => DateTime.TryParse(text, invariant, DateTimeStyles.RoundtripKind, out _),
        };
    }

    /// <summary>
    /// Writes a double in a form that reads back as the same value, whatever runtime the compiler runs on.
    /// </summary>
    /// <param name="value">A finite value.</param>
    /// <returns>Its round-trip form, or seventeen significant digits where that form does not read back.</returns>
    internal static string RoundTrip(double value)
        => ReadsBack(value, value.ToString("R", CultureInfo.InvariantCulture));

    /// <summary>
    /// Writes a float in a form that reads back as the same value, whatever runtime the compiler runs on.
    /// </summary>
    /// <param name="value">A finite value.</param>
    /// <returns>Its round-trip form, or nine significant digits where that form does not read back.</returns>
    internal static string RoundTrip(float value)
        => ReadsBack(value, value.ToString("R", CultureInfo.InvariantCulture));

    /// <summary>
    /// Keeps the text of a double when it reads back as the value, and writes seventeen significant digits otherwise.
    /// </summary>
    /// <remarks>
    /// On .NET Framework, which the compiler runs on in Visual Studio, a value written in the round-trip form does
    /// not always read back as itself in a 64-bit process, as its documentation warns: the text can name a
    /// neighbouring value. Seventeen significant digits always name the value itself. On .NET the round-trip form is
    /// exact, and is kept for being the shorter.
    /// </remarks>
    /// <param name="value">A finite value.</param>
    /// <param name="text">Its text in the round-trip form.</param>
    /// <returns>Text that reads back as <paramref name="value"/>.</returns>
    internal static string ReadsBack(double value, string text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var read) && read == value
            ? text
            : value.ToString("G17", CultureInfo.InvariantCulture);

    /// <summary>
    /// Keeps the text of a float when it reads back as the value, and writes nine significant digits otherwise, which
    /// always name the value itself.
    /// </summary>
    /// <param name="value">A finite value.</param>
    /// <param name="text">Its text in the round-trip form.</param>
    /// <returns>Text that reads back as <paramref name="value"/>.</returns>
    internal static string ReadsBack(float value, string text)
        => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var read) && read == value
            ? text
            : value.ToString("G9", CultureInfo.InvariantCulture);

    /// <summary>
    /// Whether every digit before the exponent is a zero: a number written as zero, rather than one too small for
    /// its type, which reads as zero too.
    /// </summary>
    private static bool IsWrittenAsZero(string text)
    {
        foreach (var character in text)
        {
            if (character is 'e' or 'E')
            {
                break;
            }

            if (character is >= '1' and <= '9')
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSurroundingWhiteSpace(string text)
        => text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[text.Length - 1]));

    /// <summary>
    /// Whether the text is an integer written as digits, with a leading <c>-</c> when the type is signed.
    /// </summary>
    private static bool IsInteger(string text, bool signed)
    {
        var position = 0;
        if (signed)
        {
            _ = TryRead(text, ref position, '-');
        }

        return SkipDigits(text, ref position) && position == text.Length;
    }

    /// <summary>
    /// Whether the text is a number written as digits, with an optional leading <c>-</c>, an optional fraction
    /// after a <c>.</c> and, when the type takes one, an optional exponent.
    /// </summary>
    private static bool IsNumber(string text, bool exponent)
    {
        var position = 0;
        _ = TryRead(text, ref position, '-');
        if (!SkipDigits(text, ref position) || (TryRead(text, ref position, '.') && !SkipDigits(text, ref position)))
        {
            return false;
        }

        if (exponent && (TryRead(text, ref position, 'e') || TryRead(text, ref position, 'E')))
        {
            _ = TryRead(text, ref position, '+') || TryRead(text, ref position, '-');
            if (!SkipDigits(text, ref position))
            {
                return false;
            }
        }

        return position == text.Length;
    }

    /// <summary>
    /// Reads <c>yyyy-MM-dd</c>, a date that exists.
    /// </summary>
    private static bool TryReadDate(string text, ref int position, out DateTime date)
    {
        date = default;
        if (!TryReadNumber(text, ref position, 4, 9999, out var year)
            || !TryRead(text, ref position, '-')
            || !TryReadNumber(text, ref position, 2, 12, out var month)
            || !TryRead(text, ref position, '-')
            || !TryReadNumber(text, ref position, 2, 31, out var day)
            || year == 0
            || month == 0
            || day == 0
            || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        date = new DateTime(year, month, day);
        return true;
    }

    /// <summary>
    /// Reads <c>HH:mm</c>, <c>HH:mm:ss</c> or <c>HH:mm:ss.f</c> with one to seven digits of fraction, as ticks.
    /// </summary>
    private static bool TryReadTimeOfDay(string text, ref int position, bool secondsRequired, out long ticks)
    {
        ticks = 0;
        if (!TryReadNumber(text, ref position, 2, 23, out var hours)
            || !TryRead(text, ref position, ':')
            || !TryReadNumber(text, ref position, 2, 59, out var minutes))
        {
            return false;
        }

        ticks = (hours * TimeSpan.TicksPerHour) + (minutes * TimeSpan.TicksPerMinute);
        if (!TryRead(text, ref position, ':'))
        {
            return !secondsRequired;
        }

        if (!TryReadNumber(text, ref position, 2, 59, out var seconds))
        {
            return false;
        }

        ticks += seconds * TimeSpan.TicksPerSecond;
        if (!TryRead(text, ref position, '.'))
        {
            return true;
        }

        // Seven digits are a tick each; an eighth is left unread, past the end of the form.
        var start = position;
        var fraction = 0L;
        while (position < text.Length && position - start < 7 && IsDigit(text[position]))
        {
            fraction = (fraction * 10) + (text[position++] - '0');
        }

        for (var digits = position - start; digits < 7; digits++)
        {
            fraction *= 10;
        }

        ticks += fraction;
        return position > start;
    }

    /// <summary>
    /// Reads <c>yyyy-MM-dd</c>, followed by <c>T</c> and a time of day when one is written or required, as the
    /// ticks of that clock reading.
    /// </summary>
    private static bool TryReadDateAndTime(string text, ref int position, bool timeRequired, out long ticks)
    {
        ticks = 0;
        if (!TryReadDate(text, ref position, out var date))
        {
            return false;
        }

        if (!TryRead(text, ref position, 'T'))
        {
            ticks = date.Ticks;
            return !timeRequired;
        }

        if (!TryReadTimeOfDay(text, ref position, secondsRequired: false, out var time))
        {
            return false;
        }

        ticks = date.Ticks + time;
        return true;
    }

    /// <summary>
    /// Reads <c>Z</c>, <c>+HH:mm</c> or <c>-HH:mm</c>, at most fourteen hours either way, as ticks.
    /// </summary>
    private static bool TryReadOffset(string text, ref int position, out long ticks)
    {
        ticks = 0;
        if (TryRead(text, ref position, 'Z'))
        {
            return true;
        }

        var negative = TryRead(text, ref position, '-');
        if ((!negative && !TryRead(text, ref position, '+'))
            || !TryReadNumber(text, ref position, 2, 14, out var hours)
            || !TryRead(text, ref position, ':')
            || !TryReadNumber(text, ref position, 2, 59, out var minutes)
            || (hours * 60) + minutes > 14 * 60)
        {
            return false;
        }

        ticks = (hours * TimeSpan.TicksPerHour) + (minutes * TimeSpan.TicksPerMinute);
        ticks = negative ? -ticks : ticks;
        return true;
    }

    /// <summary>
    /// Reads the invariant constant form <c>[-][d.]hh:mm:ss[.fffffff]</c> of a duration, as ticks.
    /// </summary>
    private static bool TryReadTimeSpan(string text, out long ticks)
    {
        ticks = 0;
        var position = 0;
        var negative = TryRead(text, ref position, '-');

        // Digits followed by '.' are the days; followed by ':' they are the hours, read with the time of day.
        var start = position;
        var days = BigInteger.Zero;
        if (SkipDigits(text, ref position) && TryRead(text, ref position, '.'))
        {
            days = BigInteger.Parse(text.Substring(start, position - 1 - start), NumberStyles.None, CultureInfo.InvariantCulture);
        }
        else
        {
            position = start;
        }

        if (!TryReadTimeOfDay(text, ref position, secondsRequired: true, out var time) || position != text.Length)
        {
            return false;
        }

        var total = (days * TimeSpan.TicksPerDay) + time;
        total = negative ? -total : total;
        if (total < long.MinValue || total > long.MaxValue)
        {
            return false;
        }

        ticks = (long)total;
        return true;
    }

    /// <summary>
    /// Reads exactly <paramref name="digits"/> decimal digits, holding a number no greater than
    /// <paramref name="maximum"/>.
    /// </summary>
    private static bool TryReadNumber(string text, ref int position, int digits, int maximum, out int value)
    {
        value = 0;
        if (text.Length - position < digits)
        {
            return false;
        }

        for (var i = 0; i < digits; i++)
        {
            var character = text[position + i];
            if (!IsDigit(character))
            {
                return false;
            }

            value = (value * 10) + (character - '0');
        }

        position += digits;
        return value <= maximum;
    }

    private static bool TryRead(string text, ref int position, char expected)
    {
        if (position < text.Length && text[position] == expected)
        {
            position++;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Skips a run of decimal digits, reporting whether there was at least one.
    /// </summary>
    private static bool SkipDigits(string text, ref int position)
    {
        var start = position;
        while (position < text.Length && IsDigit(text[position]))
        {
            position++;
        }

        return position > start;
    }

    // Only ASCII digits: char.IsDigit also accepts the digits of every other script.
    private static bool IsDigit(char character) => character is >= '0' and <= '9';

    /// <summary>
    /// Builds the expression of a 128-bit integer, which C# has no literal for, refusing a value outside the range
    /// of the underlying type.
    /// </summary>
    /// <remarks>
    /// The value is constructed from its two 64-bit halves, in two's complement for a negative one, the way the
    /// type stores it: a bound is evaluated on every call to <c>Validate</c>, where parsing text each time would
    /// cost what the comparison it feeds does not.
    /// </remarks>
    private static bool TryInteger128(
        string text,
        UnderlyingType underlying,
        BigInteger minimum,
        BigInteger maximum,
        out string literal,
        out IComparable key)
    {
        literal = string.Empty;
        key = string.Empty;
        if (!TryReadInteger(text, minimum, maximum, out var number))
        {
            return false;
        }

        key = number;

        var bits = number.Sign < 0 ? number + (BigInteger.One << 128) : number;
        var upper = (ulong)(bits >> 64);
        var lower = (ulong)(bits & ulong.MaxValue);

        literal = string.Format(CultureInfo.InvariantCulture, "new {0}({1}UL, {2}UL)", underlying.FullName, upper, lower);
        return true;
    }

    /// <summary>
    /// Builds an integer literal, refusing a value outside the range of the underlying type.
    /// </summary>
    /// <remarks>
    /// Parsing as some wider integer is not enough: <c>"300"</c> for a <c>byte</c> or <c>"-1"</c> for a
    /// <c>ulong</c> would become a literal the compiler rejects, inside a file the author cannot edit. The
    /// literal is written from the parsed number rather than from the text, so a leading zero the author wrote
    /// cannot change how the compiler reads it either.
    /// </remarks>
    private static bool TryInteger(
        string text,
        BigInteger minimum,
        BigInteger maximum,
        string suffix,
        string? cast,
        out string literal,
        out IComparable key)
    {
        literal = string.Empty;
        key = string.Empty;
        if (!TryReadInteger(text, minimum, maximum, out var number))
        {
            return false;
        }

        key = number;

        var digits = number.ToString(CultureInfo.InvariantCulture);

        // A cast keeps the literal well typed for the narrow integer types, which have no literal suffix.
        literal = cast is null ? digits + suffix : $"({cast})({digits})";
        return true;
    }

    /// <summary>
    /// Reads an integer written as digits, with a leading <c>-</c> when the type is signed, within the range of the
    /// type.
    /// </summary>
    private static bool TryReadInteger(string text, BigInteger minimum, BigInteger maximum, out BigInteger number)
    {
        number = BigInteger.Zero;
        if (!IsInteger(text, signed: minimum < 0))
        {
            return false;
        }

        number = BigInteger.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        return number >= minimum && number <= maximum;
    }

    /// <summary>
    /// Whether the text is an integer in the form the integer types read in the invariant culture, white space and a
    /// sign included, within the range of the type.
    /// </summary>
    private static bool IsIntegerWithin(string text, BigInteger minimum, BigInteger maximum)
        => BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
           && number >= minimum
           && number <= maximum;

    /// <summary>
    /// Whether the text is a real written as digits, white space and a leading sign included, whatever it holds: one
    /// too large for its type does not read at all on .NET Framework, where it reads as an infinity on .NET.
    /// </summary>
    private static bool IsWrittenAsReal(string text)
    {
        var trimmed = text.Trim();
        var unsigned = trimmed.StartsWith("+", StringComparison.Ordinal) || trimmed.StartsWith("-", StringComparison.Ordinal)
            ? trimmed.Substring(1)
            : trimmed;

        return !unsigned.StartsWith("-", StringComparison.Ordinal) && IsNumber(unsigned, exponent: true);
    }

    private static string Escape(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\'':
                    builder.Append("\\'");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\0':
                    builder.Append("\\0");
                    break;
                case >= '\uD800' and <= '\uDBFF' when i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]):
                    // A whole pair stands for one character, which the UTF-8 the file is written in holds.
                    builder.Append(character).Append(value[++i]);
                    break;
                default:
                    // C# also ends a line at U+0085, U+2028 and U+2029, which a regular literal cannot hold raw, and
                    // half of a surrogate pair has no UTF-8 form: written raw, it would reach the file as U+FFFD.
                    if (character < ' ' || character is '\u0085' or '\u2028' or '\u2029' || char.IsSurrogate(character))
                    {
                        builder.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.ToString();
    }
}
