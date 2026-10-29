# Class ValueObjectSchemaTransformer {#AdCodicem_ValueObjects_OpenApi_ValueObjectSchemaTransformer}

Namespace: [AdCodicem.ValueObjects.OpenApi](AdCodicem.ValueObjects.OpenApi.md)  
Assembly: AdCodicem.ValueObjects.OpenApi.dll  

Documents a value object as its underlying type rather than as an object with a <code>Value</code> property.

```csharp
public sealed class ValueObjectSchemaTransformer : IOpenApiSchemaTransformer
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectSchemaTransformer](AdCodicem.ValueObjects.OpenApi.ValueObjectSchemaTransformer.md)

#### Implements

[IOpenApiSchemaTransformer](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.openapi.iopenapischematransformer)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
Schema generation is driven by <code>JsonTypeInfo</code>, and a type carrying a custom converter is opaque to it,
so a value object would otherwise appear as an empty schema. This transformer fills it in from the
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectSchema" data-throw-if-not-resolved="false"></xref> the generator captured, which means the pattern, bounds, length and accepted
values published in the document are literally the ones the type enforces — they cannot drift. What each rule
becomes is shared with <code>ValueObjectJsonSchema</code>, in AdCodicem.ValueObjects.Json, which describes a value object
alike in a JSON Schema System.Text.Json exports, and the whole description of a value object with the Swashbuckle
filters of AdCodicem.ValueObjects.Swashbuckle, which build on the same object model.
</p>
<p>
A value object written by hand is documented as well, from the schema it declares, through the descriptor the
registry builds by reflection when nothing registered the type, as the model binder and the FluentValidation rules
resolve it. It runs while the document is built, not per request.
</p>
<p>
A value object over a number honours <xref href="System.Text.Json.JsonSerializerOptions.NumberHandling" data-throw-if-not-resolved="false"></xref> as the built-in converter of
its underlying type does, and is documented as System.Text.Json documents that type under the same options: as a
number or a string matching a numeric pattern when a number may be written or read as text, and, over a
<xref href="System.Double" data-throw-if-not-resolved="false"></xref> or a <xref href="System.Single" data-throw-if-not-resolved="false"></xref>, with the named literals its bounds let through.
</p>
<p>
A value object is described wherever it appears: as a route, query or header parameter, which ASP.NET Core hands
over as text, with the value object's schema in place; as the element of a collection or the value of a
dictionary, which System.Text.Json leaves out, with a reference to its component; and as the key of a dictionary,
in <code>propertyNames</code>, as the text the key is written in.
</p>
<p>
Text a member or a parameter holds to a value object's rules under <xref href="AdCodicem.ValueObjects.Annotations.ParsesAsAttribute" data-throw-if-not-resolved="false"></xref> is described as
that text: a string with the value object's pattern, lengths, format, bounds, example and known values, whatever its
underlying type, and each element of a collection of such text. The attribute is read on the property, on the
constructor parameter of the same name, where MVC requires it on a record, and on the parameter of a minimal API or
an MVC action, a member of a model MVC binds from the query string included. A rule the declaration states as well,
such as <code>[StringLength]</code>, is kept where it is the stricter.
</p>
<p>
A closed value set lists its values in <code>enum</code>, and their names, the names of the known values, in the
<code>x-enum-varnames</code>, <code>x-enumNames</code> and <code>x-ms-enum</code> extensions, which client generators name the
members of their enumeration after; <code>x-ms-enum</code> carries the description of each value declared with one.
</p>

## Methods

### TransformAsync\(OpenApiSchema, OpenApiSchemaTransformerContext, CancellationToken\) {#AdCodicem_ValueObjects_OpenApi_ValueObjectSchemaTransformer_TransformAsync_Microsoft_OpenApi_OpenApiSchema_Microsoft_AspNetCore_OpenApi_OpenApiSchemaTransformerContext_System_Threading_CancellationToken_}

Transforms the specified OpenAPI schema.

```csharp
public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
```

#### Parameters

`schema` [OpenApiSchema](https://github.com/microsoft/OpenAPI.NET/blob/main/src/Microsoft.OpenApi/Models/OpenApiSchema.cs)

The <xref href="Microsoft.OpenApi.OpenApiSchema" data-throw-if-not-resolved="false"></xref> to modify.

`context` [OpenApiSchemaTransformerContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.openapi.openapischematransformercontext)

The <xref href="Microsoft.AspNetCore.OpenApi.OpenApiSchemaTransformerContext" data-throw-if-not-resolved="false"></xref> associated with the <see paramref="schema"></see>.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

The cancellation token to use.

#### Returns

 [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

The task object representing the asynchronous operation.

