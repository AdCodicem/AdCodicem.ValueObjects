# AdCodicem.ValueObjects.RdgTests

Minimal API endpoints whose binding the Request Delegate Generator writes, over value objects declared in the endpoints'
own project.

- The project imports `src/AdCodicem.ValueObjects/build/AdCodicem.ValueObjects.props`, so `VO0033` fails its build for a
  value object that drops its contract.
- A test reads the RDG's output under the generated files (`CompilerGeneratedFilesOutputPath`), so that a change in the
  SDK's defaults cannot turn the suite into a test of the reflection-based binding.
- The RDG writes one interceptor for two handlers whose parameters share types and names, dropping the attribute of the
  second, a `[FromHeader]` included: name such parameters apart.
- Only the RDG refuses no empty query text and takes an empty header for an absent one (`ProblemEndpoints.cs`). The
  problem details filter tells its endpoints by the `GeneratedCodeAttribute` the RDG adds to their metadata, which a test
  pins, so that an SDK dropping it fails here first.
- `[AsParameters]` takes a class: the RDG of 10.0.12 does not compile a positional record of `string?` (CS8639,
  dotnet/aspnetcore#58953), and
  `Microsoft.AspNetCore.OpenApi` 10.0.12 fails the whole document for such a record with a validation attribute on a
  constructor parameter (dotnet/aspnetcore#65348). The guide uses `property:` there.
