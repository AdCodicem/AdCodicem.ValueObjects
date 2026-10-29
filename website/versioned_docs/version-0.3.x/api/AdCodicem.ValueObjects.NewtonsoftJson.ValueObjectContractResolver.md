# Class ValueObjectContractResolver {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectContractResolver}

Namespace: [AdCodicem.ValueObjects.NewtonsoftJson](AdCodicem.ValueObjects.NewtonsoftJson.md)  
Assembly: AdCodicem.ValueObjects.NewtonsoftJson.dll  

Resolves each value object, and its nullable type, to a contract carrying <xref href="AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectConverter" data-throw-if-not-resolved="false"></xref>, and
every other type through the resolver it wraps.

```csharp
public sealed class ValueObjectContractResolver : IContractResolver
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectContractResolver](AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectContractResolver.md)

#### Implements

IContractResolver

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
It is for code that hands Newtonsoft.Json a contract resolver alone, with no serializer settings to add the
converter to: settings take <xref href="AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectJsonSerializerSettingsExtensions.AddValueObjects(Newtonsoft.Json.JsonSerializerSettings%2cSystem.Boolean)" data-throw-if-not-resolved="false"></xref> instead, and
neither that call nor <code>AddValueObjectsNewtonsoftJson()</code> wraps a resolver.
</p>
<p>
A type <xref href="AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectConverter.CanConvert(System.Type)" data-throw-if-not-resolved="false"></xref> claims, which <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.IsValueObject(System.Type)" data-throw-if-not-resolved="false"></xref>
tells, nullable or not, a construction of a generic value object and a value object written by hand included, gets a
<xref href="Newtonsoft.Json.Serialization.JsonStringContract" data-throw-if-not-resolved="false"></xref> of this resolver's own whose <xref href="Newtonsoft.Json.Serialization.JsonContract.Converter" data-throw-if-not-resolved="false"></xref> is the converter,
kept for the resolver's lifetime. A contract of the wrapped resolver that carries a converter already, as one a
<code>[JsonConverter]</code> on the type names, is answered as it is. The wrapped resolver's contracts are never changed:
its cache may be shared by every instance of its type, as <xref href="Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver" data-throw-if-not-resolved="false"></xref>'s is.
</p>
<p>
The contract of a type holding value objects stays the wrapped resolver's, and Newtonsoft.Json keeps the contract
of each of its members on it, from the first time a serializer reads or writes that member. A serializer over this
resolver and another over the same wrapped resolver therefore see each other's member contracts, in whichever order
they first run. Wrap a resolver no other serializer uses, as the parameterless constructor does: never MVC's default
one, which is a single instance shared by every set of MVC's serializer settings in the process.
</p>
<p>
A contract's converter wins over the converters the serializer settings name: an application's own converter for a
value object in <xref href="Newtonsoft.Json.JsonSerializerSettings.Converters" data-throw-if-not-resolved="false"></xref> loses to this one. Name it on the type with
<code>[JsonConverter]</code> instead.
</p>
<p>
Given to a JSON Patch document of Microsoft.AspNetCore.JsonPatch as its <code>ContractResolver</code>, it is what the
document converts a value with. JSON Patch reads the value again under settings of its own, which drop
<xref href="Newtonsoft.Json.DateParseHandling.None" data-throw-if-not-resolved="false"></xref> and <xref href="Newtonsoft.Json.FloatParseHandling.Decimal" data-throw-if-not-resolved="false"></xref>, and replaces the converter's
refusal with a message of its own, which quotes the value and carries no code. In MVC,
<code>ValueObjectJsonPatchExtensions.TryApplyTo</code>, in AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson, reads every
value through the rules, with their codes, and needs no resolver.
</p>

## Constructors

### ValueObjectContractResolver\(\) {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectContractResolver__ctor}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectContractResolver" data-throw-if-not-resolved="false"></xref> class, wrapping a new
<xref href="Newtonsoft.Json.Serialization.DefaultContractResolver" data-throw-if-not-resolved="false"></xref> of its own.

```csharp
public ValueObjectContractResolver()
```

### ValueObjectContractResolver\(IContractResolver\) {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectContractResolver__ctor_Newtonsoft_Json_Serialization_IContractResolver_}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.NewtonsoftJson.ValueObjectContractResolver" data-throw-if-not-resolved="false"></xref> class, wrapping a resolver, which
every type but a value object keeps.

```csharp
public ValueObjectContractResolver(IContractResolver inner)
```

#### Parameters

`inner` IContractResolver

The resolver of every other type.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">inner</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

## Properties

### Inner {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectContractResolver_Inner}

Gets the resolver it wraps.

```csharp
public IContractResolver Inner { get; }
```

#### Property Value

 IContractResolver

## Methods

### ResolveContract\(Type\) {#AdCodicem_ValueObjects_NewtonsoftJson_ValueObjectContractResolver_ResolveContract_System_Type_}

Resolves the contract for a given type.

```csharp
public JsonContract ResolveContract(Type type)
```

#### Parameters

`type` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The type to resolve a contract for.

#### Returns

 JsonContract

The contract for a given type.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">type</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

