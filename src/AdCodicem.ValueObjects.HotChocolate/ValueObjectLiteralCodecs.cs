using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using HotChocolate.Buffers;
using HotChocolate.Language;
using HotChocolate.Text.Json;
using HotChocolate.Types;

namespace AdCodicem.ValueObjects.HotChocolate;

/// <summary>
/// Reads and writes an underlying value GraphQL holds as a Boolean, an Int or a Float: from a literal, from the JSON value
/// of a variable, and into a result.
/// </summary>
/// <typeparam name="TValue">The underlying type.</typeparam>
/// <remarks>
/// The codec of each underlying type is chosen once, by <c>typeof(TValue)</c>, among instantiations the compiler sees,
/// never closed at run time. Every other underlying type, text, a character, a <see cref="Guid"/>, a 128-bit integer, a
/// date, a time, and any type a value object written by hand wraps, has none: it is read and written as text.
/// </remarks>
internal abstract class LiteralCodec<TValue>
{
    /// <summary>
    /// Gets the codec of <typeparamref name="TValue"/>, or <see langword="null"/> for a value read and written as text.
    /// </summary>
    public static LiteralCodec<TValue>? Instance { get; } = Create();

    /// <summary>
    /// Gets the kind of GraphQL value the underlying type is serialized to.
    /// </summary>
    public abstract ScalarSerializationType Kind { get; }

    /// <summary>
    /// Reads a literal of the codec's kind.
    /// </summary>
    /// <param name="literal">The literal.</param>
    /// <param name="value">The value read.</param>
    /// <param name="read">A refusal with <see cref="ValueObjectErrorCodes.NotParsable"/> for a number the type cannot hold.</param>
    /// <returns><see langword="false"/> for a literal of another kind.</returns>
    public abstract bool TryRead(IValueNode literal, out TValue value, out ValidationResult read);

    /// <summary>
    /// Reads the JSON value of a variable of the codec's kind.
    /// </summary>
    /// <param name="element">The JSON value.</param>
    /// <param name="value">The value read.</param>
    /// <param name="read">A refusal with <see cref="ValueObjectErrorCodes.NotParsable"/> for a number the type cannot hold.</param>
    /// <returns><see langword="false"/> for a JSON value of another kind.</returns>
    public abstract bool TryRead(JsonElement element, out TValue value, out ValidationResult read);

    /// <summary>
    /// Tells whether a GraphQL value of the codec's kind holds a value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A refusal for a value no GraphQL value of the kind holds, a real that is not finite.</returns>
    public virtual ValidationResult Writable(TValue value) => default;

    /// <summary>
    /// Writes a value <see cref="Writable"/> accepts into a result.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="result">The result element.</param>
    public abstract void Write(TValue value, ResultElement result);

    /// <summary>
    /// Writes a value <see cref="Writable"/> accepts as a literal, of the text <see cref="Write"/> writes.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The literal.</returns>
    public abstract IValueNode ToLiteral(TValue value);

    private static LiteralCodec<TValue>? Create()
    {
        var type = typeof(TValue);
        object? codec =
            type == typeof(bool) ? new BooleanCodec()
            : type == typeof(sbyte) ? new IntegerCodec<sbyte>("sbyte")
            : type == typeof(byte) ? new IntegerCodec<byte>("byte")
            : type == typeof(short) ? new IntegerCodec<short>("short")
            : type == typeof(ushort) ? new IntegerCodec<ushort>("ushort")
            : type == typeof(int) ? new IntegerCodec<int>("int")
            : type == typeof(uint) ? new IntegerCodec<uint>("uint")
            : type == typeof(long) ? new IntegerCodec<long>("long")
            : type == typeof(ulong) ? new IntegerCodec<ulong>("ulong")
            : type == typeof(float) ? new RealCodec<float>("float")
            : type == typeof(double) ? new RealCodec<double>("double")
            : type == typeof(decimal) ? new RealCodec<decimal>("decimal")
            : null;
        return (LiteralCodec<TValue>?)codec;
    }
}

/// <summary>
/// A <see cref="bool"/>, as a GraphQL Boolean.
/// </summary>
internal sealed class BooleanCodec : LiteralCodec<bool>
{
    /// <inheritdoc />
    public override ScalarSerializationType Kind => ScalarSerializationType.Boolean;

    /// <inheritdoc />
    public override bool TryRead(IValueNode literal, out bool value, out ValidationResult read)
    {
        read = default;
        value = literal is BooleanValueNode { Value: true };
        return literal is BooleanValueNode;
    }

