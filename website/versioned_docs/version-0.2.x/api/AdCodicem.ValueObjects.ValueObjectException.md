# Class ValueObjectException {#AdCodicem_ValueObjects_ValueObjectException}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Thrown when a value object is constructed from a value that violates one of its rules.

```csharp
[Serializable]
public class ValueObjectException : Exception, ISerializable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[Exception](https://learn.microsoft.com/dotnet/api/system.exception) ← 
[ValueObjectException](AdCodicem.ValueObjects.ValueObjectException.md)

#### Implements

[ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

#### Inherited Members

[Exception.GetBaseException\(\)](https://learn.microsoft.com/dotnet/api/system.exception.getbaseexception), 
[Exception.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.exception.gettype), 
[Exception.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.exception.tostring), 
[Exception.Data](https://learn.microsoft.com/dotnet/api/system.exception.data), 
[Exception.HelpLink](https://learn.microsoft.com/dotnet/api/system.exception.helplink), 
[Exception.HResult](https://learn.microsoft.com/dotnet/api/system.exception.hresult), 
[Exception.InnerException](https://learn.microsoft.com/dotnet/api/system.exception.innerexception), 
[Exception.Message](https://learn.microsoft.com/dotnet/api/system.exception.message), 
[Exception.Source](https://learn.microsoft.com/dotnet/api/system.exception.source), 
[Exception.StackTrace](https://learn.microsoft.com/dotnet/api/system.exception.stacktrace), 
[Exception.TargetSite](https://learn.microsoft.com/dotnet/api/system.exception.targetsite), 
[Exception.SerializeObjectState](https://learn.microsoft.com/dotnet/api/system.exception.serializeobjectstate), 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

Every integration on a hot path (JSON, model binding, EF Core, Dapper) uses the <code>TryCreate</code> family
instead, so this exception is reserved for programmer errors and for the explicit <code>Create</code> entry point.

## Constructors

### ValueObjectException\(\) {#AdCodicem_ValueObjects_ValueObjectException__ctor}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> class.

```csharp
public ValueObjectException()
```

### ValueObjectException\(string\) {#AdCodicem_ValueObjects_ValueObjectException__ctor_System_String_}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> class.

```csharp
public ValueObjectException(string message)
```

#### Parameters

`message` [string](https://learn.microsoft.com/dotnet/api/system.string)

Message describing the violated rule.

### ValueObjectException\(string, Exception\) {#AdCodicem_ValueObjects_ValueObjectException__ctor_System_String_System_Exception_}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> class.

```csharp
public ValueObjectException(string message, Exception innerException)
```

#### Parameters

`message` [string](https://learn.microsoft.com/dotnet/api/system.string)

Message describing the violated rule.

`innerException` [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

Cause of this exception.

### ValueObjectException\(string, Type?, string?, object?\) {#AdCodicem_ValueObjects_ValueObjectException__ctor_System_String_System_Type_System_String_System_Object_}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> class.

```csharp
public ValueObjectException(string message, Type? valueObjectType, string? errorCode, object? attemptedValue)
```

#### Parameters

`message` [string](https://learn.microsoft.com/dotnet/api/system.string)

Message describing the violated rule.

`valueObjectType` [Type](https://learn.microsoft.com/dotnet/api/system.type)?

Value object type that rejected the value.

`errorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Stable, machine-readable code of the violated rule.

`attemptedValue` [object](https://learn.microsoft.com/dotnet/api/system.object)?

Value that was rejected.

## Properties

### AttemptedValue {#AdCodicem_ValueObjects_ValueObjectException_AttemptedValue}

Gets the value that was rejected.

```csharp
public object? AttemptedValue { get; }
```

#### Property Value

 [object](https://learn.microsoft.com/dotnet/api/system.object)?

### ErrorCode {#AdCodicem_ValueObjects_ValueObjectException_ErrorCode}

Gets the stable, machine-readable code of the violated rule.

```csharp
public string? ErrorCode { get; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### ValueObjectType {#AdCodicem_ValueObjects_ValueObjectException_ValueObjectType}

Gets the value object type that rejected the value.

```csharp
public Type? ValueObjectType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)?

## Methods

### Throw\(Type, string, string, object?\) {#AdCodicem_ValueObjects_ValueObjectException_Throw_System_Type_System_String_System_String_System_Object_}

Throws a <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> describing a rejected value.

```csharp
[DoesNotReturn]
public static void Throw(Type valueObjectType, string errorCode, string errorMessage, object? attemptedValue)
```

#### Parameters

`valueObjectType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Value object type that rejected the value.

`errorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)

Stable, machine-readable code of the violated rule.

`errorMessage` [string](https://learn.microsoft.com/dotnet/api/system.string)

Human-readable description of the violated rule.

`attemptedValue` [object](https://learn.microsoft.com/dotnet/api/system.object)?

Value that was rejected.

#### Exceptions

 [ValueObjectException](AdCodicem.ValueObjects.ValueObjectException.md)

Always.

