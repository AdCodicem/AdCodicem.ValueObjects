using AdCodicem.ValueObjects.ProtobufNet;
using ProtoBuf;
using ProtoBuf.Meta;
using static AdCodicem.ValueObjects.UnitTests.BinarySerialization.ProtoWire;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// The <c>.proto</c> protobuf-net exports for messages holding value objects, beside the same messages over the
/// primitives: the names and imports the package gives the value objects, and the differences it leaves, pinned, so that a
/// protobuf-net release that changes one shows here.
/// </summary>
public sealed class ProtoSchemaTests
{
    /// <summary>
    /// A message holding a value object of each type protobuf-net writes is exported, at each level, with the fields and
    /// the imports of the same message over the primitives.
    /// </summary>
    [Fact]
    public void A_value_object_of_each_type_is_exported_as_the_scalar_of_its_underlying_type_with_its_import()
    {
        foreach (var level in Levels)
        {
            var exported = Model(level).AddValueObjects(typeof(Iban).Assembly).GetSchema(typeof(EveryProtoScalar), ProtoSyntax.Proto3);
            var plain = Model(level).GetSchema(typeof(PlainProtoScalars), ProtoSyntax.Proto3);

            Lines(exported).Where(Field).Should().Equal(Lines(plain).Where(Field), "at {0}", level);
            Lines(exported).Should().NotContain(line => line.StartsWith("message ", StringComparison.Ordinal) && !line.StartsWith("message EveryProtoScalar", StringComparison.Ordinal));
        }

        Lines(Model().AddValueObjects(typeof(Iban).Assembly).GetSchema(typeof(EveryProtoScalar), ProtoSyntax.Proto3))
            .Should().Contain(["import \"protobuf-net/bcl.proto\";", ".bcl.Guid Customer = 2;", ".bcl.DateTime Recorded = 4;"]);
        Lines(Model(CompatibilityLevel.Level240).AddValueObjects(typeof(Iban).Assembly).GetSchema(typeof(EveryProtoScalar), ProtoSyntax.Proto3))
            .Should().Contain(["import \"google/protobuf/timestamp.proto\";", "import \"google/protobuf/duration.proto\";", ".google.protobuf.Timestamp Recorded = 4;"]);
        Lines(Model(CompatibilityLevel.Level300).AddValueObjects(typeof(Iban).Assembly).GetSchema(typeof(EveryProtoScalar), ProtoSyntax.Proto3))
            .Should().Contain(["string Customer = 2;", "string Amount = 3;"]);
    }

    /// <summary>
    /// protobuf-net guesses the package from the namespaces of the types it writes as messages, and counts the value
    /// objects among them: with value objects of another namespace than the message, the <c>package</c> line goes, unless
    /// the options name the package.
    /// </summary>
    [Fact]
    public void The_package_line_needs_naming_when_the_value_objects_live_in_another_namespace()
    {
        var model = Model().AddValueObjects(typeof(Iban).Assembly);

        Lines(Model().GetSchema(typeof(PlainProtoScalars), ProtoSyntax.Proto3))
            .Should().Contain($"package {typeof(PlainProtoScalars).Namespace};");
        Lines(model.GetSchema(typeof(EveryProtoScalar), ProtoSyntax.Proto3)).Should().NotContain(line => line.StartsWith("package ", StringComparison.Ordinal));
        Lines(model.GetSchema(new SchemaGenerationOptions { Types = { typeof(EveryProtoScalar) }, Syntax = ProtoSyntax.Proto3, Package = "orders" }))
            .Should().Contain("package orders;");
    }

