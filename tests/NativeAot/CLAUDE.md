# tests/NativeAot — applications built as consumers build them

CI publishes these rather than tests them. None imports `tests/Directory.Build.props`, so the trimming and AOT analyzers
stay on where they apply. How the script works is in the header of `.github/scripts/native-aot.sh`.

```bash
# As heavy.yml's `native AOT` job runs them; need clang and zlib
.github/scripts/native-aot.sh
.github/scripts/native-aot.sh artifacts/native-aot-protobuf tests/NativeAot/AdCodicem.ValueObjects.NativeAot.ProtobufNet
.github/scripts/compiled-model.sh compile   # ci.yml's build job: optimize and build, no database
.github/scripts/compiled-model.sh jit       # heavy.yml's integration job: plus a SQL Server round trip (Docker)
.github/scripts/compiled-model.sh aot       # heavy.yml's native AOT job, Docker-free: published, never run
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

- A minimal API referencing every package that claims AOT compatibility but HotChocolate and ProtobufNet (an
  application of its own, below), serving a contract-first gRPC service over Kestrel's cleartext HTTP/2 too. Its value objects are in `AdCodicem.ValueObjects.NativeAot.Domain`, which links
  `Domain/UnderlyingTypes.cs` of the unit suite's core project (`tests/AdCodicem.ValueObjects.UnitTests`) and opts into
  XML serialization, which nothing calls, as a guard that the emission adds no warning.
- A value object added to `UnderlyingTypes.cs`, or registered by the application, goes into `AppJsonContext` with its
  underlying type, or the script reports it missing. An underlying type no other value object has also needs its texts in
  `Underlying.cs`; a value object whose rules accept none of its underlying type's texts needs one it accepts and one it
  refuses added there, or `Logging.cs` fails it ("accepts none of the texts it is probed with").
- A tool's parameter and result types go into `AppJsonContext`, never their underlying types.
- `Annotations.cs` builds its `ValidationContext` with a display name: that is the one constructor that is not
  `RequiresUnreferencedCode`. The body type `AddValidation()` checks is public, since .NET 10's validation generator
  describes public types only and never validates an internal body.
- It links hand-written value objects from the core project's `Domain/HandWritten/`, registered without a converter so
  the JSON factory's general-purpose converter runs natively, and `UnregisteredCode`, registered by nothing, which must
  stay refused.
- The application runs under `InvariantGlobalization`: no culture but the invariant one exists there, so the localized
  lines read the neutral `ProblemMessages.resx`.
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

## `AdCodicem.ValueObjects.CompiledModel`

- `compiled-model.sh compile|jit|aot` writes the model with `dotnet ef dbcontext optimize` under
  `artifacts/compiled-model/`, never committed, and builds on it. `compile` stops there, in `ci.yml`'s build job, on
  every pull request reaching EF Core or the model's own inputs: a model that no longer compiles is what this caught.
  `jit`, the same model, takes a round trip through SQL Server in `heavy.yml`'s integration job; `aot`, written with
  its queries precompiled, is published by its native AOT job and never run (two EF Core bugs stop its queries); it
  fails on a warning about this library's code only. `compile` and `jit` write the same folder, `jit/`, which
  `-p:CompiledModel=jit` compiles in.
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
