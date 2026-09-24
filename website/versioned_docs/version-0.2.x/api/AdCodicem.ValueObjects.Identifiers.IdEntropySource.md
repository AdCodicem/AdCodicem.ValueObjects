# Class IdEntropySource {#AdCodicem_ValueObjects_Identifiers_IdEntropySource}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

The source of the random part of a new entity identifier.

```csharp
public abstract class IdEntropySource
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[IdEntropySource](AdCodicem.ValueObjects.Identifiers.IdEntropySource.md)

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
This exists so tests can make identifier generation deterministic without reaching for a seeded
<xref href="System.Random" data-throw-if-not-resolved="false"></xref> in production by mistake. The default, <xref href="AdCodicem.ValueObjects.Identifiers.IdEntropySource.System" data-throw-if-not-resolved="false"></xref>, is a cryptographic
generator, and that is not a detail: identifiers appear in URLs and logs, and a
<xref href="System.Random.Shared" data-throw-if-not-resolved="false"></xref>-derived body is recoverable from a handful of samples, which would turn every
identifier into a guessable one.
</p>
<p>
An identifier is still not a secret. Unguessability is defence in depth; authorization is the control.
</p>

## Properties

### System {#AdCodicem_ValueObjects_Identifiers_IdEntropySource_System}

Gets the cryptographic source used unless a caller substitutes one.

```csharp
public static IdEntropySource System { get; }
```

#### Property Value

 [IdEntropySource](AdCodicem.ValueObjects.Identifiers.IdEntropySource.md)

## Methods

### Fill\(Span<byte\>\) {#AdCodicem_ValueObjects_Identifiers_IdEntropySource_Fill_System_Span_System_Byte__}

Fills a buffer with uniformly distributed bytes.

```csharp
public abstract void Fill(Span<byte> destination)
```

#### Parameters

`destination` [Span](https://learn.microsoft.com/dotnet/api/system.span\-1)<[byte](https://learn.microsoft.com/dotnet/api/system.byte)\>

Buffer to fill entirely.

