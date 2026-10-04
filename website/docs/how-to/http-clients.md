---
title: Call an API With Refit, Kiota or NSwag
sidebar_label: HTTP clients
slug: /how-to/http-clients
description: Call an API whose contract holds value objects with Refit, and generate a client from its OpenAPI document with Kiota or NSwag, mapping the components back to the value objects.
---

# Call an API with Refit, Kiota or NSwag

A client in the same solution as the API can share its value objects. A client generated from the
[OpenAPI document](./openapi.md) gets what the document describes, the underlying types and their rules, and can map
them back.

## Refit

Refit works as is. Path and query values are formatted as the underlying text, a `[Query]` object flattens each value
object to one key, and bodies go through the generated JSON converter. A response holding a value the type rejects
surfaces as an `ApiException` wrapping the `JsonException`.

## Kiota

Kiota 1.35.0 generates primitives with no constraint, `string? From` and `int? Qty`, a C# enum for a closed set of
strings, and has no option to map a schema onto a type of your own. Run against a document of this version, it names
the members of that enum after the [known values](./openapi.md#names-of-known-values), `France` rather than `FR`, with
a declared description as the member's summary; a closed set of numbers stays a number. A closed set taken as a
parameter becomes an enum named after the operation, such as `GetCountryQueryParameterType`, beside the one named
after the component. Its models are `partial`, so typed accessors can be added beside them:

```csharp skip
public partial class Transfer
{
    public Iban FromIban => Iban.Parse(From!);
}
```

Kiota was run against an earlier form of the document, in which a `TimeSpan` value object carried `format: duration`,
which Kiota writes as an ISO 8601 duration, `PT1H30M`, that the server refuses, and in which the items of a
`List<Iban>`, undescribed, gave an `UntypedNode`. The document now describes a `TimeSpan` value object as a string in
its constant form, without a format, and the items of a collection and each parameter with the value object's schema,
so a generated client types them from it; that was not re-run.

## NSwag

From the OpenAPI 3.1 document, NSwag 14.7.1 generates primitives carrying the rules as DataAnnotations:
`[StringLength(34, MinimumLength = 15)]`, `[RegularExpression]`, `[Range(1, 100)]`, and an enum for a closed set,
strings and numbers alike, whose members it names after the [known values](./openapi.md#names-of-known-values). A
client in the same solution can map the components back to the shared value objects with a type resolver, which was
run end to end:

```csharp skip
sealed class ValueObjectTypeResolver(CSharpGeneratorSettings settings, OpenApiDocument document, Dictionary<string, Type> valueObjects)
    : CSharpTypeResolver(settings)
{
    public override string Resolve(JsonSchema schema, bool isNullable, string? typeNameHint)
    {
        var target = schema.ActualSchema;
        var name = document.Components.Schemas.FirstOrDefault(p => ReferenceEquals(p.Value, target)).Key;
        return name is not null && valueObjects.TryGetValue(name, out var type)
            ? $"global::{type.FullName}" + (isNullable ? "?" : "")
            : base.Resolve(schema, isNullable, typeNameHint);
    }
}

ValueObjectRegistry.EnsureAssemblyRegistered(typeof(Iban).Assembly);
var byName = ValueObjectRegistry.GetRegistered().ToDictionary(d => d.ValueObjectType.Name, d => d.ValueObjectType);
var resolver = new ValueObjectTypeResolver(settings.CSharpGeneratorSettings, document, byName);
resolver.RegisterSchemaDefinitions(document.Components.Schemas
    .Where(p => !byName.ContainsKey(p.Key))
    .ToDictionary(p => p.Key, p => p.Value));
var code = new CSharpClientGenerator(document, settings, resolver).GenerateFile();
```

The probe forced the module initializer with `RuntimeHelpers.RunModuleConstructor`; `EnsureAssemblyRegistered` is
the public call that does it. Prefer the 3.1 document: from 3.0, NSwag turns `anyOf [integer, string]` into empty
wrapper classes. The type resolver maps components, and the document describes most parameters in place rather than
as a reference to a component, so such a parameter is generated as its underlying type, with its rules (read from the
document, not re-run).
