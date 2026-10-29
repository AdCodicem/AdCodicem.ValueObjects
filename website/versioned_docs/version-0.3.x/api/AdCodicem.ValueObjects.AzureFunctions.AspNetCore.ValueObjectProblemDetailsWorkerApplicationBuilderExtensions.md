# Class ValueObjectProblemDetailsWorkerApplicationBuilderExtensions {#AdCodicem_ValueObjects_AzureFunctions_AspNetCore_ValueObjectProblemDetailsWorkerApplicationBuilderExtensions}

Namespace: [AdCodicem.ValueObjects.AzureFunctions.AspNetCore](AdCodicem.ValueObjects.AzureFunctions.AspNetCore.md)  
Assembly: AdCodicem.ValueObjects.AzureFunctions.AspNetCore.dll  

Registers the middleware that answers an HTTP trigger of the ASP.NET Core integration whose value objects the worker
could not bind with RFC 9457 problem details carrying their rule codes.

```csharp
public static class ValueObjectProblemDetailsWorkerApplicationBuilderExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectProblemDetailsWorkerApplicationBuilderExtensions](AdCodicem.ValueObjects.AzureFunctions.AspNetCore.ValueObjectProblemDetailsWorkerApplicationBuilderExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### UseValueObjectProblemDetails\(IFunctionsWorkerApplicationBuilder\) {#AdCodicem_ValueObjects_AzureFunctions_AspNetCore_ValueObjectProblemDetailsWorkerApplicationBuilderExtensions_UseValueObjectProblemDetails_Microsoft_Azure_Functions_Worker_IFunctionsWorkerApplicationBuilder_}

Answers an HTTP trigger whose value objects the worker could not bind with problem details carrying their rule
codes.

```csharp
public static IFunctionsWorkerApplicationBuilder UseValueObjectProblemDetails(this IFunctionsWorkerApplicationBuilder builder)
```

#### Parameters

`builder` [IFunctionsWorkerApplicationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.azure.functions.worker.ifunctionsworkerapplicationbuilder)

The worker's builder.

#### Returns

 [IFunctionsWorkerApplicationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.azure.functions.worker.ifunctionsworkerapplicationbuilder)

The same builder, so calls can be chained.

#### Remarks

<p>
Call it after <code>ConfigureFunctionsWebApplication()</code>, or inside
<code>ConfigureFunctionsWebApplication(worker =&gt; …)</code>: the integration's middleware, which writes the answer on the
request, has to run outside this one. Called before, the failure keeps the worker's 500 and a warning is logged
once. The built-in HTTP model, <code>HttpRequestData</code> without the integration, keeps the 500.
</p>
<p>
A refused route or query value is answered 400 with the shape <code>WithValueObjectProblemDetails()</code> writes on a
minimal API: <code>errors</code> holds the message of each rule, <code>errorCodes</code> its code, keyed by the parameter's
name; a required value object with no binding data is <code>value_object.required</code>. A <code>[FromBody]</code> value
object is answered too where the MVC package's <code>AddValueObjects()</code> reads the body, keyed by its JSON path. A
failure no value object explains, a plain <xref href="System.Int32" data-throw-if-not-resolved="false"></xref> among them, keeps the worker's 500. Each answer is
logged once, with the parameters and their codes, never the values.
</p>
<p>Calling it twice adds the middleware once.</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">builder</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### UseValueObjectProblemDetails\(IFunctionsWorkerApplicationBuilder, Action<ValueObjectMessageOptions\>\) {#AdCodicem_ValueObjects_AzureFunctions_AspNetCore_ValueObjectProblemDetailsWorkerApplicationBuilderExtensions_UseValueObjectProblemDetails_Microsoft_Azure_Functions_Worker_IFunctionsWorkerApplicationBuilder_System_Action_AdCodicem_ValueObjects_AspNetCore_ValueObjectMessageOptions__}

Answers an HTTP trigger whose value objects the worker could not bind with problem details carrying their rule
codes, with the messages the application's resource gives each code.

```csharp
public static IFunctionsWorkerApplicationBuilder UseValueObjectProblemDetails(this IFunctionsWorkerApplicationBuilder builder, Action<ValueObjectMessageOptions> configure)
```

#### Parameters

`builder` [IFunctionsWorkerApplicationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.azure.functions.worker.ifunctionsworkerapplicationbuilder)

The worker's builder.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action\-1)<[ValueObjectMessageOptions](AdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions.md)\>

Names the resource the messages are read from, through
    <xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions.UseResource%60%601" data-throw-if-not-resolved="false"></xref>.

#### Returns

 [IFunctionsWorkerApplicationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.azure.functions.worker.ifunctionsworkerapplicationbuilder)

The same builder, so calls can be chained.

#### Remarks

The messages are read through the request's <code>IStringLocalizerFactory</code>, which
<code>builder.Services.AddLocalization()</code> registers, in the culture <code>UseRequestLocalization()</code> chose when the
application added it to the integration's pipeline, and otherwise in the one the application's
<code>RequestLocalizationOptions</code> choose from the request, falling back to the current culture. A later call
naming messages replaces those of an earlier one.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">builder</code> or <code class="paramref">configure</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

