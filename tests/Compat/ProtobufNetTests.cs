using AdCodicem.ValueObjects.ProtobufNet;
using Google.Rpc;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>A waybill of value objects, as an application declares one for protobuf-net.</summary>
[ProtoContract]
public sealed class Waybill
{
    [ProtoMember(1)]
    public CustomerId Customer { get; set; }

    [ProtoMember(2)]
    public Iban Account { get; set; }

    [ProtoMember(3)]
    public Quantity Quantity { get; set; }
}

/// <summary>The same waybill, of the primitives the value objects replace, as a client of another language sends it.</summary>
[ProtoContract]
public sealed class PlainWaybill
{
    [ProtoMember(1)]
    public Guid Customer { get; set; }

    [ProtoMember(2)]
    public string? Account { get; set; }

    [ProtoMember(3)]
    public int Quantity { get; set; }
}

/// <summary>A code-first service over the waybill.</summary>
[Service("compat.Waybills")]
public interface IWaybills
{
    ValueTask<Waybill> SendAsync(Waybill waybill, CallContext context = default);
}

/// <summary>The same service, as a client over the primitives calls it.</summary>
[Service("compat.Waybills")]
public interface IPlainWaybills
{
    [Operation("Send")]
    ValueTask<PlainWaybill> SendAsync(PlainWaybill waybill, CallContext context = default);
}

/// <summary>The service, which hands the waybill back.</summary>
public sealed class Waybills : IWaybills
{
    public ValueTask<Waybill> SendAsync(Waybill waybill, CallContext context = default) => new(waybill);
}

/// <summary>A seal code naming its serializer, for protobuf-net's compile-time model, as an application on native AOT does.</summary>
[ValueObject<string>(MaxLength = 8)]
[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<SealCode, string>))]
public readonly partial struct SealCode;

/// <summary>A crate under a seal.</summary>
[ProtoContract]
public sealed class SealedCrate
{
    [ProtoMember(1)]
    public SealCode Seal { get; set; }
}

/// <summary>The same crate, of the primitive the seal replaces.</summary>
[ProtoContract]
public sealed class PlainSealedCrate
{
    [ProtoMember(1)]
    public string? Seal { get; set; }
}

#pragma warning disable PBN9001 // protobuf-net's compile-time model is experimental, an error unless suppressed.

/// <summary>The model protobuf-net's generator, coming with the package's protobuf-net, writes at build time.</summary>
[ProtoModel]
[ProtoSerializable(typeof(SealedCrate))]
[ProtoSerializable(typeof(PlainSealedCrate))]
public partial class CompatProtoModel : TypeModel;

#pragma warning restore PBN9001

/// <summary>
/// AdCodicem.ValueObjects.ProtobufNet on the next major: value objects on protobuf-net's runtime model as the values they
/// carry, read through their rules, on its compile-time model through the serializer a value object names, and a
/// code-first service of protobuf-net.Grpc over TestHost through its binder.
/// </summary>
public sealed class ProtobufNetTests
{
    private const string ValidIban = "FR7630006000011234567890189";

    private static readonly Guid Customer = Guid.Parse("0192f4a0-0000-7000-8000-000000000001");

    [Fact]
    public void A_message_of_value_objects_is_written_as_the_message_of_their_values_and_read_through_their_rules()
    {
        var model = RuntimeTypeModel.Create().AddValueObjects(typeof(Iban).Assembly);
        var waybill = new Waybill { Customer = CustomerId.Create(Customer), Account = Iban.Create(ValidIban), Quantity = Quantity.Create(3) };

        var written = Bytes(model, waybill);
        written.Should().Equal(Bytes(model, new PlainWaybill { Customer = Customer, Account = ValidIban, Quantity = 3 }));
        var back = Read<Waybill>(model, written);
        back.Customer.Should().Be(waybill.Customer);
        back.Account.Should().Be(waybill.Account);
        back.Quantity.Should().Be(waybill.Quantity);

        var refusal = FluentActions.Invoking(() => Read<Waybill>(model, Bytes(model, new PlainWaybill { Customer = Customer, Account = ValidIban, Quantity = 500 })))
            .Should().Throw<ProtoException>().Which;
        refusal.Message.Should().Be("The value read is not a valid Quantity: The value must be less than or equal to 100.");
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void Protobuf_net_s_compile_time_model_writes_a_value_object_through_the_serializer_it_names()
    {
        TypeModel model = CompatProtoModel.Instance;

        var written = Bytes(model, new SealedCrate { Seal = SealCode.Create("AB-12") });
        written.Should().Equal(Bytes(model, new PlainSealedCrate { Seal = "AB-12" }));
        Read<SealedCrate>(model, written).Seal.Should().Be(SealCode.Create("AB-12"));

        var refusal = FluentActions.Invoking(() => Read<SealedCrate>(model, Bytes(model, new PlainSealedCrate { Seal = "AB-123456" })))
            .Should().Throw<ProtoException>().Which;
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.TooLong);
    }

    [Fact]
    public async Task A_code_first_service_answers_a_refused_request_with_invalid_argument_and_its_code()
    {
        var model = RuntimeTypeModel.Create().AddValueObjects(typeof(Iban).Assembly);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(ValueObjectBinderConfiguration.ForServer(model, "compat.example.com"));
        builder.Services.AddCodeFirstGrpc();
        await using var application = builder.Build();
        application.MapGrpcService<Waybills>();
        await application.StartAsync(TestContext.Current.CancellationToken);
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = application.GetTestServer().CreateHandler() });

        var client = channel.CreateGrpcService<IWaybills>(ClientFactory.Create(BinderConfiguration.Create([ProtoBufMarshallerFactory.Create(model)])));
        (await client.SendAsync(new Waybill { Customer = CustomerId.Create(Customer), Account = Iban.Create(ValidIban), Quantity = Quantity.Create(3) }))
            .Quantity.Should().Be(Quantity.Create(3));

        var plain = channel.CreateGrpcService<IPlainWaybills>(ClientFactory.Create(BinderConfiguration.Create([ProtoBufMarshallerFactory.Create(RuntimeTypeModel.Create())])));
        var refused = (await FluentActions.Awaiting(async () => await plain.SendAsync(new PlainWaybill { Customer = Customer, Account = "FR76", Quantity = 3 }))
            .Should().ThrowAsync<RpcException>()).Which;
        refused.StatusCode.Should().Be(StatusCode.InvalidArgument);
        var info = refused.GetRpcStatus()!.GetDetail<ErrorInfo>()!;
        info.Reason.Should().Be("VALUE_OBJECT_TOO_SHORT");
        info.Domain.Should().Be("compat.example.com");
        info.Metadata["code"].Should().Be(ValueObjectErrorCodes.TooShort);
        info.Metadata["valueObject"].Should().Be("Iban");
    }

    private static byte[] Bytes<T>(TypeModel model, T value)
    {
        using var stream = new MemoryStream();
        model.Serialize(stream, value);
        return stream.ToArray();
    }

    private static T Read<T>(TypeModel model, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return model.Deserialize<T>(stream);
    }
}
