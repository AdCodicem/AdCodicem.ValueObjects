using System.Globalization;
using System.Xml;

namespace AdCodicem.ValueObjects.Metadata;

/// <summary>
/// The lexical form of each underlying type in XML: the one <c>XmlSerializer</c> writes and reads a member of that type
/// in, and the XSD type it stands for.
/// </summary>
internal static class XmlLexical
{
    private const DateTimeStyles White = DateTimeStyles.AllowLeadingWhite | DateTimeStyles.AllowTrailingWhite;

    /// <summary>Gets the name of the built-in XSD type the underlying type is written as.</summary>
    internal static string BaseType<TValue>()
    {
        if (typeof(TValue) == typeof(string) || typeof(TValue) == typeof(Guid))
        {
            return "string";
        }

        if (typeof(TValue) == typeof(bool))
        {
            return "boolean";
        }

        if (typeof(TValue) == typeof(char) || typeof(TValue) == typeof(ushort))
        {
            return "unsignedShort";
        }

        if (typeof(TValue) == typeof(sbyte))
        {
            return "byte";
        }

        if (typeof(TValue) == typeof(byte))
        {
            return "unsignedByte";
        }

        if (typeof(TValue) == typeof(short))
        {
            return "short";
        }

        if (typeof(TValue) == typeof(int))
        {
            return "int";
        }

        if (typeof(TValue) == typeof(uint))
        {
            return "unsignedInt";
        }

        if (typeof(TValue) == typeof(long))
        {
            return "long";
        }

        if (typeof(TValue) == typeof(ulong))
        {
            return "unsignedLong";
        }

        if (typeof(TValue) == typeof(Int128) || typeof(TValue) == typeof(UInt128))
        {
            return "integer";
        }

        if (typeof(TValue) == typeof(decimal))
        {
            return "decimal";
        }

        if (typeof(TValue) == typeof(double))
        {
            return "double";
        }

        if (typeof(TValue) == typeof(float))
        {
            return "float";
        }

        if (typeof(TValue) == typeof(DateOnly))
        {
            return "date";
        }

        if (typeof(TValue) == typeof(TimeOnly))
        {
            return "time";
        }

        if (typeof(TValue) == typeof(DateTime) || typeof(TValue) == typeof(DateTimeOffset))
        {
            return "dateTime";
        }

        return typeof(TValue) == typeof(TimeSpan) ? "duration" : throw Unsupported<TValue>();
    }

    /// <summary>
    /// Gets what text of the underlying type is, for a message: its XSD type, or a GUID, which <c>xs:string</c> would not
    /// tell apart from any text.
    /// </summary>
    internal static string Lexical<TValue>() => typeof(TValue) == typeof(Guid) ? "GUID" : "xs:" + BaseType<TValue>();

    /// <summary>Writes a value in the lexical form <c>XmlSerializer</c> writes its type in.</summary>
    internal static string Format<TValue>(TValue value) => value switch
    {
        string text => text,
        bool flag => XmlConvert.ToString(flag),
        char character => XmlConvert.ToString((ushort)character),
        sbyte number => XmlConvert.ToString(number),
        byte number => XmlConvert.ToString(number),
        short number => XmlConvert.ToString(number),
        ushort number => XmlConvert.ToString(number),
        int number => XmlConvert.ToString(number),
        uint number => XmlConvert.ToString(number),
        long number => XmlConvert.ToString(number),
        ulong number => XmlConvert.ToString(number),
        Int128 number => number.ToString(CultureInfo.InvariantCulture),
        UInt128 number => number.ToString(CultureInfo.InvariantCulture),
        decimal number => XmlConvert.ToString(number),
        double number => XmlConvert.ToString(number),
        float number => XmlConvert.ToString(number),
        Guid identifier => XmlConvert.ToString(identifier),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
        DateTime instant => XmlConvert.ToString(instant, XmlDateTimeSerializationMode.RoundtripKind),
        DateTimeOffset instant => XmlConvert.ToString(instant),
        TimeSpan duration => XmlConvert.ToString(duration),
        _ => throw Unsupported<TValue>(),
    };

