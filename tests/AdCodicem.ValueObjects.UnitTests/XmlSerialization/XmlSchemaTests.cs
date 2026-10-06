using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using AdCodicem.ValueObjects.Fixtures.XmlSerialization;
using AdCodicem.ValueObjects.Fixtures.XmlSerialization.\u00C9conomie;
using AdCodicem.ValueObjects.Metadata;
using Billing = AdCodicem.ValueObjects.Fixtures.XmlSerialization.Billing;
using Freight = AdCodicem.ValueObjects.Fixtures.XmlSerialization.Freight;

namespace AdCodicem.ValueObjects.UnitTests.XmlSerialization;

/// <summary>
/// The schema provider of a value object of an assembly marked <c>[assembly: ValueObjectXmlSerialization]</c>: an
/// <c>xs:simpleType</c> restricting the XSD type of its underlying value with its rules, which both exporters reach.
/// </summary>
public class XmlSchemaTests
{
    private const string FixtureNamespace = "http://schemas.datacontract.org/2004/07/AdCodicem.ValueObjects.Fixtures.XmlSerialization";

    private const string GuidPattern = @"[\da-fA-F]{8}-[\da-fA-F]{4}-[\da-fA-F]{4}-[\da-fA-F]{4}-[\da-fA-F]{12}";

    public static TheoryData<string, string> Described => new()
    {
        { nameof(TrackingCode), "TrackingCode: string minLength=4 maxLength=12 pattern=[A-Z]{2}[0-9]{2,10} \"A tracking code: two letters, then digits, in upper case.\"" },
        { nameof(Remark), "Remark: string maxLength=40 \"A remark, which may be empty.\"" },
        { nameof(ConsignmentId), $"ConsignmentId: string pattern={GuidPattern} \"A consignment, never the empty identifier.\"" },
        { nameof(Insured), "Insured: boolean \"Whether a parcel is insured.\"" },
        { nameof(HandlingClass), "HandlingClass: unsignedShort minInclusive=65 maxInclusive=69 \"A handling class, from A to E.\"" },
        { nameof(TiltDegrees), "TiltDegrees: byte minInclusive=-10 maxInclusive=10 \"A tilt, in degrees.\"" },
        { nameof(PalletCount), "PalletCount: unsignedByte minInclusive=1 maxInclusive=30 \"A number of pallets.\"" },
        { nameof(DeckLevel), "DeckLevel: short minInclusive=-2 maxInclusive=20 \"A deck of a ship, below the waterline when negative.\"" },
        { nameof(DockDoor), "DockDoor: unsignedShort minInclusive=1 \"A dock door.\"" },
        { nameof(ParcelCount), "ParcelCount: int minInclusive=1 maxInclusive=100 \"A number of parcels, from 1 to 100.\"" },
        { nameof(ManifestSequence), "ManifestSequence: unsignedInt \"A sequence number in a manifest, which starts at zero.\"" },
        { nameof(GrossGrams), "GrossGrams: long minInclusive=0 \"A gross weight, in grams.\"" },
        { nameof(ContainerSerial), "ContainerSerial: unsignedLong \"The serial number of a container.\"" },

        // System.Xml holds an xs:integer in a decimal: a bound beyond it would refuse the type, so it is left out.
        { nameof(CustomsBalance), "CustomsBalance: integer minInclusive=-1000000 \"A customs balance, in the smallest unit of a currency, whose upper bound no decimal holds.\"" },
        { nameof(TariffFingerprint), "TariffFingerprint: integer minInclusive=0 \"The fingerprint of a tariff.\"" },
        { nameof(DemurrageBalance), "DemurrageBalance: integer maxInclusive=100 \"A demurrage balance, whose lower bound no decimal holds.\"" },

        // One known value beyond a decimal leaves the enumeration out, the bound that reads staying.
        { nameof(RiskBand), "RiskBand: integer minInclusive=0 \"A band of risk, one of whose values no decimal holds.\"" },
        { nameof(DeclaredValue), "DeclaredValue: decimal minInclusive=0 \"A declared value, never negative.\"" },
        { nameof(Heading), "Heading: double minInclusive=-180 maxInclusive=180 \"A heading, in degrees.\"" },
        { nameof(Reading), "Reading: double \"A sensor reading, which may be infinite or not a number.\"" },
        { nameof(FillRatio), "FillRatio: float minInclusive=0 maxInclusive=1 \"How full a container is.\"" },
        { nameof(Gain), "Gain: float \"The gain of a sensor, which may be infinite or not a number.\"" },
        { nameof(ShippingDate), "ShippingDate: date minInclusive=2000-01-01 \"A shipping date.\"" },
        { nameof(PickupTime), "PickupTime: time minInclusive=06:00:00 maxInclusive=22:00:00 \"A pickup time, during the day.\"" },
        { nameof(ScannedAt), "ScannedAt: dateTime minInclusive=2000-01-01T00:00:00 \"When a parcel was scanned.\"" },
        { nameof(DeliveredAt), "DeliveredAt: dateTime minInclusive=2000-01-01T00:00:00Z \"When a parcel was delivered.\"" },
        { nameof(TransitTime), "TransitTime: duration minInclusive=PT0S maxInclusive=P1D \"A transit time, up to a day.\"" },
        { nameof(ParcelState), "ParcelState: string enumeration=created enumeration=shipped enumeration=delivered \"The state of a parcel.\"" },
        { nameof(ServiceLevel), "ServiceLevel: int enumeration=1 enumeration=2 enumeration=3 \"A level of service.\"" },
        { "ShipmentTag", "ShipmentTagOfShipment: string maxLength=8 \"A tag put on something, by its owner's type.\"" },

        // A name XML cannot hold is encoded; a type argument nested in a generic type is named after it, then its own.
        { "ShipmentTag<int[]>", "ShipmentTagOfInt32_x005B__x005D_: string maxLength=8 \"A tag put on something, by its owner's type.\"" },
        { "ShipmentTag<Pallet<string>.Slot>", "ShipmentTagOfPallet.SlotOfString: string maxLength=8 \"A tag put on something, by its owner's type.\"" },
        { nameof(Depot.Bay), "Depot.Bay: string maxLength=3 \"A loading bay of a depot.\"" },
        { nameof(ShipmentId), "ShipmentId: string minLength=25 maxLength=25 pattern=shp_[0123456789abcdefghjkmnpqrstvwxyz]{21} \"A shipment's identifier.\"" },
    };