    /// <inheritdoc />
    public override bool TryRead(JsonElement element, out bool value, out ValidationResult read)
    {
        read = default;
        value = element.ValueKind == JsonValueKind.True;
        return element.ValueKind is JsonValueKind.True or JsonValueKind.False;
    }

    /// <inheritdoc />
    public override void Write(bool value, ResultElement result) => result.SetBooleanValue(value);

    /// <inheritdoc />
    public override IValueNode ToLiteral(bool value) => new BooleanValueNode(value);
}

/// <summary>
/// An integer of 64 bits at most, as a GraphQL Int, read exactly as the underlying type.
/// </summary>
/// <typeparam name="T">The integer type.</typeparam>
/// <param name="keyword">The C# keyword of the type, quoted when a number does not fit it.</param>
/// <remarks>
/// GraphQL's own Int holds 32 bits; a value object over a wider type takes every number its type holds, as the JSON its
/// converter writes does: <c>18446744073709551615</c> for <see cref="ulong.MaxValue"/>.
/// </remarks>
internal sealed class IntegerCodec<T>(string keyword) : LiteralCodec<T>
    where T : struct, IBinaryInteger<T>, IUtf8SpanParsable<T>, IUtf8SpanFormattable
{
    /// <inheritdoc />
    public override ScalarSerializationType Kind => ScalarSerializationType.Int;

    /// <inheritdoc />
    public override bool TryRead(IValueNode literal, out T value, out ValidationResult read)
    {
        if (literal is not IntValueNode number)
        {
            (value, read) = (default!, default);
            return false;
        }

        read = Read(number.AsSpan(), out value);
        return true;
    }

    /// <inheritdoc />
    public override bool TryRead(JsonElement element, out T value, out ValidationResult read)
    {
        if (element.ValueKind != JsonValueKind.Number)
        {
            (value, read) = (default!, default);
            return false;
        }

        read = Read(JsonMarshal.GetRawUtf8Value(element), out value);
        return true;
    }

    /// <inheritdoc />
    public override void Write(T value, ResultElement result)
    {
        Span<byte> buffer = stackalloc byte[MaxLength];
        result.SetNumberValue(Format(value, buffer));
    }

    /// <inheritdoc />
    public override IValueNode ToLiteral(T value)
    {
        Span<byte> buffer = stackalloc byte[MaxLength];
        return new IntValueNode(new ReadOnlyMemorySegment(Format(value, buffer).ToArray()));
    }

    private ValidationResult Read(ReadOnlySpan<byte> text, out T value)
        => T.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value!)
            ? default
            : ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, $"The number is not a valid {keyword}.");

    /// <summary>
    /// The length of the longest integer of 64 bits at most, <c>-9223372036854775808</c>, and some.
    /// </summary>
    private const int MaxLength = 24;

    private static ReadOnlySpan<byte> Format(T value, Span<byte> buffer)
    {
        _ = value.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture);
        return buffer[..written];
    }
}

/// <summary>
/// A real, as a GraphQL Float, read from an Int or a Float literal.
/// </summary>
/// <typeparam name="T">The real type.</typeparam>
/// <param name="keyword">The C# keyword of the type, quoted when a number does not fit it.</param>
/// <remarks>
/// A literal too large for the type, <c>1e400</c>, reads as an infinity for a <see cref="double"/>, which no GraphQL Float
/// holds: refused, as System.Text.Json refuses a number that is not finite. A value that is not finite is never written.
/// </remarks>
internal sealed class RealCodec<T>(string keyword) : LiteralCodec<T>
    where T : struct, INumber<T>, IUtf8SpanParsable<T>, IUtf8SpanFormattable
{
    /// <inheritdoc />
    public override ScalarSerializationType Kind => ScalarSerializationType.Float;

    /// <inheritdoc />
    public override bool TryRead(IValueNode literal, out T value, out ValidationResult read)
    {
        switch (literal)
        {
            case FloatValueNode number:
                read = Read(number.AsSpan(), out value);
                return true;
            case IntValueNode number:
                read = Read(number.AsSpan(), out value);
                return true;
            default:
                (value, read) = (default!, default);
                return false;
        }
    }

    /// <inheritdoc />
    public override bool TryRead(JsonElement element, out T value, out ValidationResult read)
    {
        if (element.ValueKind != JsonValueKind.Number)
        {
            (value, read) = (default!, default);
            return false;
        }

        read = Read(JsonMarshal.GetRawUtf8Value(element), out value);
        return true;
    }

    /// <inheritdoc />
    public override ValidationResult Writable(T value)
        => T.IsFinite(value)
            ? default
            : ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, $"A {keyword} that is not finite has no GraphQL Float.");

    /// <inheritdoc />
    public override void Write(T value, ResultElement result)
    {
        Span<byte> buffer = stackalloc byte[MaxLength];
        result.SetNumberValue(Format(value, buffer));
    }

    /// <inheritdoc />
    public override IValueNode ToLiteral(T value)
    {
        Span<byte> buffer = stackalloc byte[MaxLength];
        var text = Format(value, buffer);
        return new FloatValueNode(
            new ReadOnlyMemorySegment(text.ToArray()),
            text.Contains((byte)'E') ? FloatFormat.Exponential : FloatFormat.FixedPoint);
    }

    private ValidationResult Read(ReadOnlySpan<byte> text, out T value)
        => T.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value!) && T.IsFinite(value)
            ? default
            : ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, $"The number is not a valid {keyword}.");

    /// <summary>
    /// The length of the longest real a value object wraps, a <see cref="decimal"/> of 29 digits, a sign and a point, and
    /// some.
    /// </summary>
    private const int MaxLength = 40;

    /// <summary>
    /// Writes the shortest text that reads back as the value, <c>0.1</c> for a <see cref="float"/> of 0.1 rather than the
    /// <see cref="double"/> it widens to.
    /// </summary>
    private static ReadOnlySpan<byte> Format(T value, Span<byte> buffer)
    {
        _ = value.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture);
        return buffer[..written];
    }
}

