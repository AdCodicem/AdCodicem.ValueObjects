# Class ValueObjectJsonConverter<TSelf, TValue\> {#AdCodicem_ValueObjects_Json_ValueObjectJsonConverter_2}

Namespace: [AdCodicem.ValueObjects.Json](AdCodicem.ValueObjects.Json.md)  
Assembly: AdCodicem.ValueObjects.Json.dll  

Serializes a value object as its bare underlying value, delegating the underlying value to
System.Text.Json itself.

```csharp
[RequiresUnreferencedCode("Delegating the underlying value to the serializer needs its metadata, which trimming may remove. Generated value objects carry their own converter and do not go through this one.")]
[RequiresDynamicCode("Delegating the underlying value to the serializer may need run-time code generation. Generated value objects carry their own converter and do not go through this one.")]
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

This is the general-purpose converter, used for value objects written by hand. A generated value object
carries its own converter, which writes the value directly instead of going back through the serializer, and
is the one the factory hands out whenever it is available.

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

