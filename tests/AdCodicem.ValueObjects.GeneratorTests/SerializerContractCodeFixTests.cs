using AdCodicem.ValueObjects.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// The code fix of <c>VO0043</c> and <c>VO0044</c>: it names protobuf-net's serializer in a <c>[ProtoContract]</c>, the
/// attribute route of <c>AdCodicem.ValueObjects.ProtobufNet</c>, or removes the attribute reported.
/// </summary>
/// <remarks>
/// Each fixed source is compiled through the generator and analyzed again, against the package: it must compile, and
/// the analyzer must have nothing left to say about the attribute fixed.
/// </remarks>
public sealed class SerializerContractCodeFixTests
{
    private const string Name = "Name the value object's protobuf-net serializer, strict at Level200";

    private const string RemoveSerializable =
        "Remove [Serializable]: DataContractSerializer then writes an empty element";

    [Theory]
    [InlineData("""
        [ValueObject<string>(MaxLength = 8)]
        [ProtoContract]
        public readonly partial struct Sku;
        """, "[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Sku, string>))]")]
    [InlineData("""
        [ValueObject<string>]
        [ProtoContract(Name = "sku")]
        public readonly partial struct Sku;
        """, "[ProtoContract(Name = \"sku\", Serializer = typeof(ValueObjectProtoSerializer<Sku, string>))]")]
    [InlineData("""
        [ValueObject<Guid>, ProtoContract]
        public readonly partial struct OrderId;
        """, "[ValueObject<Guid>, ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<OrderId, Guid>))]")]
    [InlineData("""
        [ValueObject<string>]
        [ProtoContract(Serializer = null, Name = "sku")]
        public readonly partial struct Sku;
        """, "[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Sku, string>), Name = \"sku\")]")]
    [InlineData("""
        [EntityId("ord")]
        [ProtoContract]
        public readonly partial struct OrderRef;
        """, "[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<OrderRef, string>))]")]
    [InlineData("""
        public static partial class Catalog
        {
            [ValueObject<int>]
            [ProtoContract]
            public readonly partial struct Quantity;
        }
        """, "[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Quantity, int>))]")]
    public async Task The_fix_names_the_serializer_and_imports_its_namespace(string source, string fixedLine)
    {
        var fixedSource = await FixAsync(source, Name);

        var lines = Lines(fixedSource);
        lines.Should().Contain(fixedLine);
        lines.Should().ContainSingle(line => line == "using AdCodicem.ValueObjects.ProtobufNet;");
        await ShouldBeSilentAndCompileAsync(fixedSource);
    }

    /// <summary>
    /// A serializer copied from another value object is closed over this one instead, a profile it names kept, which
    /// the title says: the level and the trust are the author's choice, the type is what the copy got wrong. The
    /// unbound form gets the strict profile at Level200.
    /// </summary>
    [Theory]
    [InlineData("""
        [ValueObject<string>]
        [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Other, string>))]
        public readonly partial struct Sku;
        """, Name, "[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Sku, string>))]")]
    [InlineData("""
        [ValueObject<Guid>]
        [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Other, string, ValueObjectProtoProfiles.StrictLevel300>))]
        public readonly partial struct CustomerId;
        """, "Name the value object's protobuf-net serializer, keeping the profile StrictLevel300",
        "[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<CustomerId, Guid, ValueObjectProtoProfiles.StrictLevel300>))]")]
    [InlineData("""
        [ValueObject<decimal>]
        [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Other, string, ValueObjectProtoProfiles.TrustedLevel240>))]
        public readonly partial struct Amount;
        """, "Name the value object's protobuf-net serializer, keeping the profile TrustedLevel240",
        "[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Amount, decimal, ValueObjectProtoProfiles.TrustedLevel240>))]")]
    [InlineData("""
        [ValueObject<int>]
        [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<,>))]
        public readonly partial struct Quantity;
        """, Name, "[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Quantity, int>))]")]
    [InlineData("""
        [ValueObject<int>]
        [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<,,>))]
        public readonly partial struct Quantity;
        """, Name, "[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Quantity, int>))]")]
    public async Task The_fix_closes_a_copied_serializer_over_the_value_object(
        string source,
        string title,
        string fixedLine)
    {
        var fixedSource = await FixAsync(
            """
            using AdCodicem.ValueObjects.ProtobufNet;

            [ValueObject<string>]
            [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Other, string>))]
            public readonly partial struct Other;

            """ + source,
            title);

        Lines(fixedSource).Should().Contain(fixedLine);
        await ShouldBeSilentAndCompileAsync(fixedSource);
    }

