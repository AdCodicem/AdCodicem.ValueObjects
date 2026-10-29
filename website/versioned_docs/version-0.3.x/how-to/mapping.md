---
title: Map Value Objects With Mapperly, Mapster and AutoMapper
sidebar_label: Object mappers
slug: /how-to/mapping
description: Map between DTOs of primitives and types holding value objects with Mapperly, Mapster and AutoMapper, through Create in one direction and Value in the other.
---

# Map value objects with Mapperly, Mapster and AutoMapper

A mapper that turns a DTO of primitives into a type holding value objects has to go through `Create`, or `Parse`,
for the value object to check its rules; the other way, it reads `Value`. None of these mappers finds that path on its
own for every value object, so each takes a method or a line of configuration per type.

## Mapperly

Give Mapperly a static method for each direction, which it picks by source and target type. This was run on Mapperly
4.3.1 and 5.0.0-next.11, with the value objects declared in the mapper's project and in a project it references:

```csharp skip
public static class ValueObjectMappings
{
    public static Iban ToIban(string value) => Iban.Create(value);
    public static string FromIban(Iban value) => value.Value;
    public static OrderId ToOrderId(Guid value) => OrderId.Create(value);
    public static Guid FromOrderId(OrderId value) => value.Value;
}

[Mapper]
[UseStaticMapper(typeof(ValueObjectMappings))]
public partial class OrderMapper
{
    public partial Order ToDomain(OrderDto dto);
    public partial OrderDto ToDto(Order order);
}
```

Write both directions. Without a method into a value object declared in the mapper's own project, Mapperly, which
cannot see the generated members, writes `new T()`, and `VO0032` fails the build
([A value object another generator creates](../reference/diagnostics.md#a-value-object-another-generator-creates)).
Without a method out of it, Mapperly can map an `OrderId` to a `Guid` as `new Guid()`, which is `Guid.Empty`, and
nothing reports it, since a `Guid` is not a value object. A generic pair of methods works only on Mapperly 5, with the
value objects in a referenced project.

## Mapster

`"FR7630006000011234567890189".Adapt<Iban>()` already validates, through `Parse`. Everything else takes one
`MapWith` per pair, which was run:

```csharp skip
TypeAdapterConfig<string, Iban>.NewConfig().MapWith(s => Iban.Create(s));
TypeAdapterConfig<Iban, string>.NewConfig().MapWith(v => v.Value);
```

A loop over `ValueObjectRegistry.GetRegistered()` could register them all; it was not run.

## AutoMapper

Without a map, AutoMapper throws `AutoMapperMappingException`, "Missing type map configuration or unsupported
mapping.": a value object has no constructor that takes its value alone, which is the one AutoMapper would call. Map
each type, which was run on AutoMapper 16.2.0:

```csharp skip
cfg.CreateMap<string, Iban>().ConvertUsing(s => Iban.Create(s));
cfg.CreateMap<Iban, string>().ConvertUsing(v => v.Value);
```

On a value object declaring `ExplicitConversionFromValue = true`, AutoMapper calls the explicit conversion, which
validates: a value the type rejects makes the mapping throw, around the `ValueObjectException`.

AutoMapper 15 and later is commercially licensed. 14.0.0, the last MIT release, raises `NU1903` for the advisory
GHSA-rvv3-g6hj-g44x.
