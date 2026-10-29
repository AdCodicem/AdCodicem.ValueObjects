# Class ValueObjectJsonSchema {#AdCodicem_ValueObjects_Json_ValueObjectJsonSchema}

Namespace: [AdCodicem.ValueObjects.Json](AdCodicem.ValueObjects.Json.md)  
Assembly: AdCodicem.ValueObjects.Json.dll  

Describes a value object in a JSON Schema exported by System.Text.Json as its underlying value, carrying the rules
declared on it.

```csharp
public static class ValueObjectJsonSchema
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectJsonSchema](AdCodicem.ValueObjects.Json.ValueObjectJsonSchema.md)

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
<xref href="System.Text.Json.Schema.JsonSchemaExporter" data-throw-if-not-resolved="false"></xref> builds a schema from <xref href="System.Text.Json.Serialization.Metadata.JsonTypeInfo" data-throw-if-not-resolved="false"></xref>, and a type serialized by a converter
of its own has none to read: it describes every value object as <code>true</code>, the schema that accepts anything, leaves
<code>items</code> out of a collection of them, and loses the <code>null</code> a nullable one accepts. Plugged into
<xref href="System.Text.Json.Schema.JsonSchemaExporterOptions.TransformSchemaNode" data-throw-if-not-resolved="false"></xref>, this fills each of them in from the
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectSchema" data-throw-if-not-resolved="false"></xref> of the value object, the rules its <code>Validate</code> enforces:
</p>

<pre><code class="lang-csharp">var schema = JsonSchemaExporter.GetJsonSchemaAsNode(
    AppJsonContext.Default.Options,
    typeof(Order),
    new JsonSchemaExporterOptions { TransformSchemaNode = ValueObjectJsonSchema.TransformSchemaNode });</code></pre>

<p>
The underlying type gives <code>type</code>, and <code>"null"</code> joins it, and the <code>enum</code>, for a nullable value object.
<code>MinLength</code> and <code>MaxLength</code> give <code>minLength</code> and <code>maxLength</code>, one character both for a
<xref href="System.Char" data-throw-if-not-resolved="false"></xref>, a pattern <code>pattern</code>, and a bound <code>minimum</code> or <code>maximum</code> on a number; on a value
written as a string, a date, a time or a character, which those keywords cannot bound, a sentence of the description
states it. A duration, a <xref href="System.TimeOnly" data-throw-if-not-resolved="false"></xref> and a <xref href="System.DateTime" data-throw-if-not-resolved="false"></xref> are held to the pattern of the form they
are written in, which no format of JSON Schema describes. A closed set lists its values in <code>enum</code>, and the
example goes to <code>examples</code>, each written by the value object's own converter. The description and the format
follow, the format as the profile says (<xref href="AdCodicem.ValueObjects.Json.ValueObjectJsonSchemaProfile" data-throw-if-not-resolved="false"></xref>).
</p>
<p>
A collection or a dictionary of value objects gets its <code>items</code> or <code>additionalProperties</code>, which the
exporter leaves out when the element's schema is <code>true</code>, and a dictionary keyed by a value object gets the key's
rules in <code>propertyNames</code>, as the key is written: always text, the text of a number for a value object over a
number. A value object is described in place, never as a reference.
</p>
<p>
The schema a host hands over is completed rather than replaced: a description it already wrote comes first, the
value object's following it, and a keyword the value object does not declare, such as a <code>default</code>, stays.
Every host built on the exporter exposes the <xref href="System.Text.Json.Serialization.Metadata.JsonTypeInfo" data-throw-if-not-resolved="false"></xref> <xref href="AdCodicem.ValueObjects.Json.ValueObjectJsonSchema.Apply(System.Text.Json.Serialization.Metadata.JsonTypeInfo%2cSystem.Text.Json.Nodes.JsonNode%2cAdCodicem.ValueObjects.Json.ValueObjectJsonSchemaProfile)" data-throw-if-not-resolved="false"></xref> takes, whatever its own
context is called: Microsoft.Extensions.AI, the Model Context Protocol SDK, Agent Framework.
</p>
<p>
A value object is looked up in <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry" data-throw-if-not-resolved="false"></xref>, without reflection. One nothing registered, a value
object written by hand or a construction of a generic one, is described by reflection where the runtime supports
dynamic code, and left as the exporter described it under native AOT, where it has to be registered, as serializing
it through <xref href="AdCodicem.ValueObjects.Json.ValueObjectJsonConverterFactory" data-throw-if-not-resolved="false"></xref> asks already: a value object written by hand through
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.Register%60%602(AdCodicem.ValueObjects.Metadata.ValueObjectSchema)" data-throw-if-not-resolved="false"></xref>, and a construction of a generic one
through <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.Register%60%602(System.Func%7bSystem.Text.Json.Serialization.JsonConverter%7b%60%600%7d%7d)" data-throw-if-not-resolved="false"></xref>.
</p>

## Methods

### Apply\(JsonTypeInfo, JsonNode, ValueObjectJsonSchemaProfile\) {#AdCodicem_ValueObjects_Json_ValueObjectJsonSchema_Apply_System_Text_Json_Serialization_Metadata_JsonTypeInfo_System_Text_Json_Nodes_JsonNode_AdCodicem_ValueObjects_Json_ValueObjectJsonSchemaProfile_}

Describes the value object a schema node stands for, or the value objects a collection or a dictionary holds.

```csharp
public static JsonNode Apply(JsonTypeInfo typeInfo, JsonNode schema, ValueObjectJsonSchemaProfile profile = ValueObjectJsonSchemaProfile.OpenApi)
```

#### Parameters

`typeInfo` [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo)

The contract of the node's type, under the options the schema describes the wire with.

`schema` [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode)

The schema built so far for the node, <code>true</code> for a value object.

`profile` [ValueObjectJsonSchemaProfile](AdCodicem.ValueObjects.Json.ValueObjectJsonSchemaProfile.md)

Whom the schema describes a value object for.

#### Returns

 [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode)

The schema: a new object in place of <code>true</code>, the same object completed otherwise, or the schema unchanged when
the node is neither a value object nor a container of them, or the schema <code>false</code>.

#### Remarks

<p>
This is the piece every host shares: each one that builds on <xref href="System.Text.Json.Schema.JsonSchemaExporter" data-throw-if-not-resolved="false"></xref> hands a transform of
its own the <xref href="System.Text.Json.Serialization.Metadata.JsonTypeInfo" data-throw-if-not-resolved="false"></xref> of the node, whatever its context is called. With Microsoft.Extensions.AI:
</p>

<pre><code class="lang-csharp">var options = new AIJsonSchemaCreateOptions
{
    TransformSchemaNode = (context, schema) =&gt; ValueObjectJsonSchema.Apply(
        context.TypeInfo, schema, ValueObjectJsonSchemaProfile.LanguageModel),
};</code></pre>

<p>
The node of a value object is <xref href="System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.None" data-throw-if-not-resolved="false"></xref>, its converter being its own. A value object
System.Text.Json serializes as an object with properties, one written by hand without a converter, has a contract
of another kind and is left as it is.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">typeInfo</code> or <code class="paramref">schema</code> is
    <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentOutOfRangeException](https://learn.microsoft.com/dotnet/api/system.argumentoutofrangeexception)

<code class="paramref">profile</code> is not a defined profile.

### CreateTransform\(ValueObjectJsonSchemaProfile\) {#AdCodicem_ValueObjects_Json_ValueObjectJsonSchema_CreateTransform_AdCodicem_ValueObjects_Json_ValueObjectJsonSchemaProfile_}

Creates the transform <xref href="System.Text.Json.Schema.JsonSchemaExporterOptions.TransformSchemaNode" data-throw-if-not-resolved="false"></xref> takes for a profile.

```csharp
public static Func<JsonSchemaExporterContext, JsonNode, JsonNode> CreateTransform(ValueObjectJsonSchemaProfile profile)
```

#### Parameters

`profile` [ValueObjectJsonSchemaProfile](AdCodicem.ValueObjects.Json.ValueObjectJsonSchemaProfile.md)

Whom the schema describes a value object for.

#### Returns

 [Func](https://learn.microsoft.com/dotnet/api/system.func\-3)<[JsonSchemaExporterContext](https://learn.microsoft.com/dotnet/api/system.text.json.schema.jsonschemaexportercontext), [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode), [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode)\>

The transform, the same instance for a given profile.

#### Remarks

<pre><code class="lang-csharp">var options = new JsonSchemaExporterOptions
{
    TransformSchemaNode = ValueObjectJsonSchema.CreateTransform(ValueObjectJsonSchemaProfile.LanguageModel),
};</code></pre>

#### Exceptions

 [ArgumentOutOfRangeException](https://learn.microsoft.com/dotnet/api/system.argumentoutofrangeexception)

<code class="paramref">profile</code> is not a defined profile.

### TransformSchemaNode\(JsonSchemaExporterContext, JsonNode\) {#AdCodicem_ValueObjects_Json_ValueObjectJsonSchema_TransformSchemaNode_System_Text_Json_Schema_JsonSchemaExporterContext_System_Text_Json_Nodes_JsonNode_}

Describes the value objects of a schema the exporter builds, under the
<xref href="AdCodicem.ValueObjects.Json.ValueObjectJsonSchemaProfile.OpenApi" data-throw-if-not-resolved="false"></xref> profile: the delegate
<xref href="System.Text.Json.Schema.JsonSchemaExporterOptions.TransformSchemaNode" data-throw-if-not-resolved="false"></xref> takes.

```csharp
public static JsonNode TransformSchemaNode(JsonSchemaExporterContext context, JsonNode schema)
```

#### Parameters

`context` [JsonSchemaExporterContext](https://learn.microsoft.com/dotnet/api/system.text.json.schema.jsonschemaexportercontext)

The context of the node, which names its type.

`schema` [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode)

The schema the exporter built for the node.

#### Returns

 [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode)

The schema, completed when it describes a value object or a container of value objects.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">schema</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

