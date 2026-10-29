# Class ValueObjectProtoModelExtensions {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoModelExtensions}

Namespace: [AdCodicem.ValueObjects.ProtobufNet](AdCodicem.ValueObjects.ProtobufNet.md)  
Assembly: AdCodicem.ValueObjects.ProtobufNet.dll  

Registers the serializers of value objects on a protobuf-net <xref href="ProtoBuf.Meta.RuntimeTypeModel" data-throw-if-not-resolved="false"></xref>.

```csharp
public static class ValueObjectProtoModelExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectProtoModelExtensions](AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoModelExtensions.md)

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
protobuf-net adds a member's type to a model only when the type carries a contract attribute, so a value object it was
not told about fails the first contract that holds one: "No serializer defined for type". These methods tell it, at
start-up, before the model serializes anything: <code>RuntimeTypeModel.Default.AddValueObjects(typeof(Sku).Assembly)</code>
serves <code>Serializer.Serialize</code> and protobuf-net.Grpc's default binder.
</p>
<p>
Each value object gets the serializer of <xref href="AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoSerializer%603" data-throw-if-not-resolved="false"></xref> at the
model's <xref href="ProtoBuf.Meta.RuntimeTypeModel.DefaultCompatibilityLevel" data-throw-if-not-resolved="false"></xref>, which protobuf-net lets nobody change once a type is
added, and is named in the <code>.proto</code> export as the scalar protobuf-net names its underlying type at that level,
<code>string</code> or <code>int32</code>, with the <code>import</code> a <code>.bcl.Guid</code> or a <code>.google.protobuf.Timestamp</code> needs.
A value object the model already serializes some way keeps it: one given a serializer type on the model, or by
<code>[ProtoContract(Serializer = …)]</code> on the type, which is named in the export only when the serializer is one of
this package's over a profile it ships and the attribute gives no <code>Name</code>.
</p>
<p>
These methods serve protobuf-net's runtime model, which emits code at run time: under native AOT, a value object names
its serializer with <code>[ProtoContract(Serializer = …)]</code>, and the application serializes through protobuf-net's
compile-time model, a <code>[ProtoModel]</code>.
</p>

## Methods

### AddValueObject<TSelf, TValue\>\(RuntimeTypeModel, bool\) {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoModelExtensions_AddValueObject__2_ProtoBuf_Meta_RuntimeTypeModel_System_Boolean_}

Registers the serializer of one value object, a construction of a generic one or one written by hand included,
without reflection.

```csharp
[RequiresDynamicCode("protobuf-net's runtime model emits code at run time; under native AOT, name the serializer on the value object with [ProtoContract(Serializer = ...)] and serialize through a [ProtoModel].")]
public static RuntimeTypeModel AddValueObject<TSelf, TValue>(this RuntimeTypeModel model, bool trusted = false) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`model` RuntimeTypeModel

The model, <xref href="ProtoBuf.Meta.RuntimeTypeModel.Default" data-throw-if-not-resolved="false"></xref> or the application's own.

`trusted` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

Whether reads skip validation, for bytes the application alone wrote; <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>, the default, reads
through the value object's rules.

#### Returns

 RuntimeTypeModel

<code class="paramref">model</code>, for chaining.

#### Type Parameters

`TSelf` 

Value object type, <code>Reference&lt;PurchaseOrder&gt;</code> for instance.

`TValue` 

Underlying value type.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">model</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [NotSupportedException](https://learn.microsoft.com/dotnet/api/system.notsupportedexception)

protobuf-net has no inbuilt serializer for <code class="typeparamref">TValue</code>.

 [InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

The model already serializes <code class="typeparamref">TSelf</code> through another serializer type: one of the other trust,
one of the application's own, or the one <code>[ProtoContract(Serializer = …)]</code> names on the type.

### AddValueObjects\(RuntimeTypeModel, params Assembly\[\]\) {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoModelExtensions_AddValueObjects_ProtoBuf_Meta_RuntimeTypeModel_System_Reflection_Assembly___}

Registers, strict, a serializer for every value object the registry holds, once the generated registration of the
assemblies given has run.

```csharp
[RequiresUnreferencedCode("Locates the generated registration of each assembly given by its metadata.")]
[RequiresDynamicCode("protobuf-net's runtime model emits code at run time; under native AOT, name the serializer on the value object with [ProtoContract(Serializer = ...)] and serialize through a [ProtoModel].")]
public static RuntimeTypeModel AddValueObjects(this RuntimeTypeModel model, params Assembly[] assemblies)
```

#### Parameters

`model` RuntimeTypeModel

The model, <xref href="ProtoBuf.Meta.RuntimeTypeModel.Default" data-throw-if-not-resolved="false"></xref> or the application's own.

`assemblies` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\[\]

Assemblies declaring value objects, whose generated registration runs first.

#### Returns

 RuntimeTypeModel

<code class="paramref">model</code>, for chaining.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">model</code> or <code class="paramref">assemblies</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### AddValueObjects\(RuntimeTypeModel, bool, params Assembly\[\]\) {#AdCodicem_ValueObjects_ProtobufNet_ValueObjectProtoModelExtensions_AddValueObjects_ProtoBuf_Meta_RuntimeTypeModel_System_Boolean_System_Reflection_Assembly___}

Registers, trusted or strict, a serializer for every value object the registry holds, once the generated
registration of the assemblies given has run.

```csharp
[RequiresUnreferencedCode("Locates the generated registration of each assembly given by its metadata.")]
[RequiresDynamicCode("protobuf-net's runtime model emits code at run time; under native AOT, name the serializer on the value object with [ProtoContract(Serializer = ...)] and serialize through a [ProtoModel].")]
public static RuntimeTypeModel AddValueObjects(this RuntimeTypeModel model, bool trusted, params Assembly[] assemblies)
```

#### Parameters

`model` RuntimeTypeModel

The model, <xref href="ProtoBuf.Meta.RuntimeTypeModel.Default" data-throw-if-not-resolved="false"></xref> or the application's own.

`trusted` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

Whether reads skip validation, for bytes the application alone wrote, a cache or a store among them;
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a> reads through the value objects' rules.

`assemblies` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\[\]

Assemblies declaring value objects, whose generated registration runs first.

#### Returns

 RuntimeTypeModel

<code class="paramref">model</code>, for chaining.

#### Remarks

A construction of a generic value object and a value object written by hand are in the registry only once something
registered them: <xref href="AdCodicem.ValueObjects.ProtobufNet.ValueObjectProtoModelExtensions.AddValueObject%60%602(ProtoBuf.Meta.RuntimeTypeModel%2cSystem.Boolean)" data-throw-if-not-resolved="false"></xref> adds each. A value object over
<xref href="System.Int128" data-throw-if-not-resolved="false"></xref>, <xref href="System.UInt128" data-throw-if-not-resolved="false"></xref> or <xref href="System.DateTimeOffset" data-throw-if-not-resolved="false"></xref>, which protobuf-net has no inbuilt
serializer for, is left out, and protobuf-net's own error names it when a contract holds one.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">model</code> or <code class="paramref">assemblies</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

