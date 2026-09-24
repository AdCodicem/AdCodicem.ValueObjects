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
values published in the document are literally the ones the type enforces — they cannot drift.
</p>
<p>
It runs while the document is built, not per request.
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

