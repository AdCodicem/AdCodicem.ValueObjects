# AdCodicem.ValueObjects.AzureFunctions — the input converter

User-facing behaviour: `website/docs/how-to/azure.md`. Tests: `Functions/` of
`tests/AdCodicem.ValueObjects.UnitTests.Hosts`, over `ConverterContext`s it writes, and the real host in the integration
suite. Not AOT-compatible.

- The converter and the ASP.NET Core package's middleware parse through `ValueObjectBindingText`
  (`src/Shared/ValueObjectBindingText.cs`), so the middleware parses a parameter again exactly as the converter did.
- The worker keeps no exception of a failed conversion: `DefaultFunctionInputBindingFeature` turns each failed parameter
  into a line of its `FunctionInputConverterException`'s text, with no inner exception, so the converter's
  `FormatException` carries the code in its `Data` **and at the end of its message**.
- The worker drops a `Failed` result whose source is `null` and binds the struct's default: the converter answers
  `Unhandled` for one, leaving the worker to bind `null` or fail a required parameter itself.
- The host sends binding data of another type than `string` as JSON (`InsertionTime` as `"2026-10-13T10:13:54+00:00"`):
  `ValueObjectBindingText.TryParse` decodes a JSON string literal the type refuses as it stands. The host writes a
  message's number through a double (`0.0000001` as `1E-07`), which a decimal's `TryParse` refuses: for a value object
  over `decimal` it reads a JSON number as the generated JSON converter does (`TryGetDecimal`, then `TryCreate`); every
  other underlying type's `TryParse` reads every JSON number it holds already.
- The worker reads `[InputConverter]` off the parameter, then off its type (`Nullable<T>` for a `T?`), so the generator
  emits none and `UseValueObjects()` registers the converter at index 0 of `WorkerOptions.InputConverters` from a
  `Configure`. The ASP.NET Core integration registers its `HttpContextConverter` the same way, so the converter is first
  only when `UseValueObjects()` runs after `ConfigureFunctionsWebApplication()`, and right behind that converter
  otherwise.
- Worker.Core 2.52.0 is the floor, as Microsoft's own worker extensions take it.
- Name no test namespace `Azure`, which would hide `Azure.Core` and `Azure.Storage` in the suite's.
