using AdCodicem.ValueObjects.ProtobufNet;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;
using ProtoBuf.Serializers;
using RpcStatus = Google.Rpc.Status;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// <see cref="ValueObjectBinderConfiguration.ForServer"/>: a protobuf-net.Grpc code-first service whose messages hold
/// value objects, between a server and the .NET client over TestHost, as an application runs one.
/// </summary>
public sealed class CodeFirstGrpcTests
{
    private const string ValidIban = "FR7630006000011234567890189";

    private static readonly RuntimeTypeModel Registered = ProtoWire.Model().AddValueObjects(typeof(Iban).Assembly);

    /// <summary>
    /// Without the package, protobuf-net.Grpc finds no marshaller for a message holding a value object: a client over a
    /// configuration of its own cannot be built, the default client is built and fails its first call, and the server maps
    /// none of the service's methods, nothing saying so at start.
    /// </summary>
    [Fact]
    public async Task Without_the_package_the_client_fails_and_the_server_maps_nothing()
    {
        await using var server = await CodeFirstServer.StartAsync(BinderConfiguration.Create([ProtoBufMarshallerFactory.Create(ProtoWire.Model())]));

        FluentActions.Invoking(() => server.Client<IOrderLines>(ProtoWire.Model()))
            .Should().Throw<Exception>().WithInnerException<InvalidOperationException>()
            .WithMessage($"No marshaller available for {typeof(OrderLineRequest).FullName}");

        // The default client reads RuntimeTypeModel.Default, which the suite registers its value objects on, but never a
        // construction nothing registered.
        var stranger = server.Channel.CreateGrpcService<IProtoStrangers>();
        (await FluentActions.Awaiting(async () => await stranger.SendAsync(new ProtoStrangerRequest { Owner = Reference<ProtoStranger>.Create("po-1") }))
            .Should().ThrowAsync<NotSupportedException>()).Which.Message.Should().Be("Specified method is not supported.");

        var refused = await FluentActions.Awaiting(async () => await server.PlainClient().AddAsync(new PlainOrderLineRequest { Account = ValidIban, Quantity = 3 }))
            .Should().ThrowAsync<RpcException>();
        refused.Which.StatusCode.Should().Be(StatusCode.Unimplemented);
    }

    /// <summary>
    /// With the value objects registered on both ends, a call goes through; but protobuf-net.Grpc's own configuration
    /// answers a value the server refuses with <c>UNKNOWN</c>, which is what <c>ForServer</c> is for.
    /// </summary>
    [Fact]
    public async Task With_the_model_alone_a_refused_request_is_answered_unknown()
    {
        await using var server = await CodeFirstServer.StartAsync(BinderConfiguration.Create([ProtoBufMarshallerFactory.Create(Registered)]));

        var reply = await server.Client<IOrderLines>(Registered).AddAsync(Request(3));
        reply.Account.Should().Be(Iban.Create(ValidIban));
        reply.Total.Should().Be(Quantity.Create(3));

        var refused = await FluentActions.Awaiting(async () => await server.PlainClient().AddAsync(new PlainOrderLineRequest { Account = ValidIban, Quantity = 5000 }))
            .Should().ThrowAsync<RpcException>();
        refused.Which.StatusCode.Should().Be(StatusCode.Unknown);
        refused.Which.Status.Detail.Should().Be("Exception was thrown by handler.");
    }

    /// <summary>
    /// The configuration reads a request through the model it is given, never another: here one that trusts the bytes,
    /// which accepts a quantity the default model's strict serializer refuses.
    /// </summary>
    [Fact]
    public async Task Through_ForServer_a_request_is_read_through_the_model_given()
    {
        var trusted = ProtoWire.Model().AddValueObjects(trusted: true, typeof(Iban).Assembly);
        await using var server = await CodeFirstServer.StartAsync(ValueObjectBinderConfiguration.ForServer(trusted));

        var reply = await server.PlainClient().AddAsync(new PlainOrderLineRequest { Account = ValidIban, Quantity = 5000 });

        reply.Account.Should().Be(ValidIban);
        reply.Total.Should().Be(5000, "the model given trusts the bytes");
    }

