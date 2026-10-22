using ProtoBuf;
using ProtoBuf.Meta;
using static AdCodicem.ValueObjects.UnitTests.BinarySerialization.ProtoWire;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// protobuf-net's compile-time model, which its generator writes for a <c>[ProtoModel]</c> partial type and which never
/// consults a runtime model: the route native AOT takes, here under the JIT, through the serializers the value objects
/// name with <c>[ProtoContract(Serializer = …)]</c>.
/// </summary>
/// <remarks>
/// The native binary runs the same route over every underlying type protobuf-net writes, in
/// <c>tests/NativeAot/AdCodicem.ValueObjects.NativeAot.ProtobufNet</c>.
/// </remarks>
public sealed class ProtoGeneratedModelTests
{
    private static readonly Guid Ticket = Guid.Parse("6f1c2a6e-1f0e-4c39-9d43-2b6c7f1b2a10");

    [Fact]
    public void A_generated_model_writes_value_objects_as_their_values_and_reads_them_back()
    {
        var model = ProtoGeneratedModel.Instance;
        var message = new ProtoAttributed
        {
            Sku = ProtoSku.Create("abc-123"),
            Ticket = ProtoTicket.Create(Ticket),
            Seats = ProtoSeats.Create(3),
            Tally = ProtoTally.Create(42),
            Skus = [ProtoSku.Create("xyz-9")],
        };

        var written = Bytes(model, message);
        written.Should().Equal(Bytes(model, new PlainProtoAttributed { Sku = "ABC-123", Ticket = Ticket, Seats = 3, Tally = 42, Skus = ["XYZ-9"] }));

        var back = Read<ProtoAttributed>(model, written);
        back.Sku.Should().Be(message.Sku);
        back.Ticket.Should().Be(message.Ticket);
        back.Seats.Should().Be(message.Seats);
        back.Tally.Should().Be(message.Tally);
        back.Skus.Should().Equal(message.Skus);
    }

    [Fact]
    public void A_generated_model_refuses_a_value_through_the_value_object_s_rules()
    {
        var model = ProtoGeneratedModel.Instance;

        FluentActions.Invoking(() => Read<ProtoAttributed>(model, Bytes(model, new PlainProtoAttributed { Sku = "toolongsku", Ticket = Ticket })))
            .Should().Throw<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.TooLong);
        Read<ProtoAttributed>(model, Bytes(model, new PlainProtoAttributed { Sku = "abc", Ticket = Ticket, Seats = 0 }))
            .Seats.Value.Should().Be(0, "the trusted profile of ProtoSeats skips its minimum");
#pragma warning disable VO0010 // The uninitialized instance is what the write must refuse.
        FluentActions.Invoking(() => Bytes(model, new ProtoAttributed { Ticket = ProtoTicket.Create(Ticket) }))
            .Should().Throw<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.Required);
#pragma warning restore VO0010
    }

    [Fact]
    public void A_generated_model_exports_no_schema()
    {
        FluentActions.Invoking(() => ProtoGeneratedModel.Instance.GetSchema(typeof(ProtoAttributed), ProtoSyntax.Proto3))
            .Should().Throw<NotSupportedException>();
    }
}

#pragma warning disable PBN9001 // protobuf-net's compile-time model is experimental, an error unless suppressed.

/// <summary>The model protobuf-net's generator writes for the messages over value objects that name their serializer.</summary>
[ProtoModel]
[ProtoSerializable(typeof(ProtoAttributed))]
[ProtoSerializable(typeof(PlainProtoAttributed))]
public partial class ProtoGeneratedModel : TypeModel;

#pragma warning restore PBN9001
