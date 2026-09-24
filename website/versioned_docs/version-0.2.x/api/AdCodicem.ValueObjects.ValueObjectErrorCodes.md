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
<code>ProblemDetails</code> responses and can be mapped to localized messages by the consumer.
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

