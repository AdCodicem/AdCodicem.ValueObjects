# tests/Compat — the .NET compatibility island

The thirty-three packages exactly as packed, installed from `artifacts/packages` at one fresh version into `net11.0`
applications on the .NET 11 release candidate. It checks the packages, not the code. Runbook:
`docs/maintaining.md`, "The .NET compatibility island".

```bash
# From the repository root: pack at a fresh version, since NuGet reuses whatever ~/.nuget/packages holds at a version it has seen
v=0.0.0-compat.$(date +%s)
MINVERVERSIONOVERRIDE=$v dotnet pack src/AdCodicem.ValueObjects.Packages.slnf -c Release -o artifacts/packages
# Then from this folder
cd tests/Compat && dotnet test --project AdCodicem.ValueObjects.CompatTests.csproj -p:AdCodicemVersion=$v
# Without Docker, add --filter-not-trait "Requires=Docker": the container tests fail rather than skip
dotnet test --project Swashbuckle/AdCodicem.ValueObjects.CompatTests.Swashbuckle.csproj -p:AdCodicemVersion=$v   # no Docker
```

- Invisible to the root tooling: its own `global.json` (the RC SDK CI installs exactly; `rollForward: latestFeature` lets a
  later 11.0 SDK run it locally), `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props` and
  `nuget.config`, in no solution. The root build, `dotnet format`, coverage and Dependabot never see it. Run it from this
  folder: from the root, the root `global.json` selects SDK 10 and the build stops.
- Tools that walk the repository do see it: CodeQL downloads its SDK, and GitHub's dependency submission restores it with
  SDK 10 from the root, which is why its project files leave themselves empty on an SDK that cannot target `net11.0`.
- Transitive pinning is off, so the packages' dependency floors meet the next major as an application's would.
- Its SDK and its .NET 11 packages are bumped by hand, together. Npgsql's provider pins EF Core exactly, so EF Core follows
  Npgsql's release candidate, not the SDK's: bump them in the same change.
- `Swashbuckle/` is a second project, excluded from the main one's sources (`DefaultItemExcludes`): Swashbuckle 10 fails on
  the `Microsoft.OpenApi` 3 that `Microsoft.AspNetCore.OpenApi` 11 brings. `compat (.NET 11)` runs both, against the
  packages the build job packed; documents are awaited with a timeout.
- This is the only place EF Core 11, JSON Patch 11, MVC 11 and Components 11 run, so it is where a member removed or an
  `Internal` type changed upstream shows. It pins the .NET 11 side of the DataAnnotations hosts (`{Attribute}_Error`
  keys, an attribute's exception propagated, the member alone as display name) and of Blazor (messages read off the
  `EditContext`, never the markup; `ValidateAsync()`).
- MessagePack's analyzer and protobuf-net's generator run in the SDK 11 compiler here: the island has its own
  `MessagePackAssumedFormattable` list and its own `PBN9001` suppression.
- Its MongoDB tests go through one static, strict registration first, as the suites' do (the driver's registry is
  process-wide). `librdkafka.redist` is referenced with `ExcludeAssets="native;build"`: Confluent's serdes never load the
  Kafka client's native library.
- The namespace rule of the root `CLAUDE.md` holds here too: `AdCodicem.ValueObjects.CompatTests` is an
  `AdCodicem.ValueObjects.*` namespace.
- `compat (.NET 11)` is not a required check, and no coverage measures it, until .NET 11 ships.
