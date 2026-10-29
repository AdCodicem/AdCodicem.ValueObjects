# Class ValueObjectMcpServerOptionsExtensions {#AdCodicem_ValueObjects_ModelContextProtocol_ValueObjectMcpServerOptionsExtensions}

Namespace: [AdCodicem.ValueObjects.ModelContextProtocol](AdCodicem.ValueObjects.ModelContextProtocol.md)  
Assembly: AdCodicem.ValueObjects.ModelContextProtocol.dll  

Adds the check of value-object arguments to server options the application builds or changes itself.

```csharp
public static class ValueObjectMcpServerOptionsExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectMcpServerOptionsExtensions](AdCodicem.ValueObjects.ModelContextProtocol.ValueObjectMcpServerOptionsExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### AddValueObjectValidation\(McpServerOptions\) {#AdCodicem_ValueObjects_ModelContextProtocol_ValueObjectMcpServerOptionsExtensions_AddValueObjectValidation_ModelContextProtocol_Server_McpServerOptions_}

Places the check of the value-object arguments of the tools
<xref href="AdCodicem.ValueObjects.ModelContextProtocol.ValueObjectMcpServerTool.Create(System.Reflection.MethodInfo%2cSystem.Object%2cModelContextProtocol.Server.McpServerToolCreateOptions)" data-throw-if-not-resolved="false"></xref> created last among
the call-tool filters of the options, once, so that it runs inside every filter already there.

```csharp
public static McpServerOptions AddValueObjectValidation(this McpServerOptions options)
```

#### Parameters

`options` McpServerOptions

The options of the server, before the server is created from them.

#### Returns

 McpServerOptions

The options.

#### Remarks

<p>
The <code>WithValueObjectTools</code> methods of <xref href="AdCodicem.ValueObjects.ModelContextProtocol.ValueObjectMcpServerBuilderExtensions" data-throw-if-not-resolved="false"></xref> add the check
themselves, once the options are configured. Call this method where nothing does, on the options of a server
created without dependency injection:
</p>

<pre><code class="lang-csharp">var options = new McpServerOptions { ToolCollection = [ValueObjectMcpServerTool.Create(method)] };
await using var server = McpServer.Create(transport, options.AddValueObjectValidation());</code></pre>

<p>
or after adding a call-tool filter of the application's own where the options are changed later, in
<code>HttpServerTransportOptions.ConfigureSessionOptions</code> for instance, which runs for each session after the
options are configured: the check, already there, moves after that filter. The server builds its pipeline when it
is created, from the filters the options hold then. The SDK refuses call-tool filters beside an explicit
<code>CallToolWithAlternateHandler</code>, and throws when the server is created.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">options</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

