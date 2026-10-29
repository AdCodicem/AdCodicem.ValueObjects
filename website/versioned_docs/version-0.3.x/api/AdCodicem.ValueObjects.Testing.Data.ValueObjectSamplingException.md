# Class ValueObjectSamplingException {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplingException}

Namespace: [AdCodicem.ValueObjects.Testing.Data](AdCodicem.ValueObjects.Testing.Data.md)  
Assembly: AdCodicem.ValueObjects.Testing.Data.dll  

Thrown when a sampler cannot draw a value its type accepts: no candidate, drawn from its schema or from the generator
registered for it, passed its rules, and it declares no example they accept.

```csharp
public sealed class ValueObjectSamplingException : InvalidOperationException, ISerializable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[Exception](https://learn.microsoft.com/dotnet/api/system.exception) ← 
[SystemException](https://learn.microsoft.com/dotnet/api/system.systemexception) ← 
[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception) ← 
[ValueObjectSamplingException](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplingException.md)

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
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
It is a failure of test set-up, not a value refused at a boundary: it carries the code of the last refusal in
<xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplingException.ErrorCode" data-throw-if-not-resolved="false"></xref>, and nothing in <xref href="System.Exception.Data" data-throw-if-not-resolved="false"></xref>, so <code>ValueObjectErrors.TryGetCode</code> does not
read it as a refusal. Its message never holds a candidate value.
</p>
<p>
Its last sentence names the registration that gives the sampler a generator: the one to add, or the one whose values
the type refused. A library drawing through the sampler writes it in its own terms by throwing it with its own
registration.
</p>

## Constructors

### ValueObjectSamplingException\(Type, int, string?, string, bool\) {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplingException__ctor_System_Type_System_Int32_System_String_System_String_System_Boolean_}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplingException" data-throw-if-not-resolved="false"></xref> class.

```csharp
public ValueObjectSamplingException(Type valueObjectType, int attempts, string? errorCode, string registration, bool fromGenerator = false)
```

#### Parameters

`valueObjectType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The value object no value could be drawn for.

`attempts` [int](https://learn.microsoft.com/dotnet/api/system.int32)

How many candidates were tried.

`errorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The code of the rule that refused the last candidate, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> when none was drawn.

`registration` [string](https://learn.microsoft.com/dotnet/api/system.string)

The registration of a generator, as code: <code>options.Use&lt;EvenCode, string&gt;(random =&gt; ...)</code>.

`fromGenerator` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

Whether the candidates came from a generator registered for the type, whose values its rules refused, rather than
from its schema.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">valueObjectType</code> or <code class="paramref">registration</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

## Properties

### Attempts {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplingException_Attempts}

Gets how many candidates were tried.

```csharp
public int Attempts { get; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

### ErrorCode {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplingException_ErrorCode}

Gets the code of the rule that refused the last candidate, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> when no candidate was drawn at
all: an underlying type the sampler draws no value of, with no known value, or lengths and a pattern no string it
built could meet.

```csharp
public string? ErrorCode { get; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### FromGenerator {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplingException_FromGenerator}

Gets a value indicating whether the candidates came from a generator registered for the type rather than from its
schema: the generator gives values the type refuses.

```csharp
public bool FromGenerator { get; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### ValueObjectType {#AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplingException_ValueObjectType}

Gets the value object no value could be drawn for.

```csharp
public Type ValueObjectType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)

