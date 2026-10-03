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

A rejection carries the same `ValidationResult` as the typed path, with the code of the rule that fired. `Create`
and `TryCreate` reject a `null` as `value_object.required` whatever the underlying type: a decimal value object
does not read it as zero.

## Cheaper questions

`ValueObjectRegistry.IsValueObject(type)` and `ValueObjectRegistry.GetUnderlyingType(type)` answer without building
a descriptor. Like `TryResolve`, they unwrap `Nullable<T>`, which is what a model binder or a serializer is usually
holding.

A value object is a struct implementing `IValueObject<TSelf, TValue>` over itself. `IsValueObject` answers `true`
exactly for the types `TryResolve` describes, and `false` for an interface, a class, or a struct carrying only the
`IValueObject` marker or `IValueObject<TValue>`. `GetUnderlyingType` answers `null` for the same types, even one that
declares a value through `IValueObject<TValue>`. The integrations claim a type by the same rule: the JSON converter
factory, the Newtonsoft.Json converter, the MVC model binder and `MustParseAs` leave anything else to the framework,
or refuse it.

## Nullable value objects

`TryGet` and `TryResolve` accept `Iban?` as well as `Iban`, and return the descriptor of `Iban` for both: a descriptor
describes the value object itself, never its nullable type, and nothing in it handles a `null`. An integration built on
it handles both before the descriptor does:

- A converter or a provider that closes a generic type of its own over the value object closes it over
  `descriptor.ValueObjectType`, not over the type it was asked for. Closed over `Iban?`, it fails on the first nullable
  property, since the value object's constraint refuses a `Nullable<T>`:
  ``GenericArguments[0], 'System.Nullable`1[Iban]' … violates the constraint of type 'TSelf'``.
- `GetValue`, `Format` and `ValidateWrite` take an instance, never `null`: the `null` of an `Iban?` throws a
  `NullReferenceException` inside them. Write the `null` yourself, or skip the property.

Where the host has its own wrapper for nullable types, as most serializers do, claim only the value object and let
that wrapper deal with `null`.

## Generic value objects

A generic value object registers its generic definition, since its registration knows none of its constructions:
`ValueObjectRegistry.GetRegisteredGenericDefinitions()` lists them. `TryResolve` describes a construction the first time
it is asked for it, from the schema and the converter the generator wrote on it, and caches the descriptor;
`TryGet` finds it from then on. Describing it takes reflection and dynamic code, so under native AOT, register each
construction you look up with `ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema, new TSelf.ValueJsonConverter())`.

## When a type is not found

Nothing needs registering by hand: every value object joins the registry through a generated module
initializer. A module initializer only runs once its assembly is loaded, though, so code that looks a type up
before anything else has touched that assembly can call
`ValueObjectRegistry.EnsureAssemblyRegistered(assembly)` first. The EF Core and Dapper entry points already do.
