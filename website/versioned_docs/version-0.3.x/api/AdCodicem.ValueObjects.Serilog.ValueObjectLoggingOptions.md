# Class ValueObjectLoggingOptions {#AdCodicem_ValueObjects_Serilog_ValueObjectLoggingOptions}

Namespace: [AdCodicem.ValueObjects.Serilog](AdCodicem.ValueObjects.Serilog.md)  
Assembly: AdCodicem.ValueObjects.Serilog.dll  

Options of <xref href="AdCodicem.ValueObjects.Serilog.ValueObjectLoggerConfigurationExtensions.ValueObjects(Serilog.Configuration.LoggerDestructuringConfiguration%2cSystem.Action%7bAdCodicem.ValueObjects.Serilog.ValueObjectLoggingOptions%7d)" data-throw-if-not-resolved="false"></xref>.

```csharp
public sealed class ValueObjectLoggingOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectLoggingOptions](AdCodicem.ValueObjects.Serilog.ValueObjectLoggingOptions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Properties

### Assemblies {#AdCodicem_ValueObjects_Serilog_ValueObjectLoggingOptions_Assemblies}

Gets the assemblies whose value objects are registered before the logger is built.

```csharp
public ICollection<Assembly> Assemblies { get; }
```

#### Property Value

 [ICollection](https://learn.microsoft.com/dotnet/api/system.collections.generic.icollection\-1)<[Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\>

#### Remarks

Under the JIT, the registration the generator emits for an assembly runs once its code is first used, which a
logger configured at the top of <code>Program.cs</code> comes before: name the assemblies declaring value objects there.
A native binary has registered all of them at start-up.

### CaptureAsUnderlyingValue {#AdCodicem_ValueObjects_Serilog_ValueObjectLoggingOptions_CaptureAsUnderlyingValue}

Gets or sets a value indicating whether a value object logged without <code>@</code> is captured as its underlying value
too. Off by default.

```csharp
public bool CaptureAsUnderlyingValue { get; set; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

#### Remarks

It costs one pass over the properties of every event, and a numeric value object then reaches a filter, a query and
a format in the message template as the number it holds.

