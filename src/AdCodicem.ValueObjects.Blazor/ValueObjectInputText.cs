using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;

namespace AdCodicem.ValueObjects.Blazor;

/// <summary>
/// Reads and writes the text of one value object, or of its nullable type, closed over it once, for every input bound to
/// that type.
/// </summary>
/// <typeparam name="TValue">The value object, or its nullable type.</typeparam>
internal abstract class ValueObjectInputText<TValue>
{
    /// <summary>The text of <typeparamref name="TValue"/>, once a descriptor was found for it.</summary>
    private static ValueObjectInputText<TValue>? s_text;

    /// <summary>
    /// Gets the HTML attributes the rules of the value object give an input, written the first time an input asks for them.
    /// </summary>
    public abstract IReadOnlyDictionary<string, object> SchemaAttributes { get; }

    /// <summary>
    /// Gets the text of <typeparamref name="TValue"/>, closed over the value object through the descriptor's visitor the
    /// first time a descriptor is found; until then, each call looks again, so that a registration made later is found.
    /// </summary>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidOperationException">The type is not a value object the registry describes.</exception>
    public static ValueObjectInputText<TValue> Resolve()
    {
        if (s_text is { } text)
        {
            return text;
        }

        var type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        var texts = Find(type).Accept(ValueObjectInputTexts.Closer.Instance);

        return s_text = (ValueObjectInputText<TValue>)(type == typeof(TValue) ? texts.Plain : texts.Optional);
    }

    /// <summary>Reads text through the value object's rules.</summary>
    /// <param name="text">The text typed.</param>
    /// <param name="culture">The culture it is read in.</param>
    /// <param name="result">The value read.</param>
    /// <param name="code">The code of the rule that refused the text.</param>
    /// <param name="message">
    /// The message of that rule, empty when the rule gave none, or <see langword="null"/> for empty text the type refuses,
    /// which is answered as a required field is.
    /// </param>
    /// <returns><see langword="true"/> when the type accepts the text.</returns>
    public abstract bool TryParse(string? text, CultureInfo culture, [MaybeNullWhen(false)] out TValue result, out string code, out string? message);

    /// <summary>Writes the text of a value, which reads back to it.</summary>
    /// <param name="value">The value.</param>
    /// <param name="culture">The culture it is written in.</param>
    /// <returns>The text.</returns>
    public abstract string? Format(TValue? value, CultureInfo culture);

    /// <summary>Looks the message of a refusal up in a resource the application names.</summary>
    /// <param name="resource">The type whose resources hold the messages.</param>
    /// <param name="services">The services the localizer factory is resolved from.</param>
    /// <param name="code">The code of the rule.</param>
    /// <param name="member">The name quoted as <c>{0}</c>.</param>
    /// <returns>The message, or <see langword="null"/> to keep the English one.</returns>
    public abstract string? Localize(Type resource, IServiceProvider services, string code, string member);

    /// <summary>
    /// Finds the descriptor of a value object: the one registered, or, where the runtime supports dynamic code, one the
    /// registry describes by reflection, as <c>[ParsesAs(typeof(T))]</c> does.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Only reached for a value object nothing registered: one written by hand, or a construction of a "
                        + "generic one, which the input's type argument names, and so keeps. Under native AOT, where dynamic "
                        + "code is not supported, the registered descriptor is the only one.")]
    private static ValueObjectDescriptor Find(Type type)
    {
        if (ValueObjectRegistry.TryGet(type, out var descriptor)
            || (RuntimeFeature.IsDynamicCodeSupported && ValueObjectRegistry.TryResolve(type, out descriptor)))
        {
            return descriptor;
        }

        throw new InvalidOperationException(
            $"InputValueObject binds '{type.Name}', which is not a value object the registry describes. Under native AOT, a "
            + "value object written by hand, ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema), and a construction "
            + "of a generic one, ValueObjectRegistry.Register<TSelf, TValue>(static () => new TSelf.ValueJsonConverter()), "
            + "have to be registered before the input renders.");
    }
}

