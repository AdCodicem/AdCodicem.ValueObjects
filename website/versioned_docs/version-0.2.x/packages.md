---
title: Packages
sidebar_label: Packages
slug: /packages
---

# Packages

Ten NuGet packages. Install `AdCodicem.ValueObjects` and add whichever of the others cover the boundaries your
application actually has.

| Package | What it gives you |
| --- | --- |
| **`AdCodicem.ValueObjects`** | The one to install: contracts, source generator and analyzers. |
| `AdCodicem.ValueObjects.Abstractions` | The contracts alone, with no dependency at all. |
| `AdCodicem.ValueObjects.Json` | Covers source-generated serializer contexts and hand-written value objects. |
| `AdCodicem.ValueObjects.EntityFrameworkCore` | Converters, comparers, and a convention that maps a whole assembly. |
| `AdCodicem.ValueObjects.AspNetCore` | MVC model binding and RFC 9457 problem details carrying the violated rule. |
| `AdCodicem.ValueObjects.OpenApi` | Schema transformer for the built-in .NET OpenAPI stack. |
| `AdCodicem.ValueObjects.FluentValidation` | Rules that reuse what the value object already enforces. |
| `AdCodicem.ValueObjects.Dapper` | Type handlers for raw SQL. |
| `AdCodicem.ValueObjects.NewtonsoftJson` | Interop with code that has not moved to `System.Text.Json`. |
| `AdCodicem.ValueObjects.Identifiers` | Stripe-style public entity identifiers: `acc_2K7X9…`. See [Entity Identifiers](./entity-identifiers.md). |
| `AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore` | Fixed-width, non-Unicode columns for those identifiers. |
| `AdCodicem.ValueObjects.Testing` | An xUnit contract kit for your own value objects. |

## Why this many packages

Each integration is its own package so that adding EF Core support doesn't pull FluentValidation into a
service that has no use for it, and so that a trimmed or AOT-published app only carries the generator's output
for the boundaries it actually crosses. `AdCodicem.ValueObjects` is the only package with a source generator in
it; everything else is a thin, generic-closed integration over the contracts in `Abstractions`.
