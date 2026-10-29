# Class ValueObjectProtoProfiles {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoProfiles}

Namespace: [AdCodicem.ValueObjects.ProtobufNet](AdCodicem.ValueObjects.ProtobufNet.md)  
Assembly: AdCodicem.ValueObjects.ProtobufNet.dll  

The profiles the package ships: strict or trusted, at each of protobuf-net's compatibility levels.

```csharp
public static class ValueObjectProtoProfiles
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectProtoProfiles](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoProfiles.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

Each is a type argument, never an instance. Name one in <xref href="AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoSerializer%603" data-throw-if-not-resolved="false"></xref>:
<code>[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer&lt;OrderId, Guid, ValueObjectProtoProfiles.StrictLevel300&gt;))]</code>.

