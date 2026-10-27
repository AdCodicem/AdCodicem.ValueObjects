# AdCodicem.ValueObjects.Abstractions — the contracts

`IValueObject<TSelf, TValue>`, the hooks, `ValueObjectRegistry`, the descriptors, the DataAnnotations attributes
(`Annotations/`), `ValueObjectXml`, `ValueObjectMessages`. Every package depends on it.

## Registry and descriptors

- A change to the descriptor is invisible to the typed unit tests: extend `DescriptorTests.cs`.
- `RegisterGenericDefinition` runs in module initializers, natively too: keep what it checks AOT-safe. Its
  `IsAssignableFrom` check on the definition holds natively, constructed or not
  (`src/AdCodicem.ValueObjects.Generators/CLAUDE.md`).
- `TryResolve` keeps whatever it resolves for the process, a hand-written type described by reflection included: a test
  resolving a shared hand-written type registers it for every later test.

## DataAnnotations attributes (`[ParsesAs]`, `[NotDefault]`)

- `[ParsesAs(typeof(T))]` resolves its descriptor when a value is first validated (`ValueObjectValidationAttributes.Resolve`),
  never when created, since anything reading a member's attributes creates them; the attribute then keeps it.
- `[NotDefault]` caches a closed `IsDefault` check per type in a static cache, but never a failed resolution, so a
  registration made later is found.
- They refuse with a `ValueObjectValidationResult` (the code and the type beside the message), which `Validator.TryValidate*`
  and `ObservableValidator` keep; every other host (MVC, `AddValidation()`, options validation) keeps the message alone.
  `Validator`'s throwing methods wrap it in a `ValidationException` whose `Data` is DataAnnotations', so
  `ValueObjectErrors.TryGetCode` reads the code off its result.
- A localised message comes from a resource `ErrorMessageResourceType` names alone, read through
  `ResourceManager(typeof(T))` (base name: the type's full name, never a `ResourcesPath`), cached per type; a
  `MissingManifestResourceException` is a missing key, and the next key is looked up. The lookup is
  `src/Shared/ValueObjectResourceMessages.cs`. `ErrorMessage`, or a
  resource with its name, gives DataAnnotations' single message: MVC, .NET 10 and .NET 11 keep the message of an
  attribute naming `ErrorMessageResourceType`.
- A misuse (a member of the wrong type, a type no value object, unregistered under native AOT) throws
  `InvalidOperationException`, with no code. `VO0039` to `VO0042` report misuses at compile time.
- Hosts disagree: MVC refuses a validation attribute on a record's property (`InvalidOperationException` at the first
  validation) and wants it on the primary constructor's parameter, where neither OpenAPI generator reads a
  `[StringLength]`; .NET 10's validation generator describes public types only and turns an attribute's exception into a
  400 carrying its message, where .NET 11 lets it propagate and looks a message up under `{Attribute}_Error`; the
  `[OptionsValidator]` validator names a member `Type.Member` on .NET 10 and the member alone on .NET 11. The island pins
  the .NET 11 side (`tests/Compat`).

## XML (`ValueObjectXml`, `XsdPattern`)

- `XmlSerializer` calls the schema provider when the serializer is built, insists on finding it public and static, and
  compiles its `xs:simpleType`: one facet System.Xml cannot read refuses the type, and every serializer over a type
  holding it. System.Xml holds an `xs:integer` in a `decimal`, so `ValueObjectXml` leaves out a bound no `decimal` holds,
  checks every bound and enumeration value with `XmlSchemaDatatype.ParseValue`, and drops the rules of a type that still
  does not compile.
- System.Xml hands an XSD pattern to .NET's engine as `^(…)$`, so a bare `$` would be an anchor: `XsdPattern` writes it
  `[$]`, and `XsdPatternTests` compares every translation with .NET, value by value, through System.Xml's own validation.
- `DataContractSerializer` cannot read an `IXmlSerializable` struct without dynamic code. The native AOT domain opts into
  XML serialization only as a guard that the emission adds no warning; it calls neither serializer.
