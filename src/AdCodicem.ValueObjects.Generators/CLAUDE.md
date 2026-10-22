# AdCodicem.ValueObjects.Generators — the generator and the analyzers

Targets `netstandard2.0`, ships inside the `AdCodicem.ValueObjects` package (not packable on its own). Tests:
`tests/AdCodicem.ValueObjects.GeneratorTests`.

## Emitting

- `SymbolDisplayFormat.FullyQualifiedFormat` implies `UseSpecialTypes` (`string`, not `global::System.String`), which
  breaks metadata resolution of every primitive: use `ValueObjectGenerator.QualifiedFormat`.
- The syntax predicate admits any `TypeDeclarationSyntax`, not only structs, so a class or a record struct reaches
  `VO0002` instead of silently generating nothing. Keep it so.
- `static virtual` and `static abstract` interface members are reachable only through a type parameter (CS8926, CS0103
  for an explicit implementation): `INumericValueObject`'s defaults are unusable directly, so emit concrete members, and
  reach something like `Guid.TryFormat`'s explicit four-argument overload through `UnderlyingValue`'s constrained generic
  bridges, which the JIT devirtualizes.
- An attribute cannot name a type through a type parameter, and neither System.Text.Json nor `TypeDescriptor` closes an
  open generic converter (`typeof(Code<>.ValueJsonConverter)` throws): a generic value object's `[JsonConverter]` and
  `[TypeConverter]` name `GenericValueObjectJsonConverterFactory` and `GenericValueObjectTypeConverter`, in the contracts,
  which reach the construction through its descriptor, so a construction registered by hand needs no reflection.
- Generated code names the types around a value object through their type parameters, from inside it, so a nested type
  or a type parameter of the same name in between is `VO0019` (`website/docs/reference/diagnostics.md`).
- `XmlSerializableEmitter` writes only in an assembly marked `[assembly: ValueObjectXmlSerialization]`, and leaves alone a
  type that implements `IXmlSerializable` or carries `[XmlSchemaProvider]` itself. The flag and its `Namespace` are read
  off the compilation inside the transform, so the transform runs again on every compilation and the model's equality
  keeps the emission cached. The emitted `ReadXml` assigns through `Unsafe.AsRef(in this)`; `ValueObjectXml` reads
  through `TSelf.TryCreate`.

## Registration

- A `private` or `protected` value object is reachable only from the type declaring it: the module initializer calls a
  step nested in the type declaring the outermost private or protected type, each step calls the next, and the last
  registers it (`ValueObjectModel.RegistrationRoute`). Each step is a static class of its own, named after its position:
  a member of the author's type would run its static constructor from the module initializer, and one name for every
  position would be CS0108 where a type of the route derives from another.
- `RegisterGenericDefinition` runs in the module initializer of every assembly declaring a generic value object, natively
  too, where a throw stops the application before it starts: whatever it checks must hold under native AOT. Its
  `typeof(IValueObject).IsAssignableFrom(definition)` does, whether or not anything constructs the definition: the
  `native AOT` job's `generic definitions` line lists `DocumentNumber<>`, constructed by the application, and
  `ArchiveLabel<>`, constructed nowhere (#179).

## Analyzer surface

- `VO0033` reads `EnableRequestDelegateGenerator` and `VO0042` reads `PublishAot` through the `CompilerVisibleProperty`
  items of `src/AdCodicem.ValueObjects/build/AdCodicem.ValueObjects.props`. NuGet imports them into a consumer; a project
  referencing the generator by project must `<Import>` that file itself, as RdgTests does, or the analyzer sees nothing.
- `KnownValueMemberCodeFixProvider` (`src/AdCodicem.ValueObjects.CodeFixes`) rewrites the obsolete `[KnownValue]` constructor taking a name and a
  value (`VO0034`).

## The logging analyzer (`Analyzers/DataClassificationAnalyzer.cs`, `VO0045`, `VO0046`)

- Microsoft.Extensions.Telemetry.Abstractions replaces the `[LoggerMessage]` generator wherever it reaches, project
  references included, whatever `ExcludeAssets` says: its `buildTransitive` props set
  `DisableMicrosoftExtensionsLoggingSourceGenerator`, and its Microsoft.Gen.Logging analyzer takes over.
- The analyzer's `LoggedProperties` and `Expandable` follow Microsoft.Gen.Logging 10.11.0, read from its assembly, and
  `GeneratorTests/LoggingGeneratorAssumptionsTests.cs` pins each behaviour. Reported as dotnet/extensions#7855 (a type's
  classification), #7856 (expansion and `Transitive`), #7857 (CS7036) and #7858 (a stack overflow on `Transitive`):
  when one is fixed, the matching assumption test fails first. Gen.Logging:
  - reads a classification on a parameter or a property only, never on the type of a value it logs whole;
  - passes a value object of the same project as its `ToString()`, and expands one of another assembly, or written by
    hand, into its public properties under `[LogProperties]`, which then carry the type's classification alone;
  - reads `Transitive` on the parameter alone (`[Experimental("EXTEXP0003")]`), and under it logs a property
    `[LogPropertyIgnore]` leaves out, whole and in clear; never calls a property's `[TagProvider]`;
  - takes a record constructor parameter's classification as its property's, whatever its type, and a classification of
    the type it expands, or of a base type, as each property's;
  - writes a collection it does not expand element by element;
  - builds a classification as `new TAttribute().Classification`, so an attribute written with arguments is CS7036 in its
    file, and refuses a record logged whole whose property is classified (`LOGGEN035`, then CS8795).
