using AdCodicem.ValueObjects.Shared;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;
using Grpc.Core.Interceptors;
using RpcStatus = Google.Rpc.Status;

namespace AdCodicem.ValueObjects.Grpc;

/// <summary>
/// Answers a <see cref="ValueObjectException"/> that escapes a gRPC service method with <c>INVALID_ARGUMENT</c> and a
/// <c>google.rpc.ErrorInfo</c> carrying the rule's code, rather than <c>UNKNOWN</c>.
/// </summary>
/// <remarks>
/// <para>
/// A contract-first service maps the <c>string</c> and <c>int32</c> fields of its messages to value objects in its
/// handlers; when the mapping calls <c>Create</c> on a value the value object refuses, Grpc.AspNetCore answers the
/// exception with <c>UNKNOWN</c>, "Exception was thrown by handler.", and the rule is lost. Registered with
/// <c>AddGrpc(options =&gt; options.Interceptors.Add&lt;ValueObjectExceptionInterceptor&gt;())</c>, or
/// <c>AddCodeFirstGrpc</c>'s for a protobuf-net.Grpc service, this interceptor turns such an exception, one that carries a
/// code, into an <see cref="RpcException"/> whose trailers carry a <c>google.rpc.Status</c> holding a
/// <c>google.rpc.ErrorInfo</c>: its <c>reason</c> is the code mapped by <see cref="ValueObjectErrorCodes.ToUpperSnakeCase"/>,
/// its <c>domain</c> the one given, and its <c>metadata</c> holds the <c>code</c> and the <c>valueObject</c>. The status
/// message is the exception's, which, for one the library throws, names the type and the rule, never the value. A code
/// that maps to no gRPC reason is answered <c>INVALID_ARGUMENT</c> without the <c>ErrorInfo</c>.
/// </para>
/// <para>
/// Every other exception goes through as it is, an <see cref="RpcException"/> thrown by the handler included. A request
/// value refused while Grpc.AspNetCore reads the message, before any interceptor runs, never reaches this one: for a
/// protobuf-net.Grpc service, <c>ValueObjectBinderConfiguration.ForServer</c> of <c>AdCodicem.ValueObjects.ProtobufNet</c>
/// answers it.
/// </para>
/// </remarks>
/// <param name="domain">The <c>google.rpc.ErrorInfo.domain</c> of a refusal, the service's name for instance.</param>
public sealed class ValueObjectExceptionInterceptor(string domain = ValueObjectExceptionInterceptor.DefaultDomain) : Interceptor
{
    /// <summary>
    /// The <c>google.rpc.ErrorInfo.domain</c> a refusal is answered with when the application names none.
    /// </summary>
    public const string DefaultDomain = "adcodicem.valueobjects";

    private readonly string _domain = string.IsNullOrWhiteSpace(domain)
        ? throw new ArgumentException("A domain names the service or the product the reason belongs to.", nameof(domain))
        : domain;

    /// <inheritdoc />
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        try
        {
            return await continuation(request, context).ConfigureAwait(false);
        }
        catch (ValueObjectException exception) when (exception.ErrorCode is { Length: > 0 } code)
        {
            throw InvalidArgument(exception, code);
        }
    }

    /// <inheritdoc />
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        try
        {
            return await continuation(requestStream, context).ConfigureAwait(false);
        }
        catch (ValueObjectException exception) when (exception.ErrorCode is { Length: > 0 } code)
        {
            throw InvalidArgument(exception, code);
        }
    }

    /// <inheritdoc />
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        try
        {
            await continuation(request, responseStream, context).ConfigureAwait(false);
        }
        catch (ValueObjectException exception) when (exception.ErrorCode is { Length: > 0 } code)
        {
            throw InvalidArgument(exception, code);
        }
    }

    /// <inheritdoc />
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        try
        {
            await continuation(requestStream, responseStream, context).ConfigureAwait(false);
        }
        catch (ValueObjectException exception) when (exception.ErrorCode is { Length: > 0 } code)
        {
            throw InvalidArgument(exception, code);
        }
    }

    /// <summary>Builds the exception that answers a refusal.</summary>
    private RpcException InvalidArgument(ValueObjectException exception, string code)
    {
        var status = new RpcStatus { Code = (int)Code.InvalidArgument, Message = exception.Message };
        if (GrpcReasons.Of(code) is { } reason)
        {
            var errorInfo = new ErrorInfo { Reason = reason, Domain = _domain, Metadata = { ["code"] = code } };
            if (exception.ValueObjectType is { } type)
            {
                errorInfo.Metadata["valueObject"] = TypeNames.Of(type);
            }

            status.Details.Add(Any.Pack(errorInfo));
        }

        var rpcException = status.ToRpcException();
        rpcException.Data[ValueObjectErrors.ErrorCodeKey] = code;
        return rpcException;
    }
}