    /// <summary>
    /// Removing an attribute alone in its list removes the list, and keeps the doc comment and the directives above it;
    /// a list sharing its line with what precedes it leaves that line where it ended, and the declaration on its own
    /// line; from a list holding others, it removes the attribute and its separator.
    /// </summary>
    [Theory]
    [InlineData("""
        /// <summary>A stock-keeping unit.</summary>
        [ProtoContract]
        [ValueObject<string>(MaxLength = 8)]
        public readonly partial struct Sku;
        """, "Remove [ProtoContract]", """
        /// <summary>A stock-keeping unit.</summary>
        [ValueObject<string>(MaxLength = 8)]
        public readonly partial struct Sku;
        """)]
    [InlineData("""
        /// <summary>A stock-keeping unit.</summary>
        [ValueObject<string>(MaxLength = 8)]
        [ProtoContract] // sent over gRPC
        public readonly partial struct Sku;
        """, "Remove [ProtoContract]", """
        /// <summary>A stock-keeping unit.</summary>
        [ValueObject<string>(MaxLength = 8)]
        public readonly partial struct Sku;
        """)]
    [InlineData("""
        public static partial class Catalog
        {
            #pragma warning disable CS0618 // Kept with the attribute it was written for.
            [DataContract]
            #pragma warning restore CS0618

            [ValueObject<int>]
            public readonly partial struct Quantity;
        }
        """, "Remove [DataContract]", """
        public static partial class Catalog
        {
            #pragma warning disable CS0618 // Kept with the attribute it was written for.
            #pragma warning restore CS0618

            [ValueObject<int>]
            public readonly partial struct Quantity;
        }
        """)]
    [InlineData("""
        [ValueObject<int>]
        [MemoryPackable]

        public readonly partial struct Quantity;
        """, "Remove [MemoryPackable]", """
        [ValueObject<int>]
        public readonly partial struct Quantity;
        """)]
    [InlineData("""
        [ProtoContract, ValueObject<Guid>]
        public readonly partial struct OrderId;
        """, "Remove [ProtoContract]", """
        [ValueObject<Guid>]
        public readonly partial struct OrderId;
        """)]
    [InlineData("""
        [ValueObject<Guid>, Serializable]
        public readonly partial struct LineId;
        """, RemoveSerializable, """
        [ValueObject<Guid>]
        public readonly partial struct LineId;
        """)]
    [InlineData("""
        [ValueObject<string>(MaxLength = 8), ProtoContract, DataContract]
        public readonly partial struct Sku;
        """, "Remove [ProtoContract]", """
        [ValueObject<string>(MaxLength = 8), DataContract]
        public readonly partial struct Sku;
        """)]
    [InlineData("""
        [ValueObject<string>(MaxLength = 8), ProtoContract, DataContract]
        public readonly partial struct Sku;
        """, "Remove [DataContract]", """
        [ValueObject<string>(MaxLength = 8), ProtoContract]
        public readonly partial struct Sku;
        """)]
    [InlineData("""
        [ValueObject<string>]
        [GenerateSerializer]
        [Alias("sku")]
        public readonly partial struct Sku;
        """, "Remove [GenerateSerializer]", """
        [ValueObject<string>]
        [Alias("sku")]
        public readonly partial struct Sku;
        """)]
    [InlineData("""
        [ValueObject<string>]
        [XmlType("sku")]
        [MessagePackObject(keyAsPropertyName: true)]
        [JsonObject]
        public readonly partial struct Sku;
        """, "Remove [MessagePackObject]", """
        [ValueObject<string>]
        [XmlType("sku")]
        [JsonObject]
        public readonly partial struct Sku;
        """)]
    [InlineData("""
        [ValueObject<string>] [ProtoContract]
        public readonly partial struct Sku;
        """, "Remove [ProtoContract]", """
        [ValueObject<string>]
        public readonly partial struct Sku;
        """)]
    [InlineData("""
        public static partial class Catalog
        {
            [ValueObject<int>] /* counted */ [DataContract] // sent as a message
            public readonly partial struct Quantity;
        }
        """, "Remove [DataContract]", """
        public static partial class Catalog
        {
            [ValueObject<int>] /* counted */
            public readonly partial struct Quantity;
        }
        """)]
    [InlineData("""
        [ValueObject<Guid>] [Serializable] public readonly partial struct LineId;
        """, RemoveSerializable, """
        [ValueObject<Guid>] public readonly partial struct LineId;
        """)]
    [InlineData("""
        [ProtoContract] [ValueObject<string>]
        public readonly partial struct Sku;
        """, "Remove [ProtoContract]", """
        [ValueObject<string>]
        public readonly partial struct Sku;
        """)]
    public async Task The_fix_removes_the_attribute_alone(string source, string title, string expected)
    {
        var fixedSource = await FixAsync(source, title);

        fixedSource.Should().EndWith("namespace Shop;\n\n" + expected);
    }