    [Fact]
    public async Task Through_ForServer_a_call_goes_through_as_it_does_without()
    {
        await using var server = await CodeFirstServer.StartAsync(ValueObjectBinderConfiguration.ForServer(Registered));

        var reply = await server.Client<IOrderLines>(Registered).AddAsync(Request(3, ProtoOddCode.Create("even")));

        reply.Account.Should().Be(Iban.Create(ValidIban));
        reply.Total.Should().Be(Quantity.Create(3));
        (await server.Client<IOrderLines>(Registered).UploadAsync(Requests(Request(3), Request(4)))).Total.Should().Be(Quantity.Create(7));
    }

    /// <summary>
    /// A request value a value object refuses is answered <c>INVALID_ARGUMENT</c>, whose message names the value object
    /// and the rule, never the value, and whose <c>google.rpc.Status</c> holds an <c>ErrorInfo</c> carrying the code, under
    /// the domain the application names or the package's own.
    /// </summary>
    /// <param name="domain">The domain the server names, or <see langword="null"/> for none.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("orders.example.com")]
    public async Task Through_ForServer_a_refused_request_is_answered_invalid_argument_with_its_code(string? domain)
    {
        await using var server = await CodeFirstServer.StartAsync(
            domain is null ? ValueObjectBinderConfiguration.ForServer(Registered) : ValueObjectBinderConfiguration.ForServer(Registered, domain));

        var refused = (await FluentActions.Awaiting(async () => await server.PlainClient().AddAsync(new PlainOrderLineRequest { Account = ValidIban, Quantity = 5000 }))
            .Should().ThrowAsync<RpcException>()).Which;

        refused.StatusCode.Should().Be(StatusCode.InvalidArgument);
        refused.Status.Detail.Should().Be("The value read is not a valid Quantity: The value must be less than or equal to 1000.");
        refused.Status.Detail.Should().NotContain("5000");
        var status = refused.GetRpcStatus()!;
        status.Code.Should().Be((int)Code.InvalidArgument);
        status.Message.Should().Be(refused.Status.Detail);
        var info = status.GetDetail<ErrorInfo>()!;
        info.Reason.Should().Be("VALUE_OBJECT_OUT_OF_RANGE");
        info.Domain.Should().Be(domain ?? "adcodicem.valueobjects");
        info.Metadata.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["code"] = ValueObjectErrorCodes.OutOfRange,
            ["valueObject"] = "Quantity",
        });

        var pattern = (await FluentActions.Awaiting(async () => await server.PlainClient().AddAsync(new PlainOrderLineRequest { Account = "FR76", Quantity = 3 }))
            .Should().ThrowAsync<RpcException>()).Which;
        pattern.GetRpcStatus()!.GetDetail<ErrorInfo>()!.Reason.Should().Be("VALUE_OBJECT_TOO_SHORT");
    }

    [Fact]
    public async Task Through_ForServer_a_refused_message_of_a_client_stream_is_answered_invalid_argument()
    {
        await using var server = await CodeFirstServer.StartAsync(ValueObjectBinderConfiguration.ForServer(Registered));

        var refused = (await FluentActions.Awaiting(async () => await server.PlainClient().UploadAsync(PlainOrderLineStream.RequestsAsync()))
            .Should().ThrowAsync<RpcException>()).Which;

        refused.StatusCode.Should().Be(StatusCode.InvalidArgument);
        refused.GetRpcStatus()!.GetDetail<ErrorInfo>()!.Metadata["code"].Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// A code that maps to no gRPC reason is answered <c>INVALID_ARGUMENT</c> all the same, with a
    /// <c>google.rpc.Status</c> without the <c>ErrorInfo</c> it cannot fill, as the gRPC package's interceptor
    /// answers it.
    /// </summary>
    [Fact]
    public async Task Through_ForServer_a_code_that_maps_to_no_reason_is_answered_without_details()
    {
        await using var server = await CodeFirstServer.StartAsync(ValueObjectBinderConfiguration.ForServer(Registered));

        var refused = (await FluentActions.Awaiting(async () => await server.PlainClient().AddAsync(new PlainOrderLineRequest { Account = ValidIban, Quantity = 3, Odd = "odd" }))
            .Should().ThrowAsync<RpcException>()).Which;

        refused.StatusCode.Should().Be(StatusCode.InvalidArgument);
        refused.Status.Detail.Should().Be("The value read is not a valid ProtoOddCode: The value is odd.");
        var status = refused.GetRpcStatus()!;
        status.Code.Should().Be((int)Code.InvalidArgument);
        status.Message.Should().Be("The value read is not a valid ProtoOddCode: The value is odd.");
        status.Details.Should().BeEmpty();
    }

    /// <summary>
    /// A request protobuf-net itself cannot read, refused with no code, is answered as Grpc.AspNetCore answers it.
    /// </summary>
    [Fact]
    public async Task Through_ForServer_bytes_protobuf_net_cannot_read_are_answered_as_before()
    {
        await using var server = await CodeFirstServer.StartAsync(ValueObjectBinderConfiguration.ForServer(Registered));
        var bytes = Marshallers.Create<byte[]>(value => value, value => value);
        var add = new Method<byte[], byte[]>(MethodType.Unary, "orders.Lines", "Add", bytes, bytes);

        // The header of a field no member holds, of wire type 7, which protobuf has none of.
        var refused = (await FluentActions.Awaiting(async () => await server.Channel.CreateCallInvoker().AsyncUnaryCall(add, null, new CallOptions(cancellationToken: TestContext.Current.CancellationToken), [0x7F]))
            .Should().ThrowAsync<RpcException>()).Which;

        refused.StatusCode.Should().Be(StatusCode.Unknown);
        refused.GetRpcStatus().Should().BeNull();
    }

    /// <summary>
    /// A reply the client's value objects refuse surfaces on the client as protobuf-net.Grpc reports it, with the code in
    /// the chain; one the server cannot write is a fault of the server, which Grpc.AspNetCore answers <c>UNKNOWN</c>.
    /// </summary>
    [Fact]
    public async Task A_reply_refused_on_either_side_is_answered_as_gRPC_answers_it()
    {
        await using var server = await CodeFirstServer.StartAsync(ValueObjectBinderConfiguration.ForServer(Registered));
        var client = server.Client<IOrderLines>(Registered);

        var unread = (await FluentActions.Awaiting(async () => await client.EchoBadAsync(Request(3)))
            .Should().ThrowAsync<RpcException>()).Which;
        unread.StatusCode.Should().Be(StatusCode.Internal);
        unread.InnerException.Should().BeOfType<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.TooShort);
        ValueObjectErrors.TryGetCode(unread, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.TooShort);

        var unwritten = (await FluentActions.Awaiting(async () => await client.WriteDefaultAsync(Request(3)))
            .Should().ThrowAsync<RpcException>()).Which;
        unwritten.StatusCode.Should().Be(StatusCode.Unknown);
        unwritten.Status.Detail.Should().Be("Exception was thrown by handler.");
    }

    [Fact]
    public async Task Through_ForServer_a_Google_Protobuf_message_still_travels()
    {
        await using var server = await CodeFirstServer.StartAsync(ValueObjectBinderConfiguration.ForServer(Registered));
        var client = server.Channel.CreateGrpcService<IOrderLines>(
            ClientFactory.Create(BinderConfiguration.Create([ProtoBufMarshallerFactory.Create(Registered), ProtoBufMarshallerFactory.GoogleProtobuf])));

        (await client.EchoAsync(new StringValue { Value = "hi" })).Value.Should().Be("hi");
    }

    /// <summary>
    /// A type the model cannot serialize is no message of the configuration, protobuf-net.Grpc finding no marshaller, as
    /// its own factory answers: on a runtime model, a value object over a type protobuf-net cannot write, whose serializer
    /// the value object names; on a model that hands that serializer out as it is, as a generated one does, protobuf-net's
    /// question of what it is throws, which the configuration takes for a no.
    /// </summary>
    [Fact]
    public void A_type_the_model_cannot_serialize_has_no_marshaller()
    {
        var configuration = ValueObjectBinderConfiguration.ForServer(ProtoWire.Model().AddValueObjects(typeof(Iban).Assembly));

        FluentActions.Invoking(() => configuration.GetMarshaller<ProtoHugeCount>())
            .Should().Throw<InvalidOperationException>().WithMessage($"No marshaller available for {typeof(ProtoHugeCount).FullName}");
        configuration.GetMarshaller<OrderLineRequest>().Should().NotBeNull();

        FluentActions.Invoking(() => new HandingOutModel().CanSerialize(typeof(ProtoHugeCount))).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => ValueObjectBinderConfiguration.ForServer(new HandingOutModel()).GetMarshaller<ProtoHugeCount>())
            .Should().Throw<InvalidOperationException>().WithMessage($"No marshaller available for {typeof(ProtoHugeCount).FullName}");
    }

    [Fact]
    public void ForServer_refuses_a_null_model_and_a_blank_domain()
    {
        FluentActions.Invoking(() => ValueObjectBinderConfiguration.ForServer(null!)).Should().Throw<ArgumentNullException>().WithParameterName("model");
        FluentActions.Invoking(() => ValueObjectBinderConfiguration.ForServer(Registered, " ")).Should().Throw<ArgumentException>().WithParameterName("domain");
        ValueObjectBinderConfiguration.DefaultDomain.Should().Be("adcodicem.valueobjects");
    }

    /// <summary>
    /// The exception the server throws carries the code for the server's own handling, and the refusal for its log,
    /// which Grpc.AspNetCore never sends; a refusal of another library is answered with a message of the package's own,
    /// since another library's message may quote the value, and names no value object.
    /// </summary>
    [Fact]
    public void The_exception_answering_a_refusal_carries_the_code_and_the_refusal()
    {
        var own = ProtoRefusals.Refused("The value read is not a valid Quantity: too large.", ValueObjectErrorCodes.OutOfRange, typeof(Quantity));
        var wrapped = RpcStatusDetails.InvalidArgument(new InvalidOperationException("Failed.", own), ValueObjectErrorCodes.OutOfRange, "orders.example.com");
        wrapped.Status.Detail.Should().Be("The value read is not a valid Quantity: too large.");
        wrapped.Status.DebugException.Should().BeOfType<InvalidOperationException>();
        wrapped.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.OutOfRange);
        wrapped.GetRpcStatus()!.GetDetail<ErrorInfo>()!.Metadata["valueObject"].Should().Be("Quantity");

        var foreign = new FormatException("Secret 1234 refused.");
        foreign.Data[ValueObjectErrors.ErrorCodeKey] = ValueObjectErrorCodes.InvalidFormat;
        var answered = RpcStatusDetails.InvalidArgument(foreign, ValueObjectErrorCodes.InvalidFormat, "adcodicem.valueobjects");
        answered.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
        answered.Status.Detail.Should().Be("A value of the request was refused (value_object.invalid_format).");
        var info = answered.GetRpcStatus()!.GetDetail<ErrorInfo>()!;
        info.Reason.Should().Be("VALUE_OBJECT_INVALID_FORMAT");
        info.Metadata.Should().ContainKey("code").And.NotContainKey("valueObject");
        answered.ShouldCarry(ValueObjectErrorCodes.InvalidFormat);
    }

    /// <summary>
    /// The status, written by hand, is the bytes Google.Protobuf writes for the same message, whatever the length of its
    /// message: below, at and above 128 bytes, where a length takes a second byte.
    /// </summary>
    /// <param name="length">The length of the status message, in bytes.</param>
    [Theory]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(300)]
    public void The_status_is_the_bytes_Google_Protobuf_writes(int length)
    {
        const string Prefix = "The value read is not a valid Quantity: ";
        var message = Prefix + new string('x', length - Prefix.Length);
        var own = ProtoRefusals.Refused(message, ValueObjectErrorCodes.OutOfRange, typeof(Quantity));

        var answered = RpcStatusDetails.InvalidArgument(own, ValueObjectErrorCodes.OutOfRange, "orders.example.com");

        var expected = new RpcStatus
        {
            Code = (int)Code.InvalidArgument,
            Message = message,
            Details =
            {
                Any.Pack(new ErrorInfo
                {
                    Reason = "VALUE_OBJECT_OUT_OF_RANGE",
                    Domain = "orders.example.com",
                    Metadata = { ["code"] = ValueObjectErrorCodes.OutOfRange, ["valueObject"] = "Quantity" },
                }),
            },
        };
        answered.Trailers.GetValueBytes(RpcStatusDetails.Trailer).Should().Equal(expected.ToByteArray());
    }

    /// <summary>
    /// For a code that maps to no gRPC reason, the status holds no detail, and is the bytes Google.Protobuf writes for
    /// a status without one, as the gRPC package's interceptor sends it.
    /// </summary>
    [Fact]
    public void The_status_of_a_code_that_maps_to_no_reason_is_the_bytes_Google_Protobuf_writes()
    {
        var own = ProtoRefusals.Refused("The value read is not a valid ProtoOddCode: The value is odd.", ProtoOddCode.Code, typeof(ProtoOddCode));

        var answered = RpcStatusDetails.InvalidArgument(own, ProtoOddCode.Code, "orders.example.com");

        var expected = new RpcStatus { Code = (int)Code.InvalidArgument, Message = own.Message };
        answered.Trailers.GetValueBytes(RpcStatusDetails.Trailer).Should().Equal(expected.ToByteArray());
        answered.ShouldCarry(ProtoOddCode.Code);
    }

    private static OrderLineRequest Request(short quantity, ProtoOddCode? odd = null)
        => new() { Account = Iban.Create(ValidIban), Quantity = Quantity.Create(quantity), Odd = odd };

