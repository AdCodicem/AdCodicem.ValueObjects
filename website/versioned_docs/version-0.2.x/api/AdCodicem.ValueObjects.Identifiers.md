# Namespace AdCodicem.ValueObjects.Identifiers {#AdCodicem_ValueObjects_Identifiers}

### Namespaces

 [AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore](AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore.md)

### Classes

 [AnyEntityIdJsonConverter](AdCodicem.ValueObjects.Identifiers.AnyEntityIdJsonConverter.md)

Moves an <xref href="AdCodicem.ValueObjects.Identifiers.AnyEntityId" data-throw-if-not-resolved="false"></xref> across a JSON boundary as the bare identifier text.

 [AnyEntityIdTypeConverter](AdCodicem.ValueObjects.Identifiers.AnyEntityIdTypeConverter.md)

Converts an <xref href="AdCodicem.ValueObjects.Identifiers.AnyEntityId" data-throw-if-not-resolved="false"></xref> to and from text for the boundaries that go through
<xref href="System.ComponentModel.TypeDescriptor" data-throw-if-not-resolved="false"></xref>: MVC model binding, configuration binding, and the designers.

 [CrockfordBase32](AdCodicem.ValueObjects.Identifiers.CrockfordBase32.md)

The Crockford Base32 alphabet, and the decoding that makes an entity identifier canonical.

 [EntityIdAttribute](AdCodicem.ValueObjects.Identifiers.EntityIdAttribute.md)

Marks a <code>readonly partial struct</code> as a public entity identifier and drives code generation for it.

 [EntityIdDescriptor](AdCodicem.ValueObjects.Identifiers.EntityIdDescriptor.md)

Runtime description of one entity identifier type, reachable from its prefix alone.

 [EntityIdFormat](AdCodicem.ValueObjects.Identifiers.EntityIdFormat.md)

The layout of an entity identifier: <code>prefix _ bucket random check</code>.

 [EntityIdPrefix](AdCodicem.ValueObjects.Identifiers.EntityIdPrefix.md)

The rules a declared prefix must satisfy.

 [EntityIdRegistry](AdCodicem.ValueObjects.Identifiers.EntityIdRegistry.md)

Process-wide index from a prefix to the identifier type that claims it.

 [IdCheckCharacter](AdCodicem.ValueObjects.Identifiers.IdCheckCharacter.md)

The trailing check character of an entity identifier.

 [IdEntropySource](AdCodicem.ValueObjects.Identifiers.IdEntropySource.md)

The source of the random part of a new entity identifier.

 [IdentifierErrorCodes](AdCodicem.ValueObjects.Identifiers.IdentifierErrorCodes.md)

Validation error codes specific to entity identifiers.

 [ValueObjectIds](AdCodicem.ValueObjects.Identifiers.ValueObjectIds.md)

The clock and the entropy source that <code>New()</code> reads.

### Structs

 [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

An identifier of any registered type, resolved from its prefix.

### Interfaces

 [IEntityId<TSelf\>](AdCodicem.ValueObjects.Identifiers.IEntityId\-1.md)

The full contract of a public entity identifier.

 [IEntityId](AdCodicem.ValueObjects.Identifiers.IEntityId.md)

Non-generic marker implemented by every entity identifier.

### Enums

 [IdGranularity](AdCodicem.ValueObjects.Identifiers.IdGranularity.md)

The width of the time bucket that heads the body of an entity identifier.

