# Class ValueObjectValidationResult {#AdCodicem_ValueObjects_Annotations_ValueObjectValidationResult}

Namespace: [AdCodicem.ValueObjects.Annotations](AdCodicem.ValueObjects.Annotations.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

The result DataAnnotations reports a refusal of <xref href="AdCodicem.ValueObjects.Annotations.ParsesAsAttribute" data-throw-if-not-resolved="false"></xref> or <xref href="AdCodicem.ValueObjects.Annotations.NotDefaultAttribute" data-throw-if-not-resolved="false"></xref>
with, carrying the code of the rule and the value object type beside the message.

```csharp
public sealed class ValueObjectValidationResult : ValidationResult
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValidationResult](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationresult) ← 
[ValueObjectValidationResult](AdCodicem.ValueObjects.Annotations.ValueObjectValidationResult.md)

#### Inherited Members

[ValidationResult.Success](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationresult.success), 
[ValidationResult.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationresult.tostring), 
[ValidationResult.ErrorMessage](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationresult.errormessage), 
[ValidationResult.MemberNames](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationresult.membernames), 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
A host that hands the result back keeps the code: <code>Validator.TryValidateObject</code>, <code>TryValidateProperty</code> and
<code>TryValidateValue</code> list it as it is, and so does CommunityToolkit.Mvvm's <code>ObservableValidator.GetErrors</code>. A
host that keeps the message alone loses it: MVC's model state, the errors of <code>AddValidation()</code>, the failures of
options validation. The message never carries the code, nor the value refused.
</p>
<p>
It is the <xref href="System.ComponentModel.DataAnnotations.ValidationResult" data-throw-if-not-resolved="false"></xref> of DataAnnotations, not the
<xref href="AdCodicem.ValueObjects.ValidationResult" data-throw-if-not-resolved="false"></xref> a value object's rules return, which is a struct and allocates nothing.
</p>

## Constructors

### ValueObjectValidationResult\(string, IEnumerable<string\>?, Type, string\) {#AdCodicem_ValueObjects_Annotations_ValueObjectValidationResult__ctor_System_String_System_Collections_Generic_IEnumerable_System_String__System_Type_System_String_}

Creates the result of a refusal.

```csharp
public ValueObjectValidationResult(string errorMessage, IEnumerable<string>? memberNames, Type valueObjectType, string errorCode)
```

#### Parameters

`errorMessage` [string](https://learn.microsoft.com/dotnet/api/system.string)

The message, as the host shows it.

`memberNames` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable\-1)<[string](https://learn.microsoft.com/dotnet/api/system.string)\>?

The members the refusal is reported under, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for none.

`valueObjectType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The value object whose rule refused the value.

`errorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)

The code of the rule, one of <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes" data-throw-if-not-resolved="false"></xref> or an application's own.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">valueObjectType</code> or <code class="paramref">errorCode</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">errorCode</code> is empty.

## Properties

### ErrorCode {#AdCodicem_ValueObjects_Annotations_ValueObjectValidationResult_ErrorCode}

Gets the code of the rule that refused the value, one of <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes" data-throw-if-not-resolved="false"></xref> or an application's
own.

```csharp
public string ErrorCode { get; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### ValueObjectType {#AdCodicem_ValueObjects_Annotations_ValueObjectValidationResult_ValueObjectType}

Gets the value object whose rule refused the value.

```csharp
public Type ValueObjectType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)

