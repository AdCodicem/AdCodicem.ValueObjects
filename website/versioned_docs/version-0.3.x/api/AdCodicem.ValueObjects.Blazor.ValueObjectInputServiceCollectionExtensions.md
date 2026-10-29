# Class ValueObjectInputServiceCollectionExtensions {#AdCodicem_ValueObjects_Blazor_ValueObjectInputServiceCollectionExtensions}

Namespace: [AdCodicem.ValueObjects.Blazor](AdCodicem.ValueObjects.Blazor.md)  
Assembly: AdCodicem.ValueObjects.Blazor.dll  

Registers the options of the inputs of value objects.

```csharp
public static class ValueObjectInputServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectInputServiceCollectionExtensions](AdCodicem.ValueObjects.Blazor.ValueObjectInputServiceCollectionExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### AddValueObjectInputs\(IServiceCollection, Action<ValueObjectInputOptions\>\) {#AdCodicem_ValueObjects_Blazor_ValueObjectInputServiceCollectionExtensions_AddValueObjectInputs_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_AdCodicem_ValueObjects_Blazor_ValueObjectInputOptions__}

Configures the messages the inputs of value objects answer a refusal with, and registers the localization services
they are read through.

```csharp
public static IServiceCollection AddValueObjectInputs(this IServiceCollection services, Action<ValueObjectInputOptions> configure)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The application's services.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action\-1)<[ValueObjectInputOptions](AdCodicem.ValueObjects.Blazor.ValueObjectInputOptions.md)\>

Configures the options: <code>options =&gt; options.UseResource&lt;Messages&gt;()</code>.

#### Returns

 [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The services, so calls can be chained.

#### Remarks

An input needs no registration to read text through its value object's rules, and answers a refusal with the
English message of the rule without one. <code>AddLocalization()</code> keeps the localizer factory the application
registered first, and its <code>ResourcesPath</code>.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">services</code> or <code class="paramref">configure</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

