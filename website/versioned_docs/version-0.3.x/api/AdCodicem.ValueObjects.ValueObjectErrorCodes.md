# Class ValueObjectErrorCodes {#AdCodicem_ValueObjects_ValueObjectErrorCodes}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Well-known validation error codes shared by the framework and its integrations.

```csharp
public static class ValueObjectErrorCodes
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectErrorCodes](AdCodicem.ValueObjects.ValueObjectErrorCodes.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

Codes are stable strings so they can safely cross process boundaries: they are surfaced in
<code>ProblemDetails</code> responses, and each keys the message an application shows in its users' language,
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectMessages.KeyFor(System.String)" data-throw-if-not-resolved="false"></xref> giving the key and
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectMessages.ArgumentsFor(AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%2cSystem.String%2cSystem.IFormatProvider)" data-throw-if-not-resolved="false"></xref>
the arguments the declared rules supply, which the MVC, minimal API and FluentValidation integrations look up.
Consumers are free to define their own codes; these are only the ones the framework itself emits.

## Fields

### InvalidFormat {#AdCodicem_ValueObjects_ValueObjectErrorCodes_InvalidFormat}

The value does not match the expected shape.

```csharp
public const string InvalidFormat = "value_object.invalid_format"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### NotAKnownValue {#AdCodicem_ValueObjects_ValueObjectErrorCodes_NotAKnownValue}

The value does not belong to the closed set of values accepted by the type.

```csharp
public const string NotAKnownValue = "value_object.not_a_known_value"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### NotParsable {#AdCodicem_ValueObjects_ValueObjectErrorCodes_NotParsable}

The supplied text could not be converted to the underlying type.

```csharp
public const string NotParsable = "value_object.not_parsable"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### OutOfRange {#AdCodicem_ValueObjects_ValueObjectErrorCodes_OutOfRange}

The value falls outside the accepted range.

```csharp
public const string OutOfRange = "value_object.out_of_range"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### Required {#AdCodicem_ValueObjects_ValueObjectErrorCodes_Required}

A value was expected but none was supplied.

```csharp
public const string Required = "value_object.required"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### TooLong {#AdCodicem_ValueObjects_ValueObjectErrorCodes_TooLong}

The value is longer than allowed.

```csharp
public const string TooLong = "value_object.too_long"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### TooShort {#AdCodicem_ValueObjects_ValueObjectErrorCodes_TooShort}

The value is shorter than allowed.

```csharp
public const string TooShort = "value_object.too_short"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### ToUpperSnakeCase\(string\) {#AdCodicem_ValueObjects_ValueObjectErrorCodes_ToUpperSnakeCase_System_String_}

Maps a code to the upper snake case gRPC's error model takes as a reason: <code>value_object.too_long</code> becomes
<code>VALUE_OBJECT_TOO_LONG</code>, and <code>iban.check_digits</code> becomes <code>IBAN_CHECK_DIGITS</code>.

```csharp
public static string ToUpperSnakeCase(string errorCode)
```

#### Parameters

`errorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)

The code, one of the framework's or one of your own.

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)

The code in upper snake case.

#### Remarks

<p>
<code>google.rpc.ErrorInfo.reason</code> and <code>google.rpc.BadRequest.FieldViolation.reason</code> must match
<code>[A-Z][A-Z0-9_]+[A-Z0-9]</code>, and an <code>ErrorInfo</code> reason is at most 63 characters long, so a code such as
<code>value_object.too_long</code> cannot be used as it is. Every service that maps a code through this method maps it
the same way, and a client in another language branches on the result.
</p>
<p>
ASCII letters are upper-cased and ASCII digits kept. Any run of other characters, the dot and the underscore
included, becomes one underscore, and an underscore left at either end is dropped. A code that cannot become a
valid reason is refused rather than truncated or given a prefix, which would make two codes one.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">errorCode</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

The code, once mapped, is no valid reason: it does not start with a letter, holds fewer than three letters,
digits and underscores, or more than 63.