- Gen.Logging implements each method with `[GeneratedCode("Microsoft.Gen.Logging", …)]`, which C# merges onto the
  author's definition. The analyzer therefore uses `GeneratedCodeAnalysisFlags.Analyze` without `ReportDiagnostics`,
  reports on the author's parameter, and reads only a method whose implementation carries that attribute. A test of that
  gate puts the implementation in a `.g.cs` tree of its own: in the author's file, the attribute makes the location
  generated code, where nothing is reported.
- Through logging, the HMAC redactor's hash of a value differs from its own `Redact` of it, and with the tag name
  (observed on 10.11.0): compare a value object's hash only with its underlying value's, logged under the same name.

## The Marten analyzer (`Analyzers/MartenCompiledQueryAnalyzer.cs`, `VO0048`)

- It matches ``Marten.Linq.ICompiledQuery`2`` and `Marten.Events.CodeGeneration.MartenIgnoreAttribute` by full
  metadata name (`SerializerContractAnalyzer.FullName`), gated on `GetTypesByMetadataName`, so the generator takes no
  Marten reference and stays on the 4.14 pins. Every compiled-query interface of Marten 9.50 extends
  ``ICompiledQuery`2``; `ICompiledQueryMarker` alone is no compiled query (`RegisterCompiledQueryType` refuses it), so
  it is not matched.
- The members are those `CompiledQueryPlan.findMembers` reads: public instance fields and properties, inherited,
  read-only, private-getter, write-only and indexers included, a member marked `[MartenIgnore]` or an attribute
  deriving from it excluded, `[JasperFxIgnore]` not, since only Marten.SourceGenerator honours it. Reflection hands
  Marten the most derived declaration of an overridden property with its own attributes alone (JasperFx's
  `HasAttribute` calls `MemberInfo.IsDefined`, which ignores `inherit` on a property): the walk skips what an override
  replaces, before reading its attributes.
- The remedy follows Marten 9.50's parameter finders (`string`, `Guid`, `int`, `long`, `float`, `decimal`, `DateTime`,
  `DateTimeOffset`), which the unit suite's `MartenInternalsTests` pins: a Marten release binding another type fails
  there first, and the message then names it. Never name a widening Marten matches by value: a `float` member against a
  `double`, or a `bool` computed from an `int` member, makes Marten answer every call with the first call's value.
- Marten's finder writes a member's value as it stands, never through the package: the `DateTimeOffset` form says to
  set it in UTC, since Npgsql refuses any other offset (the integration suite pins the refusal).
- The collection form names an array or a list (`IList<T>`): `CompiledQueryPlan.sortMembers` hands the package no other
  collection, and Marten refuses an `IReadOnlyList<T>`, an `IEnumerable<T>` or a `HashSet<T>` member on every call.
- The `.Value` and collection forms read right only under the Marten package's `UseValueObjects()` (without it,
  Marten reads a `Value` no document holds and answers no rows): the message names it unconditionally. Never gate it on
  the compilation referencing the package, since a query type often lives in a library that does not reference what its
  host uses.
- The identity clause approximates `ValueTypeIdGeneration.tryMatch`: a struct declared public, whatever contains it
  (Marten reads `IsPublic || IsNestedPublic`), with exactly one public property, static ones counted, of `int`, `long`,
  `Guid` or `string`, the member nullable or not. `tryMatch` also wants no public constructor of more than one parameter
  and a public constructor or static method returning the struct from that property's type alone, which every
  generated value object meets (`Create`): a value object written by hand failing one gets the clause anyway, whose
  collection form binds all the same. Marten reads no `.Value` of an identity (pinned too), so the clause names a
  collection.
- Its test (`MartenCompiledQueryAnalyzerTests`) compiles against stand-ins of Marten's types; the unit suite's
  `MartenCompiledQueryDiagnosticTests` runs the copy the compiler ran over it against the real Marten, which checks the
  names.
