# Class ValueObjectSchema {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema}

Namespace: [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

The declarative constraints of a value object, captured once at compile time.

```csharp
public sealed record ValueObjectSchema : IEquatable<ValueObjectSchema>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectSchema](AdCodicem.ValueObjects.Metadata.ValueObjectSchema.md)

#### Implements

[IEquatable<ValueObjectSchema\>](https://learn.microsoft.com/dotnet/api/system.iequatable\-1)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

The generator fills this from the <code>ValueObjectAttribute</code> so that the rules enforced by
<code>Validate</code> and the rules published in the OpenAPI schema or applied to an EF Core column can never
drift apart: they all read the same instance.

## Properties

### Description {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Description}

Gets the human-readable description of the value object, if any.

```csharp
public string? Description { get; init; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### Example {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Example}

Gets an example value, if any.

```csharp
public string? Example { get; init; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### Format {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Format}

Gets the OpenAPI <code>format</code> keyword for the value, if any.

```csharp
public string? Format { get; init; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### IsClosedValueSet {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_IsClosedValueSet}

Gets a value indicating whether only <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectSchema.KnownValues" data-throw-if-not-resolved="false"></xref> are accepted.

```csharp
public bool IsClosedValueSet { get; init; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### KnownValues {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_KnownValues}

Gets the declared known underlying values, boxed, in declaration order.

```csharp
public ImmutableArray<object> KnownValues { get; init; }
```

#### Property Value

 [ImmutableArray](https://learn.microsoft.com/dotnet/api/system.collections.immutable.immutablearray\-1)<[object](https://learn.microsoft.com/dotnet/api/system.object)\>

#### Remarks

Surfaced as the <code>enum</code> keyword of the OpenAPI schema.

### MaxLength {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_MaxLength}

Gets the maximum accepted length, if any.

```csharp
public int? MaxLength { get; init; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)?

#### Remarks

Used by the EF Core integration to size the mapped column.

### Maximum {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Maximum}

Gets the inclusive upper bound in invariant culture, if any.

```csharp
public string? Maximum { get; init; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### MinLength {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_MinLength}

Gets the minimum accepted length, if any.

```csharp
public int? MinLength { get; init; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)?

### Minimum {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Minimum}

Gets the inclusive lower bound in invariant culture, if any.

```csharp
public string? Minimum { get; init; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### Pattern {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Pattern}

Gets the regular expression the value must match, if any.

```csharp
public string? Pattern { get; init; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### Unconstrained {#AdCodicem_ValueObjects_Metadata_ValueObjectSchema_Unconstrained}

Gets the schema of a value object that declares no constraint.

```csharp
public static ValueObjectSchema Unconstrained { get; }
```

#### Property Value

 [ValueObjectSchema](AdCodicem.ValueObjects.Metadata.ValueObjectSchema.md)

