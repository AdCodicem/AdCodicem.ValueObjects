# AdCodicem.ValueObjects.AI — Microsoft.Extensions.AI

User-facing behaviour: `website/docs/how-to/language-models.md`. Tests: `LanguageModels/` of
`tests/AdCodicem.ValueObjects.UnitTests.Observability` (a scripted `IChatClient`, no network). The argument check is
shared with the MCP package (`src/Shared/ValueObjectArguments.cs`).

- It calls no `TryResolve`: it tells a value-object parameter by `ValueObjectRegistry.IsValueObject` and reads each
  argument through the contract the tool's serializer options hold, the one the tool's own binding reads it through.
- A refused argument throws nothing: the wrapper returns `{"error":"invalid_argument","argument","code","message"}` as the
  tool's result, which `FunctionInvokingChatClient` hands the model as it is.
- `AIFunctionFactory` binds an argument without asking the value object: it hands a C# `null` (what the OpenAI adapter
  makes of a JSON `null`) to a struct parameter as its uninitialized instance, without calling the converter, and fails an
  absent argument with an `ArgumentException` (the wrapper answers both `value_object.required`); it takes an object of
  another type through a JSON round trip of its own, text that may be JSON read as JSON first, swallowing what it throws,
  so the wrapper reads through the same round trip and its verdict is the binding's.
- System.Text.Json writes its own message, with the value's path (dictionary keys included), for a refusal the generated
  converter leaves without one (a number the underlying type cannot hold): the wrapper tells it by the path information
  at its end and answers "The value is not a valid X." instead.
- `AIParameterNameAttribute` is `[Experimental("MEAI001")]`, so `ValueObjectArguments.cs` reads it by name, which survives
  a renaming or a removal upstream; `HasDefault` there mirrors which parameters the binding fills in itself.
- The language-model JSON Schema profile keeps the case-insensitive `enum` of a closed set and a `minLength` counted in
  UTF-16 code units, which the MongoDB writer loosens: it describes what a model should send.
- Agent Framework's `RunAsync<T>` replaces the response format its run options carry with
  `ChatResponseFormat.ForJsonSchema<T>`, which no schema option reaches: the guide hands the package's format to a plain
  `RunAsync` and reads the answer with `AgentResponse<T>`, which `AgentFrameworkTests` pins.
