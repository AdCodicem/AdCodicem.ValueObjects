using System.Globalization;
using AdCodicem.ValueObjects.GeneratorTests.Harness;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// The <c>IXmlSerializable</c> implementation and schema provider the generator writes on every value object of an
/// assembly marked <c>[assembly: ValueObjectXmlSerialization]</c>, and on none of any other.
/// </summary>
public sealed class XmlSerializationTests
{
    /// <summary>What a snippet opting its assembly in starts with: an assembly attribute comes before the namespace.</summary>
    private const string OptIn = """
        using System;
        using AdCodicem.ValueObjects;
        using AdCodicem.ValueObjects.Annotations;
        using AdCodicem.ValueObjects.Identifiers;

        [assembly: ValueObjectXmlSerialization]

        namespace Test;

        """;

    private const string Bridge = "global::AdCodicem.ValueObjects.Metadata.ValueObjectXml";

    [Fact]
    public void An_opted_in_value_object_implements_IXmlSerializable_explicitly_and_a_public_schema_provider()
    {
        var run = GeneratorHarness.Run(OptIn + """
            [ValueObject<int>]
            public readonly partial struct Count;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        var generated = run.SingleValueObject;
        generated.Should().Contain("[global::System.Xml.Serialization.XmlSchemaProvider(\"GetXmlSchema\")]");
        generated.Should().Contain(
            "partial struct Count : global::AdCodicem.ValueObjects.IValueObject<global::Test.Count, global::System.Int32>, global::System.Xml.Serialization.IXmlSerializable");
        generated.Should().Contain("global::System.Xml.Schema.XmlSchema? global::System.Xml.Serialization.IXmlSerializable.GetSchema() => null;");
        generated.Should().Contain(
            $"global::System.Runtime.CompilerServices.Unsafe.AsRef(in this) = {Bridge}.Read<global::Test.Count, global::System.Int32>(reader);");
        generated.Should().Contain(
            $"void global::System.Xml.Serialization.IXmlSerializable.WriteXml(global::System.Xml.XmlWriter writer) => {Bridge}.Write<global::Test.Count, global::System.Int32>(writer, this);");
        generated.Should().Contain(
            $"public static global::System.Xml.XmlQualifiedName GetXmlSchema(global::System.Xml.Schema.XmlSchemaSet schemas) => {Bridge}.ProvideSchema<global::Test.Count, global::System.Int32>(schemas, null);");
        generated.Should().Contain("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");

        // Hidden, yet documented as what a tool calls to complete the schemas XmlSchemaExporter only refers to.
        generated.Should().Contain("and what completes the schemas <c>XmlSchemaExporter</c>");
    }

    [Fact]
    public void The_namespace_the_attribute_names_reaches_the_schema_provider()
    {
        var run = GeneratorHarness.Run(OptIn.Replace("[assembly: ValueObjectXmlSerialization]", "[assembly: ValueObjectXmlSerialization(Namespace = \"urn:shipping \\\"quoted\\\"\")]", StringComparison.Ordinal) + """
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("ProvideSchema<global::Test.Code, global::System.String>(schemas, \"urn:shipping \\\"quoted\\\"\");");
    }

    /// <summary>
    /// Every shape compiles with its documentation checked, without implicit usings: text, a generic value object, a
    /// nested one, an entity identifier, an arithmetic one and one over a type whose name is a keyword.
    /// </summary>
    [Theory]
    [InlineData("[ValueObject<string>(MaxLength = 3)] public readonly partial struct Code;", "Code", "global::Test.Code", "global::System.String")]
    [InlineData("[ValueObject<string>] public readonly partial struct Tag<TOwner>;", "Tag", "global::Test.Tag<TOwner>", "global::System.String")]
    [InlineData("public static partial class Depot { [ValueObject<int>] public readonly partial struct Bay; }", "Bay", "global::Test.Depot.Bay", "global::System.Int32")]
    [InlineData("[EntityId(\"shp\")] public readonly partial struct ShipmentId;", "ShipmentId", "global::Test.ShipmentId", "global::System.String")]
    [InlineData("[ValueObject<decimal>(Arithmetic = true)] public readonly partial struct Price;", "Price", "global::Test.Price", "global::System.Decimal")]
    [InlineData("[ValueObject<Int128>] public readonly partial struct Balance;", "Balance", "global::Test.Balance", "global::System.Int128")]
    public void Every_shape_of_value_object_is_opted_in(string declaration, string name, string self, string value)
    {
        var run = GeneratorHarness.Run(OptIn + declaration, DocumentationMode.Diagnose);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        var generated = run.Files.Single(file => file.HintName != "ValueObjectRegistration.g.cs" && file.Text.Contains($"partial struct {name}", StringComparison.Ordinal)).Text;
        generated.Should().Contain($"{Bridge}.Read<{self}, {value}>(reader)");
        generated.Should().Contain($"{Bridge}.ProvideSchema<{self}, {value}>(schemas, null)");
    }

    [Fact]
    public void A_value_object_of_an_assembly_that_does_not_opt_in_is_written_without_XML()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            public readonly partial struct Count;
            """);

        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().NotContain("Xml");
    }

    /// <summary>The attribute of another assembly opts that assembly in, never the one referencing it.</summary>
    [Fact]
    public void The_attribute_of_a_referenced_assembly_opts_nothing_in()
    {
        var referenced = GeneratorHarness.Emit(OptIn + "public sealed class Marker;", "OptedIn");

        var run = GeneratorHarness.Run(
            """
            [ValueObject<int>]
            public readonly partial struct Count;
            """,
            references: GeneratorHarness.LibraryReferences.Add(MetadataReference.CreateFromImage(referenced)));

        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().NotContain("Xml");
    }

    /// <summary>
    /// A value object that implements <c>IXmlSerializable</c>, directly or through an interface of its own, or declares a
    /// schema provider, is its author's: a second implementation would not compile in a file they cannot edit.
    /// </summary>
    [Theory]
    [InlineData("""
        [ValueObject<string>]
        public readonly partial struct Note : System.Xml.Serialization.IXmlSerializable
        {
            public System.Xml.Schema.XmlSchema? GetSchema() => null;

            public void ReadXml(System.Xml.XmlReader reader) => System.Runtime.CompilerServices.Unsafe.AsRef(in this) = Create(reader.ReadElementContentAsString());

            public void WriteXml(System.Xml.XmlWriter writer) => writer.WriteString(Value);
        }
        """)]
    [InlineData("""
        public interface IDocumented : System.Xml.Serialization.IXmlSerializable;

        [ValueObject<string>]
        public readonly partial struct Note : IDocumented
        {
            System.Xml.Schema.XmlSchema? System.Xml.Serialization.IXmlSerializable.GetSchema() => null;

            void System.Xml.Serialization.IXmlSerializable.ReadXml(System.Xml.XmlReader reader) => System.Runtime.CompilerServices.Unsafe.AsRef(in this) = Create(reader.ReadElementContentAsString());

            void System.Xml.Serialization.IXmlSerializable.WriteXml(System.Xml.XmlWriter writer) => writer.WriteString(Value);
        }
        """)]
    [InlineData("""
        [ValueObject<string>]
        [System.Xml.Serialization.XmlSchemaProvider(null, IsAny = true)]
        public readonly partial struct Note;
        """)]
    public void A_value_object_with_XML_of_its_own_is_left_alone(string declaration)
    {
        var run = GeneratorHarness.Run(OptIn + declaration);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.Files.Single(file => file.Text.Contains("partial struct Note", StringComparison.Ordinal)).Text.Should().NotContain("Xml");
    }

    /// <summary>
    /// The opt-in adds one name to the type's scope, the public schema provider, the members of <c>IXmlSerializable</c>
    /// being explicit; it is read off the generated code, so that a member added to the emitter is added to the names
    /// a value object may not take.
    /// </summary>
    [Fact]
    public void The_opt_in_adds_the_schema_provider_alone_to_the_names_a_value_object_may_not_take()
    {
        const string Declaration = "[ValueObject<int>] public readonly partial struct Count;";

        var opted = GeneratedMembers.WrittenOn(GeneratorHarness.Run(OptIn + Declaration).SingleValueObject, "Count");
        var plain = GeneratedMembers.WrittenOn(GeneratorHarness.Run(Declaration).SingleValueObject, "Count");

        opted.Except(plain, StringComparer.Ordinal).Should().Equal("GetXmlSchema");
        plain.Except(opted, StringComparer.Ordinal).Should().BeEmpty();

        var named = GeneratorHarness.Run(OptIn + "[ValueObject<string>] public readonly partial struct GetXmlSchema;");
        named.Diagnostics.Should().ContainSingle().Which.GetMessage(CultureInfo.InvariantCulture)
            .Should().StartWith("'GetXmlSchema' takes the name of a member the generated code writes on it");
        var identifier = GeneratorHarness.Run(OptIn + "[EntityId(\"acc\")] public readonly partial struct GetXmlSchema;");
        identifier.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("VO0019");
    }

    [Fact]
    public void An_unrelated_edit_leaves_an_opted_in_model_cached()
    {
        const string Source = OptIn + "[ValueObject<int>] public readonly partial struct Count;";

        var reasons = GeneratorHarness.RunTwice(Source, Source + "\npublic static class Unrelated { public static int Value => 42; }", "ValueObjects");

        reasons.Should().NotBeEmpty();
        reasons.Should().OnlyContain(reason => reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
    }

    [Theory]
    [InlineData("[assembly: ValueObjectXmlSerialization]", "")]
    [InlineData("", "[assembly: ValueObjectXmlSerialization]")]
    [InlineData("[assembly: ValueObjectXmlSerialization]", "[assembly: ValueObjectXmlSerialization(Namespace = \"urn:shipping\")]")]
    public void Opting_in_out_or_changing_the_namespace_re_runs_the_model(string before, string after)
    {
        const string Declaration = "[ValueObject<int>] public readonly partial struct Count;";
        var template = OptIn.Replace("[assembly: ValueObjectXmlSerialization]", "{0}", StringComparison.Ordinal) + Declaration;

        var reasons = GeneratorHarness.RunTwice(
            template.Replace("{0}", before, StringComparison.Ordinal),
            template.Replace("{0}", after, StringComparison.Ordinal),
            "ValueObjects");

        reasons.Should().Contain(IncrementalStepRunReason.Modified);
    }
}
