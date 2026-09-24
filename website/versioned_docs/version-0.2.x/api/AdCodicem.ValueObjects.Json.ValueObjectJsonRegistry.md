# Class ValueObjectJsonRegistry {#AdCodicem_ValueObjects_Json_ValueObjectJsonRegistry}

Namespace: [AdCodicem.ValueObjects.Json](AdCodicem.ValueObjects.Json.md)  
Assembly: AdCodicem.ValueObjects.Json.dll  

Directory of the converters generated for the value objects of the loaded assemblies.

```csharp
public static class ValueObjectJsonRegistry
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectJsonRegistry](AdCodicem.ValueObjects.Json.ValueObjectJsonRegistry.md)

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
A source generator only ever sees the original compilation, never the output of another generator. The
<code>[JsonConverter]</code> attribute placed on a generated value object is therefore invisible to the
System.Text.Json generator, and a <code>JsonSerializerContext</code> would otherwise fall back to serializing the
value object as an object with properties.
</p>
<p>
This registry closes that gap. The value object generator emits a registration for every value object when
this package is referenced, and <xref href="AdCodicem.ValueObjects.Json.ValueObjectJsonConverterFactory" data-throw-if-not-resolved="false"></xref> — a hand-written type the
System.Text.Json generator can see — hands the right converter back at run time. The lookup is a dictionary
hit, so nothing here needs reflection or dynamic code.
</p>

## Properties

### Count {#AdCodicem_ValueObjects_Json_ValueObjectJsonRegistry_Count}

Gets the number of registered converters.

```csharp
public static int Count { get; }
```

#### Property Value

 [int](https://learn.microsoft.com/dotnet/api/system.int32)

## Methods

### Register<TSelf\>\(JsonConverter<TSelf\>\) {#AdCodicem_ValueObjects_Json_ValueObjectJsonRegistry_Register__1_System_Text_Json_Serialization_JsonConverter___0__}

Registers the converter of a value object.

```csharp
public static void Register<TSelf>(JsonConverter<TSelf> converter) where TSelf : struct
```

#### Parameters

`converter` [JsonConverter](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter\-1)<TSelf\>

Converter to register.

#### Type Parameters

`TSelf` 

Value object type.

### TryGet\(Type, out JsonConverter?\) {#AdCodicem_ValueObjects_Json_ValueObjectJsonRegistry_TryGet_System_Type_System_Text_Json_Serialization_JsonConverter__}

Looks up the converter registered for a value object type.

```csharp
public static bool TryGet(Type type, out JsonConverter? converter)
```

#### Parameters

`type` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Value object type.

`converter` [JsonConverter](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter)?

The registered converter.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when a converter is registered.

