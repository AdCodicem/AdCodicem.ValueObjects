# Class ValueObjectKeyGeneratorFactory<TSelf, TValue\> {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectKeyGeneratorFactory_2}

Namespace: [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.EntityFrameworkCore.dll  

Creates the generator <code>HasValueObjectKeyGeneration</code> gives a key, by a type a compiled model can instantiate:
GUIDs a database keeps in order for a key over <xref href="System.Guid" data-throw-if-not-resolved="false"></xref>, and the temporary values Entity Framework Core
holds until the database generates a key over an integer.

```csharp
public sealed class ValueObjectKeyGeneratorFactory<TSelf, TValue> : ValueGeneratorFactory where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type: <xref href="System.Guid" data-throw-if-not-resolved="false"></xref>, <xref href="System.Int16" data-throw-if-not-resolved="false"></xref>, <xref href="System.Int32" data-throw-if-not-resolved="false"></xref> or <xref href="System.Int64" data-throw-if-not-resolved="false"></xref>, the types
<code>HasValueObjectKeyGeneration</code> is declared for.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueGeneratorFactory](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.valuegeneration.valuegeneratorfactory) ← 
[ValueObjectKeyGeneratorFactory<TSelf, TValue\>](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectKeyGeneratorFactory\-2.md)

#### Inherited Members

[ValueGeneratorFactory.Create\(IProperty, ITypeBase\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.valuegeneration.valuegeneratorfactory.create), 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
A GUID is SQL Server's sequential one on SQL Server (<xref href="Microsoft.EntityFrameworkCore.ValueGeneration.SequentialGuidValueGenerator" data-throw-if-not-resolved="false"></xref>), as Entity
Framework Core generates for a <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> key there, which SQL Server sorts in the order it was generated,
and a version 7 one on every other provider, as Npgsql generates for PostgreSQL, where Entity Framework Core would
generate random ones on SQLite and elsewhere. The value object is created from it through <code>Create</code>, so a rule
the GUID breaks fails the add with the value object's <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> rather than reach the
column.
</p>
<p>
An integer is a temporary value, negative, which the change tracker holds until <code>SaveChanges</code>, when the
database's identity takes its place. It is never written, so it is created unchecked, and a type counted from one
accepts it, a strict context included; the key the database hands back is read through the converter. Only a
relational database generates it: on another provider, Entity Framework Core's in-memory database for one, the add
fails with an <xref href="System.InvalidOperationException" data-throw-if-not-resolved="false"></xref> rather than store the temporary value as the key.
</p>
<p>
<code>HasValueObjectKeyGeneration</code> sets it on the property, and a compiled model writes it as
<code>new ValueObjectKeyGeneratorFactory&lt;TSelf, TValue&gt;().Create</code>.
</p>

## Methods

### Create\(IProperty, ITypeBase\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectKeyGeneratorFactory_2_Create_Microsoft_EntityFrameworkCore_Metadata_IProperty_Microsoft_EntityFrameworkCore_Metadata_ITypeBase_}

Creates a new value generator.

```csharp
public override ValueGenerator Create(IProperty property, ITypeBase typeBase)
```

#### Parameters

`property` [IProperty](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.iproperty)

The property to create the value generator for.

`typeBase` [ITypeBase](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.itypebase)

The type for which the value generator will be used.

#### Returns

 [ValueGenerator](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.valuegeneration.valuegenerator)

The newly created value generator.

#### Remarks

See <a href="https://aka.ms/efcore-docs-value-generation">EF Core value generation</a> for more information and examples.

