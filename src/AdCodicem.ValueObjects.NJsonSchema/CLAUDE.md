# AdCodicem.ValueObjects.NJsonSchema — NJsonSchema, NSwag, Confluent's serdes

User-facing behaviour: `website/docs/how-to/openapi.md`, `messaging.md`. Tests: `SchemaGeneration/` and `Web/NSwag*` of
the unit suite. Not AOT-compatible. Floor: NJsonSchema 11.6.1, which NSwag 14.7 asks for; Confluent's serdes take 11.0.2
or later.

- `AddValueObjects()` on any `JsonSchemaGeneratorSettings` wraps `settings.ReflectionService` in
  `ValueObjectReflectionService`, which answers a value object, nullable unwrapped, as an object without members, so that
  NJsonSchema files one definition and refers to it, and adds `ValueObjectSchemaProcessor`, which fills the definition in;
  both once. Both find a value object through `TryResolve`.
- `IReflectionService` has the same seven members in 11.0.2 and 11.6.1: a member a minor adds fails the build.
- NJsonSchema applies a member's validation attributes after the processors: `[Required]` under Newtonsoft.Json settings
  adds `minLength: 1` beside the reference; a `[ParsesAs]` member is described again once its object is complete, keeping
  the stricter rules, the member declared deepest in the hierarchy read per name, a derived type's own members included,
  which NJsonSchema puts in an inline part of `allOf` no processor is handed. NSwag applies a parameter's attributes last,
  so a parameter's `[StringLength]` wins (a limit the guide states).
- A definition never says `null`, nor does an `enum` under Swagger 2.0, which takes no reference from a parameter either
  (NSwag writes `type: object` and `x-schema`): under `SchemaType.Swagger2` every value object is described in place.
- The wire is read off the settings, never changed (`ValueObjectWire`): a copy of System.Text.Json's options, made afresh
  while they are not read-only, numbers strict in a document; under Newtonsoft.Json, `ValueObjectConverter` when the
  type's or the settings' converter is it, the type converter's text for a `JsonStringContract`, left alone otherwise,
  values written by those settings themselves (through `NewtonsoftJsonWrittenConverterFactory`).
- NJsonSchema looks a type mapper up by the type the reflection service describes, then by its generic definition, before
  anything else: a nullable value object is answered unwrapped, so a mapper of the value object maps both forms, and the
  package leaves alone only the form a mapper takes (`Mapped`), a mapper of the nullable type taking it only where
  Newtonsoft.Json's service keeps that type, for a `JsonStringContract`. `[JsonSchemaType]` and `[JsonSchema]` win too.
- NJsonSchema holds `minimum` and `maximum` in a `decimal`, which refuses `2E+32` and rounds `9.1E-31` to 0: such a bound
  goes to `x-minimum` and `x-maximum`, and only a decimal is written as one (`ToToken`).
- Its validator, which Confluent runs with no format validator, compares an `enum` by the `ToString()` of a token read
  with `FloatParseHandling.Double`, so a JSON Schema lists a closed set of reals as doubles (`ToEnumerationToken`), a
  decimal no double holds listed rounded; a document lists decimals as decimals. It throws `InvalidCastException` on a
  JSON integer beyond Int64 (RicoSuter/NJsonSchema#568), which a test pins.
- NSwag describes Newtonsoft.Json's default settings for an application without MVC's System.Text.Json output formatter
  whenever `Microsoft.AspNetCore.Mvc.NewtonsoftJson` can be loaded (RicoSuter/NSwag#5413): the tests' NSwag applications
  call `AddControllers()`. NSwag throws `AmbiguousMatchException` for an MVC query model hiding a property with one of
  another type (`openapi.md`, RicoSuter/NSwag#5412), and its C# client generator writes two converters on a member whose
  closed set is of `format: date` (CS0579, pinned by `NSwagDocumentTests`, RicoSuter/NJsonSchema#1945).
- Confluent's `JsonSerializer<T>` takes a class only, and 2.16's default subject name strategy calls
  `GetAssociationsByResourceNameAsync`, which a substitute registry answers with an empty list.
- The namespace hides the root `NJsonSchema` namespace (root `CLAUDE.md`). A file importing `NJsonSchema` and
  `Confluent.SchemaRegistry` aliases `SchemaType` (CS0104).
