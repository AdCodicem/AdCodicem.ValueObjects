# Class ValueObjectHttpServiceCollectionExtensions {#AdCodicem_ValueObjects_AspNetCore_Http_ValueObjectHttpServiceCollectionExtensions}

Namespace: [AdCodicem.ValueObjects.AspNetCore.Http](AdCodicem.ValueObjects.AspNetCore.Http.md)  
Assembly: AdCodicem.ValueObjects.AspNetCore.Http.dll  

Registers what answers a value object a minimal API could not bind when the binder throws instead of answering.

```csharp
public static class ValueObjectHttpServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectHttpServiceCollectionExtensions](AdCodicem.ValueObjects.AspNetCore.Http.ValueObjectHttpServiceCollectionExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### AddValueObjectHttpProblemDetails\(IServiceCollection\) {#AdCodicem_ValueObjects_AspNetCore_Http_ValueObjectHttpServiceCollectionExtensions_AddValueObjectHttpProblemDetails_Microsoft_Extensions_DependencyInjection_IServiceCollection_}

Registers the exception handler that answers a refused value object with problem details carrying the code of
its rule when <code>RouteHandlerOptions.ThrowOnBadRequest</code> is on.

```csharp
public static IServiceCollection AddValueObjectHttpProblemDetails(this IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The application's services.

#### Returns

 [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same services, so calls can be chained.

#### Remarks

<p>
With <code>ThrowOnBadRequest</code> on, the default in Development, a minimal API throws a
<xref href="Microsoft.AspNetCore.Http.BadHttpRequestException" data-throw-if-not-resolved="false"></xref> where it would answer a bare 400, before any
endpoint filter runs. The handler takes that exception, on an endpoint covered by
<xref href="AdCodicem.ValueObjects.AspNetCore.Http.ValueObjectEndpointConventionBuilderExtensions.WithValueObjectProblemDetails%60%601(%60%600)" data-throw-if-not-resolved="false"></xref> alone, and answers 400
with the problem details the filter writes: the route, query and header values of the endpoint parsed again, and
the value object a request body refused, under its JSON path, with the code
<xref href="AdCodicem.ValueObjects.ValueObjectErrors.TryGetCode(System.Exception%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref> reads from the exception the serializer threw. It never copies the
framework's message, which names the refused text; a key a dictionary keyed by a value object refuses is listed
under its JSON path, which holds the key, as MVC lists it. Any other exception, a 400 no value object caused among
them, is left to the next handler, a body the serializer cannot read at all included, even beside a refused route,
query or header value: with <code>ThrowOnBadRequest</code> off, the framework answers such a body before any filter
runs, and the handler keeps that answer. On an endpoint whose convention names a resource of messages, the handler
answers with them, in the culture of the request, whatever the order of <code>UseRequestLocalization()</code> and
<code>UseExceptionHandler()</code>.
</p>
<p>
It is an <xref href="Microsoft.AspNetCore.Diagnostics.IExceptionHandler" data-throw-if-not-resolved="false"></xref>, which the exception handler middleware calls: register
<code>AddProblemDetails()</code> and call <code>app.UseExceptionHandler()</code> wherever <code>ThrowOnBadRequest</code> is on.
Handlers run in the order they were registered, so register it before a handler that answers every exception.
Registering it twice registers it once.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">services</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

