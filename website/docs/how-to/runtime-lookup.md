---
title: Work With a Type Known Only at Run Time
sidebar_label: Run-time lookup
slug: /how-to/runtime-lookup
description: Parse, create and describe a value object when only its Type is known, through the registry every value object joins automatically.
---

# Work with a type known only at run time

Domain code and the integrations use the typed path — `Iban.TryCreate`, or the static abstract members of
`IValueObject<TSelf, TValue>` through a type parameter — which neither boxes nor allocates. Some code only has a
`Type`: a generic importer, a tool that reads configuration, an integration of your own. That code goes through
the registry, in `AdCodicem.ValueObjects.Metadata`.

```csharp skip
if (ValueObjectRegistry.TryGet(type, out var descriptor)
    && descriptor.TryParse(text, CultureInfo.InvariantCulture, out var boxed, out var validation))
{
    // boxed is the value object, as object
}
```

A descriptor exposes what the type declares and how to build one:

- `ValueObjectType` and `ValueType`, the value object and its underlying type;
- `Schema`, the declared rules — lengths, pattern, bounds, format, known values — as data;
- `Create`, `TryCreate`, `CreateUnchecked` and `TryParse`, which take and return boxed values;
- `GetValue` and `Format`, to read an instance back.

A rejection carries the same `ValidationResult` as the typed path, with the code of the rule that fired.

## Cheaper questions

`ValueObjectRegistry.IsValueObject(type)` and `ValueObjectRegistry.GetUnderlyingType(type)` answer without a
descriptor. `TryResolve` also unwraps `Nullable<T>`, which is what a model binder or a serializer is usually
holding.

## When a type is not found

Nothing needs registering by hand: every value object joins the registry through a generated module
initializer. A module initializer only runs once its assembly is loaded, though, so code that looks a type up
before anything else has touched that assembly can call
`ValueObjectRegistry.EnsureAssemblyRegistered(assembly)` first. The EF Core and Dapper entry points already do.
