# Class ValueObjectJsonConverter<TSelf, TValue\> {#AdCodicem_ValueObjects_Json_ValueObjectJsonConverter_2}

Namespace: [AdCodicem.ValueObjects.Json](AdCodicem.ValueObjects.Json.md)  
Assembly: AdCodicem.ValueObjects.Json.dll  

Serializes a value object as its bare underlying value, delegating the underlying value to
System.Text.Json itself.

```csharp
public sealed class ValueObjectJsonConverter<TSelf, TValue> : JsonConverter<TSelf> where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[JsonConverter](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter) ← 
[JsonConverter<TSelf\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1) ← 
[ValueObjectJsonConverter<TSelf, TValue\>](AdCodicem.ValueObjects.Json.ValueObjectJsonConverter\-2.md)

#### Inherited Members

[JsonConverter<TSelf\>.CanConvert\(Type\)](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1.canconvert), 
[JsonConverter<TSelf\>.Read\(ref Utf8JsonReader, Type, JsonSerializerOptions\)](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1.read), 
[JsonConverter<TSelf\>.ReadAsPropertyName\(ref Utf8JsonReader, Type, JsonSerializerOptions\)](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1.readaspropertyname), 
[JsonConverter<TSelf\>.Write\(Utf8JsonWriter, TSelf, JsonSerializerOptions\)](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1.write), 
[JsonConverter<TSelf\>.WriteAsPropertyName\(Utf8JsonWriter, TSelf, JsonSerializerOptions\)](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1.writeaspropertyname), 
[JsonConverter<TSelf\>.HandleNull](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1.handlenull), 
[JsonConverter<TSelf\>.Type](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1.type), 
[JsonConverter.CanConvert\(Type\)](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter.canconvert), 
[JsonConverter.Type](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter.type), 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
This is the general-purpose converter, used for value objects written by hand. A generated value object
carries its own converter, which writes the value directly instead of going back through the serializer, and
is the one the factory hands out whenever it is available.
</p>
<p>
The underlying value is read and written through the contract the options hold for <code class="typeparamref">TValue</code>
(<xref href="System.Text.Json.JsonSerializerOptions.GetTypeInfo(System.Type)" data-throw-if-not-resolved="false"></xref>), so the converter needs neither reflection nor dynamic code
of its own: a source-generated context listing the value object holds that contract too, since the value object
exposes its value as a property. Called directly, it takes options, never <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>, which it refuses
with an <xref href="System.ArgumentNullException" data-throw-if-not-resolved="false"></xref>; options no serializer has used and that set no resolver are given the
serializer's default one and locked, as serializing through them would lock them, so that setting one of their
properties afterwards throws an <xref href="System.InvalidOperationException" data-throw-if-not-resolved="false"></xref>, whichever member was called.
</p>
<p>
A value or a key the value object rejects, and a value to write that it rejects, are refused with a
<xref href="AdCodicem.ValueObjects.ValueObjectJsonException" data-throw-if-not-resolved="false"></xref> carrying the code of the rule, as the generated converter refuses them. The
underlying value itself is read by System.Text.Json, whose own exception, for a token that is not one of
<code class="typeparamref">TValue</code>, carries no code.
</p>

## Methods

### Read\(ref Utf8JsonReader, Type, JsonSerializerOptions\) {#AdCodicem_ValueObjects_Json_ValueObjectJsonConverter_2_Read_System_Text_Json_Utf8JsonReader__System_Type_System_Text_Json_JsonSerializerOptions_}

Reads and converts the JSON to type <code class="typeparamref">T</code>.

```csharp
public override TSelf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
```

#### Parameters

`reader` [Utf8JsonReader](https://learn.microsoft.com/dotnet/api/system.text.json.utf8jsonreader)

The reader.

`typeToConvert` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The type to convert.

`options` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

An object that specifies serialization options to use.

#### Returns

 TSelf

The converted value.

### ReadAsPropertyName\(ref Utf8JsonReader, Type, JsonSerializerOptions\) {#AdCodicem_ValueObjects_Json_ValueObjectJsonConverter_2_ReadAsPropertyName_System_Text_Json_Utf8JsonReader__System_Type_System_Text_Json_JsonSerializerOptions_}

Reads a dictionary key from a JSON property name.

```csharp
public override TSelf ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
```

#### Parameters

`reader` [Utf8JsonReader](https://learn.microsoft.com/dotnet/api/system.text.json.utf8jsonreader)

The <xref href="System.Text.Json.Utf8JsonReader" data-throw-if-not-resolved="false"></xref> to read from.

`typeToConvert` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The type to convert.

`options` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

The options to use when reading the value.

#### Returns

 TSelf

The value that was converted.

#### Remarks

The key is read as System.Text.Json reads a key of <code class="typeparamref">TValue</code>, then validated through
<code>TryCreate</code>, as a value is: the reverse of <xref href="AdCodicem.ValueObjects.Json.ValueObjectJsonConverter%602.WriteAsPropertyName(System.Text.Json.Utf8JsonWriter%2c%600%2cSystem.Text.Json.JsonSerializerOptions)" data-throw-if-not-resolved="false"></xref>, whatever text the value
object's own parser reads. A key that is not one of <code class="typeparamref">TValue</code> is refused as it is in a
dictionary of its own, and a key the value object rejects with a <xref href="AdCodicem.ValueObjects.ValueObjectJsonException" data-throw-if-not-resolved="false"></xref> naming the
rule and carrying its code.

### Write\(Utf8JsonWriter, TSelf, JsonSerializerOptions\) {#AdCodicem_ValueObjects_Json_ValueObjectJsonConverter_2_Write_System_Text_Json_Utf8JsonWriter__0_System_Text_Json_JsonSerializerOptions_}

Writes a specified value as JSON.

```csharp
public override void Write(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
```

#### Parameters

`writer` [Utf8JsonWriter](https://learn.microsoft.com/dotnet/api/system.text.json.utf8jsonwriter)

The writer to write to.

`value` TSelf

The value to convert to JSON.

`options` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

An object that specifies serialization options to use.

#### Remarks

An instance equal to the default whose value the value object rejects is refused with a
<xref href="AdCodicem.ValueObjects.ValueObjectJsonException" data-throw-if-not-resolved="false"></xref> naming the rule and carrying its code, as the converter the generator
emits refuses it, rather than written for a reader to refuse.

### WriteAsPropertyName\(Utf8JsonWriter, TSelf, JsonSerializerOptions\) {#AdCodicem_ValueObjects_Json_ValueObjectJsonConverter_2_WriteAsPropertyName_System_Text_Json_Utf8JsonWriter__0_System_Text_Json_JsonSerializerOptions_}

Writes a dictionary key as a JSON property name.

```csharp
public override void WriteAsPropertyName(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
```

#### Parameters

`writer` [Utf8JsonWriter](https://learn.microsoft.com/dotnet/api/system.text.json.utf8jsonwriter)

The <xref href="System.Text.Json.Utf8JsonWriter" data-throw-if-not-resolved="false"></xref> to write to.

`value` TSelf

The value to convert. The value of <xref href="System.Text.Json.Serialization.JsonConverter%601.HandleNull" data-throw-if-not-resolved="false"></xref> determines if the converter handles <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> values.

`options` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

The options to use when writing the value.

#### Remarks

The key is the underlying value, written as System.Text.Json writes a key of <code class="typeparamref">TValue</code>:
the form the value itself travels in. The value object's own formatting, which may print something its
parser does not read, never reaches the wire. An underlying type System.Text.Json cannot write as a key is
refused as it is in a dictionary of its own, with a <xref href="System.NotSupportedException" data-throw-if-not-resolved="false"></xref>, and a key the value
object rejects, as <xref href="AdCodicem.ValueObjects.Json.ValueObjectJsonConverter%602.Write(System.Text.Json.Utf8JsonWriter%2c%600%2cSystem.Text.Json.JsonSerializerOptions)" data-throw-if-not-resolved="false"></xref> refuses a value, with a <xref href="AdCodicem.ValueObjects.ValueObjectJsonException" data-throw-if-not-resolved="false"></xref> naming the
rule.