/// <summary>
/// The texts of one value object and of its nullable type, closed by a visitor that is not generic, so that a native
/// binary holds one closing per value object rather than one per input type and value object.
/// </summary>
internal abstract class ValueObjectInputTexts
{
    /// <summary>Gets the text of the value object.</summary>
    public abstract object Plain { get; }

    /// <summary>Gets the text of its nullable type.</summary>
    public abstract object Optional { get; }

    /// <summary>Closes both texts over the type arguments a descriptor hands back.</summary>
    internal sealed class Closer : IValueObjectVisitor<ValueObjectInputTexts>
    {
        public static readonly Closer Instance = new();

        /// <summary>Closes the texts.</summary>
        /// <typeparam name="TSelf">The value object type.</typeparam>
        /// <typeparam name="TUnderlying">Its underlying type.</typeparam>
        /// <returns>Both texts.</returns>
        public ValueObjectInputTexts Visit<TSelf, TUnderlying>()
            where TSelf : struct, IValueObject<TSelf, TUnderlying>
            => new ValueObjectInputTexts<TSelf, TUnderlying>();
    }
}

/// <summary>The texts of one value object, created as they are asked for.</summary>
/// <typeparam name="TSelf">The value object type.</typeparam>
/// <typeparam name="TUnderlying">Its underlying type.</typeparam>
internal sealed class ValueObjectInputTexts<TSelf, TUnderlying> : ValueObjectInputTexts
    where TSelf : struct, IValueObject<TSelf, TUnderlying>
{
    /// <inheritdoc />
    public override object Plain => new ValueObjectInputText<TSelf, TUnderlying>();

    /// <inheritdoc />
    public override object Optional => new NullableValueObjectInputText<TSelf, TUnderlying>();
}

