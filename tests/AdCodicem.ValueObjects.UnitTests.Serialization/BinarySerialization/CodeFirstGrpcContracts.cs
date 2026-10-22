using System.Runtime.CompilerServices;
using Google.Protobuf.WellKnownTypes;
using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>A code of the application's own, which maps to no gRPC reason.</summary>
[ValueObject<string>]
public readonly partial struct ProtoOddCode : IValueObjectValidator<string>
{
    /// <summary>The code it refuses <c>odd</c> with.</summary>
    public const string Code = "9.odd";

    public static ValidationResult ValidateValue(in string value)
        => value == "odd" ? ValidationResult.Failure(Code, "The value is odd.") : ValidationResult.Success;
}

/// <summary>A count over a type protobuf-net cannot write, whose serializer the value object names all the same.</summary>
[ValueObject<Int128>]
[ProtoContract(Serializer = typeof(AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoSerializer<ProtoHugeCount, Int128>))]
public readonly partial struct ProtoHugeCount;

/// <summary>A request of a code-first service, holding value objects.</summary>
[ProtoContract]
public sealed class OrderLineRequest
{
    [ProtoMember(1)]
    public Iban Account { get; set; }

    [ProtoMember(2)]
    public Quantity Quantity { get; set; }

    [ProtoMember(3)]
    public ProtoOddCode? Odd { get; set; }
}

/// <summary>A reply of a code-first service, holding value objects.</summary>
[ProtoContract]
public sealed class OrderLineReply
{
    [ProtoMember(1)]
    public Iban Account { get; set; }

    [ProtoMember(2)]
    public Quantity Total { get; set; }
}

/// <summary>The same request over the primitives, which a client of another language sends.</summary>
[ProtoContract]
public sealed class PlainOrderLineRequest
{
    [ProtoMember(1)]
    public string? Account { get; set; }

    [ProtoMember(2)]
    public short Quantity { get; set; }

    [ProtoMember(3)]
    public string? Odd { get; set; }
}

/// <summary>The same reply over the primitives.</summary>
[ProtoContract]
public sealed class PlainOrderLineReply
{
    [ProtoMember(1)]
    public string? Account { get; set; }

    [ProtoMember(2)]
    public short Total { get; set; }
}

/// <summary>The service as a client holding the value objects calls it.</summary>
[Service("orders.Lines")]
public interface IOrderLines
{
    ValueTask<OrderLineReply> AddAsync(OrderLineRequest request, CallContext context = default);

    ValueTask<OrderLineReply> EchoBadAsync(OrderLineRequest request, CallContext context = default);

    ValueTask<OrderLineReply> WriteDefaultAsync(OrderLineRequest request, CallContext context = default);

    ValueTask<OrderLineReply> UploadAsync(IAsyncEnumerable<OrderLineRequest> requests, CallContext context = default);

#pragma warning disable PBN2008 // A Google.Protobuf message in a code-first contract, on purpose: ForServer keeps its marshaller.
    ValueTask<StringValue> EchoAsync(StringValue value, CallContext context = default);
#pragma warning restore PBN2008
}

/// <summary>The owner of the references of <see cref="ProtoStrangerRequest"/>, which nothing registers.</summary>
public sealed class ProtoStranger;

/// <summary>A request holding a construction of a generic value object that nothing registers on any model.</summary>
[ProtoContract]
public sealed class ProtoStrangerRequest
{
    [ProtoMember(1)]
    public Reference<ProtoStranger> Owner { get; set; }
}

/// <summary>A service no model serves, called through protobuf-net.Grpc's default client.</summary>
[Service("orders.Strangers")]
public interface IProtoStrangers
{
    ValueTask<ProtoStrangerRequest> SendAsync(ProtoStrangerRequest request, CallContext context = default);
}

/// <summary>The service as a client over the primitives calls it.</summary>
[Service("orders.Lines")]
public interface IPlainOrderLines
{
    [Operation("Add")]
    ValueTask<PlainOrderLineReply> AddAsync(PlainOrderLineRequest request, CallContext context = default);

    [Operation("Upload")]
    ValueTask<PlainOrderLineReply> UploadAsync(IAsyncEnumerable<PlainOrderLineRequest> requests, CallContext context = default);
}

/// <summary>The service as the server implements it: <c>EchoBad</c> answers a reply the value objects refuse.</summary>
[Service("orders.Lines")]
public interface IServedOrderLines
{
    [Operation("Add")]
    ValueTask<OrderLineReply> AddAsync(OrderLineRequest request, CallContext context = default);

    [Operation("EchoBad")]
    ValueTask<PlainOrderLineReply> EchoBadAsync(OrderLineRequest request, CallContext context = default);

    [Operation("WriteDefault")]
    ValueTask<OrderLineReply> WriteDefaultAsync(OrderLineRequest request, CallContext context = default);

    [Operation("Upload")]
    ValueTask<OrderLineReply> UploadAsync(IAsyncEnumerable<OrderLineRequest> requests, CallContext context = default);

#pragma warning disable PBN2008 // A Google.Protobuf message in a code-first contract, on purpose: ForServer keeps its marshaller.
    [Operation("Echo")]
    ValueTask<StringValue> EchoAsync(StringValue value, CallContext context = default);
#pragma warning restore PBN2008
}

/// <summary>The code-first service the tests serve.</summary>
public sealed class OrderLines : IServedOrderLines
{
    public ValueTask<OrderLineReply> AddAsync(OrderLineRequest request, CallContext context = default)
        => new(new OrderLineReply { Account = request.Account, Total = request.Quantity });

    public ValueTask<PlainOrderLineReply> EchoBadAsync(OrderLineRequest request, CallContext context = default)
        => new(new PlainOrderLineReply { Account = "FR76", Total = request.Quantity.Value });

#pragma warning disable VO0010 // The uninitialized instance is what the server cannot write.
    public ValueTask<OrderLineReply> WriteDefaultAsync(OrderLineRequest request, CallContext context = default)
        => new(new OrderLineReply { Total = request.Quantity });
#pragma warning restore VO0010

    public async ValueTask<OrderLineReply> UploadAsync(IAsyncEnumerable<OrderLineRequest> requests, CallContext context = default)
    {
        short total = 0;
        var account = default(Iban?);
        await foreach (var request in requests.WithCancellation(context.CancellationToken))
        {
            total += request.Quantity.Value;
            account ??= request.Account;
        }

        return new OrderLineReply { Account = account!.Value, Total = Quantity.Create(total) };
    }

    public ValueTask<StringValue> EchoAsync(StringValue value, CallContext context = default) => new(value);
}

/// <summary>The requests of a client stream over the primitives, the second refused.</summary>
internal static class PlainOrderLineStream
{
    public static async IAsyncEnumerable<PlainOrderLineRequest> RequestsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new PlainOrderLineRequest { Account = "FR7630006000011234567890189", Quantity = 3 };
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new PlainOrderLineRequest { Account = "FR7630006000011234567890189", Quantity = 5000 };
        yield return new PlainOrderLineRequest { Account = "FR7630006000011234567890189", Quantity = 4 };
    }
}
