# AdCodicem.ValueObjects.UnitTests.Serialization

The serialization family's unit tests (NewtonsoftJson, AspNetCore.NewtonsoftJson, Swashbuckle, NJsonSchema,
MessagePack, ProtobufNet, Grpc), in `BinarySerialization/` (MessagePack; protobuf-net as `Proto*`; gRPC as
`CodeFirstGrpc*` and `ContractFirstGrpc*`), `SchemaGeneration/` (NJsonSchema, Confluent), `Web/` (the Newtonsoft.Json
formatters and JSON Patch, Swashbuckle, NSwag), `NewtonsoftJsonTests.cs` at its root, and
`SerializerContractDiagnosticTests.cs` beside it. The suite's conventions are in
`tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md`, each package's traps in its `src/<Package>/CLAUDE.md`.

- protobuf-net: `RuntimeTypeModel.Default` is registered once, strict (`BinarySerialization/ProtoDefaultModel.cs`);
  every other test builds a model of its own (`ProtoWire.Model`). Never put `[ProtoContract]` on a `Domain/` type, since
  protobuf-net honours it on every model: the attribute-route value objects are the tests' own (`ProtoSku`,
  `ProtoTicket`, `ProtoBareCode`, the bare `[ProtoContract]` trap, under `#pragma warning disable VO0043`). Test
  contracts carry no `[ProtoMember]` initializer (`PBN0022`); the default-model test suppresses `PBN3010`; the one
  Google.Protobuf message of `BinarySerialization/CodeFirstGrpcContracts.cs` suppresses `PBN2008`.
- The contract-first service's messages are compiled by Grpc.Tools from `BinarySerialization/ContractFirstGrpc.proto`,
  the csproj's `Protobuf` item (`GrpcServices="Both"`).
- MessagePack: a `[MessagePackObject]` holding a value object of this assembly needs
  `[assembly: MessagePackAssumedFormattable(...)]` (`BinarySerialization/MessagePackAssumptions.cs`).
- `NSwag.OpenApiDocument` meets `Web.OpenApiDocument`, which this project links from the AspNet project: alias it
  (`NSwagOpenApiDocument`, as `Web/NSwagDocumentTests` does).
- `Web/NSwagDocumentTests` compiles NSwag's C# client with `Microsoft.CodeAnalysis.CSharp` 4.14.0, the suite's Roslyn.
- `SerializerContractDiagnosticTests` runs the generator and `VO0043`'s analyzer, loaded from the generator's assembly
  that `CopyValueObjectAnalyzers` copies to `analyzers/`, as the Data project does, over value objects carrying each
  serializer's attribute, and checks every satellite name the messages write against the satellite (`nameof`,
  `typeof`): the analyzer rides the core's release train, the packages it names the satellites'.
- Swashbuckle and NSwag documents are awaited with a timeout (`SwashbuckleDocuments.BuildAsync` in
  `Web/SwashbuckleDocument.cs`, `NSwagDocuments` in `Web/NSwagDocument.cs`), so a schema-filter hang fails the test,
  not the job's time limit.
- Confluent's serdes run over an NSubstitute `ISchemaRegistryClient`, no registry server; `librdkafka.redist` is
  referenced with `ExcludeAssets="native;build"` (about 125 MB less output: the serdes never load it).
- This project adds `Microsoft.AspNetCore.OpenApi.Generated` to `InterceptorsNamespaces`, as the AspNet project does
  and for the same reason.
- It references the aspnet family's packages too (AspNetCore.Http, AspNetCore, OpenApi), the hosts the documents and
  formatters are read through, so a change to them runs this family as well.
- It links the AspNet project's `Web/AnnotatedModels.cs`, `Web/OpenApiDocument.cs`, `Web/ProbeController.cs` and
  `Web/ProblemReading.cs`, and core's `CodeCarrying.cs`, `DataAnnotations/Annotated.cs` and `Web/ValidationMessages.*`,
  at the same paths.
