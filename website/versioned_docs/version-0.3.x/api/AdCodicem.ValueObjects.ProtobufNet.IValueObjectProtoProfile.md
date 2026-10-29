# Interface IValueObjectProtoProfile {#AdCodicem_ValueObjects_ProtobufNet_IValueObjectProtoProfile}

Namespace: [AdCodicem.ValueObjects.ProtobufNet](AdCodicem.ValueObjects.ProtobufNet.md)  
Assembly: AdCodicem.ValueObjects.ProtobufNet.dll  

Fixes, for a closed <xref href="AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoSerializer%603" data-throw-if-not-resolved="false"></xref>, whether reads skip validation and
which of protobuf-net's inbuilt serializers carries the value.

```csharp
public interface IValueObjectProtoProfile
```

## Remarks

protobuf-net creates a serializer named by its type with its parameterless constructor, and keeps one per type for the
whole process, so what a serializer does is fixed by its type arguments rather than by arguments to a constructor.
<xref href="AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoProfiles" data-throw-if-not-resolved="false"></xref> holds the six combinations; a profile of your own implements this interface.

## Properties

### CompatibilityLevel {#AdCodicem_ValueObjects_ProtobufNet_IValueObjectProtoProfile_CompatibilityLevel}

Gets the level whose inbuilt serializer of the underlying type the value goes through, which decides how a
<xref href="System.Guid" data-throw-if-not-resolved="false"></xref>, a <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, a <xref href="System.DateTime" data-throw-if-not-resolved="false"></xref> and a
<xref href="System.TimeSpan" data-throw-if-not-resolved="false"></xref> are written.

```csharp
public static abstract CompatibilityLevel CompatibilityLevel { get; }
```

#### Property Value

 CompatibilityLevel

### Trusted {#AdCodicem_ValueObjects_ProtobufNet_IValueObjectProtoProfile_Trusted}

Gets whether a read goes through <code>CreateUnchecked</code>, for bytes the application alone wrote, rather than through
<code>TryCreate</code>.

```csharp
public static abstract bool Trusted { get; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

