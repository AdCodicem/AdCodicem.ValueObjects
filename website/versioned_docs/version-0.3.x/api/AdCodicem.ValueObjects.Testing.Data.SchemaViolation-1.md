# Struct SchemaViolation<TValue\> {#AdCodicem_ValueObjects_Testing_Data_SchemaViolation_1}

Namespace: [AdCodicem.ValueObjects.Testing.Data](AdCodicem.ValueObjects.Testing.Data.md)  
Assembly: AdCodicem.ValueObjects.Testing.Data.dll  

A value the schema of a value object rules out, with the rule it breaks.

```csharp
public readonly record struct SchemaViolation<TValue> : IEquatable<SchemaViolation<TValue>>
```

#### Type Parameters

`TValue` 

The underlying type.

#### Implements

[IEquatable<SchemaViolation<TValue\>\>](https://learn.microsoft.com/dotnet/api/system.iequatable\-1)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Constructors

### SchemaViolation\(TValue, string\) {#AdCodicem_ValueObjects_Testing_Data_SchemaViolation_1__ctor__0_System_String_}

A value the schema of a value object rules out, with the rule it breaks.

```csharp
public SchemaViolation(TValue Value, string Rule)
```

#### Parameters

`Value` TValue

The value, which the type is expected to refuse.

`Rule` [string](https://learn.microsoft.com/dotnet/api/system.string)

The rule it breaks, as the schema declares it: <code>MinLength (15)</code>, <code>MaxLength (34)</code>, <code>Minimum (0)</code>,
<code>Maximum (1000)</code> or <code>a closed value set</code>.

## Properties

### Rule {#AdCodicem_ValueObjects_Testing_Data_SchemaViolation_1_Rule}

The rule it breaks, as the schema declares it: <code>MinLength (15)</code>, <code>MaxLength (34)</code>, <code>Minimum (0)</code>,
<code>Maximum (1000)</code> or <code>a closed value set</code>.

```csharp
public string Rule { get; init; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### Value {#AdCodicem_ValueObjects_Testing_Data_SchemaViolation_1_Value}

The value, which the type is expected to refuse.

```csharp
public TValue Value { get; init; }
```

#### Property Value

 TValue

