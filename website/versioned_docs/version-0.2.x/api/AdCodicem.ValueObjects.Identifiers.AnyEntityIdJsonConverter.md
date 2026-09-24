# Class AnyEntityIdJsonConverter {#AdCodicem_ValueObjects_Identifiers_AnyEntityIdJsonConverter}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

Moves an <xref href="AdCodicem.ValueObjects.Identifiers.AnyEntityId" data-throw-if-not-resolved="false"></xref> across a JSON boundary as the bare identifier text.

```csharp
public sealed class AnyEntityIdJsonConverter : JsonConverter<AnyEntityId>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[JsonConverter](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter) ← 
[JsonConverter<AnyEntityId\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1) ← 
[AnyEntityIdJsonConverter](AdCodicem.ValueObjects.Identifiers.AnyEntityIdJsonConverter.md)

#### Inherited Members

[JsonConverter<AnyEntityId\>.CanConvert\(Type\)](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1.canconvert), 
[JsonConverter<AnyEntityId\>.Read\(ref Utf8JsonReader, Type, JsonSerializerOptions\)](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1.read), 
[JsonConverter<AnyEntityId\>.ReadAsPropertyName\(ref Utf8JsonReader, Type, JsonSerializerOptions\)](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1.readaspropertyname), 
[JsonConverter<AnyEntityId\>.Write\(Utf8JsonWriter, AnyEntityId, JsonSerializerOptions\)](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1.write), 
[JsonConverter<AnyEntityId\>.WriteAsPropertyName\(Utf8JsonWriter, AnyEntityId, JsonSerializerOptions\)](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1.writeaspropertyname), 
[JsonConverter<AnyEntityId\>.HandleNull](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1.handlenull), 
[JsonConverter<AnyEntityId\>.Type](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1.type), 
[JsonConverter.CanConvert\(Type\)](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter.canconvert), 
[JsonConverter.Type](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter.type), 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

Without this the serializer would reflect over the struct and write an object with a value, a prefix and a
type name — which is not what an identifier looks like anywhere else in this library, and is precisely the
wrong shape for the payloads this type exists to serve. Writing the text also makes the default OpenAPI
schema a plain string, which is the representation chosen over a <code>oneOf</code> across every registered
pattern: faithful, and unreadable past a handful of identifier types.

## Methods

### Read\(ref Utf8JsonReader, Type, JsonSerializerOptions\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityIdJsonConverter_Read_System_Text_Json_Utf8JsonReader__System_Type_System_Text_Json_JsonSerializerOptions_}

Reads and converts the JSON to type <xref href="AdCodicem.ValueObjects.Identifiers.AnyEntityId" data-throw-if-not-resolved="false"></xref>.

```csharp
public override AnyEntityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
```

#### Parameters

`reader` [Utf8JsonReader](https://learn.microsoft.com/dotnet/api/system.text.json.utf8jsonreader)

The reader.

`typeToConvert` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The type to convert.

`options` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

An object that specifies serialization options to use.

#### Returns

 [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

The converted value.

### ReadAsPropertyName\(ref Utf8JsonReader, Type, JsonSerializerOptions\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityIdJsonConverter_ReadAsPropertyName_System_Text_Json_Utf8JsonReader__System_Type_System_Text_Json_JsonSerializerOptions_}

Reads a dictionary key from a JSON property name.

```csharp
public override AnyEntityId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
```

#### Parameters

`reader` [Utf8JsonReader](https://learn.microsoft.com/dotnet/api/system.text.json.utf8jsonreader)

The <xref href="System.Text.Json.Utf8JsonReader" data-throw-if-not-resolved="false"></xref> to read from.

`typeToConvert` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The type to convert.

`options` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

The options to use when reading the value.

#### Returns

 [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

The value that was converted.

### Write\(Utf8JsonWriter, AnyEntityId, JsonSerializerOptions\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityIdJsonConverter_Write_System_Text_Json_Utf8JsonWriter_AdCodicem_ValueObjects_Identifiers_AnyEntityId_System_Text_Json_JsonSerializerOptions_}

Writes a specified value as JSON.

```csharp
public override void Write(Utf8JsonWriter writer, AnyEntityId value, JsonSerializerOptions options)
```

#### Parameters

`writer` [Utf8JsonWriter](https://learn.microsoft.com/dotnet/api/system.text.json.utf8jsonwriter)

The writer to write to.

`value` [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

The value to convert to JSON.

`options` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

An object that specifies serialization options to use.

### WriteAsPropertyName\(Utf8JsonWriter, AnyEntityId, JsonSerializerOptions\) {#AdCodicem_ValueObjects_Identifiers_AnyEntityIdJsonConverter_WriteAsPropertyName_System_Text_Json_Utf8JsonWriter_AdCodicem_ValueObjects_Identifiers_AnyEntityId_System_Text_Json_JsonSerializerOptions_}

Writes a dictionary key as a JSON property name.

```csharp
public override void WriteAsPropertyName(Utf8JsonWriter writer, AnyEntityId value, JsonSerializerOptions options)
```

#### Parameters

`writer` [Utf8JsonWriter](https://learn.microsoft.com/dotnet/api/system.text.json.utf8jsonwriter)

The <xref href="System.Text.Json.Utf8JsonWriter" data-throw-if-not-resolved="false"></xref> to write to.

`value` [AnyEntityId](AdCodicem.ValueObjects.Identifiers.AnyEntityId.md)

The value to convert. The value of <xref href="System.Text.Json.Serialization.JsonConverter%601.HandleNull" data-throw-if-not-resolved="false"></xref> determines if the converter handles <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> values.

`options` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

The options to use when writing the value.

