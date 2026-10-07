using MessagePack;
using MessagePack.Formatters;

namespace AdCodicem.ValueObjects.MessagePack;

/// <summary>
/// Writes a value object as the bare value the options' formatter of its underlying type writes, and reads it back
/// through the value object's rules.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// <para>
/// The value goes through the formatter the options' resolver holds for <typeparamref name="TValue"/>, so a value object
/// writes exactly what the primitive it replaces writes under the same options: a <see cref="Guid"/> as text under
/// <c>StandardResolver</c> and as 16 bytes under <c>NativeGuidResolver</c>, a <see cref="decimal"/> or a
/// <see cref="DateTime"/> as the <c>Native*</c> resolvers placed in the chain write them. A formatting hook of the value
/// object never reaches the wire.
/// </para>
/// <para>
/// A read is strict unless the formatter is trusted: the value read goes through <c>TryCreate</c>, which normalizes it,
/// and one the value object refuses throws a <see cref="MessagePackSerializationException"/> naming the type and the
/// rule, never the value, with the rule's code in <see cref="Exception.Data"/> under
/// <see cref="ValueObjectErrors.ErrorCodeKey"/>, where <see cref="ValueObjectErrors.TryGetCode"/> finds it through the
/// exception MessagePack wraps it in. A trusted formatter reads through <c>CreateUnchecked</c>, for bytes the
/// application alone wrote, a cache among them. Either way, a <c>nil</c> is refused with
/// <see cref="ValueObjectErrorCodes.Required"/>, since a value object cannot hold one: a member that can be missing is a
/// <c>TSelf?</c>, which MessagePack's own <c>NullableFormatter</c> reads as <see langword="null"/> without asking this
/// formatter; and a value the formatter of <typeparamref name="TValue"/> cannot read, a value of another MessagePack
/// type, one beyond the range of <typeparamref name="TValue"/>, text it cannot parse, truncated bytes, with
/// <see cref="ValueObjectErrorCodes.NotParsable"/>.
/// </para>
/// <para>
/// A write refuses, with a <see cref="MessagePackSerializationException"/> carrying the rule's code the same way, an
/// uninitialized instance, equal to the default, whose value the value object rejects: validation tells a valid zero
/// from a refused one. Any other instance went through <c>Create</c>, or was read by a trusted formatter, and is written
/// as it is, without being validated again.
/// </para>
/// </remarks>
public sealed class ValueObjectFormatter<TSelf, TValue> : IMessagePackFormatter<TSelf>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// Initializes the formatter.
    /// </summary>
    /// <param name="trusted">
    /// Whether a read skips validation, for bytes the application alone wrote; <see langword="false"/>, the default,
    /// reads through the value object's rules.
    /// </param>
    public ValueObjectFormatter(bool trusted = false) => Trusted = trusted;

    /// <summary>
    /// Gets whether a read skips validation.
    /// </summary>
    internal bool Trusted { get; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="MessagePackSerializationException">
    /// <paramref name="value"/> equals <c>default(TSelf)</c> and holds a value the value object rejects.
    /// </exception>
    /// <exception cref="FormatterNotRegisteredException">
    /// The options' resolver holds no formatter for <typeparamref name="TValue"/>.
    /// </exception>
    public void Serialize(ref MessagePackWriter writer, TSelf value, MessagePackSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (value.IsDefault)
        {
            var current = value.Value;
            var validation = TSelf.Validate(in current);
            if (!validation.IsValid)
            {
                throw Refusal(
                    new MessagePackSerializationException(
                        $"The value to write is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}"),
                    validation.ErrorCode);
            }
        }

        options.Resolver.GetFormatterWithVerify<TValue>().Serialize(ref writer, value.Value, options);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="MessagePackSerializationException">
    /// The value read is a <c>nil</c>, which a value object cannot hold, a value the formatter of the underlying value
    /// cannot read, or, unless the formatter is trusted, a value the value object refuses.
    /// </exception>
    /// <exception cref="FormatterNotRegisteredException">
    /// The options' resolver holds no formatter for <typeparamref name="TValue"/>.
    /// </exception>
    public TSelf Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Bytes that end where the value should start, which the nil check would report with an EndOfStreamException.
        if (reader.End)
        {
            throw Unreadable();
        }

        if (reader.TryReadNil())
        {
            throw Refusal(
                new MessagePackSerializationException(
                    $"A nil cannot be read as {typeof(TSelf).Name}; declare the member as a nullable "
                    + $"{typeof(TSelf).Name}? instead."),
                ValueObjectErrorCodes.Required);
        }

        // Outside the try: a resolver chain holding no formatter for TValue is a misconfiguration, not a value refused.
        var formatter = options.Resolver.GetFormatterWithVerify<TValue>();
        TValue value;
        try
        {
            value = formatter.Deserialize(ref reader, options);
        }
        catch (Exception exception) when (exception
            is MessagePackSerializationException
            or OverflowException
            or ArgumentException
            or FormatException
            or EndOfStreamException)
        {
            // Not kept as the inner exception: the formatter's own message may quote the value, as DateOnly's
            // ArgumentOutOfRangeException does, "Actual value was 2147483647.", and no exception carries a value it
            // refused. The reader throws MessagePackSerializationException for a value of another MessagePack type and
            // EndOfStreamException for truncated bytes; the formatters of the narrow integers OverflowException; those
            // of the dates and times ArgumentOutOfRangeException for a value beyond the .NET type's range, or, under
            // NativeDateTimeResolver, which reads through DateTime.FromBinary, ArgumentException; and the formatter
            // of a Uri UriFormatException, a FormatException, for text no Uri parses. Any other exception propagates
            // as it is.
            throw Unreadable();
        }

        if (Trusted)
        {
            return TSelf.CreateUnchecked(value);
        }

        return TSelf.TryCreate(value, out var result, out var validation)
            ? result
            : throw Refusal(
                new MessagePackSerializationException(
                    $"The value read is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}"),
                validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// Builds the refusal of a value the formatter of the underlying value cannot read.
    /// </summary>
    /// <returns>The exception to throw, carrying <see cref="ValueObjectErrorCodes.NotParsable"/>.</returns>
    private static MessagePackSerializationException Unreadable()
        => Refusal(
            new MessagePackSerializationException(
                $"The MessagePack value read cannot be read as {typeof(TSelf).Name}, a value object over "
                + $"{typeof(TValue).Name}."),
            ValueObjectErrorCodes.NotParsable);

    /// <summary>
    /// Stores the code of the violated rule in the exception's <see cref="Exception.Data"/>.
    /// </summary>
    /// <param name="exception">The exception that reports the refusal.</param>
    /// <param name="code">The code of the violated rule.</param>
    /// <returns><paramref name="exception"/>, to be thrown.</returns>
    private static MessagePackSerializationException Refusal(MessagePackSerializationException exception, string code)
    {
        exception.Data[ValueObjectErrors.ErrorCodeKey] = code;
        return exception;
    }
}
