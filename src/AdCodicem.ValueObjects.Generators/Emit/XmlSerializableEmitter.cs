using AdCodicem.ValueObjects.Generators.Internal;
using AdCodicem.ValueObjects.Generators.Model;

namespace AdCodicem.ValueObjects.Generators.Emit;

/// <summary>
/// Emits the explicit <c>IXmlSerializable</c> implementation and the schema provider of a value object whose assembly
/// opts into XML serialization.
/// </summary>
/// <remarks>
/// Every member defers to <c>ValueObjectXml</c>, in the contracts, through the type's own type arguments: the generated
/// code reads, writes and describes nothing itself. Reading assigns the instance the serializer created through
/// <c>Unsafe.AsRef</c>, the fields staying <c>readonly</c>.
/// </remarks>
internal static class XmlSerializableEmitter
{
    /// <summary>The public static member <c>XmlSerializer</c> asks for the schema, which it insists on finding public.</summary>
    internal const string SchemaProvider = "GetXmlSchema";

    private const string Xml = "global::System.Xml";
    private const string Bridge = "global::AdCodicem.ValueObjects.Metadata.ValueObjectXml";

    public static void Emit(CodeWriter writer, ValueObjectModel model, string value, string self)
    {
        if (!model.XmlSerializable)
        {
            return;
        }

        writer.Line("/// <inheritdoc />");
        writer.Line($"{Xml}.Schema.XmlSchema? {Xml}.Serialization.IXmlSerializable.GetSchema() => null;");
        writer.Line();

        writer.Line("/// <summary>Reads the element into this instance, normalized and validated.</summary>");
        writer.Line("/// <remarks>Meant for the serializers, which read into an instance of their own: called on a variable, it changes that variable, the one way a readonly value object changes.</remarks>");
        writer.Line($"void {Xml}.Serialization.IXmlSerializable.ReadXml({Xml}.XmlReader reader)");
        writer.Line($"    => global::System.Runtime.CompilerServices.Unsafe.AsRef(in this) = {Bridge}.Read<{self}, {value}>(reader);");
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Line($"void {Xml}.Serialization.IXmlSerializable.WriteXml({Xml}.XmlWriter writer) => {Bridge}.Write<{self}, {value}>(writer, this);");
        writer.Line();

        var ns = model.XmlNamespace is null ? "null" : LiteralFactory.Quote(model.XmlNamespace);
        writer.Line("/// <summary>");
        writer.Line("/// Adds the <c>xs:simpleType</c> describing the type, with its rules as facets, to a schema set, and names it: the schema");
        writer.Line("/// provider <c>XmlSerializer</c> and <c>DataContractSerializer</c> call, and what completes the schemas <c>XmlSchemaExporter</c>");
        writer.Line("/// refers to without including them.");
        writer.Line("/// </summary>");
        writer.Line("/// <param name=\"schemas\">The schema set, which an exporter, a serializer or a tool builds.</param>");
        writer.Line("/// <returns>The qualified name of the type in it.</returns>");
        writer.Line("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        writer.Line($"public static {Xml}.XmlQualifiedName {SchemaProvider}({Xml}.Schema.XmlSchemaSet schemas) => {Bridge}.ProvideSchema<{self}, {value}>(schemas, {ns});");
        writer.Line();
    }
}
