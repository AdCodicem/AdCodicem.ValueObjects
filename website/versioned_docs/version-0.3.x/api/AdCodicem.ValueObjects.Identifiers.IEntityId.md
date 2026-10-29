# Interface IEntityId {#AdCodicem_ValueObjects_Identifiers_IEntityId}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

Non-generic marker implemented by every entity identifier.

```csharp
public interface IEntityId : IValueObject<string>, IValueObject
```

#### Implements

[IValueObject<string\>](AdCodicem.ValueObjects.IValueObject\-1.md), 
[IValueObject](AdCodicem.ValueObjects.IValueObject.md)

## Remarks

Reflection-driven integrations test for this interface for the same reason they test for
<xref href="AdCodicem.ValueObjects.IValueObject" data-throw-if-not-resolved="false"></xref>: one <code>IsAssignableTo</code> is far cheaper than walking a generic interface list.
It also draws the line that keeps <xref href="AdCodicem.ValueObjects.Identifiers.AnyEntityId" data-throw-if-not-resolved="false"></xref> out of persistence, since the polymorphic type
deliberately implements neither this nor <xref href="AdCodicem.ValueObjects.IValueObject" data-throw-if-not-resolved="false"></xref>.

## Properties

### GranularityValue {#AdCodicem_ValueObjects_Identifiers_IEntityId_GranularityValue}

Gets the width of the time bucket heading the body.

```csharp
IdGranularity GranularityValue { get; }
```

#### Property Value

 [IdGranularity](AdCodicem.ValueObjects.Identifiers.IdGranularity.md)

#### Remarks

Reserved for reflection-driven code paths; prefer the static <code>Granularity</code> property.

### PrefixValue {#AdCodicem_ValueObjects_Identifiers_IEntityId_PrefixValue}

Gets the prefix this identifier carries, without its trailing separator.

```csharp
string PrefixValue { get; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Remarks

Reserved for reflection-driven code paths; prefer the static <code>Prefix</code> property.

