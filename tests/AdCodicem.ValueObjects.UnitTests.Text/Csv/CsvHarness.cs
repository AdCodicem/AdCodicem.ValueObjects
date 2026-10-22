using System.Globalization;
using AdCodicem.ValueObjects.CsvHelper;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace AdCodicem.ValueObjects.UnitTests.Csv;

/// <summary>
/// Reads and writes CSV text in memory, a context per read or write, so that nothing is process-wide: the converters go
/// on each context before its first read or write, as the package says.
/// </summary>
internal static class CsvHarness
{
    /// <summary>A configuration in a culture, its lines ending in <c>\n</c> so that a test writes its text plainly.</summary>
    /// <param name="culture">The culture; the invariant one when omitted.</param>
    /// <returns>The configuration.</returns>
    public static CsvConfiguration Configuration(CultureInfo? culture = null)
        => new(culture ?? CultureInfo.InvariantCulture) { NewLine = "\n" };

    /// <summary>Gives a context the package's factory.</summary>
    /// <param name="context">The context.</param>
    public static void Factory(CsvContext context) => context.TypeConverterCache.AddValueObjects();

    /// <summary>Gives a context nothing: CsvHelper on its own.</summary>
    /// <param name="context">The context.</param>
    public static void Nothing(CsvContext context) => _ = context;

    /// <summary>Writes records, the header first.</summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="records">The records.</param>
    /// <param name="configuration">The configuration; the invariant one when omitted.</param>
    /// <param name="setup">What the context gets before the write; the package's factory when omitted.</param>
    /// <returns>The text written.</returns>
    public static string Write<T>(IEnumerable<T> records, CsvConfiguration? configuration = null, Action<CsvContext>? setup = null)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using (var csv = new CsvWriter(writer, configuration ?? Configuration()))
        {
            (setup ?? Factory)(csv.Context);
            csv.WriteRecords(records);
        }

        return writer.ToString();
    }

    /// <summary>Reads every record of a text.</summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="text">The text, the header first unless the configuration says otherwise.</param>
    /// <param name="configuration">The configuration; the invariant one when omitted.</param>
    /// <param name="setup">What the context gets before the read; the package's factory when omitted.</param>
    /// <returns>The records.</returns>
    public static List<T> Read<T>(string text, CsvConfiguration? configuration = null, Action<CsvContext>? setup = null)
    {
        using var reader = new StringReader(text);
        using var csv = new CsvReader(reader, configuration ?? Configuration());
        (setup ?? Factory)(csv.Context);
        return csv.GetRecords<T>().ToList();
    }

    /// <summary>Reads a text whose one record the package refuses.</summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="text">The text.</param>
    /// <param name="configuration">The configuration; the invariant one when omitted.</param>
    /// <param name="setup">What the context gets before the read; the package's factory when omitted.</param>
    /// <returns>The refusal.</returns>
    public static TypeConverterException Refusal<T>(string text, CsvConfiguration? configuration = null, Action<CsvContext>? setup = null)
        => FluentActions.Invoking(() => Read<T>(text, configuration, setup)).Should().Throw<TypeConverterException>().Which;

    /// <summary>Writes one field as CsvHelper writes text, quoted where it needs to be.</summary>
    /// <param name="text">The field.</param>
    /// <param name="culture">The culture whose delimiter the field is quoted for; the invariant one when omitted.</param>
    /// <returns>The field as it appears in a file.</returns>
    public static string Field(string text, CultureInfo? culture = null)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using (var csv = new CsvWriter(writer, Configuration(culture)))
        {
            csv.WriteField(text);
        }

        return writer.ToString();
    }

    /// <summary>Gets whether a value object is an uninitialized instance, which its generated type answers explicitly.</summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="value">The value object.</param>
    /// <returns><see langword="true"/> for an instance nothing created.</returns>
    public static bool IsUninitialized<TSelf, TValue>(this IValueObject<TSelf, TValue> value)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => value.IsDefault;

    /// <summary>Gets the rule's code a refusal carries.</summary>
    /// <param name="exception">The refusal.</param>
    /// <returns>The code, or <see langword="null"/> for none.</returns>
    public static string? Code(this Exception exception) => ValueObjectErrors.TryGetCode(exception, out var code) ? code : null;

    /// <summary>Gets the first line of a message, before CsvHelper's report of the reader's and the parser's state.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The first line.</returns>
    public static string FirstLine(this Exception exception) => exception.Message.Split('\n')[0].TrimEnd('\r');
}
