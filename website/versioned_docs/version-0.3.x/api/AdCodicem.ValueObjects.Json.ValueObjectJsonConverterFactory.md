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
by hand. For the first, it hands out the converter the generator registered with the value object's descriptor,
whatever the assembly declaring the value object references. For a construction of a generic value object, whose
registration registers its generic definition alone, the registry closes the generated converter over the
construction, by reflection, the first time it is asked for it.
</p>
<p>
A value object written by hand carries no converter, unless it was registered with one, and gets the general-purpose
<xref href="AdCodicem.ValueObjects.Json.ValueObjectJsonConverter%602" data-throw-if-not-resolved="false"></xref>, which the factory closes over it through the type arguments its
descriptor hands back (<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.Accept%60%601(AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>), at compile
time rather than by reflection. Under native AOT, register it,
<code>ValueObjectRegistry.Register&lt;Link, Uri&gt;(Link.Schema)</code>, and list it in the context: one nothing registered
is described by reflection where the runtime supports dynamic code, and refused with a
<xref href="System.NotSupportedException" data-throw-if-not-resolved="false"></xref> naming that registration where it does not.
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

#### Remarks

Claims a value object only, never <xref href="System.Nullable%601" data-throw-if-not-resolved="false"></xref> over one: a converter in the options outranks the
serializer's own handling of nullable types, which wraps the converter of the value object itself once this
one declines.

### CreateConverter\(Type, JsonSerializerOptions\) {#AdCodicem_ValueObjects_Json_ValueObjectJsonConverterFactory_CreateConverter_System_Type_System_Text_Json_JsonSerializerOptions_}

Creates a converter for a specified type.

```csharp
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ValueObjectRegistry.TryResolve reflects only for a value object nothing registered: one written by hand, or a construction of a generic one. A generated value object registers itself, and its converter, statically. Without dynamic code, as under native AOT, it is not called. Trimmed, register the others through ValueObjectRegistry.Register: a construction with its generated converter, a value object written by hand with none, over which the general-purpose converter is then closed at compile time, through its descriptor.")]
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

The type is a value object nothing registered, and the runtime does not support the dynamic code describing it by
reflection takes, as under native AOT.

