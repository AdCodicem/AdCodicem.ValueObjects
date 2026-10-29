# Class ValueObjectProtoSerializer<TSelf, TValue, TProfile\> {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoSerializer_3}

Namespace: [AdCodicem.ValueObjects.ProtobufNet](AdCodicem.ValueObjects.ProtobufNet.md)  
Assembly: AdCodicem.ValueObjects.ProtobufNet.dll  

Writes a value object as protobuf-net writes its underlying type at the level <code class="typeparamref">TProfile</code> names,
and reads it back through its rules unless the profile trusts the bytes.

```csharp
public sealed class ValueObjectProtoSerializer<TSelf, TValue, TProfile> : ISerializer<TSelf>, ISerializer<TSelf?> where TSelf : struct, IValueObject<TSelf, TValue> where TProfile : IValueObjectProtoProfile
```

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

`TProfile` 

The trust and the compatibility level, one of <xref href="AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoProfiles" data-throw-if-not-resolved="false"></xref>.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectProtoSerializer<TSelf, TValue, TProfile\>](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoSerializer\-3.md)

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

<p>
The value goes through <code>TypeModel.GetInbuiltSerializer&lt;TValue&gt;</code> at the profile's level, so a value object
writes what protobuf-net writes for the primitive it replaces at that level, but a valid zero, which it writes where
protobuf-net leaves out the primitive's, and a formatting hook of the value object never reaches the wire. protobuf-net calls a custom serializer whatever <code>[CompatibilityLevel]</code> a member, a
type or an assembly declares, and whatever <code>DataFormat</code> a member declares: the profile alone decides, and a
<code>ZigZag</code>, <code>FixedSize</code> or <code>WellKnown</code> member of a value object is written and read as
<code>DataFormat.Default</code> writes the underlying type, so such a member keeps its primitive type or its <code>.proto</code>
changes. A root value object, the message itself, is written at the profile's level too, where protobuf-net writes a
root primitive at <xref href="ProtoBuf.CompatibilityLevel.Level200" data-throw-if-not-resolved="false"></xref> whatever the model's level.
</p>
<p>
A strict read goes through <code>TryCreate</code>, which normalizes the value, and refuses one the value object rejects with
a <xref href="ProtoBuf.ProtoException" data-throw-if-not-resolved="false"></xref> naming the type and the rule, never the value, with the rule's code in
<xref href="System.Exception.Data" data-throw-if-not-resolved="false"></xref> under <xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>. A trusted read goes through
<code>CreateUnchecked</code>. Either way, a value the inbuilt serializer cannot read, of another wire type or beyond the range
of <code class="typeparamref">TValue</code>, is refused with <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable" data-throw-if-not-resolved="false"></xref>. A field missing from
the message is never read: the member keeps the default instance, which no rule checks.
</p>
<p>
A write refuses, with a <xref href="ProtoBuf.ProtoException" data-throw-if-not-resolved="false"></xref> carrying the code the same way, an uninitialized instance whose
value the value object rejects. Any other instance went through <code>Create</code>, or was read by a trusted serializer.
</p>

## Properties

### Features {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoSerializer_3_Features}

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

### Read\(ref State, TSelf\) {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoSerializer_3_Read_ProtoBuf_ProtoReader_State___0_}

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

The value read is one the inbuilt serializer of the underlying type cannot read, or, unless the profile is trusted,
one the value object refuses.

### Write\(ref State, TSelf\) {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoSerializer_3_Write_ProtoBuf_ProtoWriter_State___0_}

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

