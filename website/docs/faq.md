---
title: Frequently Asked Questions
sidebar_label: FAQ
slug: /faq
description: Short answers to the questions people ask about AdCodicem.ValueObjects, with links to the pages that explain them.
---

# Frequently asked questions

## About the library

### What problem does it solve?

Primitive obsession: domain concepts carried as bare `string`, `int` and `Guid`, with their rules restated — and
drifting apart — in every layer. [Primitive obsession](./explanation/primitive-obsession.md) describes the
problem and how the library answers it.

### Which versions of .NET are supported?

.NET 10: every package targets `net10.0`, and so must the project that uses them. If your application cannot
move to .NET 10,
[Compared with other libraries](./explanation/comparison.md#where-the-others-are-stronger) names alternatives
that run on older frameworks.

### Is it ready for production?

It follows semantic versioning and is still at 0.x, which means the public surface may change between minor
releases; every change is recorded in the
[changelog](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/CHANGELOG.md). Stable releases are
cut by hand, and every merge to `main` publishes a preview package in between.

### How does it compare with Vogen, StronglyTypedId or Thinktecture?

[Compared with other libraries](./explanation/comparison.md) has a feature table and says where each of them is
the better choice. [Migrate from another library](./how-to/migrating.md) maps their surface onto this one.

## Declaring value objects

### Why a `readonly struct`, and not a class or a `record struct`?

A struct wrapping a `string` costs exactly what the `string` costs, to the byte; a class adds 24 bytes per
instance. A `record struct` is refused because its `with` expression and field-wise equality would bypass
validation and the declared comparison. [Design decisions](./design-decisions.md) has the reasoning and
[Benchmarks](./benchmarks.md) the numbers.

### Which underlying types can I use?

`string`, `Guid`, `bool`, `char`, every built-in integer including `Int128` and `UInt128`, `decimal`, `double`,
`float`, `DateOnly`, `TimeOnly`, `DateTime`, `DateTimeOffset` and `TimeSpan`. Anything else is `VO0003`.

### Can a value object hold several values?

No. A concept made of several values — an amount and its currency, a postal address — belongs in an ordinary type
whose members are value objects.

### My normalization or validation rule is never called. Why?

The interface is missing. A hook is found through `IValueObjectNormalizer<T>`, `IValueObjectValidator<T>` or a
formatter interface, not through its name, and `VO0011` warns about a method that looks like a rule but is not
declared as one.

### How do I see the code the generator writes?

Set `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` in the project and build: the files land
under `obj/…/generated/AdCodicem.ValueObjects.Generators/`. [Generated members](./reference/generated-members.md)
lists what to expect.

## Using value objects

### How do I represent a missing value?

With `T?`. `default(T)` and `new T()` are build errors (`VO0010`), entity identifiers included, because an
uninitialized struct would skip every rule. A type whose zero value is genuinely meaningful can opt out with
`AllowDefault = true`, on `[ValueObject<T>]` as on `[EntityId]`.

### Can I throw my own exception type?

`Create` always throws `ValueObjectException`, which carries the error code, the type and the attempted value.
To throw something else, call `TryCreate` and throw your own exception from the `ValidationResult` it returns.

### Can a value object be a dictionary key, or an EF Core key?

Yes to both. The generated equality and hashing make dictionary lookups allocation-free, and a value object
serializes as a JSON property name. In EF Core it can be a primary or a foreign key; for a string key compared
without case, give the column a matching collation.

### Why are values read from the database not validated?

Because the EF Core read path is the hottest in most applications, and it reads values the same application
validated when writing them. `ConfigureValueObjects(strict: true, …)` validates reads for tables other systems
also write to.

### Does it work with minimal APIs?

Yes, with nothing to install: a value object implements `IParsable<T>`, which is what minimal API parameter
binding looks for.

### Does it work with Swashbuckle?

No. The OpenAPI integration targets the built-in .NET stack, `Microsoft.AspNetCore.OpenApi`.

## Performance

### Does a value object cost more than the primitive it wraps?

Holding one costs nothing more: a struct wrapper has the size of the value it wraps. Validation costs what the
rules cost, on the way in only. The struct gives something back when it is boxed — passed as `object` or through
a non-generic interface — which is why the generated equality, hashing and comparison keep the hot paths
generic. [Benchmarks](./benchmarks.md) measures each case.

### Does it use reflection?

Not in the generated code, and not to find value objects: a generated module initializer registers each type
at start-up. The contracts, the generated code, the JSON package, FluentValidation and identifiers are marked
AOT-compatible and built with the trimming and AOT analyzers on. The EF Core, ASP.NET Core, OpenAPI, Dapper and
Newtonsoft.Json integrations are not, because the frameworks they plug into are not.

### Does a pattern run compiled under native AOT?

Through `IValueObjectPatternValidator`, yes. Its `Pattern` is a `[GeneratedRegex]` property you write, which the
regex source generator turns into code at build time, so it runs the same under native AOT as under the JIT and
costs nothing until it first runs. The deprecated `Pattern` option of `[ValueObject<T>]` does not: it builds its
`Regex` at start-up with `RegexOptions.Compiled`, which native AOT cannot honour, so there the expression is
interpreted. The option is reported as `VO0021` and removed in the next major version;
[Diagnostics](./reference/diagnostics.md#moving-off-pattern) shows the change. [Benchmarks](./benchmarks.md)
has the measurements.
