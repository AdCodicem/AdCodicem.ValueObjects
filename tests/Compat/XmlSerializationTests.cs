using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// XmlSerializer and DataContractSerializer on the next major, over the value objects of a domain marked
/// <c>[assembly: ValueObjectXmlSerialization]</c>: each is written as its underlying value and read back through its rules.
/// </summary>
public sealed class XmlSerializationTests
{
    [Fact]
    public void A_document_of_value_objects_round_trips_through_both_serializers()
    {
        var shipment = new Shipment
        {
            Customer = CustomerId.Create(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e")),
            Account = Iban.Create("FR7630006000011234567890189"),
            Quantity = Quantity.Create(3),
            Amount = Amount.Create(12.5m),
            Country = CountryCode.Belgium,
            Born = BirthDate.Create(new DateOnly(1980, 5, 17)),
            Payment = PaymentId.New(),
            Order = Reference<PurchaseOrder>.Create("po-1"),
            Extra = null,
        };

        var written = Serialize(shipment);
        var contract = WriteContract(shipment);

        written.Should().Contain("<Account>FR7630006000011234567890189</Account>").And.Contain("<Quantity>3</Quantity>")
            .And.Contain("<Amount>12.50</Amount>").And.Contain("<Born>1980-05-17</Born>").And.Contain("<Order>PO-1</Order>")
            .And.Contain("<Extra xsi:nil=\"true\" />");
        Deserialize<Shipment>(written).Should().BeEquivalentTo(shipment);
        ReadContract<Shipment>(contract).Should().BeEquivalentTo(shipment);
    }

    [Fact]
    public void A_value_the_type_refuses_fails_the_read_with_the_rule_s_code()
    {
        var xml = "<Shipment><Quantity>500</Quantity></Shipment>";

        var exception = FluentActions.Invoking(() => Deserialize<Shipment>(xml)).Should().Throw<InvalidOperationException>().Which;

        exception.InnerException.Should().BeOfType<XmlException>().Which.Message.Should().NotContain("500");
        ValueObjectErrors.TryGetCode(exception, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void The_schema_DataContractSerializer_exports_describes_the_rules()
    {
        var exporter = new XsdDataContractExporter();

        exporter.Export(typeof(Shipment));
        exporter.Schemas.Compile();

        var types = exporter.Schemas.Schemas().Cast<XmlSchema>().SelectMany(static schema => schema.Items.OfType<XmlSchemaSimpleType>())
            .ToDictionary(static type => type.Name!, static type => (XmlSchemaSimpleTypeRestriction)type.Content!);
        types["Quantity"].BaseTypeName.Name.Should().Be("int");
        types["Quantity"].Facets.OfType<XmlSchemaMaxInclusiveFacet>().Single().Value.Should().Be("100");
        types["CountryCode"].Facets.OfType<XmlSchemaEnumerationFacet>().Select(static facet => facet.Value).Should().Equal("FR", "BE", "LU");
        types["ReferenceOfPurchaseOrder"].Facets.OfType<XmlSchemaMaxLengthFacet>().Single().Value.Should().Be("12");
    }

    private static string Serialize<T>(T value)
    {
        using var text = new StringWriter();
        using (var writer = XmlWriter.Create(text, new XmlWriterSettings { OmitXmlDeclaration = true }))
        {
            new XmlSerializer(typeof(T)).Serialize(writer, value);
        }

        return text.ToString();
    }

    private static T Deserialize<T>(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml));
        return (T)new XmlSerializer(typeof(T)).Deserialize(reader)!;
    }

    private static string WriteContract<T>(T value)
    {
        using var text = new StringWriter();
        using (var writer = XmlWriter.Create(text))
        {
            new DataContractSerializer(typeof(T)).WriteObject(writer, value);
        }

        return text.ToString();
    }

    private static T ReadContract<T>(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml));
        return (T)new DataContractSerializer(typeof(T)).ReadObject(reader)!;
    }
}

/// <summary>A document holding value objects of the domain, a generic one, an identifier and a nullable one among them.</summary>
public sealed class Shipment
{
    public CustomerId Customer { get; set; }

    public Iban Account { get; set; }

    public Quantity Quantity { get; set; }

    public Amount Amount { get; set; }

    public CountryCode Country { get; set; }

    public BirthDate Born { get; set; }

    public PaymentId Payment { get; set; }

    public Reference<PurchaseOrder> Order { get; set; }

    public Quantity? Extra { get; set; }
}
