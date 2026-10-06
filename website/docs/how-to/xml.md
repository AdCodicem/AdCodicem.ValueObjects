---
title: Serialize to XML
sidebar_label: XML
slug: /how-to/xml
description: Opt an assembly into XML serialization, so that XmlSerializer and DataContractSerializer write value objects as their underlying values, read them back through their rules, and describe them in the schemas they export.
---

# Serialize to XML

`XmlSerializer` and `DataContractSerializer` see a value object as a struct with no settable property. Left to
themselves, they write it as an empty element and read it back as a default instance, from an empty element and from
one holding a value alike: the value is lost on the way out, and the instance `VO0010` forbids in source comes back on
the way in. Nothing outside the type can change that, so the generator implements `IXmlSerializable` on the value
objects of an assembly that asks for it. The hosts built on the two serializers, the MVC XML formatters, CoreWCF and
Dapr actor remoting, are [expected to follow](#hosts-built-on-the-serializers), the MVC formatters probed and the other
two not run.

## Opting in

One assembly attribute, next to the value objects:

```csharp
using AdCodicem.ValueObjects;
using AdCodicem.ValueObjects.Annotations;

[assembly: ValueObjectXmlSerialization]

namespace Shipping;

[ValueObject<int>]
public readonly partial struct Quantity : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    public static int Minimum => 1;

    public static int Maximum => 100;
}
```

A project file sets the same attribute with an item, which the SDK writes into the assembly's attributes:

```xml
<ItemGroup>
  <AssemblyAttribute Include="AdCodicem.ValueObjects.Annotations.ValueObjectXmlSerializationAttribute" />
</ItemGroup>
```

Every value object and every entity identifier the generator writes in that assembly then implements
`IXmlSerializable` explicitly, and gains one public static member hidden from IntelliSense, `GetXmlSchema`, the schema
provider `XmlSerializer` insists on finding public. A value object that implements `IXmlSerializable`, or declares an
`[XmlSchemaProvider]`, itself keeps its own implementation. An assembly that does not opt in gets none of it. A domain
either crosses an XML boundary or it does not, so the choice is made once for the assembly.

## What is written

A value object is written as the content of its element, in the form `XmlSerializer` writes its underlying type in:
no wrapper, no attribute. A document written while the member was the primitive the value object replaces reads back,
and so does one written after.

| Underlying type | XSD type | Written as | Example |
| --- | --- | --- | --- |
| `string` | `xs:string` | As is | `FR7630006000011234567890189` |
| `Guid` | `xs:string` | Its `D` form | `0f8fad5b-d9cb-469f-a165-70867728950e` |
| `bool` | `xs:boolean` | `true` or `false`; `1` and `0` read too | `true` |
| `char` | `xs:unsignedShort` | Its code | `66` for `'B'` |
| `sbyte` to `ulong` | `xs:byte` to `xs:unsignedLong` | Its digits | `-3` |
| `Int128`, `UInt128` | `xs:integer` | Its digits | `170141183460469231731687303715884105727` |
| `decimal` | `xs:decimal` | Its digits | `12.50` |
| `double`, `float` | `xs:double`, `xs:float` | Its digits, `INF`, `-INF`, `NaN` | `-45.25` |
| `DateOnly` | `xs:date` | `yyyy-MM-dd` | `2026-10-03` |
| `TimeOnly` | `xs:time` | `HH:mm:ss.FFFFFFF` | `09:15:00.123` |
| `DateTime` | `xs:dateTime` | Its round-trip form, with its kind | `2026-10-03T09:00:00Z` |
| `DateTimeOffset` | `xs:dateTime` | Its round-trip form, with its offset | `2026-10-03T09:00:00+02:00` |
| `TimeSpan` | `xs:duration` | ISO 8601 | `PT1H30M` |

Three forms differ from what JSON carries or from what one of the serializers wrote before:

- A `TimeSpan` is an ISO 8601 duration, `PT1H30M`, where JSON writes `01:30:00`.
- A `DateTimeOffset` is one `xs:dateTime`, as `XmlSerializer` writes it. `DataContractSerializer` writes a bare
  `DateTimeOffset` as a pair of elements, `DateTime` and `OffsetMinutes`, which holds no value a value object reads: a
  document it wrote from the primitive is refused, as not parsable.
- A 128-bit integer is its digits. Both serializers write a bare `Int128` or `UInt128` as an empty element, which a value
  object refuses, as not parsable, rather than read back as zero.

A nullable member holding nothing is written `xsi:nil="true"` by `XmlSerializer` and `i:nil="true"` by
`DataContractSerializer`, and read back as `null`. A list holds one element per value object, named after it. A value
object written as the root of a document takes the name of its schema type: `<Quantity>4</Quantity>` from
`XmlSerializer`, `<Quantity xmlns="http://schemas.datacontract.org/2004/07/Shipping">4</Quantity>` from
`DataContractSerializer`; a construction of a generic value object is named `ReferenceOfPurchaseOrder`, and a nested
one `Depot.Bay`.

## Reading

Reading goes through `TryCreate`, as every boundary does: the text is read in the form of the underlying type, then
normalized and validated. There is no trusted mode, XML being external input.

A value the type refuses fails the read with an `XmlException` naming the type and the rule, never the text, and
carrying the code of the rule under `ValueObjectErrors.ErrorCodeKey`, with the line and the position of the element.
Each serializer wraps it in its own exception:

| Serializer | Exception | Around it |
| --- | --- | --- |
| `XmlSerializer` | `InvalidOperationException`, "There is an error in XML document (1, 29).", the line and the position of the document | The `XmlException` of the value object |
| `DataContractSerializer` | `SerializationException`, "There was an error deserializing the object of type …", followed by the message of the rule | The `XmlException` of the value object |

`ValueObjectErrors.TryGetCode` reads the code through either:

```csharp skip
try
{
    var shipment = (Shipment)serializer.Deserialize(reader)!;
}
catch (InvalidOperationException exception) when (ValueObjectErrors.TryGetCode(exception, out var code))
{
    // code is value_object.out_of_range for <Quantity>500</Quantity>.
}
```

What each shape of element reads as:

| Element | Read as |
| --- | --- |
| `<Quantity>7</Quantity>`, `<Quantity> 7 </Quantity>` | `7`: the white space `XmlSerializer` ignores around a number is ignored. |
| `<Quantity>abc</Quantity>` | Refused, `value_object.not_parsable`: the text is not of the underlying type. |
| `<Quantity />` | Refused, `value_object.not_parsable`. |
| `<Code />` over `string` | The empty string: `value_object.required`, unless the type declares `AllowEmpty`. |
| `<At><DateTime>…</DateTime></At>` | Refused, `value_object.not_parsable`: an element holding elements holds no value. |
| `<Quantity xsi:nil="true" />` on a member that is not nullable, through `XmlSerializer` | Refused, `value_object.required`, whatever the underlying type, a text the type may leave empty included: a null, as a JSON `null` is. `XmlSerializer` hands the element to the type, which reads `xsi:nil` as an `xs:boolean`, `true` or `1`. |
| `<Quantity i:nil="true" />` on a member that is not nullable, through `DataContractSerializer` | Refused by `DataContractSerializer` itself, before the type reads anything: a `SerializationException`, "ValueType '…' cannot be null.", without a code, as for `IsRequired`. |
| No `<Quantity>` element at all | Nothing reads it, so nothing refuses it: the member keeps the default instance its container left, as a missing primitive keeps its zero. `[DataMember(IsRequired = true)]` makes `DataContractSerializer` refuse it, in its own terms and without a code. |

## Writing

Writing refuses what reading would. An instance that never went through `Create`, such as a member its container
never set, holds the default value: when its type refuses that value, the write fails with an `XmlException`, "The
value to write is not a valid Quantity: …", carrying the code, inside `XmlSerializer`'s `InvalidOperationException`
("There was an error generating the XML document.") or `DataContractSerializer`'s `SerializationException`. A type
that accepts its zero writes it.

## Schemas

The schema provider describes each value object as an `xs:simpleType` restricting the XSD type of its underlying
value with the rules of its `Schema`, declared once:

| Rule | Facet |
| --- | --- |
| `MinLength`, `MaxLength` | `xs:minLength`, `xs:maxLength` |
| The pattern of `IValueObjectPatternValidator` | `xs:pattern`, when XSD can say what it means (below) |
| `IValueObjectMinimum<T>`, `IValueObjectMaximum<T>` | `xs:minInclusive`, `xs:maxInclusive`, in the form the value is written in: `65` for `'A'`, `PT0S` for `TimeSpan.Zero` |
| The known values of a closed set | `xs:enumeration` |
| The description, the type's `<summary>` | `xs:documentation`, on the restriction |
| A `Guid` | The pattern of its `D` form, as `DataContractSerializer` describes one |

The type is named after the value object and put in the namespace `DataContractSerializer` derives from its CLR
namespace, `http://schemas.datacontract.org/2004/07/Shipping`, what a URI cannot hold percent-encoded as
`DataContractSerializer` writes it, unless the attribute names another:
`[assembly: ValueObjectXmlSerialization(Namespace = "urn:shipping")]`. The description goes on the restriction rather
than on the type because `XsdDataContractExporter` replaces the type's annotation with one of its own.

The name leaves the CLR namespace out, which the default namespace carries. A namespace named on the attribute puts the
value objects of every CLR namespace of the assembly side by side, so two of them with the same name, `Billing.Code` and
`Freight.Code`, would share one schema type: the provider shares it when their rules and descriptions agree, and
refuses the second with an `InvalidOperationException` naming both when they differ, rather than describe it with the
rules of the first. Give such value objects distinct names, or leave the namespace unset. Constructions of one generic
value object over type arguments of the same name, `Tag<Billing.Code>` and `Tag<Freight.Code>`, share a name in any
namespace, and a type, a generated generic value object stating the same rules for every construction.

The two exporters treat the provider apart. `XsdDataContractExporter` includes the simple type in the schemas it
exports, which is what the WSDL CoreWCF builds from them, and the clients `svcutil` generates, read (expected, not
run). `XmlSchemaExporter` refers to it, `type="q1:Quantity"`, with an `xs:import` of its namespace, and leaves it out:
add each value object's schema to the set yourself, by calling its `GetXmlSchema`, when a tool needs the whole of it.

System.Xml reads a schema before `XmlSerializer` serializes anything, and refuses the whole type, and every document
holding it, for one facet it cannot read. A facet it cannot read is therefore left out: it holds an `xs:integer` in a
`decimal`, so a bound of a 128-bit integer beyond what a `decimal` holds is dropped, and an `UInt128` publishes its
lower bound, 0, alone. A schema written by hand whose rules contradict each other, a `MinLength` above its
`MaxLength`, is published without its rules. System.Xml's own validator refuses an `xs:integer` beyond a `decimal` in a
document too: a large `Int128` is valid to the value object and invalid to it.

### Patterns

An XSD pattern matches the whole value, as if anchored at both ends, reads `^` and `$` as characters, and knows
neither lookaround nor backreference. A .NET pattern is written in XSD only when the translation keeps its meaning:

- each alternative is anchored at both ends, by `^` or `\A` and by `$`, `\z` or `\Z`, and the anchors are dropped:
  `^[A-Z]{2}[0-9]{2}$` becomes `[A-Z]{2}[0-9]{2}`;
- what both dialects read alike is written as it is; `.` becomes `[^\n]`, `\d` becomes `\p{Nd}`, `\w` and `\s` become
  the classes .NET reads them as, a `$` becomes `[$]`, and a lazy quantifier becomes greedy, which changes which match
  is found, never whether one is.

A pattern outside that is left out of the schema rather than published with another meaning, the other rules staying:
one anchored at one end or neither, a word boundary, a lookaround, a backreference, an inline option, a named block
such as `\p{IsGreek}`, a `\W` or `\S` among the items of a class, a character no XML text can hold. A character outside
the Basic Multilingual Plane counts as two in .NET and as one in XSD, in a pattern as in `xs:maxLength`, and XSD has no
counterpart for `$` matching before a final line feed.

## Hosts built on the serializers

The MVC XML formatters, `AddXmlSerializerFormatters` and `AddXmlDataContractSerializerFormatters`, read and write an
opted-in value object like any member. A body holding a value the type refuses is answered 400 with MVC's own message,
"An error occurred while deserializing input data.", and no code (probed, not tested here): the
[problem details](aspnet-core.md#a-body-read-as-xml) carry the code of a JSON body, not yet of an XML one. CoreWCF and
Dapr actor remoting sit on `DataContractSerializer`, and should read and write a value object as it does (expected, not
run).

## Limits

- **`[XmlAttribute]` and `[XmlText]`.** `XmlSerializer` refuses a value-object member marked with either when the
  serializer is built: "XmlAttribute/XmlText cannot be used to encode types implementing IXmlSerializable". It refused
  it before the opt-in too, as a complex type, so opting in breaks nothing that worked.
- **Native AOT.** `XmlSerializer` and `DataContractSerializer` carry `[RequiresUnreferencedCode]` and
  `[RequiresDynamicCode]`, and the warnings are the application's. The generated code and `ValueObjectXml` reflect on
  nothing and add no warning of their own; they stay in a trimmed binary that never serializes XML, about 2 KB per
  value object. Probed natively, `XmlSerializer` works once the application roots its own types, while
  `DataContractSerializer` cannot read an `IXmlSerializable` struct without dynamic code:
  `NotImplementedException: ReflectionCreateXmlSerializable - value type`. It writes one.
- **`ReadXml` changes the instance it is called on.** A serializer reads into an instance of its own; code calling
  `ReadXml` through a constrained call on a variable changes that variable, the one way a readonly value object
  changes. It is meant for the serializers.
- **`AnyEntityId`** is written by hand in the identifiers package and implements none of it.

A value object written by hand implements `IXmlSerializable` through the same helper the generated code calls,
`ValueObjectXml`, in `AdCodicem.ValueObjects.Metadata`:

```csharp skip
[XmlSchemaProvider("GetXmlSchema")]
public readonly struct Weight : IValueObject<Weight, int>, IXmlSerializable
{
    // ... the members of IValueObject<Weight, int> ...

    XmlSchema? IXmlSerializable.GetSchema() => null;

    void IXmlSerializable.ReadXml(XmlReader reader) => Unsafe.AsRef(in this) = ValueObjectXml.Read<Weight, int>(reader);

    void IXmlSerializable.WriteXml(XmlWriter writer) => ValueObjectXml.Write<Weight, int>(writer, this);

    public static XmlQualifiedName GetXmlSchema(XmlSchemaSet schemas) => ValueObjectXml.ProvideSchema<Weight, int>(schemas, null);
}
```
