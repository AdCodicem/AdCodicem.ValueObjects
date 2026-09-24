# Class ValueObjectDapper {#AdCodicem_ValueObjects_Dapper_ValueObjectDapper}

Namespace: [AdCodicem.ValueObjects.Dapper](AdCodicem.ValueObjects.Dapper.md)  
Assembly: AdCodicem.ValueObjects.Dapper.dll  

Registers Dapper type handlers for value objects.

```csharp
public static class ValueObjectDapper
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectDapper](AdCodicem.ValueObjects.Dapper.ValueObjectDapper.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

Dapper keeps its handlers in a process-wide table, so this is called once at start-up. Without it, every
query touching a value object would need an explicit projection.

## Methods

### AddValueObjectHandlers\(params Assembly\[\]\) {#AdCodicem_ValueObjects_Dapper_ValueObjectDapper_AddValueObjectHandlers_System_Reflection_Assembly___}

Registers a handler for every value object declared in the given assemblies.

```csharp
[RequiresUnreferencedCode("Closes the generic type handler over each value object type.")]
[RequiresDynamicCode("Closes the generic type handler over each value object type.")]
public static void AddValueObjectHandlers(params Assembly[] assemblies)
```

#### Parameters

`assemblies` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\[\]

Assemblies declaring the value objects. When none is given, everything already registered is handled.

