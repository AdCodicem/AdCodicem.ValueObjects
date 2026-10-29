# Class ValueObjectMvcExtensions {#AdCodicem_ValueObjects_AspNetCore_ValueObjectMvcExtensions}

Namespace: [AdCodicem.ValueObjects.AspNetCore](AdCodicem.ValueObjects.AspNetCore.md)  
Assembly: AdCodicem.ValueObjects.AspNetCore.dll  

Wires value object support into an ASP.NET Core application.

```csharp
public static class ValueObjectMvcExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectMvcExtensions](AdCodicem.ValueObjects.AspNetCore.ValueObjectMvcExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### AddValueObjectProblemDetails\(ApiBehaviorOptions\) {#AdCodicem_ValueObjects_AspNetCore_ValueObjectMvcExtensions_AddValueObjectProblemDetails_Microsoft_AspNetCore_Mvc_ApiBehaviorOptions_}

Adds the stable error codes of the rejected value objects to the automatic 400 response.

```csharp
public static ApiBehaviorOptions AddValueObjectProblemDetails(this ApiBehaviorOptions options)
```

#### Parameters

`options` [ApiBehaviorOptions](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.apibehavioroptions)

API behaviour options.

#### Returns

 [ApiBehaviorOptions](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.apibehavioroptions)

The same options, so calls can be chained.

#### Remarks

The framework's own <code>ValidationProblemDetails</code> response is kept as is, with an <code>errorCodes</code>
extension added next to <code>errors</code>: the human-readable messages stay where clients expect them, and a
client that wants to branch on a rule now has something stable to branch on.

### AddValueObjects\(MvcOptions\) {#AdCodicem_ValueObjects_AspNetCore_ValueObjectMvcExtensions_AddValueObjects_Microsoft_AspNetCore_Mvc_MvcOptions_}

Binds value objects from the underlying value everywhere MVC reads text.

```csharp
public static MvcOptions AddValueObjects(this MvcOptions options)
```

#### Parameters

`options` [MvcOptions](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.mvcoptions)

MVC options.

#### Returns

 [MvcOptions](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.mvcoptions)

The same options, so calls can be chained.

#### Remarks

<p>
The provider is inserted first so that it wins over the built-in simple-type and complex-type binders,
which would otherwise try to bind a value object as an object with a <code>Value</code> property. It declines a
greedy binding source, which MVC's own providers bind: a header, through this binder again, the body, through
the input formatter, the services, and a binder named by <code>[ModelBinder(typeof(…))]</code>.
</p>
<p>
This adds the binder alone. <xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMvcExtensions.AddValueObjects(Microsoft.Extensions.DependencyInjection.IMvcBuilder)" data-throw-if-not-resolved="false"></xref> also configures the JSON options, and
records the code of a value refused inside a request body.
</p>
<p>
The options hold one provider: calling this again, or
<xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMvcExtensions.AddValueObjects(Microsoft.AspNetCore.Mvc.MvcOptions%2cSystem.Action%7bAdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions%7d)" data-throw-if-not-resolved="false"></xref>, keeps the provider where the first
call put it, and the last configuration of its messages wins.
</p>

### AddValueObjects\(MvcOptions, Action<ValueObjectMessageOptions\>\) {#AdCodicem_ValueObjects_AspNetCore_ValueObjectMvcExtensions_AddValueObjects_Microsoft_AspNetCore_Mvc_MvcOptions_System_Action_AdCodicem_ValueObjects_AspNetCore_ValueObjectMessageOptions__}

Binds value objects from the underlying value everywhere MVC reads text, giving a refusal the message the
application's resource holds for its code.

```csharp
public static MvcOptions AddValueObjects(this MvcOptions options, Action<ValueObjectMessageOptions> configure)
```

#### Parameters

`options` [MvcOptions](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.mvcoptions)

MVC options.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action\-1)<[ValueObjectMessageOptions](AdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions.md)\>

Names the resource the messages are read from, through <xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions.UseResource%60%601" data-throw-if-not-resolved="false"></xref>.

#### Returns

 [MvcOptions](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.mvcoptions)

The same options, so calls can be chained.

#### Remarks

<p>
The binder is added as <xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMvcExtensions.AddValueObjects(Microsoft.AspNetCore.Mvc.MvcOptions)" data-throw-if-not-resolved="false"></xref> adds it, and a value it refuses in a route,
query, header or form value is reported with the message the resource holds under the key of the rule's code,
<code>ValueObject.value_object.too_short</code>, looked up after <code>ValueObject.value_object.out_of_range.minimum</code> or
<code>.maximum</code> for a type declaring one bound alone. It is formatted with the display name of the member as
<code>{0}</code>, and the arguments the type's rules supply from <code>{1}</code>: a length, both bounds, the known values.
The code under <code>errorCodes</code> is the same in every language, and a key the resource lacks keeps the English
message. Blank text keeps the framework's message, which MVC's own message provider localises.
</p>
<p>
The resource is read through the request's <code>IStringLocalizerFactory</code>: register
<code>builder.Services.AddLocalization()</code>, without which a refusal throws an <xref href="System.InvalidOperationException" data-throw-if-not-resolved="false"></xref>
naming it, and <code>app.UseRequestLocalization()</code>, so that the request chooses the culture. The current UI culture
chooses the template and the current culture formats the arguments. A template quoting an argument its code does
not supply throws its <xref href="System.FormatException" data-throw-if-not-resolved="false"></xref>, which is not caught.
</p>
<p>
A request body is read by the input formatter, which <xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMvcExtensions.AddValueObjects(Microsoft.Extensions.DependencyInjection.IMvcBuilder%2cSystem.Action%7bAdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions%7d)" data-throw-if-not-resolved="false"></xref>
puts in place: on the options alone, a body keeps its English message.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">options</code> or <code class="paramref">configure</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### AddValueObjects\(IMvcBuilder\) {#AdCodicem_ValueObjects_AspNetCore_ValueObjectMvcExtensions_AddValueObjects_Microsoft_Extensions_DependencyInjection_IMvcBuilder_}

Registers value object model binding and JSON serialization on an MVC builder.

```csharp
public static IMvcBuilder AddValueObjects(this IMvcBuilder builder)
```

#### Parameters

`builder` [IMvcBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.imvcbuilder)

MVC builder.

#### Returns

 [IMvcBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.imvcbuilder)

The same builder, so calls can be chained.

#### Remarks

<p>
It also puts a formatter of its own in place of MVC's System.Text.Json input formatter, at its index: one
deriving from it, which reads with the very serializer options the framework's formatter read with, and records
the code of a value object a request body refuses, so that <xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMvcExtensions.AddValueObjectProblemDetails(Microsoft.AspNetCore.Mvc.ApiBehaviorOptions)" data-throw-if-not-resolved="false"></xref> puts it in
the response under the JSON path, as it puts the code of a route or query value under the parameter. The model
state keeps the messages it keeps without it, whatever <xref href="Microsoft.AspNetCore.Mvc.JsonOptions.AllowInputFormatterExceptionMessages" data-throw-if-not-resolved="false"></xref>
says, unless messages are configured, through
<xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMvcExtensions.AddValueObjects(Microsoft.Extensions.DependencyInjection.IMvcBuilder%2cSystem.Action%7bAdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions%7d)" data-throw-if-not-resolved="false"></xref> or on the options, which replace the
message of the exception with the resource's under that option, its default. A change made to the JSON options
later on reaches it as it reaches the framework's. A System.Text.Json formatter the application built with
options of its own, or derived, is left as it is, and records no code.
</p>
<p>
The swap is a post-configuration of <xref href="Microsoft.AspNetCore.Mvc.MvcOptions" data-throw-if-not-resolved="false"></xref>. Removing the framework's formatter with
<code>InputFormatters.RemoveType&lt;SystemTextJsonInputFormatter&gt;()</code> still works in <code>AddMvcOptions</code>, in
<code>Configure&lt;MvcOptions&gt;</code> or in a post-configuration registered before this call; in one registered
after it, that finds this formatter in its place, which is not of that exact type: remove there every formatter
that is a <xref href="Microsoft.AspNetCore.Mvc.Formatters.SystemTextJsonInputFormatter" data-throw-if-not-resolved="false"></xref>, derived included.
</p>
<p>
Once Newtonsoft.Json reads the body, through <code>AddNewtonsoftJson()</code>, there is no such formatter to replace,
and a body records no code; <code>AddValueObjectsNewtonsoftJson()</code>, from
<code>AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson</code>, replaces Newtonsoft.Json's formatter instead.
</p>

### AddValueObjects\(IMvcBuilder, Action<ValueObjectMessageOptions\>\) {#AdCodicem_ValueObjects_AspNetCore_ValueObjectMvcExtensions_AddValueObjects_Microsoft_Extensions_DependencyInjection_IMvcBuilder_System_Action_AdCodicem_ValueObjects_AspNetCore_ValueObjectMessageOptions__}

Registers value object model binding and JSON serialization on an MVC builder, giving a refusal, in a route,
query, header or form value or in a JSON body, the message the application's resource holds for its code.

```csharp
public static IMvcBuilder AddValueObjects(this IMvcBuilder builder, Action<ValueObjectMessageOptions> configure)
```

#### Parameters

`builder` [IMvcBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.imvcbuilder)

MVC builder.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action\-1)<[ValueObjectMessageOptions](AdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions.md)\>

Names the resource the messages are read from, through <xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions.UseResource%60%601" data-throw-if-not-resolved="false"></xref>.

#### Returns

 [IMvcBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.imvcbuilder)

The same builder, so calls can be chained.

#### Remarks

<p>
Everything <xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMvcExtensions.AddValueObjects(Microsoft.Extensions.DependencyInjection.IMvcBuilder)" data-throw-if-not-resolved="false"></xref> registers is registered, and the messages are configured as
<xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMvcExtensions.AddValueObjects(Microsoft.AspNetCore.Mvc.MvcOptions%2cSystem.Action%7bAdCodicem.ValueObjects.AspNetCore.ValueObjectMessageOptions%7d)" data-throw-if-not-resolved="false"></xref> configures them, which says how they are
looked up. The input formatter reads them too: a value object a JSON body refuses is reported under its JSON path
with the message the resource holds for the code, quoting the name of the value object as <code>{0}</code>, since the
path may hold a refused dictionary key, the refused value itself. A type the registry cannot describe, such as
<code>AnyEntityId</code>, has the key of the code alone and no argument.
</p>
<p>
A body's message is translated only where MVC answers with it, under
<xref href="Microsoft.AspNetCore.Mvc.JsonOptions.AllowInputFormatterExceptionMessages" data-throw-if-not-resolved="false"></xref>, its default; turned off, every error of a body is
answered "The input was not valid.", which MVC's message provider holds. A body read by Newtonsoft.Json, through
<code>AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson</code>, keeps its English message.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">builder</code> or <code class="paramref">configure</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

