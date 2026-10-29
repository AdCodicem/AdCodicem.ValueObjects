# Class ValueObjectException {#AdCodicem_ValueObjects_ValueObjectException}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Thrown when a value object is constructed from a value that violates one of its rules.

```csharp
[Serializable]
public class ValueObjectException : FormatException, ISerializable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[Exception](https://learn.microsoft.com/dotnet/api/system.exception) ← 
[SystemException](https://learn.microsoft.com/dotnet/api/system.systemexception) ← 
[FormatException](https://learn.microsoft.com/dotnet/api/system.formatexception) ← 
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

<p>
The integrations that take outside input (JSON, model binding, FluentValidation, Dapper) use the
<code>TryCreate</code> and <code>TryParse</code> family and report a refusal in their own terms. This exception comes from
the explicit <code>Create</code> and <code>Parse</code> entry points and an explicit conversion, for code that treats a
rejected value as a bug, from a strict EF Core read, which goes through <code>Create</code> and fails the query, and from
an EF Core write of an uninitialized value object whose value its type rejects, which fails <code>SaveChanges</code>.
</p>
<p>
It is a <xref href="System.FormatException" data-throw-if-not-resolved="false"></xref>, the exception <xref href="System.IParsable%601.Parse(System.String%2cSystem.IFormatProvider)" data-throw-if-not-resolved="false"></xref> documents for text
it refuses, so code written against that contract catches a value object's rejection, and a framework that
recognizes a <xref href="System.FormatException" data-throw-if-not-resolved="false"></xref> as bad input, such as ASP.NET Core MVC binding through a type
converter, reports it as such. Every path throws this one type, including <code>Create</code> and the other paths
that take a value rather than text. A <code>catch (FormatException)</code> therefore catches it too, and placed
before a <code>catch (ValueObjectException)</code> of the same <code>try</code>, makes that clause unreachable (CS0160).
</p>
<p>
The message names the value object and the rule, never the rejected value, so a log recording it records no IBAN,
email address or telephone number. <xref href="AdCodicem.ValueObjects.ValueObjectException.AttemptedValue" data-throw-if-not-resolved="false"></xref> does hold the value, except on a value object its
author classifies as sensitive data: see that property.
</p>

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

#### Remarks

<p>
The value as it was passed in, before normalization, and the text for <code>Parse</code>. Unlike the message, it holds
the value itself: a logger that records the public properties of an exception, as Serilog.Exceptions does,
records it in clear.
</p>
<p>
The generated <code>Create</code> and <code>Parse</code> leave it <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> on a value object that carries an
attribute derived from <code>Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute</code>, other
than <code>NoDataClassificationAttribute</code>, and so does the type converter of a generic one: classify a value
object that holds personal data.
</p>

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

