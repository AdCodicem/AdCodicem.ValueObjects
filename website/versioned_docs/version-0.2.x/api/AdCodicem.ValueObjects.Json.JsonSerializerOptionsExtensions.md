# Class JsonSerializerOptionsExtensions {#AdCodicem_ValueObjects_Json_JsonSerializerOptionsExtensions}

Namespace: [AdCodicem.ValueObjects.Json](AdCodicem.ValueObjects.Json.md)  
Assembly: AdCodicem.ValueObjects.Json.dll  

Wires value object support into <xref href="System.Text.Json.JsonSerializerOptions" data-throw-if-not-resolved="false"></xref>.

```csharp
public static class JsonSerializerOptionsExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[JsonSerializerOptionsExtensions](AdCodicem.ValueObjects.Json.JsonSerializerOptionsExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### AddValueObjects\(JsonSerializerOptions\) {#AdCodicem_ValueObjects_Json_JsonSerializerOptionsExtensions_AddValueObjects_System_Text_Json_JsonSerializerOptions_}

Adds the value object converter factory to the options.

```csharp
public static JsonSerializerOptions AddValueObjects(this JsonSerializerOptions options)
```

#### Parameters

`options` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

Options to configure.

#### Returns

 [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

The same options, so calls can be chained.

#### Remarks

Reflection-based serialization already honours the converter each generated value object carries, so
this is only needed for value objects written by hand, or to make the behaviour explicit at the
composition root. Adding it twice is harmless but wasteful, so an existing registration is left alone.

