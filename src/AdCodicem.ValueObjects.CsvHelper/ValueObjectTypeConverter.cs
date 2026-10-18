using System.Globalization;
using AdCodicem.ValueObjects.Shared;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace AdCodicem.ValueObjects.CsvHelper;

/// <summary>
/// Reads a CSV field through a value object's rules, and writes the value object as text its own parser reads back.
/// </summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
/// <remarks>
/// <para>
/// A field is read through <c>TSelf.TryParse</c>, the overload reporting the rule, in the member's culture: its
/// <c>TypeConverterOptions.CultureInfo</c>, the configuration's unless the application set one for the type, which
/// CsvHelper keys by the member's exact type, a nullable one included, or for the member. A date is read in a copy of that
/// culture whose calendar is the Gregorian one, and the member gets the normalized instance. An empty field, or one missing
/// under <c>MissingFieldFound = null</c>, is refused with <see cref="ValueObjectErrorCodes.Required"/> unless the type
/// accepts empty text. A nullable member reads <see langword="null"/> for empty text and for each of its
/// <c>NullValues</c>: CsvHelper's <c>NullableConverter</c> answers it around the converter the cache gives, and the
/// converter answers it itself when <c>[TypeConverter]</c> or a class map's <c>TypeConverter&lt;T&gt;()</c> names it on the
/// member. A positional record's parameter reaches a converter it names with no type, so a nullable one leaves its
/// converter to the cache. <c>Formats</c>, <c>NumberStyles</c> and the boolean values of the options are not read.
/// </para>
/// <para>
/// A refusal is CsvHelper's own <see cref="TypeConverterException"/>, which <c>GetRecords</c> lets through as it is and
/// <c>ReadingExceptionOccurred</c> sees. Its message names the type and the rule, never the field, and its
/// <see cref="Exception.Data"/> carries the rule's code under <see cref="ValueObjectErrors.ErrorCodeKey"/>, which
/// <see cref="ValueObjectErrors.TryGetCode"/> reads. Its <c>Text</c> holds the field, as CsvHelper's own converters set
/// it, empty text for one a short row lacks, unless <c>ExceptionMessagesContainRawData</c> is <see langword="false"/>. A
/// parser written by hand that refuses without a reason gives <see cref="ValueObjectErrorCodes.NotParsable"/>, its message
/// naming the type alone, as for a rule with an empty message. A map's
/// <c>Default(…, useOnConversionFailure: true)</c> returns its default in place of a refusal, with no code, where
/// CsvHelper's own converters would: a default of the value object, or <see langword="null"/> for a nullable member.
/// </para>
/// <para>
/// A value object is written as its own text, in the member's culture: a date or a time in its round-trip form, and the
/// underlying value's text for a type whose formatting hook writes text its parser may not read back. A nullable member
/// holding nothing is written as the first of its <c>NullValues</c>, or empty text, so its column stays; one holding the
/// empty instance of a type that accepts empty text is written as empty text, which reads back as
/// <see langword="null"/>. An uninitialized
/// instance its rules refuse is not written: the refusal is a <see cref="TypeConverterException"/> carrying the code.
/// </para>
/// <para>
/// The converter is created by <see cref="ValueObjectTypeConverterFactory"/>, or added for one type by
/// <see cref="ValueObjectTypeConverterCacheExtensions.AddValueObject{TSelf, TValue}"/>. Its constructor takes no argument,
/// so CsvHelper can create it from <c>[TypeConverter(typeof(…))]</c> on a member and from a class map's
/// <c>TypeConverter&lt;T&gt;()</c>.
/// </para>
/// </remarks>
public sealed class ValueObjectTypeConverter<TSelf, TValue> : DefaultTypeConverter
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>What CsvHelper's own converters put in <c>Text</c> when the raw data is kept out of exceptions.</summary>
    private const string HiddenText = "Hidden because ExceptionMessagesContainRawData is false.";

    /// <summary>The value object's name, as C# writes it, in a refusal's message.</summary>
    private static readonly string Name = TypeNames.Of(typeof(TSelf));

    /// <summary>
    /// Initializes the converter; parameterless, so that CsvHelper's <c>[TypeConverter]</c> and a class map's
    /// <c>TypeConverter&lt;T&gt;()</c> can create it.
    /// </summary>
    public ValueObjectTypeConverter()
    {
    }

    /// <summary>
    /// Reads the field through <c>TSelf.TryParse</c> in the member's culture, throwing a
    /// <see cref="TypeConverterException"/> that carries the rule's code for text the value object refuses.
    /// </summary>
    /// <param name="text">The field, <see langword="null"/> for one missing from a short row.</param>
    /// <param name="row">The row being read.</param>
    /// <param name="memberMapData">The member's map data, its options' culture among them.</param>
    /// <returns>The value object, or the map's default where <c>useOnConversionFailure</c> asks for it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> or <paramref name="memberMapData"/> is <see langword="null"/>.</exception>
    /// <exception cref="TypeConverterException">
    /// The value object refuses the text, or the field is empty and the type refuses empty text.
    /// </exception>
    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(memberMapData);

        // Named directly on a nullable member, through [TypeConverter] or a class map's TypeConverter<T>(), the converter
        // gets the field CsvHelper's NullableConverter answers for a converter the cache gives: null for empty text and for
        // each of the member's null values, before any rule.
        if ((string.IsNullOrEmpty(text) || memberMapData.TypeConverterOptions.NullValues.Contains(text))
            && Nullable.GetUnderlyingType(memberMapData.Type) is not null)
        {
            return null;
        }

        // A parser written by hand that refuses without a reason is given an empty message, so that the type is named here.
        if (ValueObjectText<TSelf, TValue>.TryParse(text, Culture(memberMapData), out var result, out var validation, unexplained: string.Empty))
        {
            return result;
        }

        // Default(…, useOnConversionFailure: true): the value the application chose, where CsvHelper's own converters
        // return it; elsewhere the base would throw its own exception, which carries no code.
        if (memberMapData is { UseDefaultOnConversionFailure: true, IsDefaultSet: true }
            && (memberMapData.Default is TSelf || (memberMapData.Default is null && Nullable.GetUnderlyingType(memberMapData.Type) is not null)))
        {
            return memberMapData.Default;
        }

        string code, message;
        if (string.IsNullOrEmpty(text))
        {
            // An empty field is a missing value, whatever the parser would say of no text: a Guid's "not a valid Guid".
            code = ValueObjectErrorCodes.Required;
            message = $"An empty field cannot be read as {Name}; declare the member as a nullable {Name}? instead.";
        }
        else
        {
            code = validation.ErrorCode!;
            message = string.IsNullOrEmpty(validation.ErrorMessage)
                ? $"The field is not a valid {Name}."
                : $"The field is not a valid {Name}: {validation.ErrorMessage}";
        }

        // CsvHelper's own converters hand a field a short row lacks over as empty text.
        var shown = row.Configuration.ExceptionMessagesContainRawData ? text ?? string.Empty : HiddenText;
        var refusal = new TypeConverterException(this, memberMapData, shown, row.Context, message);
        refusal.Data[ValueObjectErrors.ErrorCodeKey] = code;
        throw refusal;
    }

    /// <summary>
    /// Writes the value object's text in the member's culture, refusing an uninitialized instance its rules reject.
    /// </summary>
    /// <param name="value">The value object, or <see langword="null"/> for a nullable member holding nothing.</param>
    /// <param name="row">The row being written.</param>
    /// <param name="memberMapData">The member's map data, its options' culture and null values among them.</param>
    /// <returns>The text: the first of the options' null values, or empty text, for <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> or <paramref name="memberMapData"/> is <see langword="null"/>.</exception>
    /// <exception cref="TypeConverterException">The value is an uninitialized instance its rules reject.</exception>
    public override string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(memberMapData);

        if (value is not TSelf instance)
        {
            // The base writes the first of the null values, or empty text: never null, which would drop the field.
            return base.ConvertToString(value, row, memberMapData);
        }

        if (instance.IsDefault && TSelf.Validate(instance.Value) is { IsValid: false } validation)
        {
            var message = string.IsNullOrEmpty(validation.ErrorMessage)
                ? $"The value to write is not a valid {Name}."
                : $"The value to write is not a valid {Name}: {validation.ErrorMessage}";
            var refusal = new TypeConverterException(this, memberMapData, value, row.Context, message);
            refusal.Data[ValueObjectErrors.ErrorCodeKey] = validation.ErrorCode;
            throw refusal;
        }

        return ValueObjectText<TSelf, TValue>.Format(instance, Culture(memberMapData));
    }

    /// <summary>Gets the culture of a member: its options', or the invariant culture for map data built without one.</summary>
    private static CultureInfo Culture(MemberMapData memberMapData)
        => memberMapData.TypeConverterOptions.CultureInfo ?? CultureInfo.InvariantCulture;
}
