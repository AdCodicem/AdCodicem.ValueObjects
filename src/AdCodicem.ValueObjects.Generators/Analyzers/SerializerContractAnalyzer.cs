using System.Collections.Immutable;
using System.Linq;
using AdCodicem.ValueObjects.Generators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AdCodicem.ValueObjects.Generators.Analyzers;

/// <summary>
/// Reports a third-party serializer's contract attribute on a value object the generator writes.
/// </summary>
/// <remarks>
/// <para>
/// People put the serializer's contract attribute on every type they send. On a value object, that attribute makes the
/// serializer describe the type by its members, and those members are the generator's: <c>Value</c>, the private field
/// holding it and the explicit <c>IsDefault</c>, which the author has nothing to put <c>[Id]</c>, <c>[ProtoMember]</c>,
/// <c>[Key]</c> or <c>[DataMember]</c> on. Orleans and protobuf-net then write the value object with no field and read it
/// back as a default instance, MessagePack refuses its members or writes <c>{}</c>, Newtonsoft.Json writes an object
/// holding <c>Value</c> it cannot read back, and <c>DataContractSerializer</c> and <c>XmlSerializer</c> refuse a value
/// object of an assembly opted into XML serialization (<c>VO0043</c>). MemoryPack, and <c>DataContractSerializer</c> on a
/// <c>[Serializable]</c> value object that does not implement <c>IXmlSerializable</c>, keep the value but read it back
/// without the rules (<c>VO0044</c>). None of the serializers' own analyzers reports any of it.
/// </para>
/// <para>
/// Each attribute is matched by the full metadata name of its class, so the analyzer needs no reference to the
/// serializers, and an attribute of the same name in another namespace, or nested in another type, is left alone.
/// protobuf-net's attribute has a route of its own,
/// <c>[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer&lt;TSelf, TValue&gt;))]</c>, which its compile-time
/// model, and so native AOT, takes: a <c>[ProtoContract]</c> naming a serializer or a surrogate is the author's, unless
/// the serializer is <c>AdCodicem.ValueObjects.ProtobufNet</c>'s closed over another value object, which protobuf-net
/// refuses at run time.
/// </para>
/// <para>
/// A value object the generator writes is what <see cref="RequestDelegateGeneratorAnalyzer.Contract"/> means by one: a
/// readonly struct, neither a record nor a ref struct, annotated with exactly one of <c>[ValueObject&lt;T&gt;]</c> and
/// <c>[EntityId]</c>, over a supported underlying type. A value object written by hand owns its members, and one of a
/// referenced assembly is reported in its own project. The diagnostic is located on the attribute, on whichever partial
/// declaration carries it, which is where the code fix acts.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SerializerContractAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The serializer of <c>AdCodicem.ValueObjects.ProtobufNet</c> the code fix names: the strict profile at Level200.</summary>
    internal const string SerializerName = "AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoSerializer`2";

    /// <summary>The same serializer under a profile of <c>ValueObjectProtoProfiles</c>.</summary>
    private const string ProfiledSerializerName = "AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoSerializer`3";

    /// <summary>
    /// What <c>DataContractSerializer</c> calls in place of reading the fields: the generator implements it under
    /// <c>[assembly: ValueObjectXmlSerialization]</c>, and an author may implement it by hand.
    /// </summary>
    private const string XmlSerializableName = "System.Xml.Serialization.IXmlSerializable";

    private const string Category = "AdCodicem.ValueObjects";

    private const string MessageFormat = "'{0}' carries {1}, so {2}. {3}.";

    private const string HelpLink = "https://adcodicem.github.io/AdCodicem.ValueObjects/docs/reference/diagnostics"
        + "#a-serializers-contract-attribute";

    /// <summary>The attributes the analyzer reads, by the full metadata name of their class.</summary>
    private static readonly ImmutableDictionary<string, Contract> Contracts = new Dictionary<string, Contract>
    {
        ["Orleans.GenerateSerializerAttribute"] = Contract.GenerateSerializer,
        ["ProtoBuf.ProtoContractAttribute"] = Contract.ProtoContract,
        ["System.Runtime.Serialization.DataContractAttribute"] = Contract.DataContract,
        ["System.Xml.Serialization.XmlTypeAttribute"] = Contract.XmlType,
        ["MessagePack.MessagePackObjectAttribute"] = Contract.MessagePackObject,
        ["Newtonsoft.Json.JsonObjectAttribute"] = Contract.JsonObject,
        ["MemoryPack.MemoryPackableAttribute"] = Contract.MemoryPackable,
        ["System.SerializableAttribute"] = Contract.Serializable,
    }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>The format a message names a type in: as the consumer writes it, keywords included.</summary>
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.CSharpShortErrorMessageFormat;

    /// <summary>VO0043, an error: the attribute makes a serializer write the value object with no member, or refuse it.</summary>
    public static readonly DiagnosticDescriptor ContractLosesTheValue = new(
        "VO0043",
        "Serializer contract attribute that loses the value",
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A serializer's contract attribute makes that serializer describe a value object by its members, "
        + "which the generator writes and no attribute of the author can mark: the value is written with no member and "
        + "read back as a default instance, or the serializer refuses the type. Reported for Orleans' "
        + "[GenerateSerializer], protobuf-net's [ProtoContract] that names no serializer of the value object's own, "
        + "[DataContract], [XmlType], [MessagePackObject] and Newtonsoft.Json's [JsonObject].",
        helpLinkUri: HelpLink);

    /// <summary>VO0044, a warning: the attribute makes a serializer read the value object back without its rules.</summary>
    public static readonly DiagnosticDescriptor ContractSkipsTheRules = new(
        "VO0044",
        "Serializer contract attribute that skips the rules",
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The attribute makes a serializer copy what a value object holds and assign it back without "
        + "TryCreate, so a value its rules refuse comes back as an instance of the type: MemoryPack's [MemoryPackable], "
        + "and [Serializable] on a value object that does not implement IXmlSerializable, whose private field "
        + "DataContractSerializer then reads.",
        helpLinkUri: HelpLink);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [ContractLosesTheValue, ContractSkipsTheRules];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            return;
        }

        // The attributes the consumer writes: the generated partial carries none of them.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    /// <summary>
    /// Determines whether an attribute class is one of the contract attributes the analyzer reads, whatever its arguments.
    /// </summary>
    /// <param name="attributeClass">The class of an attribute.</param>
    /// <returns><see langword="true"/> for one of the eight attributes, matched by full metadata name.</returns>
    internal static bool IsContractAttribute(INamedTypeSymbol attributeClass)
        => Classify(attributeClass) != Contract.None;

    /// <summary>Determines whether an attribute class is protobuf-net's <c>[ProtoContract]</c>.</summary>
    /// <param name="attributeClass">The class of an attribute.</param>
    /// <returns><see langword="true"/> for <c>ProtoBuf.ProtoContractAttribute</c>.</returns>
    internal static bool IsProtoContract(INamedTypeSymbol attributeClass)
        => Classify(attributeClass) == Contract.ProtoContract;

    /// <summary>
    /// Gets the serializer the code fix names in a <c>[ProtoContract]</c> on a value object:
    /// <c>ValueObjectProtoSerializer</c> closed over the value object and its underlying type, keeping the profile of a
    /// three-argument form the attribute already names.
    /// </summary>
    /// <param name="type">The type carrying the attribute.</param>
    /// <param name="protoContract">The <c>[ProtoContract]</c> it carries.</param>
    /// <param name="compilation">The compilation declaring it.</param>
    /// <returns>
    /// The serializer, or <see langword="null"/> when none can be named: the type is no value object the generator
    /// writes, it is generic or nested in a generic type, which an attribute cannot name through a type parameter,
    /// protobuf-net has no inbuilt serializer for its underlying type, or the compilation does not reach the serializer.
    /// </returns>
    internal static INamedTypeSymbol? SerializerFor(INamedTypeSymbol type, AttributeData protoContract, Compilation compilation)
    {
        if (ValueOf(type, compilation) is not (var value, var underlying) || type.IsGenericType || !ProtobufNetWrites(underlying))
        {
            return null;
        }

        // A profile already chosen stays, on a copy of another value object's serializer: the level and the trust are the
        // author's, the value object is what the copy got wrong.
        return ProfiledSerializer(protoContract) is { } profiled
            ? profiled.OriginalDefinition.Construct(type, value, profiled.TypeArguments[2])
            : RequestDelegateGeneratorAnalyzer.Reachable(compilation, SerializerName)?.Construct(type, value);
    }

    /// <summary>
    /// Determines whether protobuf-net has an inbuilt serializer for an underlying type, which the package's serializer
    /// writes the value through: every one but <see cref="UnderlyingKind.Int128"/>, <see cref="UnderlyingKind.UInt128"/>
    /// and <see cref="UnderlyingKind.DateTimeOffset"/>.
    /// </summary>
    /// <param name="underlying">The underlying type.</param>
    /// <returns><see langword="true"/> when protobuf-net writes it.</returns>
    internal static bool ProtobufNetWrites(UnderlyingType underlying)
        => underlying.Kind is not (UnderlyingKind.Int128 or UnderlyingKind.UInt128 or UnderlyingKind.DateTimeOffset);

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (ValueOf(type, context.Compilation) is not (var value, var underlying))
        {
            return;
        }

        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeClass
                && Consequence(type, value, underlying, attribute, attributeClass, context.Compilation) is (var descriptor, var effect, var remedy))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    descriptor,
                    attribute.ApplicationSyntaxReference!.GetSyntax(context.CancellationToken).GetLocation(),
                    type.ToDisplayString(TypeFormat),
                    ValueObjectAnnotations.Shown(attributeClass),
                    effect,
                    remedy));
            }
        }
    }

    /// <summary>
    /// Gets what an attribute makes a serializer do to a value object, and its remedy, or <see langword="null"/> for an
    /// attribute left alone.
    /// </summary>
    private static (DiagnosticDescriptor Descriptor, string Effect, string Remedy)? Consequence(
        INamedTypeSymbol type,
        ITypeSymbol value,
        UnderlyingType underlying,
        AttributeData attribute,
        INamedTypeSymbol attributeClass,
        Compilation compilation)
        => Classify(attributeClass) switch
        {
            Contract.GenerateSerializer => (
                ContractLosesTheValue,
                "Orleans writes it with no field and reads it back as a default instance",
                "Remove the attribute, and give Orleans a surrogate holding the underlying value, with a converter that "
                    + "reads it back through TryCreate"),
            Contract.ProtoContract when Copied(type, attribute) => (
                ContractLosesTheValue,
                $"protobuf-net refuses it: the serializer it names is not closed over '{type.ToDisplayString(TypeFormat)}'",
                ProtoRemedy(type, value, underlying, attribute, compilation, copied: true)),
            Contract.ProtoContract when !Names(attribute, "Serializer") && !Names(attribute, "Surrogate") => (
                ContractLosesTheValue,
                "protobuf-net writes it as a message with no field and reads it back as a default instance",
                ProtoRemedy(type, value, underlying, attribute, compilation, copied: false)),
            Contract.DataContract => (
                ContractLosesTheValue,
                "protobuf-net and MessagePack write it with no member and read it back as a default instance, and "
                + "DataContractSerializer refuses it once the assembly is opted into XML serialization",
                "Remove the attribute: [assembly: ValueObjectXmlSerialization] writes the value object as its value"),
            Contract.XmlType => (
                ContractLosesTheValue,
                "protobuf-net writes it as a message with no field and reads it back as a default instance, and "
                + "XmlSerializer refuses it once the assembly is opted into XML serialization",
                "Remove the attribute: the schema provider names the type, and [XmlRoot] names the element of a root value "
                + "object"),
            Contract.MessagePackObject => (
                ContractLosesTheValue,
                "MessagePack serializes its generated members: MsgPack004 refuses them, or, keyed by name, it is written {} "
                + "and read back as a default instance",
                "Remove the attribute, and serialize with WithValueObjects() of AdCodicem.ValueObjects.MessagePack"),
            Contract.JsonObject => (
                ContractLosesTheValue,
                "Newtonsoft.Json writes it as an object holding Value, and reads that object back as a default instance",
                "Remove the attribute: Newtonsoft.Json then writes the value, as text through the generated type converter, "
                + "or as its underlying type with ValueObjectConverter"),
            Contract.MemoryPackable => (
                ContractSkipsTheRules,
                underlying.Kind == UnderlyingKind.String
                    ? "MemoryPack's generated formatter does not compile, CS8377"
                    : "MemoryPack copies its memory and reads it back without its rules",
                "No integration of this library serializes with MemoryPack: remove the attribute, and use a serializer that "
                + "reads the value object through its rules"),

            // Where the value object implements IXmlSerializable, DataContractSerializer calls it and reads no field.
            // Elsewhere, removing the attribute loses the value: DataContractSerializer then writes an empty element.
            Contract.Serializable when !type.AllInterfaces.Any(static contract => FullName(contract) == XmlSerializableName) => (
                ContractSkipsTheRules,
                "DataContractSerializer writes its private field and reads it back without its rules",
                "Opt the assembly into XML serialization with [assembly: ValueObjectXmlSerialization]. Remove the "
                + "attribute only where no DataContractSerializer writes the type: without it, DataContractSerializer "
                + "writes an empty element and reads back a default instance"),
            _ => null,
        };

    /// <summary>
    /// Gets the remedy of a <c>[ProtoContract]</c> reported, the first that applies: a value object protobuf-net has no
    /// serializer for, whatever its shape, since <c>AddValueObject</c> refuses it too, a generic one, a serializer
    /// copied from another value object, and the bare attribute, with or without the package in reach.
    /// </summary>
    private static string ProtoRemedy(
        INamedTypeSymbol type,
        ITypeSymbol value,
        UnderlyingType underlying,
        AttributeData attribute,
        Compilation compilation,
        bool copied)
    {
        // First, since AddValueObject<TSelf, TValue>() throws NotSupportedException for such a construction too.
        if (!ProtobufNetWrites(underlying))
        {
            return $"protobuf-net has no serializer for {value.ToDisplayString(TypeFormat)}: remove the attribute";
        }

        // IsGenericType holds for a type nested in a generic type too, whose own type parameters are none.
        if (type.IsGenericType)
        {
            return "An attribute cannot name a serializer through a type parameter: remove it, and register each "
                + "construction with AddValueObject<TSelf, TValue>() of AdCodicem.ValueObjects.ProtobufNet";
        }

        // Written as text rather than through SerializerFor, so that it also reads where the package is not referenced.
        var profile = ProfiledSerializer(attribute) is { } profiled
            ? $", {profiled.TypeArguments[2].ToDisplayString(TypeFormat)}"
            : string.Empty;
        var written = $"[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<{type.ToDisplayString(TypeFormat)}, "
            + $"{value.ToDisplayString(TypeFormat)}{profile}>))]";

        if (copied)
        {
            return $"Name its own, {written}";
        }

        return RequestDelegateGeneratorAnalyzer.Reachable(compilation, SerializerName) is null
            ? $"Reference AdCodicem.ValueObjects.ProtobufNet and name its serializer, {written}, or remove the attribute"
            : $"Name its serializer, {written}, or remove the attribute and register the value object with AddValueObjects";
    }

    /// <summary>
    /// Gets the value object a type is, its underlying value type and the generator's description of it, or
    /// <see langword="null"/> for a type the generator writes nothing for.
    /// </summary>
    private static (ITypeSymbol Value, UnderlyingType Underlying)? ValueOf(INamedTypeSymbol type, Compilation compilation)
    {
        // The shapes VO0002 refuses get no member from the generator, which the attributes would describe.
        if (type.TypeKind != TypeKind.Struct || !type.IsReadOnly || type.IsRecord || type.IsRefLikeType)
        {
            return null;
        }

        var valueObject = ValueObjectGenerator.FindAttribute(type, ValueObjectGenerator.ValueObjectAttributeName);
        var entityId = ValueObjectGenerator.CarriesAttribute(type, ValueObjectGenerator.EntityIdAttributeName);

        // Neither annotation, a value object written by hand or none at all, or both, which is VO0018.
        if ((valueObject is null) != entityId)
        {
            return null;
        }

        var value = entityId
            ? compilation.GetSpecialType(SpecialType.System_String)
            : valueObject!.AttributeClass!.TypeArguments[0];

        // An unsupported underlying type is VO0003, and nothing is generated.
        return UnderlyingType.TryResolve(value.ToDisplayString(ValueObjectGenerator.QualifiedFormat), out var underlying)
            ? (value, underlying)
            : null;
    }

    /// <summary>
    /// Determines whether a <c>[ProtoContract]</c> names the package's serializer closed over another type than the value
    /// object, or not closed at all, which protobuf-net refuses: "No serializer for type … is available for model".
    /// </summary>
    private static bool Copied(INamedTypeSymbol type, AttributeData attribute)
        => NamedSerializer(attribute) is { } named
           && FullName(named.OriginalDefinition) is SerializerName or ProfiledSerializerName
           && !SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], type);

    /// <summary>
    /// Gets the package's serializer under a profile, closed, which a <c>[ProtoContract]</c> names, or
    /// <see langword="null"/> for any other.
    /// </summary>
    private static INamedTypeSymbol? ProfiledSerializer(AttributeData protoContract)
        => NamedSerializer(protoContract) is { IsUnboundGenericType: false } named
           && FullName(named.OriginalDefinition) == ProfiledSerializerName
            ? named
            : null;

    /// <summary>Gets the type a <c>[ProtoContract]</c> names as its <c>Serializer</c>, or <see langword="null"/> for none.</summary>
    private static INamedTypeSymbol? NamedSerializer(AttributeData attribute)
        => attribute.NamedArguments.FirstOrDefault(static argument => argument.Key == "Serializer").Value.Value as INamedTypeSymbol;

    /// <summary>Determines whether an attribute sets a named argument to anything but <see langword="null"/>.</summary>
    private static bool Names(AttributeData attribute, string property)
        => attribute.NamedArguments.Any(argument => argument.Key == property && argument.Value.Value is not null);

    /// <summary>Gets which of the contract attributes an attribute class is, by its full metadata name.</summary>
    private static Contract Classify(INamedTypeSymbol attributeClass)
        => Contracts.TryGetValue(FullName(attributeClass), out var contract) ? contract : Contract.None;

    /// <summary>
    /// Gets a type's full metadata name: its namespace, a dot and its metadata name, or, for a nested type, the full
    /// name of the type containing it, a plus and its metadata name. None of the names read is nested, so a nested type
    /// of the same name matches none, and nor does a type of the global namespace.
    /// </summary>
    internal static string FullName(INamedTypeSymbol type)
        => type.ContainingType is { } containing
            ? $"{FullName(containing)}+{type.MetadataName}"
            : $"{type.ContainingNamespace.ToDisplayString()}.{type.MetadataName}";

    /// <summary>The contract attributes the analyzer reads.</summary>
    private enum Contract
    {
        /// <summary>Any other attribute.</summary>
        None,

        /// <summary>Orleans' <c>[GenerateSerializer]</c>.</summary>
        GenerateSerializer,

        /// <summary>protobuf-net's <c>[ProtoContract]</c>.</summary>
        ProtoContract,

        /// <summary><c>[DataContract]</c>.</summary>
        DataContract,

        /// <summary><c>[XmlType]</c>.</summary>
        XmlType,

        /// <summary>MessagePack's <c>[MessagePackObject]</c>.</summary>
        MessagePackObject,

        /// <summary>Newtonsoft.Json's <c>[JsonObject]</c>.</summary>
        JsonObject,

        /// <summary>MemoryPack's <c>[MemoryPackable]</c>.</summary>
        MemoryPackable,

        /// <summary><c>[Serializable]</c>.</summary>
        Serializable,
    }
}
