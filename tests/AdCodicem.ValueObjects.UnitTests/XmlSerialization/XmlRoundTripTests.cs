using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using AdCodicem.ValueObjects.Fixtures.XmlSerialization;

namespace AdCodicem.ValueObjects.UnitTests.XmlSerialization;

/// <summary>
/// A value object of an assembly marked <c>[assembly: ValueObjectXmlSerialization]</c> through <c>XmlSerializer</c> and
/// <c>DataContractSerializer</c>: written as its underlying value, in the form <c>XmlSerializer</c> writes that type in,
/// and read back through its rules.
/// </summary>
public class XmlRoundTripTests
{
    [Theory]
    [MemberData(nameof(XmlSamples.Names), MemberType = typeof(XmlSamples))]
    public void A_value_object_round_trips_through_XmlSerializer(string name) => XmlSamples.All[name].RoundTripsThroughXmlSerializer();

    [Theory]
    [MemberData(nameof(XmlSamples.Names), MemberType = typeof(XmlSamples))]
    public void A_value_object_round_trips_through_DataContractSerializer(string name)
        => XmlSamples.All[name].RoundTripsThroughDataContractSerializer();

    [Theory]
    [MemberData(nameof(XmlSamples.Names), MemberType = typeof(XmlSamples))]
    public void A_nullable_value_object_holds_null_as_nil(string name) => XmlSamples.All[name].HoldsNullAsNil();

    [Theory]
    [MemberData(nameof(XmlSamples.Names), MemberType = typeof(XmlSamples))]
    public void A_value_object_reads_the_documents_written_while_it_was_its_primitive(string name)
        => XmlSamples.All[name].ReadsTheDocumentsItsPrimitiveWrote();

    [Theory]
    [MemberData(nameof(XmlSamples.Names), MemberType = typeof(XmlSamples))]
    public void A_value_object_round_trips_as_the_root_of_a_document(string name) => XmlSamples.All[name].RoundTripsAsTheRootElement();

    [Fact]
    public void A_real_keeps_its_infinities_its_not_a_number_and_its_negative_zero()
    {
        double[] values = [double.PositiveInfinity, double.NegativeInfinity, double.NaN, -0.0];

        var written = XmlDocuments.Serialize(new XmlHolder<Reading> { Many = [.. values.Select(Reading.Create)] });

        written.Should().Contain(">INF<").And.Contain(">-INF<").And.Contain(">NaN<").And.Contain(">-0<");
        XmlDocuments.Deserialize<XmlHolder<Reading>>(written).Many.Select(static reading => reading.Value)
            .Should().Equal(values, static (left, right) => left.Equals(right) && double.IsNegative(left) == double.IsNegative(right));
    }

    [Fact]
    public void A_single_precision_real_keeps_its_infinities_its_not_a_number_and_its_negative_zero()
    {
        float[] values = [float.PositiveInfinity, float.NegativeInfinity, float.NaN, -0.0f];

        var written = XmlDocuments.Serialize(new XmlHolder<Gain> { Many = [.. values.Select(Gain.Create)] });

        // The lexical forms of an xs:float, which XmlSerializer writes for a bare float, never .NET's "Infinity".
        XDocument.Parse(written).Descendants().Where(static element => element.Name.LocalName == "Gain").Select(static element => element.Value).Should().Equal("INF", "-INF", "NaN", "-0");
        XmlDocuments.Deserialize<XmlHolder<Gain>>(written).Many.Select(static gain => gain.Value)
            .Should().Equal(values, static (left, right) => left.Equals(right) && float.IsNegative(left) == float.IsNegative(right));
    }

