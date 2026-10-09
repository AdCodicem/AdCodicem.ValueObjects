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
- `GetValue` and `Format`, to read an instance back;
- `Accept`, which hands the type arguments back to code of your own, as the next section shows.

A rejection carries the same `ValidationResult` as the typed path, with the code of the rule that fired. `Create`
and `TryCreate` reject a `null` as `value_object.required` whatever the underlying type: a decimal value object
does not read it as zero.

## Back to the typed path

A boxed delegate suits a call or two at start-up. An integration that keeps working with the value object — a
formatter, a serializer, a type handler — wants an adapter closed over it, `MyFormatter<TSelf, TValue>`, which runs on
the typed path. Closing it from a `Type` with `MakeGenericType` and `Activator.CreateInstance` works under the JIT, and
fails under native AOT, which has no code for a generic closed over a struct at run time. The descriptor hands the type
arguments back instead, to an `IValueObjectVisitor<TResult>`:

```csharp skip
sealed class FormatterFor : IValueObjectVisitor<IFormatter>
{
    public IFormatter Visit<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
        => new ValueObjectFormatter<TSelf, TValue>();
}

foreach (var descriptor in ValueObjectRegistry.GetRegistered())
{
    formatters.Add(descriptor.ValueObjectType, descriptor.Accept(new FormatterFor()));
}
```

`Accept` calls `Visit` with the type arguments the descriptor was built with, so the adapter is closed at compile time.
Under native AOT, the compiler generates `Visit` for each value object a descriptor is built for in code: every one the
generator registers, and every one registered by hand, a value object written by hand or a construction of a generic
one. A visitor needing context — a builder, a flag — holds it in fields. Each `Visit` is compiled once per value
object, since a struct type argument shares no code, and only for the visitors the application creates.

Every integration that closes an adapter over a value object it knows only by its `Type` does so this way: the Dapper
integration registers its handlers, the MongoDB provider builds the serializer the driver asks it for, the MessagePack
resolver the formatter MessagePack asks it for, the MVC model binder provider creates its binders, the minimal API
filter of `AdCodicem.ValueObjects.AspNetCore.Http` closes the check of each parameter it explains, the JSON converter
factory closes the general-purpose converter it gives a value object written by hand, the test-data sampler's
`Next(descriptor)` draws a value, the AutoFixture customization answers a request, the Bogus extensions give a member a
rule, FsCheck's `MergeValueObjects` merges an arbitrary, and the Entity Framework Core conventions map each value object
and each entity identifier, while a model is built. One converter is the exception: that of an optional property,
`Iban?`, which C# names only under a constraint `Visit` cannot prove, so the value object conventions close it with
`MakeGenericType`, over the type arguments the visitor received. Entity Framework Core builds no model under native AOT,
so that never runs there.

An entity identifier's descriptor, from `EntityIdRegistry`, has the same way out: `EntityIdDescriptor.Accept` hands an
`IEntityIdVisitor<TResult>` the identifier type, constrained to `IEntityId<TId>`, so the visitor reaches `TId.New()`.
The MongoDB identifiers package registers its id generators that way:

```csharp skip
sealed class GeneratorFor : IEntityIdVisitor<IIdGenerator>
{
    public IIdGenerator Visit<TId>() where TId : struct, IEntityId<TId> => new EntityIdGenerator<TId>();
}

foreach (var descriptor in EntityIdRegistry.GetRegistered())
{
    BsonSerializer.RegisterIdGenerator(descriptor.ValueObjectType, descriptor.Accept(new GeneratorFor()));
}
```

Inside the adapter, the rules are `TSelf.Schema`, the static member of `IValueObject<TSelf, TValue>` that the generator
emits and registers as the descriptor's `Schema`: `TSelf.Schema.MaxLength` sizes a column with no registry to ask, as
`HasValueObjectConversion` does. The Entity Framework Core conventions, which hold the descriptor already, size a column
by its `Schema` instead, handed to their visitor in a field, so that a value object registered by hand with a schema of
its own is mapped by that schema.

## Cheaper questions

`ValueObjectRegistry.IsValueObject(type)` and `ValueObjectRegistry.GetUnderlyingType(type)` answer without building
a descriptor. Like `TryResolve`, they unwrap `Nullable<T>`, which is what a model binder or a serializer is usually
holding.

A value object is a struct implementing `IValueObject<TSelf, TValue>` over itself. `IsValueObject` answers `true`
exactly for the types `TryResolve` describes, and `false` for an interface, a class, or a struct carrying only the
`IValueObject` marker or `IValueObject<TValue>`. `GetUnderlyingType` answers `null` for the same types, even one that
declares a value through `IValueObject<TValue>`. The integrations claim a type by the same rule: the JSON converter
factory, the Newtonsoft.Json converter, the MVC model binder and `MustParseAs` leave anything else to the framework,
or refuse it, and the argument check of `AdCodicem.ValueObjects.AI` tells a value-object parameter by it.

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
`TryGet` finds it from then on. Describing it takes reflection and dynamic code, and so does visiting the descriptor it
builds, so under native AOT, register each construction you look up, which reads its schema off the type:
`ValueObjectRegistry.Register<Code<Order>, string>(static () => new Code<Order>.ValueJsonConverter())`.

## Value objects written by hand

A value object written by hand implements `IValueObject<TSelf, TValue>` in full, `static ValueObjectSchema Schema`
included: the rules its `Validate` enforces, as data, or `ValueObjectSchema.Unconstrained` when it publishes none. Its
known values go in `KnownValues`, and their names, which the OpenAPI document publishes, in `KnownValueDetails`, one
`KnownValueInfo` per value, in the same order, which the [contract kit](./test-value-objects.md#what-it-checks)
checks.
Nothing registers it, so `TryResolve` describes it by reflection, the first time it is asked, with that schema, the one
generic code constrained on it reads, whatever `[ValueObject<T>]` or `[KnownValue]` annotation it also carries. Where no
generator runs, nothing reads an annotation: state each rule in the schema. Under native AOT, which cannot describe it
by reflection, register it, `ValueObjectRegistry.Register<Link, Uri>(Link.Schema)`: its descriptor is then built in
code, and the JSON converter factory serves it the general-purpose converter through it.

The registry keeps the descriptor `TryResolve` builds, as it keeps that of a construction: `TryGet` finds the value
object from then on, and `GetRegistered` lists it. Whatever asks first, a serializer, a model binder or a validator,
registers it for the rest of the process, and every model built afterwards maps it through `ConfigureValueObjects`.
`ConfigureEntityIds` asks for each identifier this way, so an identifier written by hand and registered with
`EntityIdRegistry` alone joins the value object registry the first time a model is built, as a generated identifier,
which registers itself in both, always has.

## When a type is not found

Nothing needs registering by hand: every value object joins the registry through a generated module
initializer. A module initializer only runs once its assembly is loaded, though, so code that looks a type up
before anything else has touched that assembly can call
`ValueObjectRegistry.EnsureAssemblyRegistered(assembly)` first. The EF Core, Dapper and MongoDB entry points already
do, and so does the Serilog one for each assembly named in its `Assemblies` option.
