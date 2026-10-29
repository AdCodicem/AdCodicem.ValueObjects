# Class KnownValueInfo {#AdCodicem_ValueObjects_Metadata_KnownValueInfo}

Namespace: [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

A value declared through <code>[KnownValue]</code>: the underlying value, the name of the static property the generator
creates for it, and the description the attribute gives it.

```csharp
public sealed record KnownValueInfo : IEquatable<KnownValueInfo>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[KnownValueInfo](AdCodicem.ValueObjects.Metadata.KnownValueInfo.md)

#### Implements

[IEquatable<KnownValueInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable\-1)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectSchema.KnownValueDetails" data-throw-if-not-resolved="false"></xref> lists one per known value, in declaration order. The OpenAPI
integration publishes the names beside the <code>enum</code> of a closed value set, so that a client generated from the
document names the members of its enumeration as the server code names its constants, <code>France</code> rather than
<code>FR</code>.
</p>
<p>
The name therefore becomes part of the contract a generated client holds: renaming a known value renames that member
in every client generated afterwards, as renaming a member of an enumeration would.
</p>

## Constructors

### KnownValueInfo\(object, string, string?\) {#AdCodicem_ValueObjects_Metadata_KnownValueInfo__ctor_System_Object_System_String_System_String_}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.Metadata.KnownValueInfo" data-throw-if-not-resolved="false"></xref> record.

```csharp
public KnownValueInfo(object value, string name, string? description = null)
```

#### Parameters

`value` [object](https://learn.microsoft.com/dotnet/api/system.object)

The underlying value, as <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectSchema.KnownValues" data-throw-if-not-resolved="false"></xref> holds it: normalized, and of the underlying
type wherever the generator wrote the schema.

`name` [string](https://learn.microsoft.com/dotnet/api/system.string)

The name of the known value, the name of its static property on the value object.

`description` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Its description, if any.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">value</code> or <code class="paramref">name</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">name</code> is empty or consists only of white space.

## Properties

### Description {#AdCodicem_ValueObjects_Metadata_KnownValueInfo_Description}

Gets the description of the known value, if one was declared.

```csharp
public string? Description { get; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### Name {#AdCodicem_ValueObjects_Metadata_KnownValueInfo_Name}

Gets the name of the known value, the name of its static property on the value object.

```csharp
public string Name { get; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### Value {#AdCodicem_ValueObjects_Metadata_KnownValueInfo_Value}

Gets the underlying value.

```csharp
public object Value { get; }
```

#### Property Value

 [object](https://learn.microsoft.com/dotnet/api/system.object)

