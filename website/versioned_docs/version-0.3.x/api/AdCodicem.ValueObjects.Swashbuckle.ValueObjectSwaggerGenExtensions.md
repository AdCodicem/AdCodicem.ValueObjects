# Class ValueObjectSwaggerGenExtensions {#AdCodicem_ValueObjects_Swashbuckle_ValueObjectSwaggerGenExtensions}

Namespace: [AdCodicem.ValueObjects.Swashbuckle](AdCodicem.ValueObjects.Swashbuckle.md)  
Assembly: AdCodicem.ValueObjects.Swashbuckle.dll  

Wires value object support into Swashbuckle's document generation.

```csharp
public static class ValueObjectSwaggerGenExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectSwaggerGenExtensions](AdCodicem.ValueObjects.Swashbuckle.ValueObjectSwaggerGenExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### AddValueObjects\(SwaggerGenOptions\) {#AdCodicem_ValueObjects_Swashbuckle_ValueObjectSwaggerGenExtensions_AddValueObjects_Swashbuckle_AspNetCore_SwaggerGen_SwaggerGenOptions_}

Documents value objects as their underlying types, with the rules declared on them.

```csharp
public static SwaggerGenOptions AddValueObjects(this SwaggerGenOptions options)
```

#### Parameters

`options` [SwaggerGenOptions](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/main/src/Swashbuckle.AspNetCore.SwaggerGen/DependencyInjection/SwaggerGenOptions.cs)

Swashbuckle's options, from <code>AddSwaggerGen</code>.

#### Returns

 [SwaggerGenOptions](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/main/src/Swashbuckle.AspNetCore.SwaggerGen/DependencyInjection/SwaggerGenOptions.cs)

The same options, so calls can be chained.

#### Remarks

<p>
Swashbuckle describes a type from its public properties, blind to the converter a value object travels through,
so a value object would otherwise be documented as an object with a <code>value</code> property, the very wrapper the
value object keeps off the wire. A schema filter describes it as its underlying type instead, with the pattern,
bounds, lengths, example and accepted values declared on it, as AdCodicem.ValueObjects.OpenApi describes it on the
built-in stack, and a parameter filter makes a minimal API's route, query or header parameter refer to its
component, as an MVC action's already does. A value object written by hand, a construction of a generic value
object, and the value object a <code>MapType</code> maps are described too.
</p>
<p>
The examples and known values are written as the minimal API options of System.Text.Json write them. A number
System.Text.Json writes as a JSON number is documented as Swashbuckle documents the underlying type in the same
document, as a number, whatever those options let be read from text; a 128-bit integer, which travels as a JSON
string, stays a string. A nullable member, element or dictionary value is described in place with <code>null</code>
allowed, as Swashbuckle describes a nullable primitive, though with the rules of its type alone, as Swashbuckle
describes the non-nullable one, and the enumeration of a closed set is named after its component, under the
identifier <code>CustomSchemaIds</code> gives it when an application chose one.
</p>
<p>
Call it after <code>IncludeXmlComments</code>: Swashbuckle runs its schema filters in the order they were added, and its
XML comments filter would otherwise replace the description of a value object with the type's summary, losing the
sentence that states the bounds of a value written as text. Called twice, it adds each filter once, and the
options of the last call win.
</p>

### AddValueObjects\(SwaggerGenOptions, JsonSerializerOptions\) {#AdCodicem_ValueObjects_Swashbuckle_ValueObjectSwaggerGenExtensions_AddValueObjects_Swashbuckle_AspNetCore_SwaggerGen_SwaggerGenOptions_System_Text_Json_JsonSerializerOptions_}

Documents value objects as their underlying types, with the rules declared on them, writing their examples and
known values with the given serializer options rather than with the minimal API ones.

```csharp
public static SwaggerGenOptions AddValueObjects(this SwaggerGenOptions options, JsonSerializerOptions serializerOptions)
```

#### Parameters

`options` [SwaggerGenOptions](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/main/src/Swashbuckle.AspNetCore.SwaggerGen/DependencyInjection/SwaggerGenOptions.cs)

Swashbuckle's options, from <code>AddSwaggerGen</code>.

`serializerOptions` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

The options the application writes value objects with, for an application whose
    MVC and minimal API options differ.

#### Returns

 [SwaggerGenOptions](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/main/src/Swashbuckle.AspNetCore.SwaggerGen/DependencyInjection/SwaggerGenOptions.cs)

The same options, so calls can be chained.

#### Remarks

A number System.Text.Json writes as a JSON number is still documented as a number, whatever
<xref href="System.Text.Json.JsonSerializerOptions.NumberHandling" data-throw-if-not-resolved="false"></xref> says, as Swashbuckle documents the underlying type. Everything
else is as <xref href="AdCodicem.ValueObjects.Swashbuckle.ValueObjectSwaggerGenExtensions.AddValueObjects(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions)" data-throw-if-not-resolved="false"></xref> says.