    /// <summary>
    /// A list opening the file, with no token before it to hand its line break to, goes with it, and the comment above
    /// it stays.
    /// </summary>
    [Fact]
    public async Task A_list_opening_the_file_is_removed()
    {
        var fixedSource = await GeneratorHarness.FixAsync<SerializerContractAnalyzer, SerializerContractCodeFixProvider>(
            """
            // What the catalogue sells.
            [ProtoBuf.ProtoContract]
            [AdCodicem.ValueObjects.Annotations.ValueObject<string>]
            public readonly partial struct Sku;

            namespace Shop
            {
                [AdCodicem.ValueObjects.Annotations.ValueObject<int>]
                public readonly partial struct Quantity;
            }
            """,
            SerializerReferences.WithPackage,
            title: "Remove [ProtoContract]");

        fixedSource.Should().StartWith("""
            // What the catalogue sells.
            [AdCodicem.ValueObjects.Annotations.ValueObject<string>]
            public readonly partial struct Sku;
            """);
    }

    /// <summary>
    /// Each attribute reported can be removed, under a title naming it, and the naming is offered beside the removal only
    /// where it can be written: the package in reach, a value object neither generic nor nested in a generic type, and an
    /// underlying type protobuf-net writes.
    /// </summary>
    [Theory]
    [InlineData("""
        [ValueObject<string>]
        [ProtoContract]
        public readonly partial struct Sku;
        """, true, new[] { Name, "Remove [ProtoContract]" })]
    [InlineData("""
        [ValueObject<string>]
        [ProtoContract]
        public readonly partial struct Sku;
        """, false, new[] { "Remove [ProtoContract]" })]
    [InlineData("""
        [ValueObject<string>]
        [ProtoContract]
        public readonly partial struct Reference<TOwner>;
        """, true, new[] { "Remove [ProtoContract]" })]
    [InlineData("""
        public static partial class Catalog<T>
        {
            [ValueObject<int>]
            [ProtoContract]
            public readonly partial struct Stock;
        }
        """, true, new[] { "Remove [ProtoContract]" })]
    [InlineData("""
        [ValueObject<Int128>]
        [ProtoContract]
        public readonly partial struct Serial;
        """, true, new[] { "Remove [ProtoContract]" })]
    [InlineData("""
        [ValueObject<UInt128>]
        [ProtoContract]
        public readonly partial struct Serial;
        """, true, new[] { "Remove [ProtoContract]" })]
    [InlineData("""
        [ValueObject<DateTimeOffset>]
        [ProtoContract]
        public readonly partial struct Stamp;
        """, true, new[] { "Remove [ProtoContract]" })]
    [InlineData("""
        [ValueObject<string>]
        [GenerateSerializer]
        public readonly partial struct Sku;
        """, true, new[] { "Remove [GenerateSerializer]" })]
    [InlineData("""
        [ValueObject<string>]
        [DataContract]
        public readonly partial struct Sku;
        """, true, new[] { "Remove [DataContract]" })]
    [InlineData("""
        [ValueObject<string>]
        [XmlType]
        public readonly partial struct Sku;
        """, true, new[] { "Remove [XmlType]" })]
    [InlineData("""
        [ValueObject<string>]
        [MessagePackObject]
        public readonly partial struct Sku;
        """, true, new[] { "Remove [MessagePackObject]" })]
    [InlineData("""
        [ValueObject<string>]
        [JsonObject]
        public readonly partial struct Sku;
        """, true, new[] { "Remove [JsonObject]" })]
    [InlineData("""
        [ValueObject<int>]
        [MemoryPackable]
        public readonly partial struct Quantity;
        """, true, new[] { "Remove [MemoryPackable]" })]
    [InlineData("""
        [ValueObject<int>]
        [Serializable]
        public readonly partial struct Quantity;
        """, true, new[] { RemoveSerializable })]
    public async Task The_naming_is_offered_only_where_it_can_be_written(string source, bool package, string[] titles)
    {
        var offered = await GeneratorHarness.OfferedFixTitlesAsync<SerializerContractAnalyzer>(
            new SerializerContractCodeFixProvider(),
            Wrapped(source),
            package ? SerializerReferences.WithPackage : SerializerReferences.WithSerializers);

        offered.Should().ContainSingle().Which.Should().Equal(titles);
    }

