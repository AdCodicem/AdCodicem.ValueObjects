# Class ValueObjectProblemDetails {#AdCodicem_ValueObjects_AspNetCore_ValueObjectProblemDetails}

Namespace: [AdCodicem.ValueObjects.AspNetCore](AdCodicem.ValueObjects.AspNetCore.md)  
Assembly: AdCodicem.ValueObjects.AspNetCore.dll  

Carries the violated rule from the model binder to the problem details response.

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

<code>ModelStateDictionary</code> only holds a message, and a message is not something a client can branch on. The
stable error code rides alongside it on the request, and
<xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectMvcExtensions.AddValueObjectProblemDetails(Microsoft.AspNetCore.Mvc.ApiBehaviorOptions)" data-throw-if-not-resolved="false"></xref> folds it into the RFC 9457 body as an
<code>errorCodes</code> extension.

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

