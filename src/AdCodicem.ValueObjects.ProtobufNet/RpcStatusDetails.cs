using System.Buffers;
using System.Text;
using Grpc.Core;

// AdCodicem.ValueObjects.Metadata, the contracts' namespace, hides Grpc.Core's Metadata in this one.
using GrpcMetadata = Grpc.Core.Metadata;

namespace AdCodicem.ValueObjects.ProtobufNet;

/// <summary>
/// Writes the <c>google.rpc.Status</c> a refusal is answered with, holding a <c>google.rpc.ErrorInfo</c>, by hand: three
/// small messages, encoded without Google.Protobuf and without a protobuf-net model, which would build them by reflection.
/// A code that maps to no gRPC reason is answered with the status alone, as Grpc.StatusProto writes one with no detail.
/// </summary>
internal static class RpcStatusDetails
{
    /// <summary>The trailer gRPC's rich error model reads the status from.</summary>
    public const string Trailer = "grpc-status-details-bin";

    private const string ErrorInfoTypeUrl = "type.googleapis.com/google.rpc.ErrorInfo";

    /// <summary>Builds the exception that answers a refused request.</summary>
    /// <param name="refusal">The exception that carries the code.</param>
    /// <param name="code">The code.</param>
    /// <param name="domain">The domain of the <c>ErrorInfo</c>.</param>
    /// <returns>The exception to throw.</returns>
    public static RpcException InvalidArgument(Exception refusal, string code, string domain)
    {
        // The package's own refusal names the value object and the rule, never the value; another's message may hold it.
        var own = Own(refusal);
        var valueObject = own?.Data[ProtoRefusals.ValueObjectKey] as string;
        var message = own?.Message ?? $"A value of the request was refused ({code}).";

        var status = new ProtoBytes();
        status.Int32(1, (int)StatusCode.InvalidArgument);
        status.String(2, message);
        if (Reason(code) is { } reason)
        {
            var errorInfo = new ProtoBytes();
            errorInfo.String(1, reason);
            errorInfo.String(2, domain);
            errorInfo.Entry(3, "code", code);
            if (valueObject is not null)
            {
                errorInfo.Entry(3, "valueObject", valueObject);
            }

            var any = new ProtoBytes();
            any.String(1, ErrorInfoTypeUrl);
            any.Bytes(2, errorInfo.ToArray());
            status.Bytes(3, any.ToArray());
        }

        var trailers = new GrpcMetadata { { Trailer, status.ToArray() } };

        // The refusal stays on the status for the server's log, which Grpc.AspNetCore never sends; the code travels on the
        // exception too, where ValueObjectErrors.TryGetCode reads it in a server's own handling.
        var exception = new RpcException(new Status(StatusCode.InvalidArgument, message, refusal), trailers);
        exception.Data[ValueObjectErrors.ErrorCodeKey] = code;
        return exception;
    }

    /// <summary>Finds, in the exception's chain, the refusal a serializer of this package threw.</summary>
    private static Exception? Own(Exception refusal)
    {
        for (var current = refusal; current is not null; current = current.InnerException)
        {
            if (current.Data[ProtoRefusals.ValueObjectKey] is string)
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>Maps a code to a reason, or answers <see langword="null"/> for one that maps to none.</summary>
    private static string? Reason(string code)
    {
        try
        {
            return ValueObjectErrorCodes.ToUpperSnakeCase(code);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The protobuf encoding of a message's fields, varints and length-delimited values only.</summary>
    private sealed class ProtoBytes
    {
        private readonly ArrayBufferWriter<byte> _buffer = new();

        /// <summary>Writes an <c>int32</c> field.</summary>
        public void Int32(int field, int value)
        {
            Varint((uint)(field << 3));
            Varint((ulong)(long)value);
        }

        /// <summary>Writes a <c>string</c> field.</summary>
        public void String(int field, string value) => Bytes(field, Encoding.UTF8.GetBytes(value));

        /// <summary>Writes a <c>bytes</c> or message field.</summary>
        public void Bytes(int field, ReadOnlySpan<byte> value)
        {
            Varint((uint)((field << 3) | 2));
            Varint((ulong)value.Length);
            _buffer.Write(value);
        }

        /// <summary>Writes an entry of a <c>map&lt;string, string&gt;</c> field.</summary>
        public void Entry(int field, string key, string value)
        {
            var entry = new ProtoBytes();
            entry.String(1, key);
            entry.String(2, value);
            Bytes(field, entry._buffer.WrittenSpan);
        }

        /// <summary>Copies the bytes written.</summary>
        public byte[] ToArray() => _buffer.WrittenSpan.ToArray();

        private void Varint(ulong value)
        {
            var span = _buffer.GetSpan(10);
            var length = 0;
            while (value >= 0x80)
            {
                span[length++] = (byte)(value | 0x80);
                value >>= 7;
            }

            span[length++] = (byte)value;
            _buffer.Advance(length);
        }
    }
}