    /// <summary>
    /// Reads a value in the lexical form <c>XmlSerializer</c> reads its type in, without the exception
    /// <see cref="XmlConvert"/> throws, whose message quotes the text.
    /// </summary>
    internal static bool TryParse<TValue>(string text, out TValue? value)
    {
        try
        {
            if (typeof(TValue) == typeof(string))
            {
                value = (TValue)(object)text;
                return true;
            }

            if (typeof(TValue) == typeof(DateOnly))
            {
                var parsed = DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, White, out var date);
                value = (TValue)(object)date;
                return parsed;
            }

            if (typeof(TValue) == typeof(TimeOnly))
            {
                var parsed = TimeOnly.TryParseExact(text, "HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture, White, out var time);
                value = (TValue)(object)time;
                return parsed;
            }

            if (typeof(TValue) == typeof(Int128))
            {
                var parsed = Int128.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number);
                value = (TValue)(object)number;
                return parsed;
            }

            if (typeof(TValue) == typeof(UInt128))
            {
                var parsed = UInt128.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number);
                value = (TValue)(object)number;
                return parsed;
            }

            value = (TValue)Convert<TValue>(text);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            value = default;
            return false;
        }
    }

    /// <summary>
    /// Reads a bound as <see cref="ValueObjectBound.Text{TValue}"/> writes it: the invariant text of the type, the
    /// round-trip form of a date or a time, the constant form of a duration and the character itself.
    /// </summary>
    internal static bool TryParseInvariant<TValue>(string text, out TValue? value)
    {
        var invariant = CultureInfo.InvariantCulture;
        (bool Read, object? Value) parsed =
            typeof(TValue) == typeof(char) ? (char.TryParse(text, out var character), character)
            : typeof(TValue) == typeof(TimeSpan) ? (TimeSpan.TryParseExact(text, "c", invariant, out var duration), duration)
            : typeof(TValue) == typeof(DateTime) ? (DateTime.TryParse(text, invariant, DateTimeStyles.RoundtripKind, out var instant), instant)
            : typeof(TValue) == typeof(DateTimeOffset) ? (DateTimeOffset.TryParse(text, invariant, DateTimeStyles.RoundtripKind, out var offset), offset)
            : typeof(TValue) == typeof(DateOnly) ? (DateOnly.TryParse(text, invariant, out var date), date)
            : typeof(TValue) == typeof(TimeOnly) ? (TimeOnly.TryParse(text, invariant, out var time), time)
            : typeof(TValue) == typeof(double) ? (double.TryParse(text, NumberStyles.Float, invariant, out var real), real)
            : typeof(TValue) == typeof(float) ? (float.TryParse(text, NumberStyles.Float, invariant, out var single), single)
            : (TryParse(text, out TValue? lexical), lexical);

        value = parsed.Read ? (TValue?)parsed.Value : default;
        return parsed.Read;
    }

    private static object Convert<TValue>(string text)
    {
        if (typeof(TValue) == typeof(bool))
        {
            return XmlConvert.ToBoolean(text);
        }

        if (typeof(TValue) == typeof(char))
        {
            return (char)XmlConvert.ToUInt16(text);
        }

        if (typeof(TValue) == typeof(sbyte))
        {
            return XmlConvert.ToSByte(text);
        }

        if (typeof(TValue) == typeof(byte))
        {
            return XmlConvert.ToByte(text);
        }

        if (typeof(TValue) == typeof(short))
        {
            return XmlConvert.ToInt16(text);
        }

        if (typeof(TValue) == typeof(ushort))
        {
            return XmlConvert.ToUInt16(text);
        }

        if (typeof(TValue) == typeof(int))
        {
            return XmlConvert.ToInt32(text);
        }

        if (typeof(TValue) == typeof(uint))
        {
            return XmlConvert.ToUInt32(text);
        }

        if (typeof(TValue) == typeof(long))
        {
            return XmlConvert.ToInt64(text);
        }

        if (typeof(TValue) == typeof(ulong))
        {
            return XmlConvert.ToUInt64(text);
        }

        if (typeof(TValue) == typeof(decimal))
        {
            return XmlConvert.ToDecimal(text);
        }

        if (typeof(TValue) == typeof(double))
        {
            return XmlConvert.ToDouble(text);
        }

        if (typeof(TValue) == typeof(float))
        {
            return XmlConvert.ToSingle(text);
        }

        if (typeof(TValue) == typeof(Guid))
        {
            return XmlConvert.ToGuid(text);
        }

        if (typeof(TValue) == typeof(DateTime))
        {
            return XmlConvert.ToDateTime(text, XmlDateTimeSerializationMode.RoundtripKind);
        }

        if (typeof(TValue) == typeof(DateTimeOffset))
        {
            return XmlConvert.ToDateTimeOffset(text);
        }

        return typeof(TValue) == typeof(TimeSpan) ? XmlConvert.ToTimeSpan(text) : throw Unsupported<TValue>();
    }

    private static NotSupportedException Unsupported<TValue>()
        => new($"'{typeof(TValue)}' is not an underlying type a value object is written in XML as.");
}
