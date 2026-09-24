# Class ValueObjectJsonConverterFactory {#AdCodicem_ValueObjects_Json_ValueObjectJsonConverterFactory}

Namespace: [AdCodicem.ValueObjects.Json](AdCodicem.ValueObjects.Json.md)  
Assembly: AdCodicem.ValueObjects.Json.dll  

Supplies the converter of any value object, so that a value object always serializes as its bare underlying
value.

```csharp
public sealed class ValueObjectJsonConverterFactory : JsonConverterFactory
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[JsonConverter](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverter) ← 
[JsonConverterFactory](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonconverterfactory) ← 
[ValueObjectJsonConverterFactory](AdCodicem.ValueObjects.Json.ValueObjectJsonConverterFactory.md)

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
A generated value object already carries its own <code>[JsonConverter]</code> and needs nothing from this factory
when the serializer resolves types by reflection. The factory exists for the two cases the attribute cannot
reach: a <code>JsonSerializerContext</code>, whose generator never sees the attribute, and value objects written
by hand.
</p>
<p>
Register it on the context so the System.Text.Json generator picks it up:

<pre><code class="lang-csharp">[JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
[JsonSerializable(typeof(Payment))]
internal sealed partial class PaymentContext : JsonSerializerContext;</code></pre>

</p>

## Methods

### CanConvert\(Type\) {#AdCodicem_ValueObjects_Json_ValueObjectJsonConverterFactory_CanConvert_System_Type_}

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

### CreateConverter\(Type, JsonSerializerOptions\) {#AdCodicem_ValueObjects_Json_ValueObjectJsonConverterFactory_CreateConverter_System_Type_System_Text_Json_JsonSerializerOptions_}

Creates a converter for a specified type.

```csharp
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only reached for a value object that registered no converter, which never happens for a generated one: those are served from the registry, statically. A hand-written value object combined with trimming or native AOT has to supply its own converter.")]
[UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "Only reached for a value object that registered no converter, which never happens for a generated one: those are served from the registry, statically. A hand-written value object combined with trimming or native AOT has to supply its own converter.")]
[UnconditionalSuppressMessage("Trimming", "IL2071", Justification = "Only reached for a value object that registered no converter, which never happens for a generated one: those are served from the registry, statically. A hand-written value object combined with trimming or native AOT has to supply its own converter.")]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only reached for a value object that registered no converter, which never happens for a generated one: those are served from the registry, statically. A hand-written value object combined with trimming or native AOT has to supply its own converter.")]
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

