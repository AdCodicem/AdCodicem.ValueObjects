using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Reflection;
using AdCodicem.ValueObjects.Generators.Model;
using AdCodicem.ValueObjects.ProtobufNet;
using Microsoft.CodeAnalysis;
using ProtoBuf;
using ProtoBuf.Meta;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// What <c>VO0043</c> and <c>VO0044</c> report: a third-party serializer's contract attribute on a value object the
/// generator writes, which makes that serializer describe it by the generator's members.
/// </summary>
/// <remarks>
/// <para>
/// The attributes are matched by the full metadata name of their class, so the snippets compile against stand-ins of
/// Orleans', MessagePack's, MemoryPack's and Newtonsoft.Json's, declared under their real names in an assembly of their
/// own, as <see cref="RequestDelegateGeneratorTests"/> declares endpoint routing. protobuf-net is the real one in every
/// case: the naming fix needs <c>AdCodicem.ValueObjects.ProtobufNet</c>, which brings it, and a stand-in beside it would
/// be ambiguous. <c>DataContractAttribute</c>, <c>XmlTypeAttribute</c>, <c>XmlRootAttribute</c> and
/// <c>SerializableAttribute</c> come with the framework.
/// </para>
/// <para>
/// The analyzer runs over the compilation with the generator's output, as the compiler does, so that the
/// <c>IXmlSerializable</c> the generator implements under the XML opt-in is seen.
/// </para>
/// </remarks>
public sealed class SerializerContractAnalyzerTests
{
    /// <summary>The usings every snippet starts with, and its namespace.</summary>
    internal const string Usings = """
        using System;
        using System.Runtime.Serialization;
        using System.Xml.Serialization;
        using AdCodicem.ValueObjects;
        using AdCodicem.ValueObjects.Annotations;
        using AdCodicem.ValueObjects.Identifiers;
        using MemoryPack;
        using MessagePack;
        using Newtonsoft.Json;
        using Orleans;
        using ProtoBuf;

        """;

    private const string Effect = "protobuf-net writes it as a message with no field and reads it back as a default instance";

    private const string OrleansEffect = "Orleans writes it with no field and reads it back as a default instance";

    private const string GenericRemedy = "An attribute cannot name a serializer through a type parameter: remove it, and "
        + "register each construction with AddValueObject<TSelf, TValue>() of AdCodicem.ValueObjects.ProtobufNet";

    /// <summary>Every supported underlying type, by the name the diagnostics give them.</summary>
    public static TheoryData<string> SupportedUnderlyingTypes => [.. UnderlyingType.SupportedNames];

    [Theory]
    [InlineData("GenerateSerializer", "string", "VO0043",
        OrleansEffect + ". Remove the attribute, reference AdCodicem.ValueObjects.Orleans and call AddValueObjects() on the "
        + "serializer of every silo and client.")]
    [InlineData("DataContract(Name = \"sku\")", "string", "VO0043",
        "protobuf-net and MessagePack write it with no member and read it back as a default instance, and "
        + "DataContractSerializer refuses it once the assembly is opted into XML serialization. Remove the attribute: "
        + "[assembly: ValueObjectXmlSerialization] writes the value object as its value.")]
    [InlineData("XmlType(\"sku\")", "string", "VO0043",
        "protobuf-net writes it as a message with no field and reads it back as a default instance, and XmlSerializer "
        + "refuses it once the assembly is opted into XML serialization. Remove the attribute: the schema provider names "
        + "the type, and [XmlRoot] names the element of a root value object.")]
    [InlineData("MessagePackObject", "string", "VO0043",
        "MessagePack serializes its generated members: MsgPack004 refuses them, or, keyed by name, it is written {} and "
        + "read back as a default instance. Remove the attribute, and serialize with WithValueObjects() of "
        + "AdCodicem.ValueObjects.MessagePack.")]
    [InlineData("MessagePackObject(keyAsPropertyName: true)", "string", "VO0043",
        "MessagePack serializes its generated members: MsgPack004 refuses them, or, keyed by name, it is written {} and "
        + "read back as a default instance. Remove the attribute, and serialize with WithValueObjects() of "
        + "AdCodicem.ValueObjects.MessagePack.")]
    [InlineData("MessagePackObject(AllowPrivate = true)", "int", "VO0043",
        "MessagePack serializes its generated members: MsgPack004 refuses them, or, keyed by name, it is written {} and "
        + "read back as a default instance. Remove the attribute, and serialize with WithValueObjects() of "
        + "AdCodicem.ValueObjects.MessagePack.")]
    [InlineData("JsonObject", "string", "VO0043",
        "Newtonsoft.Json writes it as an object holding Value, and reads that object back as a default instance. Remove "
        + "the attribute: Newtonsoft.Json then writes the value, as text through the generated type converter, or as its "
        + "underlying type with ValueObjectConverter.")]
    [InlineData("MemoryPackable", "string", "VO0044",
        "MemoryPack's generated formatter does not compile, CS8377. No integration of this library serializes with "
        + "MemoryPack: remove the attribute, and use a serializer that reads the value object through its rules.")]
    [InlineData("MemoryPackable", "int", "VO0044",
        "MemoryPack copies its memory and reads it back without its rules. No integration of this library serializes "
        + "with MemoryPack: remove the attribute, and use a serializer that reads the value object through its rules.")]
    [InlineData("Serializable", "string", "VO0044",
        "DataContractSerializer writes its private field and reads it back without its rules. Opt the assembly into XML "
        + "serialization with [assembly: ValueObjectXmlSerialization]. Remove the attribute only where no "
        + "DataContractSerializer writes the type: without it, DataContractSerializer writes an empty element and "
        + "reads back a default instance.")]
    [InlineData("ProtoContract", "string", "VO0043",
        Effect + ". Name its serializer, [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Sku, string>))], "
        + "or remove the attribute and register the value object with AddValueObjects.")]
    public async Task Each_contract_attribute_is_reported_on_the_attribute_with_its_consequence_and_remedy(
        string attribute,
        string underlying,
        string id,
        string consequence)
    {
        var diagnostics = await RunAsync($$"""
            [ValueObject<{{underlying}}>]
            [{{attribute}}]
            public readonly partial struct Sku;
            """);

        var diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be(id);
        diagnostic.Severity.Should().Be(id == "VO0043" ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning);
        diagnostic.GetMessage(CultureInfo.InvariantCulture)
            .Should().Be($"'Sku' carries [{attribute.Split('(')[0]}], so {consequence}");
        Located(diagnostic).Should().Be((attribute, $"[{attribute}]"));
    }

