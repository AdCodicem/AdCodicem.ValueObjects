# Class ValueObjectMcpServerBuilderExtensions {#AdCodicem_ValueObjects_ModelContextProtocol_ValueObjectMcpServerBuilderExtensions}

Namespace: [AdCodicem.ValueObjects.ModelContextProtocol](AdCodicem.ValueObjects.ModelContextProtocol.md)  
Assembly: AdCodicem.ValueObjects.ModelContextProtocol.dll  

Registers Model Context Protocol tools whose schemas carry the rules of their value objects, and answers a
value-object argument a rule refuses with that rule, as a tool execution error a model can correct from.

```csharp
public static class ValueObjectMcpServerBuilderExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectMcpServerBuilderExtensions](AdCodicem.ValueObjects.ModelContextProtocol.ValueObjectMcpServerBuilderExtensions.md)

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
The SDK's <code>WithTools</code> methods hand a tool no schema options, and the SDK describes a value object, serialized by
a converter of its own, as <code>true</code>, the schema that accepts anything; it answers the exception binding a refused
argument throws with <code>An error occurred invoking 'place_order'.</code>, which loses the rule. Each method here mirrors
one of the SDK's, finding the same methods and creating an instance of an instance method's type for each call, and
closes both gaps:
</p>

<pre><code class="lang-csharp">builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithValueObjectTools&lt;OrderTools&gt;();</code></pre>

<p>
Each value-object argument is read, before the tool binds it, through the contract the tool's serializer options hold
for its parameter, which runs the value object's converter and its rules, never <code>CreateUnchecked</code>. The first
refused, in the order of the parameters, is answered instead of calling the tool, and no instance of its type is
created:
</p>

<pre><code class="lang-csharp">{"isError":true,"content":[{"type":"text","text":"Argument 'quantity' rejected (value_object.out_of_range): The value is not a valid Quantity: The value must be less than or equal to 100."}],
 "structuredContent":{"error":"invalid_argument","argument":"quantity","code":"value_object.out_of_range","message":"The value is not a valid Quantity: The value must be less than or equal to 100."}}</code></pre>

<p>
The structured content is left out for a tool that declares an output schema, which it would not conform to. The
message names the value object and the rule, never the value sent. An absent argument the parameter has no default
for, and a <code>null</code> for a value object that cannot be one, are <code>value_object.required</code>; a collection, a
dictionary or an object is answered for a value object it holds, any other error being left to the tool. Parameters
the SDK binds itself, the server, the request context, a progress reporter, a cancellation token or a service, are
never read.
</p>
<p>
The check is a call-tool filter, added once whatever the number of calls, after every filter the server's options
are configured with, so that it runs after the application's own filters and authorization; a filter added later,
in <code>HttpServerTransportOptions.ConfigureSessionOptions</code>, runs inside it, unless
<xref href="AdCodicem.ValueObjects.ModelContextProtocol.ValueObjectMcpServerOptionsExtensions.AddValueObjectValidation(ModelContextProtocol.Server.McpServerOptions)" data-throw-if-not-resolved="false"></xref> places it again there. The SDK refuses
call-tool filters beside an explicit <code>CallToolWithAlternateHandler</code>, and throws when it creates the server: at
its start over stdio, for each session over HTTP, for each request when the server is stateless.
</p>
<p>
Where reflection-based serialization is disabled, as native AOT disables it, <code>serializerOptions</code> has to describe
every parameter and result: a copy of <xref href="ModelContextProtocol.McpJsonUtilities.DefaultOptions" data-throw-if-not-resolved="false"></xref> whose
<xref href="System.Text.Json.JsonSerializerOptions.TypeInfoResolverChain" data-throw-if-not-resolved="false"></xref> starts with a source-generated context naming
<xref href="AdCodicem.ValueObjects.Json.ValueObjectJsonConverterFactory" data-throw-if-not-resolved="false"></xref> and the types of the parameters and results. Without it, the SDK throws
a <xref href="System.NotSupportedException" data-throw-if-not-resolved="false"></xref> when the server is first resolved.
</p>

## Methods

### WithValueObjectTools<TToolType\>\(IMcpServerBuilder, JsonSerializerOptions?\) {#AdCodicem_ValueObjects_ModelContextProtocol_ValueObjectMcpServerBuilderExtensions_WithValueObjectTools__1_Microsoft_Extensions_DependencyInjection_IMcpServerBuilder_System_Text_Json_JsonSerializerOptions_}

Registers the <xref href="ModelContextProtocol.Server.McpServerToolAttribute" data-throw-if-not-resolved="false"></xref> methods of a type as
<xref href="Microsoft.Extensions.DependencyInjection.McpServerBuilderExtensions.WithTools%60%601(Microsoft.Extensions.DependencyInjection.IMcpServerBuilder%2cSystem.Text.Json.JsonSerializerOptions)" data-throw-if-not-resolved="false"></xref> does, with
the rules of each value object in their schemas, and answers a refused value-object argument with its rule.

```csharp
public static IMcpServerBuilder WithValueObjectTools<TToolType>(this IMcpServerBuilder builder, JsonSerializerOptions? serializerOptions = null)
```

#### Parameters

`builder` IMcpServerBuilder

The builder of the server.

`serializerOptions` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)?

The options the arguments and results are serialized with, or
    <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for <xref href="ModelContextProtocol.McpJsonUtilities.DefaultOptions" data-throw-if-not-resolved="false"></xref>.

#### Returns

 IMcpServerBuilder

The builder.

#### Type Parameters

`TToolType` 

The tool type, whose static and instance methods, public and not, are searched; an
    instance of it is created for each call of an instance method, its constructor's parameters resolved from the
    services of the call.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">builder</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### WithValueObjectTools\(IMcpServerBuilder, IEnumerable<Type\>, JsonSerializerOptions?\) {#AdCodicem_ValueObjects_ModelContextProtocol_ValueObjectMcpServerBuilderExtensions_WithValueObjectTools_Microsoft_Extensions_DependencyInjection_IMcpServerBuilder_System_Collections_Generic_IEnumerable_System_Type__System_Text_Json_JsonSerializerOptions_}

Registers the <xref href="ModelContextProtocol.Server.McpServerToolAttribute" data-throw-if-not-resolved="false"></xref> methods of each type as
<xref href="Microsoft.Extensions.DependencyInjection.McpServerBuilderExtensions.WithTools(Microsoft.Extensions.DependencyInjection.IMcpServerBuilder%2cSystem.Collections.Generic.IEnumerable%7bSystem.Type%7d%2cSystem.Text.Json.JsonSerializerOptions)" data-throw-if-not-resolved="false"></xref> does,
with the rules of each value object in their schemas, and answers a refused value-object argument with its rule.

```csharp
[RequiresUnreferencedCode("Discovers tool methods by reflection, as the SDK's non-generic WithTools and WithToolsFromAssembly do, and might not work in native AOT. Use WithValueObjectTools<TToolType>() instead.")]
public static IMcpServerBuilder WithValueObjectTools(this IMcpServerBuilder builder, IEnumerable<Type> toolTypes, JsonSerializerOptions? serializerOptions = null)
```

#### Parameters

`builder` IMcpServerBuilder

The builder of the server.

`toolTypes` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable\-1)<[Type](https://learn.microsoft.com/dotnet/api/system.type)\>

The tool types; a <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> among them is skipped.

`serializerOptions` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)?

The options the arguments and results are serialized with, or
    <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for <xref href="ModelContextProtocol.McpJsonUtilities.DefaultOptions" data-throw-if-not-resolved="false"></xref>.

#### Returns

 IMcpServerBuilder

The builder.

#### Remarks

A static class, which cannot be a type argument, is registered here.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">builder</code> or <code class="paramref">toolTypes</code> is
    <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### WithValueObjectTools\(IMcpServerBuilder, IEnumerable<McpServerTool\>\) {#AdCodicem_ValueObjects_ModelContextProtocol_ValueObjectMcpServerBuilderExtensions_WithValueObjectTools_Microsoft_Extensions_DependencyInjection_IMcpServerBuilder_System_Collections_Generic_IEnumerable_ModelContextProtocol_Server_McpServerTool__}

Registers tools as <xref href="Microsoft.Extensions.DependencyInjection.McpServerBuilderExtensions.WithTools(Microsoft.Extensions.DependencyInjection.IMcpServerBuilder%2cSystem.Collections.Generic.IEnumerable%7bModelContextProtocol.Server.McpServerTool%7d)" data-throw-if-not-resolved="false"></xref>
does, and answers a refused value-object argument of those <xref href="AdCodicem.ValueObjects.ModelContextProtocol.ValueObjectMcpServerTool" data-throw-if-not-resolved="false"></xref> created with its
rule.

```csharp
public static IMcpServerBuilder WithValueObjectTools(this IMcpServerBuilder builder, IEnumerable<McpServerTool> tools)
```

#### Parameters

`builder` IMcpServerBuilder

The builder of the server.

`tools` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable\-1)<McpServerTool\>

The tools; a <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> among them is skipped.

#### Returns

 IMcpServerBuilder

The builder.

#### Remarks

A tool <xref href="AdCodicem.ValueObjects.ModelContextProtocol.ValueObjectMcpServerTool.Create(System.Reflection.MethodInfo%2cSystem.Object%2cModelContextProtocol.Server.McpServerToolCreateOptions)" data-throw-if-not-resolved="false"></xref> created
carries the rules in its schemas wherever it is registered; registered through the SDK's <code>WithTools</code> alone,
on a server none of these methods configured, its refused arguments get the SDK's bare error, unless the
server's options are handed to <xref href="AdCodicem.ValueObjects.ModelContextProtocol.ValueObjectMcpServerOptionsExtensions.AddValueObjectValidation(ModelContextProtocol.Server.McpServerOptions)" data-throw-if-not-resolved="false"></xref>.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">builder</code> or <code class="paramref">tools</code> is
    <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### WithValueObjectToolsFromAssembly\(IMcpServerBuilder, Assembly?, JsonSerializerOptions?\) {#AdCodicem_ValueObjects_ModelContextProtocol_ValueObjectMcpServerBuilderExtensions_WithValueObjectToolsFromAssembly_Microsoft_Extensions_DependencyInjection_IMcpServerBuilder_System_Reflection_Assembly_System_Text_Json_JsonSerializerOptions_}

Registers the <xref href="ModelContextProtocol.Server.McpServerToolAttribute" data-throw-if-not-resolved="false"></xref> methods of every <xref href="ModelContextProtocol.Server.McpServerToolTypeAttribute" data-throw-if-not-resolved="false"></xref> type of
an assembly as
<xref href="Microsoft.Extensions.DependencyInjection.McpServerBuilderExtensions.WithToolsFromAssembly(Microsoft.Extensions.DependencyInjection.IMcpServerBuilder%2cSystem.Reflection.Assembly%2cSystem.Text.Json.JsonSerializerOptions)" data-throw-if-not-resolved="false"></xref>
does, with the rules of each value object in their schemas, and answers a refused value-object argument with its
rule.

```csharp
[RequiresUnreferencedCode("Discovers tool methods by reflection, as the SDK's non-generic WithTools and WithToolsFromAssembly do, and might not work in native AOT. Use WithValueObjectTools<TToolType>() instead.")]
public static IMcpServerBuilder WithValueObjectToolsFromAssembly(this IMcpServerBuilder builder, Assembly? toolAssembly = null, JsonSerializerOptions? serializerOptions = null)
```

#### Parameters

`builder` IMcpServerBuilder

The builder of the server.

`toolAssembly` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)?

The assembly, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for the one that calls this method.

`serializerOptions` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)?

The options the arguments and results are serialized with, or
    <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for <xref href="ModelContextProtocol.McpJsonUtilities.DefaultOptions" data-throw-if-not-resolved="false"></xref>.

#### Returns

 IMcpServerBuilder

The builder.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">builder</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

