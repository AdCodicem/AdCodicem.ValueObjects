using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using AdCodicem.ValueObjects.Metadata;
using Orleans.Serialization.Buffers;
using Orleans.Serialization.Codecs;
using Orleans.Serialization.TypeSystem;
using Orleans.Serialization.WireProtocol;

namespace AdCodicem.ValueObjects.Orleans;

/// <summary>
/// Carries a <see cref="ValueObjectException"/> a grain lets escape back to the caller: its message, its stack trace, its
/// code and the name of its value object type, never the value it refused.
/// </summary>
/// <param name="typeConverter">Orleans' type converter, which loads the value object type back through the type filters.</param>
/// <remarks>
/// <para>
/// Orleans serializes an exception only from the namespaces <c>Microsoft</c>, <c>System</c> and <c>Azure</c>, and its own
/// codec restores no property of a derived type. A subclass of <see cref="ValueObjectException"/> reaches this codec too,
/// which it implements <see cref="IDerivedTypeCodec"/> for, and arrives as a <see cref="ValueObjectException"/>; the inner
/// exception does not travel. A member, an argument or a result typed as the subclass is therefore refused on write, with
/// a <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// The exception is written as a value, never as a reference to one written earlier in the message, which Orleans' own
/// exception codec refuses to read. The value object type travels as its name, and is restored only where the caller
/// loads it and its type filters allow it: a value object only the silo knows reaches the caller with no type, its code
/// intact.
/// </para>
/// </remarks>
internal sealed class ValueObjectExceptionCodec(TypeConverter typeConverter) : IFieldCodec<ValueObjectException>, IDerivedTypeCodec
{
    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// The member, the argument or the result is typed as a subclass of <see cref="ValueObjectException"/>, which the
    /// reader could not assign the <see cref="ValueObjectException"/> it builds to.
    /// </exception>
    public void WriteField<TBufferWriter>(ref Writer<TBufferWriter> writer, uint fieldIdDelta, Type? expectedType, [AllowNull] ValueObjectException value)
        where TBufferWriter : IBufferWriter<byte>
    {
        if (expectedType is not null && expectedType.IsSubclassOf(typeof(ValueObjectException)))
        {
            // Refused before anything is written: the reader builds a ValueObjectException, which Orleans would then fail
            // to cast to the subclass, with no code.
            throw new NotSupportedException(
                $"A member, an argument or a result typed {expectedType.Name} cannot cross Orleans: a subclass of "
                + "ValueObjectException arrives as ValueObjectException, so type it ValueObjectException or Exception.");
        }

        if (value is null)
        {
            ReferenceCodec.WriteNullReference(ref writer, fieldIdDelta);
            return;
        }

        ReferenceCodec.MarkValueField(writer.Session);
        writer.WriteStartObject(fieldIdDelta, expectedType, typeof(ValueObjectException));
        StringCodec.WriteField(ref writer, 0, value.Message);
        StringCodec.WriteField(ref writer, 1, value.StackTrace);
        StringCodec.WriteField(ref writer, 1, value.ErrorCode);

        // RuntimeTypeNameFormatter writes any type: Orleans' TypeConverter.Format throws for a type the writer's filters
        // refuse, whatever its allowAllTypes argument says, which would fail the whole response.
        StringCodec.WriteField(ref writer, 1, value.ValueObjectType is { } type ? RuntimeTypeNameFormatter.Format(type) : null);
        writer.WriteEndObject();
    }

    /// <inheritdoc />
    public ValueObjectException? ReadValue<TInput>(ref Reader<TInput> reader, Field field)
    {
        if (field.IsReference)
        {
            return ReferenceCodec.ReadReference<ValueObjectException, TInput>(ref reader, field);
        }

        field.EnsureWireTypeTagDelimited();
        ReferenceCodec.MarkValueField(reader.Session);
        string? message = null;
        string? stackTrace = null;
        string? code = null;
        string? typeName = null;
        uint id = 0;
        while (true)
        {
            var header = reader.ReadFieldHeader();
            if (header.IsEndBaseOrEndObject)
            {
                break;
            }

            id += header.FieldIdDelta;
            switch (id)
            {
                case 0:
                    message = StringCodec.ReadValue(ref reader, header);
                    break;
                case 1:
                    stackTrace = StringCodec.ReadValue(ref reader, header);
                    break;
                case 2:
                    code = StringCodec.ReadValue(ref reader, header);
                    break;
                case 3:
                    typeName = StringCodec.ReadValue(ref reader, header);
                    break;
                default:
                    reader.ConsumeUnknownField(header);
                    break;
            }
        }

        var exception = new ValueObjectException(message ?? string.Empty, Load(typeName), code, attemptedValue: null);
        if (stackTrace is not null)
        {
            ExceptionDispatchInfo.SetRemoteStackTrace(exception, stackTrace);
        }

        return exception;
    }

    /// <summary>
    /// Loads the value object type a name stands for.
    /// </summary>
    /// <param name="typeName">The name the writer wrote, or <see langword="null"/> for an exception carrying no type.</param>
    /// <returns>
    /// The type, or <see langword="null"/> for one this process does not load, its type filters refuse, or that is no value
    /// object.
    /// </returns>
    private Type? Load(string? typeName)
    {
        if (typeName is null)
        {
            return null;
        }

        try
        {
            return typeConverter.TryParse(typeName, out var type) && ValueObjectRegistry.IsValueObject(type) ? type : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or TypeLoadException)
        {
            // A type the type filters refuse, or a name cut short (InvalidOperationException); a type of an assembly this
            // process does not load, such as a value object only the silo references (TypeLoadException).
            return null;
        }
    }
}
