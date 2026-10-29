# Class ValueObjectDbContextOptionsBuilderExtensions {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectDbContextOptionsBuilderExtensions}

Namespace: [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.EntityFrameworkCore.dll  

Adds to the options of a context what the model cannot hold: the translation of what a LINQ query reads of a value
object, and the code of the rule a CHECK constraint refused a row under.

```csharp
public static class ValueObjectDbContextOptionsBuilderExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectDbContextOptionsBuilderExtensions](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectDbContextOptionsBuilderExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### UseValueObjectCheckErrors\(DbContextOptionsBuilder\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectDbContextOptionsBuilderExtensions_UseValueObjectCheckErrors_Microsoft_EntityFrameworkCore_DbContextOptionsBuilder_}

Adds the code of the rule a CHECK constraint of <code>AddValueObjectCheckConstraints</code> refused to the error the
database provider throws, under <xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>.

```csharp
public static DbContextOptionsBuilder UseValueObjectCheckErrors(this DbContextOptionsBuilder optionsBuilder)
```

#### Parameters

`optionsBuilder` [DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder)

Options builder of the context.

#### Returns

 [DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder)

The same builder, so calls can be chained.

#### Remarks

<p>
The application's own writes pass the constraints, since the value objects it writes went through their rules. A
constraint fires for an invalid instance a lax read let in and the application writes back, and for a statement
that skips the converter, an <code>ExecuteUpdate</code> setting a column from SQL or raw SQL. Once this is called, the
provider's error carries the code of the rule, which <xref href="AdCodicem.ValueObjects.ValueObjectErrors.TryGetCode(System.Exception%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref> reads through the
<code>DbUpdateException</code> <code>SaveChanges</code> wraps it in, and from the error <code>ExecuteUpdate</code>, raw SQL or a
migration throws as it is: <code>value_object.out_of_range</code> for <code>CK_Orders_Quantity_Minimum</code>. The error is
neither replaced nor wrapped, so the <code>catch</code> blocks that handle it keep working.
</p>
<p>
Entity Framework Core hands no interceptor an error a provider reports while the rows of a query are read. On SQL
Server, a raw SQL query that writes rows and reads them back, <code>SqlQuery</code> or <code>FromSql</code> over an
<code>INSERT</code> or an <code>UPDATE</code> with <code>OUTPUT</code>, reports a refusal there, so its error carries no code:
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckConstraints.TryGetCode(System.Exception%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref> reads it in a <code>catch</code>. PostgreSQL and SQLite report the
refusal of such a query, over <code>RETURNING</code>, as the command runs, and their error carries the code.
</p>
<p>
The code is read off the name of the constraint, which ends with its rule, as
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckConstraints" data-throw-if-not-resolved="false"></xref> describes, not off the model: a compiled model holds no check constraint,
so the code comes with one too. Only an error reporting a violated CHECK constraint is read, on SQL Server,
PostgreSQL and SQLite, in every language SQL Server writes its messages in; a migration's error naming a
constraint for another reason, "There is already an object named …", carries none. A migration adding a constraint
to a table that holds a row it refuses fails with the code, as a write does.
</p>
<p>
It runs when a command or <code>SaveChanges</code> fails, never when one succeeds; once a command interceptor is
registered, though, Entity Framework Core builds the event data of each command it sends, as for any interceptor.
It is an interceptor of the options, so it also runs on a service provider of the application's own, named by
<code>UseInternalServiceProvider</code>. An interceptor of the application's own registered before it sees the error
before the code is added, and Entity Framework Core may log the failure before then too. A second call adds
nothing. Without it, <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckConstraints.TryGetCode(System.Exception%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref> reads the code in a <code>catch</code>, from an
error whoever sent the command.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">optionsBuilder</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### UseValueObjectCheckErrors<TContext\>\(DbContextOptionsBuilder<TContext\>\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectDbContextOptionsBuilderExtensions_UseValueObjectCheckErrors__1_Microsoft_EntityFrameworkCore_DbContextOptionsBuilder___0__}

Adds the code of the rule a CHECK constraint of <code>AddValueObjectCheckConstraints</code> refused to the error the
database provider throws, under <xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>.

```csharp
public static DbContextOptionsBuilder<TContext> UseValueObjectCheckErrors<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder) where TContext : DbContext
```

#### Parameters

`optionsBuilder` [DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder\-1)<TContext\>

Options builder of the context.

#### Returns

 [DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder\-1)<TContext\>

The same builder, so calls can be chained and its options keep the type of the context.

#### Type Parameters

`TContext` 

Type of the context.

#### Remarks

The same as <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectDbContextOptionsBuilderExtensions.UseValueObjectCheckErrors(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder)" data-throw-if-not-resolved="false"></xref>, for a builder of the options of one
context type.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">optionsBuilder</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### UseValueObjectQueries\(DbContextOptionsBuilder\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectDbContextOptionsBuilderExtensions_UseValueObjectQueries_Microsoft_EntityFrameworkCore_DbContextOptionsBuilder_}

Translates <code>.Value</code> of a value object, in a LINQ query, to its column, typed as the underlying value.

```csharp
public static DbContextOptionsBuilder UseValueObjectQueries(this DbContextOptionsBuilder optionsBuilder)
```

#### Parameters

`optionsBuilder` [DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder)

Options builder of the context.

#### Returns

 [DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder)

The same builder, so calls can be chained.

#### Remarks

<p>
Entity Framework Core translates a comparison between value objects, <code>x.Iban == iban</code>, but nothing a query
reads of the value one holds: <code>x.Iban.Value.StartsWith("FR")</code> or <code>x.Quantity.Value &gt; 2</code> fail, as
"could not be translated". Once this is called, <code>.Value</code> reads the column itself, under the type mapping of
the underlying type for the column's own store type: <code>WHERE [o].[Iban] LIKE N'FR%'</code>, which the index on the
column serves, and a parameter compared with it is declared as the column is, <code>nvarchar(34)</code>. An optional
value object, a property inside a JSON column, an element of a primitive collection and a construction of a
generic value object are translated too.
</p>
<p>
Only a property this package maps is translated: through <code>ConfigureValueObjects</code>,
<code>HasValueObjectConversion</code>, <code>ConfigureEntityIds</code> or <code>HasEntityIdConversion</code>, whose converters
store the bare value, including where Entity Framework Core composes one with its own conversion of the
underlying type, a <xref href="System.UInt16" data-throw-if-not-resolved="false"></xref> to an <code>int</code> column, say. A property mapped through a converter of the
application's own, as a value object over <xref href="System.Int128" data-throw-if-not-resolved="false"></xref> must be, keeps Entity Framework Core's refusal: what
its column holds may not be the value. So does this package's converter composed with a conversion of the
application's own, except over a type the provider converts itself, a <xref href="System.UInt16" data-throw-if-not-resolved="false"></xref> or a
<xref href="System.Uri" data-throw-if-not-resolved="false"></xref>, where it cannot be told from Entity Framework Core's composition.
</p>
<p>
A column of a table is read as it stands. Anything else, the element of a collection, a column an optional
navigation joins in, a <code>COALESCE</code>, is converted to the underlying type by a <code>CAST</code> to its own store type,
which Entity Framework Core 11 leaves out of the SQL.
</p>
<p>
<code>.Value</code> reads what the column holds, in a projection too: a strict context,
<code>ConfigureValueObjects(strict: true)</code>, validates a value object it materializes, not a <code>.Value</code> a query
projects. Project the value object to have it validated.
</p>
<p>
Nothing but <code>.Value</code> is translated: <code>ToString()</code>, which follows the formatting hooks, and
<code>EF.Property</code> still fail, and so does <code>.Value</code> of an element of a collection passed as a parameter, which
has no type mapping yet when it is translated; a conversion operator stays the cast Entity Framework Core writes,
to the unbounded type.
</p>
<p>
The translation runs once per query shape, which Entity Framework Core caches: nothing more runs per execution.
A query precompiled by <code>dotnet ef dbcontext optimize --precompile-queries</code> carries the SQL it wrote, so the
options the design-time context is built with call this too, or the precompilation fails on that query. The
translator works on a relational provider; with any other, this does nothing.
</p>
<p>
Entity Framework Core adds the translator to the service provider it builds for the context, never to one the
application hands it through <code>UseInternalServiceProvider</code>: a context started on such a provider throws an
<xref href="System.InvalidOperationException" data-throw-if-not-resolved="false"></xref> as it starts, unless the services of the extensions of its options were
added to that provider, through <code>IDbContextOptionsExtension.ApplyServices</code>.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">optionsBuilder</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### UseValueObjectQueries<TContext\>\(DbContextOptionsBuilder<TContext\>\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectDbContextOptionsBuilderExtensions_UseValueObjectQueries__1_Microsoft_EntityFrameworkCore_DbContextOptionsBuilder___0__}

Translates <code>.Value</code> of a value object, in a LINQ query, to its column, typed as the underlying value.

```csharp
public static DbContextOptionsBuilder<TContext> UseValueObjectQueries<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder) where TContext : DbContext
```

#### Parameters

`optionsBuilder` [DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder\-1)<TContext\>

Options builder of the context.

#### Returns

 [DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder\-1)<TContext\>

The same builder, so calls can be chained and its options keep the type of the context.

#### Type Parameters

`TContext` 

Type of the context.

#### Remarks

The same as <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectDbContextOptionsBuilderExtensions.UseValueObjectQueries(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder)" data-throw-if-not-resolved="false"></xref>, for a builder of the options of one
context type.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">optionsBuilder</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

