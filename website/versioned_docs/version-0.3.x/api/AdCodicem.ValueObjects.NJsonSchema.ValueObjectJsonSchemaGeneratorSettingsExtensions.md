# Class ValueObjectJsonSchemaGeneratorSettingsExtensions {#AdCodicem_ValueObjects_NJsonSchema_ValueObjectJsonSchemaGeneratorSettingsExtensions}

Namespace: [AdCodicem.ValueObjects.NJsonSchema](AdCodicem.ValueObjects.NJsonSchema.md)  
Assembly: AdCodicem.ValueObjects.NJsonSchema.dll  

Wires value object support into the schema settings of NJsonSchema, on which NSwag's OpenAPI documents and
Confluent's JSON serdes for the Schema Registry are built.

```csharp
public static class ValueObjectJsonSchemaGeneratorSettingsExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectJsonSchemaGeneratorSettingsExtensions](AdCodicem.ValueObjects.NJsonSchema.ValueObjectJsonSchemaGeneratorSettingsExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### AddValueObjects<TSettings\>\(TSettings\) {#AdCodicem_ValueObjects_NJsonSchema_ValueObjectJsonSchemaGeneratorSettingsExtensions_AddValueObjects__1___0_}

Describes every value object as its underlying type, with the rules declared on it, in the schemas these settings
generate.

```csharp
public static TSettings AddValueObjects<TSettings>(this TSettings settings) where TSettings : JsonSchemaGeneratorSettings
```

#### Parameters

`settings` TSettings

The settings, NSwag's <code>SchemaSettings</code> or the ones handed to Confluent's serdes.

#### Returns

 TSettings

The same settings, so calls can be chained.

#### Type Parameters

`TSettings` 

The type of the settings: the System.Text.Json or the Newtonsoft.Json ones.

#### Remarks

<p>
NJsonSchema describes a value object by its public properties under System.Text.Json settings, and as a bare
<code>string</code>, through its type converter, under Newtonsoft.Json ones. Each value object gets one definition
instead, a component in an OpenAPI document, describing it as its underlying type with the pattern, bounds,
lengths, example and accepted values declared on it, and every use of it refers to that definition.
</p>
<p>
The schema says what the settings' serializer writes, which the settings are never changed for: the
<code>SerializerOptions</code> of System.Text.Json settings, as they stand when the schema is generated, a value object
they do not convert as a value being left as NJsonSchema describes it; under Newtonsoft.Json settings, the value
<code>ValueObjectConverter</code> writes when the type's or the settings' converter is it, and the text of its type
converter when no converter claims it; a value object written otherwise is left as NJsonSchema describes it. In an
OpenAPI or Swagger document a number is a number, whatever the options let be read from text.
</p>
<p>
A Swagger 2.0 document, which refers to no schema from a parameter, describes every value object in place. Text a
member or a parameter holds to a value object's rules with <code>[ParsesAs]</code> is described as that value object's
text. A type mapper of the settings keeps the application's description of the forms NJsonSchema hands it: the
value object and its nullable type for a mapper of the value object or of its generic definition, the nullable
type alone for a mapper of that type, which only Newtonsoft.Json settings consult. A member NJsonSchema's
<code>[JsonSchemaType]</code> or <code>[JsonSchema]</code> describes keeps the application's description too.
</p>
<p>
Called twice, it changes nothing more.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">settings</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

