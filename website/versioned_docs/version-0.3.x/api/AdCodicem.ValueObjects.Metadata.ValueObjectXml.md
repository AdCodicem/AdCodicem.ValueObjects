# Class ValueObjectXml {#AdCodicem_ValueObjects_Metadata_ValueObjectXml}

Namespace: [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Reads, writes and describes a value object in XML, for the <code>IXmlSerializable</code> implementation the generator emits
on the value objects of an assembly marked <code>[assembly: ValueObjectXmlSerialization]</code>.

```csharp
public static class ValueObjectXml
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectXml](AdCodicem.ValueObjects.Metadata.ValueObjectXml.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
A value object is written as the content of its element, in the form <code>XmlSerializer</code> writes its underlying type
in, and read in the form <code>XmlSerializer</code> reads that type in, then through its rules: it reads every document
written when the member was the primitive it replaces. A 128-bit integer, which <code>XmlSerializer</code> writes as an
empty element, is written as its digits.
</p>
<p>
A refusal is an <xref href="System.Xml.XmlException" data-throw-if-not-resolved="false"></xref> naming the type and the rule, never the text, which carries the code of the
rule under <xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>: <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref> for an element
marked <code>xsi:nil</code>, <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable" data-throw-if-not-resolved="false"></xref> for text that is not of the underlying type, or
an element holding elements, the code of the rule otherwise.
</p>
<p>
A value object written by hand implements <code>IXmlSerializable</code> through these methods too, as long as its
underlying type is one the generator supports.
</p>

## Methods

### ProvideSchema<TSelf, TValue\>\(XmlSchemaSet, string?\) {#AdCodicem_ValueObjects_Metadata_ValueObjectXml_ProvideSchema__2_System_Xml_Schema_XmlSchemaSet_System_String_}

Adds the <code>xs:simpleType</code> describing a value object to a schema set, once, and names it.

```csharp
public static XmlQualifiedName ProvideSchema<TSelf, TValue>(XmlSchemaSet schemas, string? xmlNamespace) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`schemas` [XmlSchemaSet](https://learn.microsoft.com/dotnet/api/system.xml.schema.xmlschemaset)

The schema set an exporter or a serializer hands the schema provider.

`xmlNamespace` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The namespace of the type, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for the one <code>DataContractSerializer</code> derives from its CLR
namespace.

#### Returns

 [XmlQualifiedName](https://learn.microsoft.com/dotnet/api/system.xml.xmlqualifiedname)

The qualified name of the type.

#### Type Parameters

`TSelf` 

The value object.

`TValue` 

Its underlying type.

#### Remarks

<p>
The type restricts the XSD type of the underlying value with the rules of <code>TSelf.Schema</code>: the lengths as
<code>minLength</code> and <code>maxLength</code>, the pattern as <code>pattern</code> when XSD can say what it means, the bounds as
<code>minInclusive</code> and <code>maxInclusive</code>, the known values of a closed set as <code>enumeration</code> and the
description as documentation. A facet System.Xml cannot read, such as a bound of a 128-bit integer beyond what a
<xref href="System.Decimal" data-throw-if-not-resolved="false"></xref> holds, is left out, since <code>XmlSerializer</code> would refuse the whole type for it.
</p>
<p>
The type is named after the value object, after the types it is nested in and joined to them with a dot, and,
for a construction of a generic value object, followed by <code>Of</code> and the names of its type arguments. Its name
leaves out the CLR namespace, which the default XML namespace carries: two value objects of one name put under
one XML namespace share one type when their rules agree, and are refused when they differ, since the type would
describe the second with the rules of the first.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">schemas</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

The schema set holds a type of that name and namespace describing another value object, with other rules.

 [NotSupportedException](https://learn.microsoft.com/dotnet/api/system.notsupportedexception)

<code class="typeparamref">TValue</code> is not an underlying type the generator supports.

### Read<TSelf, TValue\>\(XmlReader\) {#AdCodicem_ValueObjects_Metadata_ValueObjectXml_Read__2_System_Xml_XmlReader_}

Reads the element the reader is positioned on as a value object: its text, in the lexical form of the underlying
type, normalized and validated.

```csharp
public static TSelf Read<TSelf, TValue>(XmlReader reader) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`reader` [XmlReader](https://learn.microsoft.com/dotnet/api/system.xml.xmlreader)

The reader, on the start tag of the element; past its end tag on return.

#### Returns

 TSelf

The value object.

#### Type Parameters

`TSelf` 

The value object.

`TValue` 

Its underlying type.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">reader</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [XmlException](https://learn.microsoft.com/dotnet/api/system.xml.xmlexception)

The element is marked <code>xsi:nil</code>, its text is not of the underlying type, or the type refuses it.

 [NotSupportedException](https://learn.microsoft.com/dotnet/api/system.notsupportedexception)

<code class="typeparamref">TValue</code> is not an underlying type the generator supports.

### Write<TSelf, TValue\>\(XmlWriter, TSelf\) {#AdCodicem_ValueObjects_Metadata_ValueObjectXml_Write__2_System_Xml_XmlWriter___0_}

Writes a value object as the content of the current element, in the lexical form of its underlying type.

```csharp
public static void Write<TSelf, TValue>(XmlWriter writer, TSelf value) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`writer` [XmlWriter](https://learn.microsoft.com/dotnet/api/system.xml.xmlwriter)

The writer, inside the element.

`value` TSelf

The value object.

#### Type Parameters

`TSelf` 

The value object.

`TValue` 

Its underlying type.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">writer</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [XmlException](https://learn.microsoft.com/dotnet/api/system.xml.xmlexception)

The instance is the default, and its type refuses the default's value.

 [NotSupportedException](https://learn.microsoft.com/dotnet/api/system.notsupportedexception)

<code class="typeparamref">TValue</code> is not an underlying type the generator supports.

