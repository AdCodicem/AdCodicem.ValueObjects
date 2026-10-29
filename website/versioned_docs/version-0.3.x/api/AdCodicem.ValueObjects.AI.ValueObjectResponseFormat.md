# Class ValueObjectResponseFormat {#AdCodicem_ValueObjects_AI_ValueObjectResponseFormat}

Namespace: [AdCodicem.ValueObjects.AI](AdCodicem.ValueObjects.AI.md)  
Assembly: AdCodicem.ValueObjects.AI.dll  

Builds the JSON schema response format of a structured output whose value objects carry their rules.

```csharp
public static class ValueObjectResponseFormat
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectResponseFormat](AdCodicem.ValueObjects.AI.ValueObjectResponseFormat.md)

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
<code>GetResponseAsync&lt;T&gt;</code> and <xref href="Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema%60%601(System.Text.Json.JsonSerializerOptions%2cSystem.String%2cSystem.String)" data-throw-if-not-resolved="false"></xref> build the schema with options
of their own, which no transform reaches: each value object of the answer is published as <code>true</code>, and a list of
them as <code>"items": {}</code>. This builds the same format with the rules of every value object it holds, and the
answer is read the way <code>GetResponseAsync&lt;T&gt;</code> reads it:
</p>

<pre><code class="lang-csharp">ChatResponse response = await chatClient.GetResponseAsync(
    messages,
    new ChatOptions { ResponseFormat = ValueObjectResponseFormat.ForJsonSchema&lt;Order&gt;(AppJsonContext.Default.Options) });
var order = new ChatResponse&lt;Order&gt;(response, AppJsonContext.Default.Options).Result;</code></pre>

<p>
The same rules validate the answer as it is read: a value the model wrote that a value object refuses throws a
<xref href="AdCodicem.ValueObjects.ValueObjectJsonException" data-throw-if-not-resolved="false"></xref> from <code>Result</code>, whose code <xref href="AdCodicem.ValueObjects.ValueObjectErrors.TryGetCode(System.Exception%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref>
reads, for the application to ask again.
</p>

## Methods

### ForJsonSchema<T\>\(JsonSerializerOptions?, string?, string?\) {#AdCodicem_ValueObjects_AI_ValueObjectResponseFormat_ForJsonSchema__1_System_Text_Json_JsonSerializerOptions_System_String_System_String_}

Creates a JSON schema response format for <code class="typeparamref">T</code>, with the rules of every value object it holds.

```csharp
public static ChatResponseFormatJson ForJsonSchema<T>(JsonSerializerOptions? serializerOptions = null, string? schemaName = null, string? schemaDescription = null)
```

#### Parameters

`serializerOptions` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)?

The options the answer is read with, <xref href="Microsoft.Extensions.AI.AIJsonUtilities.DefaultOptions" data-throw-if-not-resolved="false"></xref> by default.

`schemaName` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The name of the schema: by default the type's <xref href="System.ComponentModel.DisplayNameAttribute" data-throw-if-not-resolved="false"></xref>, or its name with every character
outside <code>[0-9A-Za-z_]</code> replaced by <code>_</code>, as <xref href="Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema%60%601(System.Text.Json.JsonSerializerOptions%2cSystem.String%2cSystem.String)" data-throw-if-not-resolved="false"></xref> names it.

`schemaDescription` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The description of the schema, the type's <xref href="System.ComponentModel.DescriptionAttribute" data-throw-if-not-resolved="false"></xref> by default.

#### Returns

 ChatResponseFormatJson

The response format, its schema led by <code>$schema</code>, as Microsoft.Extensions.AI writes one.

#### Type Parameters

`T` 

The type of the structured output, which serializes as a JSON object.

#### Exceptions

 [NotSupportedException](https://learn.microsoft.com/dotnet/api/system.notsupportedexception)

The resolver of <code class="paramref">serializerOptions</code> has no contract for <code class="typeparamref">T</code>.

