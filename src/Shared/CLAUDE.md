# src/Shared — source files several packages link

Each file here is compiled into every project that links it (`<Compile Include="..\Shared\…" Link=…>`), not built as a
project. A change here changes every package in the table at once, which is the point: a description written once.

- Two copies one test project sees through `InternalsVisibleTo` (`src/Directory.Build.props`, which names each unit
  project) are two internal types, so a test naming one is CS0433. A unit project sees the copies its references bring:
  those of Abstractions, Json and Testing.Data in every one (`tests/UnitTests.props`), another package's in the projects
  whose references reach it. A test reaches a copy through an extern alias on the project reference, or through its
  package's public API. A new project linking a file follows the same pattern.
- In the table, `UnitTests` is the core project, `tests/AdCodicem.ValueObjects.UnitTests`, and `UnitTests.<Family>` a
  family's, `tests/AdCodicem.ValueObjects.UnitTests.<Family>` (`.github/ci-families.json`).

| File | Linked by | Tests reach it through |
|---|---|---|
| `ValueObjectPatternSyntax.cs`, the reader of a .NET pattern into a tree | Abstractions, MongoDB, EntityFrameworkCore, Testing.Data, Blazor, Generators | Abstractions: `extern alias abstractions` (`ValueObjectPatternSyntaxTests` of `UnitTests`); MongoDB: `PcrePattern` (`UnitTests.Data`); EF Core: `SqlPattern` (`UnitTests.EfCore`); Testing.Data: `PatternSampler`, whose tests (`UnitTests`) name no node type; Blazor: `EcmaScriptPattern` (`Forms/EcmaScriptPatterns.json` of `UnitTests.Text`); Generators: `NonUnicodeTextAnalyzerTests` (GeneratorTests see two copies and name neither) |
| `TypeNames.cs`, a type as C# names it in a message | Testing.Data, AutoFixture, Bogus, FsCheck, EntityFrameworkCore, AzureFunctions, AzureFunctions.AspNetCore, ProtobufNet, Grpc, CommandLine, CsvHelper, Marten | Testing.Data's copy: `extern alias testingdata`, which `tests/UnitTests.props` gives every unit project (`SamplerTests` of `UnitTests`, `FunctionsParity` of `UnitTests.Hosts`, `CsvParity` of `UnitTests.Text`) |
| `ValueObjectArguments.cs`, `ValueObjectArgumentRejection.cs`, a tool's arguments and the result a refusal answers | AI, ModelContextProtocol | Public API only (`LanguageModels/` of `UnitTests.Observability`); each `FirstRejection` overload is called by one package, and coverage merges both copies |
| `ValueObjectLocalizedMessages.cs`, a refusal's message in the application's resource | AspNetCore.Http, AspNetCore, AzureFunctions.AspNetCore, Wolverine.Http, HotChocolate, Blazor | Public API only (the localized tests of `Web/` of `UnitTests.AspNet`, `Functions/` and `WolverineHttp/` of `UnitTests.Hosts`, `GraphQL/` of `UnitTests.GraphQL`, `Forms/` of `UnitTests.Text`) |
| `ValueObjectLocalizedMessages.AspNetCore.cs`, the part reading `ValueObjectMessageOptions` | AspNetCore.Http, AspNetCore, AzureFunctions.AspNetCore, Wolverine.Http | Public API only |
| `ValueObjectRefusals.cs`, the refusals of a request and the validation problem that answers them | AspNetCore.Http (beside its `AddBody` part), AzureFunctions.AspNetCore, Wolverine.Http | Public API only (`Web/` of `UnitTests.AspNet`, `Functions/` and `WolverineHttp/` of `UnitTests.Hosts`); coverage merges the three copies by path |
| `ValueObjectSchemaKeywords.cs`, what a rule becomes as a schema keyword | OpenApi, Swashbuckle, Json, HotChocolate, NJsonSchema | Public API only |
| `ValueObjectOpenApiSchema.cs`, the whole description of a value object on `Microsoft.OpenApi` 2 | OpenApi, Swashbuckle | Public API only |
| `ValueObjectText.cs`, how a value object's text is read and written for a person, a date in the Gregorian calendar | Blazor, CommandLine, CsvHelper | Public API (`Forms/`, `Cli/`, `Csv/` of `UnitTests.Text`, which sees the three copies); `FormsParity` through Blazor's internal `ValueObjectInputText<TSelf, TValue>.Read` and `Write`, which call it; the CsvHelper copy's `GregorianCultures`: `extern alias csvhelper` (`CsvCultureTests`) |
| `ValueObjectResourceMessages.cs`, a refusal's message from a resource through a `ResourceManager`, with no services | Abstractions, CommandLine | Public API only (the localized tests of `DataAnnotations/` of `UnitTests` and `Cli/` of `UnitTests.Text`) |
| `ValueObjectBindingText.cs`, how the Functions worker's text is read | AzureFunctions, AzureFunctions.AspNetCore | Public API only (`Functions/` of `UnitTests.Hosts`) |
| `KnownValueSpellings.cs`, whether a closed set of text stores another spelling of a known value | MongoDB, EntityFrameworkCore | MongoDB: the validator's `enum` (`MongoDbSchemaTests` of `UnitTests.Data`); EF Core: the CHECK constraint's `IN` (`EntityFrameworkCoreCheckConstraintTests` of `UnitTests.EfCore`) |

- `PatternSampler` (Testing.Data) draws strings from the tree, and leaves a pattern outside its subset to rejection
  sampling, over the tree of the pattern read without its lookarounds and with its categories reduced to printable ASCII.
- `ValueObjectPatternSyntax.cs` also compiles for `netstandard2.0`, since the generator links it for `VO0047`: no
  `char.IsAsciiLetterOrDigit` (a local helper), no `char.IsAscii` (`<= '\x7F'`), no span parsing (`Substring` under
  `#if NETSTANDARD2_0` in `ReadHexadecimal`).
- `ValueObjectOpenApiSchema.cs` holds what the OpenAPI transformer and the Swashbuckle filters share; what is particular
  to each host stays with it (finding the value object, the options, a closed set's enumeration name, parameters,
  containers). A member or parameter carrying `[ParsesAs]` is described as the value object's text
  (`Describe(..., asText: true)`), the attribute found on the property, the constructor parameter of the same name and
  the parameter, an MVC query model's member included, with `GetCustomAttributes(...).OfType<ParsesAsAttribute>()`,
  since both forms may sit on one member; a stricter rule the declaration put on the schema is kept (`RulesOf`,
  `KeepStricterRules`).
- `ValueObjectText.cs` holds no rule of a host: what empty text means and what a default instance is written as stay
  with each (Blazor's `ValueObjectInputText`, the CsvHelper converter).
- `ValueObjectText.cs` keeps the Gregorian copy of a read-only culture (`GregorianCultures`, a `ConditionalWeakTable`),
  never of a writable one, which may change after the copy is taken: a clone per field cost a CSV import of 200,000
  `th-TH` dates 3.9 s against 0.25 s with the copy kept. The copy is read-only, since threads share it.
- `ValueObjectText<,>.TryParse` names a reason-less refusal by `typeof(TSelf).Name`, as the descriptor and `[ParsesAs]`
  do, which reads ``Reference`1`` for a generic type; a host naming types through `TypeNames.cs` passes `unexplained`
  and words the refusal itself, as the CsvHelper converter does (Blazor links no `TypeNames.cs`).
- `ValueObjectBindingText.cs` is linked by both Azure Functions packages so that the middleware parses a parameter again
  exactly as the converter parsed it.
