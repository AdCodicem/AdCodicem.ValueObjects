# Class ValueObjectProtoSerializer<TSelf, TValue\> {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoSerializer_2}

Namespace: [AdCodicem.ValueObjects.ProtobufNet](AdCodicem.ValueObjects.ProtobufNet.md)  
Assembly: AdCodicem.ValueObjects.ProtobufNet.dll  

Writes a value object as protobuf-net writes its underlying type at <xref href="ProtoBuf.CompatibilityLevel.Level200" data-throw-if-not-resolved="false"></xref>, and
reads it back through its rules.

```csharp
public sealed class ValueObjectProtoSerializer<TSelf, TValue> : ISerializer<TSelf>, ISerializer<TSelf?> where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectProtoSerializer<TSelf, TValue\>](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoSerializer\-2.md)

#### Implements

ISerializer<TSelf\>, 
ISerializer<TSelf?\>

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

The strict profile at protobuf-net's default level, <xref href="AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoProfiles.StrictLevel200" data-throw-if-not-resolved="false"></xref>: the same as
<xref href="AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoSerializer%603" data-throw-if-not-resolved="false"></xref> closed over it, under a shorter name for
<code>[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer&lt;Sku, string&gt;))]</code>.

## Properties

### Features {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoSerializer_2_Features}

Indicates the features (including the default wire-type) for this type/serializer

```csharp
public SerializerFeatures Features { get; }
```

#### Property Value

 SerializerFeatures

#### Exceptions

 [NotSupportedException](https://learn.microsoft.com/dotnet/api/system.notsupportedexception)

protobuf-net has no inbuilt serializer for <code class="typeparamref">TValue</code>.

## Methods

### Read\(ref State, TSelf\) {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoSerializer_2_Read_ProtoBuf_ProtoReader_State___0_}

Deserialize an instance from the supplied writer

```csharp
public TSelf Read(ref ProtoReader.State state, TSelf value)
```

#### Parameters

`state` ProtoReader.State

`value` TSelf

#### Returns

 TSelf

#### Exceptions

 ProtoException

The value read is one the inbuilt serializer of the underlying type cannot read, or one the value object refuses.

### Write\(ref State, TSelf\) {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoSerializer_2_Write_ProtoBuf_ProtoWriter_State___0_}

Serialize an instance to the supplied writer

```csharp
public void Write(ref ProtoWriter.State state, TSelf value)
```

#### Parameters

`state` ProtoWriter.State

`value` TSelf

#### Exceptions

 ProtoException

<code class="paramref">value</code> equals <code>default(TSelf)</code> and holds a value the value object rejects.

