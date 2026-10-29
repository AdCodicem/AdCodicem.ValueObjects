# Class ValueObjectJsonPatchExtensions {#AdCodicem_ValueObjects_AspNetCore_NewtonsoftJson_ValueObjectJsonPatchExtensions}

Namespace: [AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson](AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.md)  
Assembly: AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.dll  

Applies JSON Patch documents whose values each value object reads through its rules.

```csharp
public static class ValueObjectJsonPatchExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectJsonPatchExtensions](AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.ValueObjectJsonPatchExtensions.md)

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
MVC reads a <xref href="Microsoft.AspNetCore.JsonPatch.JsonPatchDocument%601" data-throw-if-not-resolved="false"></xref> body through its JSON Patch formatter, which holds each value as
JSON until the document is applied. JSON Patch's own <code>ApplyTo</code> then converts it by writing it to text and
reading it back under settings of its own, and reports any refusal in its own words, which quote the value and name
no rule; MVC's <code>ApplyTo(objectToApplyTo, ModelState)</code> puts every error under the type name of the object. These
methods apply the same document through the value objects' rules, with their messages and codes.
</p>
<p>
<code>add</code>, <code>replace</code> and <code>test</code> read a value into a value object member, list element or dictionary value
as the document holds it, a string, a number, a boolean, a <code>null</code> or a <code>JToken</code>, never written to text
and read again: through <xref href="AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectConverter" data-throw-if-not-resolved="false"></xref>, under the settings
<xref href="AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectJsonSerializerSettingsExtensions.AddValueObjects(Newtonsoft.Json.JsonSerializerSettings%2cSystem.Boolean)" data-throw-if-not-resolved="false"></xref> gives it and the document's contract
resolver. Its digits, its offset and its text are the client's when MVC read the document under those settings,
which it does once <code>AddValueObjectsNewtonsoftJson()</code> is in place. A refused value is reported with the
converter's message, which names the type and the rule and never the value, and the code of the rule; a <code>null</code>
read into a value object that cannot be <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> is <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref>. An
instance of the value object, in a document built in code or taken from the object by <code>copy</code> or <code>move</code>,
is kept as it is, but for a default instance its type refuses, which no writer writes either.
</p>
<p>
A dictionary keyed by a value object reads each key through the value object's rules, from its text in the invariant
culture, as a route value is read: a refused key reads "The key is not a valid CountryCode: …", with its code.
</p>
<p>
A <code>test</code> of a value object compares through the value object's equality, so <code>12.5</code> matches a stored
<code>12.50</code>, an instant matches the same instant at another offset, and a closed set compared ignoring case matches
another case. One that does not match names neither value, and carries no code.
</p>
<p>
A value object member that cannot be <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> cannot be removed, which JSON Patch would do by setting
it to its default instance: <code>remove</code> is refused with <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref>, and so is a
<code>move</code> from it, which then adds nothing at its target. Declare the member <code>T?</code> to remove it. A <code>move</code>
to the location it comes from changes nothing.
</p>
<p>
A member of another type, a value object inside an object value, which JSON Patch converts as a whole, and every
error no value explains are JSON Patch's, with its messages, which may quote the value. An operation that names no
<code>path</code>, or a <code>copy</code> or a <code>move</code> that names no <code>from</code>, is an error, where JSON Patch throws, and
so is <code>-</code>, the end of a list, where it holds no element to read, remove, replace or test.
</p>
<p>
Every operation is applied in turn, after one that failed too, as MVC's <code>ApplyTo</code> does: on
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a> the object is partly patched, and is not to be saved. JSON Patch stops at an operation it
cannot read, an unknown <code>op</code> or a path with a bad escape, which is reported. Nothing is thrown for what the client
sent; the messages are English, as those of a body Newtonsoft.Json reads.
</p>

## Methods

### TryApplyTo<TModel\>\(JsonPatchDocument<TModel\>, TModel, ModelStateDictionary, HttpContext, string?\) {#AdCodicem_ValueObjects_AspNetCore_NewtonsoftJson_ValueObjectJsonPatchExtensions_TryApplyTo__1_Microsoft_AspNetCore_JsonPatch_JsonPatchDocument___0____0_Microsoft_AspNetCore_Mvc_ModelBinding_ModelStateDictionary_Microsoft_AspNetCore_Http_HttpContext_System_String_}

Applies the operations and records each error in the model state under the operation's path, with the code of
the rule a value broke for the problem details.

```csharp
public static bool TryApplyTo<TModel>(this JsonPatchDocument<TModel> patchDocument, TModel objectToApplyTo, ModelStateDictionary modelState, HttpContext httpContext, string? prefix = null) where TModel : class
```

#### Parameters

`patchDocument` [JsonPatchDocument](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.jsonpatch.jsonpatchdocument\-1)<TModel\>

The document, as MVC read it.

`objectToApplyTo` TModel

The object it patches.

`modelState` [ModelStateDictionary](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.modelbinding.modelstatedictionary)

Where each error is recorded.

`httpContext` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

The request the codes are recorded on, for <code>AddValueObjectProblemDetails()</code> or
<xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectProblemDetails.GetErrorCodes(Microsoft.AspNetCore.Http.HttpContext)" data-throw-if-not-resolved="false"></xref>.

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Put before the path of each error's key, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when every operation applied.

#### Type Parameters

`TModel` 

The type the document patches.

#### Remarks

<p>
Each error is keyed by the operation's <code>path</code>, as the client sent it, <code>/quantity</code> or <code>/ibans/-</code>,
after <code class="paramref">prefix</code>, joined with no separator since the path starts with <code>/</code>: <code>patch</code> gives
<code>patch/quantity</code>. A <code>copy</code> or a <code>move</code> refused at its source, a <code>from</code> JSON Patch cannot read
included, is keyed by its <code>from</code>, and an operation that names no <code>path</code> by the prefix alone. A key
refused more than once keeps every message and its first code, as
<xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectProblemDetails.RecordErrorCode(Microsoft.AspNetCore.Http.HttpContext%2cSystem.String%2cSystem.String)" data-throw-if-not-resolved="false"></xref> keeps it, and an error past
<xref href="Microsoft.AspNetCore.Mvc.MvcOptions.MaxModelValidationErrors" data-throw-if-not-resolved="false"></xref> records no code, so that every code has its message.
</p>
<p>
The codes reach the response <xref href="Microsoft.AspNetCore.Mvc.ApiBehaviorOptions.InvalidModelStateResponseFactory" data-throw-if-not-resolved="false"></xref> builds, to which
<code>AddValueObjectProblemDetails()</code> adds them, when the action answers with it: the automatic 400 runs before
the action, and never sees the errors recorded here. An action answering with <code>ValidationProblem()</code> goes
through MVC's <code>ProblemDetailsFactory</code> instead, and passes them itself, in its <code>extensions</code>:
<xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectProblemDetails.GetErrorCodes(Microsoft.AspNetCore.Http.HttpContext)" data-throw-if-not-resolved="false"></xref> of the request, under
<xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectProblemDetails.ExtensionName" data-throw-if-not-resolved="false"></xref>.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">patchDocument</code>, <code class="paramref">objectToApplyTo</code>, <code class="paramref">modelState</code> or
<code class="paramref">httpContext</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### TryApplyTo<TModel\>\(JsonPatchDocument<TModel\>, TModel, Action<JsonPatchError, string?\>\) {#AdCodicem_ValueObjects_AspNetCore_NewtonsoftJson_ValueObjectJsonPatchExtensions_TryApplyTo__1_Microsoft_AspNetCore_JsonPatch_JsonPatchDocument___0____0_System_Action_Microsoft_AspNetCore_JsonPatch_JsonPatchError_System_String__}

Applies the operations and hands each error the code of the rule a value broke, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

```csharp
public static bool TryApplyTo<TModel>(this JsonPatchDocument<TModel> patchDocument, TModel objectToApplyTo, Action<JsonPatchError, string?> logError) where TModel : class
```

#### Parameters

`patchDocument` [JsonPatchDocument](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.jsonpatch.jsonpatchdocument\-1)<TModel\>

The document.

`objectToApplyTo` TModel

The object it patches.

`logError` [Action](https://learn.microsoft.com/dotnet/api/system.action\-2)<[JsonPatchError](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.jsonpatch.jsonpatcherror), [string](https://learn.microsoft.com/dotnet/api/system.string)?\>

Called with each error, and the code of the rule a value broke, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for an error no rule
explains.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when every operation applied.

#### Type Parameters

`TModel` 

The type the document patches.

#### Remarks

The error's <xref href="Microsoft.AspNetCore.JsonPatch.JsonPatchError.Operation" data-throw-if-not-resolved="false"></xref> is the operation that failed. A document read outside MVC is
best read under <code>new JsonSerializerSettings().AddValueObjects()</code>: under Newtonsoft.Json's defaults, a string
that looks like a date and the digits of a <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref> are lost when the document is read, before any
value object sees them, and the first is refused, with
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable" data-throw-if-not-resolved="false"></xref>, where it no longer says what the text said
(<xref href="Newtonsoft.Json.DateParseHandling" data-throw-if-not-resolved="false"></xref>).

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">patchDocument</code>, <code class="paramref">objectToApplyTo</code> or <code class="paramref">logError</code> is
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

