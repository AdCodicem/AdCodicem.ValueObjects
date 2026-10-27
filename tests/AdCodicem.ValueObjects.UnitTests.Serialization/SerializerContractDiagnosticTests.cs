using System.Globalization;
using System.IO;
using System.Reflection;
using AdCodicem.ValueObjects.MessagePack;
using AdCodicem.ValueObjects.NewtonsoftJson;
using AdCodicem.ValueObjects.ProtobufNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// <c>VO0043</c> over the real protobuf-net, MessagePack and Newtonsoft.Json and this family's packages: the analyzer
/// and the generator the compiler runs over this project, loaded from the copy <c>CopyValueObjectAnalyzers</c> puts
/// next to the tests, over value objects the generator writes. The analyzer names the satellites' APIs by string, which
/// nothing else compiles against the satellites, so each name it writes is checked here against the satellite itself:
/// a rename in one package fails these tests, not a consumer's build.
/// </summary>
public sealed class SerializerContractDiagnosticTests
{
    private const string Source = """
        using AdCodicem.ValueObjects.Annotations;
        using AdCodicem.ValueObjects.ProtobufNet;
        using MessagePack;
        using Newtonsoft.Json;
        using ProtoBuf;

        namespace Analyzed;

        [ValueObject<string>]
        [MessagePackObject]
        public readonly partial struct Packed;

        [ValueObject<string>]
        [JsonObject]
        public readonly partial struct Objected;

        [ValueObject<string>]
        [ProtoContract]
        public readonly partial struct Bare;

        [ValueObject<string>]
        [ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Bare, string>))]
        public readonly partial struct CopiedPlain;

        [ValueObject<string>]
        [ProtoContract(
            Serializer = typeof(ValueObjectProtoSerializer<Bare, string, ValueObjectProtoProfiles.StrictLevel240>))]
        public readonly partial struct CopiedProfiled;

        public static partial class Outer<T>
        {
            [ValueObject<string>]
            [ProtoContract]
            public readonly partial struct Nested;
        }
        """;

    [Fact]
    public async Task Each_satellite_API_the_analyzer_names_is_the_satellites_own()
    {
        var protobufNet = typeof(ValueObjectProtoModelExtensions).Assembly.GetName().Name;
        var messagePack = typeof(ValueObjectMessagePackExtensions).Assembly.GetName().Name;
        var serializer = typeof(ValueObjectProtoSerializer<,>).Name.Split('`')[0];
        const string registrationName = nameof(ValueObjectProtoModelExtensions.AddValueObject);
        var registration = typeof(ValueObjectProtoModelExtensions).GetMethod(registrationName)!;
        var parameters = string.Join(", ", registration.GetGenericArguments().Select(static argument => argument.Name));

        var messages = await MessagesAsync(Source);

        messages["Packed"].Should().EndWith(
            $"serialize with {nameof(ValueObjectMessagePackExtensions.WithValueObjects)}() of {messagePack}.");
        messages["Objected"].Should().EndWith($"or as its underlying type with {nameof(ValueObjectConverter)}.");

        // The serializer's name reached: the remedy names the attribute form and the registration, never the reference.
        messages["Bare"].Should().EndWith(
            $"Name its serializer, [ProtoContract(Serializer = typeof({serializer}<Bare, string>))], or remove the "
            + "attribute and register the value object with "
            + $"{nameof(ValueObjectProtoModelExtensions.AddValueObjects)}.");

        // Both serializers' names matched: each copied attribute is told to name its own, the profile kept.
        messages["CopiedPlain"].Should().EndWith(
            $"Name its own, [ProtoContract(Serializer = typeof({serializer}<CopiedPlain, string>))].");
        messages["CopiedProfiled"].Should().EndWith(
            $"Name its own, [ProtoContract(Serializer = typeof({serializer}<CopiedProfiled, string, "
            + $"{nameof(ValueObjectProtoProfiles)}.{nameof(ValueObjectProtoProfiles.StrictLevel240)}>))].");

        messages["Nested"].Should().EndWith(
            $"register each construction with {registration.Name}<{parameters}>() of {protobufNet}.");
    }

    /// <summary>
    /// The messages <c>VO0043</c> reports over a source the generator ran on, by the name of the type each is on, the
    /// source and the generated code compiling without an error.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>> MessagesAsync(string source)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));
        var parsing = new CSharpParseOptions(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create(
            "Analyzed",
            [CSharpSyntaxTree.ParseText(source, parsing, cancellationToken: cancellation)],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var generators = Assembly.LoadFrom(
            Path.Combine(AppContext.BaseDirectory, "analyzers", "AdCodicem.ValueObjects.Generators.dll"));
        var generator = (IIncrementalGenerator)Activator.CreateInstance(
            generators.GetType("AdCodicem.ValueObjects.Generators.ValueObjectGenerator", throwOnError: true)!)!;
        CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: parsing)
            .RunGeneratorsAndUpdateCompilation(compilation, out var generated, out _, cancellation);
        generated.GetDiagnostics(cancellation)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();

        var analyzer = (DiagnosticAnalyzer)Activator.CreateInstance(
            generators.GetType(
                "AdCodicem.ValueObjects.Generators.Analyzers.SerializerContractAnalyzer", throwOnError: true)!)!;
        var diagnostics = await generated.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync(cancellation);

        return diagnostics.Where(static diagnostic => diagnostic.Id == "VO0043").ToDictionary(
            static diagnostic => diagnostic.Location.SourceTree!.GetRoot().FindNode(diagnostic.Location.SourceSpan)
                .AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First().Identifier.Text,
            static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture));
    }
}
