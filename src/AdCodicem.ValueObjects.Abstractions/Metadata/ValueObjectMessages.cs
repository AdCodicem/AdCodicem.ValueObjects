using System.Collections.Frozen;
using System.Globalization;

namespace AdCodicem.ValueObjects.Metadata;

/// <summary>
/// The keys and the arguments an application localises the message of a refused value object with: one key per rule
/// code, and the arguments the rules the type declares supply.
/// </summary>
/// <remarks>
/// <para>
/// The message a rule refuses a value with is English, and stays so: <see cref="ValueObjectException"/> and the logs are
/// read by developers. An application that answers its users in their language looks a message up by the code of the
/// rule instead, under the key <see cref="KeyFor"/> gives it, <c>ValueObject.value_object.too_short</c> for
/// <see cref="ValueObjectErrorCodes.TooShort"/> and <c>ValueObject.iban.check_digits</c> for a code of its own. A key
/// with no entry keeps the English message, so an application translates only what it wants to.
/// </para>
/// <para>
/// The arguments a message quotes come from the <see cref="ValueObjectSchema"/> of the type that refused the value, the
/// rules it declares once: a length, the bounds, the known values. Each has a name, which FluentValidation quotes as a
/// placeholder, <c>{MinLength}</c>, and a position, from <c>{1}</c>, which an <c>IStringLocalizer</c> formats with
/// <see cref="string.Format(IFormatProvider, string, object[])"/>; <c>{0}</c> is the member the target reports the
/// refusal under, which the target adds. The value refused is never an argument: a message shown to someone else must
/// not quote what was sent.
/// </para>
/// <para>
/// The integrations look the messages up through this class: <c>AddValueObjects(messages =&gt; …)</c> on MVC,
/// <c>WithValueObjectProblemDetails(messages =&gt; …)</c> on a minimal API, and FluentValidation's
/// <c>LanguageManager</c> under <c>MustParseAs</c>, <c>MustSatisfy</c> and <c>NotDefault</c>. It reads no annotation and
/// closes no generic type at run time, so it runs in a native binary.
/// </para>
/// </remarks>
public static class ValueObjectMessages
{
    /// <summary>
    /// The prefix of every key, <c>ValueObject.</c>, which keeps the keys of value objects together in a resource.
    /// </summary>
    public const string KeyPrefix = "ValueObject.";

    /// <summary>The suffix of the key looked up first for a range of which only the minimum is declared.</summary>
    private const string MinimumSuffix = ".minimum";

    /// <summary>The suffix of the key looked up first for a range of which only the maximum is declared.</summary>
    private const string MaximumSuffix = ".maximum";

    /// <summary>The name of the argument holding the minimum length.</summary>
    private const string MinLength = "MinLength";

    /// <summary>The name of the argument holding the maximum length.</summary>
    private const string MaxLength = "MaxLength";

    /// <summary>The name of the argument holding the inclusive lower bound.</summary>
    private const string Minimum = "Minimum";

    /// <summary>The name of the argument holding the inclusive upper bound.</summary>
    private const string Maximum = "Maximum";

    /// <summary>The name of the argument holding the known values.</summary>
    private const string KnownValues = "KnownValues";

