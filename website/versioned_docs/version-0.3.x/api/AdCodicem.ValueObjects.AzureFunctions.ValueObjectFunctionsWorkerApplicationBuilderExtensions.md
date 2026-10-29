# Class ValueObjectFunctionsWorkerApplicationBuilderExtensions {#AdCodicem_ValueObjects_AzureFunctions_ValueObjectFunctionsWorkerApplicationBuilderExtensions}

Namespace: [AdCodicem.ValueObjects.AzureFunctions](AdCodicem.ValueObjects.AzureFunctions.md)  
Assembly: AdCodicem.ValueObjects.AzureFunctions.dll  

Registers the value object converter with the Azure Functions isolated worker.

```csharp
public static class ValueObjectFunctionsWorkerApplicationBuilderExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectFunctionsWorkerApplicationBuilderExtensions](AdCodicem.ValueObjects.AzureFunctions.ValueObjectFunctionsWorkerApplicationBuilderExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### UseValueObjects\(IFunctionsWorkerApplicationBuilder\) {#AdCodicem_ValueObjects_AzureFunctions_ValueObjectFunctionsWorkerApplicationBuilderExtensions_UseValueObjects_Microsoft_Azure_Functions_Worker_IFunctionsWorkerApplicationBuilder_}

Puts the value object converter first among the worker's input converters, or right behind the ASP.NET Core
integration's own converter when called before <code>ConfigureFunctionsWebApplication()</code>, so that a route value, a
query value and other binding data bind to a value object through its <code>TryParse</code>.

```csharp
public static IFunctionsWorkerApplicationBuilder UseValueObjects(this IFunctionsWorkerApplicationBuilder builder)
```

#### Parameters

`builder` [IFunctionsWorkerApplicationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.azure.functions.worker.ifunctionsworkerapplicationbuilder)

The worker's builder: the one <code>FunctionsApplication.CreateBuilder(args)</code> returns, or the
    one <code>ConfigureFunctionsWebApplication(worker =&gt; …)</code> and <code>ConfigureFunctionsWorkerDefaults(worker =&gt; …)</code>
    hand over.

#### Returns

 [IFunctionsWorkerApplicationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.azure.functions.worker.ifunctionsworkerapplicationbuilder)

The same builder, so calls can be chained.

#### Remarks

<p>
The worker tries the converter a parameter or its type names with <code>[InputConverter]</code> first, then those its
binding attribute advertises, then the registered ones in order: the value object converter goes first among the
last, where it reads only value objects, and only from text. Payloads stay with their bindings and the worker's
serializer.
</p>
<p>
The ASP.NET Core integration puts its own converter first too, from a configuration of its own, which runs after
this one when <code>ConfigureFunctionsWebApplication()</code> is called later: the value object converter then comes
right behind it. That converter binds an <code>HttpRequest</code> and an <code>HttpRequestData</code> alone, so either order
binds the same.
</p>
<p>Calling it twice registers the converter once.</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">builder</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

