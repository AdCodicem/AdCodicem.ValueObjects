# Class ValueObjectCheckConstraints {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectCheckConstraints}

Namespace: [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.EntityFrameworkCore.dll  

Reads the code of the rule a CHECK constraint written by <code>AddValueObjectCheckConstraints</code> refused a row under,
from the error the database provider throws.

```csharp
public static class ValueObjectCheckConstraints
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectCheckConstraints](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckConstraints.md)

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
Each constraint the convention writes is named after its rule, <code>CK_{table}_{column}_{rule}</code>, or
<code>CK_{table}_{column}_{rule}_{hash}</code> when the name was shortened to the provider's limit or kept apart from
another, and the rule maps to the code <code>Validate</code> reports for it: <code>MinLength</code> to
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.TooShort" data-throw-if-not-resolved="false"></xref>, <code>Minimum</code> and <code>Maximum</code> to
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.OutOfRange" data-throw-if-not-resolved="false"></xref>, <code>KnownValues</code> to
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotAKnownValue" data-throw-if-not-resolved="false"></xref> and <code>Pattern</code> to
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.InvalidFormat" data-throw-if-not-resolved="false"></xref>. The name is read off the error, never off the model: a compiled
model holds no check constraint.
</p>
<p>
Only an error reporting a violated CHECK constraint is read: PostgreSQL's SQL state <code>23514</code>, whose constraint
Npgsql names in <xref href="System.Exception.Data" data-throw-if-not-resolved="false"></xref>; SQL Server's error 547, whose number SqlClient states in
<xref href="System.Exception.Data" data-throw-if-not-resolved="false"></xref> in every language, the name read off its message, quoted or not; SQLite's
<code>CHECK constraint failed</code>. An error that names a constraint for another reason, such as the "There is already an
object named …" of a migration, carries no code. A constraint declared by hand under a name of the same shape is read
as the convention's, since its name says the same rule; a name holding a space or a quotation mark is read on
PostgreSQL only, which names it apart from its message.
</p>
<p>
The code is the only thing carried: the column is in the constraint's name unless it was shortened, in SQL Server's
message, and, for <code>SaveChanges</code>, the entity is in <code>DbUpdateException.Entries</code>; neither the value object's
type, which no name holds, nor the value is.
</p>

## Methods

### TryGetCode\(Exception, out string?\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectCheckConstraints_TryGetCode_System_Exception_System_String__}

Reads the code of the rule a CHECK constraint of <code>AddValueObjectCheckConstraints</code> refused, from the
provider's error or from one an exception wraps.

```csharp
public static bool TryGetCode(Exception exception, out string? code)
```

#### Parameters

`exception` [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

The exception, as it was caught: the provider's, or the <code>DbUpdateException</code> around it.

`code` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The code, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> when no exception of the chain reports such a refusal.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when a code was found.

#### Remarks

<p>
Each exception is read in turn, from <code class="paramref">exception</code> down its <xref href="System.Exception.InnerException" data-throw-if-not-resolved="false"></xref>
chain, since Entity Framework Core fails <code>SaveChanges</code> with a <code>DbUpdateException</code> around the provider's
error; the first <xref href="System.Data.Common.DbException" data-throw-if-not-resolved="false"></xref> reporting a violated constraint of the convention gives the code.
</p>
<p>
It reads the error whoever sent the command: Entity Framework Core without
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectDbContextOptionsBuilderExtensions.UseValueObjectCheckErrors(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder)" data-throw-if-not-resolved="false"></xref>,
Dapper, a bulk library or ADO.NET. With it, <xref href="AdCodicem.ValueObjects.ValueObjectErrors.TryGetCode(System.Exception%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref> finds the same code, which
the interceptor stores in the provider's error, but on an error Entity Framework Core hands no interceptor: on SQL
Server, that of a raw SQL query writing rows it reads back, <code>SqlQuery</code> or <code>FromSql</code> over <code>OUTPUT</code>,
which SqlClient reports while the rows are read. This reads the code there too.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">exception</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

