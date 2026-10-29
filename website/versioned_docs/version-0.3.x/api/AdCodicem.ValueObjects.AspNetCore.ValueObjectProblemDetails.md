# Class ValueObjectProblemDetails {#AdCodicem_ValueObjects_AspNetCore_ValueObjectProblemDetails}

Namespace: [AdCodicem.ValueObjects.AspNetCore](AdCodicem.ValueObjects.AspNetCore.md)  
Assembly: AdCodicem.ValueObjects.AspNetCore.Http.dll  

Names the problem details member that maps each rejected member to the code of the rule it broke, and carries the
violated rule from the MVC model binder to the problem details response.

```csharp
public static class ValueObjectProblemDetails
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectProblemDetails](AdCodicem.ValueObjects.AspNetCore.ValueObjectProblemDetails.md)

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
<code>ModelStateDictionary</code> only holds a message, and a message is not something a client can branch on. The
stable error code rides alongside it on the request, and <code>AddValueObjectProblemDetails()</code> on
<code>ApiBehaviorOptions</code>, in <code>AdCodicem.ValueObjects.AspNetCore</code>, folds it into the RFC 9457 body as an
<code>errorCodes</code> extension. A minimal API endpoint covered by
<xref href="AdCodicem.ValueObjects.AspNetCore.Http.ValueObjectEndpointConventionBuilderExtensions.WithValueObjectProblemDetails%60%601(%60%600)" data-throw-if-not-resolved="false"></xref> writes the same
member, so both answer a refused value object with one shape, and so does an Azure Functions HTTP trigger, through
<code>UseValueObjectProblemDetails()</code>.
</p>
<p>
The type lives in <code>AdCodicem.ValueObjects.AspNetCore.Http</code>, which every application on ASP.NET Core can
reference, native AOT included, and keeps its namespace: <code>AdCodicem.ValueObjects.AspNetCore</code> references that
package and forwards the type, so code compiled against either assembly finds it.
</p>

## Fields

### ExtensionName {#AdCodicem_ValueObjects_AspNetCore_ValueObjectProblemDetails_ExtensionName}

The extension member added to the problem details body, mapping each rejected member to its error code.

```csharp
public const string ExtensionName = "errorCodes"
```

#### Field Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### GetErrorCodes\(HttpContext?\) {#AdCodicem_ValueObjects_AspNetCore_ValueObjectProblemDetails_GetErrorCodes_Microsoft_AspNetCore_Http_HttpContext_}

Gets the codes recorded for the current request.

```csharp
public static IReadOnlyDictionary<string, string> GetErrorCodes(HttpContext? httpContext)
```

#### Parameters

`httpContext` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)?

Current request.

#### Returns

 [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary\-2)<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)\>

The recorded codes, or an empty dictionary.

### RecordErrorCode\(HttpContext?, string, string\) {#AdCodicem_ValueObjects_AspNetCore_ValueObjectProblemDetails_RecordErrorCode_Microsoft_AspNetCore_Http_HttpContext_System_String_System_String_}

Records the code of the rule a bound value violated.

```csharp
public static void RecordErrorCode(HttpContext? httpContext, string memberName, string errorCode)
```

#### Parameters

`httpContext` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)?

Current request.

`memberName` [string](https://learn.microsoft.com/dotnet/api/system.string)

Name of the rejected model member.

`errorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)

Stable code of the violated rule.

#### Remarks

A member recorded again keeps its first code, the one beside the first of its errors: MVC binds every element of a
query array under the array's name, and a JSON body may repeat a key, so a member can be refused more than once.
A minimal API's problem details keep the first code the same way.

