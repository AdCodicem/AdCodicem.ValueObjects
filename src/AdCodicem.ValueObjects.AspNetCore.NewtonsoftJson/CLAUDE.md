# AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson — MVC on Newtonsoft.Json, JSON Patch

Tests: `Web/NewtonsoftJson*` and `Web/NewtonsoftJsonPatchTests.cs` of
`tests/AdCodicem.ValueObjects.UnitTests.Serialization`, `JsonPatchParity` over every sample; the compatibility island
runs MVC and JSON Patch 11.

## Input formatter

- `NewtonsoftJsonInputFormatter` answers `InputFormatterExceptionPolicy.AllExceptions` for any type but its own, under
  which MVC turns any exception a read throws into a 400: keep the subclass's `ExceptionPolicy` override returning
  `MalformedInputExceptions`.
- The setup tells the framework's formatter by its settings, a protected property a derived class reads on its own
  instances alone (CS1540), hence `[UnsafeAccessor]`.
- The `Error` handler is added to the pooled serializer in `CreateJsonSerializer(context)` and removed in
  `ReleaseJsonSerializer`, or the handler of one request would record the codes of the next.
- `KeyOf` rebuilds the framework's model state key from its error handler: carry a change to that handler upstream here.
  The parity theory of `NewtonsoftJsonInputFormatterTests` and the island show it.

## JSON Patch (`TryApplyTo`, `Patching/`)

- JSON Patch converts a value under settings of its own: `ConversionResultProvider.ConvertTo` round-trips it under fresh
  settings holding the document's resolver alone and swallows every exception; `copy` (`CopyTo`) and a dictionary key
  pass no resolver; MVC's patch formatter hands the document MVC's resolver and nothing else. So `TryApplyTo` reads each
  value itself, through `ValueObjectConverter` under `AddValueObjects()`'s settings and the document's resolver (without
  `ValueObjectContractResolver`), a dictionary key through the descriptor's `TryParse`, and does `copy` and `move` itself
  (`ValueObjectObjectAdapter`): JSON Patch's `move` adds at its target after a removal it refused.
- `ApplyTo` with an error handler goes on after a failed operation and stops at a `JsonPatchException` (an unknown `op`,
  a bad escape), reported under the operation's `path`, except a `from` it cannot read, which `ValueObjectObjectAdapter`
  reports under the `from` before throwing again, its `Log` skipping JSON Patch's own report.
- JSON Patch throws `ArgumentNullException` for a missing `path` or `from`, and `ArgumentOutOfRangeException` for `-`
  read, removed or replaced in an empty list, or tested in any (it reads index -1): `TryApplyTo` reports each, its list
  adapter answering `-` for every list.
- A refusal stays on the document's `ValueObjectPatchConversion` until the error reporting it takes its code; every
  operation starts clean.
- The adapters visit no descriptor: they tell a value object by `IsValueObject` and close the adapter of a dictionary
  keyed by or holding value objects with `MakeGenericType` (`ValueObjectAdapterFactory`, on `RuntimeClosingTests`'
  allow-list), since a visitor would name the value object, never the dictionary's other type, and MVC's JSON Patch runs
  in no native binary.
- `Patching/` derives from `Microsoft.AspNetCore.JsonPatch.Internal` types, which "may change or be removed": the island
  is where a break shows. Microsoft.AspNetCore.JsonPatch comes with Microsoft.AspNetCore.Mvc.NewtonsoftJson, at its
  version, and is pinned nowhere: keep it so.
