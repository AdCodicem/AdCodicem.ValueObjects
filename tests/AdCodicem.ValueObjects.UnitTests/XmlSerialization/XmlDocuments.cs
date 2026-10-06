using System.Globalization;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace AdCodicem.ValueObjects.UnitTests.XmlSerialization;

/// <summary>
/// A document holding a value object three ways: as a member, as a nullable member and as the items of a list, which is
/// how an application's documents hold one.
/// </summary>
/// <typeparam name="T">The value object.</typeparam>
[XmlRoot("Holder")]
[DataContract(Name = "Holder", Namespace = XmlDocuments.HolderNamespace)]
public sealed class XmlHolder<T>
    where T : struct
{
    [DataMember(Order = 1)]
    public T Value { get; set; }

    [DataMember(Order = 2)]
    public T? Optional { get; set; }

    [DataMember(Order = 3)]
    public List<T> Many { get; set; } = [];
}

/// <summary>The document an application wrote while its member was the primitive a value object replaces.</summary>
/// <typeparam name="T">The primitive.</typeparam>
[XmlRoot("Holder")]
[DataContract(Name = "Holder", Namespace = XmlDocuments.HolderNamespace)]
public sealed class PrimitiveHolder<T>
{
    [DataMember(Order = 1)]
    public T? Value { get; set; }
}

/// <summary>Writes and reads documents through <see cref="XmlSerializer"/> and <see cref="DataContractSerializer"/>.</summary>
public static class XmlDocuments
{
    public const string HolderNamespace = "urn:holder";

    /// <summary>Writes a document through <see cref="XmlSerializer"/>, without a declaration.</summary>
    public static string Serialize<T>(T value)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using (var writer = XmlWriter.Create(text, new XmlWriterSettings { OmitXmlDeclaration = true }))
        {
            serializer.Serialize(writer, value);
        }

        return text.ToString();
    }

    /// <summary>Reads a document through <see cref="XmlSerializer"/>.</summary>
    public static T Deserialize<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = XmlReader.Create(new StringReader(xml));
        return (T)serializer.Deserialize(reader)!;
    }

    /// <summary>Writes a document through <see cref="DataContractSerializer"/>.</summary>
    public static string WriteContract<T>(T value)
    {
        var serializer = new DataContractSerializer(typeof(T));
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using (var writer = XmlWriter.Create(text, new XmlWriterSettings { OmitXmlDeclaration = true }))
        {
            serializer.WriteObject(writer, value);
        }

        return text.ToString();
    }

    /// <summary>Reads a document through <see cref="DataContractSerializer"/>.</summary>
    public static T ReadContract<T>(string xml)
    {
        var serializer = new DataContractSerializer(typeof(T));
        using var reader = XmlReader.Create(new StringReader(xml));
        return (T)serializer.ReadObject(reader)!;
    }

    /// <summary>Gets the text of the first element of a name in a document.</summary>
    public static string TextOf(string xml, string name)
        => XDocument.Parse(xml).Descendants().First(element => element.Name.LocalName == name).Value;
}