    /// <summary>
    /// A bare <c>[ProtoContract]</c> gets the remedy its compilation allows: the attribute route when the package is in
    /// reach, the package to reference otherwise, and removal where an attribute cannot name the serializer, a generic
    /// value object, or one protobuf-net has no serializer for, generic or not, since <c>AddValueObject</c> refuses
    /// each construction of it too. A <c>Serializer</c> or a <c>Surrogate</c> set to <see langword="null"/> names
    /// nothing.
    /// </summary>
    [Theory]
    [InlineData("""
        [ValueObject<string>]
        [ProtoContract]
        public readonly partial struct Sku;
        """, false, "Sku", "Reference AdCodicem.ValueObjects.ProtobufNet and name its serializer, "
        + "[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Sku, string>))], or remove the attribute")]
    [InlineData("""
        [ValueObject<string>]
        [ProtoContract]
        public readonly partial struct Reference<TOwner>;
        """, true, "Reference<TOwner>", GenericRemedy)]
    [InlineData("""
        public static partial class Catalog<T>
        {
            [ValueObject<int>]
            [ProtoContract]
            public readonly partial struct Stock;
        }
        """, true, "Catalog<T>.Stock", GenericRemedy)]
    [InlineData("""
        [ValueObject<Int128>]
        [ProtoContract]
        public readonly partial struct Sku;
        """, true, "Sku", "protobuf-net has no serializer for Int128: remove the attribute")]
    [InlineData("""
        [ValueObject<UInt128>]
        [ProtoContract]
        public readonly partial struct Sku;
        """, false, "Sku", "protobuf-net has no serializer for UInt128: remove the attribute")]
    [InlineData("""
        [ValueObject<DateTimeOffset>]
        [ProtoContract]
        public readonly partial struct Sku;
        """, true, "Sku", "protobuf-net has no serializer for DateTimeOffset: remove the attribute")]
    [InlineData("""
        [ValueObject<Int128>]
        [ProtoContract]
        public readonly partial struct Big<T>;
        """, true, "Big<T>", "protobuf-net has no serializer for Int128: remove the attribute")]
    [InlineData("""
        public static partial class Ledger<T>
        {
            [ValueObject<DateTimeOffset>]
            [ProtoContract]
            public readonly partial struct Stamp;
        }
        """, false, "Ledger<T>.Stamp", "protobuf-net has no serializer for DateTimeOffset: remove the attribute")]
    [InlineData("""
        [EntityId("ord")]
        [ProtoContract]
        public readonly partial struct OrderRef;
        """, true, "OrderRef", "Name its serializer, [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<OrderRef, "
        + "string>))], or remove the attribute and register the value object with AddValueObjects")]
    [InlineData("""
        public static partial class Catalog
        {
            [ValueObject<int>, ProtoContract(Name = "qty")]
            public readonly partial struct Quantity;
        }
        """, true, "Catalog.Quantity", "Name its serializer, [ProtoContract(Serializer = "
        + "typeof(ValueObjectProtoSerializer<Catalog.Quantity, int>))], or remove the attribute and register the value "
        + "object with AddValueObjects")]
    [InlineData("""
        [ValueObject<string>]
        [ProtoContract(Serializer = null, Name = "sku")]
        public readonly partial struct Sku;
        """, true, "Sku", "Name its serializer, [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Sku, "
        + "string>))], or remove the attribute and register the value object with AddValueObjects")]
    [InlineData("""
        [ValueObject<Guid>]
        [ProtoContract(Surrogate = null)]
        public readonly partial struct Sku;
        """, true, "Sku", "Name its serializer, [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Sku, "
        + "Guid>))], or remove the attribute and register the value object with AddValueObjects")]
    [InlineData("""
        [ValueObject<long>]
        [ProtoContract()]
        public readonly partial struct Sku;
        """, true, "Sku", "Name its serializer, [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Sku, "
        + "long>))], or remove the attribute and register the value object with AddValueObjects")]
    public async Task A_bare_ProtoContract_is_reported_with_the_remedy_its_compilation_allows(
        string source,
        bool package,
        string type,
        string remedy)
    {
        var diagnostics = await RunAsync(source, package ? SerializerReferences.WithPackage : SerializerReferences.WithSerializers);

        var diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0043");
        diagnostic.GetMessage(CultureInfo.InvariantCulture)
            .Should().Be($"'{type}' carries [ProtoContract], so {Effect}. {remedy}.");
    }

