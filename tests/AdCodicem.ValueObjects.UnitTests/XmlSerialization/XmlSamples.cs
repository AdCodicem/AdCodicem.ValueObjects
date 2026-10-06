using System.Xml;
using System.Xml.Schema;
using AdCodicem.ValueObjects.Fixtures.XmlSerialization;

namespace AdCodicem.ValueObjects.UnitTests.XmlSerialization;

/// <summary>
/// One <see cref="XmlSample"/> for each value object of the fixture that opts into XML serialization: the 22 underlying
/// types, a closed set of text and one of numbers, a generic value object, a nested one and an entity identifier, and
/// beside them the edges of what a schema can say: a 128-bit integer whose lower bound, or one of whose known values, no
/// <see cref="decimal"/> holds, an infinite <see cref="float"/>, and constructions over an array and over a type nested
/// in a generic one.
/// </summary>
public static class XmlSamples
{
    public static IReadOnlyDictionary<string, XmlSample> All { get; } = Build();

    public static TheoryData<string> Names => [.. All.Keys];

    private static Dictionary<string, XmlSample> Build()
    {
        var shipment = ShipmentId.New();
        XmlSample[] samples =
        [
            XmlSample.Of<TrackingCode, string>(TrackingCode.Create(" ab1234 "), "AB1234"),
            XmlSample.Of<Remark, string>(Remark.Create(string.Empty), string.Empty),
            XmlSample.Of<ConsignmentId, Guid>(
                ConsignmentId.Create(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e")), "0f8fad5b-d9cb-469f-a165-70867728950e"),
            XmlSample.Of<Insured, bool>(Insured.Create(true), "true"),
            XmlSample.Of<HandlingClass, char>(HandlingClass.Create('B'), "66"),
            XmlSample.Of<TiltDegrees, sbyte>(TiltDegrees.Create(-3), "-3"),
            XmlSample.Of<PalletCount, byte>(PalletCount.Create(12), "12"),
            XmlSample.Of<DeckLevel, short>(DeckLevel.Create(-2), "-2"),
            XmlSample.Of<DockDoor, ushort>(DockDoor.Create(443), "443"),
            XmlSample.Of<ParcelCount, int>(ParcelCount.Create(7), "7"),
            XmlSample.Of<ManifestSequence, uint>(ManifestSequence.Create(uint.MaxValue), "4294967295"),
            XmlSample.Of<GrossGrams, long>(GrossGrams.Create(long.MaxValue), "9223372036854775807"),
            XmlSample.Of<ContainerSerial, ulong>(ContainerSerial.Create(ulong.MaxValue), "18446744073709551615"),

            // Both serializers write a bare 128-bit integer as an empty element, which a value object refuses to read.
            XmlSample.Of<CustomsBalance, Int128>(
                CustomsBalance.Create(Int128.MaxValue), "170141183460469231731687303715884105727", readsItsPrimitive: false, readsItsContract: false),
            XmlSample.Of<TariffFingerprint, UInt128>(
                TariffFingerprint.Create(UInt128.MaxValue), "340282366920938463463374607431768211455", readsItsPrimitive: false, readsItsContract: false),
            XmlSample.Of<DemurrageBalance, Int128>(
                DemurrageBalance.Create(Int128.MinValue), "-170141183460469231731687303715884105728", readsItsPrimitive: false, readsItsContract: false),
            XmlSample.Of<RiskBand, Int128>(
                RiskBand.Highest, "170141183460469231731687303715884105727", readsItsPrimitive: false, readsItsContract: false),
            XmlSample.Of<DeclaredValue, decimal>(DeclaredValue.Create(12.50m), "12.50"),
            XmlSample.Of<Heading, double>(Heading.Create(-45.25), "-45.25"),
            XmlSample.Of<Reading, double>(Reading.Create(double.PositiveInfinity), "INF"),
            XmlSample.Of<FillRatio, float>(FillRatio.Create(0.1f), "0.1"),
            XmlSample.Of<Gain, float>(Gain.Create(float.NegativeInfinity), "-INF"),
            XmlSample.Of<ShippingDate, DateOnly>(ShippingDate.Create(new DateOnly(2026, 10, 3)), "2026-10-03"),
            XmlSample.Of<PickupTime, TimeOnly>(PickupTime.Create(new TimeOnly(9, 15, 0, 123)), "09:15:00.123"),
            XmlSample.Of<ScannedAt, DateTime>(
                ScannedAt.Create(new DateTime(2026, 10, 3, 9, 0, 0, DateTimeKind.Utc)), "2026-10-03T09:00:00Z"),

            // DataContractSerializer writes a bare DateTimeOffset as a pair of elements, which holds no value to read.
            XmlSample.Of<DeliveredAt, DateTimeOffset>(
                DeliveredAt.Create(new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.FromHours(2))), "2026-10-03T09:00:00+02:00", readsItsContract: false),
            XmlSample.Of<TransitTime, TimeSpan>(TransitTime.Create(TimeSpan.FromMinutes(90)), "PT1H30M"),
            XmlSample.Of<ParcelState, string>(ParcelState.Shipped, "shipped"),
            XmlSample.Of<ServiceLevel, int>(ServiceLevel.Express, "3"),
            XmlSample.Of<ShipmentTag<Shipment>, string>(ShipmentTag<Shipment>.Create("fragile"), "fragile"),
            XmlSample.Of<ShipmentTag<int[]>, string>(ShipmentTag<int[]>.Create("bulk"), "bulk", name: "ShipmentTag<int[]>"),
            XmlSample.Of<ShipmentTag<Pallet<string>.Slot>, string>(
                ShipmentTag<Pallet<string>.Slot>.Create("top"), "top", name: "ShipmentTag<Pallet<string>.Slot>"),
            XmlSample.Of<Depot.Bay, string>(Depot.Bay.Create("B1"), "B1"),
            XmlSample.Of<ShipmentId, string>(shipment, shipment.Value),
        ];

        return samples.ToDictionary(static sample => sample.Name, StringComparer.Ordinal);
    }
}

/// <summary>
/// An instance of a value object of the XML fixture and the text it is written as: enough to put both serializers
/// through every shape a document holds it in.
/// </summary>
public abstract class XmlSample
{
    /// <summary>Gets the name of the value object, which names the sample.</summary>
    public abstract string Name { get; }