    [Fact]
    public async Task Fixing_all_in_a_document_names_each_serializer_with_one_import()
    {
        var fixedSource = await GeneratorHarness.FixAllAsync<SerializerContractAnalyzer, SerializerContractCodeFixProvider>(
            Wrapped("""
                [ValueObject<string>]
                [ProtoContract]
                public readonly partial struct Sku;

                [ValueObject<int>, ProtoContract]
                public readonly partial struct Quantity;

                [ValueObject<long>]
                [DataContract]
                public readonly partial struct Tally;
                """),
            SerializerReferences.WithPackage,
            title: Name);

        Lines(fixedSource).Should().ContainSingle(line => line == "using AdCodicem.ValueObjects.ProtobufNet;");
        fixedSource.Should().EndWith("""
            namespace Shop;

            [ValueObject<string>]
            [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Sku, string>))]
            public readonly partial struct Sku;

            [ValueObject<int>, ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Quantity, int>))]
            public readonly partial struct Quantity;

            [ValueObject<long>]
            [DataContract]
            public readonly partial struct Tally;
            """);
    }

    [Fact]
    public async Task Fixing_all_in_a_document_removes_only_the_attribute_chosen()
    {
        var fixedSource = await GeneratorHarness.FixAllAsync<SerializerContractAnalyzer, SerializerContractCodeFixProvider>(
            Wrapped("""
                /// <summary>A stock-keeping unit.</summary>
                [ProtoContract]
                [ValueObject<string>]
                public readonly partial struct Sku;

                [ValueObject<int>, ProtoContract, DataContract]
                public readonly partial struct Quantity;

                [ValueObject<long>]
                [DataContract]
                public readonly partial struct Tally;
                """),
            SerializerReferences.WithPackage,
            title: "Remove [ProtoContract]");

        fixedSource.Should().EndWith("""
            namespace Shop;

            /// <summary>A stock-keeping unit.</summary>
            [ValueObject<string>]
            public readonly partial struct Sku;

            [ValueObject<int>, DataContract]
            public readonly partial struct Quantity;

            [ValueObject<long>]
            [DataContract]
            public readonly partial struct Tally;
            """);
    }

