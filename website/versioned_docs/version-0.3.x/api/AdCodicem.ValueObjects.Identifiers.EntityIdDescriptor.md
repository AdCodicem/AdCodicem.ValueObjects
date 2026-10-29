# Class EntityIdDescriptor {#AdCodicem_ValueObjects_Identifiers_EntityIdDescriptor}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

Runtime description of one entity identifier type, reachable from its prefix alone.

```csharp
public sealed class EntityIdDescriptor
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[EntityIdDescriptor](AdCodicem.ValueObjects.Identifiers.EntityIdDescriptor.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

The delegate it carries operates on boxed values, which is acceptable for the same reason it is on
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor" data-throw-if-not-resolved="false"></xref>: this is the path taken by callers that only know a prefix at run time —
a webhook dispatcher, a deep link, an audit trail. Request paths that know their type go through the static
members of <xref href="AdCodicem.ValueObjects.Identifiers.IEntityId%601" data-throw-if-not-resolved="false"></xref> and stay fully typed.

## Properties

### Granularity {#AdCodicem_ValueObjects_Identifiers_EntityIdDescriptor_Granularity}

Gets the width of the time bucket heading the body.

```csharp
public IdGranularity Granularity { get; }
```

#### Property Value

 [IdGranularity](AdCodicem.ValueObjects.Identifiers.IdGranularity.md)

### Length {#AdCodicem_ValueObjects_Identifiers_EntityIdDescriptor_Length}

Gets the exact length of an identifier of this type, which is also the width of its database column.

```csharp
public int Length { get; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

### Prefix {#AdCodicem_ValueObjects_Identifiers_EntityIdDescriptor_Prefix}

Gets the prefix the type claims, without its trailing separator.

```csharp
public string Prefix { get; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### TryParse {#AdCodicem_ValueObjects_Identifiers_EntityIdDescriptor_TryParse}

Gets the non-throwing text parser, producing a boxed identifier.

```csharp
public BoxedTryParse TryParse { get; }
```

#### Property Value

 [BoxedTryParse](AdCodicem.ValueObjects.Metadata.BoxedTryParse.md)

### ValueObjectType {#AdCodicem_ValueObjects_Identifiers_EntityIdDescriptor_ValueObjectType}

Gets the identifier type.

```csharp
public Type ValueObjectType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)

## Methods

### Accept<TResult\>\(IEntityIdVisitor<TResult\>\) {#AdCodicem_ValueObjects_Identifiers_EntityIdDescriptor_Accept__1_AdCodicem_ValueObjects_Identifiers_IEntityIdVisitor___0__}

Hands the identifier type to a visitor, closed at compile time where the descriptor was built.

```csharp
public TResult Accept<TResult>(IEntityIdVisitor<TResult> visitor)
```

#### Parameters

`visitor` [IEntityIdVisitor](AdCodicem.ValueObjects.Identifiers.IEntityIdVisitor\-1.md)<TResult\>

The visitor.

#### Returns

 TResult

What the visitor built for the identifier type.

#### Type Parameters

`TResult` 

What the visitor builds.

#### Remarks

Every descriptor is built by <xref href="AdCodicem.ValueObjects.Identifiers.EntityIdDescriptor.For%60%601" data-throw-if-not-resolved="false"></xref>, the generated registration's included, so every descriptor
<xref href="AdCodicem.ValueObjects.Identifiers.EntityIdRegistry" data-throw-if-not-resolved="false"></xref> holds can be visited, without reflection and under native AOT.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">visitor</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### For<TSelf\>\(\) {#AdCodicem_ValueObjects_Identifiers_EntityIdDescriptor_For__1}

Builds a descriptor, resolving every operation statically.

```csharp
public static EntityIdDescriptor For<TSelf>() where TSelf : struct, IEntityId<TSelf>
```

#### Returns

 [EntityIdDescriptor](AdCodicem.ValueObjects.Identifiers.EntityIdDescriptor.md)

The descriptor.

#### Type Parameters

`TSelf` 

Identifier type.

