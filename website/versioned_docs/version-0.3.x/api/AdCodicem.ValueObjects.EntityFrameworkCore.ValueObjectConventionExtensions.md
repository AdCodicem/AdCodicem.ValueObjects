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

### AddValueObjectCheckConstraints\(ModelConfigurationBuilder, Action<ValueObjectCheckOptions\>?\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectConventionExtensions_AddValueObjectCheckConstraints_Microsoft_EntityFrameworkCore_ModelConfigurationBuilder_System_Action_AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectCheckOptions__}

Writes the rules value objects declare as CHECK constraints on the columns this package maps them to, so that the
database refuses a row another writer stores with a value the type rejects.

```csharp
public static ModelConfigurationBuilder AddValueObjectCheckConstraints(this ModelConfigurationBuilder builder, Action<ValueObjectCheckOptions>? configure = null)
```

#### Parameters

`builder` [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

Model configuration builder, from <code>DbContext.ConfigureConventions</code>.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action\-1)<[ValueObjectCheckOptions](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckOptions.md)\>?

Chooses the rules and the dialect of patterns; when it is not given, lengths, bounds and closed value sets are
written, and no pattern.

#### Returns

 [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

The same builder, so calls can be chained.

#### Examples

<pre><code class="lang-csharp">protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    =&gt; builder
        .ConfigureValueObjects(typeof(Iban).Assembly)
        .AddValueObjectCheckConstraints(checks =&gt; checks.Patterns = ValueObjectPatternDialect.PostgreSql);</code></pre>

#### Remarks

<p>
Off unless called. Each column one of this package's converters maps, through <code>ConfigureValueObjects</code>,
<code>ConfigureEntityIds</code> or one property at a time, gets a constraint per rule its value object declares:
<code>MinLength</code>, the bounds of <xref href="AdCodicem.ValueObjects.IValueObjectMinimum%601" data-throw-if-not-resolved="false"></xref> and
<xref href="AdCodicem.ValueObjects.IValueObjectMaximum%601" data-throw-if-not-resolved="false"></xref>, and the known values of a closed value set. A migration picks them up,
so a rule changed on the type reaches the next migration, as a column size does; a table already holding a row the
rule refuses fails that migration.
</p>
<p>
A constraint is laxer than the type where the database cannot say the rule as .NET reads it, never stricter: a
length is counted in bytes where the database cannot count UTF-16 code units, and a bound or a closed set is left
out where the column does not compare values as .NET does. A rule only code checks, in
<xref href="AdCodicem.ValueObjects.IValueObjectValidator%601" data-throw-if-not-resolved="false"></xref>, stays in the application. SQL Server, PostgreSQL and SQLite are known;
another provider gets no constraint. A column mapped to JSON, the elements of a primitive collection, and a
property mapped by a converter of the application's own get none either.
</p>
<p>
A pattern is written only when <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckOptions.Patterns" data-throw-if-not-resolved="false"></xref> names the dialect of the database,
which the model cannot tell: <code>REGEXP_LIKE</code> on SQL Server 2025, <code>~</code> on PostgreSQL, none on SQLite. Naming
one fails the model, with an <xref href="System.InvalidOperationException" data-throw-if-not-resolved="false"></xref>, on a pattern the dialect would read apart from
.NET, unless <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckOptions.ExcludePattern%60%601" data-throw-if-not-resolved="false"></xref> leaves that value object's pattern out.
</p>
<p>
The application's own writes pass, since the value objects it writes went through their rules. A constraint refuses
what another writer stores, and an invalid instance a lax read let in and the application writes back, with the
provider's error inside a <xref href="Microsoft.EntityFrameworkCore.DbUpdateException" data-throw-if-not-resolved="false"></xref> for a write through Entity Framework Core: it complements
<code>strict: true</code>, which refuses on read a row stored before the constraint existed. Each constraint's name ends
with its rule, which
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectDbContextOptionsBuilderExtensions.UseValueObjectCheckErrors(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder)" data-throw-if-not-resolved="false"></xref>, on
the options of the context, adds to that error as the code <code>Validate</code> reports for the rule, and
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckConstraints.TryGetCode(System.Exception%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref> reads without it.
</p>
<p>
The options are read once per context type, as <code>strict</code> is: Entity Framework Core builds the model the first
time a context type is used and caches it for every later instance. A second call replaces the first, whose
options then apply nowhere, so that a context deriving from another can call it again with options of its own. The
constraints are part of the model a migration, <code>EnsureCreated</code> or a script is made from, never of a compiled
model, which reads none: nothing here runs per query or per row.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">builder</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentOutOfRangeException](https://learn.microsoft.com/dotnet/api/system.argumentoutofrangeexception)

<code class="paramref">configure</code> names a dialect of patterns <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectPatternDialect" data-throw-if-not-resolved="false"></xref> does not define.

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
A value object declaring a maximum length also sizes its column, so an IBAN lands in <code>nvarchar(34)</code> on SQL
Server, or in <code>varchar(34)</code> once it declares <code>Unicode = false</code>, rather than an unbounded column — the
rule is stated once, on the type, and the schema follows.
</p>
<p>
A value object declaring <code>Unicode = false</code> is mapped to non-Unicode text, its optional form, each construction
of a generic one and each element of a collection included, so that SQL Server stores it in <code>varchar</code> and
declares a parameter compared with it <code>varchar</code> too. PostgreSQL and SQLite store text as they always do.
<code>Properties&lt;TSelf&gt;().AreUnicode(true)</code> after this call, or <code>IsUnicode(true)</code> on a property, keeps
Unicode text.
</p>
<p>
A value object over <xref href="System.Int128" data-throw-if-not-resolved="false"></xref> or <xref href="System.UInt128" data-throw-if-not-resolved="false"></xref> is left alone: Entity Framework Core maps
neither type, on any provider, so the column type, and the conversion to it, are the application's to choose
with a converter of its own.
</p>
<p>
A generic value object, <code>Code&lt;T&gt;</code> or <code>Outer&lt;T&gt;.Code</code>, is mapped too, whatever its
constructions: each property of one gets the converter, the comparer and the length closed over its own.
</p>
<p>
A property holding a collection of value objects, <code>List&lt;Iban&gt;</code>, <code>Quantity[]</code> or
<code>IReadOnlyList&lt;Iban?&gt;</code>, in an entity type, an owned type or a complex type, is mapped as a primitive
collection: each element gets the converter, the comparer and the length of its value object, so that an IBAN
lands in a <code>character varying(34)[]</code> on PostgreSQL, and in a JSON array elsewhere, which SQL Server reads as
<code>nvarchar(34)</code>, or <code>varchar(34)</code> for a value object declaring <code>Unicode = false</code>. Only a property Entity Framework Core would map itself, one with a public getter and a setter,
is made one, unless the application converts the whole collection itself; a primitive collection the application
declares, over a read-only property, gets its elements configured too, unless the application set their converter.
</p>
<p>
Writing refuses a value the value object rejects, which only an instance equal to <code>default(TSelf)</code> can hold,
rather than store it for a read to trust: a property of the value object's type throws a
<xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref>, and a property of its nullable type, <code>TSelf?</code>, stores a <code>NULL</code>. An
element of a collection does the same, but for an element of <code>TSelf?</code> over a value type, which throws too:
Entity Framework Core cannot write the <code>null</code> there.
</p>
<p>
A primary key of one property over a <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> value object, which is no foreign key and which nothing
generates, refuses an entity added without it, with an <xref href="System.InvalidOperationException" data-throw-if-not-resolved="false"></xref> carrying
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref>, whatever the value object's rules, rather than store it with
<xref href="System.Guid.Empty" data-throw-if-not-resolved="false"></xref>, a value a value object without a rule against it accepts. Have the key generated with
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectConventionExtensions.HasValueObjectKeyGeneration%60%601(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>, or written as it stands with
<code>ValueGeneratedNever()</code>; the refusal changes neither the column nor the migration snapshot (see
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.UnassignedValueObjectKeyGeneratorFactory" data-throw-if-not-resolved="false"></xref>).
</p>
<p>
The columns can also carry the rules as CHECK constraints, on request: see
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectConventionExtensions.AddValueObjectCheckConstraints(Microsoft.EntityFrameworkCore.ModelConfigurationBuilder%2cSystem.Action%7bAdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckOptions%7d)" data-throw-if-not-resolved="false"></xref>.
</p>
<p>
A compiled model, which <code>dotnet ef dbcontext optimize</code> generates, holds the same mapping. A property of
<code>TSelf?</code> is compared by <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.NullableValueObjectComparer%601" data-throw-if-not-resolved="false"></xref>, which such a model can write.
</p>
<p>
What it configures is decided once, while the model is built. Nothing here happens per query or per row, but for
the generator of a key, which Entity Framework Core creates once and asks for a value when an entity is added.
</p>
<p>
Each converter and comparer is closed over its value object through the type arguments the descriptor hands back
to a visitor (<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.Accept%60%601(AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>), never with
<xref href="System.Type.MakeGenericType(System.Type%5b%5d)" data-throw-if-not-resolved="false"></xref> over a <xref href="System.Type" data-throw-if-not-resolved="false"></xref> read off the descriptor. The converter of a
<code>TSelf?</code> property, or of such an element over text, which C# cannot name without a constraint the visitor
does not carry, is closed with it over those type arguments. Entity Framework Core builds no model under native
AOT, where it reads a compiled model instead, so none of this runs there.
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

When <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a>, values read from the database are normalized and validated again, the elements of a
collection included. Use it for tables another system also writes to; it costs a validation per materialized
value.

`assemblies` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\[\]

Assemblies declaring the value objects.

#### Returns

 [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

The same builder, so calls can be chained.

#### Remarks

<code class="paramref">strict</code> is read once per context type, not per instance: Entity Framework Core builds the
model the first time a context type is used and caches it for every later instance. A context that chooses the
value from a constructor argument gets whichever model was built first. Give strict reads a context type of
their own, or an <xref href="Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory" data-throw-if-not-resolved="false"></xref> that puts the
choice in the cache key.

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

Use this for a property that needs to depart from the convention; otherwise prefer the convention. It cannot
map a value object over <xref href="System.Int128" data-throw-if-not-resolved="false"></xref> or <xref href="System.UInt128" data-throw-if-not-resolved="false"></xref>, which Entity Framework Core maps to no
column: such a property needs a converter of the application's own. Writing refuses a value the value object
rejects with a <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref>, as the convention does. A primary key over a
<xref href="System.Guid" data-throw-if-not-resolved="false"></xref> value object is refused unset only under <code>ConfigureValueObjects</code>, whose convention does
it; <code>HasValueObjectKeyGeneration()</code> generates a key over a <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> or an integer either way.

### HasValueObjectKeyGeneration<TSelf\>\(PropertyBuilder<TSelf\>\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectConventionExtensions_HasValueObjectKeyGeneration__1_Microsoft_EntityFrameworkCore_Metadata_Builders_PropertyBuilder___0__}

Generates a key over a <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> when an entity is added, with GUIDs the database keeps in order: SQL
Server's sequential ones on SQL Server, as Entity Framework Core generates for a <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> key there,
and version 7 ones on every other provider, as Npgsql generates for PostgreSQL.

```csharp
public static PropertyBuilder<TSelf> HasValueObjectKeyGeneration<TSelf>(this PropertyBuilder<TSelf> builder) where TSelf : struct, IValueObject<TSelf, Guid>
```

#### Parameters

`builder` [PropertyBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.propertybuilder\-1)<TSelf\>

Property builder of the key.

#### Returns

 [PropertyBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.propertybuilder\-1)<TSelf\>

The same builder, so calls can be chained.

#### Type Parameters

`TSelf` 

Value object type, over <xref href="System.Guid" data-throw-if-not-resolved="false"></xref>.

#### Remarks

<p>
The key becomes generated on add, as a <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> key is by convention, and gets the generator of
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectKeyGeneratorFactory%602" data-throw-if-not-resolved="false"></xref>, which a compiled model can write.
<code>ValueGeneratedOnAdd()</code> alone would not do: Entity Framework Core would then wrap its random generator in a
conversion on every provider, losing the order SQL Server's clustered index and PostgreSQL's version 7 keep. On
SQLite and the other providers, where Entity Framework Core generates a random GUID for a <xref href="System.Guid" data-throw-if-not-resolved="false"></xref>
key, the key is a version 7 one all the same.
</p>
<p>
A key the domain set is kept: Entity Framework Core asks a generator only for a key still at its default,
<code>default(TSelf)</code>, which a value object holding <xref href="System.Guid.Empty" data-throw-if-not-resolved="false"></xref> equals, so that one is generated over.
The GUID goes through the value object's <code>Create</code>, so a rule it breaks fails the add with a
<xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref>. <code>Attach</code> or <code>Update</code> of an entity whose key is unset adds it, as for a
<xref href="System.Guid" data-throw-if-not-resolved="false"></xref> key. The key is generated in the application: a default the column declares is not used.
</p>
<p>
It works with or without <code>ConfigureValueObjects</code>, beside
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectConventionExtensions.HasValueObjectConversion%60%602(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder%7b%60%600%7d%2cSystem.Boolean)" data-throw-if-not-resolved="false"></xref>. A key over a
<xref href="System.Int16" data-throw-if-not-resolved="false"></xref>, an <xref href="System.Int32" data-throw-if-not-resolved="false"></xref> or a <xref href="System.Int64" data-throw-if-not-resolved="false"></xref> is generated by the database, through the method
of the same name of <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectInt16KeyGenerationExtensions" data-throw-if-not-resolved="false"></xref>,
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectInt32KeyGenerationExtensions" data-throw-if-not-resolved="false"></xref> and <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectInt64KeyGenerationExtensions" data-throw-if-not-resolved="false"></xref>,
which a call picks by the key's type; a value object over any other type is a compile error.
</p>