/// <summary>The text of a value object.</summary>
/// <typeparam name="TSelf">The value object type.</typeparam>
/// <typeparam name="TUnderlying">Its underlying type.</typeparam>
internal sealed class ValueObjectInputText<TSelf, TUnderlying> : ValueObjectInputText<TSelf>
    where TSelf : struct, IValueObject<TSelf, TUnderlying>
{
    /// <summary>
    /// Whether a formatting hook writes the value object's text, which may not read back: <c>21 °C</c> for a temperature.
    /// </summary>
    private static readonly bool Hooked = typeof(TSelf).IsAssignableTo(typeof(IValueObjectFormatter<TUnderlying>))
                                          || typeof(TSelf).IsAssignableTo(typeof(IValueObjectStringFormatter<TUnderlying>));

    /// <summary>
    /// Whether the underlying type holds a date, which a culture reads in its own calendar: the Gregorian text the input
    /// writes would read hundreds of years away in the Thai Buddhist or the Persian calendar.
    /// </summary>
    private static readonly bool Dated = typeof(TUnderlying) == typeof(DateOnly)
                                         || typeof(TUnderlying) == typeof(DateTime)
                                         || typeof(TUnderlying) == typeof(DateTimeOffset);

    /// <summary>
    /// The text of the bare underlying value, in the form its parser reads back, which the generated text of a value
    /// object without a hook is written in too: a date or a time in its round-trip form, and anything else in its default
    /// format, a duration's being its constant one.
    /// </summary>
    private static readonly CompositeFormat RoundTrip = CompositeFormat.Parse(
        Dated || typeof(TUnderlying) == typeof(TimeOnly) ? "{0:O}" : "{0}");

    /// <summary>The HTML attributes of the rules, once an input asked for them.</summary>
    private static IReadOnlyDictionary<string, object>? s_attributes;

    /// <summary>
    /// Gets the HTML attributes the rules give an input, plain or nullable, written the first time one asks for them: the
    /// pattern's can take a few milliseconds to write, and most inputs never ask.
    /// </summary>
    internal static IReadOnlyDictionary<string, object> Attributes => s_attributes ??= InputAttributes.From(TSelf.Schema);

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> SchemaAttributes => Attributes;

    /// <inheritdoc />
    public override bool TryParse(string? text, CultureInfo culture, out TSelf result, out string code, out string? message)
        => Read(text, culture, out result, out code, out message);

    /// <inheritdoc />
    public override string Format(TSelf value, CultureInfo culture) => Write(value, culture);

    /// <inheritdoc />
    public override string? Localize(Type resource, IServiceProvider services, string code, string member)
        => ValueObjectLocalizedMessages.Find<TSelf, TUnderlying>(resource, services, code, member);

    /// <summary>Reads text through the value object's rules.</summary>
    /// <param name="text">The text typed.</param>
    /// <param name="culture">The culture it is read in.</param>
    /// <param name="result">The value read.</param>
    /// <param name="code">The code of the rule that refused the text.</param>
    /// <param name="message">
    /// The message of that rule, or <see langword="null"/> for empty text the type refuses.
    /// </param>
    /// <returns><see langword="true"/> when the type accepts the text.</returns>
    internal static bool Read(string? text, CultureInfo culture, out TSelf result, out string code, out string? message)
    {
        if (TSelf.TryParse(text, Dated ? Gregorian(culture) : culture, out result, out var validation))
        {
            code = string.Empty;
            message = null;
            return true;
        }

        if (string.IsNullOrEmpty(text))
        {
            // A field emptied is a field required, whatever the parser would say of no text: a Guid's "not a valid Guid".
            code = ValueObjectErrorCodes.Required;
            message = null;
            return false;
        }

        // A parser written by hand may refuse without a code, which the descriptor's parser gives it too.
        if (validation.IsValid)
        {
            validation = ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, $"The text could not be parsed as {typeof(TSelf).Name}.");
        }

        code = validation.ErrorCode!;
        message = validation.ErrorMessage!;
        return false;
    }

    /// <summary>Writes the text of a value object, which reads back to it.</summary>
    /// <param name="value">The value object.</param>
    /// <param name="culture">The culture it is written in.</param>
    /// <returns>The text.</returns>
    internal static string Write(TSelf value, CultureInfo culture)
    {
        // An instance nobody filled in, which its rules refuse; a zero they accept is written as itself.
        if (value.IsDefault && !TSelf.Validate(value.Value).IsValid)
        {
            return string.Empty;
        }

        return Hooked
            ? string.Format(culture, RoundTrip, value.Value)
            : value.ToString(null, culture);
    }

    /// <summary>
    /// Gets the culture a date is read in: the culture itself where its calendar is the Gregorian one, the calendar of the
    /// round-trip text the input writes, and otherwise a copy of it whose calendar is the Gregorian one, which every
    /// culture lists, so that its patterns and names stay and a year typed is a Gregorian one.
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

/// <summary>
/// The text of a nullable value object: <see langword="null"/> is written as no text, and empty text, which
/// <see cref="Microsoft.AspNetCore.Components.Forms.InputBase{TValue}"/> binds to <see langword="null"/> before any
/// parsing, never reaches it.
/// </summary>
/// <typeparam name="TSelf">The value object type.</typeparam>
/// <typeparam name="TUnderlying">Its underlying type.</typeparam>
internal sealed class NullableValueObjectInputText<TSelf, TUnderlying> : ValueObjectInputText<TSelf?>
    where TSelf : struct, IValueObject<TSelf, TUnderlying>
{
    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> SchemaAttributes => ValueObjectInputText<TSelf, TUnderlying>.Attributes;

    /// <inheritdoc />
    public override bool TryParse(string? text, CultureInfo culture, out TSelf? result, out string code, out string? message)
    {
        var accepted = ValueObjectInputText<TSelf, TUnderlying>.Read(text, culture, out var value, out code, out message);
        result = value;
        return accepted;
    }

    /// <inheritdoc />
    public override string? Format(TSelf? value, CultureInfo culture)
        => value is { } present ? ValueObjectInputText<TSelf, TUnderlying>.Write(present, culture) : null;

    /// <inheritdoc />
    public override string? Localize(Type resource, IServiceProvider services, string code, string member)
        => ValueObjectLocalizedMessages.Find<TSelf, TUnderlying>(resource, services, code, member);
}
