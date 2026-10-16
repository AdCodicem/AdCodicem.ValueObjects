# AdCodicem.ValueObjects.MessagePack

User-facing behaviour: `website/docs/how-to/messagepack.md`. Tests: `BinarySerialization/` of the unit suite, directly and
through a SignalR hub over TestHost.

- Reads are strict unless the resolver is trusted (`WithValueObjects(trusted: true)`, `UseValueObjects(trusted: true)`),
  since the bytes may come from another service. A `nil` is refused whatever the trust. A refusal is a
  `MessagePackSerializationException`.
- MessagePack's nuspec takes `MessagePackAnalyzer` with `include="All"`, so its analyzer and source generator reach every
  project referencing this package, by project reference too: `MsgPack003` fails the build of a `[MessagePackObject]`
  holding a value object of the same assembly, which `[assembly: MessagePackAssumedFormattable(typeof(T))]` fixes, as the
  guide tells an application to. The unit suite (`BinarySerialization/MessagePackAssumptions.cs`) and the island do so.
- Not AOT-compatible, and out of `tests/NativeAot`: MessagePack 3.1 has no trimming annotation (a native publish warns
  IL2104 and IL3053 for the whole assembly), builds the formatters of a `Nullable<T>`, a collection or a dictionary by
  reflection, which fails natively for any struct, and SignalR's MessagePack hub protocol needs dynamic code.
- Under `MessagePackSecurity.UntrustedData`, which SignalR sets, MessagePack refuses a dictionary keyed by a value object
  and a set of them (`TypeAccessException`). Add no comparer: a value object's equality may differ from its value's. A
  test pins the refusal and the guide's workaround.
- The namespace hides the root `MessagePack` namespace (root `CLAUDE.md`).
