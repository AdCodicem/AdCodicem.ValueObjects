# Class ValueObjectConverter {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectConverter}

Namespace: [AdCodicem.ValueObjects.NewtonsoftJson](AdCodicem.ValueObjects.NewtonsoftJson.md)  
Assembly: AdCodicem.ValueObjects.NewtonsoftJson.dll  

Reads and writes any value object as its bare underlying value.

```csharp
public sealed class ValueObjectConverter : JsonConverter
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
JsonConverter ← 
[ValueObjectConverter](AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectConverter.md)

#### Inherited Members

JsonConverter.WriteJson\(JsonWriter, object?, JsonSerializer\), 
JsonConverter.ReadJson\(JsonReader, Type, object?, JsonSerializer\), 
JsonConverter.CanConvert\(Type\), 
JsonConverter.CanRead, 
JsonConverter.CanWrite, 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

One converter covers every value object, because the work is delegated to the runtime descriptor rather than
to a type-specific implementation. Newtonsoft.Json resolves converters by reflection anyway, so there is
nothing to gain from a generic converter per type here.

## Methods

### CanConvert\(Type\) {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectConverter_CanConvert_System_Type_}

Determines whether this instance can convert the specified object type.

```csharp
public override bool CanConvert(Type objectType)
```

#### Parameters

`objectType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Type of the object.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<code>true</code> if this instance can convert the specified object type; otherwise, <code>false</code>.

### ReadJson\(JsonReader, Type, object?, JsonSerializer\) {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectConverter_ReadJson_Newtonsoft_Json_JsonReader_System_Type_System_Object_Newtonsoft_Json_JsonSerializer_}

Reads the JSON representation of the object.

```csharp
public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
```

#### Parameters

`reader` JsonReader

The <xref href="Newtonsoft.Json.JsonReader" data-throw-if-not-resolved="false"></xref> to read from.

`objectType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Type of the object.

`existingValue` [object](https://learn.microsoft.com/dotnet/api/system.object)?

The existing value of object being read.

`serializer` JsonSerializer

The calling serializer.

#### Returns

 [object](https://learn.microsoft.com/dotnet/api/system.object)?

The object value.

### WriteJson\(JsonWriter, object?, JsonSerializer\) {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectConverter_WriteJson_Newtonsoft_Json_JsonWriter_System_Object_Newtonsoft_Json_JsonSerializer_}

Writes the JSON representation of the object.

```csharp
public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
```

#### Parameters

`writer` JsonWriter

The <xref href="Newtonsoft.Json.JsonWriter" data-throw-if-not-resolved="false"></xref> to write to.

`value` [object](https://learn.microsoft.com/dotnet/api/system.object)?

The value.

`serializer` JsonSerializer

The calling serializer.

