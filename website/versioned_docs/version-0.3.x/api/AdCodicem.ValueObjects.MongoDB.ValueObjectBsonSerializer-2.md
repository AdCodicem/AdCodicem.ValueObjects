# Class ValueObjectBsonSerializer<TSelf, TValue\> {#AdCodicem_ValueObjects_MongoDB_ValueObjectBsonSerializer_2}

Namespace: [AdCodicem.ValueObjects.MongoDB](AdCodicem.ValueObjects.MongoDB.md)  
Assembly: AdCodicem.ValueObjects.MongoDB.dll  

Stores a value object as the bare value the serializer of its underlying type writes, and reads it back through the
value object's rules.

```csharp
public sealed class ValueObjectBsonSerializer<TSelf, TValue> : SerializerBase<TSelf>, IBsonSerializer<TSelf>, IBsonDocumentSerializer, IBsonSerializer, IChildSerializerConfigurable, IHasRepresentationSerializer where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
SerializerBase<TSelf\> ← 
[ValueObjectBsonSerializer<TSelf, TValue\>](AdCodicem.ValueObjects.MongoDB.ValueObjectBsonSerializer\-2.md)

#### Implements

IBsonSerializer<TSelf\>, 
IBsonDocumentSerializer, 
IBsonSerializer, 
IChildSerializerConfigurable, 
IHasRepresentationSerializer

#### Inherited Members

SerializerBase<TSelf\>.Deserialize\(BsonDeserializationContext, BsonDeserializationArgs\), 
SerializerBase<TSelf\>.Equals\(object\), 
SerializerBase<TSelf\>.GetHashCode\(\), 
SerializerBase<TSelf\>.Serialize\(BsonSerializationContext, BsonSerializationArgs, TSelf\), 
SerializerBase<TSelf\>.ValueType, 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The value goes through the serializer of <code class="typeparamref">TValue</code> the serializer is built over, which
<xref href="AdCodicem.ValueObjects.MongoDB.ValueObjectBson" data-throw-if-not-resolved="false"></xref> takes from the registry, so a value object writes exactly what the primitive it replaces
writes, under the application's conventions for <xref href="System.Guid" data-throw-if-not-resolved="false"></xref>, <xref href="System.DateTime" data-throw-if-not-resolved="false"></xref> or <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>:
documents and indexes written before the type replaced the primitive stay readable. A serialization option on a
member, <code>[BsonRepresentation(BsonType.String)]</code>, <code>[BsonGuidRepresentation]</code> or <code>[BsonDateTimeOptions]</code>
among them, reaches that serializer as it would on the primitive, through <xref href="MongoDB.Bson.Serialization.IChildSerializerConfigurable" data-throw-if-not-resolved="false"></xref>,
on a <code>TSelf?</code> member and on the elements of a collection too.
</p>
<p>
A read is strict unless the serializer is trusted: the value read goes through <code>TryCreate</code>, which normalizes it,
and one the value object refuses throws a <xref href="System.FormatException" data-throw-if-not-resolved="false"></xref> naming the type and the rule, never the value,
with the rule's code in <xref href="System.Exception.Data" data-throw-if-not-resolved="false"></xref> under <xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>, where
<xref href="AdCodicem.ValueObjects.ValueObjectErrors.TryGetCode(System.Exception%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref> finds it through the exception the driver wraps it in. A trusted serializer
reads through <code>CreateUnchecked</code>, for a collection the application alone writes. Either way, a BSON
<code>null</code> is refused with <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref>, since a value object cannot hold one: a
member that can be missing is a <code>TSelf?</code>, which the driver reads as <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> without asking this
serializer, and a value the serializer of <code class="typeparamref">TValue</code> cannot read, a BSON value of another type,
one out of its range, text it does not parse, with <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable" data-throw-if-not-resolved="false"></xref>.
</p>
<p>
A write refuses, with a <xref href="MongoDB.Bson.BsonSerializationException" data-throw-if-not-resolved="false"></xref> carrying the rule's code the same way, an
uninitialized instance, equal to the default, whose value the value object rejects: validation tells a valid zero
from a refused one. Any other instance went through <code>Create</code>, or was read by a trusted serializer, and is
written as it is, without being validated again. The driver serializes a query constant, in LINQ or
<code>Builders</code>, and the value of an update through the same serializer, so a default instance the type rejects is
refused there too. An update through <code>Value</code>, <code>Set(x =&gt; x.Page.Value, 0)</code>, hands its value to the
serializer of <code class="typeparamref">TValue</code> alone, as a filter through <code>Value</code> does, and writes it unchecked.
</p>
<p>
LINQ translates <code>x.Iban.Value</code> as it translates <code>x.Iban</code>: <xref href="AdCodicem.ValueObjects.MongoDB.ValueObjectBsonSerializer%602.TryGetMemberSerializationInfo(System.String%2cMongoDB.Bson.Serialization.BsonSerializationInfo%40)" data-throw-if-not-resolved="false"></xref>
answers <code>Value</code> with the field itself and the serializer of <code class="typeparamref">TValue</code>, so that a method of the
underlying type, <code>StartsWith</code>, <code>Length</code>, <code>ToLower</code>, an arithmetic operator, translates as it would on
the primitive.
</p>

## Constructors

### ValueObjectBsonSerializer\(IBsonSerializer<TValue\>, bool\) {#AdCodicem_ValueObjects_MongoDB_ValueObjectBsonSerializer_2__ctor_MongoDB_Bson_Serialization_IBsonSerializer__1__System_Boolean_}

Initializes the serializer over the serializer of the underlying value.

```csharp
public ValueObjectBsonSerializer(IBsonSerializer<TValue> valueSerializer, bool trusted = false)
```

#### Parameters

`valueSerializer` IBsonSerializer<TValue\>

Serializer of <code class="typeparamref">TValue</code>, which writes and reads the value.

`trusted` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

Whether a read skips validation, for a collection the application alone writes; <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>, the
default, reads through the value object's rules.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">valueSerializer</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

## Properties

### Representation {#AdCodicem_ValueObjects_MongoDB_ValueObjectBsonSerializer_2_Representation}

Gets the BSON type the serializer of the underlying value writes, or <xref href="MongoDB.Bson.BsonType.Undefined" data-throw-if-not-resolved="false"></xref> when that
serializer does not say.

```csharp
public BsonType Representation { get; }
```

#### Property Value

 BsonType

#### Remarks

LINQ reads it to translate a dictionary keyed by the value object, <code>x.Totals[currency]</code> and
<code>ContainsKey</code>, which it accepts only for a key written as text.

## Methods

### Deserialize\(BsonDeserializationContext, BsonDeserializationArgs\) {#AdCodicem_ValueObjects_MongoDB_ValueObjectBsonSerializer_2_Deserialize_MongoDB_Bson_Serialization_BsonDeserializationContext_MongoDB_Bson_Serialization_BsonDeserializationArgs_}

Deserializes a value.

```csharp
public override TSelf Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
```

#### Parameters

`context` BsonDeserializationContext

The deserialization context.

`args` BsonDeserializationArgs

The deserialization args.

#### Returns

 TSelf

A deserialized value.

#### Exceptions

 [FormatException](https://learn.microsoft.com/dotnet/api/system.formatexception)

The value read is a BSON <code>null</code>, which a value object cannot hold, a value the serializer of the underlying
value cannot read, or, unless the serializer is trusted, a value the value object refuses.

 BsonSerializationException

The value object is over <xref href="System.Guid" data-throw-if-not-resolved="false"></xref>, the value read is binary, and the serializer of <xref href="System.Guid" data-throw-if-not-resolved="false"></xref>
has no representation to read it with.

### Equals\(object?\) {#AdCodicem_ValueObjects_MongoDB_ValueObjectBsonSerializer_2_Equals_System_Object_}

Determines whether the specified object is equal to the current object.

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

The object to compare with the current object.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> if the specified object  is equal to the current object; otherwise, <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>.

#### Remarks

Equal for the same trust over an equal serializer of the underlying value, which is how
<xref href="MongoDB.Bson.Serialization.BsonSerializer.TryRegisterSerializer(System.Type%2cMongoDB.Bson.Serialization.IBsonSerializer)" data-throw-if-not-resolved="false"></xref> tells a second registration of the same
serializer, which it ignores, from a different one, which it refuses.

### GetHashCode\(\) {#AdCodicem_ValueObjects_MongoDB_ValueObjectBsonSerializer_2_GetHashCode}

Serves as the default hash function.

```csharp
public override int GetHashCode()
```

#### Returns

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

A hash code for the current object.

#### Remarks

Read off the trust alone: the driver's serializers, the serializer of the underlying value among them, all hash
to the same value.

### Serialize\(BsonSerializationContext, BsonSerializationArgs, TSelf\) {#AdCodicem_ValueObjects_MongoDB_ValueObjectBsonSerializer_2_Serialize_MongoDB_Bson_Serialization_BsonSerializationContext_MongoDB_Bson_Serialization_BsonSerializationArgs__0_}

Serializes a value.

```csharp
public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TSelf value)
```

#### Parameters

`context` BsonSerializationContext

The serialization context.

`args` BsonSerializationArgs

The serialization args.

`value` TSelf

The value.

#### Exceptions

 BsonSerializationException

<code class="paramref">value</code> equals <code>default(TSelf)</code> and holds a value the value object rejects, or the value
object is over <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> and the serializer of <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> has no representation.

### TryGetMemberSerializationInfo\(string, out BsonSerializationInfo\) {#AdCodicem_ValueObjects_MongoDB_ValueObjectBsonSerializer_2_TryGetMemberSerializationInfo_System_String_MongoDB_Bson_Serialization_BsonSerializationInfo__}

Answers <code>Value</code> with the field itself, so that LINQ translates <code>x.Iban.Value</code> as it does <code>x.Iban</code>.

```csharp
public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo serializationInfo)
```

#### Parameters

`memberName` [string](https://learn.microsoft.com/dotnet/api/system.string)

The member a query names.

`serializationInfo` BsonSerializationInfo

For <code>Value</code>, an empty element path, which leaves the query on the value object's own field, with the
serializer of the underlying value; <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> otherwise.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> for <code>Value</code>, the one member stored.

