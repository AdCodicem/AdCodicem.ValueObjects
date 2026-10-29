# Class ValueObjectJsonException {#AdCodicem_ValueObjects_ValueObjectJsonException}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

A value object refused the value System.Text.Json was reading or writing.

```csharp
[Serializable]
public sealed class ValueObjectJsonException : JsonException, ISerializable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[Exception](https://learn.microsoft.com/dotnet/api/system.exception) ← 
[JsonException](https://learn.microsoft.com/dotnet/api/system.text.json.jsonexception) ← 
[ValueObjectJsonException](AdCodicem.ValueObjects.ValueObjectJsonException.md)

#### Implements

[ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

#### Inherited Members

[JsonException.BytePositionInLine](https://learn.microsoft.com/dotnet/api/system.text.json.jsonexception.bytepositioninline), 
[JsonException.LineNumber](https://learn.microsoft.com/dotnet/api/system.text.json.jsonexception.linenumber), 
[JsonException.Message](https://learn.microsoft.com/dotnet/api/system.text.json.jsonexception.message), 
[JsonException.Path](https://learn.microsoft.com/dotnet/api/system.text.json.jsonexception.path), 
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
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The converter the generator emits throws it, and so do the converter for a value object written by hand and the
converter of <code>AnyEntityId</code>, wherever they refuse a value: a value or a dictionary key a rule rejects, a token
of the wrong kind or that the underlying type cannot hold, and a value to write that its type rejects. It is a
<xref href="System.Text.Json.JsonException" data-throw-if-not-resolved="false"></xref>, so a <code>catch (JsonException)</code> still catches it, and System.Text.Json treats it as
any exception a converter throws: it sets <xref href="System.Text.Json.JsonException.Path" data-throw-if-not-resolved="false"></xref> and rethrows the same instance.
</p>
<p>
It carries the code of the rule, which it also stores in <xref href="System.Exception.Data" data-throw-if-not-resolved="false"></xref> under
<xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>, so that a reader that only knows the data of an exception, such as a
logger writing it out, finds it too. <xref href="AdCodicem.ValueObjects.ValueObjectErrors.TryGetCode(System.Exception%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref> reads it from the exception or from
one that wraps it. Like every exception of the library, it never carries the value it refused.
</p>

## Constructors

### ValueObjectJsonException\(string?, Type, string\) {#AdCodicem_ValueObjects_ValueObjectJsonException__ctor_System_String_System_Type_System_String_}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.ValueObjectJsonException" data-throw-if-not-resolved="false"></xref> class.

```csharp
public ValueObjectJsonException(string? message, Type valueObjectType, string errorCode)
```

#### Parameters

`message` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Message naming the value object and the rule, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for System.Text.Json to write its own,
"The JSON value could not be converted to …", followed by the path, as it does for a token it cannot read.

`valueObjectType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Value object type that refused the value.

`errorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)

Stable, machine-readable code of the violated rule.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">valueObjectType</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">errorCode</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> or empty.

## Properties

### ErrorCode {#AdCodicem_ValueObjects_ValueObjectJsonException_ErrorCode}

Gets the stable, machine-readable code of the violated rule.

```csharp
public string ErrorCode { get; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Remarks

The code of the rule that fired, or <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable" data-throw-if-not-resolved="false"></xref> for a token that is not of
the underlying type at all, and <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref> for a JSON <code>null</code>, which only a
value object that cannot be <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> is handed.

### ValueObjectType {#AdCodicem_ValueObjects_ValueObjectJsonException_ValueObjectType}

Gets the value object type that refused the value.

```csharp
public Type ValueObjectType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)

