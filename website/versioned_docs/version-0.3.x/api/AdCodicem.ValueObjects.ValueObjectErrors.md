# Class ValueObjectErrors {#AdCodicem_ValueObjects_ValueObjectErrors}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Carries the code of a violated rule across an exception that a serializer, a data access library or any other
integration throws in its own terms.

```csharp
public static class ValueObjectErrors
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectErrors](AdCodicem.ValueObjects.ValueObjectErrors.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
An integration on a boundary reports a rejected value with the exception its ecosystem expects: a
<xref href="System.Text.Json.JsonException" data-throw-if-not-resolved="false"></xref>, a <code>JsonSerializationException</code> for Newtonsoft.Json, a
<code>DataException</code> for Dapper. Those types have no place for a code, so the integrations store it in
<xref href="System.Exception.Data" data-throw-if-not-resolved="false"></xref>, under <xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>, and the System.Text.Json converters throw a
<xref href="AdCodicem.ValueObjects.ValueObjectJsonException" data-throw-if-not-resolved="false"></xref>, which carries it as a property as well. <xref href="AdCodicem.ValueObjects.ValueObjectErrors.TryGetCode(System.Exception%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref> reads
either, and a <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref>, from the exception or from one it wraps.
</p>
<p>
An exception a value object's rule raised carries the code and the value object type, never the value it refused.
A DataAnnotations <xref href="System.ComponentModel.DataAnnotations.ValidationException" data-throw-if-not-resolved="false"></xref> that the throwing
<code>Validator</code> methods raise for a refusal of <xref href="AdCodicem.ValueObjects.Annotations.ParsesAsAttribute" data-throw-if-not-resolved="false"></xref> or
<xref href="AdCodicem.ValueObjects.Annotations.NotDefaultAttribute" data-throw-if-not-resolved="false"></xref> carries both on its
<xref href="AdCodicem.ValueObjects.Annotations.ValueObjectValidationResult" data-throw-if-not-resolved="false"></xref>, and holds the value it was handed in
<xref href="System.ComponentModel.DataAnnotations.ValidationException.Value" data-throw-if-not-resolved="false"></xref>, as DataAnnotations does for any
attribute. The error of a database provider for a row a CHECK constraint from the rules refused, to which the Entity Framework
Core package's <code>UseValueObjectCheckErrors()</code> adds the code of the constraint's rule, carries no type, and keeps
the message its provider wrote, which may hold the row.
</p>

## Fields

### ErrorCodeKey {#AdCodicem_ValueObjects_ValueObjectErrors_ErrorCodeKey}

The key under which an integration stores the code of the violated rule in <xref href="System.Exception.Data" data-throw-if-not-resolved="false"></xref>.

```csharp
public const string ErrorCodeKey = "AdCodicem.ValueObjects.ErrorCode"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Remarks

An integration of your own stores a code there the same way, so that <xref href="AdCodicem.ValueObjects.ValueObjectErrors.TryGetCode(System.Exception%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref>, and anything
that writes out the data of an exception, finds it: <code>exception.Data[ValueObjectErrors.ErrorCodeKey] = code</code>.

## Methods

### TryGetCode\(Exception, out string?\) {#AdCodicem_ValueObjects_ValueObjectErrors_TryGetCode_System_Exception_System_String__}

Reads the code of the rule an exception, or one of the exceptions it wraps, carries.

```csharp
public static bool TryGetCode(Exception exception, out string? code)
```

#### Parameters

`exception` [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

The exception, as it was caught.

`code` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The code, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> when no exception of the chain carries one.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when a code was found.

#### Remarks

<p>
Each exception is read in turn, from <code class="paramref">exception</code> down its
<xref href="System.Exception.InnerException" data-throw-if-not-resolved="false"></xref> chain, since a framework may wrap the exception that carries the code: a
minimal API answers a body it cannot read with a <code>BadHttpRequestException</code> around the
<xref href="System.Text.Json.JsonException" data-throw-if-not-resolved="false"></xref>, and Entity Framework Core fails <code>SaveChanges</code> with a
<code>DbUpdateException</code> around the <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref>. The first code found wins, read from
<xref href="AdCodicem.ValueObjects.ValueObjectException.ErrorCode" data-throw-if-not-resolved="false"></xref>, then <xref href="AdCodicem.ValueObjects.ValueObjectJsonException.ErrorCode" data-throw-if-not-resolved="false"></xref>, then the
<xref href="AdCodicem.ValueObjects.Annotations.ValueObjectValidationResult" data-throw-if-not-resolved="false"></xref> a DataAnnotations
<xref href="System.ComponentModel.DataAnnotations.ValidationException" data-throw-if-not-resolved="false"></xref> carries, which the throwing
<code>Validator.ValidateObject</code>, <code>ValidateProperty</code> and <code>ValidateValue</code> raise for a refusal of
<xref href="AdCodicem.ValueObjects.Annotations.ParsesAsAttribute" data-throw-if-not-resolved="false"></xref> or <xref href="AdCodicem.ValueObjects.Annotations.NotDefaultAttribute" data-throw-if-not-resolved="false"></xref>, then
<xref href="System.Exception.Data" data-throw-if-not-resolved="false"></xref> under <xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>.
</p>
<p>
An exception that reports no broken rule carries none. The error of a database provider for a row a CHECK
constraint from the rules refused carries one under the Entity Framework Core package's
<code>UseValueObjectCheckErrors()</code>, though no value object raised it, and a code of your own travels as the
framework's do.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">exception</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

