# Class ValueObjectCheckOptions {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectCheckOptions}

Namespace: [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.EntityFrameworkCore.dll  

Chooses the rules <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectConventionExtensions.AddValueObjectCheckConstraints(Microsoft.EntityFrameworkCore.ModelConfigurationBuilder%2cSystem.Action%7bAdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckOptions%7d)" data-throw-if-not-resolved="false"></xref> writes as CHECK
constraints, and the dialect it writes a pattern in.

```csharp
public sealed class ValueObjectCheckOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectCheckOptions](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckOptions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Properties

### Patterns {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectCheckOptions_Patterns}

Gets or sets the dialect of regular expressions the database runs, which a pattern is written in: none by default,
so that no pattern is written.

```csharp
public ValueObjectPatternDialect Patterns { get; set; }
```

#### Property Value

 [ValueObjectPatternDialect](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectPatternDialect.md)

#### Remarks

<p>
The model cannot tell which dialect the database runs, the compatibility level of a SQL Server database least of
all, so it is stated. A dialect that does not match the provider at hand, PostgreSQL's under SQLite in tests, writes
no pattern; SQLite has no regular expressions.
</p>
<p>
Naming one checks the pattern of each column the convention constrains when the model is built, under SQLite, or
the provider of the other dialect, as under the database it names: a pattern holding a construct the dialect reads
apart from .NET, or whose regular expression sets any option but <code>CultureInvariant</code>, <code>Compiled</code>,
<code>ExplicitCapture</code> and <code>NonBacktracking</code>, which its text does not carry, <code>IgnoreCase</code> say, or
<code>ECMAScript</code> and <code>RightToLeft</code>, which VO0025 does not report, fails the model with an
<xref href="System.InvalidOperationException" data-throw-if-not-resolved="false"></xref> naming the value object, the construct or the option, and the dialect,
unless <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckOptions.ExcludePattern%60%601" data-throw-if-not-resolved="false"></xref> leaves that value object out. A pattern a value object written by hand
states in its schema alone is checked from its text alone. Another provider than SQL Server, PostgreSQL and SQLite
gets no constraint, and no check.
</p>

### Rules {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectCheckOptions_Rules}

Gets or sets the rules written as CHECK constraints: lengths, bounds and closed value sets, unless one is removed.

```csharp
public ValueObjectChecks Rules { get; set; }
```

#### Property Value

 [ValueObjectChecks](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectChecks.md)

## Methods

### ExcludePattern<TSelf\>\(\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectCheckOptions_ExcludePattern__1}

Leaves the pattern of a value object out of the constraints, and out of the check of
<xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckOptions.Patterns" data-throw-if-not-resolved="false"></xref>, its other rules being written as before.

```csharp
public ValueObjectCheckOptions ExcludePattern<TSelf>() where TSelf : struct, IValueObject<TSelf, string>
```

#### Returns

 [ValueObjectCheckOptions](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectCheckOptions.md)

The same options, so calls can be chained.

#### Type Parameters

`TSelf` 

The value object, over <xref href="System.String" data-throw-if-not-resolved="false"></xref>; a construction of a generic one is left out alone.

#### Remarks

For a value object whose pattern the dialect would read apart from .NET, an email address's <code>[^@\s]</code> say, which
would otherwise fail the model: the database then checks its length, as before, and the application its pattern.