    /// <summary>
    /// A dictionary of value objects is exported as a list of entry messages rather than a <c>map</c>, the same on the
    /// wire, and two such dictionaries over other value objects of the same scalars as two messages of one name, which
    /// protoc refuses.
    /// </summary>
    [Fact]
    public void A_dictionary_of_value_objects_is_exported_as_entry_messages_rather_than_a_map()
    {
        var exported = Lines(Model().AddValueObjects(typeof(Iban).Assembly).GetSchema(typeof(ProtoStock), ProtoSyntax.Proto3));
        var plain = Lines(Model().GetSchema(typeof(PlainProtoStock), ProtoSyntax.Proto3));

        exported.Should().Contain(["repeated KeyValuePair_string_int32 Stock = 1;", "repeated KeyValuePair_string_int32 Reserved = 2;"]);
        exported.Count(line => line == "message KeyValuePair_string_int32 {").Should().Be(2);
        plain.Should().Contain(["map<string,int32> Stock = 1;", "map<string,int32> Reserved = 2;"]);
    }

    /// <summary>
    /// A list of numeric value objects is exported without the <c>[packed = false]</c> protobuf-net writes for a list of
    /// numbers, though neither is packed on the wire.
    /// </summary>
    [Fact]
    public void A_list_of_numeric_value_objects_is_exported_without_packed_false()
    {
        Lines(Model().AddValueObjects(typeof(Iban).Assembly).GetSchema(typeof(Holder<List<Quantity>>), ProtoSyntax.Proto3))
            .Should().Contain("repeated int32 Value = 1;");
        Lines(Model().GetSchema(typeof(Holder<List<short>>), ProtoSyntax.Proto3))
            .Should().Contain("repeated int32 Value = 1 [packed = false];");
    }

    /// <summary>
    /// A member's <see cref="DataFormat"/> never reaches a value object, which is exported as the member over the primitive
    /// without it is: <c>int32</c> where a <c>ZigZag</c> member over the primitive says <c>sint32</c>, a <c>.bcl.DateTime</c>
    /// where a <c>WellKnown</c> one says <c>.google.protobuf.Timestamp</c>.
    /// </summary>
    [Fact]
    public void A_member_s_DataFormat_never_reaches_a_value_object_in_the_export()
    {
        var exported = Lines(Model().AddValueObjects(typeof(Iban).Assembly).GetSchema(typeof(ProtoFormatted), ProtoSyntax.Proto3));
        var unformatted = Lines(Model().GetSchema(typeof(PlainProtoUnformatted), ProtoSyntax.Proto3));
        var formatted = Lines(Model().GetSchema(typeof(PlainProtoFormatted), ProtoSyntax.Proto3));

        exported.Where(Field).Should().Equal(unformatted.Where(Field));
        exported.Should().Contain(["int32 Quantity = 1;", "int32 Page = 2;", ".bcl.DateTime Recorded = 3;"]);
        formatted.Should().Contain(["sint32 Quantity = 1;", "sfixed32 Page = 2;", ".google.protobuf.Timestamp Recorded = 3;"]);
    }

    [Fact]
    public void Each_underlying_type_is_named_as_protobuf_net_names_it_at_each_level()
    {
        ProtoScalars.Of(typeof(string), CompatibilityLevel.Level200).Should().Be(("string", (string?)null));
        ProtoScalars.Of(typeof(Uri), CompatibilityLevel.Level300).Should().Be(("string", (string?)null));
        ProtoScalars.Of(typeof(decimal), CompatibilityLevel.Level240).Should().Be((".bcl.Decimal", "protobuf-net/bcl.proto"));
        ProtoScalars.Of(typeof(decimal), CompatibilityLevel.Level300).Should().Be(("string", (string?)null));
        ProtoScalars.Of(typeof(TimeSpan), CompatibilityLevel.Level200).Should().Be((".bcl.TimeSpan", "protobuf-net/bcl.proto"));
        ProtoScalars.Of(typeof(TimeSpan), CompatibilityLevel.Level240).Should().Be((".google.protobuf.Duration", "google/protobuf/duration.proto"));
        ProtoScalars.Of(typeof(Half), CompatibilityLevel.Level200).Should().BeNull();
    }

    /// <summary>
    /// Splits an export into its lines, without the comments protobuf-net adds, a default value it could not write for a
    /// primitive among them.
    /// </summary>
    private static string[] Lines(string schema)
        => [.. schema.Split('\n').Select(line => line.Split(" //", 2)[0].Trim()).Where(line => line.Length > 0)];

