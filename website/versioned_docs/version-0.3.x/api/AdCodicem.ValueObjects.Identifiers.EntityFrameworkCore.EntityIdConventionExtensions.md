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
Calling both is fine, given the same strictness: whichever call runs last sets the converter of the identifiers,
so a context reading through <code>ConfigureValueObjects(strict: true)</code> passes <code>strict: true</code> here too, or its
identifiers are read without validation. Either converter refuses to write an identifier that never went through
<code>New</code> or <code>Create</code>: a required one throws a <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref>, and an optional one,
<code>TId?</code>, is stored as <code>NULL</code>.
</p>
<p>
Each converter and comparer is closed over its identifier through the type arguments the identifier's
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor" data-throw-if-not-resolved="false"></xref> hands back to a visitor
(<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.Accept%60%601(AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>), never with
<xref href="System.Type.MakeGenericType(System.Type%5b%5d)" data-throw-if-not-resolved="false"></xref>. An identifier registered by hand with <xref href="AdCodicem.ValueObjects.Identifiers.EntityIdRegistry" data-throw-if-not-resolved="false"></xref>
alone is described by reflection the first time the convention meets it, as
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.TryResolve(System.Type%2cAdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%40)" data-throw-if-not-resolved="false"></xref> describes a value object nothing registered, and stays registered with
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry" data-throw-if-not-resolved="false"></xref> from then on, as a generated identifier always is: <code>ConfigureValueObjects</code> maps
it in a model built afterwards. Entity Framework Core builds no model under native AOT, where it reads a compiled
model instead, so none of this runs there.
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

### ConfigureEntityIds\(ModelConfigurationBuilder, bool, params Assembly\[\]\) {#AdCodicem_ValueObjects_Identifiers_EntityFrameworkCore_EntityIdConventionExtensions_ConfigureEntityIds_Microsoft_EntityFrameworkCore_ModelConfigurationBuilder_System_Boolean_System_Reflection_Assembly___}

Maps every registered entity identifier to a fixed-width column, optionally validating what is read.

```csharp
public static ModelConfigurationBuilder ConfigureEntityIds(this ModelConfigurationBuilder builder, bool strict, params Assembly[] assemblies)
```

#### Parameters

`builder` [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

Model configuration builder, from <code>DbContext.ConfigureConventions</code>.

`strict` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

When <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a>, identifiers read from the database are normalized and validated again, as
<code>ConfigureValueObjects(strict: true)</code> does for the other value objects.

`assemblies` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\[\]

Assemblies declaring the identifiers.

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

### ConfigureEntityIds\(ModelConfigurationBuilder, string?, bool, params Assembly\[\]\) {#AdCodicem_ValueObjects_Identifiers_EntityFrameworkCore_EntityIdConventionExtensions_ConfigureEntityIds_Microsoft_EntityFrameworkCore_ModelConfigurationBuilder_System_String_System_Boolean_System_Reflection_Assembly___}

Maps every registered entity identifier to a fixed-width column with an explicit collation, optionally
validating what is read.

```csharp
public static ModelConfigurationBuilder ConfigureEntityIds(this ModelConfigurationBuilder builder, string? collation, bool strict, params Assembly[] assemblies)
```

#### Parameters

`builder` [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

Model configuration builder, from <code>DbContext.ConfigureConventions</code>.

`collation` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Collation for the identifier columns, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> to leave the database default in place.
<xref href="AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore.IdCollations" data-throw-if-not-resolved="false"></xref> names the binary one per provider.

`strict` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

When <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a>, identifiers read from the database are normalized and validated again, as
<code>ConfigureValueObjects(strict: true)</code> does for the other value objects. A value the identifier refuses
fails the query with a <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref>.

`assemblies` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\[\]

Assemblies declaring the identifiers.

#### Returns

 [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

The same builder, so calls can be chained.

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

### HasEntityIdConversion<TId\>\(PropertyBuilder<TId\>, bool\) {#AdCodicem_ValueObjects_Identifiers_EntityFrameworkCore_EntityIdConventionExtensions_HasEntityIdConversion__1_Microsoft_EntityFrameworkCore_Metadata_Builders_PropertyBuilder___0__System_Boolean_}

Maps a single property holding an entity identifier, validating what is read from the database or not.

```csharp
public static PropertyBuilder<TId> HasEntityIdConversion<TId>(this PropertyBuilder<TId> builder, bool strict) where TId : struct, IEntityId<TId>
```

#### Parameters

`builder` [PropertyBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.propertybuilder\-1)<TId\>

Property builder.

`strict` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

When <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a>, identifiers read from the database are validated again.

#### Returns

 [PropertyBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.propertybuilder\-1)<TId\>

The same builder, so calls can be chained.

#### Type Parameters

`TId` 

Identifier type.

#### Remarks

Use this for a property that needs to depart from the convention; otherwise prefer the convention.

### HasEntityIdConversion<TId\>\(PropertyBuilder<TId\>, string?, bool\) {#AdCodicem_ValueObjects_Identifiers_EntityFrameworkCore_EntityIdConventionExtensions_HasEntityIdConversion__1_Microsoft_EntityFrameworkCore_Metadata_Builders_PropertyBuilder___0__System_String_System_Boolean_}

Maps a single property holding an entity identifier, with a collation, validating what is read from the
database or not.

```csharp
public static PropertyBuilder<TId> HasEntityIdConversion<TId>(this PropertyBuilder<TId> builder, string? collation, bool strict) where TId : struct, IEntityId<TId>
```

#### Parameters

`builder` [PropertyBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.propertybuilder\-1)<TId\>

Property builder.

`collation` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Collation for the column, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for the database default.

`strict` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

When <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a>, identifiers read from the database are validated again.

#### Returns

 [PropertyBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.propertybuilder\-1)<TId\>

The same builder, so calls can be chained.

#### Type Parameters

`TId` 

Identifier type.

#### Remarks

Use this for a property that needs to depart from the convention; otherwise prefer the convention. Pass the same
<code class="paramref">strict</code> as to the convention, which a property configured here otherwise departs from.

