# Class EntityIdPrefix {#AdCodicem_ValueObjects_Identifiers_EntityIdPrefix}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

The rules a declared prefix must satisfy.

```csharp
public static class EntityIdPrefix
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[EntityIdPrefix](AdCodicem.ValueObjects.Identifiers.EntityIdPrefix.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

The generator enforces these at compile time through <code>VO0015</code>, so a malformed prefix never reaches a
running application. They are restated here because the runtime factories are public and a hand-written
identifier type can reach them without passing through the generator.

## Fields

### MaxLength {#AdCodicem_ValueObjects_Identifiers_EntityIdPrefix_MaxLength}

The greatest total length of a prefix, separators included.

```csharp
public const int MaxLength = 16
```

#### Field Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

### MaxSegmentLength {#AdCodicem_ValueObjects_Identifiers_EntityIdPrefix_MaxSegmentLength}

The greatest length of one segment.

```csharp
public const int MaxSegmentLength = 8
```

#### Field Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

### Separator {#AdCodicem_ValueObjects_Identifiers_EntityIdPrefix_Separator}

The separator between the prefix and the body, and between the segments of a multi-segment prefix.

```csharp
public const char Separator = '_'
```

#### Field Value

 [char](https://learn.microsoft.com/dotnet/api/system.char)

## Methods

### IsValid\(string?, out string?\) {#AdCodicem_ValueObjects_Identifiers_EntityIdPrefix_IsValid_System_String_System_String__}

Determines whether a prefix is usable, and says why when it is not.

```csharp
public static bool IsValid(string? prefix, out string? error)
```

#### Parameters

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Prefix to check, without its trailing separator.

`error` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The rule that was broken, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> when the prefix is usable.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when <code class="paramref">prefix</code> is usable.

### ThrowIfInvalid\(string?, string\) {#AdCodicem_ValueObjects_Identifiers_EntityIdPrefix_ThrowIfInvalid_System_String_System_String_}

Throws when a prefix is unusable.

```csharp
public static void ThrowIfInvalid(string? prefix, string parameterName)
```

#### Parameters

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Prefix to check.

`parameterName` [string](https://learn.microsoft.com/dotnet/api/system.string)

Name of the argument being validated.

#### Exceptions

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">prefix</code> breaks one of the rules.

