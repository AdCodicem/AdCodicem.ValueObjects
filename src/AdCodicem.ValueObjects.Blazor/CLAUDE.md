# AdCodicem.ValueObjects.Blazor — the input component

User-facing behaviour: `website/docs/how-to/blazor.md`. Tests: `Forms/` of the unit suite through bUnit, over the Razor
forms of `tests/Fixtures/AdCodicem.ValueObjects.Fixtures.Blazor`; `HtmlRenderer` in `tests/NativeAot` and the island.

## What the framework does before the input runs

- `@bind` reads text through `BindConverter`, which swallows the converter's `ValueObjectException` and calls the setter
  with `default` for empty text; `InputSelect` and `InputRadioGroup` catch only `InvalidOperationException`, so a refused
  option throws out of the change event, fatal to a Blazor Server circuit.
- A statically rendered form is mapped by the framework through `IParsable<T>` in the invariant culture before any
  component runs, its converters internal: a refusal is "The value 'XX' is not valid for 'Account'.", quoting the text
  with no code, the member left at its default, and empty text is `null` only for primitive types, `DateOnly`, `TimeOnly`
  and `DateTimeOffset`, and from .NET 11 `TimeSpan`, `Guid`, `Int128`, `UInt128` and `Half` (dotnet/aspnetcore#60498,
  #66625), so an empty `TSelf?` is refused on both (dotnet/aspnetcore#69901). `StaticFormTests` pins both messages.
- `InputBase` binds `null` to a nullable value object for empty text without calling `TryParseValueFromString`, and
  formats the model only while no refused text shows, which is why the input clears a code in `FormatValueAsString` too.

## What the input does

- It finds the descriptor of its `TValue`, nullable unwrapped, as `[ParsesAs(typeof(T))]` does, closes both its text
  adapters, plain and nullable, through the non-generic visitor `ValueObjectInputTexts.Closer`, and caches them per
  `TValue`. The native AOT job guards it.
- It throws nothing for refused text: `InputBase` adds the rule's message to its `ValidationMessageStore`, and the input
  keeps the code under a private key of `EditContext.Properties` (`TryGetValueObjectErrorCode`), set on a refusal and
  cleared on the next text, when it formats the model again and when it is disposed. Messages mirror `[ParsesAs]`: empty
  text a type that cannot be `null` refuses is `value_object.required` with `[Required]`'s message; a parser refusing
  without a reason is `not_parsable`, "The text could not be parsed as X."; an empty message is "The field {0} is
  invalid.". `{0}` is the input's `DisplayName`, else its field's name.
- It writes the invariant text under static rendering, and the current culture's where `RendererInfo.IsInteractive` or
  `AssignedRenderMode` is set, which is how a prerendered interactive page reports itself (`Static`, a mode assigned).
- The round-trip text of a date is Gregorian whatever the culture, while `DateOnly`, `DateTime` and `DateTimeOffset`
  parse in the culture's calendar (`2000-01-01` is 543 years earlier in `th-TH`, 621 later in `fa-IR`, unreadable in
  `ar-SA`; fifteen cultures): the input reads a date with a copy of the culture set to `new GregorianCalendar()`, kept
  for a read-only culture, in `src/Shared/ValueObjectText.cs`, which the System.CommandLine and CsvHelper packages link
  too.
- Under `SchemaAttributes="true"`, off by default since a browser checks text before the normalizer runs, it writes
  `maxlength`, `minlength` and `pattern`; the pattern through `EcmaScriptPattern`, for the `v` flag a browser compiles it
  with as `^(?:…)$`, only for a pattern each of whose top-level alternatives is anchored at both ends, with `\d`, `\w`,
  `\s` and `\p{…}` listed from .NET's tables and nothing that can match half of a surrogate pair. Its table,
  `Forms/EcmaScriptPatterns.json`, is held to .NET by the unit suite and to Node by
  `.github/scripts/ecmascript-patterns.mjs`: change both together.
- Messages: `AddValueObjectInputs(options => options.UseResource<T>())`, which registers `AddLocalization()` itself, read
  through the component's injected services in the current UI culture.

## Building it

- Reference the `Microsoft.AspNetCore.Components.Web` package rather than the shared framework, whose copy loads in a
  server application.
- On .NET 11, `InputBase` moved into Components.Web (forwarded), `ValidationMessage` writes `data-valmsg-*` and `InputText`
  an `id` this input does not, and `EditContext.Validate()` is obsolete (CS0618) for `ValidateAsync()`: the island reads
  messages off the `EditContext`, never the markup.
- The ASP.NET Core analyzers refuse a computed sequence number (ASP0006): a helper rendering one component per call takes
  literals, inside a region per call.
- Microsoft.AspNetCore.Components warns for itself in a native publish (IL2104, IL3053), which the main native
  application lists in its `aot-warnings.allowed`.
