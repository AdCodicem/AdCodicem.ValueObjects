# Class ValueObjectModelBinder<TSelf, TValue\> {#AdCodicem_ValueObjects_AspNetCore_ModelBinding_ValueObjectModelBinder_2}

Namespace: [AdCodicem.ValueObjects.AspNetCore.ModelBinding](AdCodicem.ValueObjects.AspNetCore.ModelBinding.md)  
Assembly: AdCodicem.ValueObjects.AspNetCore.dll  

Binds a value object from the raw text of a route segment, query string value, header or form field.

```csharp
public sealed class ValueObjectModelBinder<TSelf, TValue> : IModelBinder where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectModelBinder<TSelf, TValue\>](AdCodicem.ValueObjects.AspNetCore.ModelBinding.ValueObjectModelBinder\-2.md)

#### Implements

[IModelBinder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.modelbinding.imodelbinder)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

The binder is closed over the concrete value object type, so binding costs one <code>TryParse</code> and nothing
else: no reflection, no <code>TypeDescriptor</code> lookup, and no exception on the rejection path. The violated
rule travels with the model error so that <xref href="AdCodicem.ValueObjects.AspNetCore.ValueObjectProblemDetails" data-throw-if-not-resolved="false"></xref> can put its stable code in
the response body.

## Methods

### BindModelAsync\(ModelBindingContext\) {#AdCodicem_ValueObjects_AspNetCore_ModelBinding_ValueObjectModelBinder_2_BindModelAsync_Microsoft_AspNetCore_Mvc_ModelBinding_ModelBindingContext_}

Attempts to bind a model.

```csharp
public Task BindModelAsync(ModelBindingContext bindingContext)
```

#### Parameters

`bindingContext` [ModelBindingContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.modelbinding.modelbindingcontext)

The <xref href="Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingContext" data-throw-if-not-resolved="false"></xref>.

#### Returns

 [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<p>
A <xref href="System.Threading.Tasks.Task" data-throw-if-not-resolved="false"></xref> which will complete when the model binding process completes.
</p>
<p>
If model binding was successful, the <xref href="Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingContext.Result" data-throw-if-not-resolved="false"></xref> should have
<xref href="Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult.IsModelSet" data-throw-if-not-resolved="false"></xref> set to <code>true</code>.
</p>
<p>
A model binder that completes successfully should set <xref href="Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingContext.Result" data-throw-if-not-resolved="false"></xref> to
a value returned from <xref href="Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult.Success(System.Object)" data-throw-if-not-resolved="false"></xref>.
</p>

