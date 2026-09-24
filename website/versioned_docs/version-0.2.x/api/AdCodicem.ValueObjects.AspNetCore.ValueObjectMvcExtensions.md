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

The provider is inserted first so that it wins over the built-in simple-type and complex-type binders,
which would otherwise try to bind a value object as an object with a <code>Value</code> property.

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

