# tests/NativeAot — applications built as consumers build them

CI publishes these rather than tests them. None imports `tests/Directory.Build.props`, so the trimming and AOT analyzers
stay on where they apply. How the script works is in the header of `.github/scripts/native-aot.sh`.

```bash
# As ci.yml's `native AOT` job runs them, and its `native AOT (Marten)` job the third; need clang and zlib
.github/scripts/native-aot.sh
.github/scripts/native-aot.sh artifacts/native-aot-protobuf tests/NativeAot/AdCodicem.ValueObjects.NativeAot.ProtobufNet
NATIVE_AOT_POSTGRES="Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres" \
  .github/scripts/native-aot.sh artifacts/native-aot-marten tests/NativeAot/AdCodicem.ValueObjects.NativeAot.Marten
.github/scripts/compiled-model.sh aot   # Docker-free: published, never run
.github/scripts/compiled-model.sh jit   # as the build job: optimize, build, a round trip through SQL Server (Docker)
```

## How a run is judged

- `native-aot.sh [out] [project]` runs the application's fixed script once under the JIT and once as the native binary,
  and fails on any difference between the outputs, or on a trimming or AOT warning the project's `aot-warnings.allowed`
  does not list.
- The JIT run turns on the RDG and turns off reflection-based serialization and dynamic code, as `PublishAot` does, so the
  outputs differ only where native AOT changes something: a branch guarded by `RuntimeFeature.IsDynamicCodeSupported`
  takes the same side in both runs, while an unguarded `MakeGenericType` still runs under the JIT and fails natively.
- `PublishAot` is set by the project when `NativeAot` is true, never as `-p:PublishAot`, which would reach the
  `netstandard2.0` generator.
- The AOT compiler reads compiled code, so a `#pragma` silences nothing there: a warning a library suppresses takes
  `[UnconditionalSuppressMessage]` and a guard.

## `AdCodicem.ValueObjects.NativeAot`

- A minimal API referencing every package that claims AOT compatibility but HotChocolate, HotChocolate.Data,
  ProtobufNet, Marten (an application of its own, below), Orleans (Orleans 10.4 does not start natively:
  `src/AdCodicem.ValueObjects.Orleans/CLAUDE.md`) and CsvHelper (its record API has no native code:
  `src/AdCodicem.ValueObjects.CsvHelper/CLAUDE.md`), serving a contract-first gRPC service over Kestrel's cleartext
  HTTP/2 too. Its value objects are in `AdCodicem.ValueObjects.NativeAot.Domain`, which links the unit suite's
  `Domain/UnderlyingTypes.cs` and opts into XML serialization, which nothing calls, as a guard that the emission adds no
  warning.
- A value object added to `UnderlyingTypes.cs`, or registered by the application, goes into `AppJsonContext` with its
  underlying type, or the script reports it missing. An underlying type no other value object has also needs its texts in
  `Underlying.cs`; a value object whose rules accept none of its underlying type's texts needs one it accepts and one it
  refuses added there, or `Logging.cs` fails it ("accepts none of the texts it is probed with").
- A tool's parameter and result types go into `AppJsonContext`, never their underlying types.
- `Annotations.cs` builds its `ValidationContext` with a display name: that is the one constructor that is not
  `RequiresUnreferencedCode`. The body type `AddValidation()` checks is public, since .NET 10's validation generator
  describes public types only and never validates an internal body.
- It links hand-written value objects from `Domain/HandWritten/`, registered without a converter so the JSON factory's
  general-purpose converter runs natively, and `UnregisteredCode`, registered by nothing, which must stay refused.
- `Cli.cs` parses a System.CommandLine command holding an option of every registered value object, of its nullable type
  and of an array of it, and fails a line where an accepted text reads otherwise than `TryParse`, or a refusal carries
  another code, or an invocation runs the action of a refused line, the command's options refused under its subcommand
  included, or a localized message names a token among several otherwise than from zero (`--quantities[2]`). The
  application runs under `InvariantGlobalization`: no culture but the invariant one exists there, so
  the localized lines read the neutral `ProblemMessages.resx`.
- `WolverineHttp.cs` serves Wolverine.Http endpoints in Wolverine's static mode, in an application of their own built by
  `WolverineHttp.Build()`. Their code is never committed: `native-aot.sh` writes it first, `WOLVERINE_CODEGEN=write
  dotnet run -- codegen write`, under the JIT, into `artifacts/wolverine-codegen` (the folder `WOLVERINE_CODEGEN_PATH`
  names), and the JIT run and the publish compile it in with `WOLVERINE_CODEGEN=read`. Without it, as in the solution's
  build and `dotnet format`, the application builds and its Wolverine endpoints refuse to start; `dotnet run` outside
  the script needs the same two passes. Endpoint types are listed in `Build()`'s discovery, and a line fails when a
  refused value reaches its handler or an accepted one is refused.
- Microsoft.AspNetCore.Components warns IL2104 and IL3053 for itself once a component renders: listed in
  `aot-warnings.allowed` and in `WarningsNotAsErrors`, so the AOT compiler finishes under `NativeAot`.