#pragma warning disable CS1998 // An async stream of values already in hand.
    private static async IAsyncEnumerable<OrderLineRequest> Requests(params OrderLineRequest[] requests)
#pragma warning restore CS1998
    {
        foreach (var request in requests)
        {
            yield return request;
        }
    }

    /// <summary>A model that hands out the serializer a value object names, as protobuf-net's generated models do.</summary>
    private sealed class HandingOutModel : TypeModel
    {
        protected override ISerializer<T> GetSerializer<T>()
            => typeof(T) == typeof(ProtoHugeCount)
                ? (ISerializer<T>)(object)new ValueObjectProtoSerializer<ProtoHugeCount, Int128>()
                : base.GetSerializer<T>();
    }

    /// <summary>A code-first server over TestHost, and the channel a client reaches it through.</summary>
    private sealed class CodeFirstServer : IAsyncDisposable
    {
        private readonly WebApplication _application;

        private CodeFirstServer(WebApplication application)
        {
            _application = application;
            Channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = application.GetTestServer().CreateHandler() });
        }

        public GrpcChannel Channel { get; }

        public static async Task<CodeFirstServer> StartAsync(BinderConfiguration binder)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton(binder);
            builder.Services.AddCodeFirstGrpc();
            var application = builder.Build();
            application.MapGrpcService<OrderLines>();
            await application.StartAsync(TestContext.Current.CancellationToken);
            return new CodeFirstServer(application);
        }

        public TService Client<TService>(TypeModel model)
            where TService : class
            => Channel.CreateGrpcService<TService>(ClientFactory.Create(BinderConfiguration.Create([ProtoBufMarshallerFactory.Create(model)])));

        public IPlainOrderLines PlainClient() => Client<IPlainOrderLines>(ProtoWire.Model());

        public async ValueTask DisposeAsync()
        {
            Channel.Dispose();
            await _application.DisposeAsync();
        }
    }
}
