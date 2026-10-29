# Class ValueObjectNewtonsoftJsonMvcExtensions {#AdCodicem_ValueObjects_AspNetCore_NewtonsoftJson_ValueObjectNewtonsoftJsonMvcExtensions}

Namespace: [AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson](AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.md)  
Assembly: AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.dll  

Wires value object support into an MVC application whose request bodies Newtonsoft.Json reads.

```csharp
public static class ValueObjectNewtonsoftJsonMvcExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectNewtonsoftJsonMvcExtensions](AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.ValueObjectNewtonsoftJsonMvcExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### AddValueObjectsNewtonsoftJson\(IMvcBuilder\) {#AdCodicem_ValueObjects_AspNetCore_NewtonsoftJson_ValueObjectNewtonsoftJsonMvcExtensions_AddValueObjectsNewtonsoftJson_Microsoft_Extensions_DependencyInjection_IMvcBuilder_}

Registers value object model binding and serialization on an MVC builder that reads with Newtonsoft.Json, and
records the code of the rule a value refused inside a request body broke.

```csharp
public static IMvcBuilder AddValueObjectsNewtonsoftJson(this IMvcBuilder builder)
```

#### Parameters

`builder` [IMvcBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.imvcbuilder)

MVC builder, on which <code>AddNewtonsoftJson()</code> is called, before or after this.

#### Returns

 [IMvcBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.imvcbuilder)

The same builder, so calls can be chained.

#### Remarks

<p>
It calls <xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMvcExtensions.AddValueObjects(Microsoft.Extensions.DependencyInjection.IMvcBuilder)" data-throw-if-not-resolved="false"></xref> first, so that value objects bind
from the route, the query string and the headers, and one call sets everything up; an application that calls
that method as well, before or after, behaves the same.
</p>
<p>
It adds <xref href="AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectConverter" data-throw-if-not-resolved="false"></xref> to MVC's <xref href="Microsoft.AspNetCore.Mvc.MvcNewtonsoftJsonOptions.SerializerSettings" data-throw-if-not-resolved="false"></xref>,
with <xref href="Newtonsoft.Json.DateParseHandling.None" data-throw-if-not-resolved="false"></xref> and <xref href="Newtonsoft.Json.FloatParseHandling.Decimal" data-throw-if-not-resolved="false"></xref>, as
<xref href="AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectJsonSerializerSettingsExtensions.AddValueObjects(Newtonsoft.Json.JsonSerializerSettings%2cSystem.Boolean)" data-throw-if-not-resolved="false"></xref> does, once every configuration of those
options has run, and only when the settings hold no such converter yet: settings the application gave one are its
own, and are left as it left them. Without the converter, Newtonsoft.Json reads a
value object through its type converter, whose refusal names no rule and quotes the refused text.
</p>
<p>
Those settings are the ones MVC writes every response with as well, and they apply to every member of every
body. An application that ran without the converter changes its wire: a numeric or boolean value object is
answered <code>7</code> or <code>true</code>, where the type converter wrote <code>"7"</code> or <code>"True"</code>, and a boolean
value object sent as a string is refused. A member of type <xref href="System.Object" data-throw-if-not-resolved="false"></xref> or <code>JToken</code> keeps a string
that looks like a date as a string, and reads a number with a fraction as a <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, which refuses
one beyond its range.
</p>
<p>
It then puts a formatter of its own in place of MVC's Newtonsoft.Json input formatter, at its index: one deriving
from it, built over the same serializer settings and the same <xref href="Microsoft.AspNetCore.Mvc.MvcNewtonsoftJsonOptions" data-throw-if-not-resolved="false"></xref>, with the
media types and encodings the application gave the formatter it replaces. It reads a body as the framework's
does, and records the code of each value object the body refuses under the key of its model state error, so that
<xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMvcExtensions.AddValueObjectProblemDetails(Microsoft.AspNetCore.Mvc.ApiBehaviorOptions)" data-throw-if-not-resolved="false"></xref> puts it in the response beside the message.
Newtonsoft.Json reads on past a refused member of an object whose properties it sets, so each refused member
keeps its own code, until the model state holds as many errors as
<xref href="Microsoft.AspNetCore.Mvc.MvcOptions.MaxModelValidationErrors" data-throw-if-not-resolved="false"></xref> lets it. The model state, the logs and the exceptions that
propagate are the framework's, whatever <xref href="Microsoft.AspNetCore.Mvc.MvcNewtonsoftJsonOptions.AllowInputFormatterExceptionMessages" data-throw-if-not-resolved="false"></xref>
says when the request is read. A Newtonsoft.Json formatter the application built over settings of its own, or
derived, the JSON Patch formatter among them, is left as it is, and records no code: a JSON Patch document records
the codes of its values when the action applies it through <code>TryApplyTo</code>, of
<xref href="AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.ValueObjectJsonPatchExtensions" data-throw-if-not-resolved="false"></xref>.
</p>
<p>
The swap is a post-configuration of <xref href="Microsoft.AspNetCore.Mvc.MvcOptions" data-throw-if-not-resolved="false"></xref>. In a post-configuration registered after this call,
<code>InputFormatters.RemoveType&lt;NewtonsoftJsonInputFormatter&gt;()</code> finds this formatter in its place, which
is not of that exact type: remove there every formatter that is a
<xref href="Microsoft.AspNetCore.Mvc.Formatters.NewtonsoftJsonInputFormatter" data-throw-if-not-resolved="false"></xref> but neither a
<xref href="Microsoft.AspNetCore.Mvc.Formatters.NewtonsoftJsonPatchInputFormatter" data-throw-if-not-resolved="false"></xref>, which derives from it and
which that call keeps, nor a formatter the application derived itself.
</p>
<p>
It does not call <code>AddNewtonsoftJson()</code>, which is the application's choice of serializer. Without it MVC
reads bodies with System.Text.Json, whose formatter <xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMvcExtensions.AddValueObjects(Microsoft.Extensions.DependencyInjection.IMvcBuilder)" data-throw-if-not-resolved="false"></xref>
already replaces, and this call amounts to that one.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">builder</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

