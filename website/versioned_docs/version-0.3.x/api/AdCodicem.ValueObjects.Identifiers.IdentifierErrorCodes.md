# Class IdentifierErrorCodes {#AdCodicem_ValueObjects_Identifiers_IdentifierErrorCodes}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

Validation error codes specific to entity identifiers.

```csharp
public static class IdentifierErrorCodes
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[IdentifierErrorCodes](AdCodicem.ValueObjects.Identifiers.IdentifierErrorCodes.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

Every one of them is a rejection an <code>invalid_format</code> would have flattened. A boundary that tells the
caller the check character failed, rather than that something was wrong somewhere, is the difference
between a support ticket answered in one message and one answered in five.

## Fields

### InvalidCharacter {#AdCodicem_ValueObjects_Identifiers_IdentifierErrorCodes_InvalidCharacter}

The body holds a character outside the Crockford Base32 alphabet.

```csharp
public const string InvalidCharacter = "value_object.id.invalid_character"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### InvalidChecksum {#AdCodicem_ValueObjects_Identifiers_IdentifierErrorCodes_InvalidChecksum}

The trailing check character does not match the body — a typo, or a truncation.

```csharp
public const string InvalidChecksum = "value_object.id.invalid_checksum"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### InvalidLength {#AdCodicem_ValueObjects_Identifiers_IdentifierErrorCodes_InvalidLength}

The text is not the exact length the identifier profile requires.

```csharp
public const string InvalidLength = "value_object.id.invalid_length"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### InvalidPrefix {#AdCodicem_ValueObjects_Identifiers_IdentifierErrorCodes_InvalidPrefix}

The text does not carry the prefix declared by the identifier type.

```csharp
public const string InvalidPrefix = "value_object.id.invalid_prefix"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

### UnknownPrefix {#AdCodicem_ValueObjects_Identifiers_IdentifierErrorCodes_UnknownPrefix}

The prefix carried by the text belongs to no registered identifier type.

```csharp
public const string UnknownPrefix = "value_object.id.unknown_prefix"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

