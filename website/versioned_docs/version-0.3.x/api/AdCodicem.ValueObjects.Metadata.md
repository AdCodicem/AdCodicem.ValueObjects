# Namespace AdCodicem.ValueObjects.Metadata {#AdCodicem_ValueObjects_Metadata}

### Classes

 [GenericValueObjectJsonConverterFactory](AdCodicem.ValueObjects.Metadata.GenericValueObjectJsonConverterFactory.md)

Supplies the System.Text.Json converter of each construction of a generic value object.

 [GenericValueObjectTypeConverter](AdCodicem.ValueObjects.Metadata.GenericValueObjectTypeConverter.md)

Converts each construction of a generic value object to and from text and its underlying value.

 [KnownValueInfo](AdCodicem.ValueObjects.Metadata.KnownValueInfo.md)

A value declared through <code>[KnownValue]</code>: the underlying value, the name of the static property the generator
creates for it, and the description the attribute gives it.

 [ValueObjectDescriptor](AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.md)

Runtime description of a value object type, used by the integrations that only know a <xref href="System.Type" data-throw-if-not-resolved="false"></xref>.

 [ValueObjectMessages](AdCodicem.ValueObjects.Metadata.ValueObjectMessages.md)

The keys and the arguments an application localises the message of a refused value object with: one key per rule
code, and the arguments the rules the type declares supply.

 [ValueObjectRegistry](AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.md)

Process-wide directory of the value object types known to the application.

 [ValueObjectSchema](AdCodicem.ValueObjects.Metadata.ValueObjectSchema.md)

The declarative constraints of a value object, captured once at compile time.

 [ValueObjectXml](AdCodicem.ValueObjects.Metadata.ValueObjectXml.md)

Reads, writes and describes a value object in XML, for the <code>IXmlSerializable</code> implementation the generator emits
on the value objects of an assembly marked <code>[assembly: ValueObjectXmlSerialization]</code>.

### Interfaces

 [IValueObjectVisitor<TResult\>](AdCodicem.ValueObjects.Metadata.IValueObjectVisitor\-1.md)

Receives the type arguments of a value object that a caller knows only by its descriptor, so that the caller closes
its own generic code over them at compile time.

### Delegates

 [BoxedTryCreate](AdCodicem.ValueObjects.Metadata.BoxedTryCreate.md)

Attempts to build a value object from a boxed underlying value.

 [BoxedTryParse](AdCodicem.ValueObjects.Metadata.BoxedTryParse.md)

Attempts to build a value object from its text representation.

