# Class ValueObjectConventionExtensions {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectConventionExtensions}

Namespace: [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.EntityFrameworkCore.dll  

Maps value objects onto their underlying database types.

```csharp
public static class ValueObjectConventionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectConventionExtensions](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectConventionExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### ConfigureValueObjects\(ModelConfigurationBuilder, params Assembly\[\]\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectConventionExtensions_ConfigureValueObjects_Microsoft_EntityFrameworkCore_ModelConfigurationBuilder_System_Reflection_Assembly___}

Maps every value object declared in the given assemblies to its underlying column type.

```csharp
public static ModelConfigurationBuilder ConfigureValueObjects(this ModelConfigurationBuilder builder, params Assembly[] assemblies)
```

#### Parameters

`builder` [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

Model configuration builder, from <code>DbContext.ConfigureConventions</code>.

`assemblies` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\[\]

Assemblies declaring the value objects. When none is given, everything already registered is mapped,
which is enough as soon as the entity assembly has been loaded.

#### Returns

 [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

The same builder, so calls can be chained.

#### Remarks

<p>
A value object declaring a maximum length also sizes its column, so an IBAN lands in <code>varchar(34)</code>
rather than an unbounded column — the rule is stated once, on the type, and the schema follows.
</p>
<p>
This runs once, while the model is built. Nothing here happens per query or per row.
</p>

### ConfigureValueObjects\(ModelConfigurationBuilder, bool, params Assembly\[\]\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectConventionExtensions_ConfigureValueObjects_Microsoft_EntityFrameworkCore_ModelConfigurationBuilder_System_Boolean_System_Reflection_Assembly___}

Maps every value object declared in the given assemblies, optionally re-validating on read.

```csharp
public static ModelConfigurationBuilder ConfigureValueObjects(this ModelConfigurationBuilder builder, bool strict, params Assembly[] assemblies)
```

#### Parameters

`builder` [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

Model configuration builder, from <code>DbContext.ConfigureConventions</code>.

`strict` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

When <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a>, values read from the database are normalized and validated again. Use it for
tables another system also writes to; it costs a validation per materialized value.

`assemblies` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\[\]

Assemblies declaring the value objects.

#### Returns

 [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

The same builder, so calls can be chained.

### HasValueObjectConversion<TSelf, TValue\>\(PropertyBuilder<TSelf\>, bool\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectConventionExtensions_HasValueObjectConversion__2_Microsoft_EntityFrameworkCore_Metadata_Builders_PropertyBuilder___0__System_Boolean_}

Maps a single property to the underlying value of its value object.

```csharp
public static PropertyBuilder<TSelf> HasValueObjectConversion<TSelf, TValue>(this PropertyBuilder<TSelf> builder, bool strict = false) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`builder` [PropertyBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.propertybuilder\-1)<TSelf\>

Property builder.

`strict` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

When <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a>, values read from the database are validated again.

#### Returns

 [PropertyBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.propertybuilder\-1)<TSelf\>

The same builder, so calls can be chained.

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

#### Remarks

Use this for a property that needs to depart from the convention; otherwise prefer the convention.

