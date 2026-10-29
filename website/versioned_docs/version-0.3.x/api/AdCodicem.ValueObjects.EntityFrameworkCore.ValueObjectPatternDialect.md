# Enum ValueObjectPatternDialect {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectPatternDialect}

Namespace: [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.EntityFrameworkCore.dll  

The regular expressions of the database a pattern is written for as a CHECK constraint, which the model cannot tell.

```csharp
public enum ValueObjectPatternDialect
```

## Fields

`None = 0` 

No pattern is written.



`SqlServer2025 = 1` 

SQL Server 2025, which checks a pattern with <code>REGEXP_LIKE</code> in a database at compatibility level 170: an
earlier server, or a database at an earlier level, refuses the constraint.



`PostgreSql = 2` 

PostgreSQL, which checks a pattern with its <code>~</code> operator, under the <code>C</code> collation.



