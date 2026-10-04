---
title: Packages
sidebar_label: Packages
slug: /packages
description: The twelve AdCodicem.ValueObjects packages, and which boundary each one covers.
---

# Packages

Twelve NuGet packages. Install `AdCodicem.ValueObjects` and add whichever of the others cover the boundaries your
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

## Supported frameworks

Every package targets `net10.0`, so it installs into a project on .NET 10 or any later version. The twelve are
released together under one version number: reference the same version of each. Their dependencies are minimums
with no upper bound, and the exact minimum of each is in the package's dependency list on nuget.org. A framework's
next major is supported by these same packages, never by a package per framework version
([ADR-0010](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/docs/adr/0010-version-every-package-in-lockstep-independently-of-dotnet.md)).

| Package | Target | Built and tested against | On the next .NET¹ |
| --- | --- | --- | --- |
| `AdCodicem.ValueObjects` | `net10.0` | the .NET 10 SDK | the .NET 11 SDK, whose compiler runs the generator |
| `AdCodicem.ValueObjects.Abstractions` | `net10.0` | .NET 10 | .NET 11 |
| `AdCodicem.ValueObjects.Json` | `net10.0` | .NET 10, source generation included | .NET 11, source generation included |
| `AdCodicem.ValueObjects.EntityFrameworkCore` | `net10.0` | EF Core 10, on PostgreSQL and SQL Server | EF Core 11, on SQLite, PostgreSQL and SQL Server |
| `AdCodicem.ValueObjects.AspNetCore` | `net10.0` | ASP.NET Core 10 | ASP.NET Core 11 |
| `AdCodicem.ValueObjects.OpenApi` | `net10.0` | ASP.NET Core 10, with `Microsoft.OpenApi` 2 | ASP.NET Core 11, with `Microsoft.OpenApi` 3 |
| `AdCodicem.ValueObjects.FluentValidation` | `net10.0` | FluentValidation 12 | FluentValidation 12 on .NET 11 |
| `AdCodicem.ValueObjects.Dapper` | `net10.0` | Dapper 2.1, on PostgreSQL and SQL Server | Dapper 2.1, on SQLite, PostgreSQL and SQL Server |
| `AdCodicem.ValueObjects.NewtonsoftJson` | `net10.0` | Newtonsoft.Json 13 | Newtonsoft.Json 13 on .NET 11 |
| `AdCodicem.ValueObjects.Identifiers` | `net10.0` | .NET 10 | .NET 11 |
| `AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore` | `net10.0` | EF Core 10, on PostgreSQL and SQL Server | EF Core 11, on SQLite, PostgreSQL and SQL Server |
| `AdCodicem.ValueObjects.Testing` | `net10.0` | xUnit v3 4 | xUnit v3 4 on .NET 11 |

¹ On the .NET 11 release candidate, by a CI job that installs the packages each commit builds into a `net11.0`
application ([How the library is tested](./testing.md#the-compatibility-island)). It informs and blocks nothing until
.NET 11 ships.

## Versioning

The packages follow semantic versioning from 1.0.0 on: from then, only a major version breaks the public API or
removes a member. Before 1.0.0, the version is `0.<minor>.<patch>`, and a minor version may break part of the public
API, or deprecate or remove part of it, without waiting for a major: read the
[changelog](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/CHANGELOG.md) before taking a new minor. A
patch never breaks anything, before 1.0.0 or after. A member deprecated rather than removed outright is reported by
the compiler wherever it is used, with a diagnostic naming its replacement (`VO0021`, `VO0028`).

## Trying a preview

Between stable releases, a preview of every package is published to nuget.org when something a package ships has
changed, checked every week. It carries the number of the release it leads to, `0.3.0-preview.172` for instance,
and every package is published at that version:

```bash
dotnet add package AdCodicem.ValueObjects --prerelease
```

Previews receive no fixes of their own: a fix reaches the next preview and the next release. Every package, and every
assembly inside it, carries a signed build provenance attestation; the
[security policy](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/SECURITY.md#verifying-a-package) says how to check one. The
[preview of this documentation](/docs/preview/introduction) describes the latest preview, and its label names it.

## Why this many packages

Each integration is its own package so that adding EF Core support doesn't pull FluentValidation into a
service that has no use for it, and so that a trimmed or AOT-published app only carries the generator's output
for the boundaries it actually crosses. `AdCodicem.ValueObjects` is the only package with a source generator in
it; everything else is a thin, generic-closed integration over the contracts in `Abstractions`.
