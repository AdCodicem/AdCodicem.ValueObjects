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
binder over the concrete types here costs nothing per request. The instances are cached anyway, by each provider,
because MVC creates metadata for the same type in many places. Each binder reads the messages of its provider, which
<code>AddValueObjects(messages =&gt; …)</code> configures, when it refuses a value.

<p>
Each binder is closed over its value object at compile time, through the type arguments the descriptor hands back
to a visitor (<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.Accept%60%601(AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>), never at run time
with <xref href="System.Type.MakeGenericType(System.Type%5b%5d)" data-throw-if-not-resolved="false"></xref>. The registry still describes by reflection a value object nothing
registered, one written by hand or a construction of a generic one, the first time it is asked for it.
</p>
<p>
A greedy binding source is declined: a header (<code>[FromHeader]</code>), the body (<code>[FromBody]</code>), the services
(<code>[FromServices]</code>) or a binder the application names (<code>[ModelBinder(typeof(…))]</code>). MVC binds each through a
provider of its own, which this one, inserted first, would otherwise shadow, and does not narrow the value providers
to a greedy source, so the binder would read a route, query or form value of the same name instead. MVC's header
binder asks for this binder again, under <xref href="Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.ModelBinding" data-throw-if-not-resolved="false"></xref>, and hands it the header alone.
</p>

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

