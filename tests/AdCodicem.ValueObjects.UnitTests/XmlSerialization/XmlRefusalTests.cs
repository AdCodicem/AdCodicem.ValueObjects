using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Linq;
using AdCodicem.ValueObjects.Fixtures.XmlSerialization;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.XmlSerialization;

/// <summary>
/// What a value object of an assembly marked <c>[assembly: ValueObjectXmlSerialization]</c> refuses, read or written:
/// an <see cref="XmlException"/> naming the type and the rule, never the text, carrying the rule's code, inside the
/// exception each serializer wraps it in.
/// </summary>
public class XmlRefusalTests
{
    public static TheoryData<string, string, string> Refused => new()
    {
        { nameof(ParcelCount), "500", ValueObjectErrorCodes.OutOfRange },
        { nameof(ParcelCount), "abc", ValueObjectErrorCodes.NotParsable },
        { nameof(ParcelCount), string.Empty, ValueObjectErrorCodes.NotParsable },
        { nameof(PalletCount), "300", ValueObjectErrorCodes.NotParsable },
        { nameof(TrackingCode), "XY1", ValueObjectErrorCodes.TooShort },
        { nameof(TrackingCode), "XY12345678901", ValueObjectErrorCodes.TooLong },
        { nameof(TrackingCode), "1234XY", ValueObjectErrorCodes.InvalidFormat },
        { nameof(TrackingCode), string.Empty, ValueObjectErrorCodes.Required },
        { nameof(ParcelState), "archived", ValueObjectErrorCodes.NotAKnownValue },
        { nameof(ConsignmentId), "00000000-0000-0000-0000-000000000000", ValueObjectErrorCodes.Required },
        { nameof(ConsignmentId), "not-a-guid", ValueObjectErrorCodes.NotParsable },
        { nameof(DeliveredAt), "9999-12-31T23:59:59-14:00", ValueObjectErrorCodes.NotParsable },
        { nameof(HandlingClass), "70000", ValueObjectErrorCodes.NotParsable },
        { nameof(TransitTime), "1:30", ValueObjectErrorCodes.NotParsable },
        { nameof(CustomsBalance), "-1000001", ValueObjectErrorCodes.OutOfRange },
        { nameof(CustomsBalance), "1e3", ValueObjectErrorCodes.NotParsable },
        { nameof(PickupTime), "23:00:00", ValueObjectErrorCodes.OutOfRange },
        { nameof(PickupTime), "9:15", ValueObjectErrorCodes.NotParsable },
        { nameof(ShippingDate), "03/10/2026", ValueObjectErrorCodes.NotParsable },
        { nameof(ShipmentId), "acc_000000000000000000000", AdCodicem.ValueObjects.Identifiers.IdentifierErrorCodes.InvalidPrefix },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void XmlSerializer_refuses_a_value_with_the_rule_s_code_and_never_quotes_it(string type, string text, string code)
    {
        var xml = $"<Holder>\n  <Value>{text}</Value>\n</Holder>";

        var exception = Reader(type)(xml, true).Should().Throw<InvalidOperationException>().Which;

        exception.Message.Should().StartWith("There is an error in XML document (2, ");
        var refusal = exception.InnerException.Should().BeOfType<XmlException>().Which;
        refusal.Message.Should().StartWith($"The value is not a valid {type}: ");
        refusal.LineNumber.Should().Be(2);
        refusal.LinePosition.Should().Be(4);
        if (text.Length > 0)
        {
            refusal.Message.Should().NotContain(text);
        }

        ValueObjectErrors.TryGetCode(exception, out var found).Should().BeTrue();
        found.Should().Be(code);
    }

    [Theory]
    [MemberData(nameof(Refused))]
    public void DataContractSerializer_refuses_a_value_with_the_rule_s_code_and_never_quotes_it(string type, string text, string code)
    {
        var xml = $"<Holder xmlns=\"{XmlDocuments.HolderNamespace}\"><Value>{text}</Value></Holder>";

        var exception = Reader(type)(xml, false).Should().Throw<SerializationException>().Which;

        exception.InnerException.Should().BeOfType<XmlException>().Which.Message.Should().StartWith($"The value is not a valid {type}: ");
        if (text.Length > 0)
        {
            exception.ToString().Should().NotContain(text);
        }

        ValueObjectErrors.TryGetCode(exception, out var found).Should().BeTrue();
        found.Should().Be(code);
    }

    /// <summary>
    /// The message says what the text is not: the XSD type of the underlying value, or a GUID, which <c>xs:string</c>
    /// would not tell apart from any text.
    /// </summary>
    [Theory]
    [InlineData(nameof(ConsignmentId), "not-a-guid", "The value is not a valid ConsignmentId: the text is not a valid GUID.")]
    [InlineData(nameof(ParcelCount), "abc", "The value is not a valid ParcelCount: the text is not a valid xs:int.")]
    [InlineData(nameof(HandlingClass), "70000", "The value is not a valid HandlingClass: the text is not a valid xs:unsignedShort.")]
    public void Text_not_of_the_underlying_type_is_refused_naming_the_form_it_misses(string type, string text, string message)
    {
        var exception = Reader(type)($"<Holder><Value>{text}</Value></Holder>", true).Should().Throw<InvalidOperationException>().Which;

        exception.InnerException.Should().BeOfType<XmlException>().Which.Message.Should().Be($"{message} Line 1, position 10.");
    }

    [Fact]
    public void An_element_holding_elements_holds_no_value()
    {
        var xml = "<Holder><Value><DateTime>2026-10-03T07:00:00Z</DateTime><OffsetMinutes>120</OffsetMinutes></Value></Holder>";

        var exception = FluentActions.Invoking(() => XmlDocuments.Deserialize<XmlHolder<DeliveredAt>>(xml))
            .Should().Throw<InvalidOperationException>().Which;

        var refusal = exception.InnerException.Should().BeOfType<XmlException>().Which;
        refusal.Message.Should().StartWith("The value is not a valid DeliveredAt: the element holds no text.");
        refusal.InnerException.Should().BeOfType<XmlException>();
        ValueObjectErrors.TryGetCode(exception, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// <c>XmlSerializer</c> hands an element marked nil to the type when its member cannot be null: a null, refused as
    /// required whatever the underlying type, even a text the type may leave empty. <c>DataContractSerializer</c> refuses
    /// it before the type reads anything, in its own terms and without a code.
    /// </summary>
    [Theory]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData(" true ")]
    public void Nil_on_a_member_that_is_not_nullable_is_a_null(string flag)
    {
        var xml = $"<Holder xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n  <Value xsi:nil=\"{flag}\" />\n</Holder>";

        var count = FluentActions.Invoking(() => XmlDocuments.Deserialize<XmlHolder<ParcelCount>>(xml)).Should().Throw<InvalidOperationException>().Which;

        var refusal = count.InnerException.Should().BeOfType<XmlException>().Which;
        refusal.Message.Should().Be("The value is not a valid ParcelCount: the element is nil. Line 2, position 4.");
        ValueObjectErrors.TryGetCode(count, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.Required);
        Code(() => XmlDocuments.Deserialize<XmlHolder<Remark>>(xml)).Should().Be(ValueObjectErrorCodes.Required);
    }

    [Fact]
    public void DataContractSerializer_refuses_nil_on_a_member_that_is_not_nullable_itself()
    {
        var xml = $"<Holder xmlns=\"{XmlDocuments.HolderNamespace}\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><Value i:nil=\"true\" /></Holder>";

        var count = FluentActions.Invoking(() => XmlDocuments.ReadContract<XmlHolder<ParcelCount>>(xml)).Should().Throw<SerializationException>().Which;
        var remark = FluentActions.Invoking(() => XmlDocuments.ReadContract<XmlHolder<Remark>>(xml)).Should().Throw<SerializationException>().Which;

        count.Message.Should().Contain($"ValueType '{typeof(ParcelCount).FullName}' cannot be null.");
        remark.Message.Should().Contain($"ValueType '{typeof(Remark).FullName}' cannot be null.");
        ValueObjectErrors.TryGetCode(count, out _).Should().BeFalse();
        ValueObjectErrors.TryGetCode(remark, out _).Should().BeFalse();
    }

    /// <summary>An <c>xsi:nil</c> that is false, or not an <c>xs:boolean</c>, leaves the element to be read as it is.</summary>
    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("maybe")]
    public void Nil_that_does_not_mark_the_element_nil_leaves_its_text_to_be_read(string flag)
    {
        var xml = $"<Holder xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><Value xsi:nil=\"{flag}\">7</Value></Holder>";

        XmlDocuments.Deserialize<XmlHolder<ParcelCount>>(xml).Value.Should().Be(ParcelCount.Create(7));
    }

    [Fact]
    public void An_empty_element_is_the_empty_string_a_value_object_that_allows_it_holds()
    {
        XmlDocuments.Deserialize<XmlHolder<Remark>>("<Holder><Value /></Holder>").Value.Should().Be(Remark.Create(string.Empty));
    }

    /// <summary>
    /// A missing element is never read, so nothing refuses it: the member keeps the default instance the document's
    /// constructor left, as a missing primitive keeps its zero. <c>DataMember(IsRequired = true)</c> refuses it, in
    /// DataContractSerializer's own terms.
    /// </summary>
    [Fact]
    public void A_missing_element_leaves_the_member_as_the_holder_built_it()
    {
        var back = XmlDocuments.Deserialize<XmlHolder<ParcelCount>>("<Holder />");

        ((IValueObject<ParcelCount, int>)back.Value).IsDefault.Should().BeTrue();

        var required = FluentActions.Invoking(() => XmlDocuments.ReadContract<RequiredParcels>($"<RequiredParcels xmlns=\"{XmlDocuments.HolderNamespace}\" />"))
            .Should().Throw<SerializationException>().Which;
        ValueObjectErrors.TryGetCode(required, out _).Should().BeFalse();
    }

    [Fact]
    public void A_reader_without_line_information_gives_none()
    {
        using var reader = XDocument.Parse("<Count>500</Count>").CreateReader();
        reader.MoveToContent();

        var exception = FluentActions.Invoking(() => ValueObjectXml.Read<ParcelCount, int>(reader)).Should().Throw<XmlException>().Which;

        exception.LineNumber.Should().Be(0);
        exception.Message.Should().Be("The value is not a valid ParcelCount: The value must be less than or equal to 100.");
    }

    [Fact]
    public void A_value_object_written_by_hand_refusing_a_value_without_a_code_is_not_parsable()
    {
        var xml = "<Holder><Value>-1</Value></Holder>";
        var over = "<Holder><Value>501</Value></Holder>";

        Code(() => XmlDocuments.Deserialize<XmlHolder<HandWrittenXml<int, WeightRules>>>(xml)).Should().Be(ValueObjectErrorCodes.NotParsable);
        Code(() => XmlDocuments.Deserialize<XmlHolder<HandWrittenXml<int, WeightRules>>>(over)).Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void An_underlying_type_XML_has_no_form_for_is_not_supported()
    {
        using var reader = XmlReader.Create(new StringReader("<Link>https://example.com</Link>"));
        reader.MoveToContent();
        using var text = new StringWriter();
        using var writer = XmlWriter.Create(text);
        writer.WriteStartElement("Link");

        FluentActions.Invoking(() => ValueObjectXml.Read<HandWrittenXml<Uri, LinkRules>, Uri>(reader)).Should().Throw<NotSupportedException>()
            .WithMessage("'System.Uri' is not an underlying type a value object is written in XML as.");
        FluentActions.Invoking(() => ValueObjectXml.Write<HandWrittenXml<Uri, LinkRules>, Uri>(writer, HandWrittenXml<Uri, LinkRules>.Create(new Uri("https://example.com"))))
            .Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => ValueObjectXml.ProvideSchema<HandWrittenXml<Uri, LinkRules>, Uri>(new System.Xml.Schema.XmlSchemaSet(), null))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void The_bridges_refuse_a_null_reader_writer_or_schema_set()
    {
        FluentActions.Invoking(() => ValueObjectXml.Read<ParcelCount, int>(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ValueObjectXml.Write<ParcelCount, int>(null!, ParcelCount.Create(1))).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ValueObjectXml.ProvideSchema<ParcelCount, int>(null!, null)).Should().Throw<ArgumentNullException>();
    }

#pragma warning disable VO0010 // the default instances a document's constructor leaves, which the writers must refuse
    [Fact]
    public void A_default_instance_its_type_refuses_is_refused_on_write_by_both_serializers()
    {
        var holder = new XmlHolder<ParcelCount>();

        var written = FluentActions.Invoking(() => XmlDocuments.Serialize(holder)).Should().Throw<InvalidOperationException>().Which;
        var contract = FluentActions.Invoking(() => XmlDocuments.WriteContract(holder)).Should().Throw<SerializationException>().Which;

        written.InnerException.Should().BeOfType<XmlException>().Which.Message
            .Should().Be("The value to write is not a valid ParcelCount: The value must be greater than or equal to 1.");
        ValueObjectErrors.TryGetCode(written, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
        ValueObjectErrors.TryGetCode(contract, out code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
        Code(() => XmlDocuments.Serialize(new XmlHolder<TrackingCode>())).Should().Be(ValueObjectErrorCodes.Required);
        Code(() => XmlDocuments.Serialize(new XmlHolder<ConsignmentId>())).Should().Be(ValueObjectErrorCodes.Required);
    }

    [Fact]
    public void A_default_instance_its_type_accepts_is_written()
    {
        XmlDocuments.TextOf(XmlDocuments.Serialize(new XmlHolder<ManifestSequence>()), "Value").Should().Be("0");
        XmlDocuments.TextOf(XmlDocuments.Serialize(new XmlHolder<Remark>()), "Value").Should().BeEmpty();
    }
#pragma warning restore VO0010

    private static string? Code(Action action)
    {
        var exception = action.Should().Throw<Exception>().Which;
        ValueObjectErrors.TryGetCode(exception, out var code);
        return code;
    }

    /// <summary>Reads a document, through XmlSerializer or DataContractSerializer, into the holder of the value object a type name names.</summary>
    private static Func<string, bool, Action> Reader(string type) => type switch
    {
        nameof(ParcelCount) => Read<ParcelCount>,
        nameof(PalletCount) => Read<PalletCount>,
        nameof(TrackingCode) => Read<TrackingCode>,
        nameof(ParcelState) => Read<ParcelState>,
        nameof(ConsignmentId) => Read<ConsignmentId>,
        nameof(DeliveredAt) => Read<DeliveredAt>,
        nameof(HandlingClass) => Read<HandlingClass>,
        nameof(TransitTime) => Read<TransitTime>,
        nameof(CustomsBalance) => Read<CustomsBalance>,
        nameof(PickupTime) => Read<PickupTime>,
        nameof(ShippingDate) => Read<ShippingDate>,
        _ => Read<ShipmentId>,
    };

    private static Action Read<T>(string xml, bool serializer)
        where T : struct
        => serializer
            ? () => XmlDocuments.Deserialize<XmlHolder<T>>(xml)
            : () => XmlDocuments.ReadContract<XmlHolder<T>>(xml);

    [DataContract(Name = "RequiredParcels", Namespace = XmlDocuments.HolderNamespace)]
    public sealed class RequiredParcels
    {
        [DataMember(IsRequired = true)]
        public ParcelCount Count { get; set; }
    }
}
