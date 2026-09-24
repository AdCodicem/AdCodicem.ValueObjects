# Class IdCollations {#AdCodicem_ValueObjects_Identifiers_EntityFrameworkCore_IdCollations}

Namespace: [AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore](AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore.dll  

The binary collations worth naming, per provider.

```csharp
public static class IdCollations
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[IdCollations](AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore.IdCollations.md)

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
Applying one is a performance choice, not a correctness one — and that is only true because normalization
folds an identifier to a single canonical spelling before it is ever stored. Without that, a
case-insensitive collation would collapse two distinct identifiers into one, and a lookup could return the
wrong row. With it, the collation only decides how fast the comparison runs.
</p>
<p>
Binary comparison also matches what the application does in memory, where identifiers compare ordinally, so
a query and a sort in code agree on the order.
</p>

## Fields

### PostgreSql {#AdCodicem_ValueObjects_Identifiers_EntityFrameworkCore_IdCollations_PostgreSql}

Byte-wise comparison on PostgreSQL.

```csharp
public const string PostgreSql = "C"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Remarks

Also what makes a B-tree index usable by a <code>LIKE 'acc_%'</code> prefix scan.

### SqlServer {#AdCodicem_ValueObjects_Identifiers_EntityFrameworkCore_IdCollations_SqlServer}

Byte-wise comparison on SQL Server.

```csharp
public const string SqlServer = "Latin1_General_BIN2"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Remarks

Worth setting explicitly: the default collation of a SQL Server database is case insensitive.