    /// <summary>
    /// How each underlying type that takes a bound reads its invariant text back and writes it for a culture: a
    /// <see cref="DateTime"/> keeping the kind its text names, as the type's own parser reads it.
    /// </summary>
    private static readonly FrozenDictionary<Type, Func<string, IFormatProvider, string?>> BoundFormatters =
        new Dictionary<Type, Func<string, IFormatProvider, string?>>
        {
            [typeof(char)] = Format<char>,
            [typeof(sbyte)] = Format<sbyte>,
            [typeof(byte)] = Format<byte>,
            [typeof(short)] = Format<short>,
            [typeof(ushort)] = Format<ushort>,
            [typeof(int)] = Format<int>,
            [typeof(uint)] = Format<uint>,
            [typeof(long)] = Format<long>,
            [typeof(ulong)] = Format<ulong>,
            [typeof(Int128)] = Format<Int128>,
            [typeof(UInt128)] = Format<UInt128>,
            [typeof(decimal)] = Format<decimal>,
            [typeof(double)] = Format<double>,
            [typeof(float)] = Format<float>,
            [typeof(DateOnly)] = Format<DateOnly>,
            [typeof(TimeOnly)] = Format<TimeOnly>,
            [typeof(DateTimeOffset)] = Format<DateTimeOffset>,
            [typeof(TimeSpan)] = Format<TimeSpan>,
            [typeof(DateTime)] = static (text, provider)
                => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant)
                    ? instant.ToString(null, provider)
                    : null,
        }.ToFrozenDictionary();

    /// <summary>
    /// Gets the key of a rule code: <see cref="KeyPrefix"/> followed by the code.
    /// </summary>
    /// <param name="errorCode">The code, one of <see cref="ValueObjectErrorCodes"/> or one of the application's own.</param>
    /// <returns>The key, <c>ValueObject.value_object.too_short</c> for <see cref="ValueObjectErrorCodes.TooShort"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errorCode"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errorCode"/> is empty.</exception>
    public static string KeyFor(string errorCode)
    {
        ArgumentException.ThrowIfNullOrEmpty(errorCode);

        return KeyPrefix + errorCode;
    }

    /// <summary>
    /// Gets the keys a message for a rule code is looked up under, the most specific first.
    /// </summary>
    /// <param name="schema">The rules of the type that refused the value.</param>
    /// <param name="errorCode">The code of the rule.</param>
    /// <returns>The keys, the first with an entry giving the message.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="schema"/> or <paramref name="errorCode"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="errorCode"/> is empty.</exception>
    /// <remarks>
    /// <see cref="ValueObjectErrorCodes.OutOfRange"/> is the code of both bounds, and a message saying "between {1} and
    /// {2}" reads badly when the type declares one. For a schema declaring a minimum alone, the key of the code followed
    /// by <c>.minimum</c> comes first, <c>ValueObject.value_object.out_of_range.minimum</c>; for one declaring a maximum
    /// alone, followed by <c>.maximum</c>; then the key of the code. The key is chosen by the bounds the type declares,
    /// not by the side the value crossed, which a refusal does not tell. Any other case has the key of the code alone.
    /// </remarks>
    public static IReadOnlyList<string> KeysFor(ValueObjectSchema schema, string errorCode)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var key = KeyFor(errorCode);

        if (errorCode == ValueObjectErrorCodes.OutOfRange && (schema.Minimum is null) != (schema.Maximum is null))
        {
            return [key + (schema.Minimum is null ? MaximumSuffix : MinimumSuffix), key];
        }

        return [key];
    }

    /// <summary>
    /// Gets the arguments a message for this code can quote, read from the schema of a descriptor and formatted for the
    /// culture.
    /// </summary>
    /// <param name="descriptor">The descriptor of the type that refused the value.</param>
    /// <param name="errorCode">The code of the rule.</param>
    /// <param name="provider">
    /// Formats the numbers and the dates; <see langword="null"/> for <see cref="CultureInfo.CurrentCulture"/>.
    /// </param>
    /// <returns>
    /// The name and the text of each argument, in the order of their positions from <c>{1}</c>; empty for a code other
    /// than <see cref="ValueObjectErrorCodes.TooShort"/>, <see cref="ValueObjectErrorCodes.TooLong"/>,
    /// <see cref="ValueObjectErrorCodes.OutOfRange"/> and <see cref="ValueObjectErrorCodes.NotAKnownValue"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="descriptor"/> or <paramref name="errorCode"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="errorCode"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// <see cref="ValueObjectErrorCodes.TooShort"/> has <c>MinLength</c>, <see cref="ValueObjectErrorCodes.TooLong"/>
    /// <c>MaxLength</c>, <see cref="ValueObjectErrorCodes.OutOfRange"/> <c>Minimum</c> then <c>Maximum</c>, and
    /// <see cref="ValueObjectErrorCodes.NotAKnownValue"/> <c>KnownValues</c>. Every value is text, never
    /// <see langword="null"/>: the empty text stands for a rule the schema does not declare, the other bound of a range
    /// that has one. Any other code, an application's own among them, has none.
    /// </para>
    /// <para>
    /// A bound is held as invariant text, which is read back as the underlying type and written for the culture:
    /// <c>0.01</c> becomes <c>0,01</c> in French. Text the type cannot read, which a schema written by hand may hold, is
    /// kept as it is. The known values are written each for the culture and joined with the culture's list separator
    /// followed by a space, <c>, </c> in English and <c>; </c> in French, where the comma separates the decimals.
    /// </para>
    /// <para>
    /// The descriptor's schema may differ from the type's, when the application registered one by hand: its rules are
    /// those the integrations that read the registry enforce and publish.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<KeyValuePair<string, object?>> ArgumentsFor(
        ValueObjectDescriptor descriptor,
        string errorCode,
        IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return Arguments(descriptor.Schema, descriptor.ValueType, errorCode, provider);
    }

    /// <summary>
    /// Gets the arguments a message for this code can quote, read from
    /// <see cref="IValueObject{TSelf, TValue}.Schema"/> and formatted for the culture.
    /// </summary>
    /// <typeparam name="TSelf">The value object type that refused the value.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="errorCode">The code of the rule.</param>
    /// <param name="provider">
    /// Formats the numbers and the dates; <see langword="null"/> for <see cref="CultureInfo.CurrentCulture"/>.
    /// </param>
    /// <returns>
    /// The name and the text of each argument, in the order of their positions from <c>{1}</c>; empty for a code other
    /// than <see cref="ValueObjectErrorCodes.TooShort"/>, <see cref="ValueObjectErrorCodes.TooLong"/>,
    /// <see cref="ValueObjectErrorCodes.OutOfRange"/> and <see cref="ValueObjectErrorCodes.NotAKnownValue"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="errorCode"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errorCode"/> is empty.</exception>
    /// <remarks>
    /// The arguments are those
    /// <see cref="ArgumentsFor(ValueObjectDescriptor, string, IFormatProvider?)"/> describes, read from the type's own
    /// schema, as a typed integration reads it, without asking the registry.
    /// </remarks>
    public static IReadOnlyList<KeyValuePair<string, object?>> ArgumentsFor<TSelf, TValue>(string errorCode, IFormatProvider? provider)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => Arguments(TSelf.Schema, typeof(TValue), errorCode, provider);

    /// <summary>Builds the arguments of a code from a schema.</summary>
    private static IReadOnlyList<KeyValuePair<string, object?>> Arguments(
        ValueObjectSchema schema,
        Type valueType,
        string errorCode,
        IFormatProvider? provider)
    {
        ArgumentException.ThrowIfNullOrEmpty(errorCode);
        var culture = provider ?? CultureInfo.CurrentCulture;

        return errorCode switch
        {
            ValueObjectErrorCodes.TooShort => [new(MinLength, schema.MinLength?.ToString(culture) ?? string.Empty)],
            ValueObjectErrorCodes.TooLong => [new(MaxLength, schema.MaxLength?.ToString(culture) ?? string.Empty)],
            ValueObjectErrorCodes.OutOfRange =>
            [
                new(Minimum, FormatBound(schema.Minimum, valueType, culture)),
                new(Maximum, FormatBound(schema.Maximum, valueType, culture)),
            ],
            ValueObjectErrorCodes.NotAKnownValue => [new(KnownValues, JoinKnownValues(schema, culture))],
            _ => [],
        };
    }

    /// <summary>
    /// Writes a bound held as invariant text for a culture, or keeps the text when the underlying type cannot read it.
    /// </summary>
    private static string FormatBound(string? bound, Type valueType, IFormatProvider provider)
    {
        if (bound is null)
        {
            return string.Empty;
        }

        return BoundFormatters.TryGetValue(valueType, out var format) && format(bound, provider) is { } formatted
            ? formatted
            : bound;
    }

    /// <summary>
    /// Writes the known values for a culture, joined with its list separator followed by a space.
    /// </summary>
    /// <remarks>
    /// The separator is the one of the culture <paramref name="provider"/> is, or of the current culture when it is
    /// another kind of provider, such as a <see cref="NumberFormatInfo"/>.
    /// </remarks>
    private static string JoinKnownValues(ValueObjectSchema schema, IFormatProvider provider)
    {
        var separator = (provider as CultureInfo ?? CultureInfo.CurrentCulture).TextInfo.ListSeparator + " ";

        return string.Join(
            separator,
            schema.KnownValues.Select(value => value is IFormattable formattable ? formattable.ToString(null, provider) : value.ToString()));
    }

    /// <summary>Reads invariant text as a value of a type that parses itself, and writes it for a culture.</summary>
    private static string? Format<TValue>(string text, IFormatProvider provider)
        where TValue : IParsable<TValue>, IFormattable
        => TValue.TryParse(text, CultureInfo.InvariantCulture, out var value) ? value.ToString(null, provider) : null;
}
