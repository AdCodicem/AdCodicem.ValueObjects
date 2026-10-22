# AdCodicem.ValueObjects.Grpc

User-facing behaviour: `website/docs/how-to/grpc.md`. Tests: `ContractFirstGrpc*`, `ValueObjectExceptionInterceptorTests`
and `ValueObjectBadRequestTests` in `BinarySerialization/` of `tests/AdCodicem.ValueObjects.UnitTests.Serialization`; a
contract-first service over Kestrel in `tests/NativeAot`.

- `ValueObjectExceptionInterceptor` answers a `ValueObjectException` carrying a code that escapes a handler with
  `INVALID_ARGUMENT` and a `google.rpc.ErrorInfo` written through Grpc.StatusProto (reason `ToUpperSnakeCase(code)`,
  metadata `code` and `valueObject`, none for a code that maps to no reason), as protobuf-net's `ForServer` does.
- `ValueObjectBadRequest` records every refused field of a request and throws one `INVALID_ARGUMENT` holding a
  `google.rpc.BadRequest`, a field violation's `reason` left empty for a code that maps to none.
- The namespace hides the root `Grpc` namespace (root `CLAUDE.md`): name gRPC's types through usings, or `global::Grpc`
  in a body. The code Grpc.Tools generates qualifies them through `grpc::` aliases already. A class or a file named
  `Grpc` in an `AdCodicem.ValueObjects.*` namespace would hide it too.
