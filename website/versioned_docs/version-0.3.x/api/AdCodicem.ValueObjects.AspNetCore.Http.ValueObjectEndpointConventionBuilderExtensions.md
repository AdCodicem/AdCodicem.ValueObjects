# Class ValueObjectEndpointConventionBuilderExtensions {#AdCodicem_ValueObjects_AspNetCore_Http_ValueObjectEndpointConventionBuilderExtensions}

Namespace: [AdCodicem.ValueObjects.AspNetCore.Http](AdCodicem.ValueObjects.AspNetCore.Http.md)  
Assembly: AdCodicem.ValueObjects.AspNetCore.Http.dll  

Answers a value object a minimal API endpoint could not bind with problem details carrying the code of the rule it
broke.

```csharp
public static class ValueObjectEndpointConventionBuilderExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectEndpointConventionBuilderExtensions](AdCodicem.ValueObjects.AspNetCore.Http.ValueObjectEndpointConventionBuilderExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### WithValueObjectProblemDetails<TBuilder\>\(TBuilder\) {#AdCodicem_ValueObjects_AspNetCore_Http_ValueObjectEndpointConventionBuilderExtensions_WithValueObjectProblemDetails__1___0_}

Answers a value object these endpoints could not bind with problem details carrying the code of the rule it
broke.

```csharp
public static TBuilder WithValueObjectProblemDetails<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
```

#### Parameters

`builder` TBuilder

The endpoints to cover.

#### Returns

 TBuilder

The same builder, so calls can be chained.

#### Type Parameters

`TBuilder` 

The builder: a route group, one endpoint, or any other convention builder.

#### Remarks

<p>
A minimal API refuses a value object it cannot bind with a 400 that says nothing of why. On the endpoints this
covers, the refusal is answered with the validation problem MVC writes, <code>errors</code> holding the message of each
rule a value broke, and <xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectProblemDetails.ExtensionName" data-throw-if-not-resolved="false"></xref> mapping each member to the stable
code of that rule, read from the same <code>TryParse</code> the binder ran. A route value, a query value and a header
are covered, under the name they bind from, which <code>[FromRoute]</code>, <code>[FromQuery]</code> and <code>[FromHeader]</code>
may set; so are the members of an <code>[AsParameters]</code> type and an array of value objects read from the query
string, whose refused elements are listed under its name with the code of the first. A required value object
that is absent is reported with <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref>.
</p>
<p>
A filter is added to each endpoint whose handler may bind a value object from text. When it runs, it reads one
thing, the status the binder left: anything but a 400 goes on untouched, and only a refusal pays for the second
parse. A 400 that no value object caused, a refused <xref href="System.Int32" data-throw-if-not-resolved="false"></xref> or <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> parameter for
instance, keeps the framework's answer: a parameter that is not a value object has no rule code to report, and
is not listed beside one that is. The convention acts once on an endpoint it covers twice, through a route group
and the endpoint itself.
</p>
<p>
Empty text follows the binder that ran. The reflection-based binding parses it, so that <code>?country=</code> for a
<code>CountryCode?</code> is reported with the rule that refused it. The Request Delegate Generator refuses no empty
query text: it binds <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> to a nullable value object, and the default instance, which no rule
has checked, to one that cannot be <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>, an array element included, so that nothing is reported
there. It takes an empty header for an absent one, reported with <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref> when
the parameter is required.
</p>
<p>
The binder refuses a request body before any filter runs, and the framework answers it itself. With
<code>RouteHandlerOptions.ThrowOnBadRequest</code> on, the default in Development, the binder throws instead, before
the filter runs, and the exception handler registered by
<xref href="AdCodicem.ValueObjects.AspNetCore.Http.ValueObjectHttpServiceCollectionExtensions.AddValueObjectHttpProblemDetails(Microsoft.Extensions.DependencyInjection.IServiceCollection)" data-throw-if-not-resolved="false"></xref> answers a body as well
as a route, query or header value with the same problem details. A form field and an array read from headers are
not covered. Under <code>AddValidation()</code>, whose filter runs before every other, a request that also breaks a
DataAnnotations rule is answered with that rule alone.
</p>
<p>
The filter closes the check of each value object over its type through the descriptor's visitor, so that it runs
in a native binary. Under native AOT, a value object nothing registered, one written by hand or a construction of
a generic one, cannot be described there, and its refusal is left unexplained: register it as the JSON package
asks, <code>ValueObjectRegistry.Register&lt;TSelf, TValue&gt;(TSelf.Schema)</code> for one written by hand, with its
generated converter for a construction.
</p>
<p>
The response is written with the HTTP JSON options, which, without reflection-based serialization, as in a native
binary, know a type only through a serializer context: register <code>AddProblemDetails()</code>, which chains the
framework's own for problem details into them. Without it, or a context of the application's listing
<code>HttpValidationProblemDetails</code> and <code>JsonElement</code>, the covered endpoints fail to build, when the first
request needs them, with an <xref href="System.InvalidOperationException" data-throw-if-not-resolved="false"></xref> naming the call, rather than answer each
refusal with a 500 where the framework answers a 400.
</p>
<p>
The messages are the English ones of the rules.
<xref href="AdCodicem.ValueObjects.AspNetCore.Http.ValueObjectEndpointConventionBuilderExtensions.WithValueObjectProblemDetails%60%601(%60%600%2cSystem.Action%7bAdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions%7d)" data-throw-if-not-resolved="false"></xref> answers in the
application's language.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">builder</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### WithValueObjectProblemDetails<TBuilder\>\(TBuilder, Action<ValueObjectMessageOptions\>\) {#AdCodicem_ValueObjects_AspNetCore_Http_ValueObjectEndpointConventionBuilderExtensions_WithValueObjectProblemDetails__1___0_System_Action_AdCodicem_ValueObjects_AspNetCore_ValueObjectMessageOptions__}

Answers a value object these endpoints could not bind with problem details carrying the code of the rule it
broke, and the message the application's resource gives that code.

```csharp
public static TBuilder WithValueObjectProblemDetails<TBuilder>(this TBuilder builder, Action<ValueObjectMessageOptions> configure) where TBuilder : IEndpointConventionBuilder
```

#### Parameters

`builder` TBuilder

The endpoints to cover.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action\-1)<[ValueObjectMessageOptions](AdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions.md)\>

Names the resource the messages are read from, through <xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions.UseResource%60%601" data-throw-if-not-resolved="false"></xref>.

#### Returns

 TBuilder

The same builder, so calls can be chained.

#### Type Parameters

`TBuilder` 

The builder: a route group, one endpoint, or any other convention builder.

#### Remarks

<p>
The endpoints are covered as <xref href="AdCodicem.ValueObjects.AspNetCore.Http.ValueObjectEndpointConventionBuilderExtensions.WithValueObjectProblemDetails%60%601(%60%600)" data-throw-if-not-resolved="false"></xref> covers them, and
<code>errors</code> holds, for each refused member, the message the resource holds under the key of the rule's code,
<code>ValueObject.value_object.out_of_range</code>, looked up after <code>ValueObject.value_object.out_of_range.minimum</code>
or <code>.maximum</code> for a type declaring one bound alone. It is formatted with the name the member binds from as
<code>{0}</code>, the name of the value object for a request body, whose JSON path may hold a refused dictionary key, and
the arguments the type's rules supply from <code>{1}</code>: a length, both bounds, the known values. The codes under
<xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectProblemDetails.ExtensionName" data-throw-if-not-resolved="false"></xref> are the same in every language. A key the resource lacks
keeps the English message.
</p>
<p>
The resource is read through the request's <code>IStringLocalizerFactory</code>: register
<code>builder.Services.AddLocalization()</code>, without which a refusal throws an <xref href="System.InvalidOperationException" data-throw-if-not-resolved="false"></xref>
naming it, and <code>app.UseRequestLocalization()</code>, so that the request chooses the culture. The current UI culture
chooses the template and the current culture formats the arguments. The exception handler that answers with
<code>ThrowOnBadRequest</code> on runs in the request's culture, whatever the order of <code>UseRequestLocalization()</code>
and <code>UseExceptionHandler()</code>. A template quoting an argument its code does not supply throws its
<xref href="System.FormatException" data-throw-if-not-resolved="false"></xref>, which is not caught.
</p>
<p>
The options belong to the endpoint: covered twice, through a route group and the endpoint itself, it takes those
of the convention that runs last, the endpoint's own over its group's, and a call without options keeps the
other's.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">builder</code> or <code class="paramref">configure</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

