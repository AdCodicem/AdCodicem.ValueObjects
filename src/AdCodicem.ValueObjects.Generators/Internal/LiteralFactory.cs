using System;
using System.Globalization;
using System.Numerics;
using AdCodicem.ValueObjects.Generators.Model;

namespace AdCodicem.ValueObjects.Generators.Internal;

/// <summary>
/// Turns attribute arguments into C# literal expressions of the underlying type.
/// </summary>
/// <remarks>
/// Attribute arguments can only carry a handful of constant types, so bounds and known values of types such as
/// <c>Guid</c>, <c>decimal</c> or <c>DateOnly</c> are written as text. They are parsed here, at compile time,
/// which means a malformed bound is a build error rather than a start-up exception.
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
    {
        literal = string.Empty;
        if (value is null)
        {
            return false;
        }

        var text = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
        if (text is null)
        {
            return false;
        }

        switch (underlying.Kind)
        {
            case UnderlyingKind.String:
                literal = Quote(text);
                return true;

            case UnderlyingKind.Guid:
                if (!Guid.TryParse(text, out var guid))
                {
                    return false;
                }

                literal = $"new global::System.Guid({Quote(guid.ToString("D", CultureInfo.InvariantCulture))})";
                return true;

            case UnderlyingKind.Boolean:
                if (!bool.TryParse(text, out var boolean))
                {
                    return false;
                }

                literal = boolean ? "true" : "false";
                return true;

            case UnderlyingKind.Char:
                if (text.Length != 1)
                {
                    return false;
                }

                literal = $"'{Escape(text)}'";
                return true;

            case UnderlyingKind.SByte:
                return TryInteger(text, sbyte.MinValue, sbyte.MaxValue, suffix: string.Empty, cast: underlying.Keyword, out literal);

            case UnderlyingKind.Byte:
                return TryInteger(text, byte.MinValue, byte.MaxValue, suffix: string.Empty, cast: underlying.Keyword, out literal);

            case UnderlyingKind.Int16:
                return TryInteger(text, short.MinValue, short.MaxValue, suffix: string.Empty, cast: underlying.Keyword, out literal);

            case UnderlyingKind.UInt16:
                return TryInteger(text, ushort.MinValue, ushort.MaxValue, suffix: string.Empty, cast: underlying.Keyword, out literal);

            case UnderlyingKind.Int32:
                return TryInteger(text, int.MinValue, int.MaxValue, suffix: string.Empty, cast: underlying.Keyword, out literal);

            case UnderlyingKind.UInt32:
                return TryInteger(text, uint.MinValue, uint.MaxValue, "U", cast: null, out literal);

            case UnderlyingKind.Int64:
                return TryInteger(text, long.MinValue, long.MaxValue, "L", cast: null, out literal);

            case UnderlyingKind.UInt64:
                return TryInteger(text, ulong.MinValue, ulong.MaxValue, "UL", cast: null, out literal);

            case UnderlyingKind.Int128:
                return TryInteger128(text, underlying, Int128Minimum, Int128Maximum, out literal);

            case UnderlyingKind.UInt128:
                return TryInteger128(text, underlying, BigInteger.Zero, UInt128Maximum, out literal);

            case UnderlyingKind.Decimal:
                if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var dec))
                {
                    return false;
                }

                literal = dec.ToString(CultureInfo.InvariantCulture) + "m";
                return true;

            case UnderlyingKind.Double:
                // NaN and the infinities parse, and so does text past double.MaxValue, as an infinity. None of
                // them has a literal: written with its suffix, each would be an identifier the compiler cannot find.
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var dbl)
                    || double.IsNaN(dbl)
                    || double.IsInfinity(dbl))
                {
                    return false;
                }

                literal = dbl.ToString("R", CultureInfo.InvariantCulture) + "d";
                return true;

            case UnderlyingKind.Single:
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var flt)
                    || float.IsNaN(flt)
                    || float.IsInfinity(flt))
                {
                    return false;
                }

                literal = flt.ToString("R", CultureInfo.InvariantCulture) + "f";
                return true;

            case UnderlyingKind.DateOnly:
                if (!DateTime.TryParseExact(text, ["yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
                {
                    return false;
                }

                literal = $"new global::System.DateOnly({dateOnly.Year}, {dateOnly.Month}, {dateOnly.Day})";
                return true;

            case UnderlyingKind.TimeOnly:
                if (!TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var timeOnly) || timeOnly < TimeSpan.Zero || timeOnly.Days > 0)
                {
                    return false;
                }

                literal = $"new global::System.TimeOnly({timeOnly.Ticks}L)";
                return true;

            case UnderlyingKind.DateTime:
                // A DateTime bound is a reading of the clock. Written with an offset or Z, it would have to be
                // converted to some time zone, and the only one at hand is the build machine's.
                if (!TryParseDateTime(text, out var dateTime) || dateTime.Kind != DateTimeKind.Unspecified)
                {
                    return false;
                }

                literal = $"new global::System.DateTime({dateTime.Ticks}L, global::System.DateTimeKind.Unspecified)";
                return true;

            case UnderlyingKind.DateTimeOffset:
                // Without an offset, the text would take the offset of the build machine.
                if (!TryParseDateTime(text, out var instant)
                    || instant.Kind != DateTimeKind.Utc
                    || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTimeOffset))
                {
                    return false;
                }

                literal = $"new global::System.DateTimeOffset({dateTimeOffset.Ticks}L, new global::System.TimeSpan({dateTimeOffset.Offset.Ticks}L))";
                return true;

            case UnderlyingKind.TimeSpan:
                if (!TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var timeSpan))
                {
                    return false;
                }

                literal = $"new global::System.TimeSpan({timeSpan.Ticks}L)";
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
    /// Reads a date and time without involving the time zone of the machine running the compiler.
    /// </summary>
    /// <remarks>
    /// A text that names its offset — an explicit one, <c>Z</c> or <c>GMT</c> — is converted to UTC by that
    /// offset alone and comes back with <see cref="DateTimeKind.Utc"/>. Any other text comes back as written,
    /// with <see cref="DateTimeKind.Unspecified"/>. The kind therefore says whether the text carried an offset.
    /// </remarks>
    private static bool TryParseDateTime(string text, out DateTime value)
        => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out value);

    /// <summary>
    /// Builds the parse expression of a 128-bit integer, which C# has no literal for, refusing a value outside
    /// the range of the underlying type.
    /// </summary>
    /// <remarks>
    /// The range is checked here because the expression is evaluated by the generated code: a value it cannot
    /// hold would throw an <see cref="OverflowException"/> out of <c>Create</c> and <c>TryCreate</c>.
    /// </remarks>
    private static bool TryInteger128(
        string text,
        UnderlyingType underlying,
        BigInteger minimum,
        BigInteger maximum,
        out string literal)
    {
        if (!TryInteger(text, minimum, maximum, suffix: string.Empty, cast: null, out var digits))
        {
            literal = string.Empty;
            return false;
        }

        literal = $"{underlying.FullName}.Parse({Quote(digits)}, global::System.Globalization.CultureInfo.InvariantCulture)";
        return true;
    }

    /// <summary>
    /// Builds an integer literal, refusing a value outside the range of the underlying type.
    /// </summary>
    /// <remarks>
    /// Parsing as some wider integer is not enough: <c>"300"</c> for a <c>byte</c> or <c>"-1"</c> for a
    /// <c>ulong</c> would become a literal the compiler rejects, inside a file the author cannot edit. The
    /// literal is written from the parsed number rather than from the text, so a sign or a leading zero the
    /// author wrote cannot change how the compiler reads it either.
    /// </remarks>
    private static bool TryInteger(
        string text,
        BigInteger minimum,
        BigInteger maximum,
        string suffix,
        string? cast,
        out string literal)
    {
        literal = string.Empty;
        if (!BigInteger.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            || number < minimum
            || number > maximum)
        {
            return false;
        }

        var digits = number.ToString(CultureInfo.InvariantCulture);

        // A cast keeps the literal well typed for the narrow integer types, which have no literal suffix.
        literal = cast is null ? digits + suffix : $"({cast})({digits})";
        return true;
    }

    private static string Escape(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 8);
        foreach (var character in value)
        {
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
                default:
                    // C# also ends a line at U+0085, U+2028 and U+2029, which a regular literal cannot hold raw.
                    if (character < ' ' || character is '\u0085' or '\u2028' or '\u2029')
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
