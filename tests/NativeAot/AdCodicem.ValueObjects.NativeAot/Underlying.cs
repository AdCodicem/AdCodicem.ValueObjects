using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// The texts each value object is probed with, by underlying type, and how a text reads as a raw underlying value.
/// </summary>
/// <remarks>
/// The texts are chosen for the value objects of the domain: values each one accepts, its bounds and just past them,
/// what its normalizer changes, and text that does not have the shape of its underlying type.
/// </remarks>
internal static class Underlying
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly Dictionary<Type, string[]> TextsByType = new()
    {
        [typeof(bool)] = ["true", "False", "yes", ""],
        [typeof(char)] = ["a", "F", "G", "ab", ""],
        [typeof(sbyte)] = ["-10", "10", "11", "-129", "x"],
        [typeof(byte)] = ["0", "100", "101", "256", "-1"],
        [typeof(short)] = ["1", "1000", "0", "1001", "70000", "1.5"],
        [typeof(ushort)] = ["0", "1", "8080", "65535", "65536"],
        [typeof(int)] = ["1", "2147483647", "0", "-5", "2147483648", "1,000"],
        [typeof(uint)] = ["0", "4294967295", "-1", "4294967296"],
        [typeof(long)] = ["0", "9223372036854775807", "-1", "9223372036854775808"],
        [typeof(ulong)] = ["0", "18446744073709551615", "-1"],
        [typeof(Int128)] = ["1000000000000000000000", "-1000000000000000000000", "1000000000000000000001", "x"],
        [typeof(UInt128)] = ["0", "340282366920938463463374607431768211455", "340282366920938463463374607431768211456", "-1"],
        [typeof(float)] = ["0", "0.25", "1", "1.5", "NaN", "-0"],
        [typeof(double)] = ["-90", "45.5", "90.000001", "1", "NaN", "Infinity", "1e400"],
        [typeof(decimal)] = ["0", "12.345", "5.5", "20.0", "20", "-0.01", "7", "79228162514264337593543950336"],
        [typeof(Guid)] = ["00000000-0000-0000-0000-000000000000", "0f8fad5b-d9cb-469f-a165-70867728950e", "{0F8FAD5B-D9CB-469F-A165-70867728950E}", "nope"],
        [typeof(DateTime)] = ["2024-05-06T07:08:09", "2024-05-06T07:08:09Z", "1999-12-31T23:59:59", "2100-01-01", "nope"],
        [typeof(DateTimeOffset)] = ["2024-05-06T07:08:09+02:00", "1999-12-31T23:59:59+00:00", "nope"],
        [typeof(DateOnly)] = ["2000-01-01", "1999-12-31", "2024-02-30"],
        [typeof(TimeOnly)] = ["06:00", "12:00:00", "12:00:01", "05:59", "25:00"],
        [typeof(TimeSpan)] = ["01:30:00", "1.00:00:00", "1.00:00:01", "-00:00:01", "nope"],
        [typeof(Uri)] = ["https://example.com/a", "/relative", "mailto:ada@example.com"],
        [typeof(string)] = ["", " ", "  Ada@Example.COM ", "not-an-email", "+33123456789", "+33 1 23", "draft", "FINAL", "archived", "po-1042", "PO-1042-0001-X", new string('x', 201)],
    };

    private static readonly Dictionary<Type, Func<string, object?>> Readers = new()
    {
        [typeof(bool)] = static text => bool.TryParse(text, out var value) ? value : null,
        [typeof(char)] = static text => text.Length == 1 ? text[0] : null,
        [typeof(sbyte)] = static text => sbyte.TryParse(text, NumberStyles.Integer, Invariant, out var value) ? value : null,
        [typeof(byte)] = static text => byte.TryParse(text, NumberStyles.Integer, Invariant, out var value) ? value : null,
        [typeof(short)] = static text => short.TryParse(text, NumberStyles.Integer, Invariant, out var value) ? value : null,
        [typeof(ushort)] = static text => ushort.TryParse(text, NumberStyles.Integer, Invariant, out var value) ? value : null,
        [typeof(int)] = static text => int.TryParse(text, NumberStyles.Integer, Invariant, out var value) ? value : null,
        [typeof(uint)] = static text => uint.TryParse(text, NumberStyles.Integer, Invariant, out var value) ? value : null,
        [typeof(long)] = static text => long.TryParse(text, NumberStyles.Integer, Invariant, out var value) ? value : null,
        [typeof(ulong)] = static text => ulong.TryParse(text, NumberStyles.Integer, Invariant, out var value) ? value : null,
        [typeof(Int128)] = static text => Int128.TryParse(text, NumberStyles.Integer, Invariant, out var value) ? value : null,
        [typeof(UInt128)] = static text => UInt128.TryParse(text, NumberStyles.Integer, Invariant, out var value) ? value : null,
        [typeof(float)] = static text => float.TryParse(text, NumberStyles.Float, Invariant, out var value) ? value : null,
        [typeof(double)] = static text => double.TryParse(text, NumberStyles.Float, Invariant, out var value) ? value : null,
        [typeof(decimal)] = static text => decimal.TryParse(text, NumberStyles.Number, Invariant, out var value) ? value : null,
        [typeof(Guid)] = static text => Guid.TryParse(text, out var value) ? value : null,
        [typeof(DateTime)] = static text => DateTime.TryParse(text, Invariant, DateTimeStyles.RoundtripKind, out var value) ? value : null,
        [typeof(DateTimeOffset)] = static text => DateTimeOffset.TryParse(text, Invariant, DateTimeStyles.None, out var value) ? value : null,
        [typeof(DateOnly)] = static text => DateOnly.TryParse(text, Invariant, out var value) ? value : null,
        [typeof(TimeOnly)] = static text => TimeOnly.TryParse(text, Invariant, out var value) ? value : null,
        [typeof(TimeSpan)] = static text => TimeSpan.TryParse(text, Invariant, out var value) ? value : null,
        [typeof(Uri)] = static text => Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var value) ? value : null,
        [typeof(string)] = static text => text,
    };

    /// <summary>Gets the texts the value objects over an underlying type are probed with.</summary>
    /// <param name="type">Underlying type.</param>
    /// <returns>The texts, none for a type the table does not know.</returns>
    public static IReadOnlyList<string> Texts(Type type) => TextsByType.TryGetValue(type, out var texts) ? texts : [];

    /// <summary>Reads a text as a raw underlying value, in the invariant culture.</summary>
    /// <typeparam name="TValue">Underlying type.</typeparam>
    /// <param name="text">Text to read.</param>
    /// <param name="value">The value read.</param>
    /// <returns><see langword="true"/> when the text is one of the type.</returns>
    public static bool TryRead<TValue>(string text, out TValue value)
    {
        if (Readers.TryGetValue(typeof(TValue), out var read) && read(text) is TValue parsed)
        {
            value = parsed;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>Writes a raw underlying value as JSON, in the form a value object over it travels in.</summary>
    /// <typeparam name="TValue">Underlying type.</typeparam>
    /// <param name="value">Value to write.</param>
    /// <param name="typeInfo">The context's contract for the underlying type.</param>
    /// <returns>The JSON, or <see langword="null"/> for a number JSON cannot hold: an infinity, or not a number.</returns>
    /// <remarks>
    /// A value object over <see cref="Int128"/> or <see cref="UInt128"/> travels as a JSON string, which most readers
    /// take without losing digits, where System.Text.Json writes the bare type as a number.
    /// </remarks>
    public static string? Json<TValue>(TValue value, JsonTypeInfo<TValue> typeInfo) => value switch
    {
        double number when !double.IsFinite(number) => null,
        float number when !float.IsFinite(number) => null,
        Int128 or UInt128 => JsonSerializer.Serialize(Show(value), AppJsonContext.Default.String),
        _ => JsonSerializer.Serialize(value, typeInfo),
    };

    /// <summary>Shows a value as the invariant culture formats it, a text quoted and shortened past 40 characters.</summary>
    /// <param name="value">Value to show.</param>
    /// <returns>The text shown.</returns>
    public static string Show(object? value) => value switch
    {
        null => "null",
        string text => Quote(text),
        IFormattable formattable => formattable.ToString(null, Invariant),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>Quotes a text, shortened past 40 characters.</summary>
    /// <param name="text">Text to quote.</param>
    /// <returns>The quoted text.</returns>
    public static string Quote(string text)
        => text.Length > 40 ? $"\"{text[..12]}…\" ({text.Length} characters)" : $"\"{text}\"";
}
