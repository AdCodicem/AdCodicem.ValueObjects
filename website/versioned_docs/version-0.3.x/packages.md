---
title: Packages
sidebar_label: Packages
slug: /packages
description: The thirty-two AdCodicem.ValueObjects packages, and which boundary each one covers.
---

# Packages

Thirty-two NuGet packages. Install `AdCodicem.ValueObjects` and add whichever of the others cover the boundaries your
application actually has.

| Package | What it gives you |
| --- | --- |
| **`AdCodicem.ValueObjects`** | The one to install: contracts, source generator and analyzers. |
| `AdCodicem.ValueObjects.Abstractions` | The contracts alone, with no dependency at all, and the DataAnnotations attributes `[ParsesAs]` and `[NotDefault]`. |
| `AdCodicem.ValueObjects.Json` | Covers source-generated serializer contexts and hand-written value objects, and fills in the JSON Schema System.Text.Json exports. |
| `AdCodicem.ValueObjects.EntityFrameworkCore` | Converters, comparers, a convention that maps a whole assembly, keys generated the provider's way, `.Value` translated in queries, and opt-in CHECK constraints from the rules, whose violations carry the rule code under `UseValueObjectCheckErrors()`. |
| `AdCodicem.ValueObjects.AspNetCore` | MVC model binding and RFC 9457 problem details carrying the violated rule. |
| `AdCodicem.ValueObjects.AspNetCore.Http` | The same problem details for minimal APIs, native AOT included. See [ASP.NET Core](./how-to/aspnet-core.md#problem-details-for-minimal-apis). |
| `AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson` | The same problem details for a body MVC reads with Newtonsoft.Json, and for a JSON Patch document applied through the rules. See [ASP.NET Core](./how-to/aspnet-core.md#a-body-read-by-newtonsoftjson). |
| `AdCodicem.ValueObjects.OpenApi` | Schema transformer for the built-in .NET OpenAPI stack. |
| `AdCodicem.ValueObjects.Swashbuckle` | Schema and parameter filters for Swashbuckle 10.3 and later. See [OpenAPI](./how-to/openapi.md#swashbuckle). |
| `AdCodicem.ValueObjects.NJsonSchema` | NSwag documents and the JSON Schemas Confluent's serdes register with the Schema Registry, built with NJsonSchema: each value object one definition of its underlying type, with its rules. See [OpenAPI](./how-to/openapi.md#nswag) and [Kafka](./how-to/messaging.md#kafka-with-confluents-schema-registry). |
| `AdCodicem.ValueObjects.FluentValidation` | Rules that reuse what the value object already enforces. |
| `AdCodicem.ValueObjects.Dapper` | Type handlers for raw SQL. |
| `AdCodicem.ValueObjects.MongoDB` | MongoDB.Driver serializers: the bare value in BSON, `.Value` in LINQ, strict reads, and a `$jsonSchema` validator built from the rules. See [MongoDB](./how-to/mongodb.md). |
| `AdCodicem.ValueObjects.MessagePack` | MessagePack formatters, and SignalR's MessagePack hub protocol: the bare value on the wire, strict reads. See [MessagePack and SignalR](./how-to/messagepack.md). |
| `AdCodicem.ValueObjects.ProtobufNet` | protobuf-net serializers, and protobuf-net.Grpc code-first services: the underlying value on the wire, strict reads, the scalar in the `.proto` export, a refused request answered `INVALID_ARGUMENT` with its rule code. See [protobuf-net and code-first gRPC](./how-to/protobuf-net.md). |
| `AdCodicem.ValueObjects.Grpc` | Contract-first gRPC services: every refused field answered at once as `INVALID_ARGUMENT` with a `google.rpc.BadRequest`, and a refusal that escapes a handler with a `google.rpc.ErrorInfo` carrying its rule code. See [gRPC services](./how-to/grpc.md). |
| `AdCodicem.ValueObjects.HotChocolate` | One GraphQL scalar per value object under HotChocolate 16, read through its rules, a refused value answered with its rule code. See [GraphQL](./how-to/graphql.md). |
| `AdCodicem.ValueObjects.Blazor` | A Blazor input per value object, read through its rules, the refused rule's message under the field and its code on the `EditContext`. See [Blazor forms](./how-to/blazor.md). |
| `AdCodicem.ValueObjects.NewtonsoftJson` | Interop with code that has not moved to `System.Text.Json`. |
| `AdCodicem.ValueObjects.Serilog` | Logs a value object as its underlying value, a number as a number. See [Logging](./how-to/logging.md#serilog). |
| `AdCodicem.ValueObjects.AI` | Microsoft.Extensions.AI tool and structured-output schemas that carry the rules, and a refused tool argument answered with its rule code. See [Language models](./how-to/language-models.md). |
| `AdCodicem.ValueObjects.ModelContextProtocol` | Model Context Protocol tool schemas that carry the rules, and a refused tool argument answered with its rule code, as a tool execution error. See [Model Context Protocol servers](./how-to/language-models.md#model-context-protocol-servers). |
| `AdCodicem.ValueObjects.AzureFunctions` | Azure Functions isolated worker: route values, query values and binding data bound through the rules, a refusal carrying its code. See [Azure](./how-to/azure.md#azure-functions-isolated-worker). |
| `AdCodicem.ValueObjects.AzureFunctions.AspNetCore` | The minimal API problem details for the HTTP triggers of the Functions ASP.NET Core integration. See [Azure](./how-to/azure.md#problem-details-for-http-triggers). |
| `AdCodicem.ValueObjects.Identifiers` | Stripe-style public entity identifiers: `acc_2K7X9…`. See [Entity Identifiers](./entity-identifiers.md). |
| `AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore` | Fixed-width, non-Unicode columns for those identifiers. |
| `AdCodicem.ValueObjects.Identifiers.MongoDB` | Those identifiers minted by MongoDB.Driver for a document inserted without one. See [MongoDB](./how-to/mongodb.md#entity-identifiers-as-_id). |
| `AdCodicem.ValueObjects.Testing` | An xUnit contract kit for your own value objects. |
| `AdCodicem.ValueObjects.Testing.Data` | Values each value object accepts, drawn from its rules, for any test framework; the values at its edges and those its schema rules out. See [Testing your value objects](./how-to/test-value-objects.md#generate-valid-values). |
| `AdCodicem.ValueObjects.AutoFixture` | An AutoFixture customization that creates every value object from its rules. See [Testing your value objects](./how-to/test-value-objects.md#autofixture). |
| `AdCodicem.ValueObjects.Bogus` | A Bogus rule for every value-object member of a `Faker<T>`, replayed by `UseSeed`. See [Testing your value objects](./how-to/test-value-objects.md#bogus). |
| `AdCodicem.ValueObjects.FsCheck` | FsCheck arbitraries drawn from the rules, biased towards their edges, shrinking to accepted values. See [Testing your value objects](./how-to/test-value-objects.md#fscheck). |

## Supported frameworks

Every package targets `net10.0`, so it installs into a project on .NET 10 or any later version. The thirty-two are
released together under one version number: reference the same version of each. Their dependencies are minimums
with no upper bound, and the exact minimum of each is in the package's dependency list on nuget.org. A framework's
next major is supported by these same packages, never by a package per framework version
([ADR-0010](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/docs/adr/0010-version-every-package-in-lockstep-independently-of-dotnet.md)).

| Package | Target | Built and tested against | On the next .NET¹ |
| --- | --- | --- | --- |
| `AdCodicem.ValueObjects` | `net10.0` | the .NET 10 SDK | the .NET 11 SDK, whose compiler runs the generator |
| `AdCodicem.ValueObjects.Abstractions` | `net10.0` | .NET 10 | .NET 11 |
| `AdCodicem.ValueObjects.Json` | `net10.0` | .NET 10, source generation included | .NET 11, source generation included |
| `AdCodicem.ValueObjects.EntityFrameworkCore` | `net10.0` | EF Core 10, on SQLite, PostgreSQL and SQL Server | EF Core 11, on SQLite, PostgreSQL and SQL Server |
| `AdCodicem.ValueObjects.AspNetCore` | `net10.0` | ASP.NET Core 10 | ASP.NET Core 11 |
| `AdCodicem.ValueObjects.AspNetCore.Http` | `net10.0` | ASP.NET Core 10, reflection-based binding, the Request Delegate Generator and native AOT | ASP.NET Core 11 |
| `AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson` | `net10.0` | ASP.NET Core 10, with `Microsoft.AspNetCore.Mvc.NewtonsoftJson` 10 | ASP.NET Core 11, with `Microsoft.AspNetCore.Mvc.NewtonsoftJson` 11 |
| `AdCodicem.ValueObjects.OpenApi` | `net10.0` | ASP.NET Core 10, with `Microsoft.OpenApi` 2 | ASP.NET Core 11, with `Microsoft.OpenApi` 3 |
| `AdCodicem.ValueObjects.Swashbuckle` | `net10.0` | Swashbuckle 10 on ASP.NET Core 10, with `Microsoft.OpenApi` 2 | Swashbuckle 10 on ASP.NET Core 11, with `Microsoft.OpenApi` 2 |
| `AdCodicem.ValueObjects.NJsonSchema` | `net10.0` | NJsonSchema 11.6: NSwag 14.7 on ASP.NET Core 10, Confluent's JSON serdes 2.16 | NSwag 14.7 and Confluent's JSON serdes 2.16 on .NET 11 |
| `AdCodicem.ValueObjects.FluentValidation` | `net10.0` | FluentValidation 12 | FluentValidation 12 on .NET 11 |
| `AdCodicem.ValueObjects.Dapper` | `net10.0` | Dapper 2.1, on PostgreSQL and SQL Server | Dapper 2.1, on SQLite, PostgreSQL and SQL Server |
| `AdCodicem.ValueObjects.MongoDB` | `net10.0` | MongoDB.Driver 3.12, on MongoDB 7.0² | MongoDB.Driver 3.12 on .NET 11, on MongoDB 7.0² |
| `AdCodicem.ValueObjects.MessagePack` | `net10.0` | MessagePack 3.1, with SignalR's MessagePack hub protocol 10 | MessagePack 3.1 on .NET 11, with the hub protocol 11 |
| `AdCodicem.ValueObjects.ProtobufNet` | `net10.0` | protobuf-net 3.4 and protobuf-net.Grpc 1.3 over Grpc.AspNetCore 2.84, and protobuf-net's compile-time model under native AOT | protobuf-net 3.4 on .NET 11, a code-first call over TestHost |
| `AdCodicem.ValueObjects.Grpc` | `net10.0` | Grpc.StatusProto 2.84, over Grpc.AspNetCore 2.84 and native AOT | a contract-first service over TestHost on .NET 11 |
| `AdCodicem.ValueObjects.HotChocolate` | `net10.0` | HotChocolate 16.6, in process and over ASP.NET Core | HotChocolate 16.6 on .NET 11 |
| `AdCodicem.ValueObjects.Blazor` | `net10.0` | Blazor 10 (`Microsoft.AspNetCore.Components.Web` 10), through bUnit, static rendering over TestHost and `HtmlRenderer` under native AOT | Components 11, through `HtmlRenderer` |
| `AdCodicem.ValueObjects.NewtonsoftJson` | `net10.0` | Newtonsoft.Json 13 | Newtonsoft.Json 13 on .NET 11 |
| `AdCodicem.ValueObjects.Serilog` | `net10.0` | Serilog 4, through Microsoft.Extensions.Logging too, and native AOT | Serilog 4 on .NET 11 |
| `AdCodicem.ValueObjects.AI` | `net10.0` | Microsoft.Extensions.AI 10, with its OpenAI adapter in strict mode, and native AOT | Microsoft.Extensions.AI 10 on .NET 11 |
| `AdCodicem.ValueObjects.ModelContextProtocol` | `net10.0` | ModelContextProtocol 2.2, in process over streams, and native AOT | ModelContextProtocol 2.2 on .NET 11 |
| `AdCodicem.ValueObjects.AzureFunctions` | `net10.0` | Microsoft.Azure.Functions.Worker 2.52, in the Functions host's `dotnet-isolated` 10.0 image | Worker 2.52 on .NET 11 |
| `AdCodicem.ValueObjects.AzureFunctions.AspNetCore` | `net10.0` | the Functions ASP.NET Core integration 2.1, in the Functions host's `dotnet-isolated` 10.0 image | the integration 2.1 beside ASP.NET Core 11 |
| `AdCodicem.ValueObjects.Identifiers` | `net10.0` | .NET 10 | .NET 11 |
| `AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore` | `net10.0` | EF Core 10, on PostgreSQL and SQL Server | EF Core 11, on SQLite, PostgreSQL and SQL Server |
| `AdCodicem.ValueObjects.Identifiers.MongoDB` | `net10.0` | MongoDB.Driver 3.12, on MongoDB 7.0² | MongoDB.Driver 3.12 on .NET 11, on MongoDB 7.0² |
| `AdCodicem.ValueObjects.Testing` | `net10.0` | xUnit v3 4 | xUnit v3 4 on .NET 11 |
| `AdCodicem.ValueObjects.Testing.Data` | `net10.0` | .NET 10 | .NET 11 |
| `AdCodicem.ValueObjects.AutoFixture` | `net10.0` | AutoFixture 4.18 | AutoFixture 4.18 on .NET 11 |
| `AdCodicem.ValueObjects.Bogus` | `net10.0` | Bogus 35 | Bogus 35 on .NET 11 |
| `AdCodicem.ValueObjects.FsCheck` | `net10.0` | FsCheck 3 | FsCheck 3 on .NET 11 |

¹ On the .NET 11 release candidate, by a CI job that installs the packages into `net11.0` applications, the
Swashbuckle package into one of its own: those of `main` after each push, a newer push cancelling an older one's run,
and those of every preview and stable release before it is published
([How the library is tested](./testing.md#the-compatibility-island)). A stable release ships only once it passes; a
preview too, once .NET 11 ships.

² MongoDB 7.0 until a MongoDB 8 release starts on GitHub's runners: every MongoDB 8.x refuses a Linux kernel 6.19 or
later ([SERVER-121912](https://jira.mongodb.org/browse/SERVER-121912)), which the runners run since 2026-10-18. The
packages ran against MongoDB 8 until then.

## Versioning

The packages follow semantic versioning from 1.0.0 on: from then, only a major version breaks the public API or
removes a member. Before 1.0.0, the version is `0.<minor>.<patch>`, and a minor version may break part of the public
API, or deprecate or remove part of it, without waiting for a major: read the
[changelog](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/CHANGELOG.md) before taking a new minor. A
patch never breaks anything, before 1.0.0 or after. A member deprecated rather than removed outright is reported by
the compiler wherever it is used, with a diagnostic naming its replacement. A member that is read by nothing any more
stays a while as a compile error that says what replaces it (`VO0021`, `VO0028`, `VO0034`, `VO0035`), and any minor
version may then remove it.

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
