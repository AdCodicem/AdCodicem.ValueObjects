# Class ValueObjectMcpServerTool {#AdCodicem_ValueObjects_ModelContextProtocol_ValueObjectMcpServerTool}

Namespace: [AdCodicem.ValueObjects.ModelContextProtocol](AdCodicem.ValueObjects.ModelContextProtocol.md)  
Assembly: AdCodicem.ValueObjects.ModelContextProtocol.dll  

Creates Model Context Protocol tools whose schemas carry the rules of their value objects.

```csharp
public static class ValueObjectMcpServerTool
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectMcpServerTool](AdCodicem.ValueObjects.ModelContextProtocol.ValueObjectMcpServerTool.md)

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
A tool created here is the SDK's own, built by <xref href="ModelContextProtocol.Server.McpServerTool.Create(System.Reflection.MethodInfo%2cSystem.Object%2cModelContextProtocol.Server.McpServerToolCreateOptions)" data-throw-if-not-resolved="false"></xref>
with schema options that describe each value object, alone, nullable, or held by a collection, a dictionary or an
object, with the rules declared on it, in its <code>inputSchema</code>, and in its <code>outputSchema</code> when it returns
structured content. The SDK rewrites the output schema of its own tools for a client on a protocol version before
<code>2026-07-28</code>, so the tool is never wrapped.
</p>
<p>
Its value-object arguments are checked on a server that one of the <code>WithValueObjectTools</code> methods of
<xref href="AdCodicem.ValueObjects.ModelContextProtocol.ValueObjectMcpServerBuilderExtensions" data-throw-if-not-resolved="false"></xref> configured: register it with
<xref href="AdCodicem.ValueObjects.ModelContextProtocol.ValueObjectMcpServerBuilderExtensions.WithValueObjectTools(Microsoft.Extensions.DependencyInjection.IMcpServerBuilder%2cSystem.Collections.Generic.IEnumerable%7bModelContextProtocol.Server.McpServerTool%7d)" data-throw-if-not-resolved="false"></xref>,
or, on a server created without dependency injection, hand its options to
<xref href="AdCodicem.ValueObjects.ModelContextProtocol.ValueObjectMcpServerOptionsExtensions.AddValueObjectValidation(ModelContextProtocol.Server.McpServerOptions)" data-throw-if-not-resolved="false"></xref>. The SDK's <code>WithTools</code> alone
publishes its schemas and leaves a refused argument to the SDK's bare error.
</p>

## Methods

### Create\(MethodInfo, object?, McpServerToolCreateOptions?\) {#AdCodicem_ValueObjects_ModelContextProtocol_ValueObjectMcpServerTool_Create_System_Reflection_MethodInfo_System_Object_ModelContextProtocol_Server_McpServerToolCreateOptions_}

Creates a tool for a method as <xref href="ModelContextProtocol.Server.McpServerTool.Create(System.Reflection.MethodInfo%2cSystem.Object%2cModelContextProtocol.Server.McpServerToolCreateOptions)" data-throw-if-not-resolved="false"></xref>
does, with the rules of each value object in its schemas.

```csharp
public static McpServerTool Create(MethodInfo method, object? target = null, McpServerToolCreateOptions? options = null)
```

#### Parameters

`method` [MethodInfo](https://learn.microsoft.com/dotnet/api/system.reflection.methodinfo)

The method the tool calls.

`target` [object](https://learn.microsoft.com/dotnet/api/system.object)?

The instance the method runs on, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for a static method.

`options` McpServerToolCreateOptions?

The options of the tool, copied and never changed: a transform its
    <xref href="ModelContextProtocol.Server.McpServerToolCreateOptions.SchemaCreateOptions" data-throw-if-not-resolved="false"></xref> carries runs first, and the value objects are
    described after it.

#### Returns

 McpServerTool

The SDK's tool, whose value-object arguments a server one of the <code>WithValueObjectTools</code> methods
    configured checks, or one whose options
    <xref href="AdCodicem.ValueObjects.ModelContextProtocol.ValueObjectMcpServerOptionsExtensions.AddValueObjectValidation(ModelContextProtocol.Server.McpServerOptions)" data-throw-if-not-resolved="false"></xref> was given.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">method</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">method</code> is an instance method and <code class="paramref">target</code>
    is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>, which the SDK refuses.

