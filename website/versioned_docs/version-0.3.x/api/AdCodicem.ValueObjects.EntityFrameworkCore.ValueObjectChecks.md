# Enum ValueObjectChecks {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectChecks}

Namespace: [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.EntityFrameworkCore.dll  

The rules of a value object that <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectConventionExtensions.AddValueObjectCheckConstraints(Microsoft.EntityFrameworkCore.ModelConfigurationBuilder%2cSystem.Action%7bAdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckOptions%7d)" data-throw-if-not-resolved="false"></xref> writes as
CHECK constraints, combined as flags.

```csharp
[Flags]
public enum ValueObjectChecks
```

## Fields

`None = 0` 

No rule: only a pattern, when <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckOptions.Patterns" data-throw-if-not-resolved="false"></xref> names a dialect.



`Lengths = 1` 

The minimum length of text, <code>MinLength</code>, counted so that the database never counts fewer characters than .NET
does. The maximum length stays the size of the column.



`Bounds = 2` 

The bounds of <xref href="AdCodicem.ValueObjects.IValueObjectMinimum%601" data-throw-if-not-resolved="false"></xref> and <xref href="AdCodicem.ValueObjects.IValueObjectMaximum%601" data-throw-if-not-resolved="false"></xref>, where the
column orders values as .NET does.



`KnownValues = 4` 

The known values of a closed value set, where every value the type accepts is stored as one of them.



## Remarks

Each rule is written only where the database refuses no value the value object accepts: a constraint stricter than the
type would fail a write the application validated. A rule only code checks, the checksum of an
<xref href="AdCodicem.ValueObjects.IValueObjectValidator%601" data-throw-if-not-resolved="false"></xref>, has no SQL form and stays in the application.

