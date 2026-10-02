---
title: Primitive Obsession
sidebar_label: Primitive obsession
slug: /explanation/primitive-obsession
description: What primitive obsession costs a .NET codebase, why the usual remedy is rarely applied, and what AdCodicem.ValueObjects changes about that.
---

# Primitive obsession

Primitive obsession is the habit of carrying domain concepts as the language's built-in types: an IBAN as a
`string`, a quantity as an `int`, a customer identifier as a `Guid`. Each one is harmless where it is written.
The cost shows up between the places where it is used.

## What it costs

**The compiler cannot help.** `Task PayAsync(string customerId, string iban, decimal amount)` accepts its first
two arguments in either order, and accepts `"hello"` for both. A `Guid` identifying a customer and a `Guid`
identifying an order are the same type, so a repository asked for one happily receives the other.

**Every layer states the rules again.** The controller checks the format, the domain checks it once more
because it cannot trust the controller, a migration picks a column width, and the OpenAPI document settles for
`type: string`. Four statements of one rule, written by different people at different times, and nothing fails
when they disagree.

**Normalization happens by luck.** `" fr76 3000 6000 …"` and `"FR7630006000…"` are the same account. Whether the
application treats them as the same depends on whether this particular code path remembered to trim and
upper-case — and so do equality, unique indexes and deduplication.

**The signature says nothing.** A `string` parameter carries no information about what it accepts. That
knowledge lives in validators, comments, and the heads of the people who wrote them.

## The remedy, and why it is rarely applied

The remedy is old and uncontroversial: give each concept its own type, one that cannot hold an invalid value.
Domain-driven design calls it a value object. Once `Iban` exists, swapped arguments stop compiling, the rules
live in one place, and a method that receives an `Iban` has nothing left to check.

It stays rare because the type is only the beginning. A value object that crosses the boundaries of a real
application also needs equality and hashing that agree with its comparison rules, parsing and formatting, a
`System.Text.Json` converter, a `TypeConverter`, an EF Core value converter and comparer, a model binder, and a
schema in the OpenAPI document. Written by hand, that is a few hundred lines per concept, most of them the same
from one type to the next. At that price a team writes three value objects and goes back to `string` for the
rest.

## What changes here

AdCodicem.ValueObjects moves that cost to the compiler. A `readonly partial struct` marked `[ValueObject<T>]`
declares the concept and its rules; a source generator writes the implementation, and the integration packages
carry the same rules to each boundary:

| Where the rule is needed | Where it comes from |
| --- | --- |
| Construction, parsing, deserialization, model binding | The generated `Validate`, always run after `Normalize` |
| The database column | `MaxLength`, applied by the EF Core convention |
| The OpenAPI document | `MaxLength`, `Minimum`, the pattern hook, known values, turned into schema keywords |
| An API error response | The stable error code of the rule that was violated |

On the wire nothing changes: an `Iban` is still a JSON string, a route segment and a query-string parameter,
and in the database it is still a `varchar`. Clients and schemas see the underlying type; only the code sees
the concept.

## When not to bother

A value that never leaves one method, or that the domain genuinely treats as free text — a comment, a display
label — gains little from a type of its own. The concepts worth wrapping are the ones that carry rules, cross
boundaries, or get confused with one another: identifiers, codes, amounts, addresses, anything with a format.

Next: [Getting started](../getting-started.md), or [Design decisions](../design-decisions.md) for why the type is
a struct and why `default` is a build error.