    private static bool Field(string line) => line.StartsWith("import ", StringComparison.Ordinal) || line.Contains(" = ", StringComparison.Ordinal);
}

/// <summary>A message holding a value object over each type protobuf-net writes.</summary>
[ProtoContract]
public sealed class EveryProtoScalar
{
    [ProtoMember(1)]
    public Iban Account { get; set; }

    [ProtoMember(2)]
    public CustomerId Customer { get; set; }

    [ProtoMember(3)]
    public Amount Amount { get; set; }

    [ProtoMember(4)]
    public RecordedAt Recorded { get; set; }

    [ProtoMember(5)]
    public Duration Took { get; set; }

    [ProtoMember(6)]
    public Quantity Quantity { get; set; }

    [ProtoMember(7)]
    public Grade Grade { get; set; }

    [ProtoMember(8)]
    public Consent Consent { get; set; }

    [ProtoMember(9)]
    public Adjustment Adjustment { get; set; }

    [ProtoMember(10)]
    public Score Score { get; set; }

    [ProtoMember(11)]
    public Port Port { get; set; }

    [ProtoMember(12)]
    public PageNumber Page { get; set; }

    [ProtoMember(13)]
    public SequenceNumber Sequence { get; set; }

    [ProtoMember(14)]
    public FileSize Size { get; set; }

    [ProtoMember(15)]
    public ByteCount Bytes { get; set; }

    [ProtoMember(16)]
    public Latitude Latitude { get; set; }

    [ProtoMember(17)]
    public Ratio Ratio { get; set; }

    [ProtoMember(18)]
    public EffectiveDate Effective { get; set; }

    [ProtoMember(19)]
    public OpeningTime Opens { get; set; }
}

/// <summary>The same message over the primitives.</summary>
[ProtoContract]
public sealed class PlainProtoScalars
{
    [ProtoMember(1)]
    public string? Account { get; set; }

    [ProtoMember(2)]
    public Guid Customer { get; set; }

    [ProtoMember(3)]
    public decimal Amount { get; set; }

    [ProtoMember(4)]
    public DateTime Recorded { get; set; }

    [ProtoMember(5)]
    public TimeSpan Took { get; set; }

    [ProtoMember(6)]
    public short Quantity { get; set; }

    [ProtoMember(7)]
    public char Grade { get; set; }

    [ProtoMember(8)]
    public bool Consent { get; set; }

    [ProtoMember(9)]
    public sbyte Adjustment { get; set; }

    [ProtoMember(10)]
    public byte Score { get; set; }

    [ProtoMember(11)]
    public ushort Port { get; set; }

    [ProtoMember(12)]
    public int Page { get; set; }

    [ProtoMember(13)]
    public uint Sequence { get; set; }

    [ProtoMember(14)]
    public long Size { get; set; }

    [ProtoMember(15)]
    public ulong Bytes { get; set; }

    [ProtoMember(16)]
    public double Latitude { get; set; }

    [ProtoMember(17)]
    public float Ratio { get; set; }

    [ProtoMember(18)]
    public DateOnly Effective { get; set; }

    [ProtoMember(19)]
    public TimeOnly Opens { get; set; }
}

/// <summary>Two dictionaries of other value objects over the same scalars.</summary>
[ProtoContract]
public sealed class ProtoStock
{
    [ProtoMember(1)]
    public Dictionary<CountryCode, Quantity>? Stock { get; set; }

    [ProtoMember(2)]
    public Dictionary<Iban, PageNumber>? Reserved { get; set; }
}

/// <summary>The same dictionaries over the primitives.</summary>
[ProtoContract]
public sealed class PlainProtoStock
{
    [ProtoMember(1)]
    public Dictionary<string, short>? Stock { get; set; }

    [ProtoMember(2)]
    public Dictionary<string, int>? Reserved { get; set; }
}