    public override string ToString() => Name;

    /// <summary>Writes the value object as a member, a nullable member and a list's items, and reads it back.</summary>
    public abstract void RoundTripsThroughXmlSerializer();

    /// <inheritdoc cref="RoundTripsThroughXmlSerializer"/>
    public abstract void RoundTripsThroughDataContractSerializer();

    /// <summary>Writes a null nullable member, and reads it back as null.</summary>
    public abstract void HoldsNullAsNil();

    /// <summary>
    /// Reads the document each serializer wrote from the primitive the value object replaces, or refuses it, as not
    /// parsable, where that document holds no value.
    /// </summary>
    public abstract void ReadsTheDocumentsItsPrimitiveWrote();

    /// <summary>Writes the value object as the root of a document, and reads it back.</summary>
    public abstract void RoundTripsAsTheRootElement();

    /// <summary>Adds the simple type describing the value object to a schema set, through the generated provider.</summary>
    /// <param name="schemas">The schema set.</param>
    /// <returns>The name of the type.</returns>
    public abstract XmlQualifiedName ProvideSchema(XmlSchemaSet schemas);

    /// <summary>Gets the type of a document holding the value object.</summary>
    public abstract Type HolderType { get; }

    public static XmlSample Of<TSelf, TValue>(
        TSelf value, string text, bool readsItsPrimitive = true, bool readsItsContract = true, string? name = null)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => new XmlSample<TSelf, TValue>(value, text, readsItsPrimitive, readsItsContract, name);
}

/// <inheritdoc />
public sealed class XmlSample<TSelf, TValue>(TSelf value, string text, bool readsItsPrimitive, bool readsItsContract, string? name)
    : XmlSample
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    public override string Name => name ?? (typeof(TSelf).IsGenericType ? "ShipmentTag" : typeof(TSelf).Name);

