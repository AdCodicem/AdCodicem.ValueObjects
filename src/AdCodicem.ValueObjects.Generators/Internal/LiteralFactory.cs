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

            case UnderlyingKind.Int128 or UnderlyingKind.UInt128:
                literal = $"{underlying.FullName}.Parse({Quote(text)}, global::System.Globalization.CultureInfo.InvariantCulture)";
                return decimal.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

            case UnderlyingKind.Decimal:
                if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var dec))
                {
                    return false;
                }

                literal = dec.ToString(CultureInfo.InvariantCulture) + "m";
                return true;

            case UnderlyingKind.Double:
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var dbl))
                {
                    return false;
                }

                literal = dbl.ToString("R", CultureInfo.InvariantCulture) + "d";
                return true;

            case UnderlyingKind.Single:
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var flt))
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
                if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime))
                {
                    return false;
                }

                literal = $"new global::System.DateTime({dateTime.Ticks}L, global::System.DateTimeKind.{dateTime.Kind})";
                return true;

            case UnderlyingKind.DateTimeOffset:
                if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTimeOffset))
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
                    if (character < ' ')
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