/// <summary>
/// Writes an underlying value GraphQL holds as a String in the invariant form the type's parser reads back, never through
/// a formatting hook of the value object, as its JSON converter writes the value.
/// </summary>
/// <typeparam name="TValue">The underlying type.</typeparam>
/// <remarks>
/// A date or a time is written in its round-trip form, <c>O</c>, and a duration in its constant one, <c>c</c>, as the
/// value object formats them by default. A type a value object written by hand wraps has no text of its own here, and is
/// written as the value object formats itself.
/// </remarks>
internal abstract class ValueText<TValue>
{
    /// <summary>
    /// Gets the text form of <typeparamref name="TValue"/>, or <see langword="null"/> for a type of no known text.
    /// </summary>
    public static ValueText<TValue>? Instance { get; } = Create();

    /// <summary>
    /// Writes a value into a result, as a GraphQL String.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="result">The result element.</param>
    public abstract void Write(TValue value, ResultElement result);

    /// <summary>
    /// Gets the text of a value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Its text.</returns>
    public abstract string Format(TValue value);

    private static ValueText<TValue>? Create()
    {
        var type = typeof(TValue);
        object? text =
            type == typeof(string) ? new StringText()
            : type == typeof(char) ? new CharText()
            : type == typeof(Guid) ? new FormattedText<Guid>(null)
            : type == typeof(Int128) ? new FormattedText<Int128>(null)
            : type == typeof(UInt128) ? new FormattedText<UInt128>(null)
            : type == typeof(TimeSpan) ? new FormattedText<TimeSpan>("c")
            : type == typeof(DateOnly) ? new FormattedText<DateOnly>("O")
            : type == typeof(TimeOnly) ? new FormattedText<TimeOnly>("O")
            : type == typeof(DateTime) ? new FormattedText<DateTime>("O")
            : type == typeof(DateTimeOffset) ? new FormattedText<DateTimeOffset>("O")
            : null;
        return (ValueText<TValue>?)text;
    }
}

/// <summary>
/// Text, as it stands.
/// </summary>
internal sealed class StringText : ValueText<string>
{
    /// <inheritdoc />
    public override void Write(string value, ResultElement result) => result.SetStringValue(value);

    /// <inheritdoc />
    public override string Format(string value) => value;
}

/// <summary>
/// A character, as a string of one.
/// </summary>
internal sealed class CharText : ValueText<char>
{
    /// <inheritdoc />
    public override void Write(char value, ResultElement result) => result.SetStringValue([value]);

    /// <inheritdoc />
    public override string Format(char value) => value.ToString();
}

/// <summary>
/// A value the type formats itself, in a format of its own.
/// </summary>
/// <typeparam name="T">The type.</typeparam>
/// <param name="format">The format, or <see langword="null"/> for the type's default.</param>
internal sealed class FormattedText<T>(string? format) : ValueText<T>
    where T : struct, ISpanFormattable
{
    /// <inheritdoc />
    public override void Write(T value, ResultElement result)
    {
        Span<char> buffer = stackalloc char[64];
        _ = value.TryFormat(buffer, out var written, format, CultureInfo.InvariantCulture);
        result.SetStringValue(buffer[..written]);
    }

    /// <inheritdoc />
    public override string Format(T value) => value.ToString(format, CultureInfo.InvariantCulture);
}
