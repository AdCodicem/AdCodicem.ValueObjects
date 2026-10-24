# tests/Compat — the .NET compatibility island

The thirty-eight packages exactly as packed, installed from `artifacts/packages`, each at the version in its file
name, into `net11.0` applications on the .NET 11 release candidate. It checks the packages, not the code. Runbook:
`docs/maintaining.md`, "The .NET compatibility island".

```bash
# From the repository root: pack into an empty folder at a fresh version, every release train at it, since NuGet reuses
# whatever ~/.nuget/packages holds at a version it has seen, and the island installs every package the folder holds
v=0.0.0-compat.$(date +%s)
rm -rf artifacts/packages
RELEASE_TRAIN_VERSIONS="core=$v satellites=$v" \
  dotnet pack src/AdCodicem.ValueObjects.Packages.slnf -c Release -o artifacts/packages
# Then from this folder
cd tests/Compat && dotnet test --project AdCodicem.ValueObjects.CompatTests.csproj
# Without Docker, add --filter-not-trait "Requires=Docker": the container tests fail rather than skip
dotnet test --project Swashbuckle/AdCodicem.ValueObjects.CompatTests.Swashbuckle.csproj   # no Docker
```

- Invisible to the root tooling: its own `global.json` (the RC SDK CI installs exactly; `rollForward: latestFeature` lets a
  later 11.0 SDK run it locally), `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props` and
  `nuget.config`, in no solution. The root build, coverage and Dependabot never see it; `ci.yml`'s format job
  (`dotnet format whitespace . --folder`) checks its `.cs` files against the root `.editorconfig`. Run it from this
  folder: from the root, the root `global.json` selects SDK 10 and the build stops.
- Tools that walk the repository do see it: CodeQL downloads its SDK, and GitHub's dependency submission restores it with
  SDK 10 from the root, which is why its project files leave themselves empty on an SDK that cannot target `net11.0`.
- Transitive pinning is off, so the packages' dependency floors meet the next major as an application's would.
- Its SDK and its .NET 11 packages are bumped by hand, together. Npgsql's provider pins EF Core exactly, so EF Core follows
  Npgsql's release candidate, not the SDK's: bump them in the same change.
- `Swashbuckle/` is a second project, excluded from the main one's sources (`DefaultItemExcludes`): Swashbuckle 10 fails
  on the `Microsoft.OpenApi` 3 that `Microsoft.AspNetCore.OpenApi` 11 brings. `compat (.NET 11)` runs both, against the
  packages `heavy.yml`'s caller packed or, on `main` and a pull request, the ones the job packs itself; documents are
  awaited with a timeout.
- This is the only place EF Core 11, JSON Patch 11, MVC 11 and Components 11 run, so it is where a member removed or an
  `Internal` type changed upstream shows. It pins the .NET 11 side of the DataAnnotations hosts (`{Attribute}_Error`
  keys, an attribute's exception propagated, the member alone as display name) and of Blazor (messages read off the
  `EditContext`, never the markup; `ValidateAsync()`).
- The System.CommandLine package, built against 2.0, runs on System.CommandLine 3.0's release candidate
  (`CommandLineTests.cs`), whose version carries .NET's build number: bump it with the island's other .NET 11 packages,
  and to 3.0.0 when .NET 11 ships.
- The Orleans package is exercised at serializer level (`OrleansTests.cs`): it brings Orleans' serializer alone, and no
  grain, no Microsoft.Orleans.Sdk and no Orleans generator run here.
- The CsvHelper package brings CsvHelper at its floor, 33.1.0, which the island pins nowhere: an application without
  central package management gets that version (`CsvHelperTests.cs`).
- The Marten package brings Marten at its floor, which the island pins nowhere, and runs against the island's
  PostgreSQL container (`MartenTests.cs`), through async calls alone, the only ones Marten 9 supports.
- The Wolverine.Http package runs through Wolverine's dynamic mode over TestHost (`WolverineHttpTests.cs`): the island
  pins `WolverineFx.RuntimeCompilation` at the version of the WolverineFx.Http the package brings at its floor; bump
  them together when the package's floor moves. Its endpoint class is named apart (`IslandWolverineEndpoints`), and the
  discovery is narrowed to it, since Wolverine scans the island's assembly.
- MessagePack's analyzer and protobuf-net's generator run in the SDK 11 compiler here: the island has its own
  `MessagePackAssumedFormattable` list and its own `PBN9001` suppression.
- Its MongoDB tests run `mongo:7.0`, as the integration suite's do: MongoDB 8.x does not start on the runners' Linux
  kernel (SERVER-121912, `tests/AdCodicem.ValueObjects.IntegrationTests/CLAUDE.md`).
- Its MongoDB tests go through one static, strict registration first, as the suites' do (the driver's registry is
  process-wide). `librdkafka.redist` is referenced with `ExcludeAssets="native;build"`: Confluent's serdes never load the
  Kafka client's native library.
- The namespace rule of the root `CLAUDE.md` holds here too: `AdCodicem.ValueObjects.CompatTests` is an
  `AdCodicem.ValueObjects.*` namespace.
- Packed without `RELEASE_TRAIN_VERSIONS` (`heavy.yml` with no caller's packages), the packages take each train's
  version from its own tags, and the pack stops when it finds none, so the checkout fetches them (`fetch-depth: 0`). A
  commit tagged stable for the satellites alone fails that pack with NU5104, since a stable satellite cannot depend on
  the core's next prerelease (`.github/CLAUDE.md`): never dispatch `heavy.yml` there. One tagged for the core alone
  packs.
- `compat (.NET 11)` is a job of `heavy.yml`, not a required check, and never becomes one (ADR 0013). It gates every
  stable release (`compat-blocks: true`), and every preview once .NET 11 ships; until then, and on `main` and a pull
  request labelled `ci:heavy`, it only informs. No coverage measures it.
