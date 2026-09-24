# Class ValueObjectModelBinderProvider {#AdCodicem_ValueObjects_AspNetCore_ModelBinding_ValueObjectModelBinderProvider}

Namespace: [AdCodicem.ValueObjects.AspNetCore.ModelBinding](AdCodicem.ValueObjects.AspNetCore.ModelBinding.md)  
Assembly: AdCodicem.ValueObjects.AspNetCore.dll  

Supplies the model binder of any value object.

```csharp
public sealed class ValueObjectModelBinderProvider : IModelBinderProvider
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectModelBinderProvider](AdCodicem.ValueObjects.AspNetCore.ModelBinding.ValueObjectModelBinderProvider.md)

#### Implements

[IModelBinderProvider](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.modelbinding.imodelbinderprovider)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

Providers are consulted once per action parameter while the application starts, so closing the generic
binder over the concrete types here costs nothing per request. The instances are cached anyway, because MVC
creates metadata for the same type in many places.

## Methods

### GetBinder\(ModelBinderProviderContext\) {#AdCodicem_ValueObjects_AspNetCore_ModelBinding_ValueObjectModelBinderProvider_GetBinder_Microsoft_AspNetCore_Mvc_ModelBinding_ModelBinderProviderContext_}

Creates a <xref href="Microsoft.AspNetCore.Mvc.ModelBinding.IModelBinder" data-throw-if-not-resolved="false"></xref> based on <xref href="Microsoft.AspNetCore.Mvc.ModelBinding.ModelBinderProviderContext" data-throw-if-not-resolved="false"></xref>.

```csharp
public IModelBinder? GetBinder(ModelBinderProviderContext context)
```

#### Parameters

`context` [ModelBinderProviderContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.modelbinding.modelbinderprovidercontext)

The <xref href="Microsoft.AspNetCore.Mvc.ModelBinding.ModelBinderProviderContext" data-throw-if-not-resolved="false"></xref>.

#### Returns

 [IModelBinder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.modelbinding.imodelbinder)?

An <xref href="Microsoft.AspNetCore.Mvc.ModelBinding.IModelBinder" data-throw-if-not-resolved="false"></xref>.

