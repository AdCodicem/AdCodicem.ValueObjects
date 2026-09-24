# Class ValueObjectDescriptor {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor}

Namespace: [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Runtime description of a value object type, used by the integrations that only know a <xref href="System.Type" data-throw-if-not-resolved="false"></xref>.

```csharp
public sealed class ValueObjectDescriptor
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectDescriptor](AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The delegates it exposes operate on boxed values, which is acceptable because every one of them is invoked
during start-up work — building the EF Core model, generating an OpenAPI document, registering a Dapper
type handler — never on a per-request path. Request paths go through the strongly typed generic APIs.
</p>
<p>
Descriptors are produced by <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.For%60%602(AdCodicem.ValueObjects.Metadata.ValueObjectSchema)" data-throw-if-not-resolved="false"></xref>, which is fully generic and therefore free
of reflection: the source generator emits one call per value object in a module initializer.
</p>

## Properties

### Create {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_Create}

Gets the validating factory. Throws <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> on a rejected value.

```csharp
public Func<object?, object> Create { get; }
```

#### Property Value

 [Func](https://learn.microsoft.com/dotnet/api/system.func\-2)<[object](https://learn.microsoft.com/dotnet/api/system.object)?, [object](https://learn.microsoft.com/dotnet/api/system.object)\>

### CreateUnchecked {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_CreateUnchecked}

Gets the trusted-source factory, which performs neither normalization nor validation.

```csharp
public Func<object?, object> CreateUnchecked { get; }
```

#### Property Value

 [Func](https://learn.microsoft.com/dotnet/api/system.func\-2)<[object](https://learn.microsoft.com/dotnet/api/system.object)?, [object](https://learn.microsoft.com/dotnet/api/system.object)\>

### Format {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_Format}

Gets the invariant text representation of a boxed value object.

```csharp
public Func<object, string> Format { get; }
```

#### Property Value

 [Func](https://learn.microsoft.com/dotnet/api/system.func\-2)<[object](https://learn.microsoft.com/dotnet/api/system.object), [string](https://learn.microsoft.com/dotnet/api/system.string)\>

### GetValue {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_GetValue}

Gets the accessor returning the boxed underlying value of a boxed value object.

```csharp
public Func<object, object?> GetValue { get; }
```

#### Property Value

 [Func](https://learn.microsoft.com/dotnet/api/system.func\-2)<[object](https://learn.microsoft.com/dotnet/api/system.object), [object](https://learn.microsoft.com/dotnet/api/system.object)?\>

### Schema {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_Schema}

Gets the declarative constraints of the value object.

```csharp
public ValueObjectSchema Schema { get; }
```

#### Property Value

 [ValueObjectSchema](AdCodicem.ValueObjects.Metadata.ValueObjectSchema.md)

### TryCreate {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_TryCreate}

Gets the non-throwing validating factory.

```csharp
public BoxedTryCreate TryCreate { get; }
```

#### Property Value

 [BoxedTryCreate](AdCodicem.ValueObjects.Metadata.BoxedTryCreate.md)

### TryParse {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_TryParse}

Gets the non-throwing text parser.

```csharp
public BoxedTryParse TryParse { get; }
```

#### Property Value

 [BoxedTryParse](AdCodicem.ValueObjects.Metadata.BoxedTryParse.md)

### ValueObjectType {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_ValueObjectType}

Gets the value object type.

```csharp
public Type ValueObjectType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)

### ValueType {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_ValueType}

Gets the underlying value type.

```csharp
public Type ValueType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)

## Methods

### For<TSelf, TValue\>\(ValueObjectSchema\) {#AdCodicem_ValueObjects_Metadata_ValueObjectDescriptor_For__2_AdCodicem_ValueObjects_Metadata_ValueObjectSchema_}

Builds a descriptor for a value object, resolving every operation statically.

```csharp
public static ValueObjectDescriptor For<TSelf, TValue>(ValueObjectSchema schema) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`schema` [ValueObjectSchema](AdCodicem.ValueObjects.Metadata.ValueObjectSchema.md)

Declarative constraints of the value object.

#### Returns

 [ValueObjectDescriptor](AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.md)

The descriptor.

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

