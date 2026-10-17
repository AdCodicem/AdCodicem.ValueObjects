# src/Shared — source files several packages link

Each file here is compiled into every project that links it (`<Compile Include="..\Shared\…" Link=…>`), not built as a
project. A change here changes every package in the table at once, which is the point: a description written once.

- Two copies the unit suite sees through `InternalsVisibleTo` (`src/Directory.Build.props`) are two internal types, so a
  test naming one is CS0433. A test reaches a copy through an extern alias on the project reference, or through its
  package's public API. A new project linking a file follows the same pattern.

| File | Linked by | Tests reach it through |
|---|---|---|
| `ValueObjectPatternSyntax.cs`, the reader of a .NET pattern into a tree | Abstractions, MongoDB, EntityFrameworkCore, Testing.Data, Blazor, Generators | Abstractions: `extern alias abstractions` (`ValueObjectPatternSyntaxTests`); MongoDB: `PcrePattern`; EF Core: `SqlPattern`; Testing.Data: `PatternSampler`, whose tests name no node type; Blazor: `EcmaScriptPattern` (`Forms/EcmaScriptPatterns.json`); Generators: `NonUnicodeTextAnalyzerTests` (GeneratorTests see two copies and name neither) |
| `TypeNames.cs`, a type as C# names it in a message | Testing.Data, AutoFixture, Bogus, FsCheck, EntityFrameworkCore, AzureFunctions, AzureFunctions.AspNetCore, ProtobufNet, Grpc, CommandLine | Testing.Data's copy: `extern alias testingdata` |
| `ValueObjectArguments.cs`, `ValueObjectArgumentRejection.cs`, a tool's arguments and the result a refusal answers | AI, ModelContextProtocol | Public API only (`LanguageModels/`); each `FirstRejection` overload is called by one package, and coverage merges both copies |
| `ValueObjectLocalizedMessages.cs`, a refusal's message in the application's resource | AspNetCore.Http, AspNetCore, AzureFunctions.AspNetCore, HotChocolate, Blazor | Public API only (the localized tests of `Web/`, `Functions/`, `GraphQL/`, `Forms/`) |
| `ValueObjectLocalizedMessages.AspNetCore.cs`, the part reading `ValueObjectMessageOptions` | AspNetCore.Http, AspNetCore, AzureFunctions.AspNetCore | Public API only |
| `ValueObjectSchemaKeywords.cs`, what a rule becomes as a schema keyword | OpenApi, Swashbuckle, Json, HotChocolate, NJsonSchema | Public API only |
| `ValueObjectOpenApiSchema.cs`, the whole description of a value object on `Microsoft.OpenApi` 2 | OpenApi, Swashbuckle | Public API only |
| `ValueObjectText.cs`, how a value object's text is read and written for a person, a date in the Gregorian calendar | Blazor, CommandLine | Public API only (`Forms/`, `Cli/`); `FormsParity` through Blazor's internal `ValueObjectInputText<TSelf, TValue>.Read` and `Write`, which call it |
| `ValueObjectResourceMessages.cs`, a refusal's message from a resource through a `ResourceManager`, with no services | Abstractions, CommandLine | Public API only (the localized tests of `DataAnnotations/` and `Cli/`) |
| `ValueObjectBindingText.cs`, how the Functions worker's text is read | AzureFunctions, AzureFunctions.AspNetCore | Public API only (`Functions/`) |
| `KnownValueSpellings.cs`, whether a closed set of text stores another spelling of a known value | MongoDB, EntityFrameworkCore | MongoDB: the validator's `enum` (`MongoDbSchemaTests`); EF Core: the CHECK constraint's `IN` (`EntityFrameworkCoreCheckConstraintTests`) |

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
  with each (Blazor's `ValueObjectInputText`), so that the CsvHelper package, which links it next, keeps its own too.
- `ValueObjectBindingText.cs` is linked by both Azure Functions packages so that the middleware parses a parameter again
  exactly as the converter parsed it.
