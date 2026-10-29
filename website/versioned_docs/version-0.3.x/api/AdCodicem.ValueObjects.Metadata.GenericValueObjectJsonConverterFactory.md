# Class GenericValueObjectJsonConverterFactory {#AdCodicem_ValueObjects_Metadata_GenericValueObjectJsonConverterFactory}

Namespace: [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Supplies the System.Text.Json converter of each construction of a generic value object.

```csharp
public sealed class GenericValueObjectJsonConverterFactory : JsonConverterFactory
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[JsonConverter](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter) ← 
[JsonConverterFactory](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverterfactory) ← 
[GenericValueObjectJsonConverterFactory](AdCodicem.ValueObjects.Metadata.GenericValueObjectJsonConverterFactory.md)

#### Inherited Members

[JsonConverterFactory.CreateConverter\(Type, JsonSerializerOptions\)](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverterfactory.createconverter), 
[JsonConverterFactory.Type](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverterfactory.type), 
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
The generator puts this factory in the <code>[JsonConverter]</code> attribute of a generic value object, where a value
object that is not generic names its own converter. An attribute cannot name a type through a type parameter, and
System.Text.Json does not close an open generic converter over the type it converts, so the attribute names this
factory, which hands out the converter the generator wrote, closed over the construction it is asked for.
</p>
<p>
It is reached through the attribute, which only the serializer resolving types by reflection reads. A
source-generated serializer context reaches the value object through <code>ValueObjectJsonConverterFactory</code>
instead, which hands out the same converter.
</p>

## Methods

### CanConvert\(Type\) {#AdCodicem_ValueObjects_Metadata_GenericValueObjectJsonConverterFactory_CanConvert_System_Type_}

When overridden in a derived class, determines whether the converter instance can convert the specified object type.

```csharp
public override bool CanConvert(Type typeToConvert)
```

#### Parameters

`typeToConvert` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The type of the object to check whether it can be converted by this converter instance.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> if the instance can convert the specified object type; otherwise, <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>.

### CreateConverter\(Type, JsonSerializerOptions\) {#AdCodicem_ValueObjects_Metadata_GenericValueObjectJsonConverterFactory_CreateConverter_System_Type_System_Text_Json_JsonSerializerOptions_}

Creates a converter for a specified type.

```csharp
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Reached through the [JsonConverter] attribute of a generic value object, which only the serializer resolving types by reflection reads, and which the source generator never sees, since another generator writes it.")]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reached through the [JsonConverter] attribute of a generic value object, which only the serializer resolving types by reflection reads, and which the source generator never sees, since another generator writes it.")]
public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
```

#### Parameters

`typeToConvert` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The type handled by the converter.

`options` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

The serialization options to use.

#### Returns

 [JsonConverter](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter)?

A converter for which <code class="typeparamref">T</code> is compatible with <code class="paramref">typeToConvert</code>.

#### Exceptions

 [NotSupportedException](https://learn.microsoft.com/dotnet/api/system.notsupportedexception)

The construction carries no generated converter.