    [Fact]
    public void The_fix_is_offered_for_both_diagnostics_and_fixes_all_in_one_batch()
    {
        var fix = new SerializerContractCodeFixProvider();

        fix.FixableDiagnosticIds.Should().Equal("VO0043", "VO0044");
        fix.GetFixAllProvider().Should().BeSameAs(WellKnownFixAllProviders.BatchFixer);
    }

    /// <summary>
    /// The analyzer reports only on a contract attribute of a value object, so a diagnostic anywhere else is only met in
    /// a direct call: nothing is offered where no contract attribute is, and only the removal where the attribute applies
    /// to no type declaration the naming could reach.
    /// </summary>
    [Theory]
    [InlineData("using System;", "using System;", new string[0])]
    [InlineData("ValueObject<string>]\n[Missing]", "ValueObject<string>", new string[0])]
    [InlineData("[Missing]", "Missing", new string[0])]
    [InlineData("[ProtoContract]\npublic sealed class Order", "ProtoContract", new[] { "Remove [ProtoContract]" })]
    [InlineData("[ProtoContract]\npublic enum Status", "ProtoContract", new[] { "Remove [ProtoContract]" })]
    [InlineData("[method: ProtoContract]", "ProtoContract", new[] { "Remove [ProtoContract]" })]
    public async Task What_is_offered_where_the_analyzer_reports_nothing(string context, string spanned, string[] titles)
    {
        var source = Wrapped("""
            [ValueObject<string>]
            [Missing]
            public readonly partial struct Sku;

            [ProtoContract]
            public sealed class Order;

            [ProtoContract]
            public enum Status
            {
                Open,
            }

            [ValueObject<string>]
            [method: ProtoContract]
            public readonly partial struct Code;
            """);

        var offered = await GeneratorHarness.OfferedFixTitlesAsync(
            new SerializerContractCodeFixProvider(),
            source,
            SerializerContractAnalyzer.ContractLosesTheValue,
            text => new TextSpan(
                text.IndexOf(context, StringComparison.Ordinal) + context.IndexOf(spanned, StringComparison.Ordinal),
                spanned.Length),
            SerializerReferences.WithPackage);

        offered.Should().Equal(titles);
    }

    /// <summary>
    /// The naming answers <c>VO0043</c> alone, the diagnostic a <c>[ProtoContract]</c> gets: met with another, in a direct
    /// call, the attribute can only be removed.
    /// </summary>
    [Fact]
    public async Task The_naming_answers_the_diagnostic_of_a_ProtoContract_alone()
    {
        var source = Wrapped("""
            [ValueObject<string>]
            [ProtoContract]
            public readonly partial struct Sku;
            """);

        var offered = await GeneratorHarness.OfferedFixTitlesAsync(
            new SerializerContractCodeFixProvider(),
            source,
            SerializerContractAnalyzer.ContractSkipsTheRules,
            text => new TextSpan(text.IndexOf("ProtoContract]", StringComparison.Ordinal), "ProtoContract".Length),
            SerializerReferences.WithPackage);

        offered.Should().Equal("Remove [ProtoContract]");
    }

    /// <summary>Puts a snippet in the namespace and under the usings of <see cref="SerializerContractAnalyzerTests"/>.</summary>
    private static string Wrapped(string source) => SerializerContractAnalyzerTests.Usings + "\nnamespace Shop;\n\n" + source;

    /// <summary>Applies the action of a title, against the package, to each diagnostic it is offered for.</summary>
    private static Task<string> FixAsync(string source, string title)
        => GeneratorHarness.FixAsync<SerializerContractAnalyzer, SerializerContractCodeFixProvider>(
            Wrapped(source),
            SerializerReferences.WithPackage,
            title: title);

    /// <summary>
    /// Asserts that fixed source compiles with the generator and that the analyzer reports nothing it did not report
    /// before on another attribute.
    /// </summary>
    private static async Task ShouldBeSilentAndCompileAsync(string fixedSource)
        => (await SerializerContractAnalyzerTests.RunAsync(fixedSource, wrap: false)).Should().BeEmpty();

    private static IReadOnlyList<string> Lines(string source)
        => [.. source.Split('\n').Select(line => line.Trim())];
}