    [Fact]
    public void A_value_object_reads_the_forms_XmlSerializer_reads_its_primitive_in()
    {
        Read<Insured>("1").Should().Be(Insured.Create(true));
        Read<Insured>("0").Should().Be(Insured.Create(false));
        Read<ParcelCount>(" 7 ").Should().Be(ParcelCount.Create(7));
        Read<TransitTime>("P1D").Should().Be(TransitTime.Create(TimeSpan.FromDays(1)));
        Read<ShippingDate>(" 2026-10-03 ").Should().Be(ShippingDate.Create(new DateOnly(2026, 10, 3)));
        Read<PickupTime>(" 09:15:00 ").Should().Be(PickupTime.Create(new TimeOnly(9, 15)));
        Read<ScannedAt>("2026-10-03T09:00:00").Value.Kind.Should().Be(DateTimeKind.Unspecified);

        // XmlSerializer has no form for a 128-bit integer: it is read as an xs:integer, with its sign and white space.
        Read<CustomsBalance>(" 5 ").Should().Be(CustomsBalance.Create(5));
        Read<CustomsBalance>("+5").Should().Be(CustomsBalance.Create(5));
        Read<TariffFingerprint>(" 5 ").Should().Be(TariffFingerprint.Create(5));
        Read<TariffFingerprint>("+5").Should().Be(TariffFingerprint.Create(5));
        Read<Remark>("  fragile  ").Should().Be(Remark.Create("  fragile  "), "text is the value as it is, for a type that normalizes nothing");

        static T Read<T>(string text)
            where T : struct
            => XmlDocuments.Deserialize<XmlHolder<T>>($"<Holder><Value>{text}</Value></Holder>").Value;
    }

    [Fact]
    public void Reading_normalizes_the_text_as_creating_the_value_object_does()
    {
        XmlDocuments.Deserialize<XmlHolder<TrackingCode>>("<Holder><Value>  ab99  </Value></Holder>").Value
            .Should().Be(TrackingCode.Create("AB99"));
    }

    /// <summary>
    /// The serializers read into an instance of their own. Called on a variable through a constrained call, as only
    /// code written for it does, <c>ReadXml</c> changes that variable: the one way a readonly value object changes, which
    /// its documentation states.
    /// </summary>
    [Fact]
    public void ReadXml_called_on_a_variable_changes_that_variable()
    {
        var count = ParcelCount.Create(1);
        using var reader = XmlReader.Create(new StringReader("<Count>42</Count>"));
        reader.MoveToContent();

        ReadInto(ref count, reader);

        count.Should().Be(ParcelCount.Create(42));

        static void ReadInto<T>(ref T target, XmlReader reader)
            where T : IXmlSerializable
            => target.ReadXml(reader);
    }

    [Fact]
    public void A_value_object_that_implements_IXmlSerializable_itself_keeps_its_own_implementation()
    {
        var written = XmlDocuments.Serialize(new XmlHolder<CarrierNote> { Value = CarrierNote.Create("handle with care") });

        XmlDocuments.TextOf(written, "Value").Should().Be("note:handle with care");
        XmlDocuments.Deserialize<XmlHolder<CarrierNote>>(written).Value.Should().Be(CarrierNote.Create("handle with care"));
        typeof(CarrierNote).GetMethod("GetXmlSchema").Should().BeNull();
    }

    [Fact]
    public void A_value_object_written_by_hand_round_trips_through_ValueObjectXml()
    {
        var weight = HandWrittenXml<int, WeightRules>.Create(12);

        var written = XmlDocuments.Serialize(new XmlHolder<HandWrittenXml<int, WeightRules>> { Value = weight });
        var contract = XmlDocuments.WriteContract(new XmlHolder<HandWrittenXml<int, WeightRules>> { Value = weight });

        XmlDocuments.TextOf(written, "Value").Should().Be("12");
        XmlDocuments.Deserialize<XmlHolder<HandWrittenXml<int, WeightRules>>>(written).Value.Should().Be(weight);
        XmlDocuments.ReadContract<XmlHolder<HandWrittenXml<int, WeightRules>>>(contract).Value.Should().Be(weight);
    }
}