    public override void RoundTripsThroughXmlSerializer()
    {
        var xml = XmlDocuments.Serialize(new XmlHolder<TSelf> { Value = value, Optional = value, Many = [value, value] });

        XmlDocuments.TextOf(xml, "Value").Should().Be(text);
        XmlDocuments.TextOf(xml, "Optional").Should().Be(text);
        var back = XmlDocuments.Deserialize<XmlHolder<TSelf>>(xml);
        back.Value.Should().Be(value);
        back.Optional.Should().Be(value);
        back.Many.Should().Equal(value, value);
    }

    public override void RoundTripsThroughDataContractSerializer()
    {
        var xml = XmlDocuments.WriteContract(new XmlHolder<TSelf> { Value = value, Optional = value, Many = [value, value] });

        XmlDocuments.TextOf(xml, "Value").Should().Be(text);
        XmlDocuments.TextOf(xml, "Optional").Should().Be(text);
        var back = XmlDocuments.ReadContract<XmlHolder<TSelf>>(xml);
        back.Value.Should().Be(value);
        back.Optional.Should().Be(value);
        back.Many.Should().Equal(value, value);
    }

    public override void HoldsNullAsNil()
    {
        var holder = new XmlHolder<TSelf> { Value = value };

        var written = XmlDocuments.Serialize(holder);
        var contract = XmlDocuments.WriteContract(holder);

        written.Should().Contain("<Optional xsi:nil=\"true\" />");
        contract.Should().Contain("<Optional i:nil=\"true\" />");
        XmlDocuments.Deserialize<XmlHolder<TSelf>>(written).Optional.Should().BeNull();
        XmlDocuments.ReadContract<XmlHolder<TSelf>>(contract).Optional.Should().BeNull();
    }

    public override void ReadsTheDocumentsItsPrimitiveWrote()
    {
        var primitive = new PrimitiveHolder<TValue> { Value = value.Value };
        var written = XmlDocuments.Serialize(primitive);
        var contract = XmlDocuments.WriteContract(primitive);

        if (readsItsPrimitive)
        {
            XmlDocuments.Deserialize<XmlHolder<TSelf>>(written).Value.Should().Be(value);
        }
        else
        {
            Refusal(() => XmlDocuments.Deserialize<XmlHolder<TSelf>>(written)).Should().Be(ValueObjectErrorCodes.NotParsable);
        }

        if (readsItsContract)
        {
            XmlDocuments.ReadContract<XmlHolder<TSelf>>(contract).Value.Should().Be(value);
        }
        else
        {
            Refusal(() => XmlDocuments.ReadContract<XmlHolder<TSelf>>(contract)).Should().Be(ValueObjectErrorCodes.NotParsable);
        }
    }

    public override void RoundTripsAsTheRootElement()
    {
        var written = XmlDocuments.Serialize(value);
        var contract = XmlDocuments.WriteContract(value);

        XmlDocuments.Deserialize<TSelf>(written).Should().Be(value);
        XmlDocuments.ReadContract<TSelf>(contract).Should().Be(value);
    }

    public override XmlQualifiedName ProvideSchema(XmlSchemaSet schemas)
        => (XmlQualifiedName)typeof(TSelf).GetMethod("GetXmlSchema")!.Invoke(null, [schemas])!;

    public override Type HolderType => typeof(XmlHolder<TSelf>);

    private static string? Refusal(Action read)
    {
        var exception = read.Should().Throw<Exception>().Which;
        ValueObjectErrors.TryGetCode(exception, out var code).Should().BeTrue();
        return code;
    }
}
