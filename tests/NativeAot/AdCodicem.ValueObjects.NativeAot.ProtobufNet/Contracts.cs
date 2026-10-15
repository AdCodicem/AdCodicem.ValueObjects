using AdCodicem.ValueObjects.ProtobufNet;
using ProtoBuf;
using ProtoBuf.Meta;

namespace AdCodicem.ValueObjects.NativeAot.Protobuf;

/// <summary>A ticket, over a <see cref="Guid"/> written as text, at Level300, which UnderlyingTypes.cs has none of.</summary>
[ValueObject<Guid>]
[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<TicketId, Guid, ValueObjectProtoProfiles.StrictLevel300>))]
public readonly partial struct TicketId;

/// <summary>A price, over a <see cref="decimal"/> written as text, at Level300.</summary>
[ValueObject<decimal>]
[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Price, decimal, ValueObjectProtoProfiles.StrictLevel300>))]
public readonly partial struct Price : IValueObjectMinimum<decimal>
{
    public static decimal Minimum => 0m;
}

/// <summary>A message holding a value object of each type protobuf-net writes.</summary>
[ProtoContract]
public sealed class Everything
{
    [ProtoMember(1)]
    public Consent Consent { get; set; }

    [ProtoMember(2)]
    public Grade Grade { get; set; }

    [ProtoMember(3)]
    public Adjustment Adjustment { get; set; }

    [ProtoMember(4)]
    public Score Score { get; set; }

    [ProtoMember(5)]
    public Port Port { get; set; }

    [ProtoMember(6)]
    public PageNumber PageNumber { get; set; }

    [ProtoMember(7)]
    public SequenceNumber SequenceNumber { get; set; }

    [ProtoMember(8)]
    public FileSize FileSize { get; set; }

    [ProtoMember(9)]
    public ByteCount ByteCount { get; set; }

    [ProtoMember(10)]
    public Latitude Latitude { get; set; }

    [ProtoMember(11)]
    public Ratio Ratio { get; set; }

    [ProtoMember(12)]
    public OpeningTime OpeningTime { get; set; }

    [ProtoMember(13)]
    public RecordedAt RecordedAt { get; set; }

    [ProtoMember(14)]
    public Duration Duration { get; set; }

    [ProtoMember(15)]
    public EffectiveDate EffectiveDate { get; set; }

    [ProtoMember(16)]
    public Tolerance Tolerance { get; set; }

    [ProtoMember(17)]
    public PhoneNumber PhoneNumber { get; set; }

    [ProtoMember(18)]
    public Label Label { get; set; }

    [ProtoMember(19)]
    public DocumentStatus DocumentStatus { get; set; }

    [ProtoMember(20)]
    public TicketId Ticket { get; set; }

    [ProtoMember(21)]
    public Price Price { get; set; }

    [ProtoMember(22)]
    public Bic Bic { get; set; }
}

/// <summary>The same message over the primitives, each at the level its value object's serializer names.</summary>
[ProtoContract]
public sealed class PlainEverything
{
    [ProtoMember(1)]
    public bool Consent { get; set; }

    [ProtoMember(2)]
    public char Grade { get; set; }

    [ProtoMember(3)]
    public sbyte Adjustment { get; set; }

    [ProtoMember(4)]
    public byte Score { get; set; }

    [ProtoMember(5)]
    public ushort Port { get; set; }

    [ProtoMember(6)]
    public int PageNumber { get; set; }

    [ProtoMember(7)]
    public uint SequenceNumber { get; set; }

    [ProtoMember(8)]
    public long FileSize { get; set; }

    [ProtoMember(9)]
    public ulong ByteCount { get; set; }

    [ProtoMember(10)]
    public double Latitude { get; set; }

    [ProtoMember(11)]
    public float Ratio { get; set; }

    [ProtoMember(12)]
    public TimeOnly OpeningTime { get; set; }

    [ProtoMember(13)]
    [CompatibilityLevel(CompatibilityLevel.Level240)]
    public DateTime RecordedAt { get; set; }

    [ProtoMember(14)]
    [CompatibilityLevel(CompatibilityLevel.Level240)]
    public TimeSpan Duration { get; set; }

    [ProtoMember(15)]
    public DateOnly EffectiveDate { get; set; }

    [ProtoMember(16)]
    public double Tolerance { get; set; }

    [ProtoMember(17)]
    public string? PhoneNumber { get; set; }

    [ProtoMember(18)]
    public string? Label { get; set; }

    [ProtoMember(19)]
    public string? DocumentStatus { get; set; }

    [ProtoMember(20)]
    [CompatibilityLevel(CompatibilityLevel.Level300)]
    public Guid Ticket { get; set; }

    [ProtoMember(21)]
    [CompatibilityLevel(CompatibilityLevel.Level300)]
    public decimal Price { get; set; }

    [ProtoMember(22)]
    public string? Bic { get; set; }
}

/// <summary>Lists of value objects, of a number and of text.</summary>
[ProtoContract]
public sealed class Lists
{
    [ProtoMember(1)]
    public List<PageNumber>? Pages { get; set; }

    [ProtoMember(2)]
    public List<PhoneNumber>? Phones { get; set; }
}

/// <summary>The same lists over the primitives, the numbers unpacked as protobuf-net writes a list of value objects.</summary>
[ProtoContract]
public sealed class PlainLists
{
    [ProtoMember(1, IsPacked = false)]
    public List<int>? Pages { get; set; }

    [ProtoMember(2)]
    public List<string>? Phones { get; set; }
}

#pragma warning disable PBN9001 // protobuf-net's compile-time model is experimental, an error unless suppressed.

/// <summary>The model protobuf-net's generator writes at build time, which never consults a runtime model.</summary>
[ProtoModel]
[ProtoSerializable(typeof(Everything))]
[ProtoSerializable(typeof(PlainEverything))]
[ProtoSerializable(typeof(Lists))]
[ProtoSerializable(typeof(PlainLists))]
public partial class AppModel : TypeModel;

#pragma warning restore PBN9001