    /// <summary>
    /// <c>[GenerateSerializer]</c> gets the remedy its compilation allows: the package's registration when
    /// <c>AdCodicem.ValueObjects.Orleans</c> is in reach, the package to reference otherwise, an <see langword="internal"/>
    /// type of that name being out of reach. A generic value object gets the same remedy, since <c>AddValueObjects()</c>
    /// serializes each construction.
    /// </summary>
    /// <param name="source">The declaration.</param>
    /// <param name="package">The stand-in of the package referenced: none, a public one or an internal one.</param>
    /// <param name="type">The type the message names.</param>
    /// <param name="reachable">Whether the remedy is the package's registration.</param>
    [Theory]
    [InlineData("""
        [ValueObject<string>]
        [GenerateSerializer]
        public readonly partial struct Sku;
        """, "none", "Sku", false)]
    [InlineData("""
        [ValueObject<string>]
        [GenerateSerializer]
        public readonly partial struct Sku;
        """, "public", "Sku", true)]
    [InlineData("""
        [ValueObject<string>]
        [GenerateSerializer]
        public readonly partial struct Sku;
        """, "internal", "Sku", false)]
    [InlineData("""
        [ValueObject<string>]
        [GenerateSerializer]
        public readonly partial struct Reference<TOwner>;
        """, "public", "Reference<TOwner>", true)]
    [InlineData("""
        [EntityId("ord")]
        [GenerateSerializer, Alias("ord")]
        public readonly partial struct OrderRef;
        """, "none", "OrderRef", false)]
    public async Task GenerateSerializer_is_reported_with_the_remedy_its_compilation_allows(string source, string package, string type, bool reachable)
    {
        var references = package switch
        {
            "public" => SerializerReferences.WithOrleansPackage,
            "internal" => SerializerReferences.WithInternalOrleansPackage,
            _ => SerializerReferences.WithPackage,
        };

        var diagnostics = await RunAsync(source, references);

        var diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0043");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"'{type}' carries [GenerateSerializer], so {OrleansEffect}. " + (reachable
                ? "Remove the attribute: AddValueObjects() of AdCodicem.ValueObjects.Orleans serializes the value object as its "
                    + "underlying value."
                : "Remove the attribute, reference AdCodicem.ValueObjects.Orleans and call AddValueObjects() on the serializer "
                    + "of every silo and client."));
    }

    /// <summary>
    /// protobuf-net writes every underlying type but the 128-bit integers and <see cref="DateTimeOffset"/>, so the remedy
    /// names the serializer for each of the others.
    /// </summary>
    [Theory]
    [MemberData(nameof(SupportedUnderlyingTypes))]
    public async Task Every_underlying_type_gets_the_protobuf_net_remedy_its_type_allows(string underlying)
    {
        var shown = underlying.Replace("System.", string.Empty, StringComparison.Ordinal);

        var diagnostics = await RunAsync($$"""
            [ValueObject<{{underlying}}>]
            [ProtoContract]
            public readonly partial struct Wrapper;
            """);

        diagnostics.Should().ContainSingle().Which.GetMessage(CultureInfo.InvariantCulture).Should().EndWith(
            shown is "Int128" or "UInt128" or "DateTimeOffset"
                ? $"protobuf-net has no serializer for {shown}: remove the attribute."
                : $"Name its serializer, [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Wrapper, {shown}>))], "
                  + "or remove the attribute and register the value object with AddValueObjects.");
    }

    /// <summary>
    /// MemoryPack's generated formatter does not compile over text, an identifier included (CS8377), and copies the
    /// memory of every other underlying type, which the probe of every one of them showed.
    /// </summary>
    [Theory]
    [MemberData(nameof(SupportedUnderlyingTypes))]
    public async Task MemoryPackable_over_text_fails_to_compile_and_over_anything_else_copies_memory(string underlying)
    {
        var diagnostics = await RunAsync($$"""
            [ValueObject<{{underlying}}>]
            [MemoryPackable]
            public readonly partial struct Wrapper;

            [EntityId("ord")]
            [MemoryPackable]
            public readonly partial struct OrderRef;
            """);

        diagnostics.Select(Message).Should().Equal(
            "'Wrapper' carries [MemoryPackable], so "
            + (underlying == "string"
                ? "MemoryPack's generated formatter does not compile, CS8377"
                : "MemoryPack copies its memory and reads it back without its rules")
            + ". No integration of this library serializes with MemoryPack: remove the attribute, and use a serializer "
            + "that reads the value object through its rules.",
            "'OrderRef' carries [MemoryPackable], so MemoryPack's generated formatter does not compile, CS8377. No "
            + "integration of this library serializes with MemoryPack: remove the attribute, and use a serializer that "
            + "reads the value object through its rules.");
    }

    /// <summary>
    /// The package's serializer closed over another type than the value object, or not closed at all, is refused by
    /// protobuf-net at run time, "No serializer for type … is available for model": it is reported, and the remedy names
    /// the value object's own, keeping a profile already chosen.
    /// </summary>
    [Theory]
    [InlineData("""
        [ValueObject<string>]
        [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Other, string>))]
        public readonly partial struct Sku;
        """, "Sku", "Name its own, [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Sku, string>))]")]
    [InlineData("""
        [ValueObject<Guid>]
        [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Other, string, ValueObjectProtoProfiles.StrictLevel300>))]
        public readonly partial struct CustomerId;
        """, "CustomerId", "Name its own, [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<CustomerId, Guid, "
        + "ValueObjectProtoProfiles.StrictLevel300>))]")]
    [InlineData("""
        [ValueObject<int>]
        [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<,>))]
        public readonly partial struct Quantity;
        """, "Quantity", "Name its own, [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Quantity, int>))]")]
    [InlineData("""
        [ValueObject<int>]
        [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<,,>))]
        public readonly partial struct Quantity;
        """, "Quantity", "Name its own, [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Quantity, int>))]")]
    [InlineData("""
        [ValueObject<Int128>]
        [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Other, string>))]
        public readonly partial struct Serial;
        """, "Serial", "protobuf-net has no serializer for Int128: remove the attribute")]
    [InlineData("""
        [ValueObject<string>]
        [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Other, string>))]
        public readonly partial struct Reference<TOwner>;
        """, "Reference<TOwner>", GenericRemedy)]
    [InlineData("""
        [ValueObject<UInt128>]
        [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Other, string>))]
        public readonly partial struct Big<T>;
        """, "Big<T>", "protobuf-net has no serializer for UInt128: remove the attribute")]
    public async Task A_ProtoContract_naming_another_value_objects_serializer_is_reported(string source, string type, string remedy)
    {
        var diagnostics = await RunAsync("""
            using AdCodicem.ValueObjects.ProtobufNet;

            [ValueObject<string>]
            [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Other, string>))]
            public readonly partial struct Other;

            """ + source);

        diagnostics.Select(Message).Should().Equal(
            $"'{type}' carries [ProtoContract], so protobuf-net refuses it: the serializer it names is not closed over "
            + $"'{type}'. {remedy}.");
    }

    /// <summary>
    /// What protobuf-net's attribute may name: the package's serializer over the value object itself, under any profile
    /// and over a type protobuf-net cannot write, whose refusal is loud at run time; a serializer of the application's
    /// own; a surrogate; and <c>IsScalar</c>, which the guide warns of.
    /// </summary>
    [Fact]
    public async Task What_protobuf_net_may_name_is_left_alone()
    {
        var diagnostics = await RunAsync("""
            using AdCodicem.ValueObjects.ProtobufNet;
            using ProtoBuf.Serializers;

            [ValueObject<string>]
            [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Sku, string>))]
            public readonly partial struct Sku;

            [ValueObject<Guid>]
            [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<CustomerId, Guid, ValueObjectProtoProfiles.TrustedLevel300>))]
            public readonly partial struct CustomerId;

            [ValueObject<Int128>]
            [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<HugeCount, Int128>))]
            public readonly partial struct HugeCount;

            [ValueObject<int>]
            [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Quantity, int>), IsScalar = true)]
            public readonly partial struct Quantity;

            [ValueObject<string>]
            [ProtoContract(Serializer = typeof(TagSerializer))]
            public readonly partial struct Tag;

            public sealed class TagSerializer : ISerializer<Tag>
            {
                public SerializerFeatures Features => SerializerFeatures.WireTypeString | SerializerFeatures.CategoryScalar;

                public Tag Read(ref ProtoReader.State state, Tag value) => Tag.Create(state.ReadString());

                public void Write(ref ProtoWriter.State state, Tag value) => state.WriteString(value.Value);
            }

            [ValueObject<string>]
            [ProtoContract(Surrogate = typeof(LabelSurrogate))]
            public readonly partial struct Label;

            [ProtoContract]
            public sealed class LabelSurrogate
            {
                [ProtoMember(1)]
                public string? Value { get; set; }
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The diagnostic is located on the attribute, on whichever partial declaration of the value object carries it.
    /// </summary>
    [Fact]
    public async Task The_attribute_is_reported_on_the_partial_declaration_that_carries_it()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>]
            public readonly partial struct Sku;

            [DataContract]
            public readonly partial struct Sku
            {
                public int Length => Value.Length;
            }
            """);

        var diagnostic = diagnostics.Should().ContainSingle().Subject;
        Located(diagnostic).Should().Be(("DataContract", "[DataContract]"));
        diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken)
            .Lines[diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1].ToString()
            .Should().Be("public readonly partial struct Sku", "the second declaration carries it");
    }

    /// <summary>
    /// A generic value object, one nested in a generic type, one nested in a class, a private one and an entity identifier
    /// are value objects the generator writes, and reported.
    /// </summary>
    [Fact]
    public async Task Generic_nested_private_and_identifier_value_objects_are_reported()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>]
            [DataContract]
            public readonly partial struct Reference<TOwner>;

            public static partial class Catalog<T>
            {
                [ValueObject<int>]
                [GenerateSerializer]
                public readonly partial struct Stock;
            }

            public static partial class Catalog
            {
                [ValueObject<int>]
                [JsonObject]
                public readonly partial struct Quantity;
            }

            public sealed partial class Vault
            {
                [ValueObject<string>]
                [XmlType]
                private readonly partial struct Secret;
            }

            [EntityId("ord")]
            [MessagePackObject]
            public readonly partial struct OrderRef;
            """);

        diagnostics.Select(diagnostic => (diagnostic.Id, Message(diagnostic).Split(',')[0])).Should().Equal(
            ("VO0043", "'Reference<TOwner>' carries [DataContract]"),
            ("VO0043", "'Catalog<T>.Stock' carries [GenerateSerializer]"),
            ("VO0043", "'Catalog.Quantity' carries [JsonObject]"),
            ("VO0043", "'Vault.Secret' carries [XmlType]"),
            ("VO0043", "'OrderRef' carries [MessagePackObject]"));
    }

    /// <summary>
    /// Under <c>[assembly: ValueObjectXmlSerialization]</c> the generator implements <c>IXmlSerializable</c>, which
    /// <c>DataContractSerializer</c> calls in place of reading the private field, and so does a value object implementing
    /// it by hand, directly or through an interface of its own: <c>[Serializable]</c> changes nothing there.
    /// </summary>
    [Fact]
    public async Task Serializable_is_left_alone_where_IXmlSerializable_wins()
    {
        var optedIn = await RunAsync(
            """
            using System;
            using AdCodicem.ValueObjects.Annotations;

            [assembly: ValueObjectXmlSerialization]

            namespace Shop;

            [ValueObject<string>(MaxLength = 3)]
            [Serializable]
            public readonly partial struct Tag;
            """,
            wrap: false);

        var byHand = await RunAsync("""
            [ValueObject<string>(MaxLength = 3)]
            [Serializable]
            public readonly partial struct Tag : IXmlSerializable
            {
                System.Xml.Schema.XmlSchema? IXmlSerializable.GetSchema() => null;

                void IXmlSerializable.ReadXml(System.Xml.XmlReader reader)
                {
                }

                void IXmlSerializable.WriteXml(System.Xml.XmlWriter writer) => writer.WriteString(Value);
            }
            """);

        var throughAnother = await RunAsync("""
            public interface IXmlValue : IXmlSerializable;

            [ValueObject<string>(MaxLength = 3)]
            [Serializable]
            public readonly partial struct Tag : IXmlValue
            {
                System.Xml.Schema.XmlSchema? IXmlSerializable.GetSchema() => null;

                void IXmlSerializable.ReadXml(System.Xml.XmlReader reader)
                {
                }

                void IXmlSerializable.WriteXml(System.Xml.XmlWriter writer) => writer.WriteString(Value);
            }
            """);

        optedIn.Should().BeEmpty();
        byHand.Should().BeEmpty();
        throughAnother.Should().BeEmpty("DataContractSerializer takes any type assignable to IXmlSerializable");
    }

    /// <summary>
    /// The attributes a value object may carry, which describe nothing by its members: <c>[XmlRoot]</c>, which names the
    /// element of a root value object, Orleans' <c>[Immutable]</c> and <c>[Alias]</c>, MessagePack's
    /// <c>[MessagePackFormatter]</c> and Newtonsoft.Json's <c>[JsonConverter]</c>.
    /// </summary>
    [Fact]
    public async Task Attributes_a_value_object_may_carry_are_left_alone()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>]
            [XmlRoot("sku")]
            [Immutable]
            [Alias("sku")]
            [MessagePackFormatter(typeof(object))]
            [JsonConverter(typeof(object))]
            public readonly partial struct Sku;
            """);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// Only a value object the generator writes is read: not the types holding value objects, which the attributes
    /// describe as their authors mean, not the shapes the generator refuses, <c>VO0002</c>, <c>VO0003</c> and
    /// <c>VO0018</c>, not a value object written by hand, which owns its members, and not one of a referenced assembly,
    /// which is reported in its own project.
    /// </summary>
    [Fact]
    public async Task Only_value_objects_the_generator_writes_are_read()
    {
        var domain = MetadataReference.CreateFromImage(GeneratorHarness.Emit(
            """
            [ValueObject<string>]
            [System.Runtime.Serialization.DataContract]
            public readonly partial struct CountryCode;
            """,
            "Domain"));

        var diagnostics = await RunAsync(
            """
            [ValueObject<string>]
            public readonly partial struct Sku;

            [DataContract, ProtoContract, Serializable, MemoryPackable]
            public sealed class Order
            {
                [DataMember, ProtoMember(1)]
                public Sku? Code { get; set; }

                [DataMember, ProtoMember(2)]
                public Test.CountryCode Country { get; set; }
            }

            [DataContract, Serializable]
            public sealed record Line(Sku Code);

            [DataContract, ProtoContract, Serializable, MessagePackObject]
            public readonly struct Plain;

            [ValueObject<string>]
            [DataContract, Serializable]
            public partial struct Mutable;

            [ValueObject<string>]
            [DataContract, Serializable]
            public readonly partial record struct Recorded;

            [ValueObject<string>]
            [DataContract, ProtoContract]
            public readonly ref partial struct OnTheStack;

            [ValueObject<object>]
            [DataContract, ProtoContract]
            public readonly partial struct Unsupported;

            [ValueObject<string>]
            [EntityId("cus")]
            [DataContract, ProtoContract]
            public readonly partial struct Both;

            """ + HandWritten,
            SerializerReferences.WithPackage.Add(domain));

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// An attribute of the same name in another namespace, or nested in a type of the serializer's namespace, is not
    /// the serializer's, and neither is a serializer of the package's name nested in a type of its namespace, which is
    /// the application's own.
    /// </summary>
    [Fact]
    public async Task An_attribute_of_the_same_name_in_another_namespace_is_left_alone()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>]
            [Elsewhere.ProtoContract]
            [Elsewhere.DataContract]
            [Elsewhere.GenerateSerializer]
            [Elsewhere.MemoryPackable]
            [Elsewhere.Serializable]
            public readonly partial struct Sku;

            [ValueObject<string>]
            [ProtoBuf.Holder.ProtoContract]
            [System.Runtime.Serialization.Holder.DataContract]
            [Orleans.Holder.GenerateSerializer]
            [MemoryPack.Holder.MemoryPackable]
            [System.Holder.Serializable]
            public readonly partial struct Code;

            [ValueObject<string>]
            [ProtoContract(Serializer = typeof(AdCodicem.ValueObjects.ProtobufNet.Holder.ValueObjectProtoSerializer<Code, string>))]
            public readonly partial struct Tag;
            """);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// An analyzer's diagnostics honour <c>#pragma</c>, unlike the generator's: a type serialized wrongly on purpose, as
    /// the unit suite's trap, takes one around its attribute.
    /// </summary>
    [Fact]
    public async Task A_pragma_silences_each_diagnostic()
    {
        var diagnostics = await RunAsync("""
            [ValueObject<string>]
            #pragma warning disable VO0043 // The trap a test exercises on purpose.
            [ProtoContract]
            #pragma warning restore VO0043
            public readonly partial struct Sku;

            [ValueObject<int>]
            #pragma warning disable VO0044 // Bytes this application alone writes.
            [MemoryPackable]
            #pragma warning restore VO0044
            public readonly partial struct Quantity;

            [ValueObject<int>]
            [MemoryPackable]
            public readonly partial struct Count;
            """);

        diagnostics.Select(Message).Should().ContainSingle().Which.Should().StartWith("'Count' carries [MemoryPackable]");
    }

    /// <summary>
    /// An attribute the compiler cannot bind, which an IDE analyzes all the same, is no contract attribute.
    /// </summary>
    [Fact]
    public async Task An_attribute_the_compiler_cannot_bind_is_left_alone()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<SerializerContractAnalyzer>(
            Usings + """

            namespace Shop;

            [ValueObject<string>]
            [Missing]
            [ProtoContract]
            public readonly partial struct Sku;
            """,
            SerializerReferences.WithPackage);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("VO0043");
    }

    [Fact]
    [Trait("Reads", "Documentation")]
    public void The_help_links_land_on_the_section_of_the_diagnostics_reference()
    {
        var analyzer = new SerializerContractAnalyzer();

        analyzer.SupportedDiagnostics.Select(descriptor => descriptor.Id).Should().Equal("VO0043", "VO0044");
        analyzer.SupportedDiagnostics.Select(descriptor => descriptor.DefaultSeverity)
            .Should().Equal(DiagnosticSeverity.Error, DiagnosticSeverity.Warning);

        var page = File.ReadAllLines(Path.Combine(RepositoryRoot(), "website", "docs", "reference", "diagnostics.md"));
        page.Should().Contain("slug: /reference/diagnostics");

        foreach (var descriptor in analyzer.SupportedDiagnostics)
        {
            var link = new Uri(descriptor.HelpLinkUri);
            link.GetLeftPart(UriPartial.Path)
                .Should().Be("https://adcodicem.github.io/AdCodicem.ValueObjects/docs/reference/diagnostics");
            link.Fragment.Should().Be("#a-serializers-contract-attribute");
            page.Select(Anchor).Should().Contain(link.Fragment.TrimStart('#'));
        }
    }

    /// <summary>
    /// The compiler never hands an analyzer a null context, so the guard is only reached by a direct call, which it
    /// tolerates.
    /// </summary>
    [Fact]
    public void The_analyzer_initialized_without_a_context_does_nothing()
    {
        var analyzer = new SerializerContractAnalyzer();

        analyzer.Invoking(target => target.Initialize(null!)).Should().NotThrow();
    }

    /// <summary>
    /// The underlying types the remedy and the code fix leave to removal are those protobuf-net has no inbuilt
    /// serializer for, at every level the package's profiles name: an update that adds one fails here, rather than
    /// leaving its naming unoffered.
    /// </summary>
    [Theory]
    [MemberData(nameof(SupportedUnderlyingTypes))]
    public void The_types_protobuf_net_does_not_write_are_those_it_has_no_inbuilt_serializer_for(string underlying)
    {
        UnderlyingType.TryResolve($"global::{ClrType(underlying).FullName}", out var resolved).Should().BeTrue();
        var inbuilt = typeof(TypeModel).GetMethod(nameof(TypeModel.GetInbuiltSerializer))!.MakeGenericMethod(ClrType(underlying));

        foreach (var level in new[] { CompatibilityLevel.Level200, CompatibilityLevel.Level240, CompatibilityLevel.Level300 })
        {
            (inbuilt.Invoke(null, [level, DataFormat.Default]) is not null)
                .Should().Be(SerializerContractAnalyzer.ProtobufNetWrites(resolved!), "{0} at {1}", underlying, level);
        }
    }

    /// <summary>A value object written by hand, which owns its members.</summary>
    private const string HandWritten = """
        [DataContract, ProtoContract, Serializable, MemoryPackable]
        public readonly struct Code : IValueObject<Code, string>
        {
            private readonly string? _value;
            private Code(string value) => _value = value;
            public static AdCodicem.ValueObjects.Metadata.ValueObjectSchema Schema => AdCodicem.ValueObjects.Metadata.ValueObjectSchema.Unconstrained;
            public string Value => _value ?? string.Empty;
            public bool IsDefault => _value is null;
            public static string Normalize(string value) => value;
            public static ValidationResult Validate(in string value) => ValidationResult.Success;
            public static Code Create(string value) => new(value);
            public static bool TryCreate(string value, out Code result) => TryCreate(value, out result, out _);
            public static bool TryCreate(string value, out Code result, out ValidationResult validation) { validation = default; result = new(value); return true; }
            public static Code CreateUnchecked(string value) => new(value);
            public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out Code result, out ValidationResult validation) => TryCreate(text.ToString(), out result, out validation);
            public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Code result) => TryParse(s, provider, out result, out _);
            public static bool TryParse(string? s, IFormatProvider? provider, out Code result) => TryParse(s.AsSpan(), provider, out result, out _);
            public static Code Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());
            public static Code Parse(string s, IFormatProvider? provider) => Create(s);
            public bool Equals(Code other) => Value == other.Value;
            public override bool Equals(object? obj) => obj is Code other && Equals(other);
            public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
            public int CompareTo(Code other) => string.CompareOrdinal(Value, other.Value);
            public override string ToString() => Value;
            public string ToString(string? format, IFormatProvider? formatProvider) => Value;
            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) { charsWritten = 0; return Value.TryCopyTo(destination) && (charsWritten = Value.Length) >= 0; }
            public static bool operator ==(Code left, Code right) => left.Equals(right);
            public static bool operator !=(Code left, Code right) => !left.Equals(right);
            public static bool operator <(Code left, Code right) => left.CompareTo(right) < 0;
            public static bool operator >(Code left, Code right) => left.CompareTo(right) > 0;
            public static bool operator <=(Code left, Code right) => left.CompareTo(right) <= 0;
            public static bool operator >=(Code left, Code right) => left.CompareTo(right) >= 0;
        }
        """;

    /// <summary>
    /// Runs the analyzer over a snippet that is otherwise valid C#, so that what it reports is never a side effect of code
    /// the compiler would refuse anyway, in the order of the source.
    /// </summary>
    internal static async Task<IReadOnlyList<Diagnostic>> RunAsync(
        string source,
        ImmutableArray<MetadataReference>? references = null,
        bool wrap = true)
    {
        var text = wrap ? Usings + "\nnamespace Shop;\n\n" + source : source;
        var compiled = references ?? SerializerReferences.WithPackage;

        GeneratorHarness.Run(text, references: compiled).CompilationDiagnostics.Should().BeEmpty();

        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<SerializerContractAnalyzer>(text, compiled);

        return [.. diagnostics.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start).ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)];
    }

    private static string Message(Diagnostic diagnostic) => diagnostic.GetMessage(CultureInfo.InvariantCulture);

    private static (string Text, string Line) Located(Diagnostic diagnostic)
    {
        var text = diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken);
        var span = diagnostic.Location.SourceSpan;

        return (text.ToString(span), text.Lines.GetLineFromPosition(span.Start).ToString().Trim());
    }

    /// <summary>Gets the CLR type of an underlying type, by the name the diagnostics give it.</summary>
    private static Type ClrType(string underlying) => underlying switch
    {
        "string" => typeof(string),
        "bool" => typeof(bool),
        "char" => typeof(char),
        "sbyte" => typeof(sbyte),
        "byte" => typeof(byte),
        "short" => typeof(short),
        "ushort" => typeof(ushort),
        "int" => typeof(int),
        "uint" => typeof(uint),
        "long" => typeof(long),
        "ulong" => typeof(ulong),
        "decimal" => typeof(decimal),
        "double" => typeof(double),
        "float" => typeof(float),
        _ => Type.GetType(underlying, throwOnError: true)!,
    };

    /// <summary>
    /// Gets the anchor Docusaurus gives a heading of any level: lower case, punctuation dropped, spaces as hyphens. Any
    /// other line gives none.
    /// </summary>
    private static string Anchor(string line)
    {
        if (!line.StartsWith('#'))
        {
            return string.Empty;
        }

        var heading = line.TrimStart('#').Trim().ToLowerInvariant();

        return string.Concat(heading.Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-'))
            .Replace(' ', '-');
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AdCodicem.ValueObjects.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}

/// <summary>
/// What the snippets of <see cref="SerializerContractAnalyzerTests"/> and <see cref="SerializerContractCodeFixTests"/>
/// compile against.
/// </summary>
internal static class SerializerReferences
{
    /// <summary>
    /// Stand-ins for the attributes of Orleans, MessagePack, MemoryPack and Newtonsoft.Json, under their real full
    /// names, and attributes of the same names in another namespace or nested in a type of the serializer's namespace,
    /// beside a serializer of the package's name nested so.
    /// </summary>
    private static readonly MetadataReference StandIns = MetadataReference.CreateFromImage(GeneratorHarness.Emit(
        """
        using System;

        namespace Orleans
        {
            public sealed class GenerateSerializerAttribute : Attribute;

            public sealed class ImmutableAttribute : Attribute;

            public sealed class AliasAttribute(string alias) : Attribute
            {
                public string Alias { get; } = alias;
            }
        }

        namespace MessagePack
        {
            public sealed class MessagePackObjectAttribute(bool keyAsPropertyName = false) : Attribute
            {
                public bool KeyAsPropertyName { get; } = keyAsPropertyName;

                public bool AllowPrivate { get; set; }
            }

            public sealed class MessagePackFormatterAttribute(Type formatterType) : Attribute
            {
                public Type FormatterType { get; } = formatterType;
            }
        }

        namespace MemoryPack
        {
            public sealed class MemoryPackableAttribute : Attribute;
        }

        namespace Newtonsoft.Json
        {
            public sealed class JsonObjectAttribute : Attribute;

            public sealed class JsonConverterAttribute(Type converterType) : Attribute
            {
                public Type ConverterType { get; } = converterType;
            }
        }

        namespace Elsewhere
        {
            public sealed class ProtoContractAttribute : Attribute;

            public sealed class DataContractAttribute : Attribute;

            public sealed class GenerateSerializerAttribute : Attribute;

            public sealed class MemoryPackableAttribute : Attribute;

            public sealed class SerializableAttribute : Attribute;
        }

        namespace ProtoBuf
        {
            public static class Holder
            {
                public sealed class ProtoContractAttribute : Attribute;
            }
        }

        namespace System.Runtime.Serialization
        {
            public static class Holder
            {
                public sealed class DataContractAttribute : Attribute;
            }
        }

        namespace Orleans
        {
            public static class Holder
            {
                public sealed class GenerateSerializerAttribute : Attribute;
            }
        }

        namespace MemoryPack
        {
            public static class Holder
            {
                public sealed class MemoryPackableAttribute : Attribute;
            }
        }

        namespace System
        {
            public static class Holder
            {
                public sealed class SerializableAttribute : Attribute;
            }
        }

        namespace AdCodicem.ValueObjects.ProtobufNet
        {
            public static class Holder
            {
                public sealed class ValueObjectProtoSerializer<TSelf, TValue>;
            }
        }
        """,
        "SerializerStandIns",
        GeneratorHarness.FrameworkReferences));

    /// <summary>
    /// Gets the library, the stand-ins, and protobuf-net, without <c>AdCodicem.ValueObjects.ProtobufNet</c>: an application
    /// serializing through protobuf-net alone.
    /// </summary>
    public static ImmutableArray<MetadataReference> WithSerializers { get; } =
    [
        .. GeneratorHarness.LibraryReferences,
        StandIns,
        MetadataReference.CreateFromFile(typeof(ProtoContractAttribute).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(RuntimeTypeModel).Assembly.Location),
    ];

    /// <summary>Gets <see cref="WithSerializers"/> and <c>AdCodicem.ValueObjects.ProtobufNet</c>.</summary>
    public static ImmutableArray<MetadataReference> WithPackage { get; } =
        WithSerializers.Add(MetadataReference.CreateFromFile(typeof(ValueObjectProtoSerializer<,>).Assembly.Location));

    /// <summary>
    /// Gets <see cref="WithPackage"/> and a stand-in of <c>AdCodicem.ValueObjects.Orleans</c>, whose registration
    /// <c>VO0043</c> looks for: the real package brings Orleans' Roslyn 5 dependencies into a project compiled against the
    /// generator's Roslyn.
    /// </summary>
    public static ImmutableArray<MetadataReference> WithOrleansPackage { get; } = WithPackage.Add(OrleansStandIn("public"));

    /// <summary>Gets <see cref="WithPackage"/> and an assembly declaring the Orleans package's registration internal.</summary>
    public static ImmutableArray<MetadataReference> WithInternalOrleansPackage { get; } = WithPackage.Add(OrleansStandIn("internal"));

    private static PortableExecutableReference OrleansStandIn(string accessibility) => MetadataReference.CreateFromImage(GeneratorHarness.Emit(
        $$"""
        namespace AdCodicem.ValueObjects.Orleans
        {
            {{accessibility}} static class ValueObjectSerializerBuilderExtensions
            {
            }
        }
        """,
        $"OrleansPackageStandIn{accessibility}",
        GeneratorHarness.FrameworkReferences));
}
