# Class ValueObjectInt64KeyGenerationExtensions {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectInt64KeyGenerationExtensions}

Namespace: [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.EntityFrameworkCore.dll  

Generates a key over a 64-bit integer the way the provider generates a <xref href="System.Int64" data-throw-if-not-resolved="false"></xref> key.

```csharp
public static class ValueObjectInt64KeyGenerationExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectInt64KeyGenerationExtensions](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectInt64KeyGenerationExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### HasValueObjectKeyGeneration<TSelf\>\(PropertyBuilder<TSelf\>\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectInt64KeyGenerationExtensions_HasValueObjectKeyGeneration__1_Microsoft_EntityFrameworkCore_Metadata_Builders_PropertyBuilder___0__}

Has the database generate the key when an entity is added: an identity column on SQL Server and PostgreSQL, the
row identifier on SQLite, with a temporary value in the change tracker until the row is saved.

```csharp
public static PropertyBuilder<TSelf> HasValueObjectKeyGeneration<TSelf>(this PropertyBuilder<TSelf> builder) where TSelf : struct, IValueObject<TSelf, long>
```

#### Parameters

`builder` [PropertyBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.propertybuilder\-1)<TSelf\>

Property builder of the key.

#### Returns

 [PropertyBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.propertybuilder\-1)<TSelf\>

The same builder, so calls can be chained.

#### Type Parameters

`TSelf` 

Value object type, over <xref href="System.Int64" data-throw-if-not-resolved="false"></xref>.

#### Remarks

The column is a <code>bigint</code> identity on SQL Server and PostgreSQL. See
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectInt32KeyGenerationExtensions.HasValueObjectKeyGeneration%60%601(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>
for what generation changes.

