# Class ValueObjectProtoProfiles.StrictLevel300 {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoProfiles_StrictLevel300}

Namespace: [AdCodicem.ValueObjects.ProtobufNet](AdCodicem.ValueObjects.ProtobufNet.md)  
Assembly: AdCodicem.ValueObjects.ProtobufNet.dll  

Reads through the rules, and writes as protobuf-net writes the underlying type at <xref href="ProtoBuf.CompatibilityLevel.Level300" data-throw-if-not-resolved="false"></xref>.

```csharp
public sealed class ValueObjectProtoProfiles.StrictLevel300 : IValueObjectProtoProfile
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectProtoProfiles.StrictLevel300](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoProfiles.StrictLevel300.md)

#### Implements

[IValueObjectProtoProfile](AdCodicem.ValueObjects.ProtobufNet.IValueObjectProtoProfile.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Properties

### CompatibilityLevel {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoProfiles_StrictLevel300_CompatibilityLevel}

Gets the level whose inbuilt serializer of the underlying type the value goes through, which decides how a
<xref href="System.Guid" data-throw-if-not-resolved="false"></xref>, a <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, a <xref href="System.DateTime" data-throw-if-not-resolved="false"></xref> and a
<xref href="System.TimeSpan" data-throw-if-not-resolved="false"></xref> are written.

```csharp
public static CompatibilityLevel CompatibilityLevel { get; }
```

#### Property Value

 CompatibilityLevel

### Trusted {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoProfiles_StrictLevel300_Trusted}

Gets whether a read goes through <code>CreateUnchecked</code>, for bytes the application alone wrote, rather than through
<code>TryCreate</code>.

```csharp
public static bool Trusted { get; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

