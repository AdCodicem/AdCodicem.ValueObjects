# Class EntityIdConventionExtensions {#AdCodicem_ValueObjects_Identifiers_EntityFrameworkCore_EntityIdConventionExtensions}

Namespace: [AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore](AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore.dll  

Maps entity identifiers onto the narrowest column that can hold them.

```csharp
public static class EntityIdConventionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[EntityIdConventionExtensions](AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore.EntityIdConventionExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
An identifier is a value object, so <code>ConfigureValueObjects</code> already maps it. What this adds is
everything that follows from the identifier being fixed-width and ASCII: <code>char(n)</code> rather than
<code>varchar(n)</code>, non-Unicode so a SQL Server column is not silently doubled to <code>nchar</code>, and
optionally a binary collation.
</p>
<p>
It also applies the conversion itself, so a model holding nothing but identifiers needs this call alone.
Calling both is fine: the second call configures the same properties the same way.
</p>
<p>
What it deliberately does not do is decide the physical layout of your tables. On SQL Server a primary key
is clustered by default, which makes the table itself order by the key; declaring it
<code>IsClustered(false)</code> confines index churn to the ~30-byte index instead of the whole row, and with the
monotonic time bucket at the head of the body the fill factor can go back up towards 95. Those are choices
about your schema, not about the identifier type, and they need the provider-specific packages.
</p>

## Methods

### ConfigureEntityIds\(ModelConfigurationBuilder, params Assembly\[\]\) {#AdCodicem_ValueObjects_Identifiers_EntityFrameworkCore_EntityIdConventionExtensions_ConfigureEntityIds_Microsoft_EntityFrameworkCore_ModelConfigurationBuilder_System_Reflection_Assembly___}

Maps every registered entity identifier to a fixed-width column.

```csharp
public static ModelConfigurationBuilder ConfigureEntityIds(this ModelConfigurationBuilder builder, params Assembly[] assemblies)
```

#### Parameters

`builder` [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

Model configuration builder, from <code>DbContext.ConfigureConventions</code>.

`assemblies` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\[\]

Assemblies declaring the identifiers. When none is given, everything already registered is mapped, which
is enough as soon as the entity assembly has been loaded.

#### Returns

 [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

The same builder, so calls can be chained.

### ConfigureEntityIds\(ModelConfigurationBuilder, string?, params Assembly\[\]\) {#AdCodicem_ValueObjects_Identifiers_EntityFrameworkCore_EntityIdConventionExtensions_ConfigureEntityIds_Microsoft_EntityFrameworkCore_ModelConfigurationBuilder_System_String_System_Reflection_Assembly___}

Maps every registered entity identifier to a fixed-width column with an explicit collation.

```csharp
public static ModelConfigurationBuilder ConfigureEntityIds(this ModelConfigurationBuilder builder, string? collation, params Assembly[] assemblies)
```

#### Parameters

`builder` [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

Model configuration builder, from <code>DbContext.ConfigureConventions</code>.

`collation` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Collation for the identifier columns, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> to leave the database default in place.
<xref href="AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore.IdCollations" data-throw-if-not-resolved="false"></xref> names the binary one per provider.

`assemblies` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\[\]

Assemblies declaring the identifiers.

#### Returns

 [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

The same builder, so calls can be chained.

#### Examples

<pre><code class="lang-csharp">protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    =&gt; builder.ConfigureEntityIds(IdCollations.PostgreSql, typeof(AccountId).Assembly);</code></pre>

### HasEntityIdConversion<TId\>\(PropertyBuilder<TId\>, string?\) {#AdCodicem_ValueObjects_Identifiers_EntityFrameworkCore_EntityIdConventionExtensions_HasEntityIdConversion__1_Microsoft_EntityFrameworkCore_Metadata_Builders_PropertyBuilder___0__System_String_}

Maps a single property holding an entity identifier.

```csharp
public static PropertyBuilder<TId> HasEntityIdConversion<TId>(this PropertyBuilder<TId> builder, string? collation = null) where TId : struct, IEntityId<TId>
```

#### Parameters

`builder` [PropertyBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.propertybuilder\-1)<TId\>

Property builder.

`collation` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Collation for the column, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for the database default.

#### Returns

 [PropertyBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.propertybuilder\-1)<TId\>

The same builder, so calls can be chained.

#### Type Parameters

`TId` 

Identifier type.

#### Remarks

Use this for a property that needs to depart from the convention; otherwise prefer the convention.

