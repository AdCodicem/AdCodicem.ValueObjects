namespace AdCodicem.ValueObjects.Annotations;

/// <summary>
/// Opts every value object of the assembly into XML serialization: the generator implements
/// <see cref="System.Xml.Serialization.IXmlSerializable"/> and a schema provider on each of them.
/// </summary>
/// <remarks>
/// <para>
/// <c>XmlSerializer</c> and <c>DataContractSerializer</c> then write a value object as its underlying value, in the form
/// <c>XmlSerializer</c> writes that type in, and read it back through its rules: a value the type refuses fails the read
/// with an <see cref="System.Xml.XmlException"/> carrying the rule's code under <see cref="ValueObjectErrors.ErrorCodeKey"/>.
/// Without it, both write an empty element and read back a default instance.
/// </para>
/// <para>
/// The schema each serializer exports describes the value object as an <c>xs:simpleType</c> restricting the XSD type of
/// its underlying value, with its rules as facets.
/// </para>
/// <para>
/// It covers the value objects and the entity identifiers the generator writes in the assembly, but one that implements
/// <see cref="System.Xml.Serialization.IXmlSerializable"/> or declares a schema provider itself, whose implementation
/// stands. A project file sets it with an <c>AssemblyAttribute</c> item whose <c>Include</c> is the full name of this
/// attribute, which the SDK writes into the assembly's attributes.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class ValueObjectXmlSerializationAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the XML namespace of the schema types the value objects are described with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see langword="null"/>, the default, stands for the namespace <c>DataContractSerializer</c> derives from the CLR
    /// namespace of each type, which it resolves against http://schemas.datacontract.org/2004/07/ as a relative URI.
    /// </para>
    /// <para>
    /// A schema type is named after its value object, without the CLR namespace, so a namespace set here puts the value
    /// objects of every CLR namespace of the assembly side by side: two of the same name then share one schema type, and
    /// the schema provider refuses the second with an <see cref="InvalidOperationException"/> when their rules differ,
    /// rather than describe it with the rules of the first. Give such value objects distinct names, or leave this unset.
    /// </para>
    /// </remarks>
    public string? Namespace { get; set; }
}
