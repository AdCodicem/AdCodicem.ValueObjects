# Namespace AdCodicem.ValueObjects.Annotations {#AdCodicem_ValueObjects_Annotations}

### Classes

 [KnownValueAttribute](AdCodicem.ValueObjects.Annotations.KnownValueAttribute.md)

Marks a static member of a value object as one of its known values.

 [NotDefaultAttribute](AdCodicem.ValueObjects.Annotations.NotDefaultAttribute.md)

Requires a value-object member not to hold the instance equal to <code>default(TSelf)</code>, which
<xref href="System.ComponentModel.DataAnnotations.RequiredAttribute" data-throw-if-not-resolved="false"></xref> lets through, since a struct is never <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ParsesAsAttribute<TSelf, TValue\>](AdCodicem.ValueObjects.Annotations.ParsesAsAttribute\-2.md)

Requires text to be accepted by a value object named at compile time, through its static members, with no registry.

 [ParsesAsAttribute](AdCodicem.ValueObjects.Annotations.ParsesAsAttribute.md)

Requires text to be accepted by a value object, and reports the message and the code of the rule that refuses it.

 [ValueObjectAttribute<TValue\>](AdCodicem.ValueObjects.Annotations.ValueObjectAttribute\-1.md)

Marks a <code>readonly partial struct</code> as a single-value value object and drives code generation for it.

 [ValueObjectValidationResult](AdCodicem.ValueObjects.Annotations.ValueObjectValidationResult.md)

The result DataAnnotations reports a refusal of <xref href="AdCodicem.ValueObjects.Annotations.ParsesAsAttribute" data-throw-if-not-resolved="false"></xref> or <xref href="AdCodicem.ValueObjects.Annotations.NotDefaultAttribute" data-throw-if-not-resolved="false"></xref>
with, carrying the code of the rule and the value object type beside the message.

 [ValueObjectXmlSerializationAttribute](AdCodicem.ValueObjects.Annotations.ValueObjectXmlSerializationAttribute.md)

Opts every value object of the assembly into XML serialization: the generator implements
<xref href="System.Xml.Serialization.IXmlSerializable" data-throw-if-not-resolved="false"></xref> and a schema provider on each of them.

### Enums

 [ValueSetKind](AdCodicem.ValueObjects.Annotations.ValueSetKind.md)

Whether a value object accepts arbitrary valid values or only an enumerated set.

