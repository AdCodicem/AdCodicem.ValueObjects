# Struct ValidationResult {#AdCodicem_ValueObjects_ValidationResult}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Outcome of validating a candidate underlying value for a value object.

```csharp
public readonly struct ValidationResult : IEquatable<ValidationResult>
```

#### Implements

[IEquatable<ValidationResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable\-1)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

This is a <code>readonly struct</code> whose successful state is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/default">default</a>, so the
success path never allocates. Validation is fail-fast: the first violated rule wins and only that rule is
reported. Aggregating several errors across several members is the responsibility of a higher level
validator (see the FluentValidation integration package).

## Properties

### ErrorCode {#AdCodicem_ValueObjects_ValidationResult_ErrorCode}

Gets the stable, machine-readable code of the violated rule, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> when valid.

```csharp
public string? ErrorCode { get; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Remarks

Well-known codes are listed on <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes" data-throw-if-not-resolved="false"></xref>.

### ErrorMessage {#AdCodicem_ValueObjects_ValidationResult_ErrorMessage}

Gets the human-readable description of the violated rule, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> when valid.

```csharp
public string? ErrorMessage { get; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

### IsValid {#AdCodicem_ValueObjects_ValidationResult_IsValid}

Gets a value indicating whether the candidate value satisfies every rule.

```csharp
[MemberNotNullWhen(false, "ErrorCode")]
[MemberNotNullWhen(false, "ErrorMessage")]
public bool IsValid { get; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Success {#AdCodicem_ValueObjects_ValidationResult_Success}

Gets the successful result. Equivalent to <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/default">default</a>.

```csharp
public static ValidationResult Success { get; }
```

#### Property Value

 [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

## Methods

### Equals\(ValidationResult\) {#AdCodicem_ValueObjects_ValidationResult_Equals_AdCodicem_ValueObjects_ValidationResult_}

Indicates whether the current object is equal to another object of the same type.

```csharp
public bool Equals(ValidationResult other)
```

#### Parameters

`other` [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

An object to compare with this object.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> if the current object is equal to the <code class="paramref">other</code> parameter; otherwise, <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>.

### Equals\(object?\) {#AdCodicem_ValueObjects_ValidationResult_Equals_System_Object_}

Indicates whether this instance and a specified object are equal.

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

The object to compare with the current instance.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> if <code class="paramref">obj</code> and this instance are the same type and represent the same value; otherwise, <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>.

### Failure\(string, string\) {#AdCodicem_ValueObjects_ValidationResult_Failure_System_String_System_String_}

Creates a failed result.

```csharp
public static ValidationResult Failure(string errorCode, string errorMessage)
```

#### Parameters

`errorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)

Stable, machine-readable code of the violated rule.

`errorMessage` [string](https://learn.microsoft.com/dotnet/api/system.string)

Human-readable description of the violated rule.

#### Returns

 [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

A failed <xref href="AdCodicem.ValueObjects.ValidationResult" data-throw-if-not-resolved="false"></xref>.

#### Exceptions

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">errorCode</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> or empty.

### GetHashCode\(\) {#AdCodicem_ValueObjects_ValidationResult_GetHashCode}

Returns the hash code for this instance.

```csharp
public override int GetHashCode()
```

#### Returns

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

A 32-bit signed integer that is the hash code for this instance.

### InvalidFormat\(string\) {#AdCodicem_ValueObjects_ValidationResult_InvalidFormat_System_String_}

Creates a failed result carrying the <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.InvalidFormat" data-throw-if-not-resolved="false"></xref> code.

```csharp
public static ValidationResult InvalidFormat(string errorMessage = "The value has an invalid format.")
```

#### Parameters

`errorMessage` [string](https://learn.microsoft.com/dotnet/api/system.string)

Human-readable description of the violated rule.

#### Returns

 [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

A failed <xref href="AdCodicem.ValueObjects.ValidationResult" data-throw-if-not-resolved="false"></xref>.

### OutOfRange\(string\) {#AdCodicem_ValueObjects_ValidationResult_OutOfRange_System_String_}

Creates a failed result carrying the <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.OutOfRange" data-throw-if-not-resolved="false"></xref> code.

```csharp
public static ValidationResult OutOfRange(string errorMessage = "The value is out of range.")
```

#### Parameters

`errorMessage` [string](https://learn.microsoft.com/dotnet/api/system.string)

Human-readable description of the violated rule.

#### Returns

 [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

A failed <xref href="AdCodicem.ValueObjects.ValidationResult" data-throw-if-not-resolved="false"></xref>.

### Required\(string\) {#AdCodicem_ValueObjects_ValidationResult_Required_System_String_}

Creates a failed result carrying the <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref> code.

```csharp
public static ValidationResult Required(string errorMessage = "The value is required.")
```

#### Parameters

`errorMessage` [string](https://learn.microsoft.com/dotnet/api/system.string)

Human-readable description of the violated rule.

#### Returns

 [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

A failed <xref href="AdCodicem.ValueObjects.ValidationResult" data-throw-if-not-resolved="false"></xref>.

### ThrowIfInvalid\(Type, object?\) {#AdCodicem_ValueObjects_ValidationResult_ThrowIfInvalid_System_Type_System_Object_}

Throws a <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> when this result is a failure.

```csharp
public void ThrowIfInvalid(Type valueObjectType, object? attemptedValue)
```

#### Parameters

`valueObjectType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Value object type the validation was performed for.

`attemptedValue` [object](https://learn.microsoft.com/dotnet/api/system.object)?

Value that was rejected, surfaced on the exception for diagnostics.

#### Exceptions

 [ValueObjectException](AdCodicem.ValueObjects.ValueObjectException.md)

This result is a failure.

### ToString\(\) {#AdCodicem_ValueObjects_ValidationResult_ToString}

Returns the fully qualified type name of this instance.

```csharp
public override string ToString()
```

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)

The fully qualified type name.

## Operators

### operator ==\(ValidationResult, ValidationResult\) {#AdCodicem_ValueObjects_ValidationResult_op_Equality_AdCodicem_ValueObjects_ValidationResult_AdCodicem_ValueObjects_ValidationResult_}

Determines whether two results are equal.

```csharp
public static bool operator ==(ValidationResult left, ValidationResult right)
```

#### Parameters

`left` [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

Left operand.

`right` [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

Right operand.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when both results are equal.

### operator \!=\(ValidationResult, ValidationResult\) {#AdCodicem_ValueObjects_ValidationResult_op_Inequality_AdCodicem_ValueObjects_ValidationResult_AdCodicem_ValueObjects_ValidationResult_}

Determines whether two results differ.

```csharp
public static bool operator !=(ValidationResult left, ValidationResult right)
```

#### Parameters

`left` [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

Left operand.

`right` [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

Right operand.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when both results differ.

