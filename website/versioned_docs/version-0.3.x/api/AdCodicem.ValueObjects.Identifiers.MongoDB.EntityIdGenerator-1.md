# Class EntityIdGenerator<TId\> {#AdCodicem_ValueObjects_Identifiers_MongoDB_EntityIdGenerator_1}

Namespace: [AdCodicem.ValueObjects.Identifiers.MongoDB](AdCodicem.ValueObjects.Identifiers.MongoDB.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.MongoDB.dll  

Mints an entity identifier for a document inserted without one, as MongoDB.Driver mints an <code>ObjectId</code>.

```csharp
public sealed class EntityIdGenerator<TId> : IIdGenerator where TId : struct, IEntityId<TId>
```

#### Type Parameters

`TId` 

The identifier type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[EntityIdGenerator<TId\>](AdCodicem.ValueObjects.Identifiers.MongoDB.EntityIdGenerator\-1.md)

#### Implements

IIdGenerator

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The driver asks the generator of a document's id member, before it serializes the document, whether the id is still
unassigned, and has it mint one if so. An unassigned identifier is the default instance, which holds no valid value:
without a generator, the serializer of <code>AdCodicem.ValueObjects.MongoDB</code> would refuse to write it, with
<code>value_object.required</code>.
</p>
<p>
<xref href="AdCodicem.ValueObjects.Identifiers.MongoDB.EntityIdBson" data-throw-if-not-resolved="false"></xref> registers one per identifier type, which the driver's class maps take up when they are
built; a class map built by hand sets it with <xref href="MongoDB.Bson.Serialization.BsonMemberMap.SetIdGenerator(MongoDB.Bson.Serialization.IIdGenerator)" data-throw-if-not-resolved="false"></xref>.
</p>

## Methods

### GenerateId\(object, object\) {#AdCodicem_ValueObjects_Identifiers_MongoDB_EntityIdGenerator_1_GenerateId_System_Object_System_Object_}

Mints a new identifier, through <xref href="AdCodicem.ValueObjects.Identifiers.IEntityId%601.New" data-throw-if-not-resolved="false"></xref>, under the clock and the entropy
<xref href="AdCodicem.ValueObjects.Identifiers.ValueObjectIds" data-throw-if-not-resolved="false"></xref> holds for the current flow.

```csharp
public object GenerateId(object container, object document)
```

#### Parameters

`container` [object](https://learn.microsoft.com/dotnet/api/system.object)

The collection the document is inserted into.

`document` [object](https://learn.microsoft.com/dotnet/api/system.object)

The document.

#### Returns

 [object](https://learn.microsoft.com/dotnet/api/system.object)

The new identifier, boxed.

### IsEmpty\(object?\) {#AdCodicem_ValueObjects_Identifiers_MongoDB_EntityIdGenerator_1_IsEmpty_System_Object_}

Tells an unassigned identifier: the default instance, or anything that is not an identifier of the type.

```csharp
public bool IsEmpty(object? id)
```

#### Parameters

`id` [object](https://learn.microsoft.com/dotnet/api/system.object)?

The value of the document's id member.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the document needs a new identifier.