- Serilog destructures no object under `PublishTrimmed`: the project sets `Serilog.Capturing.IsStructureValueSupported` to
  false for its JIT run too, conditioned on `PublishTrimmed` being off, or every `{@object}` line would differ.

## `AdCodicem.ValueObjects.NativeAot.ProtobufNet`

- protobuf-net's compile-time model (`AppModel`) over the twenty value objects of the linked `UnderlyingTypes.cs` that
  protobuf-net writes, each naming its serializer on a partial declaration in `ProtoContracts.cs`, and two of its own at
  `Level300`. A new value object protobuf-net writes needs a partial there.
- protobuf-net's generated model warns at every call, so the Roslyn trim and AOT analyzers are off
  (`EnableTrimAnalyzer`, `EnableAotAnalyzer`) and `WarningsNotAsErrors` holds `IL2026;IL2104;IL2111;IL3050;IL3053`;
  `native-aot.sh` then lets through only what its `aot-warnings.allowed` lists.

## `AdCodicem.ValueObjects.NativeAot.Marten`

- A Marten document store over the PostgreSQL that `NATIVE_AOT_POSTGRES` names, in the schema `native_aot_marten`, which
  each run cleans first: in CI, the `postgres:17-alpine` service container of `native AOT (Marten)`; locally, any
  PostgreSQL (`docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=postgres postgres:17-alpine`). Without it the application
  exits 2.
- `native AOT (Marten)` is a job of its own beside `native AOT`, so that the required check keeps its duration: the
  native AOT compiler alone takes about 20 minutes over Marten, on one core, and the script 17.5 to 24 minutes on a warm
  4-core machine, hence its 45-minute timeout. It is not a required check until the maintainer adds it to the ruleset.
- Its documents hold the main application's domain (`AdCodicem.ValueObjects.NativeAot.Domain`), the construction of a
  generic value object and `HandWrittenCode`, both registered in `Program.cs`, and `UnregisteredCode`, which the member
  source must refuse; `UseValueObjectId` declares the identity. Serialization goes through `MartenJsonContext`'s
  options: a document type added needs its `[JsonSerializable]` there, and a `Schema.For<T>()` in the store's
  configuration, or Marten closes `DocumentMappingBuilder<T>` at run time and the native binary throws
  `MissingMethodException`.
- A scenario whose outcome is a refusal in both runs (a compiled query's single value object, whose member takes a
  targeted `#pragma warning disable VO0048`, the generator being an analyzer of the project) is written with
  `LineAsync`, which writes the exception's type and first line; `AnswersAsync` fails the process where the server and
  the documents in memory answer otherwise. Marten's duplicated field selected is written with `RefusedAsync`, which
  writes that it was refused alone: Marten refuses the result type under the JIT (`NotSupportedException`) and a native
  image lacks its `DataSelectClause<TSelf?>` (`MissingMethodException`), both Marten's, before a row is read.
- Marten, JasperFx, Weasel and FastExpressionCompiler warn for themselves: `aot-warnings.allowed` matches them by their
  Source Link paths (`/_/src/Marten/`, `/_/src/Weasel.Storage/`) and assemblies, never a member of this library, and
  `WarningsNotAsErrors` holds `IL2026;IL2104;IL3053` under `NativeAot`, so the AOT compiler finishes.

## `AdCodicem.ValueObjects.CompiledModel`

- `compiled-model.sh jit|aot` writes the model with `dotnet ef dbcontext optimize` under `artifacts/compiled-model/`,
  never committed, and builds on it. `jit` takes a round trip through SQL Server in the build job; `aot`, written with
  its queries precompiled, is published by the native AOT job and never run (two EF Core bugs stop its queries); it fails
  on a warning about this library's code only.
- A change to a convention shows in the model `optimize` writes, never in a publish warning: EF Core builds no model
  natively, so the AOT compiler drops the conventions.
- `ShopContext.OnConfiguring` calls `UseValueObjectQueries()` and `UseValueObjectCheckErrors()`, so the design-time
  factory precompiles with the translator. The CHECK constraints are in no compiled model: `EnsureCreated` builds the
  design-time model and creates them.
- A strict context cannot track on a compiled model, so the `jit` round trip reads untracked (`UNTRACKED_READS`).
- `dotnet-ef` is pinned in `.config/dotnet-tools.json` to the version of `Microsoft.EntityFrameworkCore.Design`: Dependabot's
  `dotnet` group bumps them together.
- Transitive pinning is off, since Design depends on a later Roslyn than the generator's pin: the project therefore
  references `System.Security.Cryptography.Pkcs` itself, the pin raising it above the vulnerable version SqlClient asks for.
- EF Core 10's query precompilation accepts, and `Program.cs` is written around it: the context held in a local, over
  locals (a context parameter is a "dynamic" query, a method parameter inside the query throws); tracked queries, on
  entity types that are not sealed; no model type named as one of EF Core's internal ones (`Reference<T>` is CS0104);
  `Contains` over an array called as `IEnumerable<T>` (C# 14 binds the span overload), and over a `List<T?>` handed a local
  of type `T?`; no value object nested in a generic type ("Couldn't find nested type"); no local of type `ushort`, `uint`
  or `ulong` compared with `.Value` on SQL Server, whose parameter a precompiled query fails to map natively.
