using System.Buffers;
using System.Text;
using Orleans.Serialization;
using Orleans.Serialization.Buffers;
using Orleans.Serialization.Codecs;
using Orleans.Serialization.WireProtocol;

namespace AdCodicem.ValueObjects.Orleans;

/// <summary>
/// Writes a value object as Orleans writes its underlying value, and reads it back through its rules.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// <para>
/// A member, an argument, a result or an element typed as the value object is written exactly as the underlying value it
/// replaces: the value goes through Orleans' codec of <typeparamref name="TValue"/>, with that type as the expected one,
/// so no type is written, and bytes a peer wrote for the primitive read as the value object. A string is written in full
/// each time, where Orleans writes one it met earlier in the message as a reference to it, which its codec of a
/// <c>TSelf?</c> reads only as <see langword="null"/>. A value object held by a member typed <see cref="object"/> or an
/// interface is written as Orleans writes a struct there: a header naming <typeparamref name="TSelf"/>, the underlying
/// value as field 0, and the end of the object.
/// </para>
/// <para>
/// A read is strict unless the codec is trusted: the value read goes through <c>TryCreate</c>, which normalizes it, and one
/// the value object refuses throws a <see cref="SerializerException"/> naming the type and the rule, never the value, with
/// the rule's code in <see cref="Exception.Data"/> under <see cref="ValueObjectErrors.ErrorCodeKey"/>. A trusted codec
/// reads through <c>CreateUnchecked</c>, for a cluster whose silos and clients are all the application's own. Either way,
/// a <see langword="null"/> is refused with <see cref="ValueObjectErrorCodes.Required"/>, since a value object cannot hold
/// one: a member that can be missing is a <c>TSelf?</c>, which Orleans' own codec of a nullable value reads as
/// <see langword="null"/> without asking this codec; and a value the codec of <typeparamref name="TValue"/> cannot read, a
/// field of another wire type, a value beyond the range of <typeparamref name="TValue"/>, with
/// <see cref="ValueObjectErrorCodes.NotParsable"/>.
/// </para>
/// <para>
/// A write refuses, with a <see cref="SerializerException"/> carrying the rule's code the same way, an uninitialized
/// instance, equal to the default, whose value the value object rejects: validation tells a valid zero from a refused one.
/// Any other instance went through <c>Create</c>, or was read by a trusted codec, and is written as it is, without being
/// validated again.
/// </para>
/// </remarks>
public sealed class ValueObjectCodec<TSelf, TValue> : IFieldCodec<TSelf>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// Initializes the codec.
    /// </summary>
    /// <param name="trusted">
    /// Whether a read skips validation, for a cluster whose silos and clients are all the application's own;
    /// <see langword="false"/>, the default, reads through the value object's rules.
    /// </param>
    public ValueObjectCodec(bool trusted = false) => Trusted = trusted;

    /// <summary>
    /// Gets whether a read skips validation.
    /// </summary>
    internal bool Trusted { get; }

    /// <inheritdoc />
    /// <exception cref="SerializerException">
    /// <paramref name="value"/> equals <c>default(TSelf)</c> and holds a value the value object rejects.
    /// </exception>
    public void WriteField<TBufferWriter>(ref Writer<TBufferWriter> writer, uint fieldIdDelta, Type? expectedType, TSelf value)
        where TBufferWriter : IBufferWriter<byte>
    {
        if (value.IsDefault)
        {
            var current = value.Value;
            var validation = TSelf.Validate(in current);
            if (!validation.IsValid)
            {
                throw Refusal($"The value to write is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}", validation.ErrorCode);
            }
        }

        if (expectedType == typeof(TSelf) && value.Value is string text)
        {
            // In full, as Orleans' codec writes a string met for the first time, never as a reference to one met earlier in
            // the message: Orleans' codec of a TSelf?, which calls this codec, reads a reference only as null and fails on
            // any other. The field takes its place among the references, as the string would.
            ReferenceCodec.MarkValueField(writer.Session);
            writer.WriteFieldHeader(fieldIdDelta, typeof(string), typeof(string), WireType.LengthPrefixed);
            var length = Encoding.UTF8.GetByteCount(text);
            writer.WriteVarUInt32((uint)length);
            StringCodec.WriteRaw(ref writer, text, length);
            return;
        }

        var codec = writer.Session.CodecProvider.GetCodec<TValue>();
        if (expectedType == typeof(TSelf))
        {
            codec.WriteField(ref writer, fieldIdDelta, typeof(TValue), value.Value);
            return;
        }

        // A member typed object or an interface: the reader learns the type from the header, as for Orleans' own structs,
        // which are values, never referenced from elsewhere in the message.
        ReferenceCodec.MarkValueField(writer.Session);
        writer.WriteStartObject(fieldIdDelta, expectedType, typeof(TSelf));
        codec.WriteField(ref writer, 0, typeof(TValue), value.Value);
        writer.WriteEndObject();
    }

    /// <inheritdoc />
    /// <exception cref="SerializerException">
    /// The value read is <see langword="null"/>, a value the codec of the underlying value cannot read, or, unless the
    /// codec is trusted, a value the value object refuses.
    /// </exception>
    public TSelf ReadValue<TInput>(ref Reader<TInput> reader, Field field)
    {
        var codec = reader.Session.CodecProvider.GetCodec<TValue>();
        var value = field.FieldType == typeof(TSelf) && field.WireType == WireType.TagDelimited
            ? ReadTagged(codec, ref reader)
            : ReadUnderlying(codec, ref reader, field);

        if (value is null)
        {
            throw Refusal(
                $"A null cannot be read as {typeof(TSelf).Name}; declare the member as {typeof(TSelf).Name}? instead.",
                ValueObjectErrorCodes.Required);
        }

        if (Trusted)
        {
            return TSelf.CreateUnchecked(value);
        }

        return TSelf.TryCreate(value, out var result, out var validation)
            ? result
            : throw Refusal(
                $"The value read is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}",
                validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// Reads the underlying value of a value object written with its type, as field 0 of an object.
    /// </summary>
    /// <typeparam name="TInput">The reader's input.</typeparam>
    /// <param name="codec">Orleans' codec of the underlying value.</param>
    /// <param name="reader">The reader, past the object's header.</param>
    /// <returns>The underlying value, read past the end of the object.</returns>
    /// <exception cref="SerializerException">The object holds no field 0, or one the codec cannot read.</exception>
    private static TValue? ReadTagged<TInput>(IFieldCodec<TValue> codec, ref Reader<TInput> reader)
    {
        ReferenceCodec.MarkValueField(reader.Session);
        TValue? value = default;
        var found = false;
        uint id = 0;
        while (true)
        {
            var header = reader.ReadFieldHeader();
            if (header.IsEndBaseOrEndObject)
            {
                break;
            }

            id += header.FieldIdDelta;
            if (id == 0)
            {
                value = ReadUnderlying(codec, ref reader, header);
                found = true;
            }
            else
            {
                // A field a later version of the codec may write, skipped as Orleans' own codecs skip one.
                reader.ConsumeUnknownField(header);
            }
        }

        return found ? value : throw Unreadable();
    }

    /// <summary>
    /// Reads the underlying value through Orleans' codec of it.
    /// </summary>
    /// <typeparam name="TInput">The reader's input.</typeparam>
    /// <param name="codec">Orleans' codec of the underlying value.</param>
    /// <param name="reader">The reader, at the field.</param>
    /// <param name="field">The field's header.</param>
    /// <returns>The underlying value, or <see langword="null"/> for a null reference.</returns>
    /// <exception cref="SerializerException">The codec cannot read the field as the underlying value.</exception>
    private static TValue? ReadUnderlying<TInput>(IFieldCodec<TValue> codec, ref Reader<TInput> reader, Field field)
    {
        try
        {
            return codec.ReadValue(ref reader, field);
        }
        catch (Exception exception) when (exception
            is SerializerException
            or OverflowException
            or ArgumentException)
        {
            // Not kept as the inner exception: the codec's own message may quote the value, as DateOnly's
            // ArgumentOutOfRangeException does. Orleans throws a SerializerException for a field of another wire type or
            // length, OverflowException for a value beyond a narrower type, and ArgumentException for a date or a time
            // beyond the .NET type's range; its reader's IndexOutOfRangeException for truncated bytes propagates as it is.
            throw Unreadable();
        }
    }

    /// <summary>
    /// Builds the refusal of a value the codec of the underlying value cannot read.
    /// </summary>
    /// <returns>The exception to throw, carrying <see cref="ValueObjectErrorCodes.NotParsable"/>.</returns>
    private static SerializerException Unreadable()
        => Refusal(
            $"The Orleans value read cannot be read as {typeof(TSelf).Name}, a value object over {typeof(TValue).Name}.",
            ValueObjectErrorCodes.NotParsable);

    /// <summary>
    /// Builds a refusal carrying the code of the violated rule in its <see cref="Exception.Data"/>.
    /// </summary>
    /// <param name="message">What was refused, naming the type and the rule, never the value.</param>
    /// <param name="code">The code of the violated rule.</param>
    /// <returns>The exception to throw.</returns>
    private static SerializerException Refusal(string message, string code)
    {
        var exception = new SerializerException(message);
        exception.Data[ValueObjectErrors.ErrorCodeKey] = code;
        return exception;
    }
}