    [Theory]
    [MemberData(nameof(Described))]
    public void A_value_object_is_described_as_a_simple_type_restricted_by_its_rules(string name, string expected)
    {
        var schemas = new XmlSchemaSet();

        var type = XmlSamples.All[name].ProvideSchema(schemas);

        type.Namespace.Should().Be(FixtureNamespace);
        Describe(schemas, type).Should().Be(expected);
        Compile(schemas).Should().BeEmpty();
    }

    [Fact]
    public void The_fixture_opts_in_under_the_default_namespace()
    {
        var attribute = typeof(ParcelCount).Assembly.GetCustomAttribute<ValueObjectXmlSerializationAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Namespace.Should().BeNull();
        new ValueObjectXmlSerializationAttribute { Namespace = "urn:shipping" }.Namespace.Should().Be("urn:shipping");
    }

    [Fact]
    public void A_type_is_added_to_a_schema_set_once()
    {
        var schemas = new XmlSchemaSet();

        var first = ValueObjectXml.ProvideSchema<ParcelCount, int>(schemas, null);
        var second = ValueObjectXml.ProvideSchema<ParcelCount, int>(schemas, null);
        ValueObjectXml.ProvideSchema<PalletCount, byte>(schemas, null);

        second.Should().Be(first);
        schemas.Schemas(FixtureNamespace).Cast<XmlSchema>().SelectMany(static schema => schema.Items.OfType<XmlSchemaSimpleType>())
            .Select(static type => type.Name).Should().Equal("ParcelCount", "PalletCount");
        Compile(schemas).Should().BeEmpty();
    }

