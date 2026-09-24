# Interface IEntityId<TSelf\> {#AdCodicem_ValueObjects_Identifiers_IEntityId_1}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

The full contract of a public entity identifier.

```csharp
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "New() is the name every caller reaches for, and the alternatives all stutter against the type name. The rule guards Visual Basic consumers, who would write New[](); that trade is worth making for the member domain code touches most.")]
public interface IEntityId<TSelf> : IEntityId, IValueObject<TSelf, string>, IValueObject<string>, IValueObject, IEquatable<TSelf>, IComparable<TSelf>, IComparable, ISpanParsable<TSelf>, IParsable<TSelf>, ISpanFormattable, IFormattable where TSelf : struct, IEntityId<TSelf>
```

#### Type Parameters

`TSelf` 

The identifier type itself.

#### Implements

[IEntityId](AdCodicem.ValueObjects.Identifiers.IEntityId.md), 
[IValueObject<TSelf, string\>](AdCodicem.ValueObjects.IValueObject\-2.md), 
[IValueObject<string\>](AdCodicem.ValueObjects.IValueObject\-1.md), 
[IValueObject](AdCodicem.ValueObjects.IValueObject.md), 
[IEquatable<TSelf\>](https://learn.microsoft.com/dotnet/api/system.iequatable\-1), 
[IComparable<TSelf\>](https://learn.microsoft.com/dotnet/api/system.icomparable\-1), 
[IComparable](https://learn.microsoft.com/dotnet/api/system.icomparable), 
[ISpanParsable<TSelf\>](https://learn.microsoft.com/dotnet/api/system.ispanparsable\-1), 
[IParsable<TSelf\>](https://learn.microsoft.com/dotnet/api/system.iparsable\-1), 
[ISpanFormattable](https://learn.microsoft.com/dotnet/api/system.ispanformattable), 
[IFormattable](https://learn.microsoft.com/dotnet/api/system.iformattable)

## Remarks

Implementations are produced by the source generator from <xref href="AdCodicem.ValueObjects.Identifiers.EntityIdAttribute" data-throw-if-not-resolved="false"></xref>. Everything the
contract adds over <xref href="AdCodicem.ValueObjects.IValueObject%602" data-throw-if-not-resolved="false"></xref> is static, so a caller holding only a type
parameter can mint identifiers without reflection and without a factory to inject.

## Properties

### Granularity {#AdCodicem_ValueObjects_Identifiers_IEntityId_1_Granularity}

Gets the width of the time bucket heading the body.

```csharp
public static abstract IdGranularity Granularity { get; }
```

#### Property Value

 [IdGranularity](AdCodicem.ValueObjects.Identifiers.IdGranularity.md)

### Length {#AdCodicem_ValueObjects_Identifiers_IEntityId_1_Length}

Gets the exact length of an identifier of this type, which is also the width of its database column.

```csharp
public static abstract int Length { get; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

### Prefix {#AdCodicem_ValueObjects_Identifiers_IEntityId_1_Prefix}

Gets the prefix identifiers of this type carry, without its trailing separator.

```csharp
public static abstract string Prefix { get; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### New\(\) {#AdCodicem_ValueObjects_Identifiers_IEntityId_1_New}

Mints a new identifier from the ambient clock and entropy source.

```csharp
public static abstract TSelf New()
```

#### Returns

 TSelf

A fresh identifier, canonical and valid by construction.

#### Remarks

Both sources resolve through <xref href="AdCodicem.ValueObjects.Identifiers.ValueObjectIds" data-throw-if-not-resolved="false"></xref>, so a test can substitute them for the current
execution flow without an injected factory reaching every aggregate that creates an entity.

### New\(TimeProvider, IdEntropySource\) {#AdCodicem_ValueObjects_Identifiers_IEntityId_1_New_System_TimeProvider_AdCodicem_ValueObjects_Identifiers_IdEntropySource_}

Mints a new identifier from explicit sources.

```csharp
public static abstract TSelf New(TimeProvider timeProvider, IdEntropySource entropy)
```

#### Parameters

`timeProvider` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

Clock supplying the time bucket.

`entropy` [IdEntropySource](AdCodicem.ValueObjects.Identifiers.IdEntropySource.md)

Source of the random part.

#### Returns

 TSelf

A fresh identifier, canonical and valid by construction.

