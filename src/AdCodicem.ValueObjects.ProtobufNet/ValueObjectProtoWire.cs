using AdCodicem.ValueObjects.Shared;
using ProtoBuf;
using ProtoBuf.Meta;
using ProtoBuf.Serializers;

namespace AdCodicem.ValueObjects.ProtobufNet;

/// <summary>
/// What every closed serializer of a value object does: read through the inbuilt serializer of the underlying type, then
/// through the rules; refuse a default the type rejects on write.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
internal static class ValueObjectProtoWire<TSelf, TValue>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// Gets protobuf-net's inbuilt serializer of the underlying type at a level, or <see langword="null"/> when it has none,
    /// as for <see cref="Int128"/>, <see cref="UInt128"/> and <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="level">The compatibility level.</param>
    /// <returns>The serializer, or <see langword="null"/>.</returns>
    public static ISerializer<TValue>? Inbuilt(CompatibilityLevel level)
        => TypeModel.GetInbuiltSerializer<TValue>(level, DataFormat.Default);

    /// <summary>
    /// Returns the inbuilt serializer, or refuses a value object over a type protobuf-net cannot write.
    /// </summary>
    /// <param name="inbuilt">The serializer, or <see langword="null"/>.</param>
    /// <param name="level">The level it was asked for at.</param>
    /// <returns><paramref name="inbuilt"/>.</returns>
    /// <exception cref="NotSupportedException"><paramref name="inbuilt"/> is <see langword="null"/>.</exception>
    public static ISerializer<TValue> Require(ISerializer<TValue>? inbuilt, CompatibilityLevel level)
        => inbuilt ?? throw ProtoRefusals.Unsupported(typeof(TSelf), typeof(TValue), level);

    /// <summary>Reads a value object.</summary>
    /// <param name="inbuilt">The inbuilt serializer of the underlying type.</param>
    /// <param name="trusted">Whether the read skips validation.</param>
    /// <param name="state">The reader's state.</param>
    /// <returns>The value object.</returns>
    public static TSelf Read(ISerializer<TValue> inbuilt, bool trusted, ref ProtoReader.State state)
    {
        TValue value;
        try
        {
            value = inbuilt.Read(ref state, default!);
        }
        catch (Exception exception) when (exception
            is ProtoException
            or OverflowException
            or ArgumentException
            or FormatException
            or EndOfStreamException
            or InvalidOperationException and not ObjectDisposedException)
        {
            // Not kept as the inner exception: the message of the inbuilt serializer may quote the value. protobuf-net
            // throws ProtoException for a field of another wire type, OverflowException for a varint beyond a narrow
            // integer, ArgumentOutOfRangeException for a date beyond the .NET type's range, EndOfStreamException for
            // truncated bytes, and InvalidOperationException for a negative length and for a decimal written as text
            // that no decimal parses or that is longer than the 32 bytes it reads one into, "Unable to parse decimal:
            // 'abc'". A stream disposed under the reader, an ObjectDisposedException, is no value refused.
            throw ProtoRefusals.Refused(
                $"The protobuf value read cannot be read as {typeof(TSelf).Name}, a value object over {typeof(TValue).Name}.",
                ValueObjectErrorCodes.NotParsable,
                typeof(TSelf));
        }

        if (trusted)
        {
            return TSelf.CreateUnchecked(value);
        }

        // A value object written by hand may fail TryCreate without a rule, as MessagePack's formatter allows for too.
        return TSelf.TryCreate(value, out var result, out var validation)
            ? result
            : throw ProtoRefusals.Refused(
                $"The value read is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}",
                validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable,
                typeof(TSelf));
    }

    /// <summary>Writes a value object.</summary>
    /// <param name="inbuilt">The inbuilt serializer of the underlying type.</param>
    /// <param name="state">The writer's state.</param>
    /// <param name="value">The value object.</param>
    public static void Write(ISerializer<TValue> inbuilt, ref ProtoWriter.State state, TSelf value)
    {
        if (value.IsDefault)
        {
            var current = value.Value;
            var validation = TSelf.Validate(in current);
            if (!validation.IsValid)
            {
                throw ProtoRefusals.Refused(
                    $"The value to write is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}",
                    validation.ErrorCode,
                    typeof(TSelf));
            }
        }

        inbuilt.Write(ref state, value.Value);
    }
}

/// <summary>
/// The exceptions the serializers refuse a value with.
/// </summary>
internal static class ProtoRefusals
{
    /// <summary>
    /// The key under which a refusal names the value object, as C# writes it, which the code-first binder reads into the
    /// <c>ErrorInfo</c> it answers with.
    /// </summary>
    public const string ValueObjectKey = "AdCodicem.ValueObjects.ProtobufNet.ValueObject";

    /// <summary>Builds the refusal of a value.</summary>
    /// <param name="message">The message, which names the type and the rule, never the value.</param>
    /// <param name="code">The code of the violated rule.</param>
    /// <param name="valueObjectType">The value object.</param>
    /// <returns>The exception to throw.</returns>
    public static ProtoException Refused(string message, string code, Type valueObjectType)
    {
        var exception = new ProtoException(message);
        exception.Data[ValueObjectErrors.ErrorCodeKey] = code;
        exception.Data[ValueObjectKey] = TypeNames.Of(valueObjectType);
        return exception;
    }

    /// <summary>Builds the refusal of a value object over a type protobuf-net cannot write.</summary>
    /// <param name="valueObjectType">The value object.</param>
    /// <param name="valueType">Its underlying type.</param>
    /// <param name="level">The level asked for.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException Unsupported(Type valueObjectType, Type valueType, CompatibilityLevel level)
        => new($"'{TypeNames.Of(valueObjectType)}' is a value object over {valueType.Name}, which protobuf-net has no "
               + $"inbuilt serializer for at {level}.");
}
