using System.Globalization;
using System.Text;

namespace AdCodicem.ValueObjects.Shared;

/// <summary>
/// Reads and writes the text of one value object as a person types and reads it: in a culture, a date in the Gregorian
/// calendar whatever the culture's own, and a refusal always carrying a code.
/// </summary>
/// <typeparam name="TSelf">The value object type.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
/// <remarks>
/// This file is linked into the packages that read text a person typed: the Blazor one, for the input of a form, and the
/// System.CommandLine one, for the options and arguments of a command. Each compiles its own copy, and keeps the rules
/// that are its own: what empty text means, and what text a default instance is written as. It reads no annotation and
/// closes no generic type at run time, so it runs in a native binary.
/// </remarks>
internal static class ValueObjectText<TSelf, TValue>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// Whether a formatting hook writes the value object's text, which may not read back: <c>21 °C</c> for a temperature.
    /// </summary>
    private static readonly bool Hooked = typeof(TSelf).IsAssignableTo(typeof(IValueObjectFormatter<TValue>))
                                          || typeof(TSelf).IsAssignableTo(typeof(IValueObjectStringFormatter<TValue>));

    /// <summary>
    /// Whether the underlying type holds a date, which a culture reads in its own calendar: the Gregorian text written
    /// here would read hundreds of years away in the Thai Buddhist or the Persian calendar.
    /// </summary>
    private static readonly bool Dated = typeof(TValue) == typeof(DateOnly)
                                         || typeof(TValue) == typeof(DateTime)
                                         || typeof(TValue) == typeof(DateTimeOffset);

    /// <summary>
    /// The text of the bare underlying value, in the form its parser reads back, which the generated text of a value
    /// object without a hook is written in too: a date or a time in its round-trip form, and anything else in its default
    /// format, a duration's being its constant one.
    /// </summary>
    private static readonly CompositeFormat RoundTrip = CompositeFormat.Parse(
        Dated || typeof(TValue) == typeof(TimeOnly) ? "{0:O}" : "{0}");

    /// <summary>Reads text through the value object's rules, in a culture.</summary>
    /// <param name="text">The text.</param>
    /// <param name="culture">The culture it is read in; a date is read in the Gregorian calendar whatever its own.</param>
    /// <param name="result">The value read.</param>
    /// <param name="validation">
    /// Why the text was refused: the rule the value breaks, or <see cref="ValueObjectErrorCodes.NotParsable"/> for a parser
    /// written by hand that refused without saying why.
    /// </param>
    /// <returns><see langword="true"/> when the type accepts the text.</returns>
    public static bool TryParse(ReadOnlySpan<char> text, CultureInfo culture, out TSelf result, out ValidationResult validation)
    {
        if (TSelf.TryParse(text, Dated ? Gregorian(culture) : culture, out result, out validation))
        {
            return true;
        }

        // A parser written by hand may refuse without a code, which the descriptor's parser gives it too.
        if (validation.IsValid)
        {
            validation = ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, $"The text could not be parsed as {typeof(TSelf).Name}.");
        }

        return false;
    }

    /// <summary>Writes an underlying value in the form its parser reads back, in a culture.</summary>
    /// <param name="value">The underlying value.</param>
    /// <param name="culture">The culture it is written in; a date or a time is written in its round-trip form whatever it is.</param>
    /// <returns>The text.</returns>
    public static string FormatValue(TValue value, CultureInfo culture) => string.Format(culture, RoundTrip, value);

    /// <summary>Writes the text of a value object, which reads back to it, in a culture.</summary>
    /// <param name="value">The value object.</param>
    /// <param name="culture">The culture it is written in.</param>
    /// <returns>The text: its underlying value's when a formatting hook writes its own, which may not read back.</returns>
    public static string Format(TSelf value, CultureInfo culture)
        => Hooked ? FormatValue(value.Value, culture) : value.ToString(null, culture);

    /// <summary>
    /// Gets the culture a date is read in: the culture itself where its calendar is the Gregorian one, the calendar of the
    /// round-trip text written here, and otherwise a copy of it whose calendar is the Gregorian one, which every culture
    /// lists, so that its patterns and names stay and a year typed is a Gregorian one.
    /// </summary>
    private static CultureInfo Gregorian(CultureInfo culture)
    {
        if (culture.DateTimeFormat.Calendar is GregorianCalendar)
        {
            return culture;
        }

        var gregorian = (CultureInfo)culture.Clone();
        gregorian.DateTimeFormat.Calendar = new GregorianCalendar();
        return gregorian;
    }
}
