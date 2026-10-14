using System.Diagnostics.CodeAnalysis;
using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Meta;

namespace AdCodicem.ValueObjects.ProtobufNet;

/// <summary>
/// Builds the protobuf-net.Grpc binder configuration of a code-first server whose messages hold value objects.
/// </summary>
public static class ValueObjectBinderConfiguration
{
    /// <summary>
    /// The <c>google.rpc.ErrorInfo.domain</c> a refusal is answered with when the application names none.
    /// </summary>
    public const string DefaultDomain = "adcodicem.valueobjects";

    /// <summary>
    /// Builds a binder configuration for a code-first server: payloads go through the model, and a request value a value
    /// object refuses is answered <c>INVALID_ARGUMENT</c> with the rule's code.
    /// </summary>
    /// <param name="model">The model the value objects are registered on, <see cref="RuntimeTypeModel.Default"/> or the application's own.</param>
    /// <param name="domain">The <c>google.rpc.ErrorInfo.domain</c> of a refusal, the service's name for instance.</param>
    /// <returns>The configuration, which <c>AddCodeFirstGrpc()</c> reads from the service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="domain"/> is empty or white space.</exception>
    /// <remarks>
    /// <para>
    /// Grpc.AspNetCore reads a request before any interceptor runs, and answers an exception its deserializer throws with
    /// <c>UNKNOWN</c>, "Exception was thrown by handler.". The marshaller of this configuration turns an exception that
    /// carries a rule's code, under <see cref="ValueObjectErrors.ErrorCodeKey"/>, into an <see cref="RpcException"/> with
    /// <see cref="StatusCode.InvalidArgument"/>, whose trailers carry <c>grpc-status-details-bin</c>: a
    /// <c>google.rpc.Status</c> holding a <c>google.rpc.ErrorInfo</c>, whose <c>reason</c> is the code mapped by
    /// <see cref="ValueObjectErrorCodes.ToUpperSnakeCase"/>, and whose <c>metadata</c> holds the <c>code</c> and the
    /// <c>valueObject</c>. Neither the status nor the detail holds the value refused. A code that maps to no gRPC reason is
    /// answered <c>INVALID_ARGUMENT</c> with the <c>google.rpc.Status</c> alone, without the <c>ErrorInfo</c>.
    /// </para>
    /// <para>
    /// Everything else is protobuf-net.Grpc's own: the marshallers of <c>ProtoBufMarshallerFactory.Create(model)</c>, then
    /// those of Google.Protobuf messages, as in <see cref="BinderConfiguration.Default"/>. A client needs nothing of this
    /// package beyond the model: a reply it refuses surfaces as <see cref="StatusCode.Internal"/>, whose inner exception
    /// carries the code.
    /// </para>
    /// </remarks>
    [RequiresDynamicCode("protobuf-net.Grpc binds a code-first service and closes its marshallers by reflection at run time.")]
    public static BinderConfiguration ForServer(TypeModel model, string domain = DefaultDomain)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        return BinderConfiguration.Create([new ValueObjectMarshallerFactory(model, domain), ProtoBufMarshallerFactory.GoogleProtobuf]);
    }
}

/// <summary>
/// Wraps protobuf-net's marshallers so that a request value a value object refuses is answered <c>INVALID_ARGUMENT</c>.
/// </summary>
/// <param name="model">The model.</param>
/// <param name="domain">The domain of the <c>ErrorInfo</c>.</param>
internal sealed class ValueObjectMarshallerFactory(TypeModel model, string domain) : MarshallerFactory
{
    private readonly BinderConfiguration _protobufNet = BinderConfiguration.Create([ProtoBufMarshallerFactory.Create(model)]);

    /// <inheritdoc />
    protected override bool CanSerialize(Type type)
    {
        try
        {
            return model.CanSerialize(type);
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    protected override Marshaller<T> CreateMarshaller<T>()
    {
        var inner = _protobufNet.GetMarshaller<T>();
        return new Marshaller<T>(inner.ContextualSerializer, context => Deserialize(inner, context));
    }

    /// <summary>Reads a message, and answers a value a value object refuses with <c>INVALID_ARGUMENT</c>.</summary>
    private T Deserialize<T>(Marshaller<T> inner, DeserializationContext context)
    {
        try
        {
            return inner.ContextualDeserializer(context);
        }
        catch (Exception exception) when (ValueObjectErrors.TryGetCode(exception, out var code))
        {
            throw RpcStatusDetails.InvalidArgument(exception, code, domain);
        }
    }
}
