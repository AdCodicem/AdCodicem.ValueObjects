# Class ValueObjectFormatter<TSelf, TValue\> {#AdCodicem_ValueObjects_MessagePack_ValueObjectFormatter_2}

Namespace: [AdCodicem.ValueObjects.MessagePack](AdCodicem.ValueObjects.MessagePack.md)  
Assembly: AdCodicem.ValueObjects.MessagePack.dll  

Writes a value object as the bare value the options' formatter of its underlying type writes, and reads it back
through the value object's rules.

```csharp
public sealed class ValueObjectFormatter<TSelf, TValue> : IMessagePackFormatter<TSelf> where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectFormatter<TSelf, TValue\>](AdCodicem.ValueObjects.MessagePack.ValueObjectFormatter\-2.md)

#### Implements

IMessagePackFormatter<TSelf\>

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The value goes through the formatter the options' resolver holds for <code class="typeparamref">TValue</code>, so a value object
writes exactly what the primitive it replaces writes under the same options: a <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> as text under
<code>StandardResolver</code> and as 16 bytes under <code>NativeGuidResolver</code>, a <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref> or a
<xref href="System.DateTime" data-throw-if-not-resolved="false"></xref> as the <code>Native*</code> resolvers placed in the chain write them. A formatting hook of the value
object never reaches the wire.
</p>
<p>
A read is strict unless the formatter is trusted: the value read goes through <code>TryCreate</code>, which normalizes it,
and one the value object refuses throws a <xref href="MessagePack.MessagePackSerializationException" data-throw-if-not-resolved="false"></xref> naming the type and the
rule, never the value, with the rule's code in <xref href="System.Exception.Data" data-throw-if-not-resolved="false"></xref> under
<xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>, where <xref href="AdCodicem.ValueObjects.ValueObjectErrors.TryGetCode(System.Exception%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref> finds it through the
exception MessagePack wraps it in. A trusted formatter reads through <code>CreateUnchecked</code>, for bytes the
application alone wrote, a cache among them. Either way, a <code>nil</code> is refused with
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref>, since a value object cannot hold one: a member that can be missing is a
<code>TSelf?</code>, which MessagePack's own <code>NullableFormatter</code> reads as <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> without asking this
formatter; and a value the formatter of <code class="typeparamref">TValue</code> cannot read, a value of another MessagePack
type, one beyond the range of <code class="typeparamref">TValue</code>, text it cannot parse, truncated bytes, with
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable" data-throw-if-not-resolved="false"></xref>.
</p>
<p>
A write refuses, with a <xref href="MessagePack.MessagePackSerializationException" data-throw-if-not-resolved="false"></xref> carrying the rule's code the same way, an
uninitialized instance, equal to the default, whose value the value object rejects: validation tells a valid zero
from a refused one. Any other instance went through <code>Create</code>, or was read by a trusted formatter, and is written
as it is, without being validated again.
</p>

## Constructors

### ValueObjectFormatter\(bool\) {#AdCodicem_ValueObjects_MessagePack_ValueObjectFormatter_2__ctor_System_Boolean_}

Initializes the formatter.

```csharp
public ValueObjectFormatter(bool trusted = false)
```

#### Parameters

`trusted` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

Whether a read skips validation, for bytes the application alone wrote; <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>, the default,
reads through the value object's rules.

## Methods

### Deserialize\(ref MessagePackReader, MessagePackSerializerOptions\) {#AdCodicem_ValueObjects_MessagePack_ValueObjectFormatter_2_Deserialize_MessagePack_MessagePackReader__MessagePack_MessagePackSerializerOptions_}

Deserializes a value.

```csharp
public TSelf Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
```

#### Parameters

`reader` MessagePackReader

The reader to deserialize from.

`options` MessagePackSerializerOptions

The serialization settings to use, including the resolver to use to obtain formatters for types that make up the composite type <code class="typeparamref">T</code>.

#### Returns

 TSelf

The deserialized value.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">options</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 MessagePackSerializationException

The value read is a <code>nil</code>, which a value object cannot hold, a value the formatter of the underlying value
cannot read, or, unless the formatter is trusted, a value the value object refuses.

 FormatterNotRegisteredException

The options' resolver holds no formatter for <code class="typeparamref">TValue</code>.

### Serialize\(ref MessagePackWriter, TSelf, MessagePackSerializerOptions\) {#AdCodicem_ValueObjects_MessagePack_ValueObjectFormatter_2_Serialize_MessagePack_MessagePackWriter___0_MessagePack_MessagePackSerializerOptions_}

Serializes a value.

```csharp
public void Serialize(ref MessagePackWriter writer, TSelf value, MessagePackSerializerOptions options)
```

#### Parameters

`writer` MessagePackWriter

The writer to use when serializing the value.

`value` TSelf

The value to be serialized.

`options` MessagePackSerializerOptions

The serialization settings to use, including the resolver to use to obtain formatters for types that make up the composite type <code class="typeparamref">T</code>.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">options</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 MessagePackSerializationException

<code class="paramref">value</code> equals <code>default(TSelf)</code> and holds a value the value object rejects.

 FormatterNotRegisteredException

The options' resolver holds no formatter for <code class="typeparamref">TValue</code>.

