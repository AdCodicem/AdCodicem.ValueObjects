# Class ValueObjectOpenApiExtensions {#AdCodicem_ValueObjects_OpenApi_ValueObjectOpenApiExtensions}

Namespace: [AdCodicem.ValueObjects.OpenApi](AdCodicem.ValueObjects.OpenApi.md)  
Assembly: AdCodicem.ValueObjects.OpenApi.dll  

Wires value object support into OpenAPI document generation.

```csharp
public static class ValueObjectOpenApiExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectOpenApiExtensions](AdCodicem.ValueObjects.OpenApi.ValueObjectOpenApiExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### AddValueObjects\(OpenApiOptions\) {#AdCodicem_ValueObjects_OpenApi_ValueObjectOpenApiExtensions_AddValueObjects_Microsoft_AspNetCore_OpenApi_OpenApiOptions_}

Documents value objects as their underlying types.

```csharp
public static OpenApiOptions AddValueObjects(this OpenApiOptions options)
```

#### Parameters

`options` [OpenApiOptions](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.openapi.openapioptions)

OpenAPI options, from <code>AddOpenApi</code>.

#### Returns

 [OpenApiOptions](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.openapi.openapioptions)

The same options, so calls can be chained.