    /// <summary>
    /// The default namespace is the one <c>DataContractSerializer</c> gives the classes of the same CLR namespace, which it
    /// resolves as a relative URI, escaping what a URI cannot hold.
    /// </summary>
    [Fact]
    public void The_default_namespace_is_the_one_DataContractSerializer_derives_escaped_as_a_URI()
    {
        var type = Tariff.GetXmlSchema(new XmlSchemaSet());

        type.Namespace.Should().Be(FixtureNamespace + ".%C3%89conomie");
        type.Namespace.Should().Be(new XsdDataContractExporter().GetSchemaTypeName(typeof(Invoice)).Namespace);
    }

    [Fact]
    public void Two_value_objects_of_one_name_keep_a_type_each_under_the_default_namespaces()
    {
        var exporter = new XsdDataContractExporter();

        exporter.Export(typeof(Codes));

        Compile(exporter.Schemas).Should().BeEmpty();
        var billing = Billing.Code.GetXmlSchema(exporter.Schemas);
        var freight = Freight.Code.GetXmlSchema(exporter.Schemas);
        billing.Namespace.Should().Be(FixtureNamespace + ".Billing");
        freight.Namespace.Should().Be(FixtureNamespace + ".Freight");
        Describe(exporter.Schemas, billing).Should().StartWith("Code: string maxLength=3 ");
        Describe(exporter.Schemas, freight).Should().StartWith("Code: int minInclusive=1 maxInclusive=10 ");
    }

