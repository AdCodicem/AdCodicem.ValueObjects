---
title: Design Decisions
sidebar_label: Design Decisions
slug: /design-decisions
---

# Design decisions worth knowing

**A `readonly partial struct`, not a `record struct`.** A record's `with` expression and field-wise equality
would both bypass validation and the configured comparison. The generator owns equality, ordering and hashing
so that `Comparison = StringComparison.OrdinalIgnoreCase` actually means something.

**A struct, even when the underlying type is a `string`.** Holding 100 000 struct wrappers allocates exactly
what holding 100 000 bare strings allocates, to the byte; the class equivalent costs four times the memory and
twice the time, because a reference type adds 24 bytes of header, method table pointer and field per instance.
The struct gives that back only when it crosses a non-generic boundary and boxes, so the generated equality,
hashing and comparison exist to keep the hot paths generic — dictionary lookups and sorts on value objects
allocate nothing. See [Benchmarks](./benchmarks.md) for the numbers and for where the struct loses.

**`default(Iban)` is a build error.** A struct can always be brought into existence uninitialized, and that is
the one hole a struct value object cannot close by itself. The `VO0010` analyzer closes it at compile time,
which is what makes the struct representation — zero allocation, no null — safe to choose. Opt out per type
with `AllowDefault = true`.

**Rejection is not an exception.** `Validate` returns a `readonly struct` that allocates nothing when the
value is valid, and every integration — JSON, model binding, EF Core, Dapper — goes through `TryCreate`.
`Create` throws, and is for the call sites that want it. Validation is fail-fast: the first violated rule
wins.

**Normalize, then validate, then assign.** So a non-default instance is by construction both normalized and
valid. It happens on construction, on parsing, on deserialization and on model binding — but *not* when
materializing a row from the database, which is the hottest path in most applications and reads values this
same application wrote. `ConfigureValueObjects(strict: true)` turns that back on for a table another system
also writes to.

**Rules are declared once.** `MaxLength = 34` validates the value, sizes the EF Core column, and becomes the
`maxLength` keyword of the OpenAPI schema. `[KnownValue]` entries become named constants, a frozen membership
lookup, and the `enum` keyword of the schema.

## Constraints that shape the generated code

A few constraints of the .NET compiler and source generator model are the reason some of the generated code
looks the way it does — worth knowing if a compile error in generated code is confusing:

- **Source generators never observe each other's output.** The `[JsonConverter]` this generator writes is
  invisible to the System.Text.Json generator, which is the entire reason `AdCodicem.ValueObjects.Json` exists:
  a hand-written converter factory the STJ generator *can* see. The same constraint is why `Pattern` compiles a
  `Regex` at runtime rather than using `[GeneratedRegex]`.
- **Generated code cannot rely on the consumer's usings.** Every type and extension method is fully qualified
  in emitted code. A consumer with `ImplicitUsings` disabled would otherwise get a compile error in code they
  cannot edit.
- **`static virtual` and `static abstract` interface members are reachable only through a type parameter.**
  That's a C# rule, not a choice this library made — it's why the generator emits concrete members for each
  value object rather than relying on default interface implementations.

Next: [Entity Identifiers](./entity-identifiers.md), which build on the same generator for a different shape of
value.
