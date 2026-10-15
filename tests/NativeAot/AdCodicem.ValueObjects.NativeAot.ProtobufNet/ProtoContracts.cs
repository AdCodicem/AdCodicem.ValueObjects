// The serializer of each value object protobuf-net writes, named on a partial declaration of the unit suite's
// UnderlyingTypes.cs, as an author names it beside [ValueObject<T>]: protobuf-net's generator reads the attribute, which
// this library's generator cannot write for it. Most name the two-argument serializer, strict at Level200; RecordedAt and
// Duration a strict profile at Level240, where a DateTime and a TimeSpan are a Timestamp and a Duration; Tolerance a
// trusted one. LedgerBalance, Fingerprint and OccurredAt are left out: protobuf-net writes no Int128, UInt128 or
// DateTimeOffset.
using AdCodicem.ValueObjects.ProtobufNet;
using ProtoBuf;

namespace AdCodicem.ValueObjects.UnitTests.Domain;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Consent, bool>))]
public readonly partial struct Consent;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Grade, char>))]
public readonly partial struct Grade;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Adjustment, sbyte>))]
public readonly partial struct Adjustment;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Score, byte>))]
public readonly partial struct Score;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Port, ushort>))]
public readonly partial struct Port;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<PageNumber, int>))]
public readonly partial struct PageNumber;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<SequenceNumber, uint>))]
public readonly partial struct SequenceNumber;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<FileSize, long>))]
public readonly partial struct FileSize;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<ByteCount, ulong>))]
public readonly partial struct ByteCount;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Latitude, double>))]
public readonly partial struct Latitude;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Ratio, float>))]
public readonly partial struct Ratio;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<OpeningTime, TimeOnly>))]
public readonly partial struct OpeningTime;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<RecordedAt, DateTime, ValueObjectProtoProfiles.StrictLevel240>))]
public readonly partial struct RecordedAt;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Duration, TimeSpan, ValueObjectProtoProfiles.StrictLevel240>))]
public readonly partial struct Duration;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<EffectiveDate, DateOnly>))]
public readonly partial struct EffectiveDate;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Tolerance, double, ValueObjectProtoProfiles.TrustedLevel200>))]
public readonly partial struct Tolerance;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<PhoneNumber, string>))]
public readonly partial struct PhoneNumber;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Label, string>))]
public readonly partial struct Label;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Bic, string>))]
public readonly partial struct Bic;

[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<DocumentStatus, string>))]
public readonly partial struct DocumentStatus;