    /// <summary>
    /// A namespace named for the assembly puts value objects of every CLR namespace side by side, and their names leave
    /// the CLR namespace out: the second of one name is refused when its underlying type, its rules or its description
    /// differ, rather than described as the first.
    /// </summary>
    [Fact]
    public void A_second_value_object_of_one_name_under_one_namespace_is_refused_naming_both()
    {
        var schemas = new XmlSchemaSet();

        var billing = ValueObjectXml.ProvideSchema<Billing.Code, string>(schemas, "urn:shared");

        FluentActions.Invoking(() => ValueObjectXml.ProvideSchema<Freight.Code, int>(schemas, "urn:shared"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage(
                "The XML schema type 'Code' of namespace 'urn:shared' describes "
                + "'AdCodicem.ValueObjects.Fixtures.XmlSerialization.Billing.Code', whose rules differ from those of "
                + "'AdCodicem.ValueObjects.Fixtures.XmlSerialization.Freight.Code': give the value objects distinct names, "
                + "or leave the Namespace of [assembly: ValueObjectXmlSerialization] unset, so that each CLR namespace keeps "
                + "an XML namespace of its own.");
        Describe(schemas, billing).Should().Be("Code: string maxLength=3 \"A billing code, three characters at most.\"");
        Compile(schemas).Should().BeEmpty();
    }

    public static TheoryData<string> Namesakes => [nameof(Billing.Reference), nameof(Billing.Grade), nameof(Billing.Note)];

    /// <summary>Each pair differs by one thing alone: its underlying type, a bound, or its description.</summary>
    [Theory]
    [MemberData(nameof(Namesakes))]
    public void Value_objects_of_one_name_that_differ_by_one_thing_are_told_apart(string name)
    {
        var schemas = new XmlSchemaSet();
        var (billing, freight) = name switch
        {
            nameof(Billing.Reference) => (Shared<Billing.Reference, string>(), Shared<Freight.Reference, int>()),
            nameof(Billing.Grade) => (Shared<Billing.Grade, int>(), Shared<Freight.Grade, int>()),
            _ => (Shared<Billing.Note, string>(), Shared<Freight.Note, string>()),
        };

        billing(schemas);

        FluentActions.Invoking(() => freight(schemas)).Should().Throw<InvalidOperationException>()
            .WithMessage($"The XML schema type '{name}' of namespace 'urn:shared' describes '*.Billing.{name}', whose rules differ from those of '*.Freight.{name}': *");
    }

    /// <summary>
    /// Constructions of one generic value object over type arguments of one name share the name, and the rules: they
    /// share the type.
    /// </summary>
    [Fact]
    public void Value_objects_of_one_name_whose_rules_agree_share_a_type()
    {
        var schemas = new XmlSchemaSet();

        var first = ShipmentTag<Billing.Code>.GetXmlSchema(schemas);
        var second = ShipmentTag<Freight.Code>.GetXmlSchema(schemas);

        second.Should().Be(first);
        first.Name.Should().Be("ShipmentTagOfCode");
        schemas.Schemas().Cast<XmlSchema>().SelectMany(static schema => schema.Items.OfType<XmlSchemaSimpleType>())
            .Select(static type => type.Name).Should().Equal("ShipmentTagOfCode");
        Compile(schemas).Should().BeEmpty();
    }

    [Fact]
    public void A_type_of_that_name_the_set_already_holds_stands()
    {
        var schemas = new XmlSchemaSet();
        var own = new XmlSchema { TargetNamespace = FixtureNamespace };
        own.Items.Add(new XmlSchemaSimpleType
        {
            Name = nameof(ParcelCount),
            Content = new XmlSchemaSimpleTypeRestriction { BaseTypeName = new XmlQualifiedName("string", XmlSchema.Namespace) },
        });
        schemas.Add(own);

        var type = ValueObjectXml.ProvideSchema<ParcelCount, int>(schemas, null);

        type.Should().Be(new XmlQualifiedName(nameof(ParcelCount), FixtureNamespace));
        Describe(schemas, type).Should().Be("ParcelCount: string");
    }

    [Fact]
    public void A_namespace_puts_the_type_under_it()
    {
        var schemas = new XmlSchemaSet();

        var type = ValueObjectXml.ProvideSchema<ParcelCount, int>(schemas, "urn:shipping");

        type.Should().Be(new XmlQualifiedName("ParcelCount", "urn:shipping"));
        schemas.Schemas("urn:shipping").Count.Should().Be(1);
    }

    [Fact]
    public void A_schema_written_by_hand_keeps_only_the_rules_System_Xml_reads()
    {
        var schemas = new XmlSchemaSet();

        var weight = ValueObjectXml.ProvideSchema<HandWrittenXml<int, WeightRules>, int>(schemas, HandWrittenXml<int, WeightRules>.Namespace);
        var code = HandWrittenXml<string, CodeRules>.GetXmlSchema(schemas);

        // A bound no reader takes and known values of another type are left out; the bound that reads stays.
        Describe(schemas, weight).Should().Be("HandWrittenXmlOfInt32WeightRules: int maxInclusive=500 \"A weight, in kilograms.\"");

        // Lengths that contradict each other would refuse the whole type: the rules go, the description stays.
        Describe(schemas, code).Should().Be("HandWrittenXmlOfStringCodeRules: string \"A code no value meets.\"");
        code.Namespace.Should().Be("urn:handwritten");
        Compile(schemas).Should().BeEmpty();
    }

    [Fact]
    public void XmlSchemaExporter_refers_to_the_type_without_including_it()
    {
        var schemas = new XmlSchemas();
        var exporter = new XmlSchemaExporter(schemas);

        exporter.ExportTypeMapping(new XmlReflectionImporter().ImportTypeMapping(typeof(XmlHolder<ParcelCount>)));

        var written = Write(schemas.Cast<XmlSchema>());
        written.Should().Contain($"<xs:import namespace=\"{FixtureNamespace}\" />");
        written.Should().MatchRegex("name=\"Value\" xmlns:q\\d+=\"" + Regex.Escape(FixtureNamespace) + "\" type=\"q\\d+:ParcelCount\"");
        written.Should().NotContain("<xs:simpleType name=\"ParcelCount\"");
    }

    [Fact]
    public void XsdDataContractExporter_includes_the_type_with_its_documentation()
    {
        var exporter = new XsdDataContractExporter();

        exporter.Export(typeof(XmlHolder<ParcelState>));

        Compile(exporter.Schemas).Should().BeEmpty();
        var written = Write(exporter.Schemas.Schemas(FixtureNamespace).Cast<XmlSchema>());
        written.Should().Contain("<xs:simpleType name=\"ParcelState\">");
        written.Should().Contain("<xs:enumeration value=\"delivered\" />");
        written.Should().Contain("<xs:documentation>The state of a parcel.</xs:documentation>");
    }

    public static TheoryData<string, string, string> Invalid => new()
    {
        { nameof(ParcelCount), "7", "500" },
        { nameof(ParcelState), "shipped", "archived" },
        { nameof(TrackingCode), "AB1234", "AB12CD" },
        { nameof(ShippingDate), "2026-10-03", "1999-12-31" },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public void A_document_DataContractSerializer_writes_validates_against_the_schema_it_exports(string name, string valid, string invalid)
    {
        var sample = XmlSamples.All[name];
        var exporter = new XsdDataContractExporter();
        exporter.Export(sample.HolderType);
        var document = $"<Holder xmlns=\"{XmlDocuments.HolderNamespace}\"><Value>{valid}</Value></Holder>";

        Validate(exporter.Schemas, document).Should().BeEmpty();
        Validate(exporter.Schemas, document.Replace($">{valid}<", $">{invalid}<", StringComparison.Ordinal)).Should().ContainSingle();
    }

    [DataContract(Name = "Codes", Namespace = XmlDocuments.HolderNamespace)]
    public sealed class Codes
    {
        [DataMember(Order = 1)]
        public Billing.Code BillingCode { get; set; }

        [DataMember(Order = 2)]
        public Freight.Code FreightCode { get; set; }
    }

    private static Func<XmlSchemaSet, XmlQualifiedName> Shared<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
        => static schemas => ValueObjectXml.ProvideSchema<TSelf, TValue>(schemas, "urn:shared");

    private static string Describe(XmlSchemaSet schemas, XmlQualifiedName name)
    {
        var type = schemas.Schemas(name.Namespace).Cast<XmlSchema>()
            .SelectMany(static schema => schema.Items.OfType<XmlSchemaSimpleType>())
            .Single(type => type.Name == name.Name);
        var restriction = (XmlSchemaSimpleTypeRestriction)type.Content!;
        var parts = new List<string> { $"{type.Name}: {restriction.BaseTypeName.Name}" };
        parts.AddRange(restriction.Facets.Cast<XmlSchemaFacet>().Select(static facet => $"{FacetName(facet)}={facet.Value}"));
        if (restriction.Annotation?.Items.OfType<XmlSchemaDocumentation>().SingleOrDefault() is { Markup: [{ } text] })
        {
            parts.Add($"\"{text.Value}\"");
        }

        return string.Join(' ', parts);
    }

    private static string FacetName(XmlSchemaFacet facet)
    {
        var name = facet.GetType().Name["XmlSchema".Length..^"Facet".Length];
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static List<string> Compile(XmlSchemaSet schemas)
    {
        var errors = new List<string>();
        schemas.ValidationEventHandler += (_, error) => errors.Add(error.Message);
        schemas.Compile();
        return errors;
    }

    private static List<string> Validate(XmlSchemaSet schemas, string document)
    {
        var errors = new List<string>();
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = schemas };
        settings.ValidationEventHandler += (_, error) => errors.Add(error.Message);
        using var reader = XmlReader.Create(new StringReader(document), settings);
        while (reader.Read())
        {
        }

        return errors;
    }

    private static string Write(IEnumerable<XmlSchema> schemas)
    {
        using var text = new StringWriter();
        foreach (var schema in schemas)
        {
            schema.Write(text);
        }

        return text.ToString();
    }
}
